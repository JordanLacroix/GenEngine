using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using GenEngine.PlayerExperience.Application;

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