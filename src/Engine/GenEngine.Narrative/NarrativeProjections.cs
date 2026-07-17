namespace GenEngine.Narrative;

public sealed record PlayerCollectionProjection(
    IReadOnlyList<string> Inventory,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Rewards);

public sealed record PlayerSummaryProjection(
    string CurrentNodeId,
    SessionStatus Status,
    int Turn,
    int LogicalDay,
    int VisitedNodeCount,
    int ChoiceCount,
    int InteractionCount,
    int PendingDeferredEffectCount,
    IReadOnlyDictionary<string, int> Variables,
    IReadOnlyDictionary<string, int> Relations,
    IReadOnlyDictionary<string, int> Characteristics);

public sealed record PlayerProjection(
    PlayerSummaryProjection Summary,
    PlayerCollectionProjection Collection,
    IReadOnlyList<JournalEntry> Journal);

public static class PlayerProjectionBuilder
{
    public static PlayerProjection Build(GameState state) => new(
        new PlayerSummaryProjection(
            state.CurrentNodeId,
            state.Status,
            state.Turn,
            state.World.LogicalDay,
            state.World.VisitedNodes.Count,
            state.World.ChoiceHistory.Count,
            state.World.InteractionHistory.Count,
            state.World.ScheduledEffects.Count,
            CopySorted(state.World.Variables),
            CopySorted(state.World.Relations),
            CopySorted(state.World.Characteristics)),
        new PlayerCollectionProjection(
            Sort(state.World.Inventory),
            Sort(state.World.Evidence),
            Sort(state.World.Rewards)),
        [.. state.World.Journal]);

    private static string[] Sort(IEnumerable<string> values) =>
        values.Order(StringComparer.Ordinal).ToArray();

    private static Dictionary<string, int> CopySorted(IReadOnlyDictionary<string, int> values) =>
        values
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
}