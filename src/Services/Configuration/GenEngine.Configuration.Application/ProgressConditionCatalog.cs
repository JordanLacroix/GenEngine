namespace GenEngine.Configuration.Application;

/// <summary>
/// The kinds of cross-session condition a configuration block may declare.
/// </summary>
/// <remarks>
/// This vocabulary was introduced for the finale and is now shared with the conditional
/// rewards: both ask the same question — "has this player reached that milestone?" — of
/// the same recorded progression, so they must ask it in the same words. A second,
/// parallel condition model would drift, would have to be validated twice, and would
/// force a client to render two different progress shapes for the same idea.
/// <para>
/// Every kind is answerable from progression the player already carries — the mastery
/// recorded per (profile, scenario version) and the configurable statistics — so no
/// second progression store is ever introduced to evaluate one.
/// </para>
/// </remarks>
public enum ProgressConditionType
{
    /// <summary>At least <c>Threshold</c> distinct scenarios completed, optionally restricted to <c>ScenarioIds</c>.</summary>
    ScenariosCompleted,

    /// <summary>Every scenario attached to <c>CategoryId</c> completed.</summary>
    CategoryCompleted,

    /// <summary>Every category of <c>JourneyId</c> completed.</summary>
    JourneyCompleted,

    /// <summary>At least <c>Threshold</c> of the endings listed in <c>EndingIds</c> reached.</summary>
    EndingsReached,

    /// <summary>Average mastery over the scenarios in scope reaches <c>Threshold</c> percent.</summary>
    MasteryPercentReached,

    /// <summary>The player statistic <c>StatKey</c> has reached <c>Threshold</c> points.</summary>
    PlayerStatReached,
}

/// <summary>Whether every condition must hold, or any single one is enough.</summary>
public enum ProgressConditionMode { All, Any }

/// <summary>
/// One composable condition. Only the fields its <see cref="Type"/> uses are read; the
/// others stay null so the document remains additive.
/// </summary>
public sealed record ProgressConditionDefinition(
    Guid Id,
    ProgressConditionType Type,
    string Description,
    int? Threshold = null,
    Guid? CategoryId = null,
    Guid? JourneyId = null,
    IReadOnlyList<string>? EndingIds = null,
    IReadOnlyList<Guid>? ScenarioIds = null,
    string? StatKey = null);

/// <summary>Limits and validation shared by every block that declares conditions.</summary>
public static class ProgressConditionCatalog
{
    /// <summary>
    /// Upper bound on the conditions of a single block. It exists so a player can be
    /// shown what is left in one readable list rather than a wall of requirements.
    /// </summary>
    public const int MaximumConditions = 12;

    /// <summary>
    /// Validates a condition list against the content it references.
    /// </summary>
    /// <remarks>
    /// A condition that can never be satisfied is worse than a missing one: the player
    /// waits for something that will not come, and nothing in the runtime can tell them
    /// why. So an operand its type requires must be present, and every identifier it
    /// names must exist in the same document.
    /// <para>
    /// <paramref name="errorCode"/> is supplied by the caller so the finale and the
    /// rewards each report their own code — an operator must learn <em>which</em> block
    /// they broke, not merely that a condition somewhere is malformed.
    /// </para>
    /// </remarks>
    public static void Validate(
        IReadOnlyList<ProgressConditionDefinition> conditions,
        HashSet<Guid> categoryIds,
        HashSet<Guid> journeyIds,
        HashSet<string> statKeys,
        string errorCode)
    {
        foreach (ProgressConditionDefinition condition in conditions)
        {
            bool valid = condition.Type switch
            {
                ProgressConditionType.ScenariosCompleted => condition.Threshold is > 0,
                ProgressConditionType.CategoryCompleted => condition.CategoryId is Guid categoryId && categoryIds.Contains(categoryId),
                ProgressConditionType.JourneyCompleted => condition.JourneyId is Guid journeyId && journeyIds.Contains(journeyId),
                ProgressConditionType.EndingsReached => condition.EndingIds is { Count: > 0 }
                    && condition.EndingIds.All(static endingId => !string.IsNullOrWhiteSpace(endingId))
                    && condition.Threshold is null or > 0
                    && (condition.Threshold ?? condition.EndingIds.Count) <= condition.EndingIds.Count,
                ProgressConditionType.MasteryPercentReached => condition.Threshold is > 0 and <= 100,

                // The statistic is checked against the published catalogue, not against a
                // grammar: a threshold on a key the front does not declare would be a
                // condition no scenario could ever move.
                ProgressConditionType.PlayerStatReached => condition.Threshold is > 0
                    && !string.IsNullOrWhiteSpace(condition.StatKey)
                    && statKeys.Contains(condition.StatKey.Trim()),
                _ => false,
            };

            if (!valid)
            {
                throw new ConfigurationException(
                    errorCode,
                    "A condition must carry the operands its type requires and reference existing content.");
            }
        }
    }
}