namespace GenEngine.Narrative.Tests;

public sealed class NarrativeAdvancedTests
{
    [Fact]
    public void RichEffectsBuildPlayerStateHistoryAndJournal()
    {
        ScenarioDocument scenario = CreateRichScenario();

        GameState state = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Start(scenario),
            "trust-aya");

        Assert.Contains("testimony", state.World.Evidence);
        Assert.Equal(2, state.World.Relations["aya"]);
        Assert.Contains("critical-listener", state.World.Rewards);
        Assert.DoesNotContain("sealed-letter", state.World.Inventory);
        Assert.Equal(new ChoiceHistoryEntry("briefing", "trust-aya", 1), Assert.Single(state.World.ChoiceHistory));
        Assert.Equal(
            new JournalEntry("Vous avez accordé votre confiance à Aya.", "journey", 1),
            Assert.Single(state.World.Journal));
    }

    [Fact]
    public void ChoiceExplanationDescribesEverySatisfiedAndMissingRequirement()
    {
        ScenarioDocument scenario = CreateRichScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        ChoiceAvailability choice = Assert.Single(NarrativeRuntime.ExplainChoices(scenario, state));

        Assert.True(choice.IsAvailable);
        Assert.Equal("all", choice.Evaluation.Operator);
        Assert.Equal(2, choice.Evaluation.Children.Count);
        Assert.All(choice.Evaluation.Children, static child => Assert.True(child.Result));
        Assert.Contains("aya", choice.Evaluation.Children[1].Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzerExploresEveryReachableEnding()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.Schema,
            "Two endings",
            "start",
            [
                new NarrativeNode(
                    "start",
                    "Choose.",
                    null,
                    [],
                    [
                        new NarrativeChoice("left", "Left", "left-end", null, []),
                        new NarrativeChoice("right", "Right", "right-end", null, []),
                    ]),
                new NarrativeNode("left-end", "Left ending.", null, [], [], true),
                new NarrativeNode("right-end", "Right ending.", null, [], [], true),
            ]);

        SimulationReport report = ScenarioAnalyzer.Explore(scenario);

        Assert.Equal(3, report.ExploredStates);
        Assert.Equal(["left-end", "right-end"], report.EndingNodeIds);
        Assert.Empty(report.DeadEnds);
        Assert.False(report.StateBudgetExceeded);
    }

    [Fact]
    public void ValidatorRejectsAmbiguousChoicesAndInvalidDeferredEffects()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.Schema,
            "Invalid",
            "start",
            [
                new NarrativeNode(
                    "start",
                    "Start.",
                    null,
                    [new ScheduleEffect(-1, new GrantRewardEffect("reward"))],
                    [
                        new NarrativeChoice("same", "One", "end", null, []),
                        new NarrativeChoice("same", "Two", "end", null, []),
                    ]),
                new NarrativeNode("end", "End.", null, [], [], true),
            ]);

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.Contains(report.Issues, static issue => issue.Code == "duplicate_choice");
        Assert.Contains(report.Issues, static issue => issue.Code == "schedule_turns_invalid");
    }

    [Fact]
    public void LegacyGameStateWithoutRichCollectionsGetsSafeDefaults()
    {
        const string json = """
            {
              "currentNodeId":"start",
              "turn":0,
              "status":"AwaitingInput",
              "world":{
                "variables":{},
                "inventory":[],
                "visitedNodes":["start"],
                "scheduledEffects":[]
              }
            }
            """;

        GameState state = NarrativeJson.Deserialize<GameState>(json);

        Assert.Empty(state.World.Evidence);
        Assert.Empty(state.World.Relations);
        Assert.Empty(state.World.Rewards);
        Assert.Empty(state.World.ChoiceHistory);
        Assert.Empty(state.World.Journal);
        Assert.Empty(state.World.InteractionHistory);
    }

    private static ScenarioDocument CreateRichScenario() => new(
        NarrativeVersions.Schema,
        "The testimony",
        "briefing",
        [
            new NarrativeNode(
                "briefing",
                "Aya offers a sealed letter.",
                null,
                [
                    new CollectEffect("sealed-letter"),
                    new DiscoverEvidenceEffect("testimony"),
                    new ChangeRelationEffect("aya", 1),
                ],
                [
                    new NarrativeChoice(
                        "trust-aya",
                        "Trust Aya",
                        "end",
                        new AllCondition(
                        [
                            new HasEvidenceCondition("testimony"),
                            new RelationAtLeastCondition("aya", 1),
                        ]),
                        [
                            new ChangeRelationEffect("aya", 1),
                            new RemoveItemEffect("sealed-letter"),
                            new GrantRewardEffect("critical-listener"),
                            new RecordNotableEventEffect(
                                "Vous avez accordé votre confiance à Aya.",
                                "journey"),
                        ]),
                ]),
            new NarrativeNode("end", "Aya remembers your choice.", null, [], [], true),
        ]);
}