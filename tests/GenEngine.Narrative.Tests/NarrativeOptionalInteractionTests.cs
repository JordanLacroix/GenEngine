namespace GenEngine.Narrative.Tests;

/// <summary>
/// An optional interaction is content the player may look at, not a toll gate.
/// Playing it applies its effects and hands the node's choices back, richer than
/// before because a condition can now see what it granted; ignoring it costs the
/// player nothing but that extra content.
/// </summary>
public sealed class NarrativeOptionalInteractionTests
{
    [Fact]
    public void OptionalInteractionIsOfferedTogetherWithTheNodeExitChoices()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
        Assert.Equal(InteractionKind.Narration, step.Kind);
        Assert.True(step.IsOptional);
        Assert.Equal(["leave"], step.ExitChoices.Select(static choice => choice.Id));
    }

    [Fact]
    public void OptionalInteractionCanBeSkippedByTakingAnExitChoice()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);

        GameState skipped = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "leave");

        Assert.Equal("ending", skipped.CurrentNodeId);
        Assert.DoesNotContain("heard-the-hall", skipped.World.Evidence);

        // A skipped interaction leaves no trace: only the exit choice set is
        // recorded, so a condition testing the unplayed content stays false.
        InteractionHistoryEntry entry = Assert.Single(skipped.World.InteractionHistory);
        Assert.Equal("first-decision", entry.InteractionId);
        Assert.Equal("leave", entry.InputId);
    }

    [Fact]
    public void PlayingAnOptionalInteractionAppliesItsEffectsAndRevealsAConditionedChoice()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);

        GameState played = NarrativeRuntime.Continue(scenario, NarrativeRuntime.Start(scenario));
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, played);

        Assert.Contains("heard-the-hall", played.World.Evidence);
        Assert.Equal("start", played.CurrentNodeId);
        Assert.Equal(InteractionKind.ChoiceSet, step.Kind);

        // The conditioned line only exists for a player who listened.
        Assert.Equal(["leave", "answer-the-hall"], step.Choices.Select(static choice => choice.Id));
        Assert.Empty(step.ExitChoices);
    }

    [Fact]
    public void ConditionedChoiceStaysHiddenForAPlayerWhoSkipped()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.DoesNotContain("answer-the-hall", step.ExitChoices.Select(static choice => choice.Id));
    }

    [Fact]
    public void MandatoryInteractionStillBlocksTheNodeChoices()
    {
        ScenarioDocument scenario = CreateScenario(optional: false);
        GameState start = NarrativeRuntime.Start(scenario);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, start);
        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.SubmitChoice(scenario, start, "leave"));

        Assert.False(step.IsOptional);
        Assert.Empty(step.ExitChoices);
        Assert.Empty(step.Choices);
        Assert.Equal("choice_not_available", exception.Code);
    }

    /// <summary>
    /// Mixed node, documented semantics: the exit is offered only when every
    /// interaction from the current index up to the exit choice set is optional.
    /// Here a mandatory quiz sits behind an optional narration, so the narration
    /// shows no exit — the player cannot jump over the quiz. Once the quiz is
    /// answered the exit becomes reachable again.
    /// </summary>
    [Fact]
    public void OptionalInteractionBeforeAMandatoryOneDoesNotOpenTheExit()
    {
        ScenarioDocument scenario = CreateMixedScenario();
        GameState start = NarrativeRuntime.Start(scenario);

        CurrentStep narration = NarrativeRuntime.GetCurrentStep(scenario, start);
        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.SubmitChoice(scenario, start, "leave"));

        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
        Assert.True(narration.IsOptional);
        Assert.Empty(narration.ExitChoices);
        Assert.Equal("choice_not_available", exception.Code);
    }

    [Fact]
    public void MandatoryInteractionAfterAnOptionalOneStillHasToBePlayed()
    {
        ScenarioDocument scenario = CreateMixedScenario();

        GameState afterNarration = NarrativeRuntime.Continue(scenario, NarrativeRuntime.Start(scenario));
        CurrentStep quiz = NarrativeRuntime.GetCurrentStep(scenario, afterNarration);
        GameState afterQuiz = NarrativeRuntime.SubmitAnswer(scenario, afterNarration, "fifth");

        Assert.Equal(InteractionKind.Quiz, quiz.Kind);
        Assert.False(quiz.IsOptional);
        Assert.Empty(quiz.ExitChoices);
        Assert.Equal(InteractionKind.ChoiceSet, NarrativeRuntime.GetCurrentStep(scenario, afterQuiz).Kind);
    }

    /// <summary>
    /// A run of consecutive optional interactions stays a single forward walk:
    /// the exit is open at every index, and playing one simply moves to the next.
    /// </summary>
    [Fact]
    public void ConsecutiveOptionalInteractionsKeepTheExitOpenAtEveryIndex()
    {
        ScenarioDocument scenario = CreateTwoOptionalScenario();
        GameState start = NarrativeRuntime.Start(scenario);
        GameState afterFirst = NarrativeRuntime.Continue(scenario, start);

        CurrentStep second = NarrativeRuntime.GetCurrentStep(scenario, afterFirst);
        GameState skipped = NarrativeRuntime.SubmitChoice(scenario, afterFirst, "leave");

        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
        Assert.True(second.IsOptional);
        Assert.Equal(["leave"], second.ExitChoices.Select(static choice => choice.Id));
        Assert.Contains("first", skipped.World.Inventory);
        Assert.DoesNotContain("second", skipped.World.Inventory);
        Assert.Equal("ending", skipped.CurrentNodeId);
    }

    [Fact]
    public void OptionalFreeTextCanBeSkippedDespiteAwaitingExternalInput()
    {
        ScenarioDocument scenario = CreateFreeTextScenario();
        GameState start = NarrativeRuntime.Start(scenario);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, start);
        GameState skipped = NarrativeRuntime.SubmitChoice(scenario, start, "leave");

        Assert.Equal(SessionStatus.AwaitingExternalInput, start.Status);
        Assert.True(step.IsOptional);
        Assert.Equal(["leave"], step.ExitChoices.Select(static choice => choice.Id));
        Assert.Equal("ending", skipped.CurrentNodeId);
    }

    /// <summary>
    /// While an analysis awaits validation the player is mid-flow: the exit is
    /// withdrawn until the analysis is confirmed or refused, so a single input
    /// can never be interpreted two ways.
    /// </summary>
    [Fact]
    public void ExitIsWithdrawnWhileATextAnalysisAwaitsValidation()
    {
        ScenarioDocument scenario = CreateFreeTextScenario();
        GameState pending = NarrativeRuntime.SubmitText(scenario, NarrativeRuntime.Start(scenario), "resonance");

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, pending);
        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.SubmitChoice(scenario, pending, "leave"));

        Assert.Equal(SessionStatus.AwaitingValidation, pending.Status);
        Assert.Empty(step.ExitChoices);
        Assert.Equal("session_not_awaiting_input", exception.Code);
    }

    [Fact]
    public void ExplorationWalksBothTheSkippedAndThePlayedBranch()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);

        SimulationReport report = ScenarioAnalyzer.Explore(scenario);

        Assert.Empty(report.DeadEnds);
        Assert.Equal(["ending"], report.EndingNodeIds);
    }

    [Fact]
    public void OptionalFlagDeclaredBeforeSchemaFourIsRejected()
    {
        ScenarioDocument scenario = CreateScenario(optional: true) with { SchemaVersion = NarrativeVersions.MediaSchema };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "optional_requires_schema_4");
    }

    [Fact]
    public void OptionalFlagOnAChoiceSetIsRejected()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);
        NarrativeNode start = scenario.Nodes[0];
        scenario = scenario with
        {
            Nodes =
            [
                start with { Interactions = [start.Interactions![0], start.Interactions[1] with { IsOptional = true }] },
                scenario.Nodes[1],
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "optional_interaction_not_supported");
    }

    [Fact]
    public void OptionalFlagOnAGateIsRejected()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.OptionalInteractionsSchema,
            "Optional gate",
            "start",
            [
                new NarrativeNode("start", "The hall listens.", null, [], [])
                {
                    Interactions =
                    [
                        new CharacteristicGateInteraction("gate", new AlwaysCondition(), "ending", "ending", [], [])
                        {
                            IsOptional = true,
                        },
                    ],
                },
                new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
            ]);

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "optional_interaction_not_supported");
    }

    /// <summary>
    /// An ending node has no exit choice set, so nothing could ever be skipped
    /// there: the flag would promise the player a way out that does not exist.
    /// </summary>
    [Fact]
    public void OptionalFlagWithoutAnExitChoiceSetIsRejected()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.OptionalInteractionsSchema,
            "Optional epilogue",
            "start",
            [
                new NarrativeNode("start", "The hall listens.", null, [], [])
                {
                    Interactions =
                    [
                        new ChoiceSetInteraction(
                            "decide",
                            "How do you leave?",
                            [new NarrativeChoice("leave", "Leave", "ending", null, [])]),
                    ],
                },
                new NarrativeNode("ending", "The interval resolves.", null, [], [], true)
                {
                    Interactions =
                    [
                        new NarrationInteraction("epilogue", "The fork keeps humming.", []) { IsOptional = true },
                    ],
                },
            ]);

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "optional_requires_exit_choice_set");
    }

    [Fact]
    public void ExplicitlyMandatoryFlagIsAcceptedAndKeepsTheInteractionBlocking()
    {
        ScenarioDocument scenario = CreateScenario(optional: true);
        NarrativeNode start = scenario.Nodes[0];
        scenario = scenario with
        {
            Nodes =
            [
                start with { Interactions = [start.Interactions![0] with { IsOptional = false }, start.Interactions[1]] },
                scenario.Nodes[1],
            ],
        };

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
        Assert.False(step.IsOptional);
        Assert.Empty(step.ExitChoices);
    }

    [Fact]
    public void DocumentedOptionalExampleIsValidAndSkippable()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "specs", "domain", "examples", "optional-aside.json");
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(File.ReadAllText(path));

        ValidationReport report = ScenarioValidator.Validate(scenario);
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(report.IsValid);
        Assert.Equal(NarrativeVersions.OptionalInteractionsSchema, scenario.SchemaVersion);
        Assert.True(step.IsOptional);
        Assert.NotEmpty(step.ExitChoices);
    }

    private static ScenarioDocument CreateScenario(bool optional) => new(
        NarrativeVersions.OptionalInteractionsSchema,
        "Optional aside",
        "start",
        [
            new NarrativeNode("start", "The tuning fork hums in the atrium.", null, [], [])
            {
                Interactions =
                [
                    new NarrationInteraction(
                        "listen",
                        "The hall listens before it answers.",
                        [new DiscoverEvidenceEffect("heard-the-hall")])
                    {
                        IsOptional = optional ? true : null,
                    },
                    new ChoiceSetInteraction(
                        "first-decision",
                        "How do you approach the hall?",
                        [
                            new NarrativeChoice("leave", "Cross the atrium", "ending", null, []),
                            new NarrativeChoice(
                                "answer-the-hall",
                                "Answer the note you just heard",
                                "ending",
                                new HasEvidenceCondition("heard-the-hall"),
                                []),
                        ]),
                ],
            },
            new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
        ]);

    private static ScenarioDocument CreateMixedScenario() => new(
        NarrativeVersions.OptionalInteractionsSchema,
        "Mixed node",
        "start",
        [
            new NarrativeNode("start", "The tuning fork hums in the atrium.", null, [], [])
            {
                Interactions =
                [
                    new NarrationInteraction("listen", "The hall listens.", []) { IsOptional = true },
                    new QuizInteraction(
                        "pitch",
                        "Which interval is the hall holding?",
                        [new QuizAnswer("fifth", "A perfect fifth"), new QuizAnswer("third", "A major third")],
                        "fifth",
                        [],
                        []),
                    new ChoiceSetInteraction(
                        "first-decision",
                        "How do you approach the hall?",
                        [new NarrativeChoice("leave", "Cross the atrium", "ending", null, [])]),
                ],
            },
            new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
        ]);

    private static ScenarioDocument CreateTwoOptionalScenario() => new(
        NarrativeVersions.OptionalInteractionsSchema,
        "Two asides",
        "start",
        [
            new NarrativeNode("start", "The tuning fork hums in the atrium.", null, [], [])
            {
                Interactions =
                [
                    new NarrationInteraction("first", "A first aside.", [new CollectEffect("first")]) { IsOptional = true },
                    new NarrationInteraction("second", "A second aside.", [new CollectEffect("second")]) { IsOptional = true },
                    new ChoiceSetInteraction(
                        "first-decision",
                        "How do you approach the hall?",
                        [new NarrativeChoice("leave", "Cross the atrium", "ending", null, [])]),
                ],
            },
            new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
        ]);

    private static ScenarioDocument CreateFreeTextScenario() => new(
        NarrativeVersions.OptionalInteractionsSchema,
        "Optional reflection",
        "start",
        [
            new NarrativeNode("start", "The tuning fork hums in the atrium.", null, [], [])
            {
                Interactions =
                [
                    new FreeTextInteraction(
                        "reflect",
                        "What did the hall answer?",
                        ["resonance"],
                        1,
                        [new DiscoverEvidenceEffect("reflected")],
                        [])
                    {
                        IsOptional = true,
                    },
                    new ChoiceSetInteraction(
                        "first-decision",
                        "How do you approach the hall?",
                        [new NarrativeChoice("leave", "Cross the atrium", "ending", null, [])]),
                ],
            },
            new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
        ]);
}