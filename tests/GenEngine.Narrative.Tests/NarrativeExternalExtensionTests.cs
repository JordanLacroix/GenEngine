namespace GenEngine.Narrative.Tests;

public sealed class NarrativeExternalExtensionTests
{
    [Fact]
    public void SuppliedAnalysisIsFrozenThenExternalEffectIsEmittedDeterministically()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState initial = NarrativeRuntime.Start(scenario);
        var analysis = new TextAnalysisResult(
            "reflection",
            true,
            ["source", "incertitude"],
            2,
            "Deux critères de la rubrique ont été reconnus.");

        GameState awaitingConfirmation = NarrativeRuntime.SubmitTextAnalysis(scenario, initial, analysis);
        GameState completed = NarrativeRuntime.ConfirmTextAnalysis(scenario, awaitingConfirmation, true);
        ExternalEffectEvent emitted = Assert.Single(completed.World.ExternalEvents);

        Assert.Equal(SessionStatus.AwaitingValidation, awaitingConfirmation.Status);
        Assert.NotSame(analysis, awaitingConfirmation.PendingTextAnalysis);
        Assert.Equal(
            NarrativeJson.Serialize(analysis),
            NarrativeJson.Serialize(awaitingConfirmation.PendingTextAnalysis));
        Assert.Equal(1, emitted.Sequence);
        Assert.Equal("pedagogy.reflection.accepted", emitted.EventName);
        Assert.Equal("critical-thinking", emitted.Attributes["dimension"]);
        Assert.Equal(1, emitted.Turn);
        Assert.Equal(0, emitted.LogicalDay);
        Assert.Equal(SessionStatus.Completed, completed.Status);
    }

    [Fact]
    public void SuppliedAnalysisMustMatchTheDeclaredRubric()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState initial = NarrativeRuntime.Start(scenario);
        var invalid = new TextAnalysisResult(
            "reflection",
            true,
            ["invented-criterion", "source"],
            2,
            "Invalid external result.");

        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.SubmitTextAnalysis(scenario, initial, invalid));

        Assert.Equal("text_analysis_invalid", exception.Code);
        Assert.Equal(SessionStatus.AwaitingExternalInput, initial.Status);
        Assert.Empty(initial.World.ExternalEvents);
    }

    [Fact]
    public void KeywordAnalyzerImplementsTheReplaceableInputPort()
    {
        FreeTextInteraction interaction = Assert.IsType<FreeTextInteraction>(CreateScenario().Nodes[0].Interactions![0]);
        var analyzer = new KeywordTextInputAnalyzer();

        TextAnalysisResult result = analyzer.Analyze(
            interaction,
            "Je cite une SOURCE et je reconnais l’incertitude.");

        Assert.IsAssignableFrom<ITextInputAnalyzer>(analyzer);
        Assert.True(result.IsAccepted);
        Assert.Equal(["incertitude", "source"], result.MatchedTerms);
    }

    [Fact]
    public void ExternalEventsRoundTripWithoutLosingTheirSequenceOrAttributes()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState analyzed = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "source incertitude");
        GameState completed = NarrativeRuntime.ConfirmTextAnalysis(scenario, analyzed, true);
        GameSave save = GameSaveSerializer.Create(2, 5, DateTimeOffset.UnixEpoch, completed);

        GameSave restored = GameSaveSerializer.Deserialize(
            GameSaveSerializer.Serialize(save),
            0,
            DateTimeOffset.MinValue);

        Assert.Equal(
            NarrativeJson.Serialize(completed.World.ExternalEvents),
            NarrativeJson.Serialize(restored.State.World.ExternalEvents));
    }

    [Fact]
    public void ValidatorBoundsExternalEventContracts()
    {
        ScenarioDocument scenario = CreateScenario();
        FreeTextInteraction interaction = Assert.IsType<FreeTextInteraction>(scenario.Nodes[0].Interactions![0]);
        scenario = scenario with
        {
            Nodes =
            [
                scenario.Nodes[0] with
                {
                    Interactions =
                    [
                        interaction with
                        {
                            AcceptedEffects =
                            [
                                new EmitExternalEventEffect(
                                    string.Empty,
                                    new Dictionary<string, string> { [string.Empty] = new string('x', 501) }),
                            ],
                        },
                    ],
                },
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.Contains(report.Issues, static issue => issue.Code == "external_event_name_invalid");
        Assert.Contains(report.Issues, static issue => issue.Code == "external_event_attributes_invalid");
    }

    private static ScenarioDocument CreateScenario() => new(
        NarrativeVersions.LatestSchema,
        "External analysis",
        "reflection",
        [
            new NarrativeNode("reflection", "Justifiez votre décision.", null, [], [], true)
            {
                Interactions =
                [
                    new FreeTextInteraction(
                        "reflection",
                        "Citez la source et explicitez l'incertitude.",
                        ["source", "incertitude"],
                        2,
                        [
                            new EmitExternalEventEffect(
                                "pedagogy.reflection.accepted",
                                new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["dimension"] = "critical-thinking",
                                }),
                        ],
                        []),
                ],
            },
        ]);
}