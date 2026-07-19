using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

public static class NarrativeVersions
{
    public const int Schema = 1;
    public const int LatestSchema = 5;

    /// <summary>Schema that introduced typed step interactions.</summary>
    public const int InteractionsSchema = 2;

    /// <summary>Schema that introduced optional media references on steps and choices.</summary>
    public const int MediaSchema = 3;

    /// <summary>Schema that introduced skippable (optional) step interactions.</summary>
    public const int OptionalInteractionsSchema = 4;

    /// <summary>Schema that introduced optional author-written help on steps and choices.</summary>
    public const int AuthorHelpSchema = 5;
    public const string Runtime = "1.0.0";
    public const string HashFormat = "sha256-canonical-json-v1";
    public const string RngAlgorithm = "splitmix64-v1";
}

public sealed record ScenarioDocument(
    int SchemaVersion,
    string Title,
    string InitialNodeId,
    IReadOnlyList<NarrativeNode> Nodes);

/// <summary>
/// Optional media attached to a narrative step. These are references only: the
/// engine never resolves, downloads or plays them. A step must stay fully
/// understandable without any of them, so a client is free to ignore the whole
/// record — for accessibility, or because the operator disabled media.
/// </summary>
public sealed record StepMedia
{
    /// <summary>Absolute HTTPS URL of an illustration for the step.</summary>
    public string? VisualUrl { get; init; }

    /// <summary>Text alternative for <see cref="VisualUrl"/>, never a substitute for the narrative text.</summary>
    public string? VisualDescription { get; init; }

    /// <summary>Absolute HTTPS URL of an ambience or musical bed for the step.</summary>
    public string? SoundUrl { get; init; }
}

/// <summary>
/// Optional media attached to a choice: a short interaction signature and an
/// animation cue. The cue is an opaque identifier that a client maps to its own
/// animation; the engine gives it no meaning and never derives state from it.
/// </summary>
public sealed record ChoiceMedia
{
    /// <summary>Absolute HTTPS URL of a short sound played when the choice is taken.</summary>
    public string? SoundUrl { get; init; }

    /// <summary>Client-side animation identifier, for example <c>choice-confirm</c>.</summary>
    public string? AnimationCue { get; init; }
}

/// <summary>
/// Optional help written by the author for a step or a choice. It is pure
/// presentation: the engine never reads it, never branches on it and never
/// derives state from it, so a step must stay fully playable when a client — or
/// the assistant policy — ignores the whole record.
/// <para>
/// Each property is one <em>modality</em> of help. They are kept separate rather
/// than merged into a single blob so a policy can serve only what the player's
/// help level allows: a discreet hint is not the same disclosure as spelling out
/// a blocking condition.
/// </para>
/// <para>
/// Every property is nullable, and the record itself is nullable on its owner, so
/// a scenario authored before schema
/// <see cref="NarrativeVersions.AuthorHelpSchema"/> serializes to exactly the same
/// canonical bytes and keeps its published hash.
/// </para>
/// </summary>
public sealed record AuthorHelp
{
    /// <summary>A discreet nudge that does not name the answer.</summary>
    public string? Hint { get; init; }

    /// <summary>The current objective restated in the player's terms.</summary>
    public string? Objective { get; init; }

    /// <summary>Consequences the player is already supposed to know about.</summary>
    public string? Consequence { get; init; }

    /// <summary>Why a visible option is currently unavailable.</summary>
    public string? Blocker { get; init; }
}

public sealed record NarrativeNode(
    string Id,
    string Text,
    ConditionExpression? EnterCondition,
    IReadOnlyList<LocalGameEffect> OnEnterEffects,
    IReadOnlyList<NarrativeChoice> Choices,
    bool IsEnding = false)
{
    public IReadOnlyList<StepInteraction>? Interactions { get; init; }

    public StepMedia? Media { get; init; }

    /// <summary>Author-written help for this step. See <see cref="AuthorHelp"/>.</summary>
    public AuthorHelp? Help { get; init; }
}

public sealed record NarrativeChoice(
    string Id,
    string Text,
    string TargetNodeId,
    ConditionExpression? Condition,
    IReadOnlyList<LocalGameEffect> Effects)
{
    public ChoiceMedia? Media { get; init; }

    /// <summary>Author-written help for this choice. See <see cref="AuthorHelp"/>.</summary>
    public AuthorHelp? Help { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NarrationInteraction), "narration")]
[JsonDerivedType(typeof(ChoiceSetInteraction), "choiceSet")]
[JsonDerivedType(typeof(QuizInteraction), "quiz")]
[JsonDerivedType(typeof(CharacteristicGateInteraction), "characteristicGate")]
[JsonDerivedType(typeof(FreeTextInteraction), "freeText")]
public abstract record StepInteraction(string Id)
{
    /// <summary>
    /// When <c>true</c>, the player may leave the node without playing this
    /// interaction, by taking one of the node's exit choices offered alongside it.
    /// The property is nullable so an unset flag is omitted from the serialized
    /// document: a scenario authored before schema
    /// <see cref="NarrativeVersions.OptionalInteractionsSchema"/> keeps exactly the
    /// same canonical bytes, hence the same published hash.
    /// </summary>
    public bool? IsOptional { get; init; }
}

public sealed record NarrationInteraction(
    string Id,
    string Text,
    IReadOnlyList<LocalGameEffect> ContinueEffects) : StepInteraction(Id);

public sealed record ChoiceSetInteraction(
    string Id,
    string Prompt,
    IReadOnlyList<NarrativeChoice> Choices) : StepInteraction(Id);

public sealed record QuizInteraction(
    string Id,
    string Prompt,
    IReadOnlyList<QuizAnswer> Answers,
    string CorrectAnswerId,
    IReadOnlyList<LocalGameEffect> CorrectEffects,
    IReadOnlyList<LocalGameEffect> IncorrectEffects) : StepInteraction(Id);

public sealed record QuizAnswer(string Id, string Text);

public sealed record CharacteristicGateInteraction(
    string Id,
    ConditionExpression Condition,
    string SatisfiedTargetNodeId,
    string FailedTargetNodeId,
    IReadOnlyList<LocalGameEffect> SatisfiedEffects,
    IReadOnlyList<LocalGameEffect> FailedEffects) : StepInteraction(Id);

public sealed record FreeTextInteraction(
    string Id,
    string Prompt,
    IReadOnlyList<string> RequiredTerms,
    int MinimumMatches,
    IReadOnlyList<LocalGameEffect> AcceptedEffects,
    IReadOnlyList<LocalGameEffect> RejectedEffects) : StepInteraction(Id);

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
[JsonDerivedType(typeof(CharacteristicAtLeastCondition), "characteristicAtLeast")]
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

public sealed record CharacteristicAtLeastCondition(string Name, int Value) : ConditionExpression;

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
[JsonDerivedType(typeof(AdvanceLogicalTimeEffect), "advanceLogicalTime")]
[JsonDerivedType(typeof(EmitExternalEventEffect), "emitExternalEvent")]
[JsonDerivedType(typeof(SetCharacteristicEffect), "setCharacteristic")]
[JsonDerivedType(typeof(ChangeCharacteristicEffect), "changeCharacteristic")]
public abstract record LocalGameEffect;

public sealed record AssignEffect(string Name, int Value) : LocalGameEffect;

public sealed record IncrementEffect(string Name, int Amount) : LocalGameEffect;

public sealed record CollectEffect(string Item) : LocalGameEffect;

public sealed record RemoveItemEffect(string Item) : LocalGameEffect;

public sealed record DiscoverEvidenceEffect(string Evidence) : LocalGameEffect;

public sealed record ChangeRelationEffect(string Character, int Amount) : LocalGameEffect;

public sealed record GrantRewardEffect(string Reward) : LocalGameEffect;

public sealed record RecordNotableEventEffect(string Label, string? Scope = null) : LocalGameEffect;

public sealed record ScheduleEffect(int Turns, LocalGameEffect Effect) : LocalGameEffect
{
    public int Days { get; init; }

    public ConditionExpression? Condition { get; init; }
}

public sealed record AdvanceLogicalTimeEffect(int Days) : LocalGameEffect;

public sealed record EmitExternalEventEffect(
    string EventName,
    IReadOnlyDictionary<string, string> Attributes) : LocalGameEffect;

public sealed record SetCharacteristicEffect(string Name, int Value) : LocalGameEffect;

public sealed record ChangeCharacteristicEffect(string Name, int Amount) : LocalGameEffect;

public sealed record ScheduledEffect(int DueTurn, LocalGameEffect Effect)
{
    public int? DueLogicalDay { get; init; }

    public ConditionExpression? Condition { get; init; }
}

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

    public List<InteractionHistoryEntry> InteractionHistory { get; init; } = [];

    public Dictionary<string, int> Characteristics { get; init; } = new(StringComparer.Ordinal);

    public int LogicalDay { get; set; }

    public List<ExternalEffectEvent> ExternalEvents { get; init; } = [];

    public static WorldState Empty() => new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        []);
}

public sealed record ChoiceHistoryEntry(string NodeId, string ChoiceId, int Turn);

public sealed record JournalEntry(string Label, string? Scope, int Turn);

public sealed record ExternalEffectEvent(
    int Sequence,
    string EventName,
    IReadOnlyDictionary<string, string> Attributes,
    int Turn,
    int LogicalDay);

public sealed record InteractionHistoryEntry(
    string NodeId,
    string InteractionId,
    string InputId,
    bool? WasCorrect,
    int Turn);

[JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))]
public enum SessionStatus
{
    AwaitingInput,
    Paused,
    Completed,
    Abandoned,
    AwaitingExternalInput,
    AwaitingValidation,
}

public sealed record GameState(
    string CurrentNodeId,
    int Turn,
    SessionStatus Status,
    WorldState World)
{
    public int InteractionIndex { get; init; }

    public TextAnalysisResult? PendingTextAnalysis { get; init; }

    public SessionStatus? StatusBeforePause { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<InteractionKind>))]
public enum InteractionKind
{
    LegacyChoice,
    Narration,
    ChoiceSet,
    Quiz,
    CharacteristicGate,
    FreeText,
    Completed,
}

public sealed record CurrentStep(
    string NodeId,
    string Text,
    SessionStatus Status,
    IReadOnlyList<VisibleChoice> Choices,
    int Turn)
{
    public string? InteractionId { get; init; }

    public InteractionKind Kind { get; init; } = InteractionKind.LegacyChoice;

    public TextAnalysisResult? PendingTextAnalysis { get; init; }

    /// <summary>Media of the node the step belongs to, or <c>null</c> when the scenario declares none.</summary>
    public StepMedia? Media { get; init; }

    /// <summary>
    /// <c>true</c> when the current interaction may be skipped. A client must then
    /// present <see cref="ExitChoices"/> next to the interaction rather than
    /// forcing the player through it.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Choices that leave the node, offered alongside an optional interaction.
    /// Empty whenever the current interaction must be played, and empty when the
    /// current interaction already is the node's exit choice set — in that case
    /// the choices are carried by <see cref="Choices"/> as before.
    /// </summary>
    public IReadOnlyList<VisibleChoice> ExitChoices { get; init; } = [];
}

public sealed record TextAnalysisResult(
    string InteractionId,
    bool IsAccepted,
    IReadOnlyList<string> MatchedTerms,
    int MinimumMatches,
    string Explanation);

public sealed record VisibleChoice(string Id, string Text)
{
    public ChoiceMedia? Media { get; init; }
}

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