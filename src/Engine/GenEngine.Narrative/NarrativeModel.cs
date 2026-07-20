using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

public static class NarrativeVersions
{
    public const int Schema = 1;
    public const int LatestSchema = 7;

    /// <summary>Schema that introduced typed step interactions.</summary>
    public const int InteractionsSchema = 2;

    /// <summary>Schema that introduced optional media references on steps and choices.</summary>
    public const int MediaSchema = 3;

    /// <summary>Schema that introduced skippable (optional) step interactions.</summary>
    public const int OptionalInteractionsSchema = 4;

    /// <summary>Schema that introduced optional author-written help on steps and choices.</summary>
    public const int AuthorHelpSchema = 5;

    /// <summary>Schema that introduced the document interaction and its consultation condition.</summary>
    public const int DocumentSchema = 6;

    /// <summary>Schema that introduced the player-stat grant effect.</summary>
    public const int PlayerStatSchema = 7;
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

/// <summary>
/// What a presented document <em>is</em>, from the player's point of view. The
/// taxonomy is named rather than free-form so a client can style a memo like a
/// memo and an application log like a log, and so authoring tooling can reason
/// about it. <see cref="Other"/> keeps it open: a nature nobody anticipated
/// still ships as a document instead of forcing a new schema version.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentNature>))]
public enum DocumentNature
{
    Other,
    Memo,
    Email,
    Code,
    Diff,
    Log,
    Table,
    Conversation,
    Report,
}

/// <summary>Unit an excerpt is counted in, so a client can word the disclosure correctly.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentUnit>))]
public enum DocumentUnit
{
    Lines,
    Rows,
    Messages,
    Entries,
    Paragraphs,
}

/// <summary>
/// Honest disclosure that the body is a sample, not the whole thing: "12 lignes
/// affichées sur 412". The engine carries and validates the counts; presenting a
/// sample as a whole would be an interface lie, and this game is precisely about
/// lucidity in front of information.
/// </summary>
public sealed record DocumentExcerpt(int ShownUnits, int TotalUnits, DocumentUnit Unit);

/// <summary>A named header line — an email's <c>From</c>, a memo's <c>Objet</c>.</summary>
public sealed record DocumentHeader(string Name, string Value);

/// <summary>
/// How a single line stands out. One marker set serves a diff (<see cref="Added"/>,
/// <see cref="Removed"/>, <see cref="Context"/>) and an application log
/// (<see cref="Warning"/>, <see cref="Error"/>), which is why lines are one block
/// type and not two.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentLineMarker>))]
public enum DocumentLineMarker
{
    Context,
    Added,
    Removed,
    Warning,
    Error,
}

/// <summary>A line of a <see cref="DocumentLinesBlock"/>, optionally marked and labelled.</summary>
public sealed record DocumentLine(string Text)
{
    /// <summary>Diff or severity marker, or <c>null</c> for a plain line.</summary>
    public DocumentLineMarker? Marker { get; init; }

    /// <summary>Author-written gutter label: a line number, a timestamp. Never parsed by the engine.</summary>
    public string? Label { get; init; }
}

/// <summary>A row of a <see cref="DocumentTableBlock"/>; cell count must match the columns.</summary>
public sealed record DocumentRow(IReadOnlyList<string> Cells);

/// <summary>
/// A block of document body. The set is deliberately closed at three shapes —
/// prose, lines, table — because that is the smallest vocabulary that still lets
/// a client render each nature correctly: a table renders as a table, a diff and
/// a log as marked lines, a memo or an email as prose under headers. Going
/// further would mean shipping a markup language, which the engine has no
/// business interpreting; stopping at a single free string would throw away
/// structure that carries meaning.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DocumentParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(DocumentLinesBlock), "lines")]
[JsonDerivedType(typeof(DocumentTableBlock), "table")]
public abstract record DocumentBlock;

public sealed record DocumentParagraphBlock(string Text) : DocumentBlock;

public sealed record DocumentLinesBlock(IReadOnlyList<DocumentLine> Lines) : DocumentBlock;

public sealed record DocumentTableBlock(
    IReadOnlyList<string> Columns,
    IReadOnlyList<DocumentRow> Rows) : DocumentBlock;

/// <summary>
/// A document the scenario shows to the player: the memo that was only ever
/// alluded to, the blocked diff, the table of 412 applications. The content is
/// carried by the scenario itself, so it is versioned, hashed and replayed like
/// everything else — the engine never fetches anything.
/// </summary>
public sealed record PresentedDocument(
    string Title,
    DocumentNature Nature,
    IReadOnlyList<DocumentBlock> Blocks)
{
    /// <summary>Ordered headers rendered above the body, or <c>null</c> when the nature has none.</summary>
    public IReadOnlyList<DocumentHeader>? Headers { get; init; }

    /// <summary>Sampling disclosure. <c>null</c> means the body is the document in full.</summary>
    public DocumentExcerpt? Excerpt { get; init; }
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
[JsonDerivedType(typeof(DocumentInteraction), "document")]
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

/// <summary>
/// Presents a <see cref="PresentedDocument"/> to the player. Consulting it is a
/// single explicit command, recorded in the session's interaction history like
/// every other interaction — which is what makes
/// <see cref="ConsultedDocumentCondition"/> evaluable without introducing a
/// second state system.
/// </summary>
public sealed record DocumentInteraction(
    string Id,
    string Prompt,
    PresentedDocument Document,
    IReadOnlyList<LocalGameEffect> ConsultEffects) : StepInteraction(Id)
{
    /// <summary>
    /// The <see cref="InteractionHistoryEntry.InputId"/> written when the player
    /// consults the document. It is the single source of truth read back by
    /// <see cref="ConsultedDocumentCondition"/>.
    /// </summary>
    public const string ConsultedInputId = "consulted";
}

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
[JsonDerivedType(typeof(ConsultedDocumentCondition), "consultedDocument")]
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

/// <summary>
/// True once the player has consulted the <see cref="DocumentInteraction"/> with
/// this id, at any point in the session. It reads the interaction history the
/// engine already records and persists, so it adds no state, needs no save-format
/// change, and replays exactly from the recorded commands.
/// </summary>
public sealed record ConsultedDocumentCondition(string InteractionId) : ConditionExpression;

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
[JsonDerivedType(typeof(GrantPlayerStatEffect), "grantPlayerStat")]
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

/// <summary>
/// Grants <paramref name="Amount"/> points of the configured player stat
/// <paramref name="Stat"/>.
/// </summary>
/// <remarks>
/// Every other effect of this hierarchy mutates the <em>session</em>. This one does
/// not, and cannot: a player stat lives in <c>PlayerExperience</c>, persists across
/// scenarios, and its ceiling is published by <c>Configuration</c> — three things the
/// engine is forbidden to know, since it performs no I/O and must stay a pure
/// function of the scenario, the world and the commands.
/// <para>
/// So the engine only <em>records the intent</em>: applying this effect appends a
/// <see cref="ExternalEffectEvent"/> named <see cref="PlayerStatEventName"/> to
/// <see cref="WorldState.ExternalEvents"/>, exactly the path <c>economy.reward</c>
/// already takes. The session state stays complete and replayable on its own, the
/// determinism invariant is untouched, and <c>Play</c> relays the recorded events to
/// <c>PlayerExperience</c>, which is the only authority on the value and its cap.
/// The engine never learns whether the stat exists, nor what it ended up worth.
/// </para>
/// </remarks>
public sealed record GrantPlayerStatEffect(string Stat, int Amount) : LocalGameEffect
{
    /// <summary>Name of the external event this effect records. Read by <c>Play</c>.</summary>
    public const string PlayerStatEventName = "player.stat";

    /// <summary>Attribute carrying the configured stat key.</summary>
    public const string StatAttribute = "stat";

    /// <summary>Attribute carrying the granted amount, as an invariant decimal integer.</summary>
    public const string AmountAttribute = "amount";
}

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
    Document,
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

    /// <summary>
    /// The document to present, set only when <see cref="Kind"/> is
    /// <see cref="InteractionKind.Document"/>. <c>null</c> everywhere else, so the
    /// field is purely additive for every existing client.
    /// </summary>
    public PresentedDocument? Document { get; init; }
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