namespace GenEngine.PlayerExperience.Application;

public sealed record FinalePlan(
    Guid Id,
    bool Enabled,
    string Title,
    string Summary,
    string Body,
    ProgressMode Mode,
    IReadOnlyList<ProgressCondition> Conditions,
    string? VisualUrl,
    string? MusicUrl,
    string? LabelKey);

/// <summary>
/// The player's standing with respect to the finale.
/// </summary>
/// <param name="Reached">Whether the threshold has ever been crossed.</param>
/// <param name="ReachedAt">When it was crossed. Never cleared, never re-stamped.</param>
public sealed record FinaleView(
    Guid Id,
    string Title,
    string Summary,
    string Body,
    bool Reached,
    DateTimeOffset? ReachedAt,
    string Mode,
    IReadOnlyList<ProgressConditionProgress> Conditions,
    string? VisualUrl,
    string? MusicUrl,
    string? LabelKey);

/// <summary>
/// The finale's reading of the shared condition evaluator.
/// </summary>
/// <remarks>
/// The evaluation itself lives in <see cref="ProgressConditionEvaluator"/>, which the
/// conditional rewards use too. What stays here is the one rule that is the finale's own:
/// a disabled finale is never satisfied, whatever its conditions say.
/// <para>
/// Crossing the finale is stamped once and grants nothing and forbids nothing. There is
/// no code path here that can make a scenario, a category or a journey unavailable.
/// </para>
/// </remarks>
public static class FinaleEvaluator
{
    public static IReadOnlyList<ProgressConditionProgress> Evaluate(FinalePlan plan, ProgressSnapshot snapshot) =>
        ProgressConditionEvaluator.Evaluate(plan.Conditions, snapshot);

    public static bool IsSatisfied(FinalePlan plan, IReadOnlyList<ProgressConditionProgress> conditions) =>
        plan.Enabled && ProgressConditionEvaluator.IsSatisfied(plan.Mode, conditions);
}