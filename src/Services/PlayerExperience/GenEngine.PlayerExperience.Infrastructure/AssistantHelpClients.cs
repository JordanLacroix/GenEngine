using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using GenEngine.PlayerExperience.Application;
using GenEngine.Secrets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using Polly;

namespace GenEngine.PlayerExperience.Infrastructure;

/// <summary>Mirror of Authoring's published-snapshot contract, duplicated on purpose: no service references another.</summary>
internal sealed record PublishedSnapshotContract(
    Guid Id,
    Guid ScenarioId,
    string FrontId,
    Guid? CategoryId,
    int Number,
    string SnapshotJson,
    string SnapshotHash);

/// <summary>
/// Reads author help out of a published scenario version, through Authoring's
/// internal route. Two properties matter here.
/// <para>
/// First, it is read-only and presentation-only: the snapshot is parsed with
/// <see cref="JsonDocument"/> into the few fields the assistant needs, and no
/// narrative type crosses the boundary — PlayerExperience does not reference the
/// engine and must not start now.
/// </para>
/// <para>
/// Second, it never throws. Contextual help is an overlay, so an unavailable or
/// malformed Authoring must degrade to the offline rules rather than fail the
/// player's request. Every failure path returns <c>null</c> and is logged.
/// </para>
/// </summary>
internal sealed class ScenarioHelpProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ScenarioHelpProvider> logger) : IScenarioHelpProvider
{
    private static readonly Action<ILogger, int, Guid, Exception?> RefusedByAuthoring =
        LoggerMessage.Define<int, Guid>(
            LogLevel.Information,
            new EventId(1, nameof(RefusedByAuthoring)),
            "Contextual help falls back offline: Authoring answered {StatusCode} for version {VersionId}.");

    private static readonly Action<ILogger, Guid, Exception?> AuthoringUnavailable =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(AuthoringUnavailable)),
            "Contextual help falls back offline: Authoring is unavailable for version {VersionId}.");

    public async Task<ScenarioHelpSnapshot?> GetAsync(
        Guid scenarioVersionId,
        string? nodeId,
        string? choiceId,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"/internal/scenario-versions/{scenarioVersionId}");
            request.Headers.Add("X-Internal-Key", configuration["InternalApi:Key"] ?? string.Empty);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
            {
                RefusedByAuthoring(logger, (int)response.StatusCode, scenarioVersionId, null);
                return null;
            }

            PublishedSnapshotContract? snapshot = await response.Content
                .ReadFromJsonAsync<PublishedSnapshotContract>(cancellationToken)
                .ConfigureAwait(false);
            return snapshot is null ? null : Project(snapshot.SnapshotJson, nodeId, choiceId);
        }
        // Degrading is the whole contract of this port: help is a presentation
        // layer, and Authoring being slow or down must never fail a player's
        // request. Enumerating exception types proved too narrow — the resilience
        // handler also raises TimeoutRejectedException and BrokenCircuitException,
        // neither of which is an HttpRequestException. Anything short of the
        // caller cancelling is therefore caught, and the offline path takes over.
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AuthoringUnavailable(logger, scenarioVersionId, null);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AuthoringUnavailable(logger, scenarioVersionId, exception);
            return null;
        }
    }

    /// <summary>
    /// Projects the published document onto <see cref="ScenarioHelpSnapshot"/>.
    /// Unknown or missing properties are tolerated: a version published before the
    /// help schema simply yields no help, which is exactly the offline case.
    /// </summary>
    private static ScenarioHelpSnapshot? Project(string snapshotJson, string? nodeId, string? choiceId)
    {
        using JsonDocument document = JsonDocument.Parse(snapshotJson);
        JsonElement root = document.RootElement;
        string title = GetString(root, "title") ?? string.Empty;

        if (!root.TryGetProperty("nodes", out JsonElement nodes) || nodes.ValueKind != JsonValueKind.Array)
        {
            return new ScenarioHelpSnapshot(title, nodeId, null, [], null, null);
        }

        string? targetNodeId = nodeId ?? GetString(root, "initialNodeId");
        JsonElement? node = nodes.EnumerateArray()
            .Cast<JsonElement?>()
            .FirstOrDefault(item => string.Equals(GetString(item!.Value, "id"), targetNodeId, StringComparison.Ordinal));
        if (node is not JsonElement current)
        {
            return new ScenarioHelpSnapshot(title, targetNodeId, null, [], null, null);
        }

        JsonElement[] choices = CollectChoices(current).ToArray();
        JsonElement? choice = choiceId is null
            ? null
            : choices.Cast<JsonElement?>()
                .FirstOrDefault(item => string.Equals(GetString(item!.Value, "id"), choiceId, StringComparison.Ordinal));

        return new ScenarioHelpSnapshot(
            title,
            targetNodeId,
            GetString(current, "text"),
            choices.Select(item => GetString(item, "text") ?? string.Empty)
                .Where(static text => text.Length > 0)
                .ToArray(),
            ReadHelp(current),
            choice is JsonElement found ? ReadHelp(found) : null);
    }

    /// <summary>A node carries choices directly, and/or inside its typed choice-set interactions.</summary>
    private static IEnumerable<JsonElement> CollectChoices(JsonElement node)
    {
        if (node.TryGetProperty("choices", out JsonElement direct) && direct.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement choice in direct.EnumerateArray())
            {
                yield return choice;
            }
        }

        if (!node.TryGetProperty("interactions", out JsonElement interactions)
            || interactions.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement interaction in interactions.EnumerateArray())
        {
            if (interaction.TryGetProperty("choices", out JsonElement nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement choice in nested.EnumerateArray())
                {
                    yield return choice;
                }
            }
        }
    }

    private static AuthorHelpView? ReadHelp(JsonElement owner)
    {
        if (!owner.TryGetProperty("help", out JsonElement help) || help.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        AuthorHelpView view = new(
            GetString(help, "hint"),
            GetString(help, "objective"),
            GetString(help, "consequence"),
            GetString(help, "blocker"));
        return view.IsEmpty ? null : view;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// The default assistant provider: none. It reports itself unconfigured, so
/// contextual help resolves entirely from the published content and the offline
/// rules. This is what keeps AI genuinely optional — Docker, CI and the playable
/// journey never need a provider.
/// <para>
/// A real provider ships as another <see cref="IAssistantAiClient"/> registered in
/// this layer. It resolves its own credentials locally, exactly as Authoring does
/// for Azure AI Foundry; the published configuration document this service reads
/// has its <c>secretReference</c> redacted by Configuration, so no secret can
/// reach the assistant through the catalog.
/// </para>
/// </summary>
internal sealed class OfflineAssistantAiClient : IAssistantAiClient
{
    public bool IsConfigured => false;

    public Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}

/// <summary>
/// Reads the AI provider connection details of a front from Configuration's internal,
/// key-guarded route. Like <see cref="ScenarioHelpProvider"/>, it never throws: an
/// unavailable or malformed Configuration degrades to an empty list, and the assistant
/// falls back to the offline rules.
/// </summary>
internal sealed class ConfigurationAssistantProviderCatalog(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ConfigurationAssistantProviderCatalog> logger) : IAssistantProviderCatalog
{
    private static readonly Action<ILogger, string, Exception?> CatalogUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(CatalogUnavailable)),
            "Assistant falls back offline: the AI provider catalog is unavailable for front {FrontId}.");

    /// <summary>Mirror of Configuration's <c>AiProviderConnectionView</c>; the enum arrives as its name.</summary>
    private sealed record ProviderContract(
        Guid Id,
        string Name,
        string Type,
        bool Enabled,
        string Endpoint,
        string Deployment,
        string Authentication,
        string? SecretReference,
        IReadOnlyList<string>? Capabilities);

    public async Task<IReadOnlyList<AssistantAiProvider>> GetAsync(string frontId, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/internal/ai-providers/{Uri.EscapeDataString(frontId)}");
            request.Headers.Add("X-Internal-Key", configuration["InternalApi:Key"] ?? string.Empty);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                CatalogUnavailable(logger, frontId, null);
                return [];
            }

            List<ProviderContract>? contracts = await response.Content
                .ReadFromJsonAsync<List<ProviderContract>>(cancellationToken)
                .ConfigureAwait(false);
            return contracts is null
                ? []
                : contracts
                    .Select(static contract => new AssistantAiProvider(
                        contract.Name,
                        contract.Type,
                        contract.Enabled,
                        contract.Endpoint,
                        contract.Deployment,
                        contract.SecretReference))
                    .ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CatalogUnavailable(logger, frontId, null);
            return [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CatalogUnavailable(logger, frontId, exception);
            return [];
        }
    }
}

/// <summary>
/// The real assistant provider: an Azure AI Foundry deployment exposed over the
/// OpenAI-compatible surface (<c>/openai/v1</c>). The standard chat-completions wire
/// format is used directly, so no SDK dependency is introduced.
/// <para>
/// Selection is per front and comes from the published <c>aiProviders[]</c> entry:
/// the enabled Azure AI Foundry provider gives the endpoint, the deployment (the Azure
/// deployment name, sent as <c>model</c>) and the opaque secret reference. The reference
/// is resolved locally through <see cref="ISecretStore"/>; no secret ever travels over
/// the configuration catalog.
/// </para>
/// <para>
/// It authenticates with the <c>api-key</c> header and also sends the same value as a
/// bearer token, so both an Azure Foundry resource and a plain OpenAI-compatible endpoint
/// are satisfied. The <c>api-version</c> query parameter is only added when configured.
/// </para>
/// <para>
/// The invariant is graceful degradation: a missing provider, a disabled one, an
/// unresolved secret, an HTTP error or a timeout all return <c>null</c>, and contextual
/// help falls back to the offline rules — never an exception surfacing to the player.
/// </para>
/// </summary>
internal sealed class AzureFoundryAssistantAiClient(
    IAssistantProviderCatalog catalog,
    ISecretStore secretStore,
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AzureFoundryAssistantAiClient> logger) : IAssistantAiClient
{
    private const string AzureAiFoundryType = "AzureAiFoundry";

    private static readonly Action<ILogger, string, Exception?> NoUsableProvider =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, nameof(NoUsableProvider)),
            "Assistant resolves offline: no usable AI provider is published for front {FrontId}.");

    private static readonly Action<ILogger, string, Exception?> SecretUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(SecretUnavailable)),
            "Assistant resolves offline: the AI provider secret could not be resolved for front {FrontId}.");

    private static readonly Action<ILogger, int, string, Exception?> ProviderRefused =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(3, nameof(ProviderRefused)),
            "Assistant resolves offline: the AI provider answered {StatusCode} for front {FrontId}.");

    private static readonly Action<ILogger, string, Exception?> ProviderUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, nameof(ProviderUnavailable)),
            "Assistant resolves offline: the AI provider is unavailable for front {FrontId}.");

    /// <summary>
    /// True because a real provider is wired in this deployment. Whether a given front
    /// actually has a usable provider is decided per request in <see cref="GenerateAsync"/>,
    /// which returns <c>null</c> to degrade offline when it does not.
    /// </summary>
    public bool IsConfigured => true;

    public async Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            IReadOnlyList<AssistantAiProvider> providers = await catalog
                .GetAsync(context.FrontId, cancellationToken)
                .ConfigureAwait(false);
            AssistantAiProvider? provider = providers.FirstOrDefault(static candidate =>
                candidate.Enabled
                && string.Equals(candidate.Type, AzureAiFoundryType, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(candidate.Endpoint)
                && !string.IsNullOrWhiteSpace(candidate.Deployment)
                && !string.IsNullOrWhiteSpace(candidate.SecretReference));
            if (provider is null)
            {
                NoUsableProvider(logger, context.FrontId, null);
                return null;
            }

            SecretResolution resolution = await secretStore
                .ResolveAsync(provider.SecretReference!, cancellationToken)
                .ConfigureAwait(false);
            if (!resolution.Succeeded || !resolution.Value.HasValue)
            {
                SecretUnavailable(logger, context.FrontId, null);
                return null;
            }

            string key = resolution.Value.Reveal();
            string endpoint = provider.Endpoint.TrimEnd('/');
            string? apiVersion = configuration["GENENGINE_AI_AZURE_FOUNDRY_API_VERSION"];
            string url = string.IsNullOrWhiteSpace(apiVersion)
                ? $"{endpoint}/chat/completions"
                : $"{endpoint}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";

            ChatCompletionRequest payload = new(
                provider.Deployment,
                [
                    new ChatMessagePayload("system", BuildSystemPrompt(context)),
                    new ChatMessagePayload("user", BuildUserPrompt(context)),
                ],
                0.4,
                400);

            using HttpRequestMessage request = new(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Add("api-key", key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                ProviderRefused(logger, (int)response.StatusCode, context.FrontId, null);
                return null;
            }

            ChatCompletionResponse? completion = await response.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken)
                .ConfigureAwait(false);
            string? content = completion?.Choices is { Count: > 0 } choices
                ? choices[0].Message?.Content
                : null;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ProviderUnavailable(logger, context.FrontId, null);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ProviderUnavailable(logger, context.FrontId, exception);
            return null;
        }
    }

    private static string BuildSystemPrompt(AssistantAiContext context)
    {
        string guidance = context.Modality switch
        {
            HelpModality.Objective => "Reformule l'objectif courant sans dévoiler d'option.",
            HelpModality.Hint => "Donne un indice discret qui ne nomme jamais la « bonne » réponse.",
            HelpModality.Consequence => "Rappelle une conséquence que le joueur connaît déjà.",
            HelpModality.Blocker => "Explique pourquoi une option visible reste indisponible.",
            _ => "Donne un indice discret et bienveillant.",
        };

        return $"Tu es {context.FamiliarName}, un compagnon d'aide dans un jeu narratif. "
            + $"Réponds en français, en une à trois phrases, sur un ton {context.FamiliarTone} "
            + $"et dans un style {context.FamiliarWritingStyle}. {guidance} "
            + "Ne révèle jamais un contenu que le joueur n'a pas encore vu.";
    }

    private static string BuildUserPrompt(AssistantAiContext context)
    {
        System.Text.StringBuilder prompt = new();
        prompt.Append("Scénario : ").AppendLine(context.ScenarioTitle);
        if (!string.IsNullOrWhiteSpace(context.NodeText))
        {
            prompt.Append("Étape : ").AppendLine(context.NodeText);
        }

        if (context.VisibleChoiceTexts.Count > 0)
        {
            prompt.AppendLine("Options visibles :");
            foreach (string choice in context.VisibleChoiceTexts)
            {
                prompt.Append("- ").AppendLine(choice);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.AuthorHelpText))
        {
            prompt.Append("Aide prévue par l'auteur : ").AppendLine(context.AuthorHelpText);
        }

        if (context.AlreadyExplored)
        {
            prompt.AppendLine("Le joueur a déjà emprunté ce chemin.");
        }

        prompt.Append("Le joueur demande de l'aide dans ce contexte : ").Append(context.Context);
        return prompt.ToString();
    }

    private sealed record ChatCompletionRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("model")] string Model,
        [property: System.Text.Json.Serialization.JsonPropertyName("messages")] IReadOnlyList<ChatMessagePayload> Messages,
        [property: System.Text.Json.Serialization.JsonPropertyName("temperature")] double Temperature,
        [property: System.Text.Json.Serialization.JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessagePayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("role")] string Role,
        [property: System.Text.Json.Serialization.JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(
        [property: System.Text.Json.Serialization.JsonPropertyName("message")] ChatMessageContent? Message);

    private sealed record ChatMessageContent(
        [property: System.Text.Json.Serialization.JsonPropertyName("content")] string? Content);
}

public static class AssistantHelpResilience
{
    /// <summary>
    /// Resilience for the outbound call to Authoring's internal snapshot route,
    /// aligned with the Play → Authoring policy. The call is an idempotent GET, so
    /// a bounded jittered retry is safe. Budgets are tighter than Play's: help is
    /// an overlay a player is waiting on, and degrading to the offline rules is
    /// always preferable to making them wait.
    /// </summary>
    public static void Configure(HttpStandardResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    }
}