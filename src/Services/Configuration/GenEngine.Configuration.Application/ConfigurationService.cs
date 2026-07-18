using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Domain;

namespace GenEngine.Configuration.Application;

public enum OrganizationType { School, Company, TrainingProvider, Community, Custom }
public enum AuthenticationMode { LocalOnly, EntraOnly, Cumulative }
public enum AiProviderType { Offline, AzureAiFoundry }

public sealed record GameDefinition(string Name, string Description, string GlobalStory, string Locale, string TimeZone);
public sealed record GameLanguageDefinition(IReadOnlyDictionary<string, string> Labels);
public sealed record OrganizationUnitDefinition(Guid Id, Guid? ParentId, string Type, string Name, string Code, string Description, int Order, bool Enabled);
public sealed record OrganizationDefinition(string Name, string Description, IReadOnlyList<OrganizationUnitDefinition> Units);
public sealed record AuthenticationDefinition(AuthenticationMode Mode, bool LocalEnabled, bool EntraEnabled, string? EntraTenantId, string? EntraClientId);
public sealed record AiProviderDefinition(Guid Id, string Name, AiProviderType Type, bool Enabled, string Endpoint, string Deployment, string Authentication, string? SecretReference, IReadOnlyList<string> Capabilities);
public sealed record CategoryDefinition(Guid Id, string Name, string Description, string Accent, int Order, bool IsVisible);
public sealed record FamiliarDefinition(Guid Id, string Name, string Description, string Form, string WritingStyle, string Tone, string Accent, int HelpLevel, IReadOnlyList<string> Capabilities, IReadOnlyList<string> AvailableForms, IReadOnlyList<string> AvailableTones);
public sealed record RewardRuleDefinition(string Trigger, string ReferenceId, int Amount, string Description);
public sealed record OfferDefinition(Guid Id, string Name, string Description, int Price, string RewardType, string RewardReference, bool Enabled);
public sealed record EconomyDefinition(string CurrencyCode, string CurrencyName, string CurrencyIcon, int InitialBalance, IReadOnlyList<RewardRuleDefinition> RewardRules, IReadOnlyList<OfferDefinition> Offers);
public sealed record ModuleDefinition(string Id, string Name, string Description, bool Enabled, IReadOnlyList<string> RequiredPermissions);

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
    IReadOnlyList<ModuleDefinition> Modules);

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
            new FamiliarDefinition(Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "Lueur", "Un éclat curieux qui pose les bonnes questions.", "spark", "Socratic", "Warm", "amber", 2, ["hint", "recap", "rephrase"], ["spark", "owl", "fox"], ["Warm", "Playful", "Direct", "Mysterious"]),
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
        ]);

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

        if (document.Categories.Select(static category => category.Id).Distinct().Count() != document.Categories.Count
            || document.Familiars.Select(static familiar => familiar.Id).Distinct().Count() != document.Familiars.Count
            || document.AiProviders.Select(static provider => provider.Id).Distinct().Count() != document.AiProviders.Count)
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

        if (document.Economy.InitialBalance < 0 || document.Economy.RewardRules.Any(static rule => rule.Amount <= 0) || document.Economy.Offers.Any(static offer => offer.Price < 0))
        {
            throw new ConfigurationException("invalid_economy", "Economy amounts and prices must be valid positive values.");
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
        };
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
        ["nav.home"] = "Accueil",
        ["nav.library"] = "Bibliothèque",
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