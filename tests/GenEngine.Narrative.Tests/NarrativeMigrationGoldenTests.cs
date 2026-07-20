namespace GenEngine.Narrative.Tests;

public sealed class NarrativeMigrationGoldenTests
{
    [Fact]
    public void ScenarioAndSaveMigrationsPreserveGoldenReplay()
    {
        ScenarioMigrationResult scenarioMigration = ScenarioMigrationPipeline.MigrateToLatest(
            ReadGolden("scenario-v1.json"));
        GameSave save = GameSaveSerializer.Deserialize(
            ReadGolden("save-v1.json"),
            0,
            DateTimeOffset.MinValue);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenarioMigration.Document,
            save.State,
            "brave");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("replay-final-state-v2.json"));

        Assert.Equal(1, scenarioMigration.OriginalSchemaVersion);
        Assert.Equal(NarrativeVersions.LatestSchema, scenarioMigration.Document.SchemaVersion);
        Assert.Equal(
            [
                "scenario-v1-to-v2",
                "scenario-v2-to-v3",
                "scenario-v3-to-v4",
                "scenario-v4-to-v5",
                "scenario-v5-to-v6",
                "scenario-v6-to-v7",
            ],
            scenarioMigration.AppliedMigrations);
        Assert.Equal(GameSaveVersions.Current, save.FormatVersion);
        Assert.Equal(NarrativeVersions.Runtime, save.RuntimeVersion);
        Assert.Equal(["save-v1-to-v2"], save.AppliedMigrations);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    [Fact]
    public void AlreadyCurrentDocumentsRemainUnchangedByMigrationPipeline()
    {
        ScenarioMigrationResult first = ScenarioMigrationPipeline.MigrateToLatest(ReadGolden("scenario-v1.json"));
        string currentJson = NarrativeJson.Serialize(first.Document);

        ScenarioMigrationResult second = ScenarioMigrationPipeline.MigrateToLatest(currentJson);

        Assert.Equal(NarrativeVersions.LatestSchema, second.OriginalSchemaVersion);
        Assert.Empty(second.AppliedMigrations);
        Assert.Equal(currentJson, NarrativeJson.Serialize(second.Document));
    }

    [Fact]
    public void UnknownFutureScenarioVersionIsRejectedExplicitly()
    {
        string json = ReadGolden("scenario-v1.json").Replace(
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 99",
            StringComparison.Ordinal);

        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            ScenarioMigrationPipeline.MigrateToLatest(json));

        Assert.Equal("scenario_version_not_supported", exception.Code);
    }

    [Fact]
    public void MigrationLeavesBusinessValidationToAuthoringWorkflow()
    {
        string json = ReadGolden("scenario-v1.json").Replace(
            "\"initialNodeId\": \"start\"",
            "\"initialNodeId\": \"missing\"",
            StringComparison.Ordinal);

        ScenarioMigrationResult migration = ScenarioMigrationPipeline.MigrateToLatest(json);
        ValidationReport validation = ScenarioValidator.Validate(migration.Document);

        Assert.Equal(NarrativeVersions.LatestSchema, migration.Document.SchemaVersion);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, static issue => issue.Code == "initial_node_missing");
    }

    /// <summary>
    /// Canonical hash of <c>scenario-v2.json</c>, computed with the engine as it
    /// was before media were introduced. A published snapshot is replayed against
    /// its stored hash, so this literal must never change: it proves that the
    /// optional media fields stay out of the canonical bytes of a document that
    /// does not use them.
    /// </summary>
    private const string PublishedSchemaTwoHash = "b4ee3cd036ba7cd1c9c28fad7031b3b4ec4ca995e6216041544eff33c13c82b3";

    [Fact]
    public void PublishedSchemaTwoSnapshotKeepsItsCanonicalHashAfterTheMediaBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v2.json"));
        string canonicalJson = System.Text.Encoding.UTF8.GetString(CanonicalSnapshot.GetCanonicalBytes(scenario));

        Assert.Equal(PublishedSchemaTwoHash, CanonicalSnapshot.ComputeHash(scenario));
        Assert.DoesNotContain("media", canonicalJson, StringComparison.Ordinal);
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    [Fact]
    public void SchemaTwoSaveStillReplaysIdenticallyAfterTheMediaBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v2.json"));
        GameSave save = GameSaveSerializer.Deserialize(ReadGolden("save-v2.json"), 42UL, DateTimeOffset.UnixEpoch);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Continue(scenario, save.State),
            "listen");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("scenario-v2-replay-final-state.json"));

        Assert.Empty(save.AppliedMigrations);
        Assert.Equal(2, save.ScenarioSchemaVersion);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    /// <summary>
    /// Canonical hash of <c>scenario-v3.json</c>, computed with the engine as it
    /// was before optional interactions existed, by compiling the pre-change
    /// sources from git and running them against this very fixture. Freezing a
    /// value produced by the older binary is the only assertion that proves the
    /// new <c>isOptional</c> field stays out of the canonical bytes of a document
    /// that does not declare it — an expectation written against the new code
    /// would prove nothing.
    /// </summary>
    private const string PublishedSchemaThreeHash =
        "def2efc53e6b417fa6f42336c80fe81c99fc01989631c0746edf912c6214bfcf";

    [Fact]
    public void PublishedSchemaThreeSnapshotKeepsItsCanonicalHashAfterTheOptionalBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v3.json"));
        string canonicalJson = System.Text.Encoding.UTF8.GetString(CanonicalSnapshot.GetCanonicalBytes(scenario));

        Assert.Equal(PublishedSchemaThreeHash, CanonicalSnapshot.ComputeHash(scenario));
        Assert.DoesNotContain("isOptional", canonicalJson, StringComparison.Ordinal);
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    /// <summary>
    /// The final state is the one the pre-change engine produced from
    /// <c>save-v3.json</c>. A session published before this change must follow the
    /// exact same sequence: both interactions stay mandatory and the turn counter,
    /// interaction history and world are byte-identical.
    /// </summary>
    [Fact]
    public void SchemaThreeSaveStillReplaysIdenticallyAfterTheOptionalBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v3.json"));
        GameSave save = GameSaveSerializer.Deserialize(ReadGolden("save-v3.json"), 42UL, DateTimeOffset.UnixEpoch);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.SubmitAnswer(scenario, NarrativeRuntime.Continue(scenario, save.State), "fifth"),
            "listen");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("scenario-v3-replay-final-state.json"));

        Assert.Empty(save.AppliedMigrations);
        Assert.Equal(3, save.ScenarioSchemaVersion);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    /// <summary>
    /// The same published snapshot must also stay <em>mandatory</em>: without an
    /// explicit flag, no exit choice leaks next to the narration, and taking one
    /// early is refused exactly as before.
    /// </summary>
    [Fact]
    public void SchemaThreeSnapshotOffersNoSkipAndStillBlocksAnEarlyChoice()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v3.json"));
        GameState start = NarrativeRuntime.Start(scenario);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, start);
        NarrativeException exception = Assert.Throws<NarrativeException>(() =>
            NarrativeRuntime.SubmitChoice(scenario, start, "listen"));

        Assert.Equal(InteractionKind.Narration, step.Kind);
        Assert.False(step.IsOptional);
        Assert.Empty(step.ExitChoices);
        Assert.Empty(step.Choices);
        Assert.Equal("choice_not_available", exception.Code);
    }

    /// <summary>
    /// Canonical hash of <c>scenario-v4.json</c>, computed with the engine as it
    /// was before author help existed: the fixture was hashed by the pre-change
    /// binary on this branch, before <c>AuthorHelp</c>, <c>LatestSchema = 5</c> and
    /// the v4→v5 migration were written. Freezing a value produced by the older
    /// engine is the only assertion that proves the new <c>help</c> record stays
    /// out of the canonical bytes of a document that does not declare it — an
    /// expectation written against the new code would prove nothing.
    /// </summary>
    private const string PublishedSchemaFourHash =
        "dbd5bd1d4470287c68f1561aeee12b96eb283c3f6f162ebca6fac0251127c040";

    [Fact]
    public void PublishedSchemaFourSnapshotKeepsItsCanonicalHashAfterTheHelpBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v4.json"));
        string canonicalJson = System.Text.Encoding.UTF8.GetString(CanonicalSnapshot.GetCanonicalBytes(scenario));

        Assert.Equal(PublishedSchemaFourHash, CanonicalSnapshot.ComputeHash(scenario));
        Assert.DoesNotContain("help", canonicalJson, StringComparison.Ordinal);
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    /// <summary>
    /// The final state is the one the pre-change engine produced from
    /// <c>save-v4.json</c>. A session published before this change must follow the
    /// exact same sequence: same turn counter, same interaction history, same
    /// world, byte for byte.
    /// </summary>
    [Fact]
    public void SchemaFourSaveStillReplaysIdenticallyAfterTheHelpBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v4.json"));
        GameSave save = GameSaveSerializer.Deserialize(ReadGolden("save-v4.json"), 42UL, DateTimeOffset.UnixEpoch);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Continue(scenario, save.State),
            "answer-the-hall");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("scenario-v4-replay-final-state.json"));

        Assert.Empty(save.AppliedMigrations);
        Assert.Equal(4, save.ScenarioSchemaVersion);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    /// <summary>
    /// Help declared before schema 5 is refused, and the check is bound to its own
    /// capability constant: a v4 document that legitimately uses <c>isOptional</c>
    /// must not be invalidated by the bump.
    /// </summary>
    [Fact]
    public void AuthorHelpDeclaredBeforeSchemaFiveIsRejected()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v4.json"));
        ScenarioDocument withHelp = scenario with
        {
            Nodes = [scenario.Nodes[0] with { Help = new AuthorHelp { Hint = "Écoutez avant de répondre." } }, .. scenario.Nodes.Skip(1)],
        };

        ValidationReport report = ScenarioValidator.Validate(withHelp);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "help_requires_schema_5");
    }

    /// <summary>
    /// Canonical hash of <c>scenario-v5.json</c>, computed with the engine as it
    /// was before the document interaction existed: the fixture was written first
    /// and hashed by the pre-change binary at commit <c>43d2e11</c>, before
    /// <c>DocumentInteraction</c>, <c>LatestSchema = 6</c> and the v5→v6 migration
    /// were written. Freezing a value produced by the older engine is the only
    /// assertion that proves the new document types stay out of the canonical bytes
    /// of a document that does not declare them — an expectation regenerated after
    /// the change would prove nothing.
    /// </summary>
    private const string PublishedSchemaFiveHash =
        "028aff60843ebefd1d6a1f9701b76536646a119ddbaee2ab0f1287f1f952a591";

    [Fact]
    public void PublishedSchemaFiveSnapshotKeepsItsCanonicalHashAfterTheDocumentBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v5.json"));
        string canonicalJson = System.Text.Encoding.UTF8.GetString(CanonicalSnapshot.GetCanonicalBytes(scenario));

        Assert.Equal(PublishedSchemaFiveHash, CanonicalSnapshot.ComputeHash(scenario));
        Assert.DoesNotContain("document", canonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("consultedDocument", canonicalJson, StringComparison.Ordinal);
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    /// <summary>
    /// The final state is the one the pre-change engine produced from
    /// <c>save-v5.json</c>. A session published before this change must follow the
    /// exact same sequence: same turn counter, same interaction history, same
    /// world, byte for byte.
    /// </summary>
    [Fact]
    public void SchemaFiveSaveStillReplaysIdenticallyAfterTheDocumentBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v5.json"));
        GameSave save = GameSaveSerializer.Deserialize(ReadGolden("save-v5.json"), 42UL, DateTimeOffset.UnixEpoch);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Continue(scenario, save.State),
            "answer-the-hall");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("scenario-v5-replay-final-state.json"));

        Assert.Empty(save.AppliedMigrations);
        Assert.Equal(5, save.ScenarioSchemaVersion);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    /// <summary>
    /// A document declared before schema 6 is refused, and the check is bound to
    /// its own capability constant: a v5 document that legitimately uses
    /// <c>help</c> and <c>isOptional</c> must not be invalidated by the bump.
    /// </summary>
    [Fact]
    public void DocumentDeclaredBeforeSchemaSixIsRejected()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v5.json"));
        NarrativeNode start = scenario.Nodes[0];
        ScenarioDocument withDocument = scenario with
        {
            Nodes =
            [
                start with
                {
                    Interactions =
                    [
                        new DocumentInteraction(
                            "the-memo",
                            "Consulter la note de service",
                            new PresentedDocument(
                                "Note de service",
                                DocumentNature.Memo,
                                [new DocumentParagraphBlock("Le hall sera accordé lundi.")]),
                            []),
                        .. start.Interactions!,
                    ],
                },
                .. scenario.Nodes.Skip(1),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(withDocument);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "document_requires_schema_6");
    }

    /// <summary>
    /// The consulted-document condition is gated on the same capability constant,
    /// independently of the interaction, so neither half can slip into an older
    /// document unnoticed.
    /// </summary>
    [Fact]
    public void ConsultedDocumentConditionDeclaredBeforeSchemaSixIsRejected()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v5.json"));
        ScenarioDocument withCondition = scenario with
        {
            Nodes =
            [
                scenario.Nodes[0] with { EnterCondition = new ConsultedDocumentCondition("the-memo") },
                .. scenario.Nodes.Skip(1),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(withCondition);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "consulted_document_requires_schema_6");
        Assert.Contains(report.Issues, static issue => issue.Code == "consulted_document_missing");
    }

    /// <summary>
    /// Canonical hash of <c>scenario-v6.json</c>, computed with the engine as it
    /// was before player statistics existed: the fixture was written first and
    /// hashed by the pre-change binary at commit <c>b7bc549</c>, before
    /// <c>GrantPlayerStatEffect</c>, <c>LatestSchema = 7</c> and the v6→v7
    /// migration were written. Freezing a value produced by the older engine is the
    /// only assertion that proves the new effect stays out of the canonical bytes of
    /// a document that does not declare it — an expectation regenerated after the
    /// change would prove nothing.
    /// </summary>
    private const string PublishedSchemaSixHash =
        "46332fdfdf7af32222968efc44f78394bbdd562c8e029006600430d1b4b71a8d";

    [Fact]
    public void PublishedSchemaSixSnapshotKeepsItsCanonicalHashAfterThePlayerStatBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v6.json"));
        string canonicalJson = System.Text.Encoding.UTF8.GetString(CanonicalSnapshot.GetCanonicalBytes(scenario));

        Assert.Equal(PublishedSchemaSixHash, CanonicalSnapshot.ComputeHash(scenario));
        Assert.DoesNotContain("grantPlayerStat", canonicalJson, StringComparison.Ordinal);
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    /// <summary>
    /// The final state is the one the pre-change engine produced from
    /// <c>save-v6.json</c>. A session published before this change must follow the
    /// exact same sequence: same turn counter, same interaction history, same world,
    /// byte for byte — and, in particular, an empty <c>externalEvents</c> list, since
    /// the new effect is the first thing to write there outside an explicit emit.
    /// </summary>
    [Fact]
    public void SchemaSixSaveStillReplaysIdenticallyAfterThePlayerStatBump()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v6.json"));
        GameSave save = GameSaveSerializer.Deserialize(ReadGolden("save-v6.json"), 42UL, DateTimeOffset.UnixEpoch);

        GameState replayed = NarrativeRuntime.SubmitChoice(
            scenario,
            NarrativeRuntime.Continue(scenario, NarrativeRuntime.ConsultDocument(scenario, save.State)),
            "answer-the-hall");
        GameState expected = NarrativeJson.Deserialize<GameState>(ReadGolden("scenario-v6-replay-final-state.json"));

        Assert.Empty(save.AppliedMigrations);
        Assert.Equal(6, save.ScenarioSchemaVersion);
        Assert.Equal(NarrativeJson.Serialize(expected), NarrativeJson.Serialize(replayed));
    }

    /// <summary>
    /// A player stat granted before schema 7 is refused, and the check is bound to its
    /// own capability constant: the very same v6 document, which legitimately uses a
    /// document interaction, must stay valid without the grant.
    /// </summary>
    [Fact]
    public void PlayerStatGrantedBeforeSchemaSevenIsRejected()
    {
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(ReadGolden("scenario-v6.json"));
        ScenarioDocument withGrant = scenario with
        {
            Nodes =
            [
                scenario.Nodes[0] with { OnEnterEffects = [new GrantPlayerStatEffect("lucidite", 5)] },
                .. scenario.Nodes.Skip(1),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(withGrant);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "player_stat_requires_schema_7");
        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
    }

    private static string ReadGolden(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name));
}