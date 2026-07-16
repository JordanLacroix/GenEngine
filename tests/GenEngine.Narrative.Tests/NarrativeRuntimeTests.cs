using System.Text.Json;

namespace GenEngine.Narrative.Tests;

public sealed class NarrativeRuntimeTests
{
    [Fact]
    public void SameScenarioAndCommandsProduceSameStateAndHash()
    {
        ScenarioDocument scenario = CreateScenario();
        GameState first = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "enter");
        GameState second = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "enter");

        Assert.Equal(NarrativeJson.Serialize(first), NarrativeJson.Serialize(second));
        Assert.Equal(CanonicalSnapshot.ComputeHash(scenario), CanonicalSnapshot.ComputeHash(scenario));
        Assert.Equal(2, first.World.Variables["courage"]);
        Assert.Equal(SessionStatus.Completed, first.Status);
    }

    [Fact]
    public void JsonRoundTripPreservesPolymorphicTypes()
    {
        ScenarioDocument scenario = CreateScenario();

        string json = NarrativeJson.Serialize(scenario);
        ScenarioDocument restored = NarrativeJson.Deserialize<ScenarioDocument>(json);

        NarrativeChoice choice = Assert.Single(restored.Nodes[0].Choices);
        Assert.IsType<VariableAtLeastCondition>(choice.Condition);
        Assert.Contains(choice.Effects, static effect => effect is ScheduleEffect);
    }

    [Fact]
    public void UnknownPolymorphicTypeIsRejected()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "title": "Invalid",
              "initialNodeId": "start",
              "nodes": [{
                "id": "start",
                "text": "Start",
                "onEnterEffects": [],
                "choices": [{
                  "id": "x",
                  "text": "X",
                  "targetNodeId": "end",
                  "condition": { "$type": "script", "code": "danger" },
                  "effects": []
                }],
                "isEnding": false
              }, {
                "id": "end",
                "text": "End",
                "onEnterEffects": [],
                "choices": [],
                "isEnding": true
              }]
            }
            """;

        Assert.Throws<JsonException>(() => NarrativeJson.Deserialize<ScenarioDocument>(json));
    }

    [Fact]
    public void PolymorphicMetadataCanFollowDataPropertiesAfterJsonbNormalization()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "title": "JSONB order",
              "initialNodeId": "start",
              "nodes": [{
                "id": "start",
                "text": "Start",
                "onEnterEffects": [],
                "choices": [{
                  "id": "go",
                  "text": "Go",
                  "targetNodeId": "end",
                  "effects": [{ "item": "key", "$type": "collect" }]
                }]
              }, {
                "id": "end",
                "text": "End",
                "onEnterEffects": [],
                "choices": [],
                "isEnding": true
              }]
            }
            """;

        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(json);

        Assert.IsType<CollectEffect>(Assert.Single(scenario.Nodes[0].Choices[0].Effects));
    }

    [Fact]
    public void SplitMix64MatchesPublishedVectorForSeedZero()
    {
        SplitMix64 generator = new(0);

        ulong[] actual = [generator.NextUInt64(), generator.NextUInt64(), generator.NextUInt64()];

        Assert.Equal([0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL], actual);
    }

    [Fact]
    public void ValidatorReportsBrokenTargets()
    {
        ScenarioDocument invalid = CreateScenario() with
        {
            Nodes =
            [
                new NarrativeNode(
                    "start",
                    "Broken",
                    null,
                    [],
                    [new NarrativeChoice("go", "Go", "missing", null, [])]),
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(invalid);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "target_missing");
    }

    private static ScenarioDocument CreateScenario() => new(
        NarrativeVersions.Schema,
        "The gate",
        "start",
        [
            new NarrativeNode(
                "start",
                "A gate blocks the path.",
                null,
                [new AssignEffect("courage", 1)],
                [
                    new NarrativeChoice(
                        "enter",
                        "Enter",
                        "end",
                        new VariableAtLeastCondition("courage", 1),
                        [new IncrementEffect("courage", 1), new ScheduleEffect(1, new CollectEffect("badge"))]),
                ]),
            new NarrativeNode("end", "The end.", null, [], [], true),
        ]);
}