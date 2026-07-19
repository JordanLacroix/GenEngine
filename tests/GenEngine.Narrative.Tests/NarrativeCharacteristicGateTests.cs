namespace GenEngine.Narrative.Tests;

public sealed class NarrativeCharacteristicGateTests
{
    [Fact]
    public void GateAutomaticallySelectsSatisfiedBranchAndRecordsDecision()
    {
        ScenarioDocument scenario = CreateGateScenario(3);

        GameState state = NarrativeRuntime.Start(scenario);

        Assert.Equal("success", state.CurrentNodeId);
        Assert.Equal(SessionStatus.Completed, state.Status);
        Assert.Equal(0, state.Turn);
        Assert.Equal(3, state.World.Characteristics["insight"]);
        Assert.Contains("insightful", state.World.Rewards);
        InteractionHistoryEntry history = Assert.Single(state.World.InteractionHistory);
        Assert.Equal("insight-gate", history.InteractionId);
        Assert.Equal("satisfied", history.InputId);
        Assert.True(history.WasCorrect);
    }

    [Fact]
    public void GateAutomaticallySelectsFailedBranchAndAppliesFailureEffects()
    {
        ScenarioDocument scenario = CreateGateScenario(1);

        GameState state = NarrativeRuntime.Start(scenario);

        Assert.Equal("fallback", state.CurrentNodeId);
        Assert.Equal(1, state.World.Variables["needs-help"]);
        Assert.False(Assert.Single(state.World.InteractionHistory).WasCorrect);
    }

    [Fact]
    public void GateAfterNarrationResolvesWithoutAdditionalPlayerTurn()
    {
        ScenarioDocument scenario = CreateGateScenario(2, withNarration: true);
        GameState state = NarrativeRuntime.Start(scenario);

        state = NarrativeRuntime.Continue(scenario, state);

        Assert.Equal("success", state.CurrentNodeId);
        Assert.Equal(1, state.Turn);
        Assert.Equal(2, state.World.InteractionHistory.Count);
    }

    [Fact]
    public void TreeExposesBothExplainedAutomaticBranches()
    {
        ScenarioDocument scenario = CreateGateScenario(3);
        GameState state = NarrativeRuntime.Start(scenario);

        NarrativeTree tree = NarrativeTreeBuilder.Build(scenario, state);

        NarrativeTreeEdge satisfied = Assert.Single(tree.Edges, static edge => edge.InputId == "insight-gate:satisfied");
        NarrativeTreeEdge failed = Assert.Single(tree.Edges, static edge => edge.InputId == "insight-gate:failed");
        Assert.True(satisfied.IsAvailable);
        Assert.False(failed.IsAvailable);
        Assert.Contains("insight", satisfied.Evaluation.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorRejectsMissingGateTargetAndEmptyCharacteristicName()
    {
        ScenarioDocument scenario = CreateGateScenario(3);
        NarrativeNode start = scenario.Nodes[0] with
        {
            Interactions =
            [
                new CharacteristicGateInteraction(
                    "gate",
                    new CharacteristicAtLeastCondition("", 2),
                    "missing",
                    "fallback",
                    [],
                    []),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario with { Nodes = [start, .. scenario.Nodes.Skip(1)] });

        Assert.Contains(report.Issues, static issue => issue.Code == "gate_target_missing");
        Assert.Contains(report.Issues, static issue => issue.Code == "condition_name_required");
    }

    private static ScenarioDocument CreateGateScenario(int insight, bool withNarration = false) => new(
        NarrativeVersions.LatestSchema,
        "The insight gate",
        "trial",
        [
            new NarrativeNode(
                "trial",
                "Read the signs.",
                null,
                [new SetCharacteristicEffect("insight", insight)],
                [])
            {
                Interactions =
                [
                    .. withNarration
                        ? new StepInteraction[]
                        {
                            new NarrationInteraction("observe", "You observe the signs.", []),
                        }
                        : [],
                    new CharacteristicGateInteraction(
                        "insight-gate",
                        new CharacteristicAtLeastCondition("insight", 2),
                        "success",
                        "fallback",
                        [new GrantRewardEffect("insightful")],
                        [new IncrementEffect("needs-help", 1)]),
                ],
            },
            new NarrativeNode("success", "You understand.", null, [], [], true),
            new NarrativeNode("fallback", "A guide helps you.", null, [], [], true),
        ]);

    [Fact]
    public void StructureKeepsGateBranchesInTheSameOrderAsTheStatefulTree()
    {
        ScenarioDocument scenario = CreateGateScenario(3);
        NarrativeTree tree = NarrativeTreeBuilder.Build(scenario, NarrativeRuntime.Start(scenario));

        NarrativeStructure structure = NarrativeTreeBuilder.BuildStructure(scenario);

        Assert.Equal(
            tree.Edges.Select(edge => (edge.SourceNodeId, edge.TargetNodeId, edge.InputId, edge.Text)),
            structure.Edges.Select(edge => (edge.SourceNodeId, edge.TargetNodeId, edge.InputId, edge.Text)));
        Assert.Contains(structure.Edges, edge => edge.InputId == "insight-gate:satisfied");
        Assert.Contains(structure.Edges, edge => edge.InputId == "insight-gate:failed");
    }
}