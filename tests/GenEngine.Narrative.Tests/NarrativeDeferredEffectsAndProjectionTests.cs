namespace GenEngine.Narrative.Tests;

public sealed class NarrativeDeferredEffectsAndProjectionTests
{
    [Fact]
    public void LogicalDeferredEffectsRoundTripWithPolymorphicJson()
    {
        ScenarioDocument restored = NarrativeJson.Deserialize<ScenarioDocument>(
            NarrativeJson.Serialize(CreateScenario()));
        ScheduleEffect scheduled = Assert.IsType<ScheduleEffect>(restored.Nodes[0].OnEnterEffects[2]);

        Assert.Equal(2, scheduled.Days);
        Assert.IsType<HasEvidenceCondition>(scheduled.Condition);
        Assert.IsType<AdvanceLogicalTimeEffect>(restored.Nodes[0].Choices[0].Effects[0]);
    }

    [Fact]
    public void DeferredEffectsWaitForTurnLogicalDayAndCondition()
    {
        ScenarioDocument scenario = CreateScenario();

        GameState waiting = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Start(scenario),
            "investigate");

        Assert.Equal(1, waiting.Turn);
        Assert.Equal(2, waiting.World.LogicalDay);
        Assert.DoesNotContain("decoded-signal", waiting.World.Rewards);
        Assert.Equal(2, waiting.World.ScheduledEffects.Count);

        GameState completed = NarrativeRuntime.SubmitChoice(scenario, waiting, "decode");

        Assert.Equal(2, completed.Turn);
        Assert.Contains("decoded-signal", completed.World.Rewards);
        Assert.Empty(completed.World.ScheduledEffects);
        Assert.Contains(
            new JournalEntry("Le rendez-vous est arrivé.", "timeline", 2),
            completed.World.Journal);
    }

    [Fact]
    public void DeferredTriggerMetadataRoundTripsInCurrentSave()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState waiting = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Start(scenario),
            "investigate");
        GameSave save = GameSaveSerializer.Create(2, 42, DateTimeOffset.UnixEpoch, waiting);

        GameSave restored = GameSaveSerializer.Deserialize(
            GameSaveSerializer.Serialize(save),
            0,
            DateTimeOffset.MinValue);
        ScheduledEffect conditional = Assert.Single(
            restored.State.World.ScheduledEffects,
            static effect => effect.Condition is not null);

        Assert.Equal(2, restored.State.World.LogicalDay);
        Assert.Equal(2, conditional.DueLogicalDay);
        Assert.IsType<HasEvidenceCondition>(conditional.Condition);
    }

    [Fact]
    public void PlayerProjectionExposesStableSummaryCollectionAndJournal()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "investigate"),
            "decode");

        PlayerProjection projection = PlayerProjectionBuilder.Build(state);

        Assert.Equal("ending", projection.Summary.CurrentNodeId);
        Assert.Equal(SessionStatus.Completed, projection.Summary.Status);
        Assert.Equal(2, projection.Summary.Turn);
        Assert.Equal(2, projection.Summary.LogicalDay);
        Assert.Equal(3, projection.Summary.VisitedNodeCount);
        Assert.Equal(2, projection.Summary.ChoiceCount);
        Assert.Equal(0, projection.Summary.PendingDeferredEffectCount);
        Assert.Equal(["signal-fragment"], projection.Collection.Evidence);
        Assert.Equal(["field-map"], projection.Collection.Inventory);
        Assert.Equal(["decoded-signal"], projection.Collection.Rewards);
        Assert.Equal(2, projection.Journal.Count);
    }

    [Fact]
    public void ValidatorRejectsInvalidLogicalSchedulesAndTheirConditions()
    {
        ScenarioDocument scenario = CreateScenario() with
        {
            Nodes =
            [
                new NarrativeNode(
                    "start",
                    "Invalid",
                    null,
                    [
                        new ScheduleEffect(0, new GrantRewardEffect("reward"))
                        {
                            Days = -1,
                            Condition = new VisitedNodeCondition("missing"),
                        },
                        new AdvanceLogicalTimeEffect(-1),
                    ],
                    [new NarrativeChoice("finish", "Finish", "ending", null, [])]),
                new NarrativeNode("ending", "End", null, [], [], true),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.Contains(report.Issues, static issue => issue.Code == "schedule_days_invalid");
        Assert.Contains(report.Issues, static issue => issue.Code == "logical_time_days_invalid");
        Assert.Contains(report.Issues, static issue => issue.Code == "condition_node_missing");
    }

    [Fact]
    public void PreviewRejectsNegativeLogicalDay()
    {
        WorldState world = WorldState.Empty();
        world.LogicalDay = -1;

        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.PreviewAt(CreateScenario(), "start", world));

        Assert.Equal("preview_logical_day_invalid", exception.Code);
    }

    private static ScenarioDocument CreateScenario() => new(
        NarrativeVersions.LatestSchema,
        "Deferred signal",
        "start",
        [
            new NarrativeNode(
                "start",
                "Un signal traverse la brume.",
                null,
                [
                    new CollectEffect("field-map"),
                    new AssignEffect("focus", 1),
                    new ScheduleEffect(0, new GrantRewardEffect("decoded-signal"))
                    {
                        Days = 2,
                        Condition = new HasEvidenceCondition("signal-fragment"),
                    },
                    new ScheduleEffect(
                        2,
                        new RecordNotableEventEffect("Le rendez-vous est arrivé.", "timeline")),
                ],
                [
                    new NarrativeChoice(
                        "investigate",
                        "Enquêter",
                        "relay",
                        null,
                        [
                            new AdvanceLogicalTimeEffect(2),
                            new RecordNotableEventEffect("Vous avez suivi le signal.", "journey"),
                        ]),
                ]),
            new NarrativeNode(
                "relay",
                "Le fragment est accessible.",
                null,
                [],
                [
                    new NarrativeChoice(
                        "decode",
                        "Décoder",
                        "ending",
                        null,
                        [new DiscoverEvidenceEffect("signal-fragment")]),
                ]),
            new NarrativeNode("ending", "Le message est révélé.", null, [], [], true),
        ]);
}