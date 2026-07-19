namespace GenEngine.Narrative;

public static class ScenarioAnalyzer
{
    public static SimulationReport Explore(ScenarioDocument scenario, int maximumStates = 1_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStates);

        Queue<GameState> pending = new();
        HashSet<string> visitedStates = new(StringComparer.Ordinal);
        HashSet<string> endings = new(StringComparer.Ordinal);
        List<SimulationDeadEnd> deadEnds = [];

        GameState initial = NarrativeRuntime.Start(scenario);
        pending.Enqueue(initial);
        visitedStates.Add(Fingerprint(initial));

        int explored = 0;
        while (pending.TryDequeue(out GameState? state) && explored < maximumStates)
        {
            explored++;
            if (state.Status is SessionStatus.Completed)
            {
                endings.Add(state.CurrentNodeId);
                continue;
            }

            if (state.Status is not SessionStatus.AwaitingInput
                && state.Status is not SessionStatus.AwaitingExternalInput
                && state.Status is not SessionStatus.AwaitingValidation)
            {
                deadEnds.Add(new SimulationDeadEnd(state.CurrentNodeId, state.Turn, $"Unexpected status {state.Status}."));
                continue;
            }

            CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);
            if (state.Status is SessionStatus.AwaitingInput
                && step.Kind is not InteractionKind.Narration
                && step.Choices.Count == 0)
            {
                deadEnds.Add(new SimulationDeadEnd(state.CurrentNodeId, state.Turn, "No choice is available."));
                continue;
            }

            bool progressed = false;
            foreach ((string inputId, Func<GameState> transition) in GetTransitions(scenario, state, step))
            {
                try
                {
                    GameState next = transition();
                    if (visitedStates.Add(Fingerprint(next)))
                    {
                        pending.Enqueue(next);
                    }

                    progressed = true;
                }
                catch (NarrativeException exception)
                {
                    deadEnds.Add(new SimulationDeadEnd(
                        state.CurrentNodeId,
                        state.Turn,
                        $"Input '{inputId}' failed: {exception.Code}."));
                }
            }

            if (!progressed)
            {
                deadEnds.Add(new SimulationDeadEnd(state.CurrentNodeId, state.Turn, "Every visible choice failed."));
            }
        }

        return new SimulationReport(
            explored,
            endings.Order(StringComparer.Ordinal).ToArray(),
            deadEnds,
            pending.Count != 0);
    }

    private static IEnumerable<(string InputId, Func<GameState> Transition)> GetTransitions(
        ScenarioDocument scenario,
        GameState state,
        CurrentStep step)
    {
        if (state.Status is SessionStatus.AwaitingExternalInput)
        {
            NarrativeNode node = scenario.Nodes.Single(candidate =>
                string.Equals(candidate.Id, state.CurrentNodeId, StringComparison.Ordinal));
            FreeTextInteraction freeText = (FreeTextInteraction)node.Interactions![state.InteractionIndex];
            string acceptedInput = string.Join(' ', freeText.RequiredTerms.Take(freeText.MinimumMatches));
            yield return ("matching-text", () => NarrativeRuntime.SubmitText(scenario, state, acceptedInput));
            foreach ((string InputId, Func<GameState> Transition) skip in GetSkipTransitions(scenario, state, step))
            {
                yield return skip;
            }

            yield break;
        }

        if (state.Status is SessionStatus.AwaitingValidation)
        {
            yield return ("confirm-analysis", () => NarrativeRuntime.ConfirmTextAnalysis(scenario, state, true));
            yield break;
        }

        if (step.Kind is InteractionKind.Narration)
        {
            yield return ("continue", () => NarrativeRuntime.Continue(scenario, state));
            foreach ((string InputId, Func<GameState> Transition) skip in GetSkipTransitions(scenario, state, step))
            {
                yield return skip;
            }

            yield break;
        }

        foreach (VisibleChoice choice in step.Choices)
        {
            yield return step.Kind is InteractionKind.Quiz
                ? (choice.Id, () => NarrativeRuntime.SubmitAnswer(scenario, state, choice.Id))
                : (choice.Id, () => NarrativeRuntime.SubmitChoice(scenario, state, choice.Id));
        }

        foreach ((string InputId, Func<GameState> Transition) skip in GetSkipTransitions(scenario, state, step))
        {
            yield return skip;
        }
    }

    /// <summary>
    /// Skipping an optional interaction is a real branch of the state space: the
    /// exploration must walk it, otherwise a report would claim a node has no
    /// alternative path when the player is free to take one.
    /// </summary>
    private static IEnumerable<(string InputId, Func<GameState> Transition)> GetSkipTransitions(
        ScenarioDocument scenario,
        GameState state,
        CurrentStep step)
    {
        foreach (VisibleChoice choice in step.ExitChoices)
        {
            yield return ($"skip:{choice.Id}", () => NarrativeRuntime.SubmitChoice(scenario, state, choice.Id));
        }
    }

    private static string Fingerprint(GameState state) => NarrativeJson.Serialize(state);
}