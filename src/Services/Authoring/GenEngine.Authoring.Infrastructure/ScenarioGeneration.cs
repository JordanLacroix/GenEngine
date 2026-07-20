using System.Globalization;
using System.Text;
using System.Text.Json;

using Azure.AI.OpenAI;
using Azure.Identity;

using GenEngine.Authoring.Application;
using GenEngine.Narrative;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GenEngine.Authoring.Infrastructure;

internal sealed class ConfigurationStoryExperienceProvider(HttpClient httpClient) : IStoryExperienceProvider
{
    public async Task<StoryExperienceContext> GetAsync(string frontId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            $"/experience/{Uri.EscapeDataString(frontId)}",
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthoringException("experience_unavailable", "The published game configuration is unavailable.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        JsonElement document = json.RootElement.GetProperty("document");
        JsonElement game = document.GetProperty("game");
        StoryCategoryContext[] categories = document.GetProperty("categories")
            .EnumerateArray()
            .Where(static category => category.GetProperty("isVisible").GetBoolean())
            .Select(static category => new StoryCategoryContext(
                category.GetProperty("id").GetGuid(),
                category.GetProperty("name").GetString() ?? string.Empty,
                category.GetProperty("description").GetString() ?? string.Empty))
            .ToArray();
        return new StoryExperienceContext(
            document.GetProperty("frontId").GetString() ?? frontId,
            game.GetProperty("name").GetString() ?? string.Empty,
            game.GetProperty("description").GetString() ?? string.Empty,
            game.GetProperty("globalStory").GetString() ?? string.Empty,
            categories);
    }
}

internal sealed class OfflineScenarioDraftGenerator : IScenarioDraftGenerator
{
    public string Provider => "offline";

    public Task<ScenarioDocument> GenerateAsync(
        StoryExperienceContext experience,
        StoryCategoryContext category,
        ScenarioGenerationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string seed = request.Prompt.Trim().TrimEnd('.', '!', '?');
        string titleSeed = seed.Length > 52 ? string.Concat(seed.AsSpan(0, 49), "...") : seed;
        ScenarioDocument document = new(
            NarrativeVersions.LatestSchema,
            $"{category.Name} — {titleSeed}",
            "opening",
            [
                new NarrativeNode(
                    "opening",
                    $"Dans {experience.GameName}, {seed}. Cette aventure s'inscrit dans {experience.GlobalStory}",
                    null,
                    [],
                    [
                        new NarrativeChoice("investigate", "Suivre l'indice le plus fragile", "crossroads", null, [new IncrementEffect("curiosity", 1)]),
                        new NarrativeChoice("observe", "Prendre le temps d'observer", "crossroads", null, [new IncrementEffect("wisdom", 1)]),
                    ]),
                new NarrativeNode(
                    "crossroads",
                    $"Le fil de l'histoire révèle un choix décisif lié à « {category.Description} ».",
                    null,
                    [],
                    [
                        new NarrativeChoice(
                            "courageous-choice",
                            "Agir malgré l'incertitude",
                            "dawn",
                            null,
                            [
                                new GrantRewardEffect("courage"),
                                new EmitExternalEventEffect("economy.reward", new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["trigger"] = "ChoiceSelected",
                                    ["referenceId"] = "courageous-choice",
                                }),
                            ]),
                        new NarrativeChoice("patient-choice", "Préserver ce qui peut encore l'être", "embers", null, [new RecordNotableEventEffect("patience")]),
                    ]),
                new NarrativeNode(
                    "dawn",
                    "Une vérité nouvelle rejoint l'histoire globale et ouvre la voie au prochain scénario.",
                    null,
                    [
                        new EmitExternalEventEffect("economy.reward", new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["trigger"] = "ScenarioCompleted",
                            ["referenceId"] = "*",
                        }),
                    ],
                    [],
                    true),
                new NarrativeNode(
                    "embers",
                    "Le mystère demeure, mais un fragment essentiel a été sauvegardé pour la suite.",
                    null,
                    [
                        new EmitExternalEventEffect("economy.reward", new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["trigger"] = "ScenarioCompleted",
                            ["referenceId"] = "*",
                        }),
                    ],
                    [],
                    true),
            ]);
        return Task.FromResult(document);
    }
}

internal sealed class AzureFoundryScenarioDraftGenerator(IChatClient chatClient) : IScenarioDraftGenerator
{
    public string Provider => "azureAiFoundry";

    public async Task<ScenarioDocument> GenerateAsync(
        StoryExperienceContext experience,
        StoryCategoryContext category,
        ScenarioGenerationRequest request,
        CancellationToken cancellationToken)
    {
        string example = NarrativeJson.Serialize(await new OfflineScenarioDraftGenerator()
            .GenerateAsync(experience, category, request, cancellationToken).ConfigureAwait(false));
        List<ChatMessage> messages =
        [
            new(ChatRole.System, """
                You design deterministic branching stories for GenEngine.
                Return only one JSON object matching the supplied example schema.
                Keep identifiers stable ASCII kebab-case, every path reachable, and at least two distinct endings.
                Never add unknown JSON properties. Use emitExternalEvent effects named economy.reward for configured rewards.
                """),
            new(ChatRole.User, BuildPrompt(experience, category, request, example)),
        ];
        ChatResponse response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        string json = StripCodeFence(response.Text);
        try
        {
            return NarrativeJson.Deserialize<ScenarioDocument>(json);
        }
        catch (JsonException exception)
        {
            throw new AuthoringException("invalid_ai_response", "Azure AI Foundry returned an invalid scenario document.", exception);
        }
    }

    private static string BuildPrompt(
        StoryExperienceContext experience,
        StoryCategoryContext category,
        ScenarioGenerationRequest request,
        string example)
    {
        StringBuilder prompt = new();
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Game: {experience.GameName}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Game description: {experience.GameDescription}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Global story: {experience.GlobalStory}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Category: {category.Name} — {category.Description}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Target duration: {Math.Clamp(request.TargetMinutes, 3, 90)} minutes");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Tone: {request.Tone}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Creator brief: {request.Prompt}");
        prompt.AppendLine("Valid schema example:");
        prompt.Append(example);
        return prompt.ToString();
    }

    private static string StripCodeFence(string value)
    {
        string trimmed = value.Trim();
        string fence = new((char)96, 3);
        if (!trimmed.StartsWith(fence, StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLine = trimmed.IndexOf('\n');
        int finalFence = trimmed.LastIndexOf(fence, StringComparison.Ordinal);
        return firstLine >= 0 && finalFence > firstLine
            ? trimmed[(firstLine + 1)..finalFence].Trim()
            : trimmed;
    }
}

internal static class ScenarioGenerationExtensions
{
    public static IServiceCollection AddScenarioGeneration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string configurationBaseUrl = configuration["Configuration:BaseUrl"] ?? "http://localhost:5204";
        services.AddHttpClient<IStoryExperienceProvider, ConfigurationStoryExperienceProvider>(
            client => client.BaseAddress = new Uri(configurationBaseUrl));
        services.AddSingleton<IScenarioDraftGenerator, OfflineScenarioDraftGenerator>();

        // This generator keeps DefaultAzureCredential (Entra) on purpose, and it is a
        // documented divergence from the assistant provider in PlayerExperience, which
        // authenticates with an api-key resolved through SecretStore against the
        // OpenAI-compatible surface. The AzureOpenAIClient below builds Azure-style URLs
        // (/openai/deployments/…) that do not fit the /openai/v1 surface, and Entra without
        // a secret is legitimate here. Aligning both onto the same api-key resolution means
        // switching to the plain OpenAI client and reading the internal ai-providers route —
        // an evolution to make when scenario generation is verified end to end, not here.
        // See specs/platform-configuration.md § "Deux clients Azure, une seule résolution".
        string? endpoint = configuration["AzureFoundry:Endpoint"];
        string? deployment = configuration["AzureFoundry:Deployment"];
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deployment))
        {
            services.AddSingleton<IChatClient>(_ =>
                new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
                    .GetChatClient(deployment)
                    .AsIChatClient());
            services.AddSingleton<IScenarioDraftGenerator, AzureFoundryScenarioDraftGenerator>();
        }

        return services;
    }
}