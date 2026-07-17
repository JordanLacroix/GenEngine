namespace GenEngine.Narrative.Tests;

public sealed class NarrativeAuthoringAnalysisTests
{
    [Fact]
    public void StructureReportFindsLoopConditionalExitAndUnreachableEnding()
    {
        ScenarioDocument scenario = CreateLoopScenario(includeUnconditionalExit: false);

        NarrativeStructureReport report = NarrativeStructureAnalyzer.Analyze(scenario);

        NarrativeLoop loop = Assert.Single(report.Loops);
        Assert.Equal(["loop-a", "loop-b"], loop.NodeIds);
        Assert.True(loop.HasExit);
        Assert.False(loop.HasGuaranteedExit);
        Assert.Equal(["orphan-end"], report.UnreachableEndingNodeIds);
        Assert.Empty(report.NodesWithoutEndingPath);
    }

    [Fact]
    public void StructureReportFindsConditionalDeadEndRisk()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.Schema,
            "Conditional path",
            "start",
            [
                new NarrativeNode(
                    "start",
                    "Choose if allowed.",
                    null,
                    [],
                    [
                        new NarrativeChoice(
                            "locked-choice",
                            "Continue",
                            "ending",
                            new HasItemCondition("key"),
                            []),
                    ]),
                new NarrativeNode("ending", "Done.", null, [], [], true),
            ]);

        ConditionalDeadEndRisk risk = Assert.Single(
            NarrativeStructureAnalyzer.Analyze(scenario).ConditionalDeadEnds);

        Assert.Equal("start", risk.NodeId);
        Assert.Equal(["locked-choice"], risk.ConditionalInputIds);
    }

    [Fact]
    public void StructureReportFindsTrapWithNoPathToAnEnding()
    {
        ScenarioDocument scenario = CreateLoopScenario(includeUnconditionalExit: false, includeExit: false);

        NarrativeStructureReport report = NarrativeStructureAnalyzer.Analyze(scenario);

        Assert.False(Assert.Single(report.Loops).HasExit);
        Assert.Equal(["loop-a", "loop-b", "start"], report.NodesWithoutEndingPath);
    }

    [Fact]
    public void PreviewStartsAtRequestedNodeWithClonedInjectedStateAndEntryEffects()
    {
        ScenarioDocument scenario = CreatePreviewScenario();
        WorldState injected = WorldState.Empty();
        injected.Characteristics["insight"] = 2;
        injected.Inventory.Add("map");

        GameState preview = NarrativeRuntime.PreviewAt(scenario, "secret", injected, 7);

        Assert.Equal("secret", preview.CurrentNodeId);
        Assert.Equal(SessionStatus.Completed, preview.Status);
        Assert.Equal(7, preview.Turn);
        Assert.Equal(3, preview.World.Characteristics["insight"]);
        Assert.Contains("secret", preview.World.VisitedNodes);
        Assert.Contains("map", preview.World.Inventory);
        Assert.Equal(2, injected.Characteristics["insight"]);
        Assert.DoesNotContain("secret", injected.VisitedNodes);
    }

    [Fact]
    public void PreviewRejectsNodeLockedByInjectedState()
    {
        NarrativeException exception = Assert.Throws<NarrativeException>(() => NarrativeRuntime.PreviewAt(
            CreatePreviewScenario(),
            "secret",
            WorldState.Empty()));

        Assert.Equal("preview_node_locked", exception.Code);
    }

    private static ScenarioDocument CreateLoopScenario(
        bool includeUnconditionalExit,
        bool includeExit = true)
    {
        List<NarrativeChoice> loopChoices =
        [
            new NarrativeChoice("again", "Again", "loop-a", null, []),
        ];
        if (includeExit)
        {
            loopChoices.Add(new NarrativeChoice(
                "leave",
                "Leave",
                "ending",
                includeUnconditionalExit ? null : new HasItemCondition("key"),
                []));
        }

        return new ScenarioDocument(
            NarrativeVersions.Schema,
            "Loop",
            "start",
            [
                new NarrativeNode(
                    "start",
                    "Enter.",
                    null,
                    [],
                    [new NarrativeChoice("enter", "Enter", "loop-a", null, [])]),
                new NarrativeNode(
                    "loop-a",
                    "A.",
                    null,
                    [],
                    [new NarrativeChoice("next", "Next", "loop-b", null, [])]),
                new NarrativeNode("loop-b", "B.", null, [], loopChoices),
                new NarrativeNode("ending", "Done.", null, [], [], true),
                new NarrativeNode("orphan-end", "Orphan.", null, [], [], true),
            ]);
    }

    private static ScenarioDocument CreatePreviewScenario() => new(
        NarrativeVersions.Schema,
        "Preview",
        "start",
        [
            new NarrativeNode(
                "start",
                "Start.",
                null,
                [],
                [new NarrativeChoice("finish", "Finish", "ending", null, [])]),
            new NarrativeNode(
                "secret",
                "Secret.",
                new CharacteristicAtLeastCondition("insight", 2),
                [new ChangeCharacteristicEffect("insight", 1)],
                [],
                true),
            new NarrativeNode("ending", "Done.", null, [], [], true),
        ]);
}