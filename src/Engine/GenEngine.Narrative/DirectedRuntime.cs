namespace GenEngine.Narrative;

/// <summary>
/// The <b>director</b> of a directed scenario. It is a pure, deterministic function
/// of (document, state, player utterance, narrator proposal): given the same
/// inputs it always produces the same next state. It performs no I/O and never
/// calls a model — the model call is a service concern. This is invariant 15 made
/// concrete: an AI output never mutates state directly; it becomes an explicit,
/// validated, frozen input first.
/// <para>
/// Because the accepted proposal is an explicit input, a directed session replays
/// exactly by re-feeding the recorded (utterance, proposal) pairs. What replay does
/// <b>not</b> promise is that re-calling the model reproduces the same prose — it
/// will not. The recorded proposal is the source of truth, not the model.
/// </para>
/// </summary>
public static class DirectedRuntime
{
    /// <summary>The pressure the director injects into the prompt when a player lingers past a beat's patience.</summary>
    public enum PressureLevel
    {
        None,
        Soft,
        Strong,
    }

    /// <summary>Builds the initial state of a fresh session from the document.</summary>
    public static DirectedState CreateInitialState(DirectedScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var npcs = new Dictionary<string, DirectedNpcState>(StringComparer.Ordinal);
        foreach (DirectedNpc npc in document.Npcs)
        {
            npcs[npc.Id] = new DirectedNpcState(npc.Alive, npc.Trust);
        }

        return new DirectedState(
            Turn: 0,
            BeatId: document.InitialBeatId,
            BeatTurns: 0,
            Location: document.Locations.Count > 0 ? document.Locations[0].Id : string.Empty,
            Inventory: [],
            KnownFacts: [],
            VisitedBeats: [document.InitialBeatId],
            Npcs: npcs,
            Dread: 0,
            Clock: document.Clock.StartMinute,
            Summary: string.Empty,
            Recent: [],
            Over: false,
            Ending: null);
    }

    /// <summary>
    /// The facts the current beat allows the narrator to reveal: the beat's own
    /// reveals plus everything the player already knows. The narrator prompt is
    /// built from exactly this set, never the full fact table.
    /// </summary>
    public static IReadOnlyList<DirectedFact> RevealableFacts(DirectedScenarioDocument document, DirectedState state)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);

        DirectedBeat beat = CurrentBeat(document, state);
        var allowed = new HashSet<string>(beat.Reveals ?? [], StringComparer.Ordinal);
        allowed.UnionWith(state.KnownFacts);
        return document.Facts.Where(f => allowed.Contains(f.Id)).ToList();
    }

    /// <summary>
    /// The player is lingering? The director does not block, it pushes. This returns
    /// the pressure level the caller must fold into the prompt rather than a forced
    /// beat transition.
    /// </summary>
    public static PressureLevel Pressure(DirectedScenarioDocument document, DirectedState state)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);

        DirectedBeat beat = CurrentBeat(document, state);
        if (state.BeatTurns < beat.Patience)
        {
            return PressureLevel.None;
        }

        return state.BeatTurns - beat.Patience >= 3 ? PressureLevel.Strong : PressureLevel.Soft;
    }

    /// <summary>Evaluates a declarative predicate against the state. Unknown ids evaluate to false, never throw.</summary>
    public static bool Evaluate(DirectedPredicate predicate, DirectedState state) => predicate switch
    {
        DirectedAlways => true,
        DirectedAll all => all.Predicates.All(p => Evaluate(p, state)),
        DirectedAny any => any.Predicates.Any(p => Evaluate(p, state)),
        DirectedNot not => !Evaluate(not.Predicate, state),
        DirectedAtLocation atLocation => string.Equals(state.Location, atLocation.Location, StringComparison.Ordinal),
        DirectedHasItem hasItem => state.Inventory.Contains(hasItem.Item),
        DirectedKnowsFact knowsFact => state.KnownFacts.Contains(knowsFact.Fact),
        DirectedKnownFactCountAtLeast count => state.KnownFacts.Count >= count.Count,
        DirectedNpcAlive npcAlive => state.Npcs.TryGetValue(npcAlive.Npc, out DirectedNpcState? n) && n.Alive,
        DirectedNpcTrustAtLeast trust =>
            state.Npcs.TryGetValue(trust.Npc, out DirectedNpcState? n) && n.Trust >= trust.Value,
        DirectedDreadAtLeast dread => state.Dread >= dread.Value,
        DirectedClockAtLeast clock => state.Clock >= clock.Minute,
        DirectedTurnAtLeast turn => state.Turn >= turn.Turn,
        DirectedBeatTurnsAtLeast beatTurns => state.BeatTurns >= beatTurns.Turns,
        DirectedVisitedBeat visited => state.VisitedBeats.Contains(visited.Beat),
        DirectedEnded => state.Over,
        _ => false,
    };

    /// <summary>
    /// Validates the narrator's proposal against the world and the live state, then
    /// applies what survives. Anything incoherent — an unknown location, an unowned
    /// item, a dead character interacting, a fact not revealable here — is rejected
    /// silently and never applied even in part.
    /// </summary>
    public static DirectedTurnResult ApplyTurn(
        DirectedScenarioDocument document,
        DirectedState state,
        string playerUtterance,
        NarratorProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(proposal);

        if (state.Over)
        {
            throw new NarrativeException("directed_session_over", "The directed session is already over.");
        }

        string clean = SanitizeUtterance(playerUtterance);
        var rejected = new List<string>();
        NarratorDelta d = proposal.Deltas;

        var locations = new HashSet<string>(document.Locations.Select(l => l.Id), StringComparer.Ordinal);
        var facts = new HashSet<string>(document.Facts.Select(f => f.Id), StringComparer.Ordinal);

        int turn = state.Turn + 1;
        int beatTurns = state.BeatTurns + 1;
        int dread = Clamp(state.Dread + Clamp(d.Dread, DirectedVersions.MinDreadDelta, DirectedVersions.MaxDreadDelta),
            DirectedVersions.MinDread, DirectedVersions.MaxDread);
        int clock = state.Clock + Clamp(d.Minutes, DirectedVersions.MinMinutes, DirectedVersions.MaxMinutes);

        string location = state.Location;
        if (!string.IsNullOrEmpty(d.MoveTo))
        {
            if (locations.Contains(d.MoveTo))
            {
                location = d.MoveTo;
            }
            else
            {
                rejected.Add($"unknown location: {d.MoveTo}");
            }
        }

        var inventory = new List<string>(state.Inventory);
        foreach (string item in d.AddItems)
        {
            if (!inventory.Contains(item))
            {
                inventory.Add(item);
            }
        }

        foreach (string item in d.RemoveItems)
        {
            if (!inventory.Remove(item))
            {
                rejected.Add($"item not owned: {item}");
            }
        }

        var knownFacts = new List<string>(state.KnownFacts);
        var revealableNow = new HashSet<string>(
            RevealableFacts(document, state).Select(f => f.Id), StringComparer.Ordinal);
        foreach (string id in d.LearnedFacts)
        {
            if (!facts.Contains(id))
            {
                rejected.Add($"unknown fact: {id}");
                continue;
            }

            if (!revealableNow.Contains(id))
            {
                rejected.Add($"fact not revealable here: {id}");
                continue;
            }

            if (!knownFacts.Contains(id))
            {
                knownFacts.Add(id);
            }
        }

        var npcs = state.Npcs.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (TrustChange change in d.TrustChanges)
        {
            if (!npcs.TryGetValue(change.Npc, out DirectedNpcState? npc))
            {
                rejected.Add($"unknown npc: {change.Npc}");
                continue;
            }

            if (!npc.Alive)
            {
                rejected.Add($"dead npc interacts: {change.Npc}");
                continue;
            }

            int trust = Clamp(
                npc.Trust + Clamp(change.Delta, DirectedVersions.MinTrustDelta, DirectedVersions.MaxTrustDelta),
                DirectedVersions.MinTrust,
                DirectedVersions.MaxTrust);
            npcs[change.Npc] = npc with { Trust = trust };
        }

        foreach (string id in d.Killed)
        {
            if (!npcs.TryGetValue(id, out DirectedNpcState? npc))
            {
                rejected.Add($"unknown npc killed: {id}");
                continue;
            }

            if (!npc.Alive)
            {
                rejected.Add($"npc already dead: {id}");
                continue;
            }

            npcs[id] = npc with { Alive = false };
        }

        var recent = new List<DirectedExchange>(state.Recent) { new(clean, proposal.Prose) };
        while (recent.Count > RecentWindow)
        {
            recent.RemoveAt(0);
        }

        DirectedState next = state with
        {
            Turn = turn,
            BeatTurns = beatTurns,
            Dread = dread,
            Clock = clock,
            Location = location,
            Inventory = inventory,
            KnownFacts = knownFacts,
            Npcs = npcs,
            Recent = recent,
        };

        next = AdvanceBeat(document, next, proposal.BeatSatisfied);
        return new DirectedTurnResult(next, rejected);
    }

    /// <summary>
    /// Beat transition. The narrator's <c>beatSatisfied</c> flag is not enough — the
    /// beat's declarative <c>satisfied</c> predicate must also pass. That is what
    /// stops the model from skipping half the story. The hard clock overrides both.
    /// </summary>
    private static DirectedState AdvanceBeat(
        DirectedScenarioDocument document,
        DirectedState state,
        bool narratorSaysDone)
    {
        DirectedBeat beat = CurrentBeat(document, state);

        if (beat.Ending is not null)
        {
            return state with { Over = true, Ending = beat.Ending };
        }

        // Hard clock: once the ritual minute is crossed, the world imposes the scene.
        if (document.Clock is { RitualMinute: int ritualMinute, RitualBeatId: string ritualBeat }
            && state.Clock >= ritualMinute
            && !string.Equals(beat.Id, ritualBeat, StringComparison.Ordinal)
            && document.Beats.Any(b => string.Equals(b.Id, ritualBeat, StringComparison.Ordinal)))
        {
            return EnterBeat(document, state, ritualBeat);
        }

        if (!narratorSaysDone || !Evaluate(beat.Satisfied, state))
        {
            return state;
        }

        foreach (string candidateId in beat.Next)
        {
            DirectedBeat? candidate = document.Beats.FirstOrDefault(
                b => string.Equals(b.Id, candidateId, StringComparison.Ordinal));
            if (candidate is not null && Evaluate(candidate.Requires, state))
            {
                return EnterBeat(document, state, candidateId);
            }
        }

        return state;
    }

    // Enters a beat and, when that beat is an ending, resolves the session in the
    // same turn — an ending is a terminal state, not a scene the player replays into.
    private static DirectedState EnterBeat(DirectedScenarioDocument document, DirectedState state, string beatId)
    {
        var visited = state.VisitedBeats.Contains(beatId)
            ? state.VisitedBeats
            : new List<string>(state.VisitedBeats) { beatId };
        DirectedState entered = state with { BeatId = beatId, BeatTurns = 0, VisitedBeats = visited };

        DirectedBeat? target = document.Beats.FirstOrDefault(
            b => string.Equals(b.Id, beatId, StringComparison.Ordinal));
        if (target?.Ending is string ending)
        {
            return entered with { Over = true, Ending = ending };
        }

        return entered;
    }

    /// <summary>
    /// Injection guard. The player utterance is data, never an instruction: it is
    /// trimmed, cut to <see cref="DirectedVersions.MaxUtterance"/> characters, and
    /// stripped of control characters so a player cannot reprogram the narrator.
    /// </summary>
    public static string SanitizeUtterance(string? utterance)
    {
        if (string.IsNullOrEmpty(utterance))
        {
            return string.Empty;
        }

        string trimmed = utterance.Trim();
        if (trimmed.Length > DirectedVersions.MaxUtterance)
        {
            trimmed = trimmed[..DirectedVersions.MaxUtterance];
        }

        return new string(trimmed.Where(c => !char.IsControl(c) || c == '\n').ToArray());
    }

    /// <summary>The current beat, falling back to the initial beat when the id is unknown.</summary>
    public static DirectedBeat CurrentBeat(DirectedScenarioDocument document, DirectedState state)
    {
        return document.Beats.FirstOrDefault(b => string.Equals(b.Id, state.BeatId, StringComparison.Ordinal))
            ?? document.Beats.First(b => string.Equals(b.Id, document.InitialBeatId, StringComparison.Ordinal));
    }

    private const int RecentWindow = 3;

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}