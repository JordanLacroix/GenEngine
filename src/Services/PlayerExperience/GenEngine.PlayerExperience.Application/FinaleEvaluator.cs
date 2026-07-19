namespace GenEngine.PlayerExperience.Application;

/// <summary>The kinds of trigger a finale condition may express. Mirrors the published configuration.</summary>
public enum FinaleConditionKind
{
    ScenariosCompleted,
    CategoryCompleted,
    JourneyCompleted,
    EndingsReached,
    MasteryPercentReached,
    Unknown,
}

public enum FinaleMode { All, Any }

public sealed record FinaleCondition(
    Guid Id,
    FinaleConditionKind Kind,
    string Description,
    int? Threshold,
    Guid? CategoryId,
    Guid? JourneyId,
    IReadOnlyList<string> EndingIds,
    IReadOnlyList<Guid> ScenarioIds);

public sealed record FinalePlan(
    Guid Id,
    bool Enabled,
    string Title,
    string Summary,
    string Body,
    FinaleMode Mode,
    IReadOnlyList<FinaleCondition> Conditions,
    string? VisualUrl,
    string? MusicUrl,
    string? LabelKey);

/// <summary>Minimal catalogue shape needed to say which scenarios a category holds.</summary>
public sealed record CategoryPlan(Guid Id, IReadOnlyList<Guid> ScenarioIds);

/// <summary>Minimal catalogue shape needed to say which categories a journey holds.</summary>
public sealed record JourneyPlan(Guid Id, IReadOnlyList<Guid> CategoryIds);

/// <summary>Progress of one condition, so a client can show what is left rather than a locked door.</summary>
public sealed record FinaleConditionProgress(Guid Id, string Kind, string Description, bool Satisfied, int Current, int Target);

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
    IReadOnlyList<FinaleConditionProgress> Conditions,
    string? VisualUrl,
    string? MusicUrl,
    string? LabelKey);

/// <summary>
/// Pure, deterministic evaluation of the finale conditions against the mastery already
/// recorded per (profile, scenario version).
/// </summary>
/// <remarks>
/// This introduces no second progression store: everything is answered from
/// <see cref="ScenarioProgress"/>, which projects the existing <c>ScenarioMastery</c>.
/// Evaluation is a pure function of that projection and the published plan, so the same
/// inputs always give the same verdict.
///
/// Crossing the finale is stamped once and grants nothing and forbids nothing. There is
/// no code path here that can make a scenario, a category or a journey unavailable — the
/// evaluator only ever answers a question.
/// </remarks>
public static class FinaleEvaluator
{
    /// <summary>What the evaluator needs to know about one scenario the player has touched.</summary>
    public sealed record ScenarioProgress(Guid ScenarioId, bool Completed, IReadOnlyList<string> EndingIds, int MasteryPercent);

    public static IReadOnlyList<FinaleConditionProgress> Evaluate(
        FinalePlan plan,
        IReadOnlyList<ScenarioProgress> progress,
        IReadOnlyList<CategoryPlan> categories,
        IReadOnlyList<JourneyPlan> journeys) =>
        plan.Conditions.Select(condition => Evaluate(condition, progress, categories, journeys)).ToArray();

    public static bool IsSatisfied(FinalePlan plan, IReadOnlyList<FinaleConditionProgress> conditions) =>
        plan.Enabled
        && conditions.Count > 0
        && (plan.Mode == FinaleMode.Any ? conditions.Any(static item => item.Satisfied) : conditions.All(static item => item.Satisfied));

    private static FinaleConditionProgress Evaluate(
        FinaleCondition condition,
        IReadOnlyList<ScenarioProgress> progress,
        IReadOnlyList<CategoryPlan> categories,
        IReadOnlyList<JourneyPlan> journeys)
    {
        (int current, int target) = condition.Kind switch
        {
            FinaleConditionKind.ScenariosCompleted => (
                CompletedScenarios(progress, condition.ScenarioIds).Count,
                Math.Max(1, condition.Threshold ?? 1)),

            FinaleConditionKind.CategoryCompleted => CategoryProgress(condition.CategoryId, progress, categories),

            FinaleConditionKind.JourneyCompleted => JourneyProgress(condition.JourneyId, progress, categories, journeys),

            FinaleConditionKind.EndingsReached => (
                condition.EndingIds.Count(endingId => progress.Any(item => item.EndingIds.Contains(endingId, StringComparer.Ordinal))),
                Math.Max(1, condition.Threshold ?? condition.EndingIds.Count)),

            FinaleConditionKind.MasteryPercentReached => (
                AverageMastery(progress, condition.ScenarioIds),
                Math.Clamp(condition.Threshold ?? 100, 1, 100)),

            // An unknown condition type can only come from a document published by a
            // newer engine. It is reported as never satisfied rather than ignored: a
            // silently dropped condition would let the finale fire too early.
            _ => (0, 1),
        };

        return new FinaleConditionProgress(
            condition.Id,
            condition.Kind.ToString(),
            condition.Description,
            condition.Kind != FinaleConditionKind.Unknown && target > 0 && current >= target,
            current,
            target);
    }

    private static HashSet<Guid> CompletedScenarios(IReadOnlyList<ScenarioProgress> progress, IReadOnlyList<Guid> scope)
    {
        IEnumerable<ScenarioProgress> completed = progress.Where(static item => item.Completed);
        if (scope.Count > 0)
        {
            HashSet<Guid> allowed = scope.ToHashSet();
            completed = completed.Where(item => allowed.Contains(item.ScenarioId));
        }

        return completed.Select(static item => item.ScenarioId).ToHashSet();
    }

    /// <summary>
    /// A category with no attached scenario is never complete. Treating "nothing to do"
    /// as "done" would fire the finale on a freshly seeded instance, before the operator
    /// has attached a single scenario.
    /// </summary>
    private static (int Current, int Target) CategoryProgress(Guid? categoryId, IReadOnlyList<ScenarioProgress> progress, IReadOnlyList<CategoryPlan> categories)
    {
        CategoryPlan? category = categoryId is Guid id ? categories.FirstOrDefault(item => item.Id == id) : null;
        if (category is null || category.ScenarioIds.Count == 0) return (0, 1);
        HashSet<Guid> completed = CompletedScenarios(progress, category.ScenarioIds);
        return (completed.Count, category.ScenarioIds.Distinct().Count());
    }

    /// <summary>
    /// A journey is measured in scenarios, not in categories, so its progress bar moves
    /// with every scenario closed instead of jumping in steps.
    /// </summary>
    private static (int Current, int Target) JourneyProgress(
        Guid? journeyId,
        IReadOnlyList<ScenarioProgress> progress,
        IReadOnlyList<CategoryPlan> categories,
        IReadOnlyList<JourneyPlan> journeys)
    {
        JourneyPlan? journey = journeyId is Guid id ? journeys.FirstOrDefault(item => item.Id == id) : null;
        if (journey is null) return (0, 1);
        Guid[] scenarioIds = journey.CategoryIds
            .SelectMany(categoryId => categories.FirstOrDefault(item => item.Id == categoryId)?.ScenarioIds ?? [])
            .Distinct()
            .ToArray();
        if (scenarioIds.Length == 0) return (0, 1);
        return (CompletedScenarios(progress, scenarioIds).Count, scenarioIds.Length);
    }

    private static int AverageMastery(IReadOnlyList<ScenarioProgress> progress, IReadOnlyList<Guid> scope)
    {
        IEnumerable<ScenarioProgress> considered = progress;
        if (scope.Count > 0)
        {
            HashSet<Guid> allowed = scope.ToHashSet();
            considered = considered.Where(item => allowed.Contains(item.ScenarioId));
        }

        ScenarioProgress[] items = considered.ToArray();
        if (items.Length == 0) return 0;
        // Integer arithmetic on purpose: the verdict must not depend on floating point.
        return items.Sum(static item => item.MasteryPercent) / (scope.Count > 0 ? scope.Distinct().Count() : items.Length);
    }
}