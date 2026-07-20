namespace GenEngine.Narrative;

/// <summary>One structural problem found in a directed scenario document.</summary>
public sealed record DirectedValidationIssue(string Code, string Message);

/// <summary>The outcome of validating a directed scenario document.</summary>
public sealed record DirectedValidationReport(IReadOnlyList<DirectedValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Referential validation of a directed scenario. This is not the LLM turn loop; it
/// is the authoring-time check that the graph is internally consistent, so that a
/// published directed scenario cannot leave a beat unreachable or a predicate
/// pointing at an id that does not exist. It runs no model and reads no I/O.
/// </summary>
public static class DirectedValidator
{
    public static DirectedValidationReport Validate(DirectedScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<DirectedValidationIssue>();

        if (document.SchemaVersion < DirectedVersions.Schema || document.SchemaVersion > DirectedVersions.LatestSchema)
        {
            issues.Add(new DirectedValidationIssue(
                "directed_schema_unsupported",
                $"Directed schema version must be between {DirectedVersions.Schema} and {DirectedVersions.LatestSchema}."));
        }

        if (string.IsNullOrWhiteSpace(document.Title))
        {
            issues.Add(new DirectedValidationIssue("directed_title_required", "A directed scenario needs a title."));
        }

        var locationIds = new HashSet<string>(document.Locations.Select(l => l.Id), StringComparer.Ordinal);
        var factIds = new HashSet<string>(document.Facts.Select(f => f.Id), StringComparer.Ordinal);
        var npcIds = new HashSet<string>(document.Npcs.Select(n => n.Id), StringComparer.Ordinal);
        var beatIds = new HashSet<string>(document.Beats.Select(b => b.Id), StringComparer.Ordinal);

        RequireUnique(document.Locations.Select(l => l.Id), "directed_duplicate_location", issues);
        RequireUnique(document.Facts.Select(f => f.Id), "directed_duplicate_fact", issues);
        RequireUnique(document.Npcs.Select(n => n.Id), "directed_duplicate_npc", issues);
        RequireUnique(document.Beats.Select(b => b.Id), "directed_duplicate_beat", issues);

        if (document.Beats.Count == 0)
        {
            issues.Add(new DirectedValidationIssue("directed_no_beats", "A directed scenario needs at least one beat."));
        }

        if (!beatIds.Contains(document.InitialBeatId))
        {
            issues.Add(new DirectedValidationIssue(
                "directed_initial_beat_missing", $"The initial beat '{document.InitialBeatId}' does not exist."));
        }

        if (document.Clock.RitualBeatId is string ritualBeat && !beatIds.Contains(ritualBeat))
        {
            issues.Add(new DirectedValidationIssue(
                "directed_ritual_beat_missing", $"The hard-clock beat '{ritualBeat}' does not exist."));
        }

        foreach (DirectedNpc npc in document.Npcs)
        {
            if (npc.Trust < DirectedVersions.MinTrust || npc.Trust > DirectedVersions.MaxTrust)
            {
                issues.Add(new DirectedValidationIssue(
                    "directed_npc_trust_out_of_range",
                    $"Npc '{npc.Id}' starts with trust {npc.Trust} outside [{DirectedVersions.MinTrust}, {DirectedVersions.MaxTrust}]."));
            }
        }

        foreach (DirectedBeat beat in document.Beats)
        {
            if (beat.Patience < 0)
            {
                issues.Add(new DirectedValidationIssue(
                    "directed_beat_patience_invalid", $"Beat '{beat.Id}' has negative patience."));
            }

            foreach (string next in beat.Next)
            {
                if (!beatIds.Contains(next))
                {
                    issues.Add(new DirectedValidationIssue(
                        "directed_beat_next_missing", $"Beat '{beat.Id}' points to unknown beat '{next}'."));
                }
            }

            foreach (string reveal in beat.Reveals ?? [])
            {
                if (!factIds.Contains(reveal))
                {
                    issues.Add(new DirectedValidationIssue(
                        "directed_reveal_missing", $"Beat '{beat.Id}' reveals unknown fact '{reveal}'."));
                }
            }

            CheckPredicate(beat.Requires, beat.Id, locationIds, factIds, npcIds, beatIds, issues);
            CheckPredicate(beat.Satisfied, beat.Id, locationIds, factIds, npcIds, beatIds, issues);
        }

        return new DirectedValidationReport(issues);
    }

    /// <summary>Validates and throws the first issue as a <see cref="NarrativeException"/> when invalid.</summary>
    public static void EnsureValid(DirectedScenarioDocument document)
    {
        DirectedValidationReport report = Validate(document);
        if (!report.IsValid)
        {
            DirectedValidationIssue first = report.Issues[0];
            throw new NarrativeException(first.Code, first.Message);
        }
    }

    private static void CheckPredicate(
        DirectedPredicate predicate,
        string beatId,
        HashSet<string> locationIds,
        HashSet<string> factIds,
        HashSet<string> npcIds,
        HashSet<string> beatIds,
        List<DirectedValidationIssue> issues)
    {
        switch (predicate)
        {
            case DirectedAll all:
                foreach (DirectedPredicate p in all.Predicates)
                {
                    CheckPredicate(p, beatId, locationIds, factIds, npcIds, beatIds, issues);
                }

                break;
            case DirectedAny any:
                foreach (DirectedPredicate p in any.Predicates)
                {
                    CheckPredicate(p, beatId, locationIds, factIds, npcIds, beatIds, issues);
                }

                break;
            case DirectedNot not:
                CheckPredicate(not.Predicate, beatId, locationIds, factIds, npcIds, beatIds, issues);
                break;
            case DirectedAtLocation atLocation when !locationIds.Contains(atLocation.Location):
                issues.Add(Missing(beatId, "location", atLocation.Location));
                break;
            case DirectedKnowsFact knowsFact when !factIds.Contains(knowsFact.Fact):
                issues.Add(Missing(beatId, "fact", knowsFact.Fact));
                break;
            case DirectedNpcAlive npcAlive when !npcIds.Contains(npcAlive.Npc):
                issues.Add(Missing(beatId, "npc", npcAlive.Npc));
                break;
            case DirectedNpcTrustAtLeast trust when !npcIds.Contains(trust.Npc):
                issues.Add(Missing(beatId, "npc", trust.Npc));
                break;
            case DirectedVisitedBeat visited when !beatIds.Contains(visited.Beat):
                issues.Add(Missing(beatId, "beat", visited.Beat));
                break;
            default:
                break;
        }
    }

    private static DirectedValidationIssue Missing(string beatId, string kind, string id) =>
        new("directed_predicate_reference_missing", $"Beat '{beatId}' references unknown {kind} '{id}'.");

    private static void RequireUnique(IEnumerable<string> ids, string code, List<DirectedValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!seen.Add(id))
            {
                issues.Add(new DirectedValidationIssue(code, $"Duplicate id '{id}'."));
            }
        }
    }
}