using System.Net;
using System.Net.Http.Json;

using GenEngine.Play.Application;
using GenEngine.Play.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenEngine.Play.Infrastructure;

public sealed class PlayDbContext(DbContextOptions<PlayDbContext> options) : DbContext(options)
{
    public DbSet<GameSession> Sessions => Set<GameSession>();

    public DbSet<ProcessedCommand> ProcessedCommands => Set<ProcessedCommand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.ToTable("game_sessions");
            entity.HasKey(static session => session.Id);
            entity.Property(static session => session.OwnerId).HasMaxLength(100).IsRequired();
            entity.Property(static session => session.SnapshotHash).HasMaxLength(64).IsRequired();
            entity.Property(static session => session.SnapshotJson).HasColumnType("jsonb").IsRequired();
            entity.Property(static session => session.StateJson).HasColumnType("jsonb").IsRequired();
            entity.Property(static session => session.Status).HasMaxLength(30).IsRequired();
            entity.Property(static session => session.Revision).IsConcurrencyToken();
            entity.HasIndex(static session => session.OwnerId);
            entity.HasIndex(static session => session.ScenarioVersionId);
        });

        modelBuilder.Entity<ProcessedCommand>(entity =>
        {
            entity.ToTable("processed_commands");
            entity.HasKey(static command => new { command.SessionId, command.CommandId });
            entity.Property(static command => command.ResponseJson).HasColumnType("jsonb").IsRequired();
            entity.HasOne<GameSession>()
                .WithMany()
                .HasForeignKey(static command => command.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class PlayRepository(PlayDbContext dbContext) : IPlayRepository
{
    public async Task AddAsync(GameSession session, CancellationToken cancellationToken) =>
        await dbContext.Sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

    public Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        dbContext.Sessions.SingleOrDefaultAsync(
            session => session.Id == id && session.OwnerId == ownerId,
            cancellationToken);

    public Task<ProcessedCommand?> GetProcessedCommandAsync(
        Guid sessionId,
        Guid commandId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessedCommands.SingleOrDefaultAsync(
            command => command.SessionId == sessionId && command.CommandId == commandId,
            cancellationToken);

    public async Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken) =>
        await dbContext.ProcessedCommands.AddAsync(command, cancellationToken).ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PlayException("revision_conflict", "The session was modified concurrently.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains(
            "processed_commands",
            StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new PlayException("command_conflict", "The command is already being processed.", exception);
        }
    }
}

internal sealed class AuthoringSnapshotClient(
    HttpClient httpClient,
    IConfiguration configuration) : IAuthoringSnapshotClient
{
    public async Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/internal/scenario-versions/{versionId}");
        request.Headers.Add("X-Internal-Key", configuration["InternalApi:Key"] ?? string.Empty);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            throw new PlayException("version_not_found", "The published scenario version was not found.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PublishedSnapshotContract>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new PlayException("invalid_authoring_response", "Authoring returned an empty response.");
    }
}

internal sealed class PlayDatabaseHealthCheck(PlayDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Play database is unavailable.");
}

public static class PlayInfrastructureExtensions
{
    public static IServiceCollection AddPlayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Play")
            ?? "Host=localhost;Port=5433;Database=genengine_play;Username=postgres;Password=postgres";
        services.AddDbContext<PlayDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPlayRepository, PlayRepository>();
        services.AddScoped<PlayService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IAuthoringSnapshotClient, AuthoringSnapshotClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Authoring"] ?? "http://localhost:5201");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHealthChecks().AddCheck<PlayDatabaseHealthCheck>("play-database");
        return services;
    }

    public static async Task MigratePlayDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PlayDbContext dbContext = scope.ServiceProvider.GetRequiredService<PlayDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static void MapPlayHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}