namespace GenEngine.Narrative.Tests;

/// <summary>
/// The engine side of a player statistic. Everything asserted here is about the
/// <em>boundary</em>: the engine records an intent and stays out of the value.
/// </summary>
public sealed class NarrativePlayerStatTests
{
    private static ScenarioDocument Scenario(params LocalGameEffect[] effects) => new(
        NarrativeVersions.PlayerStatSchema,
        "Player stats",
        "start",
        [
            new NarrativeNode(
                "start",
                "Vous tenez le fait qui manque.",
                null,
                effects,
                [new NarrativeChoice("speak", "Le dire", "ending", null, [])]),
            new NarrativeNode("ending", "Quelqu'un d'autre pourra le relire.", null, [], [], true),
        ]);

    [Fact]
    public void GrantingAStatRecordsAnExternalEventAndNothingElse()
    {
        GameState state = NarrativeRuntime.Start(Scenario(new GrantPlayerStatEffect("lucidite", 5)));

        ExternalEffectEvent recorded = Assert.Single(state.World.ExternalEvents);
        Assert.Equal(GrantPlayerStatEffect.PlayerStatEventName, recorded.EventName);
        Assert.Equal("lucidite", recorded.Attributes[GrantPlayerStatEffect.StatAttribute]);
        Assert.Equal("5", recorded.Attributes[GrantPlayerStatEffect.AmountAttribute]);

        // A player stat is not session state: none of the world's own stores moved.
        Assert.Empty(state.World.Variables);
        Assert.Empty(state.World.Characteristics);
        Assert.Empty(state.World.Inventory);
        Assert.Empty(state.World.Rewards);
    }

    /// <summary>
    /// Determinism invariant: identical scenario, state and commands must produce an
    /// identical state sequence. The grant must therefore be reproducible byte for byte.
    /// </summary>
    [Fact]
    public void GrantingAStatIsDeterministic()
    {
        ScenarioDocument scenario = Scenario(new GrantPlayerStatEffect("courage", 3));

        GameState first = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "speak");
        GameState second = NarrativeRuntime.SubmitChoice(scenario, NarrativeRuntime.Start(scenario), "speak");

        Assert.Equal(NarrativeJson.Serialize(first), NarrativeJson.Serialize(second));
    }

    [Fact]
    public void AStatGrantSurvivesSaveAndReload()
    {
        ScenarioDocument scenario = Scenario(new GrantPlayerStatEffect("transmission", 2));
        GameState state = NarrativeRuntime.Start(scenario);

        GameSave save = GameSaveSerializer.Create(scenario.SchemaVersion, 42UL, DateTimeOffset.UnixEpoch, state);
        GameSave reloaded = GameSaveSerializer.Deserialize(GameSaveSerializer.Serialize(save), 42UL, DateTimeOffset.UnixEpoch);

        Assert.Equal(NarrativeJson.Serialize(state), NarrativeJson.Serialize(reloaded.State));
    }

    [Theory]
    [InlineData("Lucidite", "player_stat_key_invalid")]
    [InlineData("lucidité", "player_stat_key_invalid")]
    [InlineData("player stat", "player_stat_key_invalid")]
    [InlineData("", "player_stat_key_invalid")]
    public void AnInvalidStatKeyIsRefused(string key, string expectedCode)
    {
        ValidationReport report = ScenarioValidator.Validate(Scenario(new GrantPlayerStatEffect(key, 1)));

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_001)]
    public void AnInvalidAmountIsRefused(int amount)
    {
        ValidationReport report = ScenarioValidator.Validate(Scenario(new GrantPlayerStatEffect("arbitrage", amount)));

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "player_stat_amount_invalid");
    }

    [Fact]
    public void AValidGrantPasses()
    {
        Assert.True(ScenarioValidator.Validate(Scenario(new GrantPlayerStatEffect("autonomie", 10))).IsValid);
    }

    /// <summary>
    /// An unknown schema version fails explicitly rather than being coerced. The
    /// invariant predates this change; the assertion pins it against the new latest.
    /// </summary>
    [Fact]
    public void AnUnknownSchemaVersionStillFailsExplicitly()
    {
        string json = NarrativeJson.Serialize(Scenario() with { SchemaVersion = NarrativeVersions.LatestSchema + 1 });

        NarrativeException exception = Assert.Throws<NarrativeException>(() => ScenarioMigrationPipeline.MigrateToLatest(json));

        Assert.Equal("scenario_version_not_supported", exception.Code);
    }
}