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
    public DbSet<OnboardingState> OnboardingStates => Set<OnboardingState>();
    public DbSet<PlayerJournalEntry> JournalEntries => Set<PlayerJournalEntry>();
    public DbSet<ScenarioMastery> ScenarioMasteries => Set<ScenarioMastery>();
    public DbSet<PlayerStatValue> StatValues => Set<PlayerStatValue>();
    public DbSet<EarnedReward> EarnedRewards => Set<EarnedReward>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("player_profiles");
            entity.HasKey(static profile => profile.Id);
            entity.Property(static profile => profile.Id).ValueGeneratedNever();
            entity.Property(static profile => profile.UserId).HasMaxLength(100).IsRequired();
            entity.Property(static profile => profile.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static profile => profile.FamiliarForm).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarTone).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarWritingStyle).HasMaxLength(120);
            entity.Property(static profile => profile.FamiliarAccent).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarCustomName).HasMaxLength(80);
            entity.Property(static profile => profile.FamiliarAxisSelectionsJson).HasColumnType("jsonb");
            entity.Property(static profile => profile.Revision).IsConcurrencyToken();
            entity.HasIndex(static profile => new { profile.UserId, profile.FrontId }).IsUnique();
            entity.HasMany(static profile => profile.WalletEntries).WithOne().HasForeignKey(static entry => entry.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.OwnedItems).WithOne().HasForeignKey(static item => item.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.OnboardingStates).WithOne().HasForeignKey(static state => state.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.JournalEntries).WithOne().HasForeignKey(static entry => entry.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.ScenarioMasteries).WithOne().HasForeignKey(static mastery => mastery.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.StatValues).WithOne().HasForeignKey(static stat => stat.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(static profile => profile.EarnedRewards).WithOne().HasForeignKey(static reward => reward.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EarnedReward>(entity =>
        {
            entity.ToTable("player_earned_rewards");

            // The composite key is the uniqueness guarantee itself: the database refuses a
            // second stamp for the same (profile, reward) even if two evaluations were to
            // race, so "earned once" does not depend on the in-memory check alone.
            entity.HasKey(static reward => new { reward.ProfileId, reward.RewardId });
        });
        modelBuilder.Entity<PlayerStatValue>(entity =>
        {
            entity.ToTable("player_stat_values");
            entity.HasKey(static stat => new { stat.ProfileId, stat.Key });
            entity.Property(static stat => stat.Key).HasMaxLength(40).IsRequired();
            entity.Property(static stat => stat.ProcessedCommandIdsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<WalletEntry>(entity =>
        {
            entity.ToTable("wallet_entries");
            entity.HasKey(static entry => entry.Id);
            entity.Property(static entry => entry.Id).ValueGeneratedNever();
            entity.Property(static entry => entry.IdempotencyKey).HasMaxLength(160).IsRequired();
            entity.Property(static entry => entry.Reason).HasMaxLength(300).IsRequired();
            entity.HasIndex(static entry => new { entry.ProfileId, entry.IdempotencyKey }).IsUnique();
        });
        modelBuilder.Entity<OwnedItem>(entity =>
        {
            entity.ToTable("owned_items");
            entity.HasKey(static item => new { item.ProfileId, item.OfferId });
        });
        modelBuilder.Entity<OnboardingState>(entity =>
        {
            entity.ToTable("onboarding_states");
            entity.HasKey(static state => new { state.ProfileId, state.TutorialId, state.Version });
            entity.Property(static state => state.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(static state => state.CompletedStepIdsJson).HasColumnType("jsonb");
            entity.Property(static state => state.ProcessedCommandIdsJson).HasColumnType("jsonb");
            entity.Property(static state => state.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<PlayerJournalEntry>(entity =>
        {
            entity.ToTable("player_journal_entries");
            entity.HasKey(static entry => entry.Id);
            entity.Property(static entry => entry.Id).ValueGeneratedNever();
            entity.Property(static entry => entry.IdempotencyKey).HasMaxLength(160).IsRequired();
            entity.Property(static entry => entry.Type).HasMaxLength(80).IsRequired();
            entity.Property(static entry => entry.Title).HasMaxLength(200).IsRequired();
            entity.Property(static entry => entry.Summary).HasMaxLength(1200).IsRequired();
            entity.Property(static entry => entry.ReferenceId).HasMaxLength(160);
            entity.HasIndex(static entry => new { entry.ProfileId, entry.IdempotencyKey }).IsUnique();
            entity.HasIndex(static entry => new { entry.ProfileId, entry.OccurredAt });

            // Filtres du journal poussés en SQL : type, parcours, catégorie et scénario.
            entity.HasIndex(static entry => new { entry.ProfileId, entry.Type, entry.OccurredAt });
            entity.HasIndex(static entry => new { entry.ProfileId, entry.JourneyId, entry.OccurredAt });
            entity.HasIndex(static entry => new { entry.ProfileId, entry.CategoryId, entry.OccurredAt });
            entity.HasIndex(static entry => new { entry.ProfileId, entry.ScenarioId, entry.OccurredAt });
        });
        modelBuilder.Entity<ScenarioMastery>(entity =>
        {
            entity.ToTable("scenario_masteries");
            entity.HasKey(static mastery => new { mastery.ProfileId, mastery.ScenarioVersionId });
            entity.Property(static mastery => mastery.ChoiceIdsJson).HasColumnType("jsonb");
            entity.Property(static mastery => mastery.NodeIdsJson).HasColumnType("jsonb");
            entity.Property(static mastery => mastery.EndingIdsJson).HasColumnType("jsonb");
            entity.Property(static mastery => mastery.SessionIdsJson).HasColumnType("jsonb");
            entity.Property(static mastery => mastery.ProcessedCommandIdsJson).HasColumnType("jsonb");
            entity.HasIndex(static mastery => new { mastery.ProfileId, mastery.ScenarioId });
        });
    }
}

internal sealed class PlayerExperienceRepository(PlayerExperienceDbContext dbContext) : IPlayerExperienceRepository
{
    public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) =>
        dbContext.Profiles
            .Include(static profile => profile.WalletEntries)
            .Include(static profile => profile.OwnedItems)
            .Include(static profile => profile.OnboardingStates)
            .Include(static profile => profile.JournalEntries)
            .Include(static profile => profile.ScenarioMasteries)
            .Include(static profile => profile.StatValues)
            .Include(static profile => profile.EarnedRewards)
            .SingleOrDefaultAsync(profile => profile.UserId == userId && profile.FrontId == frontId, cancellationToken);
    public async Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken) =>
        await dbContext.Profiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

    public async Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken) =>
        await dbContext.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.FrontId == frontId)
            .Select(static profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken)
    {
        IQueryable<PlayerJournalEntry> filtered = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.ProfileId == profileId);
        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filtered = filtered.Where(entry => EF.Functions.ILike(entry.Type, filter.Type));
        }

        if (filter.JourneyId is Guid journeyId) filtered = filtered.Where(entry => entry.JourneyId == journeyId);
        if (filter.CategoryId is Guid categoryId) filtered = filtered.Where(entry => entry.CategoryId == categoryId);
        if (filter.ScenarioId is Guid scenarioId) filtered = filtered.Where(entry => entry.ScenarioId == scenarioId);

        // Les agrégats portent sur l'ensemble filtré, jamais sur la page.
        int total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var groups = await filtered
            .GroupBy(static entry => entry.Type)
            .Select(static group => new { Type = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, int> totalsByType = new(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            totalsByType[group.Type] = totalsByType.TryGetValue(group.Type, out int existing)
                ? existing + group.Count
                : group.Count;
        }

        PlayerJournalEntry[] items = await filtered
            .OrderByDescending(static entry => entry.OccurredAt)
            .ThenByDescending(static entry => entry.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new JournalPage(items, total, totalsByType);
    }
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
                GetString(item, "description"),
                item.GetProperty("form").GetString() ?? string.Empty,
                item.GetProperty("tone").GetString() ?? string.Empty,
                item.GetProperty("writingStyle").GetString() ?? string.Empty,
                item.GetProperty("accent").GetString() ?? string.Empty,
                item.GetProperty("helpLevel").GetInt32(),
                GetStrings(item, "capabilities"),
                item.GetProperty("availableForms").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray(),
                item.GetProperty("availableTones").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray(),
                GetNullableString(item, "portraitUrl"),
                GetNullableString(item, "avatarUrl"),
                GetNullableString(item, "backgroundUrl"),
                GetNullableString(item, "license"),
                GetNullableString(item, "attribution"),
                ReadAxes(item))).ToArray();
        RewardRule[] rewards = economy.GetProperty("rewardRules").EnumerateArray().Select(static item =>
            new RewardRule(item.GetProperty("trigger").GetString() ?? string.Empty, item.GetProperty("referenceId").GetString() ?? string.Empty, item.GetProperty("amount").GetInt32(), item.GetProperty("description").GetString() ?? string.Empty)).ToArray();
        ShopOffer[] offers = economy.GetProperty("offers").EnumerateArray().Select(static item =>
            new ShopOffer(item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString() ?? string.Empty, item.GetProperty("description").GetString() ?? string.Empty, item.GetProperty("price").GetInt32(), item.GetProperty("rewardType").GetString() ?? string.Empty, item.GetProperty("rewardReference").GetString() ?? string.Empty, item.GetProperty("enabled").GetBoolean())).ToArray();
        JsonElement onboarding = document.GetProperty("onboarding");
        OnboardingTutorial tutorial = new(
            onboarding.GetProperty("id").GetGuid(),
            onboarding.GetProperty("version").GetInt32(),
            onboarding.GetProperty("enabled").GetBoolean(),
            onboarding.GetProperty("allowSkip").GetBoolean(),
            onboarding.GetProperty("requiredAfterUpgrade").GetBoolean(),
            onboarding.GetProperty("steps").EnumerateArray().Select(static step => new OnboardingStep(
                step.GetProperty("id").GetGuid(),
                GetString(step, "title"),
                GetString(step, "body"),
                GetString(step, "target"),
                GetString(step, "action"),
                step.GetProperty("order").GetInt32(),
                step.GetProperty("required").GetBoolean())).OrderBy(static step => step.Order).ToArray());
        JsonElement assistant = document.GetProperty("assistantPolicy");
        AssistantPolicy assistantPolicy = new(
            assistant.GetProperty("enabled").GetBoolean(),
            assistant.GetProperty("requireFirstRunConfiguration").GetBoolean(),
            assistant.GetProperty("proactive").GetBoolean(),
            assistant.GetProperty("warnOnKnownPath").GetBoolean(),
            assistant.GetProperty("defaultFrequency").GetInt32(),
            GetStrings(assistant, "offlineCapabilities"));
        return new PlayerExperienceCatalog(
            economy.GetProperty("currencyCode").GetString() ?? string.Empty,
            economy.GetProperty("currencyName").GetString() ?? string.Empty,
            economy.GetProperty("currencyIcon").GetString() ?? string.Empty,
            economy.GetProperty("initialBalance").GetInt32(),
            familiars,
            rewards,
            offers,
            tutorial,
            assistantPolicy,
            GetJourneys(document),
            GetCategories(document),
            ReadFinale(document),
            ReadPlayerStats(document),
            ReadRewards(document));
    }

    /// <summary>
    /// Reads the optional <c>rewards</c> block. A front published before conditional
    /// rewards existed has no such property and reports none, so nothing is shown and
    /// nothing is ever stamped.
    /// </summary>
    private static RewardsPlan ReadRewards(JsonElement document)
    {
        if (!document.TryGetProperty("rewards", out JsonElement rewards) || rewards.ValueKind != JsonValueKind.Object)
        {
            return new RewardsPlan(false, []);
        }

        return new RewardsPlan(
            !rewards.TryGetProperty("enabled", out JsonElement enabled) || enabled.ValueKind != JsonValueKind.False,
            rewards.TryGetProperty("rewards", out JsonElement items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(static item => new ConditionalRewardPlan(
                    item.GetProperty("id").GetGuid(),
                    !item.TryGetProperty("enabled", out JsonElement rewardEnabled) || rewardEnabled.ValueKind != JsonValueKind.False,
                    GetString(item, "label"),
                    GetString(item, "description"),
                    Enum.TryParse(GetString(item, "mode"), ignoreCase: true, out ProgressMode mode) ? mode : ProgressMode.All,
                    ReadConditions(item),
                    item.TryGetProperty("grants", out JsonElement grants) && grants.ValueKind == JsonValueKind.Array
                        ? grants.EnumerateArray().Select(static grant => new RewardGrantPlan(
                            GetString(grant, "type"),
                            GetString(grant, "label"),
                            GetNullableString(grant, "reference"),
                            GetNullableInt(grant, "amount"))).ToArray()
                        : [],
                    GetNullableString(item, "visualUrl"),
                    GetNullableString(item, "labelKey"))).ToArray()
                : []);
    }

    /// <summary>
    /// Reads the shared condition list of a block. An unknown condition type is mapped to
    /// <see cref="ProgressConditionKind.Unknown"/> rather than dropped, so a document
    /// published by a newer engine can never make a block <em>easier</em> to satisfy here.
    /// </summary>
    private static ProgressCondition[] ReadConditions(JsonElement owner) =>
        owner.TryGetProperty("conditions", out JsonElement conditions) && conditions.ValueKind == JsonValueKind.Array
            ? conditions.EnumerateArray().Select(static condition => new ProgressCondition(
                condition.GetProperty("id").GetGuid(),
                Enum.TryParse(GetString(condition, "type"), ignoreCase: true, out ProgressConditionKind kind) ? kind : ProgressConditionKind.Unknown,
                GetString(condition, "description"),
                GetNullableInt(condition, "threshold"),
                GetNullableGuid(condition, "categoryId"),
                GetNullableGuid(condition, "journeyId"),
                GetStrings(condition, "endingIds"),
                GetGuids(condition, "scenarioIds"),
                GetNullableString(condition, "statKey"))).ToArray()
            : [];

    /// <summary>
    /// Reads the optional <c>playerStats</c> block. A front published before player
    /// statistics existed simply has no such property and reports none, which is exactly
    /// what a client should then display: nothing.
    /// </summary>
    private static PlayerStatPlan ReadPlayerStats(JsonElement document)
    {
        if (!document.TryGetProperty("playerStats", out JsonElement stats) || stats.ValueKind != JsonValueKind.Object)
        {
            return new PlayerStatPlan(false, []);
        }

        return new PlayerStatPlan(
            !stats.TryGetProperty("enabled", out JsonElement enabled) || enabled.ValueKind != JsonValueKind.False,
            stats.TryGetProperty("stats", out JsonElement items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(static item => new PlayerStatPlanEntry(
                    item.GetProperty("id").GetGuid(),
                    GetString(item, "key"),
                    GetString(item, "label"),
                    GetString(item, "description"),
                    item.TryGetProperty("maximum", out JsonElement maximum) && maximum.ValueKind == JsonValueKind.Number ? maximum.GetInt32() : 0)).ToArray()
                : []);
    }

    private static FamiliarAxis[]? ReadAxes(JsonElement familiar)
    {
        if (!familiar.TryGetProperty("axes", out JsonElement axes) || axes.ValueKind != JsonValueKind.Array) return null;
        return axes.EnumerateArray().Select(static axis => new FamiliarAxis(
            GetString(axis, "axis"),
            GetString(axis, "label"),
            GetString(axis, "description"),
            GetString(axis, "defaultValue"),
            axis.TryGetProperty("options", out JsonElement options) && options.ValueKind == JsonValueKind.Array
                ? options.EnumerateArray().Select(static option => new FamiliarAxisOption(
                    GetString(option, "value"),
                    GetString(option, "label"),
                    GetString(option, "description"),
                    GetNullableString(option, "accentToken"),
                    GetNullableString(option, "assetReference"),
                    option.TryGetProperty("order", out JsonElement order) && order.ValueKind == JsonValueKind.Number ? order.GetInt32() : 0)).ToArray()
                : [])).ToArray();
    }

    /// <summary>
    /// Reads the optional finale block. Its conditions go through the same
    /// <see cref="ReadConditions"/> the rewards use — one published shape, one reader.
    /// </summary>
    private static FinalePlan? ReadFinale(JsonElement document)
    {
        if (!document.TryGetProperty("finale", out JsonElement finale) || finale.ValueKind != JsonValueKind.Object) return null;
        return new FinalePlan(
            finale.GetProperty("id").GetGuid(),
            finale.TryGetProperty("enabled", out JsonElement enabled) && enabled.GetBoolean(),
            GetString(finale, "title"),
            GetString(finale, "summary"),
            GetString(finale, "body"),
            Enum.TryParse(GetString(finale, "mode"), ignoreCase: true, out ProgressMode mode) ? mode : ProgressMode.All,
            ReadConditions(finale),
            GetNullableString(finale, "visualUrl"),
            GetNullableString(finale, "musicUrl"),
            GetNullableString(finale, "labelKey"));
    }

    /// <summary>
    /// Journeys and categories are read defensively: a front published before journeys
    /// existed simply has no such property, and the player experience must stay usable.
    /// </summary>
    private static JourneyCatalogEntry[] GetJourneys(JsonElement document) =>
        document.TryGetProperty("journeys", out JsonElement journeys) && journeys.ValueKind == JsonValueKind.Array
            ? journeys.EnumerateArray().Select(static item => new JourneyCatalogEntry(
                item.GetProperty("id").GetGuid(),
                GetString(item, "name"),
                GetString(item, "description"),
                GetString(item, "accent"),
                GetNullableString(item, "imageUrl"),
                item.GetProperty("order").GetInt32(),
                item.GetProperty("isVisible").GetBoolean(),
                GetGuids(item, "categoryIds"),
                GetGuids(item, "prerequisiteJourneyIds"),
                GetStrings(item, "tags"))).ToArray()
            : [];

    private static CategoryCatalogEntry[] GetCategories(JsonElement document) =>
        document.TryGetProperty("categories", out JsonElement categories) && categories.ValueKind == JsonValueKind.Array
            ? categories.EnumerateArray().Select(static item => new CategoryCatalogEntry(
                item.GetProperty("id").GetGuid(),
                GetString(item, "name"),
                GetString(item, "description"),
                GetString(item, "accent"),
                item.GetProperty("order").GetInt32(),
                item.GetProperty("isVisible").GetBoolean(),
                GetNullableString(item, "imageUrl"),
                GetGuids(item, "scenarioIds"))).ToArray()
            : [];

    private static Guid[] GetGuids(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetGuid()).ToArray()
            : [];

    private static int? GetNullableInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    private static Guid? GetNullableGuid(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out Guid parsed) ? parsed : null;

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
    private static string? GetNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string[] GetStrings(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray()
            : [];
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
        services.AddHttpClient<IScenarioHelpProvider, ScenarioHelpProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Authoring"] ?? "http://localhost:5201");
        })
        .AddStandardResilienceHandler(AssistantHelpResilience.Configure);

        // AI stays optional: without a provider registered over it, this default
        // reports itself unconfigured and help resolves offline.
        services.AddSingleton<IAssistantAiClient, OfflineAssistantAiClient>();
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