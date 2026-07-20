namespace GenEngine.PlayerExperience.Application;

/// <summary>
/// The kinds of condition a published block may express. Mirrors the published
/// configuration, and is shared by the finale and the conditional rewards.
/// </summary>
public enum ProgressConditionKind
{
    ScenariosCompleted,
    CategoryCompleted,
    JourneyCompleted,
    EndingsReached,
    MasteryPercentReached,
    PlayerStatReached,
    Unknown,
}

/// <summary>Whether every condition must hold, or any single one is enough.</summary>
public enum ProgressMode { All, Any }

/// <summary>One published condition. Only the operands its <see cref="Kind"/> uses are read.</summary>
public sealed record ProgressCondition(
    Guid Id,
    ProgressConditionKind Kind,
    string Description,
    int? Threshold,
    Guid? CategoryId,
    Guid? JourneyId,
    IReadOnlyList<string> EndingIds,
    IReadOnlyList<Guid> ScenarioIds,
    string? StatKey = null);

/// <summary>Progress of one condition, so a client can show what is left rather than a locked door.</summary>
public sealed record ProgressConditionProgress(Guid Id, string Kind, string Description, bool Satisfied, int Current, int Target);

/// <summary>What the evaluator knows about one scenario the player has touched.</summary>
public sealed record ScenarioProgress(Guid ScenarioId, bool Completed, IReadOnlyList<string> EndingIds, int MasteryPercent);

/// <summary>
/// Everything the evaluator is allowed to read: the progression the player already
/// carries, and the catalogue that gives it meaning.
/// </summary>
/// <remarks>
/// Passing this as one value rather than four parameters is what makes adding a condition
/// kind a local change: a new kind reads a field of the snapshot instead of growing every
/// call site.
/// </remarks>
public sealed record ProgressSnapshot(
    IReadOnlyList<ScenarioProgress> Scenarios,
    IReadOnlyList<CategoryCatalogEntry> Categories,
    IReadOnlyList<JourneyCatalogEntry> Journeys,
    IReadOnlyDictionary<string, int> Stats);

/// <summary>
/// Pure, deterministic evaluation of composable conditions against progression the player
/// already carries: the mastery recorded per (profile, scenario version), and the
/// configurable statistics accumulated on the profile.
/// </summary>
/// <remarks>
/// This introduces no progression store of its own. Everything is answered from
/// <see cref="ProgressSnapshot"/>, which projects state written by other paths, so the
/// same inputs always give the same verdict and nothing here can be the reason a value
/// changed.
/// <para>
/// The evaluator only ever answers a question. There is no code path here that grants,
/// denies, locks or unlocks anything; deciding what a satisfied condition means is the
/// caller's business, which is precisely what lets the finale and the rewards share it.
/// </para>
/// </remarks>
public static class ProgressConditionEvaluator
{
    public static IReadOnlyList<ProgressConditionProgress> Evaluate(
        IReadOnlyList<ProgressCondition> conditions,
        ProgressSnapshot snapshot) =>
        conditions.Select(condition => Evaluate(condition, snapshot)).ToArray();

    /// <summary>
    /// Whether a set of already-evaluated conditions satisfies its mode. An empty set is
    /// never satisfied: a block declaring no condition must not fire on everyone.
    /// </summary>
    public static bool IsSatisfied(ProgressMode mode, IReadOnlyList<ProgressConditionProgress> conditions) =>
        conditions.Count > 0
        && (mode == ProgressMode.Any
            ? conditions.Any(static item => item.Satisfied)
            : conditions.All(static item => item.Satisfied));

    private static ProgressConditionProgress Evaluate(ProgressCondition condition, ProgressSnapshot snapshot)
    {
        IReadOnlyList<ScenarioProgress> progress = snapshot.Scenarios;
        (int current, int target) = condition.Kind switch
        {
            ProgressConditionKind.ScenariosCompleted => (
                CompletedScenarios(progress, condition.ScenarioIds).Count,
                Math.Max(1, condition.Threshold ?? 1)),

            ProgressConditionKind.CategoryCompleted => CategoryProgress(condition.CategoryId, progress, snapshot.Categories),

            ProgressConditionKind.JourneyCompleted => JourneyProgress(condition.JourneyId, progress, snapshot.Categories, snapshot.Journeys),

            ProgressConditionKind.EndingsReached => (
                condition.EndingIds.Count(endingId => progress.Any(item => item.EndingIds.Contains(endingId, StringComparer.Ordinal))),
                Math.Max(1, condition.Threshold ?? condition.EndingIds.Count)),

            ProgressConditionKind.MasteryPercentReached => (
                AverageMastery(progress, condition.ScenarioIds),
                Math.Clamp(condition.Threshold ?? 100, 1, 100)),

            // A statistic the player has never been granted has no entry: absence is
            // zero, exactly as it is on the profile itself. The value is read raw and
            // never clamped to a published ceiling here — a ceiling lowered after the
            // fact must not un-earn a reward the player already crossed.
            ProgressConditionKind.PlayerStatReached => (
                condition.StatKey is string key && snapshot.Stats.TryGetValue(key, out int value) ? value : 0,
                Math.Max(1, condition.Threshold ?? 1)),

            // An unknown condition kind can only come from a document published by a
            // newer engine. It is reported as never satisfied rather than ignored: a
            // silently dropped condition would let the block fire too early.
            _ => (0, 1),
        };

        return new ProgressConditionProgress(
            condition.Id,
            condition.Kind.ToString(),
            condition.Description,
            condition.Kind != ProgressConditionKind.Unknown && target > 0 && current >= target,
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
    /// as "done" would fire on a freshly seeded instance, before the operator has
    /// attached a single scenario.
    /// </summary>
    private static (int Current, int Target) CategoryProgress(Guid? categoryId, IReadOnlyList<ScenarioProgress> progress, IReadOnlyList<CategoryCatalogEntry> categories)
    {
        CategoryCatalogEntry? category = categoryId is Guid id ? categories.FirstOrDefault(item => item.Id == id) : null;
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
        IReadOnlyList<CategoryCatalogEntry> categories,
        IReadOnlyList<JourneyCatalogEntry> journeys)
    {
        JourneyCatalogEntry? journey = journeyId is Guid id ? journeys.FirstOrDefault(item => item.Id == id) : null;
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