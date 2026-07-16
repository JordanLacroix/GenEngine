using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

public static class NarrativeJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException("The JSON payload was empty.");

    private static JsonSerializerOptions CreateOptions() => new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };
}

public static class CanonicalSnapshot
{
    public static byte[] GetCanonicalBytes(ScenarioDocument scenario)
    {
        JsonNode node = JsonSerializer.SerializeToNode(scenario, NarrativeJson.Options)
            ?? throw new JsonException("The scenario could not be serialized.");
        JsonNode canonical = Sort(node);
        return Encoding.UTF8.GetBytes(canonical.ToJsonString(NarrativeJson.Options));
    }

    public static string ComputeHash(ScenarioDocument scenario) =>
        Convert.ToHexStringLower(SHA256.HashData(GetCanonicalBytes(scenario)));

    private static JsonNode Sort(JsonNode node)
    {
        return node switch
        {
            JsonObject source => SortObject(source),
            JsonArray source => new JsonArray(source.Select(item => item is null ? null : Sort(item)).ToArray()),
            _ => node.DeepClone(),
        };
    }

    private static JsonObject SortObject(JsonObject source)
    {
        JsonObject result = [];
        foreach ((string name, JsonNode? value) in source.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            result.Add(name, value is null ? null : Sort(value));
        }

        return result;
    }
}

public struct SplitMix64(ulong seed)
{
    private ulong state = seed;

    public ulong NextUInt64()
    {
        ulong value = state += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

public static class ScenarioSimulator
{
    public static GameState RunFirstAvailableChoice(ScenarioDocument scenario, int maximumTurns = 100)
    {
        GameState state = NarrativeRuntime.Start(scenario);

        while (state.Status is SessionStatus.AwaitingInput && state.Turn < maximumTurns)
        {
            CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, state);
            if (step.Choices.Count == 0)
            {
                throw new NarrativeException("no_visible_choice", "The simulator found no visible choice.");
            }

            state = NarrativeRuntime.SubmitChoice(scenario, state, step.Choices[0].Id);
        }

        if (state.Status is SessionStatus.AwaitingInput)
        {
            throw new NarrativeException("simulation_budget_exceeded", "The simulator exceeded its turn budget.");
        }

        return state;
    }
}