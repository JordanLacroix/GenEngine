using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

/// <summary>
/// Versioning of the directed-scenario family. This is a <b>separate</b> document
/// type from <see cref="ScenarioDocument"/>: a directed scenario is not a
/// deterministic node graph carrying an extra capability flag, it is a distinct
/// shape (beats, predicates, keyed facts, a narrator contract). Keeping it apart
/// means the deterministic document is never touched — its canonical bytes and
/// hash cannot move — and this family gets its own version ladder, independent of
/// <see cref="NarrativeVersions.LatestSchema"/>.
/// </summary>
public static class DirectedVersions
{
    /// <summary>First directed-scenario schema.</summary>
    public const int Schema = 1;

    /// <summary>Latest directed-scenario schema.</summary>
    public const int LatestSchema = 1;

    /// <summary>Maximum length of a raw player utterance the runtime will look at.</summary>
    public const int MaxUtterance = 400;

    /// <summary>Clamp bounds for the narrator-proposed deltas, applied before any effect.</summary>
    public const int MinDreadDelta = -15;
    public const int MaxDreadDelta = 25;
    public const int MinMinutes = 0;
    public const int MaxMinutes = 90;
    public const int MinTrust = -3;
    public const int MaxTrust = 3;
    public const int MinTrustDelta = -3;
    public const int MaxTrustDelta = 3;
    public const int MinDread = 0;
    public const int MaxDread = 100;
}

/// <summary>
/// A directed scenario: the structure the director enforces while a connected model
/// writes the prose. The model may only propose <see cref="NarratorProposal"/>
/// deltas; the director validates each against this document and the live state and
/// silently rejects anything incoherent (see <see cref="DirectedRuntime"/>).
/// </summary>
public sealed record DirectedScenarioDocument(
    int SchemaVersion,
    string Title,
    string Premise,
    string InitialBeatId,
    DirectedClock Clock,
    IReadOnlyList<DirectedLocation> Locations,
    IReadOnlyList<DirectedFact> Facts,
    IReadOnlyList<DirectedNpc> Npcs,
    IReadOnlyList<DirectedBeat> Beats);

/// <summary>
/// The hard clock that doubles the model on non-negotiable switches. When the
/// logical clock crosses <see cref="RitualMinute"/>, the director forces the beat
/// to <see cref="RitualBeatId"/> regardless of what the model proposed — the world
/// imposes the scene, exactly as the prototype flips to "nuit" at 23:00.
/// </summary>
public sealed record DirectedClock(
    int StartMinute,
    int? RitualMinute,
    string? RitualBeatId);

/// <summary>A place that exists in the world. The model may only move the player to a known location id.</summary>
public sealed record DirectedLocation(string Id, string Description);

/// <summary>
/// A canon fact. Its text only enters the model's prompt when it is revealable in
/// the current beat or already known — facts are kept under key so the narrator
/// cannot disclose what it cannot see.
/// </summary>
public sealed record DirectedFact(string Id, string Text);

/// <summary>
/// A tracked non-player character. <see cref="Secret"/> is never sent to the model
/// until a beat reveals it; the director exposes only <see cref="Known"/>.
/// </summary>
public sealed record DirectedNpc(
    string Id,
    string Name,
    string Role,
    string Known,
    string Secret,
    bool Alive,
    int Trust);

/// <summary>
/// A narrative beat of the graph. Unlike the reference prototype, which carried
/// <c>requires</c>/<c>satisfied</c> as JavaScript closures, GenEngine may never
/// execute author-supplied code (invariant 8): the predicates are <b>declarative</b>
/// <see cref="DirectedPredicate"/> trees the engine evaluates itself.
/// </summary>
public sealed record DirectedBeat(
    string Id,
    string Goal,
    DirectedPredicate Requires,
    DirectedPredicate Satisfied,
    IReadOnlyList<string> Next,
    int Patience,
    IReadOnlyList<string>? Reveals,
    string? Ending);

/// <summary>
/// A declarative predicate over the directed state. Serialized like the engine's
/// existing condition language, with a <c>$type</c> discriminator, so no arbitrary
/// author expression is ever executed.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DirectedAlways), "always")]
[JsonDerivedType(typeof(DirectedAll), "all")]
[JsonDerivedType(typeof(DirectedAny), "any")]
[JsonDerivedType(typeof(DirectedNot), "not")]
[JsonDerivedType(typeof(DirectedAtLocation), "atLocation")]
[JsonDerivedType(typeof(DirectedHasItem), "hasItem")]
[JsonDerivedType(typeof(DirectedKnowsFact), "knowsFact")]
[JsonDerivedType(typeof(DirectedKnownFactCountAtLeast), "knownFactCountAtLeast")]
[JsonDerivedType(typeof(DirectedNpcAlive), "npcAlive")]
[JsonDerivedType(typeof(DirectedNpcTrustAtLeast), "npcTrustAtLeast")]
[JsonDerivedType(typeof(DirectedDreadAtLeast), "dreadAtLeast")]
[JsonDerivedType(typeof(DirectedClockAtLeast), "clockAtLeast")]
[JsonDerivedType(typeof(DirectedTurnAtLeast), "turnAtLeast")]
[JsonDerivedType(typeof(DirectedBeatTurnsAtLeast), "beatTurnsAtLeast")]
[JsonDerivedType(typeof(DirectedVisitedBeat), "visitedBeat")]
[JsonDerivedType(typeof(DirectedEnded), "ended")]
public abstract record DirectedPredicate;

public sealed record DirectedAlways : DirectedPredicate;
public sealed record DirectedAll(IReadOnlyList<DirectedPredicate> Predicates) : DirectedPredicate;
public sealed record DirectedAny(IReadOnlyList<DirectedPredicate> Predicates) : DirectedPredicate;
public sealed record DirectedNot(DirectedPredicate Predicate) : DirectedPredicate;
public sealed record DirectedAtLocation(string Location) : DirectedPredicate;
public sealed record DirectedHasItem(string Item) : DirectedPredicate;
public sealed record DirectedKnowsFact(string Fact) : DirectedPredicate;
public sealed record DirectedKnownFactCountAtLeast(int Count) : DirectedPredicate;
public sealed record DirectedNpcAlive(string Npc) : DirectedPredicate;
public sealed record DirectedNpcTrustAtLeast(string Npc, int Value) : DirectedPredicate;
public sealed record DirectedDreadAtLeast(int Value) : DirectedPredicate;
public sealed record DirectedClockAtLeast(int Minute) : DirectedPredicate;
public sealed record DirectedTurnAtLeast(int Turn) : DirectedPredicate;
public sealed record DirectedBeatTurnsAtLeast(int Turns) : DirectedPredicate;
public sealed record DirectedVisitedBeat(string Beat) : DirectedPredicate;
public sealed record DirectedEnded : DirectedPredicate;

/// <summary>
/// The live, replayable state of a directed session. This is the only thing the
/// runtime mutates, and it is what a session persists after each turn (together
/// with the accepted proposal). Npc state carries only the mutable bits; a
/// character's name, role and secrets live in the document, not the state.
/// </summary>
public sealed record DirectedState(
    int Turn,
    string BeatId,
    int BeatTurns,
    string Location,
    IReadOnlyList<string> Inventory,
    IReadOnlyList<string> KnownFacts,
    IReadOnlyList<string> VisitedBeats,
    IReadOnlyDictionary<string, DirectedNpcState> Npcs,
    int Dread,
    int Clock,
    string Summary,
    IReadOnlyList<DirectedExchange> Recent,
    bool Over,
    string? Ending);

/// <summary>Mutable per-character state: whether the character is alive and the current trust.</summary>
public sealed record DirectedNpcState(bool Alive, int Trust);

/// <summary>One recorded player/narrator exchange, kept verbatim in the rolling window.</summary>
public sealed record DirectedExchange(string Player, string Narrator);

/// <summary>
/// The narrow, schema-validated output the model is allowed to return. It proposes;
/// it does not decide. The director validates <see cref="Deltas"/> and gates
/// <see cref="BeatSatisfied"/> behind the beat predicate before anything is applied.
/// </summary>
public sealed record NarratorProposal(
    string Prose,
    IReadOnlyList<string> Choices,
    NarratorDelta Deltas,
    bool BeatSatisfied);

/// <summary>The world variations the narrator proposes. Every field is checked and clamped before effect.</summary>
public sealed record NarratorDelta(
    int Dread,
    int Minutes,
    string MoveTo,
    IReadOnlyList<string> AddItems,
    IReadOnlyList<string> RemoveItems,
    IReadOnlyList<string> LearnedFacts,
    IReadOnlyList<TrustChange> TrustChanges,
    IReadOnlyList<string> Killed);

/// <summary>A proposed change of one character's trust toward the player.</summary>
public sealed record TrustChange(string Npc, int Delta);

/// <summary>
/// The result of applying one turn: the new state, and the list of proposal
/// fragments the director refused. Rejections are never applied even partially;
/// they are surfaced for observability, not for the player.
/// </summary>
public sealed record DirectedTurnResult(DirectedState State, IReadOnlyList<string> Rejected);