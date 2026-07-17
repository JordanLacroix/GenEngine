using System.Text.Json;

namespace GenEngine.Narrative;

public static class GameSaveVersions
{
    public const int Current = 1;
}

public sealed record GameSave(
    int FormatVersion,
    int ScenarioSchemaVersion,
    ulong Seed,
    DateTimeOffset SavedAt,
    GameState State);

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
        if (document.RootElement.TryGetProperty("formatVersion", out _))
        {
            GameSave save = NarrativeJson.Deserialize<GameSave>(json);
            EnsureSupported(save);
            return save;
        }

        GameState legacyState = NarrativeJson.Deserialize<GameState>(json);
        return Create(NarrativeVersions.Schema, legacySeed, legacySavedAt, legacyState);
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
}