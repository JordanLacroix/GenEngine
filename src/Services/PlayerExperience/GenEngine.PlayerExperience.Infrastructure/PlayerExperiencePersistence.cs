using System.Text.Json;

using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenEngine.PlayerExperience.Infrastructure;

public sealed class PlayerExperienceDbContext(DbContextOptions<PlayerExperienceDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfile> Profiles => Set<PlayerProfile>();
    public DbSet<WalletEntry> WalletEntries => Set<WalletEntry>();
    public DbSet<OwnedItem> OwnedItems => Set<OwnedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("player_profiles");
            entity.HasKey(static profile => profile.Id);
            entity.Property(static profile => profile.UserId).HasMaxLength(100).IsRequired();
            entity.Property(static profile => profile.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static profile => profile.FamiliarForm).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarTone).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarWritingStyle).HasMaxLength(120);
            entity.Property(static profile => profile.FamiliarAccent).HasMaxLength(80);
            entity.Property(static profile => profile.Revision).IsConcurrencyToken();
            entity.HasIndex(static profile => new { profile.UserId, profile.FrontId }).IsUnique();
            entity.HasMany(static profile => profile.WalletEntries).WithOne().HasForeignKey(static entry => entry.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.OwnedItems).WithOne().HasForeignKey(static item => item.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<WalletEntry>(entity =>
        {
            entity.ToTable("wallet_entries");
            entity.HasKey(static entry => entry.Id);
            entity.Property(static entry => entry.IdempotencyKey).HasMaxLength(160).IsRequired();
            entity.Property(static entry => entry.Reason).HasMaxLength(300).IsRequired();
            entity.HasIndex(static entry => new { entry.ProfileId, entry.IdempotencyKey }).IsUnique();
        });
        modelBuilder.Entity<OwnedItem>(entity =>
        {
            entity.ToTable("owned_items");
            entity.HasKey(static item => new { item.ProfileId, item.OfferId });
        });
    }
}

internal sealed class PlayerExperienceRepository(PlayerExperienceDbContext dbContext) : IPlayerExperienceRepository
{
    public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) =>
        dbContext.Profiles.Include(static profile => profile.WalletEntries).Include(static profile => profile.OwnedItems)
            .SingleOrDefaultAsync(profile => profile.UserId == userId && profile.FrontId == frontId, cancellationToken);
    public async Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken) =>
        await dbContext.Profiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException exception) { throw new PlayerExperienceException("revision_conflict", "The player profile was modified concurrently.", exception); }
    }
}

internal sealed class ConfigurationCatalogProvider(HttpClient httpClient) : IPlayerExperienceCatalogProvider
{
    public async Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync($"/experience/{Uri.EscapeDataString(frontId)}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new PlayerExperienceException("catalog_unavailable", "The published experience catalog is unavailable.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement document = json.RootElement.GetProperty("document");
        JsonElement economy = document.GetProperty("economy");
        FamiliarOption[] familiars = document.GetProperty("familiars").EnumerateArray().Select(static item =>
            new FamiliarOption(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("name").GetString() ?? string.Empty,
                item.GetProperty("form").GetString() ?? string.Empty,
                item.GetProperty("tone").GetString() ?? string.Empty,
                item.GetProperty("writingStyle").GetString() ?? string.Empty,
                item.GetProperty("accent").GetString() ?? string.Empty,
                item.GetProperty("helpLevel").GetInt32(),
                item.GetProperty("availableForms").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray(),
                item.GetProperty("availableTones").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray())).ToArray();
        RewardRule[] rewards = economy.GetProperty("rewardRules").EnumerateArray().Select(static item =>
            new RewardRule(item.GetProperty("trigger").GetString() ?? string.Empty, item.GetProperty("referenceId").GetString() ?? string.Empty, item.GetProperty("amount").GetInt32(), item.GetProperty("description").GetString() ?? string.Empty)).ToArray();
        ShopOffer[] offers = economy.GetProperty("offers").EnumerateArray().Select(static item =>
            new ShopOffer(item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString() ?? string.Empty, item.GetProperty("description").GetString() ?? string.Empty, item.GetProperty("price").GetInt32(), item.GetProperty("rewardType").GetString() ?? string.Empty, item.GetProperty("rewardReference").GetString() ?? string.Empty, item.GetProperty("enabled").GetBoolean())).ToArray();
        return new PlayerExperienceCatalog(
            economy.GetProperty("currencyCode").GetString() ?? string.Empty,
            economy.GetProperty("currencyName").GetString() ?? string.Empty,
            economy.GetProperty("currencyIcon").GetString() ?? string.Empty,
            economy.GetProperty("initialBalance").GetInt32(),
            familiars,
            rewards,
            offers);
    }
}

internal sealed class PlayerExperienceDatabaseHealthCheck(PlayerExperienceDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Player experience database is unavailable.");
}

public static class PlayerExperienceInfrastructureExtensions
{
    public static IServiceCollection AddPlayerExperienceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("PlayerExperience") ?? "Host=localhost;Port=5436;Database=genengine_player_experience;Username=postgres;Password=postgres";
        services.AddDbContext<PlayerExperienceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPlayerExperienceRepository, PlayerExperienceRepository>();
        string baseUrl = configuration["Configuration:BaseUrl"] ?? "http://localhost:5204";
        services.AddHttpClient<IPlayerExperienceCatalogProvider, ConfigurationCatalogProvider>(client => client.BaseAddress = new Uri(baseUrl));
        services.AddScoped<PlayerExperienceService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<PlayerExperienceDatabaseHealthCheck>("player-experience-database");
        return services;
    }

    public static async Task MigratePlayerExperienceDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PlayerExperienceDbContext dbContext = scope.ServiceProvider.GetRequiredService<PlayerExperienceDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static void MapPlayerExperienceHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}