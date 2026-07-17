using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenEngine.Configuration.Infrastructure;

public sealed class ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : DbContext(options)
{
    public DbSet<ExperienceConfiguration> Configurations => Set<ExperienceConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExperienceConfiguration>(entity =>
        {
            entity.ToTable("experience_configurations");
            entity.HasKey(static configuration => configuration.Id);
            entity.Property(static configuration => configuration.FrontId).HasMaxLength(80).IsRequired();
            entity.HasIndex(static configuration => configuration.FrontId).IsUnique();
            entity.Property(static configuration => configuration.DocumentJson).HasColumnType("jsonb").IsRequired();
            entity.Property(static configuration => configuration.PublishedJson).HasColumnType("jsonb");
            entity.Property(static configuration => configuration.Revision).IsConcurrencyToken();
        });
    }
}

internal sealed class ConfigurationRepository(ConfigurationDbContext dbContext) : IConfigurationRepository
{
    public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken)
    {
        string normalized = frontId.Trim().ToLowerInvariant();
        return dbContext.Configurations.SingleOrDefaultAsync(configuration => configuration.FrontId == normalized, cancellationToken);
    }

    public async Task AddAsync(ExperienceConfiguration configuration, CancellationToken cancellationToken) =>
        await dbContext.Configurations.AddAsync(configuration, cancellationToken).ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConfigurationException("revision_conflict", "The configuration was modified concurrently.", exception);
        }
    }
}

internal sealed class ConfigurationDatabaseHealthCheck(ConfigurationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Configuration database is unavailable.");
}

public static class ConfigurationInfrastructureExtensions
{
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Configuration")
            ?? "Host=localhost;Port=5435;Database=genengine_configuration;Username=postgres;Password=postgres";
        services.AddDbContext<ConfigurationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ConfigurationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<ConfigurationDatabaseHealthCheck>("configuration-database");
        return services;
    }

    public static async Task MigrateAndSeedConfigurationDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ConfigurationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        if (!await dbContext.Configurations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            ConfigurationService service = scope.ServiceProvider.GetRequiredService<ConfigurationService>();
            ExperienceConfigurationView created = await service.UpsertAsync("default", null, ConfigurationService.CreateDefault("default"), cancellationToken).ConfigureAwait(false);
            await service.PublishAsync("default", created.Revision, cancellationToken).ConfigureAwait(false);
        }
    }

    public static void MapConfigurationHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}