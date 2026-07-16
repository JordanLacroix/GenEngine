using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenEngine.Authoring.Infrastructure;

public sealed class AuthoringDbContext(DbContextOptions<AuthoringDbContext> options) : DbContext(options)
{
    public DbSet<Scenario> Scenarios => Set<Scenario>();

    public DbSet<ScenarioVersion> ScenarioVersions => Set<ScenarioVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.ToTable("scenarios");
            entity.HasKey(static scenario => scenario.Id);
            entity.Property(static scenario => scenario.OwnerId).HasMaxLength(100).IsRequired();
            entity.Property(static scenario => scenario.Title).HasMaxLength(200).IsRequired();
            entity.Property(static scenario => scenario.DraftJson).HasColumnType("jsonb").IsRequired();
            entity.Property(static scenario => scenario.Revision).IsConcurrencyToken();
            entity.HasIndex(static scenario => scenario.OwnerId);
            entity.HasMany(static scenario => scenario.Versions)
                .WithOne()
                .HasForeignKey(static version => version.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScenarioVersion>(entity =>
        {
            entity.ToTable("scenario_versions");
            entity.HasKey(static version => version.Id);
            entity.Property(static version => version.SnapshotJson).HasColumnType("jsonb").IsRequired();
            entity.Property(static version => version.SnapshotHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(static version => new { version.ScenarioId, version.Number }).IsUnique();
            entity.HasIndex(static version => version.SnapshotHash);
        });
    }
}

internal sealed class AuthoringRepository(AuthoringDbContext dbContext) : IAuthoringRepository
{
    public async Task AddAsync(Scenario scenario, CancellationToken cancellationToken) =>
        await dbContext.Scenarios.AddAsync(scenario, cancellationToken).ConfigureAwait(false);

    public Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        dbContext.Scenarios
            .Include(static scenario => scenario.Versions)
            .SingleOrDefaultAsync(
                scenario => scenario.Id == id && scenario.OwnerId == ownerId,
                cancellationToken);

    public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        dbContext.ScenarioVersions.SingleOrDefaultAsync(version => version.Id == versionId, cancellationToken);

    public async Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken) =>
        await dbContext.ScenarioVersions.AddAsync(version, cancellationToken).ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new AuthoringException("revision_conflict", "The scenario was modified concurrently.", exception);
        }
    }
}

internal sealed class AuthoringDatabaseHealthCheck(AuthoringDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Authoring database is unavailable.");
}

public static class AuthoringInfrastructureExtensions
{
    public static IServiceCollection AddAuthoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Authoring")
            ?? "Host=localhost;Port=5432;Database=genengine_authoring;Username=postgres;Password=postgres";
        services.AddDbContext<AuthoringDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAuthoringRepository, AuthoringRepository>();
        services.AddScoped<AuthoringService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<AuthoringDatabaseHealthCheck>("authoring-database");
        return services;
    }

    public static async Task MigrateAuthoringDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AuthoringDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthoringDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static void MapAuthoringHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}