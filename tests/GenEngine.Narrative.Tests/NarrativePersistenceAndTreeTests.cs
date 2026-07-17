namespace GenEngine.Narrative.Tests;

public sealed class NarrativePersistenceAndTreeTests
{
    [Fact]
    public void VersionedSaveRoundTripsTheCompleteDeterministicState()
    {
        ScenarioDocument scenario = CreateTreeScenario();
        GameState state = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "open");
        DateTimeOffset savedAt = new(2026, 7, 17, 20, 0, 0, TimeSpan.Zero);
        GameSave save = GameSaveSerializer.Create(scenario.SchemaVersion, 42, savedAt, state);

        GameSave restored = GameSaveSerializer.Deserialize(
            GameSaveSerializer.Serialize(save),
            0,
            DateTimeOffset.MinValue);

        Assert.Equal(GameSaveVersions.Current, restored.FormatVersion);
        Assert.Equal(42UL, restored.Seed);
        Assert.Equal(savedAt, restored.SavedAt);
        Assert.Equal(NarrativeJson.Serialize(state), NarrativeJson.Serialize(restored.State));
    }

    [Fact]
    public void LegacyRawGameStateIsLoadedAsVersionedSave()
    {
        GameState state = NarrativeRuntime.Start(CreateTreeScenario());
        DateTimeOffset legacyDate = new(2026, 7, 17, 19, 0, 0, TimeSpan.Zero);

        GameSave restored = GameSaveSerializer.Deserialize(
            NarrativeJson.Serialize(state),
            77,
            legacyDate);

        Assert.Equal(GameSaveVersions.Current, restored.FormatVersion);
        Assert.Equal(NarrativeVersions.Schema, restored.ScenarioSchemaVersion);
        Assert.Equal(77UL, restored.Seed);
        Assert.Equal(legacyDate, restored.SavedAt);
        Assert.Equal(["legacy-state-to-save-v1", "save-v1-to-v2"], restored.AppliedMigrations);
        Assert.Equal(NarrativeJson.Serialize(state), NarrativeJson.Serialize(restored.State));
    }

    [Fact]
    public void UnknownSaveVersionIsRejectedExplicitly()
    {
        GameSave unsupported = GameSaveSerializer.Create(
            NarrativeVersions.Schema,
            1,
            DateTimeOffset.UnixEpoch,
            NarrativeRuntime.Start(CreateTreeScenario())) with
        {
            FormatVersion = 99,
        };

        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            GameSaveSerializer.Serialize(unsupported));

        Assert.Equal("save_version_not_supported", exception.Code);
    }

    [Fact]
    public void TreeMarksCurrentVisitedUnexploredAndLockedNodes()
    {
        ScenarioDocument scenario = CreateTreeScenario();
        GameState state = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "open");

        NarrativeTree tree = NarrativeTreeBuilder.Build(scenario, state);

        Assert.Equal(NarrativeTreeNodeState.Visited, Node("start").State);
        Assert.Equal(NarrativeTreeNodeState.Current, Node("open-end").State);
        Assert.Equal(NarrativeTreeNodeState.Locked, Node("locked-end").State);
        NarrativeTreeEdge lockedEdge = Assert.Single(tree.Edges, static edge => edge.InputId == "locked");
        Assert.False(lockedEdge.IsAvailable);
        Assert.Equal("hasReward", lockedEdge.Evaluation.Operator);
        Assert.Contains("missing", lockedEdge.Evaluation.Explanation, StringComparison.Ordinal);

        NarrativeTreeNode Node(string id) => Assert.Single(tree.Nodes, node => node.Id == id);
    }

    [Fact]
    public void TreeIncludesChoiceSetsFromTypedInteractions()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.LatestSchema,
            "Typed tree",
            "start",
            [
                new NarrativeNode("start", "Start", null, [], [])
                {
                    Interactions =
                    [
                        new ChoiceSetInteraction(
                            "route",
                            "Choose",
                            [new NarrativeChoice("finish", "Finish", "end", null, [])]),
                    ],
                },
                new NarrativeNode("end", "End", null, [], [], true),
            ]);

        NarrativeTree tree = NarrativeTreeBuilder.Build(scenario, NarrativeRuntime.Start(scenario));

        NarrativeTreeEdge edge = Assert.Single(tree.Edges);
        Assert.Equal("start", edge.SourceNodeId);
        Assert.Equal("end", edge.TargetNodeId);
    }

    private static ScenarioDocument CreateTreeScenario() => new(
        NarrativeVersions.Schema,
        "Tree",
        "start",
        [
            new NarrativeNode(
                "start",
                "Choose a route.",
                null,
                [],
                [
                    new NarrativeChoice("open", "Open route", "open-end", null, []),
                    new NarrativeChoice(
                        "locked",
                        "Locked route",
                        "locked-end",
                        new HasRewardCondition("secret-pass"),
                        []),
                ]),
            new NarrativeNode("open-end", "Open ending.", null, [], [], true),
            new NarrativeNode(
                "locked-end",
                "Locked ending.",
                new HasRewardCondition("secret-pass"),
                [],
                [],
                true),
        ]);
}