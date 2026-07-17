namespace GenEngine.Narrative.Tests;

public sealed class NarrativeFreeTextTests
{
    [Fact]
    public void TextIsAnalyzedWithoutProgressingOrPersistingRawInput()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        state = NarrativeRuntime.SubmitText(scenario, state, "J'ÉCOUTE puis je compare ce témoignage.");

        Assert.Equal(SessionStatus.AwaitingValidation, state.Status);
        Assert.Equal(0, state.Turn);
        Assert.True(state.PendingTextAnalysis!.IsAccepted);
        Assert.Equal(["compare", "écoute"], state.PendingTextAnalysis.MatchedTerms);
        Assert.DoesNotContain("témoignage", NarrativeJson.Serialize(state), StringComparison.OrdinalIgnoreCase);
        GameSave restored = GameSaveSerializer.Deserialize(
            GameSaveSerializer.Serialize(GameSaveSerializer.Create(2, 42, DateTimeOffset.UnixEpoch, state)),
            42,
            DateTimeOffset.UnixEpoch);
        Assert.Equal(state.PendingTextAnalysis.InteractionId, restored.State.PendingTextAnalysis!.InteractionId);
        Assert.Equal(state.PendingTextAnalysis.IsAccepted, restored.State.PendingTextAnalysis.IsAccepted);
        Assert.Equal(state.PendingTextAnalysis.MatchedTerms, restored.State.PendingTextAnalysis.MatchedTerms);
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);
        Assert.Equal(InteractionKind.FreeText, step.Kind);
        Assert.Equal(state.PendingTextAnalysis, step.PendingTextAnalysis);
    }

    [Fact]
    public void ConfirmedAcceptedAnalysisAppliesEffectsAndCompletesInteraction()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState pending = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "J'écoute et je compare.");

        GameState completed = NarrativeRuntime.ConfirmTextAnalysis(scenario, pending, true);

        Assert.Equal(SessionStatus.Completed, completed.Status);
        Assert.Equal(1, completed.Turn);
        Assert.Contains("critical-reader", completed.World.Rewards);
        Assert.Null(completed.PendingTextAnalysis);
        Assert.True(Assert.Single(completed.World.InteractionHistory).WasCorrect);
    }

    [Fact]
    public void ConfirmedRejectedAnalysisAppliesItsOwnEffects()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState pending = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "Je réponds sans mots attendus.");

        GameState completed = NarrativeRuntime.ConfirmTextAnalysis(scenario, pending, true);

        Assert.Equal(SessionStatus.Completed, completed.Status);
        Assert.Equal(1, completed.World.Variables["reflection-help"]);
        Assert.False(Assert.Single(completed.World.InteractionHistory).WasCorrect);
    }

    [Fact]
    public void RefusingAnalysisReturnsToInputWithoutConsumingTurn()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState pending = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "J'écoute et je compare.");

        GameState retry = NarrativeRuntime.ConfirmTextAnalysis(scenario, pending, false);

        Assert.Equal(SessionStatus.AwaitingExternalInput, retry.Status);
        Assert.Equal(0, retry.Turn);
        Assert.Null(retry.PendingTextAnalysis);
        Assert.Empty(retry.World.InteractionHistory);
    }

    [Fact]
    public void PauseAndResumePreserveValidationStage()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState pending = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "J'écoute et je compare.");

        GameState resumed = NarrativeRuntime.Resume(NarrativeRuntime.Pause(pending));

        Assert.Equal(SessionStatus.AwaitingValidation, resumed.Status);
        Assert.NotNull(resumed.PendingTextAnalysis);
        Assert.Null(resumed.StatusBeforePause);
    }

    [Fact]
    public void TextLengthAndAuthoringRulesAreBounded()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState partialWord = NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            "Je compare une ressource.");
        Assert.False(partialWord.PendingTextAnalysis!.IsAccepted);
        Assert.Equal(["compare"], partialWord.PendingTextAnalysis.MatchedTerms);

        NarrativeException exception = Assert.Throws<NarrativeException>(() => NarrativeRuntime.SubmitText(
            scenario,
            NarrativeRuntime.Start(scenario),
            new string('a', DeterministicTextAnalyzer.MaximumTextLength + 1)));
        Assert.Equal("text_too_long", exception.Code);

        FreeTextInteraction malformed = new("reflection", "", ["écoute", "ecoute"], 3, [], []);
        NarrativeNode node = scenario.Nodes[0] with { Interactions = [malformed] };
        ValidationReport report = ScenarioValidator.Validate(scenario with { Nodes = [node] });
        Assert.Contains(report.Issues, static issue => issue.Code == "free_text_prompt_required");
        Assert.Contains(report.Issues, static issue => issue.Code == "free_text_terms_invalid");
        Assert.Contains(report.Issues, static issue => issue.Code == "free_text_threshold_invalid");
    }

    [Fact]
    public void SimulatorCompletesDeterministicFreeTextInteraction()
    {
        ScenarioDocument scenario = CreateScenario();

        GameState state = ScenarioSimulator.RunFirstAvailableChoice(scenario);
        SimulationReport report = ScenarioAnalyzer.Explore(scenario);

        Assert.Equal(SessionStatus.Completed, state.Status);
        Assert.Contains("reflection", report.EndingNodeIds);
        Assert.Empty(report.DeadEnds);
    }

    private static ScenarioDocument CreateScenario() => new(
        NarrativeVersions.LatestSchema,
        "Critical reflection",
        "reflection",
        [
            new NarrativeNode("reflection", "Explain your method.", null, [], [], true)
            {
                Interactions =
                [
                    new FreeTextInteraction(
                        "reflection",
                        "Comment vérifiez-vous une affirmation ?",
                        ["écoute", "compare", "source"],
                        2,
                        [new GrantRewardEffect("critical-reader")],
                        [new IncrementEffect("reflection-help", 1)]),
                ],
            },
        ]);
}