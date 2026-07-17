using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

public static class NarrativeVersions
{
    public const int Schema = 1;
    public const string Runtime = "1.0.0";
    public const string HashFormat = "sha256-canonical-json-v1";
    public const string RngAlgorithm = "splitmix64-v1";
}

public sealed record ScenarioDocument(
    int SchemaVersion,
    string Title,
    string InitialNodeId,
    IReadOnlyList<NarrativeNode> Nodes);

public sealed record NarrativeNode(
    string Id,
    string Text,
    ConditionExpression? EnterCondition,
    IReadOnlyList<LocalGameEffect> OnEnterEffects,
    IReadOnlyList<NarrativeChoice> Choices,
    bool IsEnding = false);

public sealed record NarrativeChoice(
    string Id,
    string Text,
    string TargetNodeId,
    ConditionExpression? Condition,
    IReadOnlyList<LocalGameEffect> Effects);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AlwaysCondition), "always")]
[JsonDerivedType(typeof(AllCondition), "all")]
[JsonDerivedType(typeof(AnyCondition), "any")]
[JsonDerivedType(typeof(NotCondition), "not")]
[JsonDerivedType(typeof(VariableEqualsCondition), "variableEquals")]
[JsonDerivedType(typeof(VariableAtLeastCondition), "variableAtLeast")]
[JsonDerivedType(typeof(HasItemCondition), "hasItem")]
[JsonDerivedType(typeof(HasEvidenceCondition), "hasEvidence")]
[JsonDerivedType(typeof(RelationAtLeastCondition), "relationAtLeast")]
[JsonDerivedType(typeof(HasRewardCondition), "hasReward")]
[JsonDerivedType(typeof(VisitedNodeCondition), "visitedNode")]
public abstract record ConditionExpression;

public sealed record AlwaysCondition : ConditionExpression;

public sealed record AllCondition(IReadOnlyList<ConditionExpression> Conditions) : ConditionExpression;

public sealed record AnyCondition(IReadOnlyList<ConditionExpression> Conditions) : ConditionExpression;

public sealed record NotCondition(ConditionExpression Condition) : ConditionExpression;

public sealed record VariableEqualsCondition(string Name, int Value) : ConditionExpression;

public sealed record VariableAtLeastCondition(string Name, int Value) : ConditionExpression;

public sealed record HasItemCondition(string Item) : ConditionExpression;

public sealed record HasEvidenceCondition(string Evidence) : ConditionExpression;

public sealed record RelationAtLeastCondition(string Character, int Value) : ConditionExpression;

public sealed record HasRewardCondition(string Reward) : ConditionExpression;

public sealed record VisitedNodeCondition(string NodeId) : ConditionExpression;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AssignEffect), "assign")]
[JsonDerivedType(typeof(IncrementEffect), "increment")]
[JsonDerivedType(typeof(CollectEffect), "collect")]
[JsonDerivedType(typeof(RemoveItemEffect), "removeItem")]
[JsonDerivedType(typeof(DiscoverEvidenceEffect), "discoverEvidence")]
[JsonDerivedType(typeof(ChangeRelationEffect), "changeRelation")]
[JsonDerivedType(typeof(GrantRewardEffect), "grantReward")]
[JsonDerivedType(typeof(RecordNotableEventEffect), "recordNotableEvent")]
[JsonDerivedType(typeof(ScheduleEffect), "schedule")]
public abstract record LocalGameEffect;

public sealed record AssignEffect(string Name, int Value) : LocalGameEffect;

public sealed record IncrementEffect(string Name, int Amount) : LocalGameEffect;

public sealed record CollectEffect(string Item) : LocalGameEffect;

public sealed record RemoveItemEffect(string Item) : LocalGameEffect;

public sealed record DiscoverEvidenceEffect(string Evidence) : LocalGameEffect;

public sealed record ChangeRelationEffect(string Character, int Amount) : LocalGameEffect;

public sealed record GrantRewardEffect(string Reward) : LocalGameEffect;

public sealed record RecordNotableEventEffect(string Label, string? Scope = null) : LocalGameEffect;

public sealed record ScheduleEffect(int Turns, LocalGameEffect Effect) : LocalGameEffect;

public sealed record ScheduledEffect(int DueTurn, LocalGameEffect Effect);

public sealed record WorldState(
    Dictionary<string, int> Variables,
    HashSet<string> Inventory,
    HashSet<string> VisitedNodes,
    List<ScheduledEffect> ScheduledEffects)
{
    public HashSet<string> Evidence { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Relations { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> Rewards { get; init; } = new(StringComparer.Ordinal);

    public List<ChoiceHistoryEntry> ChoiceHistory { get; init; } = [];

    public List<JournalEntry> Journal { get; init; } = [];

    public static WorldState Empty() => new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        []);
}

public sealed record ChoiceHistoryEntry(string NodeId, string ChoiceId, int Turn);

public sealed record JournalEntry(string Label, string? Scope, int Turn);

[JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))]
public enum SessionStatus
{
    AwaitingInput,
    Paused,
    Completed,
    Abandoned,
}

public sealed record GameState(
    string CurrentNodeId,
    int Turn,
    SessionStatus Status,
    WorldState World);

public sealed record CurrentStep(
    string NodeId,
    string Text,
    SessionStatus Status,
    IReadOnlyList<VisibleChoice> Choices,
    int Turn);

public sealed record VisibleChoice(string Id, string Text);

public sealed record ConditionEvaluation(
    string Operator,
    bool Result,
    string Explanation,
    IReadOnlyList<ConditionEvaluation> Children);

public sealed record ChoiceAvailability(
    string Id,
    string Text,
    bool IsAvailable,
    ConditionEvaluation Evaluation);

public sealed record SimulationDeadEnd(
    string NodeId,
    int Turn,
    string Reason);

public sealed record SimulationReport(
    int ExploredStates,
    IReadOnlyList<string> EndingNodeIds,
    IReadOnlyList<SimulationDeadEnd> DeadEnds,
    bool StateBudgetExceeded);

public sealed record ValidationIssue(string Code, string Path, string Message, bool IsError);

public sealed record ValidationReport(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(static issue => !issue.IsError);
}

public sealed class NarrativeException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}