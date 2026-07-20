using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Domain;

namespace GenEngine.Configuration.Application;

public enum OrganizationType { School, Company, TrainingProvider, Community, Custom }
public enum AuthenticationMode { LocalOnly, EntraOnly, Cumulative }
public enum AiProviderType { Offline, AzureAiFoundry }
public enum IntroDisplayPolicy { EveryLaunch, OncePerVersion, FirstInstall }

public sealed record GameDefinition(string Name, string Description, string GlobalStory, string Locale, string TimeZone);
public sealed record GameLanguageDefinition(IReadOnlyDictionary<string, string> Labels);
public sealed record OrganizationUnitDefinition(Guid Id, Guid? ParentId, string Type, string Name, string Code, string Description, int Order, bool Enabled);
public sealed record OrganizationDefinition(string Name, string Description, IReadOnlyList<OrganizationUnitDefinition> Units);
public sealed record AuthenticationDefinition(AuthenticationMode Mode, bool LocalEnabled, bool EntraEnabled, string? EntraTenantId, string? EntraClientId);
public sealed record AiProviderDefinition(Guid Id, string Name, AiProviderType Type, bool Enabled, string Endpoint, string Deployment, string Authentication, string? SecretReference, IReadOnlyList<string> Capabilities);
public sealed record CategoryDefinition(
    Guid Id,
    string Name,
    string Description,
    string Accent,
    int Order,
    bool IsVisible,
    string? ImageUrl = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<Guid>? ScenarioIds = null);
public sealed record JourneyDefinition(
    Guid Id,
    string Name,
    string Description,
    string Accent,
    string? ImageUrl,
    int Order,
    bool IsVisible,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyList<Guid> PrerequisiteJourneyIds,
    IReadOnlyList<string> Tags);
public sealed record CatalogAssignmentDefinition(
    Guid Id,
    Guid OrganizationUnitId,
    string ContentType,
    Guid ContentId,
    string Name,
    bool Required,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? DueAt);
public sealed record FamiliarDefinition(
    Guid Id,
    string Name,
    string Description,
    string Form,
    string WritingStyle,
    string Tone,
    string Accent,
    int HelpLevel,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> AvailableForms,
    IReadOnlyList<string> AvailableTones,
    string? PortraitUrl = null,
    string? AvatarUrl = null,
    string? BackgroundUrl = null,
    string? License = null,
    string? Attribution = null,
    IReadOnlyList<FamiliarAxisDefinition>? Axes = null);
public sealed record RewardRuleDefinition(string Trigger, string ReferenceId, int Amount, string Description);
public sealed record OfferDefinition(Guid Id, string Name, string Description, int Price, string RewardType, string RewardReference, bool Enabled);
public sealed record EconomyDefinition(string CurrencyCode, string CurrencyName, string CurrencyIcon, int InitialBalance, IReadOnlyList<RewardRuleDefinition> RewardRules, IReadOnlyList<OfferDefinition> Offers);
public sealed record ModuleDefinition(string Id, string Name, string Description, bool Enabled, IReadOnlyList<string> RequiredPermissions);
public sealed record IntroSceneDefinition(Guid Id, string Eyebrow, string Title, string Body, string? ImageUrl, int Order);
public sealed record IntroDefinition(bool Enabled, IntroDisplayPolicy DisplayPolicy, bool AllowSkip, int MinimumDisplaySeconds, IReadOnlyList<IntroSceneDefinition> Scenes);
public sealed record NavigationItemDefinition(string Destination, string LabelKey, string Icon, int Order, bool Enabled, string? RequiredModule = null);
public sealed record PlayerShellDefinition(IReadOnlyList<NavigationItemDefinition> Navigation);
public sealed record DemoExperienceDefinition(bool Enabled, string ScenarioSlug, int TargetMinutes, Guid? FamiliarId, string CallToActionLabelKey);
public sealed record HelpArticleDefinition(Guid Id, string Slug, string Title, string Summary, string Body, IReadOnlyList<string> Contexts, IReadOnlyList<string> Tags, int Order, bool Published);
public sealed record GlossaryEntryDefinition(string Term, string Definition);
public sealed record HelpCenterDefinition(bool Enabled, IReadOnlyList<HelpArticleDefinition> Articles, IReadOnlyList<GlossaryEntryDefinition> Glossary);
public sealed record OnboardingStepDefinition(Guid Id, string Title, string Body, string Target, string Action, int Order, bool Required);
public sealed record OnboardingDefinition(Guid Id, int Version, bool Enabled, bool AllowSkip, bool RequiredAfterUpgrade, IReadOnlyList<OnboardingStepDefinition> Steps);
public sealed record AssistantPolicyDefinition(bool Enabled, bool RequireFirstRunConfiguration, bool Proactive, bool WarnOnKnownPath, int DefaultFrequency, IReadOnlyList<string> OfflineCapabilities);
public sealed record JournalPolicyDefinition(bool Enabled, bool AllowExport, int RetentionDays, bool ShowStoryTimeline);

/// <summary>
/// Ambience of an application location (home, map, player, journal, familiar…).
/// Every asset is optional: a location without media stays fully usable, and the
/// audio never carries information on its own.
/// </summary>
public sealed record AppLocationMediaDefinition(
    string Location,
    string? AmbienceUrl = null,
    string? MusicUrl = null,
    string? BackgroundUrl = null,
    string? BackgroundDescription = null,
    int? Bpm = null,
    bool Loop = true);

/// <summary>Music and visual played when a run ends on a failure.</summary>
public sealed record GameOverMediaDefinition(
    string? MusicUrl = null,
    string? VisualUrl = null,
    string? VisualDescription = null,
    string? LabelKey = null);

/// <summary>
/// Per-instance media policy. <see cref="Enabled"/> lets an operator turn every
/// client-side media off, and <see cref="DefaultMuted"/> keeps sound opt-in.
/// </summary>
public sealed record MediaDefinition(
    bool Enabled,
    bool DefaultMuted,
    IReadOnlyList<AppLocationMediaDefinition> Locations,
    GameOverMediaDefinition? GameOver);

public enum BrandingColorScheme { Dark, Light, Auto }

/// <summary>
/// Named colour set of the instance. <see cref="Colors"/> must carry the eight
/// documented tokens so a client never has to invent a fallback, and may carry
/// extra ones a given client understands.
/// </summary>
public sealed record BrandingThemeDefinition(
    IReadOnlyDictionary<string, string> Colors,
    BrandingColorScheme ColorScheme = BrandingColorScheme.Auto,
    int CornerRadius = 12,
    string? FontFamily = null);

/// <summary>
/// Optional and purely additive brand block. A configuration without it stays
/// valid, publishable and readable exactly as before; clients then fall back to
/// their own defaults ("GenEngine" for the application name).
/// <para>
/// <see cref="AccentPalette"/> maps the named accent tokens already carried by
/// <see cref="CategoryDefinition.Accent"/>, <see cref="JourneyDefinition.Accent"/>
/// and <see cref="FamiliarDefinition.Accent"/> onto real colours, which is what
/// finally makes those accents renderable.
/// </para>
/// </summary>
public sealed record BrandingDefinition(
    string? ApplicationName = null,
    string? ShortName = null,
    string? Tagline = null,
    string? BrandIconUrl = null,
    string? ClientIconUrl = null,
    string? LogoUrl = null,
    string? FaviconUrl = null,
    BrandingThemeDefinition? Theme = null,
    IReadOnlyDictionary<string, string>? AccentPalette = null);

public sealed record ExperienceDocument(
    string FrontId,
    OrganizationType OrganizationType,
    OrganizationDefinition? Organization,
    GameDefinition Game,
    GameLanguageDefinition? Language,
    AuthenticationDefinition Authentication,
    IReadOnlyList<AiProviderDefinition> AiProviders,
    IReadOnlyList<CategoryDefinition> Categories,
    IReadOnlyList<FamiliarDefinition> Familiars,
    EconomyDefinition Economy,
    IReadOnlyList<ModuleDefinition> Modules,
    IReadOnlyList<JourneyDefinition>? Journeys = null,
    IReadOnlyList<CatalogAssignmentDefinition>? Assignments = null,
    IntroDefinition? Intro = null,
    PlayerShellDefinition? PlayerShell = null,
    DemoExperienceDefinition? Demo = null,
    HelpCenterDefinition? Help = null,
    OnboardingDefinition? Onboarding = null,
    AssistantPolicyDefinition? AssistantPolicy = null,
    JournalPolicyDefinition? Journal = null,
    MediaDefinition? Media = null,
    BrandingDefinition? Branding = null,
    FinaleDefinition? Finale = null,
    PlayerStatsDefinition? PlayerStats = null,
    RewardsDefinition? Rewards = null);

/// <summary>
/// Stable identifiers of the Diapason reference configuration.
/// They must stay in sync with <c>content/diapason/manifest.json</c>, which carries the playable
/// scenarios and their category/journey mapping. See <c>specs/domain/diapason/</c>.
/// </summary>
public static class DiapasonIds
{
    public const string DemoScenarioSlug = "la-note-de-service";

    public static Guid Lucidite { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e01");
    public static Guid Discernement { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e02");
    public static Guid Arbitrage { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e03");
    public static Guid Courage { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e04");
    public static Guid Transmission { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e05");
    public static Guid Autonomie { get; } = Guid.Parse("2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e06");

    public static Guid PremierAccord { get; } = Guid.Parse("7d4c2a10-1b3e-4f52-8a6d-9c0e2f4a6b01");
    public static Guid ChaineDeDecision { get; } = Guid.Parse("7d4c2a10-1b3e-4f52-8a6d-9c0e2f4a6b02");
    public static Guid CeQuiReste { get; } = Guid.Parse("7d4c2a10-1b3e-4f52-8a6d-9c0e2f4a6b03");
}

public sealed record JourneyCategoryView(Guid Id, string Name, int Order, bool IsVisible, int ScenarioCount);
public sealed record JourneyPrerequisiteView(Guid Id, string Name, bool IsVisible);
public sealed record JourneyAdminView(
    Guid Id,
    string Name,
    string Description,
    string Accent,
    string? ImageUrl,
    int Order,
    bool IsVisible,
    IReadOnlyList<string> Tags,
    IReadOnlyList<JourneyCategoryView> Categories,
    IReadOnlyList<JourneyPrerequisiteView> Prerequisites,
    int ScenarioCount);

/// <summary>
/// Read-only administration view of the journey catalog. Journeys stay edited through
/// the configuration document: a second write path would race the Studio and the
/// Administration on the same optimistic revision. See <c>specs/api/http.md</c>.
/// </summary>
public sealed record JourneyCatalogView(string FrontId, int Revision, int PublishedVersion, IReadOnlyList<JourneyAdminView> Journeys);

public sealed record ExperienceConfigurationView(Guid Id, int Revision, int PublishedVersion, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, ExperienceDocument Document);
public sealed record PublishedExperienceView(int Version, DateTimeOffset PublishedAt, ExperienceDocument Document);

/// <summary>
/// Strictly minimal, non-sensitive payload for a client that boots before it
/// holds any credential. It deliberately carries no catalog, no organization,
/// no assignment, no AI provider, no economy and no module: everything a client
/// needs to paint its first screen and offer a way in, and nothing more.
/// <para>
/// Only the authentication <em>mode</em> is exposed. The Entra authority, tenant
/// and client identifiers are published by Identity on <c>GET /auth/providers</c>,
/// which stays the single source for an OIDC bootstrap.
/// </para>
/// </summary>
public sealed record ClientBootstrapView(
    string FrontId,
    int Version,
    DateTimeOffset PublishedAt,
    string ApplicationName,
    string? ShortName,
    string? Tagline,
    BrandingDefinition? Branding,
    string Locale,
    string TimeZone,
    IReadOnlyDictionary<string, string> Labels,
    AuthenticationMode AuthenticationMode,
    bool DemoEnabled,
    IntroDefinition? Intro);

public interface IConfigurationRepository
{
    Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken);
    Task AddAsync(ExperienceConfiguration configuration, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConfigurationService(IConfigurationRepository repository, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<ExperienceConfigurationView> GetAdminAsync(string frontId, CancellationToken cancellationToken) =>
        Map(await GetRequiredAsync(frontId, cancellationToken).ConfigureAwait(false));

    public async Task<PublishedExperienceView> GetPublishedAsync(string frontId, CancellationToken cancellationToken)
    {
        ExperienceConfiguration configuration = await GetRequiredAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (configuration.PublishedJson is null || configuration.PublishedAt is null)
        {
            throw new ConfigurationException("configuration_not_published", "The front configuration is not published.");
        }

        ExperienceDocument document = Deserialize(configuration.PublishedJson);
        return new PublishedExperienceView(configuration.PublishedVersion, configuration.PublishedAt.Value, Anonymize(document));
    }

    /// <summary>
    /// Anonymous projection of a published document, served by <c>GET /experience/{frontId}</c>.
    /// <para>
    /// The route is reachable without a token, so it must not carry anything an
    /// operator would not print on a poster. Four families are removed here:
    /// the Entra tenant and client identifiers (Identity publishes what a client
    /// legitimately needs on <c>GET /auth/providers</c>), the AI provider
    /// endpoint, credential scheme and secret reference, the organization
    /// structure, and the catalog assignments — the last two describe internal
    /// units, cohorts and deadlines.
    /// </para>
    /// <para>
    /// A caller holding <c>config.read</c> keeps the complete document, secret
    /// reference included, through <c>GET /admin/configuration/{frontId}</c>.
    /// </para>
    /// </summary>
    private static ExperienceDocument Anonymize(ExperienceDocument document) => document with
    {
        // Emptied rather than nulled. The goal is to withhold the unit tree, and
        // an empty organization does that just as well — while a null breaks the
        // iOS client, whose `organization` is a non-optional property and whose
        // decoder therefore fails the whole document, not just this field.
        Organization = document.Organization is null
            ? null
            : new OrganizationDefinition(document.Organization.Name, string.Empty, []),
        Assignments = [],
        Authentication = document.Authentication with { EntraTenantId = null, EntraClientId = null },
        AiProviders = document.AiProviders
            .Select(static provider => provider with
            {
                Endpoint = string.Empty,
                Authentication = string.Empty,
                SecretReference = null,
            })
            .ToArray(),
    };

    /// <summary>
    /// Anonymous bootstrap payload. See <see cref="ClientBootstrapView"/> for the
    /// exclusion rules; this method is the only place that decides what a client
    /// may read before it authenticates.
    /// </summary>
    public async Task<ClientBootstrapView> GetClientBootstrapAsync(string frontId, CancellationToken cancellationToken)
    {
        ExperienceConfiguration configuration = await GetRequiredAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (configuration.PublishedJson is null || configuration.PublishedAt is null)
        {
            throw new ConfigurationException("configuration_not_published", "The front configuration is not published.");
        }

        ExperienceDocument document = Deserialize(configuration.PublishedJson);
        BrandingDefinition? branding = document.Branding;
        return new ClientBootstrapView(
            document.FrontId,
            configuration.PublishedVersion,
            configuration.PublishedAt.Value,
            string.IsNullOrWhiteSpace(branding?.ApplicationName) ? document.Game.Name : branding.ApplicationName,
            branding?.ShortName,
            branding?.Tagline ?? document.Game.Description,
            branding,
            document.Game.Locale,
            document.Game.TimeZone,
            document.Language?.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal),
            document.Authentication.Mode,
            document.Demo?.Enabled ?? false,
            document.Intro);
    }

    /// <summary>
    /// Journey catalog of a front, including the journeys hidden from players, with the
    /// categories and prerequisites resolved to their names. Guarded by
    /// <c>journey.manage</c>: it is the operator view of the catalog, not the player one.
    /// </summary>
    public async Task<JourneyCatalogView> GetJourneyCatalogAsync(string frontId, CancellationToken cancellationToken)
    {
        ExperienceConfiguration configuration = await GetRequiredAsync(frontId, cancellationToken).ConfigureAwait(false);
        ExperienceDocument document = Deserialize(configuration.DocumentJson);
        IReadOnlyList<JourneyDefinition> journeys = document.Journeys ?? [];
        Dictionary<Guid, CategoryDefinition> categories = document.Categories.ToDictionary(static category => category.Id);
        Dictionary<Guid, JourneyDefinition> byId = journeys.ToDictionary(static journey => journey.Id);
        JourneyAdminView[] views = journeys.OrderBy(static journey => journey.Order).Select(journey =>
        {
            JourneyCategoryView[] journeyCategories = journey.CategoryIds
                .Where(categories.ContainsKey)
                .Select(categoryId => categories[categoryId])
                .OrderBy(static category => category.Order)
                .Select(static category => new JourneyCategoryView(category.Id, category.Name, category.Order, category.IsVisible, (category.ScenarioIds ?? []).Count))
                .ToArray();
            return new JourneyAdminView(
                journey.Id,
                journey.Name,
                journey.Description,
                journey.Accent,
                journey.ImageUrl,
                journey.Order,
                journey.IsVisible,
                journey.Tags,
                journeyCategories,
                journey.PrerequisiteJourneyIds
                    .Where(byId.ContainsKey)
                    .Select(prerequisiteId => new JourneyPrerequisiteView(prerequisiteId, byId[prerequisiteId].Name, byId[prerequisiteId].IsVisible))
                    .ToArray(),
                journey.CategoryIds
                    .Where(categories.ContainsKey)
                    .SelectMany(categoryId => categories[categoryId].ScenarioIds ?? [])
                    .Distinct()
                    .Count());
        }).ToArray();
        return new JourneyCatalogView(document.FrontId, configuration.Revision, configuration.PublishedVersion, views);
    }

    public async Task<ExperienceConfigurationView> UpsertAsync(string frontId, int? expectedRevision, ExperienceDocument document, CancellationToken cancellationToken)
    {
        document = Normalize(document);
        Validate(frontId, document);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        ExperienceConfiguration? configuration = await repository.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            if (expectedRevision is not null)
            {
                throw new ConfigurationException("configuration_not_found", "The front configuration was not found.");
            }

            configuration = ExperienceConfiguration.Create(frontId, json, timeProvider.GetUtcNow());
            await repository.AddAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            configuration.Update(json, expectedRevision ?? 0, timeProvider.GetUtcNow());
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(configuration);
    }

    public async Task<ExperienceConfigurationView> PublishAsync(string frontId, int expectedRevision, CancellationToken cancellationToken)
    {
        ExperienceConfiguration configuration = await GetRequiredAsync(frontId, cancellationToken).ConfigureAwait(false);
        _ = Deserialize(configuration.DocumentJson);
        configuration.Publish(expectedRevision, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(configuration);
    }

    public static ExperienceDocument CreateDefault(string frontId) => new(
        frontId,
        OrganizationType.Custom,
        new OrganizationDefinition("Organisation principale", "Structure configurable du jeu.",
        [
            new OrganizationUnitDefinition(Guid.Parse("efc447ef-fdd6-42e6-b3d8-5de6841d9bce"), null, "Organization", "Structure principale", "ROOT", "Racine des classes, équipes ou groupes.", 1, true),
        ]),
        new GameDefinition("Le Diapason", "Dix situations professionnelles de 2026 où il faut décider sans pouvoir tout vérifier.", "Les systèmes d'intelligence artificielle décident partout, vite, et de façon plausible. Étudiant ingénieur en alternance, vous êtes souvent la seule personne à détenir le fait qui manque. Chaque scénario vous demande ce que vous en faites.", "fr-FR", "Europe/Paris"),
        new GameLanguageDefinition(CreateDefaultLabels()),
        new AuthenticationDefinition(AuthenticationMode.LocalOnly, true, false, null, null),
        [
            new AiProviderDefinition(Guid.Parse("3e6f6554-9b55-49ca-bd24-19e2f57e672a"), "Hors ligne", AiProviderType.Offline, true, string.Empty, "deterministic", "None", null, ["chat", "scenario-generation"]),
            new AiProviderDefinition(Guid.Parse("43b164f2-5a7d-48c0-b5c6-0dd7a3d44ea4"), "Azure AI Foundry", AiProviderType.AzureAiFoundry, false, "https://resource.openai.azure.com/openai/v1/", "gpt-4.1-mini", "EntraId", "env:GENENGINE_AI_AZURE_FOUNDRY_KEY", ["chat", "scenario-generation", "input-analysis"]),
        ],
        [
            new CategoryDefinition(DiapasonIds.Lucidite, "Lucidité", "Voir ce qui est réellement là avant d'interpréter.", "encre", 1, true),
            new CategoryDefinition(DiapasonIds.Discernement, "Discernement", "Trier ce qui compte quand tout est plausible.", "azur", 2, true),
            new CategoryDefinition(DiapasonIds.Arbitrage, "Arbitrage", "Décider sous contrainte et assumer ce qu'on perd.", "or", 3, true),
            new CategoryDefinition(DiapasonIds.Courage, "Courage", "Parler, refuser ou signaler quand c'est coûteux.", "cuivre", 4, true),
            new CategoryDefinition(DiapasonIds.Transmission, "Transmission", "Rendre son raisonnement utilisable par d'autres.", "sauge", 5, true),
            new CategoryDefinition(DiapasonIds.Autonomie, "Autonomie", "Garder une compétence qu'on pourrait déléguer.", "aube", 6, true),
        ],
        [
            // The axis catalogue is left implicit: Normalize expands it from the forms
            // and tones below plus the built-in axes, which is exactly the path an
            // instance configured before the axes existed takes.
            new FamiliarDefinition(Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "Tierce", "Une voix qui ne répond jamais à votre place : elle demande sur quoi vous vous appuyez.", "tuning-fork", "Socratic", "Warm", "amber", 2, ["hint", "recap", "rephrase"], ["tuning-fork", "spark", "echo", "owl", "fox"], ["Warm", "Playful", "Direct", "Mysterious", "Neutral"], "diapason-core:familiar.portrait.tuning-fork", null, null, null, null),
        ],
        new EconomyDefinition("ACCORD", "Accords", "♪", 0,
            [
                new RewardRuleDefinition("ScenarioCompleted", "*", 25, "Terminer un scénario"),
                new RewardRuleDefinition("RewardGranted", "frequence-du-doute", 15, "Avoir suspendu une conclusion trop fluide"),
                new RewardRuleDefinition("RewardGranted", "frequence-des-biais", 15, "Avoir identifié ce qu'un système mesure réellement"),
            ],
            [
                new OfferDefinition(Guid.Parse("370b6f82-a264-45cc-a0d0-2d71e58be15e"), "Sourdine de cuivre", "Une apparence rare pour le familier.", 80, "FamiliarCosmetic", "copper-mute", true),
            ]),
        [
            new ModuleDefinition("play", "Jouer", "Accéder aux histoires publiées.", true, ["session.play"]),
            new ModuleDefinition("studio", "Studio", "Créer, générer et publier des scénarios.", true, ["scenario.author"]),
            new ModuleDefinition("administration", "Administration", "Configurer le jeu, les accès et les providers.", true, ["config.read"]),
            new ModuleDefinition("shop", "Magasin", "Dépenser les monnaies narratives.", true, ["shop.read"]),
        ],
        [
            new JourneyDefinition(
                DiapasonIds.PremierAccord,
                "Le premier accord",
                "Établir les faits avant de les interpréter, et distinguer ce qu'un système mesure de ce qu'il prétend constater.",
                "encre",
                null,
                1,
                true,
                [DiapasonIds.Lucidite, DiapasonIds.Discernement],
                [],
                ["provenance", "preuve"]),
            new JourneyDefinition(
                DiapasonIds.ChaineDeDecision,
                "La chaîne de décision",
                "Décider sous contrainte, assumer ce qu'on perd, et parler au bon moment dans la bonne forme.",
                "or",
                null,
                2,
                true,
                [DiapasonIds.Arbitrage, DiapasonIds.Courage],
                [DiapasonIds.PremierAccord],
                ["arbitrage", "alerte"]),
            new JourneyDefinition(
                DiapasonIds.CeQuiReste,
                "Ce qui reste après toi",
                "Écrire ce qui doit être vrai, et garder les compétences qu'on pourrait déléguer.",
                "sauge",
                null,
                3,
                true,
                [DiapasonIds.Transmission, DiapasonIds.Autonomie],
                [DiapasonIds.ChaineDeDecision],
                ["spécification", "autonomie"]),
        ],
        [],
        CreateDefaultIntro(),
        CreateDefaultPlayerShell(),
        new DemoExperienceDefinition(true, DiapasonIds.DemoScenarioSlug, 18, Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "demo.createAccount"),
        CreateDefaultHelp(),
        CreateDefaultOnboarding(),
        CreateDefaultAssistantPolicy(),
        new JournalPolicyDefinition(true, true, 0, true),
        CreateDefaultMedia(),
        CreateDiapasonBranding(),
        FinaleCatalog.CreateDiapasonDefault(),
        PlayerStatCatalog.CreateDiapasonDefault(),
        RewardCatalog.CreateDiapasonDefault());

    /// <summary>
    /// Branding of the Diapason reference configuration. The colours come from
    /// the art direction recorded in <c>specs/domain/diapason/README.md</c> and
    /// from the <c>palette</c> block of <c>assets/diapason/asset-manifest.json</c>.
    /// <para>
    /// The brand and client icons are pack references resolved by the client
    /// through the shipped manifest: <c>brand.icon</c> is the tuning-fork motif of
    /// the product, <c>client.icon</c> a deliberately fictional demonstration
    /// glyph. Both assets ship in <c>diapason-core</c>, so the references resolve.
    /// The logo and favicon stay null: the pack ships no such asset, and a
    /// reference that does not resolve would be worse than none.
    /// </para>
    /// </summary>
    private static BrandingDefinition CreateDiapasonBranding() => new(
        "Le Diapason",
        "Diapason",
        "Une réponse fluide n'est pas une réponse vérifiée.",
        BrandIconUrl: "diapason-core:brand.icon",
        ClientIconUrl: "diapason-core:client.icon",
        Theme: new BrandingThemeDefinition(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ink"] = "#17344a",
                ["surface"] = "#fffaf0",
                ["accent"] = "#d7a746",
                ["accentAlt"] = "#2f7fa0",
                ["success"] = "#7a9a55",
                ["warning"] = "#c98a2e",
                ["danger"] = "#a33b2a",
                ["muted"] = "#c8b98d",
            },
            BrandingColorScheme.Light,
            12,
            "Georgia, \"Times New Roman\", serif"),
        // One entry per accent token actually used by the six postures, the three
        // journeys and the familiar. An unmapped token would leave a category
        // unrenderable, which is exactly what this block exists to prevent.
        AccentPalette: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encre"] = "#17344a",
            ["azur"] = "#2f7fa0",
            ["or"] = "#d7a746",
            ["cuivre"] = "#b0733a",
            ["sauge"] = "#7a9a55",
            ["aube"] = "#e8b98c",
            ["amber"] = "#c98a2e",
        });

    private async Task<ExperienceConfiguration> GetRequiredAsync(string frontId, CancellationToken cancellationToken) =>
        await repository.GetAsync(frontId, cancellationToken).ConfigureAwait(false)
        ?? throw new ConfigurationException("configuration_not_found", "The front configuration was not found.");

    private static ExperienceConfigurationView Map(ExperienceConfiguration configuration) =>
        new(configuration.Id, configuration.Revision, configuration.PublishedVersion, configuration.UpdatedAt, configuration.PublishedAt, Deserialize(configuration.DocumentJson));

    private static ExperienceDocument Deserialize(string json)
    {
        try
        {
            ExperienceDocument document = JsonSerializer.Deserialize<ExperienceDocument>(json, JsonOptions)
                ?? throw new ConfigurationException("invalid_configuration", "The configuration document is empty.");
            return Normalize(document);
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException("invalid_configuration", "The configuration document is invalid.", exception);
        }
    }

    private static void Validate(string frontId, ExperienceDocument document)
    {
        if (!string.Equals(frontId.Trim(), document.FrontId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException("front_mismatch", "The route and document front identifiers must match.");
        }

        if (string.IsNullOrWhiteSpace(document.Game.Name) || string.IsNullOrWhiteSpace(document.Game.GlobalStory))
        {
            throw new ConfigurationException("invalid_game", "The game name and global story are required.");
        }

        GameLanguageDefinition language = document.Language
            ?? throw new ConfigurationException("invalid_language", "The game language definition is required.");
        if (language.Labels.Count is 0 or > 250
            || language.Labels.Any(static label =>
                string.IsNullOrWhiteSpace(label.Key)
                || label.Key.Length > 80
                || string.IsNullOrWhiteSpace(label.Value)
                || label.Value.Length > 500))
        {
            throw new ConfigurationException("invalid_language", "Game wording keys and values must be non-empty and within their size limits.");
        }

        if (!document.Authentication.LocalEnabled && !document.Authentication.EntraEnabled)
        {
            throw new ConfigurationException("invalid_authentication", "At least one authentication provider must be enabled.");
        }

        if (document.Authentication.EntraEnabled && (string.IsNullOrWhiteSpace(document.Authentication.EntraTenantId) || string.IsNullOrWhiteSpace(document.Authentication.EntraClientId)))
        {
            throw new ConfigurationException("invalid_entra_configuration", "Tenant and client identifiers are required when Entra ID is enabled.");
        }

        IReadOnlyList<JourneyDefinition> journeys = document.Journeys ?? [];
        IReadOnlyList<CatalogAssignmentDefinition> assignments = document.Assignments ?? [];
        if (document.Categories.Select(static category => category.Id).Distinct().Count() != document.Categories.Count
            || document.Familiars.Select(static familiar => familiar.Id).Distinct().Count() != document.Familiars.Count
            || document.AiProviders.Select(static provider => provider.Id).Distinct().Count() != document.AiProviders.Count
            || journeys.Select(static journey => journey.Id).Distinct().Count() != journeys.Count
            || assignments.Select(static assignment => assignment.Id).Distinct().Count() != assignments.Count)
        {
            throw new ConfigurationException("duplicate_identifier", "Category, familiar and provider identifiers must be unique.");
        }

        // A blank reference means "no credential needed"; anything else must match the
        // scheme:identifier grammar. The offending value is never echoed back.
        if (document.AiProviders.Any(static provider =>
                !string.IsNullOrWhiteSpace(provider.SecretReference)
                && !GenEngine.Secrets.SecretReferenceGrammar.IsWellFormed(provider.SecretReference)))
        {
            throw new ConfigurationException(
                "invalid_secret_reference",
                "An AI provider secret reference must match the scheme:identifier grammar.");
        }

        OrganizationDefinition organization = document.Organization
            ?? throw new ConfigurationException("invalid_organization", "The organization definition is required.");
        if (string.IsNullOrWhiteSpace(organization.Name)
            || organization.Units.Select(static unit => unit.Id).Distinct().Count() != organization.Units.Count)
        {
            throw new ConfigurationException("invalid_organization", "The organization name and unique unit identifiers are required.");
        }

        HashSet<Guid> unitIds = organization.Units.Select(static unit => unit.Id).ToHashSet();
        HashSet<Guid> categoryIds = document.Categories.Select(static category => category.Id).ToHashSet();
        HashSet<Guid> journeyIds = journeys.Select(static journey => journey.Id).ToHashSet();
        if (organization.Units.Any(unit =>
                string.IsNullOrWhiteSpace(unit.Name)
                || string.IsNullOrWhiteSpace(unit.Type)
                || unit.ParentId == unit.Id
                || (unit.ParentId is not null && !unitIds.Contains(unit.ParentId.Value))))
        {
            throw new ConfigurationException("invalid_organization_hierarchy", "Organization units must have a name, type and a valid parent.");
        }

        foreach (OrganizationUnitDefinition unit in organization.Units)
        {
            HashSet<Guid> ancestors = [];
            OrganizationUnitDefinition current = unit;
            while (current.ParentId is Guid parentId)
            {
                if (!ancestors.Add(parentId))
                {
                    throw new ConfigurationException("organization_cycle", "The organization hierarchy cannot contain a cycle.");
                }

                current = organization.Units.Single(candidate => candidate.Id == parentId);
            }
        }

        if (journeys.Any(journey =>
                string.IsNullOrWhiteSpace(journey.Name)
                || journey.CategoryIds.Any(categoryId => !categoryIds.Contains(categoryId))
                || journey.PrerequisiteJourneyIds.Any(prerequisiteId => prerequisiteId == journey.Id || !journeyIds.Contains(prerequisiteId))))
        {
            throw new ConfigurationException("invalid_journey", "Journeys must have a name and reference existing categories and prerequisites.");
        }

        ValidateJourneyPrerequisiteGraph(journeys);

        if (assignments.Any(assignment =>
                !unitIds.Contains(assignment.OrganizationUnitId)
                || assignment.DueAt < assignment.AvailableFrom
                || (assignment.ContentType.Equals("Journey", StringComparison.OrdinalIgnoreCase) && !journeyIds.Contains(assignment.ContentId))
                || (assignment.ContentType.Equals("Category", StringComparison.OrdinalIgnoreCase) && !categoryIds.Contains(assignment.ContentId))))
        {
            throw new ConfigurationException("invalid_assignment", "Assignments must reference an existing unit and catalog item with a valid availability window.");
        }

        if (document.Familiars.Any(static familiar =>
                string.IsNullOrWhiteSpace(familiar.Name)
                || familiar.HelpLevel is < 0 or > 5
                || !IsValidAssetUrl(familiar.PortraitUrl)
                || !IsValidAssetUrl(familiar.AvatarUrl)
                || !IsValidAssetUrl(familiar.BackgroundUrl)))
        {
            throw new ConfigurationException("invalid_familiar", "Familiars require a name, a valid help level and HTTPS asset URLs.");
        }

        foreach (FamiliarDefinition familiar in document.Familiars)
        {
            ValidateFamiliarAxes(familiar);
        }

        if (document.Economy.InitialBalance < 0 || document.Economy.RewardRules.Any(static rule => rule.Amount <= 0) || document.Economy.Offers.Any(static offer => offer.Price < 0))
        {
            throw new ConfigurationException("invalid_economy", "Economy amounts and prices must be valid positive values.");
        }

        IntroDefinition intro = document.Intro ?? CreateDefaultIntro();
        if (intro.MinimumDisplaySeconds is < 0 or > 60
            || intro.Scenes.Count is 0 or > 12
            || intro.Scenes.Select(static scene => scene.Id).Distinct().Count() != intro.Scenes.Count
            || intro.Scenes.Any(static scene => string.IsNullOrWhiteSpace(scene.Title) || !IsValidAssetUrl(scene.ImageUrl)))
        {
            throw new ConfigurationException("invalid_intro", "The introduction requires unique titled scenes, valid timing and HTTPS assets.");
        }

        OnboardingDefinition onboarding = document.Onboarding ?? CreateDefaultOnboarding();
        if (onboarding.Version < 1
            || onboarding.Steps.Select(static step => step.Id).Distinct().Count() != onboarding.Steps.Count
            || onboarding.Steps.Any(static step => string.IsNullOrWhiteSpace(step.Title) || string.IsNullOrWhiteSpace(step.Target)))
        {
            throw new ConfigurationException("invalid_onboarding", "The onboarding requires a positive version and unique valid steps.");
        }

        HelpCenterDefinition help = document.Help ?? CreateDefaultHelp();
        if (help.Articles.Select(static article => article.Id).Distinct().Count() != help.Articles.Count
            || help.Articles.Select(static article => article.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != help.Articles.Count
            || help.Articles.Any(static article => string.IsNullOrWhiteSpace(article.Slug) || string.IsNullOrWhiteSpace(article.Title)))
        {
            throw new ConfigurationException("invalid_help", "Help articles require unique identifiers, slugs and titles.");
        }

        AssistantPolicyDefinition assistant = document.AssistantPolicy ?? CreateDefaultAssistantPolicy();
        if (assistant.DefaultFrequency is < 0 or > 5)
        {
            throw new ConfigurationException("invalid_assistant_policy", "The assistant frequency must be between 0 and 5.");
        }

        ValidateMedia(document.Media ?? CreateDefaultMedia());
        ValidateBranding(document.Branding);

        // The statistics are validated first because the conditions below reference their
        // keys: a condition may only name a statistic this document actually publishes.
        PlayerStatsDefinition playerStats = document.PlayerStats ?? PlayerStatCatalog.CreateDefault();
        ValidatePlayerStats(playerStats);
        HashSet<string> statKeys = playerStats.Stats
            .Select(static stat => (stat.Key ?? string.Empty).Trim())
            .ToHashSet(StringComparer.Ordinal);

        ValidateFinale(document.Finale, categoryIds, journeyIds, statKeys);
        ValidateRewards(document.Rewards ?? RewardCatalog.CreateDefault(), categoryIds, journeyIds, statKeys);
    }

    /// <summary>
    /// A reward is optional, but a declared one must be earnable and displayable: a label
    /// and a description the player reads, conditions that can actually be satisfied, and
    /// at least one grant — a reward that grants nothing is a promise with no payload.
    /// </summary>
    /// <remarks>
    /// The conditions go through <see cref="ProgressConditionCatalog.Validate"/>, the very
    /// same check the finale runs. That is the point of the extraction: an operand rule
    /// fixed once is fixed for both blocks, and the two can never drift into accepting
    /// different documents for the same condition.
    /// </remarks>
    private static void ValidateRewards(
        RewardsDefinition rewards,
        HashSet<Guid> categoryIds,
        HashSet<Guid> journeyIds,
        HashSet<string> statKeys)
    {
        IReadOnlyList<ConditionalRewardDefinition> items = rewards.Rewards;
        if (items.Count > RewardCatalog.MaximumRewards
            || items.Select(static reward => reward.Id).Distinct().Count() != items.Count)
        {
            throw new ConfigurationException(
                "invalid_reward",
                $"Rewards must have unique identifiers and number at most {RewardCatalog.MaximumRewards}.");
        }

        foreach (ConditionalRewardDefinition reward in items)
        {
            if (string.IsNullOrWhiteSpace(reward.Label)
                || reward.Label.Length > RewardCatalog.MaximumLabelLength
                || string.IsNullOrWhiteSpace(reward.Description)
                || reward.Description.Length > RewardCatalog.MaximumDescriptionLength
                || !IsValidAssetUrl(reward.VisualUrl))
            {
                throw new ConfigurationException(
                    "invalid_reward",
                    "A reward requires a label and a description within their size limits and a valid visual reference.");
            }

            if (reward.Conditions.Count is 0 or > ProgressConditionCatalog.MaximumConditions
                || reward.Conditions.Select(static condition => condition.Id).Distinct().Count() != reward.Conditions.Count)
            {
                throw new ConfigurationException(
                    "invalid_reward",
                    $"A reward requires 1 to {ProgressConditionCatalog.MaximumConditions} uniquely identified conditions.");
            }

            if (reward.Grants.Count is 0 or > RewardCatalog.MaximumGrantsPerReward)
            {
                throw new ConfigurationException(
                    "invalid_reward",
                    $"A reward must grant between 1 and {RewardCatalog.MaximumGrantsPerReward} things.");
            }

            foreach (RewardGrantDefinition grant in reward.Grants)
            {
                ValidateRewardGrant(grant);
            }

            ProgressConditionCatalog.Validate(reward.Conditions, categoryIds, journeyIds, statKeys, "invalid_reward_condition");
        }
    }

    /// <summary>
    /// Each grant nature carries its own operand and only its own: an achievement and a
    /// title need the stable reference a client renders them by, a currency grant needs a
    /// strictly positive amount. Zero is refused rather than tolerated — a grant that
    /// credits nothing is indistinguishable at runtime from one that failed.
    /// </summary>
    private static void ValidateRewardGrant(RewardGrantDefinition grant)
    {
        if (string.IsNullOrWhiteSpace(grant.Label) || grant.Label.Length > RewardCatalog.MaximumLabelLength)
        {
            throw new ConfigurationException("invalid_reward", "A reward grant requires a label within its size limit.");
        }

        bool valid = grant.Type switch
        {
            RewardGrantType.Achievement or RewardGrantType.Title =>
                IsRewardReference(grant.Reference) && grant.Amount is null,
            RewardGrantType.Currency =>
                grant.Amount is > 0 and <= RewardCatalog.MaximumGrantAmount && grant.Reference is null,
            _ => false,
        };

        if (!valid)
        {
            throw new ConfigurationException(
                "invalid_reward",
                $"An achievement or title grant requires a slug reference and no amount; a currency grant requires an amount between 1 and {RewardCatalog.MaximumGrantAmount} and no reference.");
        }
    }

    /// <summary>
    /// Same closed slug grammar as a player statistic key, for the same reason: the
    /// reference is stored on the profile and matched by clients, so it must never
    /// depend on casing, accents or whitespace.
    /// </summary>
    private static bool IsRewardReference(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > RewardCatalog.MaximumReferenceLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A statistic is displayed on the player profile and written to by scenarios, so a
    /// declared one must be renderable and reachable: a key an author can actually write,
    /// a label and a description a player can actually read, and a ceiling a grant can
    /// actually reach.
    /// </summary>
    /// <remarks>
    /// The ceiling is required to be strictly positive because zero would make every
    /// grant saturate immediately: the stat would exist, accept effects, and never move —
    /// the least diagnosable failure of the whole block.
    /// </remarks>
    private static void ValidatePlayerStats(PlayerStatsDefinition playerStats)
    {
        IReadOnlyList<PlayerStatDefinition> stats = playerStats.Stats;
        if (stats.Count > PlayerStatCatalog.MaximumStats
            || stats.Select(static stat => stat.Id).Distinct().Count() != stats.Count
            || stats.Select(static stat => (stat.Key ?? string.Empty).Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != stats.Count)
        {
            throw new ConfigurationException(
                "invalid_player_stat",
                $"Player statistics must have unique identifiers and keys, and number at most {PlayerStatCatalog.MaximumStats}.");
        }

        foreach (PlayerStatDefinition stat in stats)
        {
            if (!IsPlayerStatKey(stat.Key))
            {
                throw new ConfigurationException(
                    "invalid_player_stat",
                    $"A player statistic key must be 1 to {PlayerStatCatalog.MaximumKeyLength} characters of a-z, 0-9 and '-'.");
            }

            if (string.IsNullOrWhiteSpace(stat.Label)
                || stat.Label.Length > PlayerStatCatalog.MaximumLabelLength
                || string.IsNullOrWhiteSpace(stat.Description)
                || stat.Description.Length > PlayerStatCatalog.MaximumDescriptionLength)
            {
                throw new ConfigurationException(
                    "invalid_player_stat",
                    "A player statistic requires a non-empty label and description within their size limits.");
            }

            if (stat.Maximum is <= 0 or > PlayerStatCatalog.MaximumCeiling)
            {
                throw new ConfigurationException(
                    "invalid_player_stat",
                    $"A player statistic ceiling must be between 1 and {PlayerStatCatalog.MaximumCeiling}.");
            }
        }
    }

    /// <summary>
    /// Same slug grammar the narrative engine enforces on a <c>grantPlayerStat</c> key.
    /// The two must agree: a key one side accepts and the other refuses would be a stat
    /// no scenario could ever feed.
    /// </summary>
    private static bool IsPlayerStatKey(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > PlayerStatCatalog.MaximumKeyLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A finale is optional, but a declared one must be evaluable without guessing:
    /// every condition carries the operand its type needs and points at content that
    /// actually exists, otherwise the trigger could never fire and the player would
    /// wait for an ending that does not come.
    /// </summary>
    private static void ValidateFinale(FinaleDefinition? finale, HashSet<Guid> categoryIds, HashSet<Guid> journeyIds, HashSet<string> statKeys)
    {
        if (finale is null) return;

        if (string.IsNullOrWhiteSpace(finale.Title)
            || finale.Conditions.Count is 0 or > FinaleCatalog.MaximumConditions
            || finale.Conditions.Select(static condition => condition.Id).Distinct().Count() != finale.Conditions.Count
            || !IsValidAssetUrl(finale.VisualUrl)
            || !IsValidAssetUrl(finale.MusicUrl))
        {
            throw new ConfigurationException("invalid_finale", "A finale requires a title, unique conditions within their limit and HTTPS assets.");
        }

        // The operand rules live in ProgressConditionCatalog, shared with the rewards. The
        // finale keeps its own error code so an operator still learns which block failed.
        ProgressConditionCatalog.Validate(finale.Conditions, categoryIds, journeyIds, statKeys, "invalid_finale_condition");
    }

    /// <summary>
    /// A familiar axis is a closed catalogue, never free text: a client must be able to
    /// render and explain every value before the player picks it. This is what
    /// <c>writingStyle</c> and <c>accent</c> lacked, and why they were unpreviewable.
    /// </summary>
    private static void ValidateFamiliarAxes(FamiliarDefinition familiar)
    {
        IReadOnlyList<FamiliarAxisDefinition> axes = familiar.Axes ?? [];
        if (axes.Count > FamiliarCatalog.MaximumAxes
            || axes.Select(static axis => axis.Axis?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Count() != axes.Count)
        {
            throw new ConfigurationException("invalid_familiar_axis", "Familiar axes must be uniquely keyed and within their count limit.");
        }

        foreach (FamiliarAxisDefinition axis in axes)
        {
            if (string.IsNullOrWhiteSpace(axis.Axis)
                || axis.Axis.Length > FamiliarCatalog.MaximumValueLength
                || string.IsNullOrWhiteSpace(axis.Label)
                || axis.Options.Count is 0 or > FamiliarCatalog.MaximumOptionsPerAxis)
            {
                throw new ConfigurationException("invalid_familiar_axis", "A familiar axis requires a key, a label and between 1 and 24 options.");
            }

            if (axis.Options.Any(static option =>
                    string.IsNullOrWhiteSpace(option.Value)
                    || option.Value.Length > FamiliarCatalog.MaximumValueLength
                    || string.IsNullOrWhiteSpace(option.Label)
                    || option.Value.Any(char.IsControl)
                    || !IsValidAssetUrl(option.AssetReference)))
            {
                throw new ConfigurationException("invalid_familiar_axis", "A familiar option requires a printable value, a label and a valid asset reference.");
            }

            if (axis.Options.Select(static option => option.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != axis.Options.Count)
            {
                throw new ConfigurationException("invalid_familiar_axis", "Familiar option values must be unique inside their axis.");
            }

            if (!axis.Options.Any(option => string.Equals(option.Value, axis.DefaultValue, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConfigurationException("invalid_familiar_axis", "The default value of a familiar axis must be one of its options.");
            }
        }
    }

    /// <summary>
    /// Colour tokens a theme must always define. A client can then style every
    /// documented surface without guessing, and an operator learns at publication
    /// time that a token is missing rather than in front of a blank screen.
    /// </summary>
    public static IReadOnlyList<string> RequiredThemeColors { get; } =
        ["ink", "surface", "accent", "accentAlt", "success", "warning", "danger", "muted"];

    /// <summary>
    /// The branding block is optional and additive: a document without it is
    /// valid and left untouched. When present, every colour is a strict
    /// <c>#RRGGBB</c> or <c>#RRGGBBAA</c> value and every icon goes through the
    /// same <see cref="IsValidAssetUrl"/> grammar as familiars, intro scenes and
    /// media, so an HTTPS URL and a "packId:assetId" pack reference are the only
    /// two accepted forms.
    /// </summary>
    private static void ValidateBranding(BrandingDefinition? branding)
    {
        if (branding is null)
        {
            return;
        }

        if (IsBlankOrTooLong(branding.ApplicationName, 80)
            || IsBlankOrTooLong(branding.ShortName, 24)
            || IsBlankOrTooLong(branding.Tagline, 160))
        {
            throw new ConfigurationException("invalid_branding", "Branding names and tagline must be non-empty and within their size limits.");
        }

        if (!IsValidAssetUrl(branding.BrandIconUrl)
            || !IsValidAssetUrl(branding.ClientIconUrl)
            || !IsValidAssetUrl(branding.LogoUrl)
            || !IsValidAssetUrl(branding.FaviconUrl))
        {
            throw new ConfigurationException("invalid_branding", "Branding icons must be absolute HTTPS URLs or 'packId:assetId' pack references.");
        }

        if (branding.Theme is BrandingThemeDefinition theme)
        {
            if (theme.CornerRadius is < 0 or > 64 || IsBlankOrTooLong(theme.FontFamily, 120))
            {
                throw new ConfigurationException("invalid_branding", "The theme corner radius must be between 0 and 64 and the font family within its size limit.");
            }

            ValidateColorMap(theme.Colors, "theme");
            if (RequiredThemeColors.Any(token => !theme.Colors.ContainsKey(token)))
            {
                throw new ConfigurationException(
                    "invalid_branding",
                    $"The theme must define every required colour token: {string.Join(", ", RequiredThemeColors)}.");
            }
        }

        if (branding.AccentPalette is IReadOnlyDictionary<string, string> palette)
        {
            ValidateColorMap(palette, "accent palette");
        }
    }

    private static void ValidateColorMap(IReadOnlyDictionary<string, string>? colors, string what)
    {
        // A positional record parameter that the request body omits arrives as
        // null, not as an empty map. Dereferencing it here would surface as an
        // opaque 500 instead of the invalid_branding an operator can act on.
        if (colors is null)
        {
            throw new ConfigurationException("invalid_branding", $"The {what} must define its colours.");
        }

        if (colors.Count > 60
            || colors.Any(static color => string.IsNullOrWhiteSpace(color.Key) || color.Key.Length > 40 || !IsHexColor(color.Value)))
        {
            throw new ConfigurationException("invalid_branding", $"Every {what} colour must be named once and expressed as #RRGGBB or #RRGGBBAA.");
        }
    }

    private static bool IsBlankOrTooLong(string? value, int maximumLength) =>
        value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength);

    /// <summary>
    /// Strict hexadecimal colour: a leading '#' then exactly six or eight
    /// hexadecimal digits. Named CSS colours, <c>rgb()</c> and three-digit
    /// shorthands are refused on purpose, so every client renders the same value.
    /// </summary>
    private static bool IsHexColor(string? value)
    {
        if (value is null || (value.Length != 7 && value.Length != 9) || value[0] != '#')
        {
            return false;
        }

        foreach (char character in value.AsSpan(1))
        {
            bool hexadecimal = (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F');
            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A journey prerequisite graph must stay acyclic, exactly like the organization
    /// hierarchy above. Rejecting only the self-reference let A → B → A through, and a
    /// cycle makes every journey of the loop permanently locked: no player can ever
    /// satisfy its prerequisites. A journey may declare several prerequisites, so the
    /// ancestor walk used for units becomes a breadth-first closure here — the rule is
    /// the same, only the branching factor differs.
    /// </summary>
    private static void ValidateJourneyPrerequisiteGraph(IReadOnlyList<JourneyDefinition> journeys)
    {
        Dictionary<Guid, IReadOnlyList<Guid>> prerequisites = journeys.ToDictionary(
            static journey => journey.Id,
            static journey => journey.PrerequisiteJourneyIds);

        foreach (JourneyDefinition journey in journeys)
        {
            HashSet<Guid> reached = [];
            Queue<Guid> pending = new(journey.PrerequisiteJourneyIds);
            while (pending.Count > 0)
            {
                Guid current = pending.Dequeue();
                if (current == journey.Id)
                {
                    throw new ConfigurationException("journey_cycle", "The journey prerequisite graph cannot contain a cycle.");
                }

                if (!reached.Add(current)) continue;
                foreach (Guid ancestor in prerequisites[current])
                {
                    pending.Enqueue(ancestor);
                }
            }
        }
    }

    /// <summary>
    /// Media are decorative and always optional, but a declared asset must be an
    /// absolute HTTPS URL and a declared tempo must stay within the documented
    /// ambient range, so a client never has to guess what to load.
    /// </summary>
    private static void ValidateMedia(MediaDefinition media)
    {
        if (media.Locations.Count > 40
            || media.Locations.Any(static location => string.IsNullOrWhiteSpace(location.Location) || location.Location.Length > 40)
            || media.Locations
                .Select(static location => location.Location.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != media.Locations.Count)
        {
            throw new ConfigurationException("invalid_media", "Application locations must be named once and within their size limit.");
        }

        if (media.Locations.Any(static location =>
                !IsValidAssetUrl(location.AmbienceUrl)
                || !IsValidAssetUrl(location.MusicUrl)
                || !IsValidAssetUrl(location.BackgroundUrl)
                || location.Bpm is < 40 or > 200))
        {
            throw new ConfigurationException("invalid_media", "Location media require HTTPS assets and a tempo between 40 and 200 BPM.");
        }

        if (media.GameOver is GameOverMediaDefinition gameOver
            && (!IsValidAssetUrl(gameOver.MusicUrl) || !IsValidAssetUrl(gameOver.VisualUrl)))
        {
            throw new ConfigurationException("invalid_media", "Game-over media require HTTPS assets.");
        }
    }

    /// <summary>
    /// Default ambience map. Each location carries its generative pack background
    /// (shipped in <c>diapason-core</c>, so the reference resolves offline). The
    /// audio stays null on purpose: no ambience or music file exists in the pack
    /// yet, and pointing at a reference that does not resolve would be worse than
    /// silence. The game-over visual is likewise left null until an asset exists.
    /// </summary>
    private static MediaDefinition CreateDefaultMedia() => new(
        true,
        true,
        [
            new AppLocationMediaDefinition("home", BackgroundUrl: "diapason-core:background.home"),
            new AppLocationMediaDefinition("map", BackgroundUrl: "diapason-core:background.map"),
            new AppLocationMediaDefinition("player", BackgroundUrl: "diapason-core:background.player"),
            new AppLocationMediaDefinition("journal", BackgroundUrl: "diapason-core:background.journal"),
            new AppLocationMediaDefinition("familiar", BackgroundUrl: "diapason-core:background.familiar"),
            new AppLocationMediaDefinition("shop", BackgroundUrl: "diapason-core:background.shop"),
        ],
        new GameOverMediaDefinition(LabelKey: "gameOver.title"));

    private static OrganizationDefinition CreateOrganizationDefault(OrganizationType type) => new(
        type switch
        {
            OrganizationType.School => "Établissement",
            OrganizationType.Company => "Entreprise",
            OrganizationType.TrainingProvider => "Organisme de formation",
            _ => "Organisation principale",
        },
        "Structure configurable du jeu.",
        []);

    private static ExperienceDocument Normalize(ExperienceDocument document)
    {
        Dictionary<string, string> labels = CreateDefaultLabels();
        if (document.Language is not null)
        {
            foreach (KeyValuePair<string, string> label in document.Language.Labels)
            {
                labels[label.Key] = label.Value;
            }
        }

        return document with
        {
            Organization = document.Organization ?? CreateOrganizationDefault(document.OrganizationType),
            Language = new GameLanguageDefinition(labels),
            Categories = document.Categories.Select(static category => category with
            {
                Tags = category.Tags ?? [],
                ScenarioIds = category.ScenarioIds ?? [],
            }).ToArray(),
            // The familiar personalisation catalogue is always materialised, so a
            // document written before the axes existed publishes the same contract as
            // one authored today and no client has to special-case a missing block.
            Familiars = document.Familiars.Select(FamiliarCatalog.Expand).ToArray(),
            Journeys = document.Journeys ?? [],
            Assignments = document.Assignments ?? [],
            Intro = document.Intro ?? CreateDefaultIntro(),
            PlayerShell = document.PlayerShell ?? CreateDefaultPlayerShell(),
            Demo = document.Demo ?? new DemoExperienceDefinition(true, DiapasonIds.DemoScenarioSlug, 18, document.Familiars.Count > 0 ? document.Familiars[0].Id : null, "demo.createAccount"),
            Help = document.Help ?? CreateDefaultHelp(),
            Onboarding = document.Onboarding ?? CreateDefaultOnboarding(),
            AssistantPolicy = document.AssistantPolicy ?? CreateDefaultAssistantPolicy(),
            Journal = document.Journal ?? new JournalPolicyDefinition(true, true, 0, true),
            Media = document.Media ?? CreateDefaultMedia(),
            // Materialised like the media block: every published document carries the
            // catalogue, so a client reads one shape whether the instance uses stats or
            // not. The default is empty — see PlayerStatsDefinition for why.
            PlayerStats = document.PlayerStats ?? PlayerStatCatalog.CreateDefault(),
            // Same treatment, same reason: one published shape for every instance, and an
            // empty default so nobody is granted rewards they never configured.
            Rewards = document.Rewards ?? RewardCatalog.CreateDefault(),
            // Left null when absent rather than defaulted: an instance that declares no
            // finale simply has none, and inventing one would change what its players see.
            Finale = document.Finale,
        };
    }

    private static IntroDefinition CreateDefaultIntro() => new(
        true,
        IntroDisplayPolicy.OncePerVersion,
        true,
        0,
        [
            new IntroSceneDefinition(
                Guid.Parse("59ee8932-281f-4fbc-a02b-90221d0a0ad4"),
                "Le Diapason",
                "Une réponse fluide n'est pas une réponse vérifiée.",
                "2026. Vous êtes étudiant ingénieur en alternance. Personne autour de vous n'a le temps de douter, et vous êtes souvent la seule personne à détenir le fait qui manque. Ce que vous en faites vous appartient.",
                null,
                1),
        ]);

    private static PlayerShellDefinition CreateDefaultPlayerShell() => new(
        [
            new NavigationItemDefinition("map", "nav.map", "map", 1, true, "play"),
            new NavigationItemDefinition("progress", "nav.progress", "chart", 2, true, "play"),
            new NavigationItemDefinition("companion", "nav.companion", "sparkles", 3, true, "assistant"),
            new NavigationItemDefinition("shop", "nav.shop", "bag", 4, true, "shop"),
            new NavigationItemDefinition("help", "nav.help", "help", 5, true),
            new NavigationItemDefinition("account", "nav.account", "account", 6, true),
        ]);

    private static HelpCenterDefinition CreateDefaultHelp() => new(
        true,
        [
            new HelpArticleDefinition(Guid.Parse("b01faeca-c3a6-4e44-9374-bf1f5f23a948"), "premiers-pas", "Premiers pas", "Comprendre la carte, les scénarios et votre progression.", "Votre carte rassemble les parcours et catégories accessibles. Ouvrez une catégorie pour découvrir ses scénarios.", ["map", "onboarding"], ["débuter", "carte"], 1, true),
            new HelpArticleDefinition(Guid.Parse("b6adb4dc-a161-45db-989d-5a34786b54fe"), "rejouer", "Explorer d’autres chemins", "Rejouez sans perdre vos découvertes.", "Chaque nouvelle branche enrichit votre arbre et votre journal. Votre compagnon peut vous prévenir avant un chemin déjà parcouru.", ["player", "completion", "progress"], ["rejeu", "arbre"], 2, true),
        ],
        [
            new GlossaryEntryDefinition("Parcours", "Un ensemble ordonné de catégories qui compose une aventure globale."),
            new GlossaryEntryDefinition("Maîtrise", "La part des fins, branches et jalons d’exploration que vous avez découverts."),
        ]);

    private static OnboardingDefinition CreateDefaultOnboarding() => new(
        Guid.Parse("9cccf7f7-fba6-45ff-a3be-42d8993bb8cc"),
        1,
        true,
        true,
        false,
        [
            new OnboardingStepDefinition(Guid.Parse("7b28af3a-e075-420f-9a60-c42cae6fdfea"), "Votre compagnon", "Je resterai à vos côtés et vous aiderai sans choisir à votre place.", "companion", "open", 1, true),
            new OnboardingStepDefinition(Guid.Parse("eea6be3b-1810-45ea-8428-9fc4adb42aa2"), "Votre carte", "Explorez les catégories pour retrouver les scénarios accessibles.", "map", "open", 2, true),
            new OnboardingStepDefinition(Guid.Parse("17664c57-f8b0-4106-897b-a55e4d918429"), "Votre progression", "Chaque fin et chaque branche découverte enrichissent votre journal.", "progress", "open", 3, true),
        ]);

    private static AssistantPolicyDefinition CreateDefaultAssistantPolicy() =>
        new(true, true, true, true, 2, ["hint", "recap", "rephrase", "known-path-warning"]);

    /// <summary>
    /// An asset is either an absolute HTTPS URL, for assets the instance hosts
    /// itself, or a pack reference "packId:assetId" resolved by the client through
    /// the shipped pack manifest. The second form is what lets a configuration —
    /// and its demonstration — run with no host to serve the files from. The
    /// grammar mirrors the narrative engine's, so both validate references alike.
    /// </summary>
    private static bool IsValidAssetUrl(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps)
        || IsPackReference(value);

    private static bool IsPackReference(string value)
    {
        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        return IsPackSegment(value.AsSpan(0, separator))
            && IsPackSegment(value.AsSpan(separator + 1));
    }

    private static bool IsPackSegment(ReadOnlySpan<char> segment)
    {
        foreach (char character in segment)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character is '.' or '-' or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return !segment.IsEmpty;
    }

    private static Dictionary<string, string> CreateDefaultLabels() => new(StringComparer.Ordinal)
    {
        ["welcome.eyebrow"] = "Vos choix. Votre histoire.",
        ["welcome.title"] = "Entrez dans des mondes qui se souviennent de vous.",
        ["welcome.subtitle"] = "Une nouvelle génération de récits interactifs.",
        ["auth.username"] = "Identifiant",
        ["auth.password"] = "Mot de passe",
        ["auth.login"] = "Se connecter",
        ["auth.register"] = "Créer un compte",
        ["auth.microsoft"] = "Continuer avec Microsoft",
        ["auth.existingAccount"] = "J’ai déjà un compte",
        ["demo.explore"] = "Explorer la démo",
        ["demo.createAccount"] = "Continuer mon aventure",
        ["nav.home"] = "Accueil",
        ["nav.library"] = "Bibliothèque",
        ["nav.map"] = "Carte",
        ["nav.progress"] = "Progression",
        ["nav.companion"] = "Compagnon",
        ["nav.shop"] = "Magasin",
        ["nav.help"] = "Aide",
        ["nav.account"] = "Compte",
        ["nav.menu"] = "Menu",
        ["nav.experience"] = "Mon univers",
        ["nav.studio"] = "Studio",
        ["nav.administration"] = "Administration",
        ["home.discover"] = "À découvrir",
        ["library.resume"] = "Reprendre le fil",
        ["action.start"] = "Commencer",
        ["action.continue"] = "Continuer",
        ["action.close"] = "Fermer",
        ["entity.journey.singular"] = "Parcours",
        ["entity.journey.plural"] = "Parcours",
        ["entity.category.singular"] = "Catégorie",
        ["entity.category.plural"] = "Catégories",
        ["entity.scenario.singular"] = "Scénario",
        ["entity.scenario.plural"] = "Scénarios",
        ["entity.story.singular"] = "Histoire",
        ["entity.story.plural"] = "Histoires",
        ["entity.familiar.singular"] = "Familier",
        ["entity.familiar.plural"] = "Familiers",
        ["experience.title"] = "Mon univers",
        ["experience.familiar.title"] = "Mon familier",
        ["experience.familiar.configuration"] = "Configuration personnelle",
        ["experience.familiar.subtitle"] = "Une présence qui vous ressemble",
        ["experience.familiar.form"] = "Forme",
        ["experience.familiar.tone"] = "Ton",
        ["experience.familiar.helpLevel"] = "Niveau d’aide",
        ["experience.familiar.helpLow"] = "Discret",
        ["experience.familiar.helpHigh"] = "Très présent",
        ["experience.familiar.apply"] = "Appliquer cette personnalité",
        ["experience.familiar.saved"] = "Votre familier s’adaptera dès la prochaine scène.",
        ["experience.familiar.axis.aura"] = "Aura",
        ["experience.familiar.axis.silhouette"] = "Silhouette",
        ["experience.familiar.axis.speechRhythm"] = "Rythme d’élocution",
        ["experience.familiar.axis.languageRegister"] = "Registre de langage",
        ["experience.familiar.axis.interventionDensity"] = "Densité d’intervention",
        ["experience.familiar.preview"] = "Aperçu",
        ["finale.title"] = "Ce qui reste après vous",
        ["finale.reached"] = "Vous avez atteint la fin. Rien ne se ferme : les branches non ouvertes vous attendent.",
        ["finale.progress"] = "Progression vers la fin",
        ["finale.continue"] = "Continuer à jouer",
        ["rewards.title"] = "Récompenses",
        ["rewards.earned"] = "Obtenue",
        ["rewards.progress"] = "Progression vers cette récompense",
        ["rewards.cinqScenarios"] = "Cinq fois plutôt qu'une",
        ["experience.wallet.title"] = "Portefeuille",
        ["experience.wallet.empty"] = "Vos choix écriront ici les premières lignes de votre progression.",
        ["experience.shop.title"] = "Magasin",
        ["experience.shop.subtitle"] = "Des objets qui racontent qui vous êtes",
        ["experience.shop.owned"] = "Acquis",
        ["experience.shop.purchased"] = "Objet ajouté à votre collection.",
        ["experience.loading"] = "Connexion à votre univers…",
        ["studio.eyebrow"] = "Atelier narratif",
        ["studio.title"] = "Créer une nouvelle histoire",
        ["studio.generate"] = "Générer le scénario",
        ["studio.copilot.eyebrow"] = "Copilote narratif",
        ["studio.copilot.title"] = "Du monde global au premier brouillon",
        ["studio.copilot.subtitle"] = "Le moteur combine l’histoire globale, la catégorie et votre intention. Le résultat reste un brouillon, jamais publié automatiquement.",
        ["studio.prompt.label"] = "Votre intention",
        ["studio.category.label"] = "Catégorie",
        ["studio.duration.label"] = "Durée cible",
        ["studio.tone.label"] = "Ton",
        ["studio.provider.label"] = "Provider",
        ["studio.generateDraft"] = "Générer le brouillon",
        ["administration.eyebrow"] = "Centre de contrôle",
        ["administration.title"] = "Piloter l’expérience",
        ["administration.subtitle"] = "Le paramétrage du jeu et des accès reste séparé du Studio éditorial.",
        ["status.soon"] = "Bientôt",
    };
}

public sealed class ConfigurationException : InvalidOperationException
{
    public ConfigurationException(string code, string message) : base(message) => Code = code;
    public ConfigurationException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;
    public string Code { get; }
}