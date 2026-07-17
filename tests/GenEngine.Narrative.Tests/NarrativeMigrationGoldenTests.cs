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
        Assert.Equal(["scenario-v1-to-v2"], scenarioMigration.AppliedMigrations);
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

    private static string ReadGolden(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name));
}