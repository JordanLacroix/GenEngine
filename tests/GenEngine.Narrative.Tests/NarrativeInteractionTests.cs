namespace GenEngine.Narrative.Tests;

public sealed class NarrativeInteractionTests
{
    [Fact]
    public void TypedSequenceRunsNarrationQuizChoiceAndEndingInOrder()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        Assert.Equal(InteractionKind.Narration, NarrativeRuntime.GetCurrentStep(scenario, state).Kind);

        state = NarrativeRuntime.Continue(scenario, state);
        CurrentStep quiz = NarrativeRuntime.GetCurrentStep(scenario, state);
        Assert.Equal(InteractionKind.Quiz, quiz.Kind);
        Assert.Equal(2, quiz.Choices.Count);

        state = NarrativeRuntime.SubmitAnswer(scenario, state, "fact");
        Assert.Contains("fact-checker", state.World.Rewards);
        Assert.Equal(InteractionKind.ChoiceSet, NarrativeRuntime.GetCurrentStep(scenario, state).Kind);

        state = NarrativeRuntime.SubmitChoice(scenario, state, "conclude");
        CurrentStep ending = NarrativeRuntime.GetCurrentStep(scenario, state);
        Assert.Equal(InteractionKind.Narration, ending.Kind);
        Assert.Equal(SessionStatus.AwaitingInput, ending.Status);

        state = NarrativeRuntime.Continue(scenario, state);

        Assert.Equal(SessionStatus.Completed, state.Status);
        Assert.Equal(4, state.Turn);
        Assert.Equal(4, state.World.InteractionHistory.Count);
        Assert.True(state.World.InteractionHistory[1].WasCorrect);
        Assert.Equal(InteractionKind.Completed, NarrativeRuntime.GetCurrentStep(scenario, state).Kind);
        Assert.Contains(state.World.Journal, static entry => entry.Label == "Vous avez distingué un fait d’une opinion.");
    }

    [Fact]
    public void IncorrectQuizAnswerAppliesItsOwnEffectsWithoutRevealingCorrectAnswer()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Continue(scenario, NarrativeRuntime.Start(scenario));

        state = NarrativeRuntime.SubmitAnswer(scenario, state, "opinion");

        Assert.Equal(1, state.World.Variables["mistakes"]);
        Assert.DoesNotContain("fact-checker", state.World.Rewards);
        Assert.False(state.World.InteractionHistory[^1].WasCorrect);
    }

    [Fact]
    public void AnalyzerExploresEveryQuizAnswerAndChoice()
    {
        SimulationReport report = ScenarioAnalyzer.Explore(CreateScenario());

        Assert.Contains("ending", report.EndingNodeIds);
        Assert.Empty(report.DeadEnds);
        Assert.False(report.StateBudgetExceeded);
        Assert.True(report.ExploredStates >= 8);
    }

    [Fact]
    public void ValidatorRejectsTypedInteractionsInLegacySchemaAndMalformedQuiz()
    {
        ScenarioDocument scenario = CreateScenario() with { SchemaVersion = NarrativeVersions.Schema };
        NarrativeNode start = scenario.Nodes[0] with
        {
            Interactions =
            [
                new QuizInteraction(
                    "quiz",
                    "Question",
                    [new QuizAnswer("same", "One"), new QuizAnswer("same", "Two")],
                    "missing",
                    [],
                    []),
            ],
        };
        scenario = scenario with { Nodes = [start, scenario.Nodes[1]] };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.Contains(report.Issues, static issue => issue.Code == "interactions_require_schema_2");
        Assert.Contains(report.Issues, static issue => issue.Code == "duplicate_quiz_answer");
        Assert.Contains(report.Issues, static issue => issue.Code == "quiz_correct_answer_missing");
        Assert.Contains(report.Issues, static issue => issue.Code == "interaction_sequence_incomplete");
    }

    [Fact]
    public void LegacyScenarioSerializationDoesNotGainInteractionProperties()
    {
        ScenarioDocument legacy = CreateScenario() with
        {
            SchemaVersion = NarrativeVersions.Schema,
            InitialNodeId = "start",
            Nodes =
            [
                new NarrativeNode(
                    "start",
                    "Legacy.",
                    null,
                    [],
                    [new NarrativeChoice("end", "End", "ending", null, [])]),
                new NarrativeNode("ending", "Done.", null, [], [], true),
            ],
        };

        string json = NarrativeJson.Serialize(legacy);
        ValidationReport report = ScenarioValidator.Validate(legacy);

        Assert.DoesNotContain("interactions", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(report.IsValid, string.Join(" | ", report.Issues.Select(static issue => issue.Code)));
        Assert.Equal(SessionStatus.Completed, ScenarioSimulator.RunFirstAvailableChoice(legacy).Status);
    }

    private static ScenarioDocument CreateScenario() => new(
        NarrativeVersions.LatestSchema,
        "Critical reading",
        "lesson",
        [
            new NarrativeNode("lesson", "A short lesson.", null, [], [])
            {
                Interactions =
                [
                    new NarrationInteraction(
                        "intro",
                        "Une affirmation apparaît sans source.",
                        [new DiscoverEvidenceEffect("unsupported-claim")]),
                    new QuizInteraction(
                        "fact-or-opinion",
                        "« Cette ville compte cent habitants » est…",
                        [
                            new QuizAnswer("fact", "un fait vérifiable"),
                            new QuizAnswer("opinion", "une opinion"),
                        ],
                        "fact",
                        [new GrantRewardEffect("fact-checker")],
                        [new IncrementEffect("mistakes", 1)]),
                    new ChoiceSetInteraction(
                        "next",
                        "Comment poursuivre ?",
                        [new NarrativeChoice("conclude", "Conclure", "ending", null, [])]),
                ],
            },
            new NarrativeNode("ending", "Conclusion.", null, [], [], true)
            {
                Interactions =
                [
                    new NarrationInteraction(
                        "outro",
                        "Vous confrontez désormais les affirmations à leurs sources.",
                        [new RecordNotableEventEffect("Vous avez distingué un fait d’une opinion.", "learning")]),
                ],
            },
        ]);
}