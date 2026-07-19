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
            entity.Property(static scenario => scenario.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static scenario => scenario.CreationBrief).HasMaxLength(4000).IsRequired();
            entity.Property(static scenario => scenario.Revision).IsConcurrencyToken();
            entity.HasIndex(static scenario => scenario.OwnerId);
            entity.HasIndex(static scenario => new { scenario.FrontId, scenario.CategoryId });

            // Tri de la liste « mes scénarios » et filtrage du catalogue publié.
            entity.HasIndex(static scenario => new { scenario.OwnerId, scenario.UpdatedAt });
            entity.HasIndex(static scenario => new { scenario.IsArchived, scenario.CategoryId });
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

            // Sous-requête `MAX(published_at)` du tri du catalogue publié.
            entity.HasIndex(static version => new { version.ScenarioId, version.PublishedAt });
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

    public async Task<(IReadOnlyList<PublishedScenarioRecord> Items, int Total)> ListPublishedAsync(
        Guid? categoryId,
        string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<Scenario> filtered = dbContext.Scenarios
            .AsNoTracking()
            .Where(static scenario => scenario.Versions.Count != 0 && !scenario.IsArchived)
            .Where(scenario => categoryId == null || scenario.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim()}%";
            filtered = filtered.Where(scenario => EF.Functions.ILike(scenario.Title, pattern));
        }

        int total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        // Aucun `Include` : seule la dernière version de chaque scénario est projetée, sinon
        // trier le catalogue matérialiserait toutes les versions de tous les scénarios.
        PublishedScenarioRecord[] items = await filtered
            .OrderByDescending(static scenario => scenario.Versions.Max(version => version.PublishedAt))
            .ThenByDescending(static scenario => scenario.Id)
            .Skip(offset)
            .Take(limit)
            .Select(static scenario => new PublishedScenarioRecord(
                scenario.Id,
                scenario.FrontId,
                scenario.CategoryId,
                scenario.Versions.OrderByDescending(version => version.Number).First().Id,
                scenario.Versions.OrderByDescending(version => version.Number).First().Number,
                scenario.Versions.OrderByDescending(version => version.Number).First().PublishedAt,
                scenario.Versions.OrderByDescending(version => version.Number).First().SnapshotJson,
                scenario.Versions.OrderByDescending(version => version.Number).First().SnapshotHash))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<(IReadOnlyList<ScenarioVersion> Items, int Total)> ListVersionsAsync(
        Guid scenarioId,
        string ownerId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        bool owned = await dbContext.Scenarios
            .AsNoTracking()
            .AnyAsync(scenario => scenario.Id == scenarioId && scenario.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (!owned)
        {
            throw new AuthoringException("scenario_not_found", "The scenario was not found.");
        }

        IQueryable<ScenarioVersion> filtered = dbContext.ScenarioVersions
            .AsNoTracking()
            .Where(version => version.ScenarioId == scenarioId);
        int total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        ScenarioVersion[] items = await filtered
            .OrderBy(static version => version.Number)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<(IReadOnlyList<Scenario> Items, int Total)> ListOwnedAsync(
        string ownerId,
        string? query,
        Guid? categoryId,
        bool includeArchived,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<Scenario> filtered = dbContext.Scenarios.AsNoTracking()
            .Include(static scenario => scenario.Versions)
            .Where(scenario => scenario.OwnerId == ownerId)
            .Where(scenario => includeArchived || !scenario.IsArchived)
            .Where(scenario => categoryId == null || scenario.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim()}%";
            filtered = filtered.Where(scenario => EF.Functions.ILike(scenario.Title, pattern) || EF.Functions.ILike(scenario.CreationBrief, pattern));
        }

        int total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        Scenario[] items = await filtered.OrderByDescending(static scenario => scenario.UpdatedAt)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        dbContext.ScenarioVersions.SingleOrDefaultAsync(version => version.Id == versionId, cancellationToken);

    public Task<Scenario?> GetScenarioByIdAsync(Guid scenarioId, CancellationToken cancellationToken) =>
        dbContext.Scenarios.AsNoTracking().SingleOrDefaultAsync(scenario => scenario.Id == scenarioId, cancellationToken);

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
        services.AddScenarioGeneration(configuration);
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