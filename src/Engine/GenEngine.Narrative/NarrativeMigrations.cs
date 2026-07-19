using System.Text.Json;

namespace GenEngine.Narrative;

public sealed record ScenarioMigrationResult(
    int OriginalSchemaVersion,
    ScenarioDocument Document,
    IReadOnlyList<string> AppliedMigrations);

public static class ScenarioMigrationPipeline
{
    public static ScenarioMigrationResult MigrateToLatest(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json);
        if (!payload.RootElement.TryGetProperty("schemaVersion", out JsonElement versionElement))
        {
            throw new NarrativeException("scenario_version_missing", "The scenario schema version is required.");
        }

        int originalVersion = versionElement.GetInt32();
        if (originalVersion < NarrativeVersions.Schema || originalVersion > NarrativeVersions.LatestSchema)
        {
            throw new NarrativeException(
                "scenario_version_not_supported",
                $"Scenario schema version {originalVersion} is not supported.");
        }

        ScenarioDocument document = NarrativeJson.Deserialize<ScenarioDocument>(json);
        List<string> migrations = [];
        while (document.SchemaVersion < NarrativeVersions.LatestSchema)
        {
            document = document.SchemaVersion switch
            {
                1 => document with { SchemaVersion = 2 },

                // v3 only adds optional media references. Nothing is rewritten:
                // a v2 document migrates by raising its version, and a snapshot
                // already published keeps its own version and canonical hash.
                2 => document with { SchemaVersion = 3 },

                // v4 only adds an optional "isOptional" flag on interactions. An
                // unset flag stays absent, so a migrated document keeps the exact
                // sequencing it had: every interaction remains mandatory.
                3 => document with { SchemaVersion = 4 },
                _ => throw new NarrativeException(
                    "scenario_migration_missing",
                    $"No migration is registered from scenario schema {document.SchemaVersion}."),
            };
            migrations.Add($"scenario-v{document.SchemaVersion - 1}-to-v{document.SchemaVersion}");
        }

        return new ScenarioMigrationResult(originalVersion, document, migrations);
    }
}