using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Domain;

namespace GenEngine.Configuration.Application;

public enum OrganizationType { School, Company, TrainingProvider, Community, Custom }
public enum AuthenticationMode { LocalOnly, EntraOnly, Cumulative }
public enum AiProviderType { Offline, AzureAiFoundry }

public sealed record GameDefinition(string Name, string Description, string GlobalStory, string Locale, string TimeZone);
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
    GameDefinition Game,
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
        new GameDefinition("Les Chroniques de la Brume", "Une expérience narrative où chaque décision transforme le monde.", "La Brume efface les souvenirs du royaume. Les joueurs restaurent son histoire, fragment après fragment.", "fr-FR", "Europe/Paris"),
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
            new FamiliarDefinition(Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f"), "Mote", "Un éclat curieux qui pose les bonnes questions.", "spark", "Socratic", "Warm", "amber", 2, ["hint", "recap", "rephrase"], ["spark", "owl", "fox"], ["Warm", "Playful", "Direct", "Mysterious"]),
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
            return JsonSerializer.Deserialize<ExperienceDocument>(json, JsonOptions)
                ?? throw new ConfigurationException("invalid_configuration", "The configuration document is empty.");
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

        if (document.Economy.InitialBalance < 0 || document.Economy.RewardRules.Any(static rule => rule.Amount <= 0) || document.Economy.Offers.Any(static offer => offer.Price < 0))
        {
            throw new ConfigurationException("invalid_economy", "Economy amounts and prices must be valid positive values.");
        }
    }
}

public sealed class ConfigurationException : InvalidOperationException
{
    public ConfigurationException(string code, string message) : base(message) => Code = code;
    public ConfigurationException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;
    public string Code { get; }
}