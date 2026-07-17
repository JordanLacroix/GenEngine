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

            if (state.Status is not SessionStatus.AwaitingInput)
            {
                deadEnds.Add(new SimulationDeadEnd(state.CurrentNodeId, state.Turn, $"Unexpected status {state.Status}."));
                continue;
            }

            CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);
            if (step.Choices.Count == 0)
            {
                deadEnds.Add(new SimulationDeadEnd(state.CurrentNodeId, state.Turn, "No choice is available."));
                continue;
            }

            bool progressed = false;
            foreach (VisibleChoice choice in step.Choices)
            {
                try
                {
                    GameState next = NarrativeRuntime.SubmitChoice(scenario, state, choice.Id);
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
                        $"Choice '{choice.Id}' failed: {exception.Code}."));
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

    private static string Fingerprint(GameState state) => NarrativeJson.Serialize(state);
}