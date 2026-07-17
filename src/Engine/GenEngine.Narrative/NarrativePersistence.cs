using System.Text.Json;

namespace GenEngine.Narrative;

public static class GameSaveVersions
{
    public const int Initial = 1;
    public const int Current = 2;
}

public sealed record GameSave(
    int FormatVersion,
    int ScenarioSchemaVersion,
    ulong Seed,
    DateTimeOffset SavedAt,
    GameState State)
{
    public string RuntimeVersion { get; init; } = NarrativeVersions.Runtime;

    public IReadOnlyList<string> AppliedMigrations { get; init; } = [];
}

public static class GameSaveSerializer
{
    public static GameSave Create(
        int scenarioSchemaVersion,
        ulong seed,
        DateTimeOffset savedAt,
        GameState state) =>
        new(GameSaveVersions.Current, scenarioSchemaVersion, seed, savedAt, state);

    public static string Serialize(GameSave save)
    {
        EnsureSupported(save);
        return NarrativeJson.Serialize(save);
    }

    public static GameSave Deserialize(
        string json,
        ulong legacySeed,
        DateTimeOffset legacySavedAt)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("formatVersion", out JsonElement versionElement))
        {
            int version = versionElement.GetInt32();
            EnsureMigratableVersion(version);
            GameSave save = NarrativeJson.Deserialize<GameSave>(json);
            return MigrateToCurrent(save);
        }

        GameState legacyState = NarrativeJson.Deserialize<GameState>(json);
        GameSave legacySave = new(
            GameSaveVersions.Initial,
            NarrativeVersions.Schema,
            legacySeed,
            legacySavedAt,
            legacyState)
        {
            AppliedMigrations = ["legacy-state-to-save-v1"],
        };
        return MigrateToCurrent(legacySave);
    }

    private static void EnsureSupported(GameSave save)
    {
        if (save.FormatVersion != GameSaveVersions.Current)
        {
            throw new NarrativeException(
                "save_version_not_supported",
                $"Game save version {save.FormatVersion} is not supported.");
        }

        if (save.ScenarioSchemaVersion < NarrativeVersions.Schema
            || save.ScenarioSchemaVersion > NarrativeVersions.LatestSchema)
        {
            throw new NarrativeException(
                "save_scenario_version_not_supported",
                $"Scenario schema version {save.ScenarioSchemaVersion} is not supported by this runtime.");
        }
    }

    private static void EnsureMigratableVersion(int version)
    {
        if (version < GameSaveVersions.Initial || version > GameSaveVersions.Current)
        {
            throw new NarrativeException(
                "save_version_not_supported",
                $"Game save version {version} is not supported.");
        }
    }

    private static GameSave MigrateToCurrent(GameSave save)
    {
        EnsureMigratableVersion(save.FormatVersion);
        while (save.FormatVersion < GameSaveVersions.Current)
        {
            save = save.FormatVersion switch
            {
                1 => save with
                {
                    FormatVersion = 2,
                    RuntimeVersion = NarrativeVersions.Runtime,
                    AppliedMigrations = [.. save.AppliedMigrations, "save-v1-to-v2"],
                },
                _ => throw new NarrativeException(
                    "save_migration_missing",
                    $"No migration is registered from game save version {save.FormatVersion}."),
            };
        }

        EnsureSupported(save);
        return save;
    }
}