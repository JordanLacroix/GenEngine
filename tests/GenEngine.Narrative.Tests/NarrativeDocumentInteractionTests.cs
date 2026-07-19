namespace GenEngine.Narrative.Tests;

public sealed class NarrativeDocumentInteractionTests
{
    [Fact]
    public void DocumentStepCarriesItsContentAndItsExcerptDisclosure()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);

        Assert.Equal(InteractionKind.Document, step.Kind);
        Assert.Equal("the-memo", step.InteractionId);
        Assert.Equal("Consulter la note de service", step.Text);
        PresentedDocument document = Assert.IsType<PresentedDocument>(step.Document);
        Assert.Equal("Note de service — accordage du hall", document.Title);
        Assert.Equal(DocumentNature.Memo, document.Nature);
        Assert.Equal(new DocumentExcerpt(2, 14, DocumentUnit.Lines), document.Excerpt);
        Assert.Equal("Objet", document.Headers![0].Name);
    }

    /// <summary>
    /// The whole point of the mechanic: consulting is never compulsory, so an
    /// unread document must let the session run to its end untouched.
    /// </summary>
    [Fact]
    public void UnconsultedOptionalDocumentLetsTheSessionProgressAndLeavesNoTrace()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);
        GameState left = NarrativeRuntime.SubmitChoice(scenario, state, "sign-anyway");

        Assert.True(step.IsOptional);
        Assert.Contains(step.ExitChoices, choice => choice.Id == "sign-anyway");
        Assert.DoesNotContain(step.ExitChoices, choice => choice.Id == "refuse-with-reason");
        Assert.Equal(SessionStatus.Completed, left.Status);
        Assert.DoesNotContain(
            left.World.InteractionHistory,
            entry => entry.InteractionId == "the-memo");
        Assert.False(ConditionEvaluator.Evaluate(new ConsultedDocumentCondition("the-memo"), left.World));
    }

    /// <summary>
    /// And its pedagogical counterpart: reading the memo is what unlocks the
    /// informed answer. A purely decorative document would teach nothing.
    /// </summary>
    [Fact]
    public void ConsultingTheDocumentSatisfiesTheConditionAndRevealsADistinctChoice()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState state = NarrativeRuntime.Start(scenario);

        GameState consulted = NarrativeRuntime.ConsultDocument(scenario, state);
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, consulted);

        Assert.True(ConditionEvaluator.Evaluate(new ConsultedDocumentCondition("the-memo"), consulted.World));
        Assert.Equal(InteractionKind.ChoiceSet, step.Kind);
        Assert.Contains(step.Choices, choice => choice.Id == "refuse-with-reason");
        Assert.Contains(step.Choices, choice => choice.Id == "sign-anyway");
        Assert.Contains(
            consulted.World.InteractionHistory,
            entry => entry.InteractionId == "the-memo" && entry.InputId == "consulted");
        Assert.Contains("read-the-memo", consulted.World.Evidence);
    }

    /// <summary>
    /// Consulting advances the sequence, so the same document cannot be consulted
    /// a second time while standing on it: the second call is refused instead of
    /// applying the consult effects twice.
    /// </summary>
    [Fact]
    public void ConsultingTwiceNeverDoublesTheConsultEffects()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState consulted = NarrativeRuntime.ConsultDocument(scenario, NarrativeRuntime.Start(scenario));

        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.ConsultDocument(scenario, consulted));

        Assert.Equal("interaction_not_document", exception.Code);
        Assert.Equal(1, consulted.World.Variables["memo-reads"]);
        Assert.Single(
            consulted.World.InteractionHistory,
            entry => entry.InteractionId == "the-memo");
    }

    [Fact]
    public void ConsultingIsDeterministicAndReplaysFromTheRecordedCommandsAlone()
    {
        ScenarioDocument scenario = CreateScenario();

        GameState first = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.ConsultDocument(scenario, NarrativeRuntime.Start(scenario)),
            "refuse-with-reason");
        GameState second = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.ConsultDocument(scenario, NarrativeRuntime.Start(scenario)),
            "refuse-with-reason");

        Assert.Equal(NarrativeJson.Serialize(first), NarrativeJson.Serialize(second));
    }

    [Fact]
    public void ADocumentIsRefusedWhenItsChoiceIsGatedOnAnUnknownDocumentId()
    {
        ScenarioDocument scenario = CreateScenario();
        ScenarioDocument broken = scenario with
        {
            Nodes =
            [
                scenario.Nodes[0] with { EnterCondition = new ConsultedDocumentCondition("nope") },
                .. scenario.Nodes.Skip(1),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(broken);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "consulted_document_missing");
    }

    [Theory]
    [MemberData(nameof(MalformedDocuments))]
    public void AMalformedDocumentIsRejectedAtValidation(PresentedDocument document, string expectedCode)
    {
        ScenarioDocument scenario = CreateScenario(document);

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == expectedCode);
    }

    public static TheoryData<PresentedDocument, string> MalformedDocuments() => new()
    {
        {
            new PresentedDocument("", DocumentNature.Memo, [new DocumentParagraphBlock("Corps.")]),
            "document_title_invalid"
        },
        {
            new PresentedDocument("Titre", DocumentNature.Memo, []),
            "document_blocks_invalid"
        },
        {
            new PresentedDocument("Titre", DocumentNature.Memo, [new DocumentParagraphBlock("   ")]),
            "document_paragraph_invalid"
        },
        {
            new PresentedDocument("Titre", DocumentNature.Log, [new DocumentLinesBlock([])]),
            "document_lines_invalid"
        },
        {
            new PresentedDocument(
                "Titre",
                DocumentNature.Table,
                [new DocumentTableBlock(["A", "B"], [new DocumentRow(["seule"])])]),
            "document_row_arity_mismatch"
        },
        {
            // An excerpt claiming to show more than exists is the exact interface
            // lie the mechanic is meant to prevent.
            new PresentedDocument("Titre", DocumentNature.Table, [new DocumentParagraphBlock("Corps.")])
            {
                Excerpt = new DocumentExcerpt(500, 412, DocumentUnit.Rows),
            },
            "document_excerpt_invalid"
        },
    };

    private static ScenarioDocument CreateScenario(PresentedDocument? document = null) => new(
        NarrativeVersions.LatestSchema,
        "Le Diapason — note de service",
        "desk",
        [
            new NarrativeNode(
                "desk",
                "Le parapheur attend une signature.",
                null,
                [],
                [],
                false)
            {
                Interactions =
                [
                    new DocumentInteraction(
                        "the-memo",
                        "Consulter la note de service",
                        document ?? CreateMemo(),
                        [
                            new DiscoverEvidenceEffect("read-the-memo"),
                            new IncrementEffect("memo-reads", 1),
                        ])
                    {
                        IsOptional = true,
                    },
                    new ChoiceSetInteraction(
                        "decide",
                        "Que faites-vous ?",
                        [
                            new NarrativeChoice("sign-anyway", "Signer", "closing", null, []),
                            new NarrativeChoice(
                                "refuse-with-reason",
                                "Refuser en citant la note",
                                "closing",
                                new ConsultedDocumentCondition("the-memo"),
                                []),
                        ]),
                ],
            },
            new NarrativeNode("closing", "Le parapheur se referme.", null, [], [], true),
        ]);

    private static PresentedDocument CreateMemo() => new(
        "Note de service — accordage du hall",
        DocumentNature.Memo,
        [
            new DocumentParagraphBlock("Le hall sera accordé lundi, sans interruption de service."),
            new DocumentParagraphBlock("Toute signature engage le service émetteur."),
        ])
    {
        Headers =
        [
            new DocumentHeader("Objet", "Accordage du hall"),
            new DocumentHeader("De", "Direction technique"),
        ],
        Excerpt = new DocumentExcerpt(2, 14, DocumentUnit.Lines),
    };
}