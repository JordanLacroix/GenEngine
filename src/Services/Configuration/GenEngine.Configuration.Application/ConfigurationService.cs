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
    string? Attribution = null);
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
    JournalPolicyDefinition? Journal = null);

public sealed record ExperienceConfigurationView(Guid Id, int Revision, int PublishedVersion, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, ExperienceDocument Document);
public sealed record PublishedExperienceView(int Version, DateTimeOffset PublishedAt, ExperienceDocument Document);

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
        ExperienceDocument publicDocument = document with
        {
            AiProviders = document.AiProviders.Select(provider => provider with { SecretReference = null }).ToArray(),
        };
        return new PublishedExperienceView(configuration.PublishedVersion, configuration.PublishedAt.Value, publicDocument);
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
        new GameDefinition("Les Chroniques de la Brume", "Une expérience narrative où chaque décision transforme le monde.", "La Brume efface les souvenirs du royaume. Les joueurs restaurent son histoire, fragment après fragment.", "fr-FR", "Europe/Paris"),
        new GameLanguageDefinition(CreateDefaultLabels()),
        new AuthenticationDefinition(AuthenticationMode.LocalOnly, true, false, null, null),
        [
            new AiProviderDefinition(Guid.Parse("3e6f6554-9b55-49ca-bd24-19e2f57e672a"), "Hors ligne", AiProviderType.Offline, true, string.Empty, "deterministic", "None", null, ["chat", "scenario-generation"]),
            new AiProviderDefinition(Guid.Parse("43b164f2-5a7d-48c0-b5c6-0dd7a3d44ea4"), "Azure AI Foundry", AiProviderType.AzureAiFoundry, false, "https://resource.openai.azure.com/openai/v1/", "gpt-4.1-mini", "EntraId", "azure-foundry-credential", ["chat", "scenario-generation", "input-analysis"]),
        ],
        [
            new CategoryDefinition(Guid.Parse("8dc4d13b-f6ca-4e16-bf52-a78cdf755f9e"), "Mystères", "Enquêtes, indices et vérités enfouies.", "ember", 1, true),
            new CategoryDefinition(Guid.Parse("00a575d4-9de8-47df-b713-35176969d410"), "Exploration", "Mondes inconnus et chemins alternatifs.", "verdigris", 2, true),
        ],
        [
            new FamiliarDefinition(Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "Lueur", "Un éclat curieux qui pose les bonnes questions.", "spark", "Socratic", "Warm", "amber", 2, ["hint", "recap", "rephrase"], ["spark", "owl", "fox"], ["Warm", "Playful", "Direct", "Mysterious"], "https://images.unsplash.com/photo-1518791841217-8f162f1e1131?auto=format&fit=crop&w=900&q=85", null, null, "Unsplash", "Photo de démonstration — remplacer avant production"),
        ],
        new EconomyDefinition("BRAISE", "Braises", "✦", 0,
            [
                new RewardRuleDefinition("ScenarioCompleted", "*", 25, "Terminer un scénario"),
                new RewardRuleDefinition("ChoiceSelected", "courageous-choice", 5, "Faire un choix courageux"),
            ],
            [
                new OfferDefinition(Guid.Parse("370b6f82-a264-45cc-a0d0-2d71e58be15e"), "Plumage nocturne", "Une apparence rare pour le familier.", 80, "FamiliarCosmetic", "nocturnal-plumage", true),
            ]),
        [
            new ModuleDefinition("play", "Jouer", "Accéder aux histoires publiées.", true, ["session.play"]),
            new ModuleDefinition("studio", "Studio", "Créer, générer et publier des scénarios.", true, ["scenario.author"]),
            new ModuleDefinition("administration", "Administration", "Configurer le jeu, les accès et les providers.", true, ["config.read"]),
            new ModuleDefinition("shop", "Magasin", "Dépenser les monnaies narratives.", true, ["shop.read"]),
        ],
        [
            new JourneyDefinition(
                Guid.Parse("b1eeb069-dcca-4db1-a5fb-a787299d4958"),
                "Les traces de la Brume",
                "Un parcours progressif pour comprendre le royaume et retrouver ses souvenirs.",
                "ember",
                null,
                1,
                true,
                [Guid.Parse("8dc4d13b-f6ca-4e16-bf52-a78cdf755f9e"), Guid.Parse("00a575d4-9de8-47df-b713-35176969d410")],
                [],
                ["découverte", "mystère"]),
        ],
        [],
        CreateDefaultIntro(),
        CreateDefaultPlayerShell(),
        new DemoExperienceDefinition(true, "les-braises-sous-la-brume", 15, Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "demo.createAccount"),
        CreateDefaultHelp(),
        CreateDefaultOnboarding(),
        CreateDefaultAssistantPolicy(),
        new JournalPolicyDefinition(true, true, 0, true));

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
    }

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
            Journeys = document.Journeys ?? [],
            Assignments = document.Assignments ?? [],
            Intro = document.Intro ?? CreateDefaultIntro(),
            PlayerShell = document.PlayerShell ?? CreateDefaultPlayerShell(),
            Demo = document.Demo ?? new DemoExperienceDefinition(true, "les-braises-sous-la-brume", 15, document.Familiars.Count > 0 ? document.Familiars[0].Id : null, "demo.createAccount"),
            Help = document.Help ?? CreateDefaultHelp(),
            Onboarding = document.Onboarding ?? CreateDefaultOnboarding(),
            AssistantPolicy = document.AssistantPolicy ?? CreateDefaultAssistantPolicy(),
            Journal = document.Journal ?? new JournalPolicyDefinition(true, true, 0, true),
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
                "Les Chroniques de la Brume",
                "Le monde se souvient de chacun de vos choix.",
                "Traversez la Brume, retrouvez les fragments oubliés et écrivez une histoire qui vous appartient.",
                "https://images.unsplash.com/photo-1511497584788-876760111969?auto=format&fit=crop&w=1800&q=85",
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

    private static bool IsValidAssetUrl(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps);

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
