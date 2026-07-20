using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;
using GenEngine.Narrative;
using GenEngine.Play.Application;
using GenEngine.Play.Domain;
using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

/// <summary>
/// Player statistics, end to end: the configuration block that declares them, the
/// narrative effect that grants them, the boundary they cross to leave the session,
/// and the per-player value that starts at zero and saturates at its ceiling.
/// </summary>
public sealed class PlayerStatTests
{
    private static readonly Guid StatId = Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a101");
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");

    // ---------------------------------------------------------------- configuration

    [Fact]
    public async Task TheShippedConfigurationPublishesItsStatisticCatalogue()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView view = await service.UpsertAsync(
            "default",
            null,
            ConfigurationService.CreateDefault("default"),
            CancellationToken.None);

        PlayerStatsDefinition stats = Assert.IsType<PlayerStatsDefinition>(view.Document.PlayerStats);
        Assert.True(stats.Enabled);
        Assert.Equal(6, stats.Stats.Count);
        Assert.Contains(stats.Stats, stat => stat.Key == "lucidite" && stat.Maximum == 100);
        Assert.All(stats.Stats, stat =>
        {
            Assert.False(string.IsNullOrWhiteSpace(stat.Label));
            Assert.False(string.IsNullOrWhiteSpace(stat.Description));
        });
    }

    /// <summary>
    /// A document written before the block existed stays publishable and normalizes to
    /// the materialised, empty catalogue — never to invented statistics.
    /// </summary>
    [Fact]
    public async Task ADocumentWithoutTheBlockNormalizesToAnEmptyCatalogue()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView view = await service.UpsertAsync(
            "default",
            null,
            ConfigurationService.CreateDefault("default") with { PlayerStats = null },
            CancellationToken.None);

        PlayerStatsDefinition stats = Assert.IsType<PlayerStatsDefinition>(view.Document.PlayerStats);
        Assert.True(stats.Enabled);
        Assert.Empty(stats.Stats);
    }

    public static TheoryData<PlayerStatDefinition> RefusedStats() =>
    [
        // Key outside the slug charset, in three ways an operator plausibly types it.
        new(StatId, "Lucidite", "Lucidité", "Description.", 100),
        new(StatId, "lucidité", "Lucidité", "Description.", 100),
        new(StatId, "player stat", "Lucidité", "Description.", 100),
        new(StatId, "", "Lucidité", "Description.", 100),
        new(StatId, new string('a', 41), "Lucidité", "Description.", 100),

        // A blank label or description leaves the profile with an unreadable bar.
        new(StatId, "lucidite", "   ", "Description.", 100),
        new(StatId, "lucidite", "Lucidité", "   ", 100),
        new(StatId, "lucidite", new string('a', 81), "Description.", 100),
        new(StatId, "lucidite", "Lucidité", new string('a', 501), 100),

        // A zero ceiling would make every grant saturate immediately.
        new(StatId, "lucidite", "Lucidité", "Description.", 0),
        new(StatId, "lucidite", "Lucidité", "Description.", -1),
        new(StatId, "lucidite", "Lucidité", "Description.", 1_000_001),
    ];

    [Theory]
    [MemberData(nameof(RefusedStats))]
    public async Task AnInvalidStatisticIsRefused(PlayerStatDefinition stat)
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                ConfigurationService.CreateDefault("default") with { PlayerStats = new PlayerStatsDefinition(true, [stat]) },
                CancellationToken.None));

        Assert.Equal("invalid_player_stat", exception.Code);
    }

    [Fact]
    public async Task AValidStatisticIsAccepted()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView view = await service.UpsertAsync(
            "default",
            null,
            ConfigurationService.CreateDefault("default") with
            {
                PlayerStats = new PlayerStatsDefinition(true, [new PlayerStatDefinition(StatId, "esprit-critique-2", "Esprit critique", "Ce que vous avez su remettre en cause.", 42)]),
            },
            CancellationToken.None);

        PlayerStatDefinition stat = Assert.Single(Assert.IsType<PlayerStatsDefinition>(view.Document.PlayerStats).Stats);
        Assert.Equal("esprit-critique-2", stat.Key);
        Assert.Equal(42, stat.Maximum);
    }

    [Theory]
    [InlineData("lucidite", "lucidite")]
    [InlineData("lucidite", "LUCIDITE")]
    public async Task DuplicateStatisticKeysAreRefused(string first, string second)
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                ConfigurationService.CreateDefault("default") with
                {
                    PlayerStats = new PlayerStatsDefinition(true,
                    [
                        new PlayerStatDefinition(Guid.NewGuid(), first, "A", "Description.", 100),
                        new PlayerStatDefinition(Guid.NewGuid(), second, "B", "Description.", 100),
                    ]),
                },
                CancellationToken.None));

        Assert.Equal("invalid_player_stat", exception.Code);
    }

    [Fact]
    public async Task TooManyStatisticsAreRefused()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        PlayerStatDefinition[] stats = Enumerable.Range(0, PlayerStatCatalog.MaximumStats + 1)
            .Select(index => new PlayerStatDefinition(Guid.NewGuid(), $"stat-{index}", "Stat", "Description.", 100))
            .ToArray();

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                ConfigurationService.CreateDefault("default") with { PlayerStats = new PlayerStatsDefinition(true, stats) },
                CancellationToken.None));

        Assert.Equal("invalid_player_stat", exception.Code);
    }

    // ------------------------------------------------------------------ per player

    [Fact]
    public async Task EveryStatisticStartsAtZeroForAFreshProfile()
    {
        PlayerExperienceService service = CreateExperience(out _);

        PlayerExperienceView view = await service.GetAsync("player-1", "default", CancellationToken.None);

        PlayerStatView stat = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PlayerStatView>>(view.Stats));
        Assert.Equal("lucidite", stat.Key);
        Assert.Equal(0, stat.Value);

        // Everything needed to draw the bar travels with the value: a client never has
        // to join this contract with the configuration contract.
        Assert.Equal("Lucidité", stat.Label);
        Assert.Equal("Ce que vous avez su voir avant d'interpréter.", stat.Description);
        Assert.Equal(10, stat.Maximum);
    }

    [Fact]
    public async Task AGrantAccumulatesAndSaturatesAtTheConfiguredCeiling()
    {
        PlayerExperienceService service = CreateExperience(out _);

        await service.ApplyPlayerStatAsync(new PlayerStatCommand("default", "player-1", "lucidite", 4, "grant-1"), CancellationToken.None);
        PlayerExperienceView afterFirst = await service.GetAsync("player-1", "default", CancellationToken.None);

        // 4 + 9 overshoots the ceiling of 10: it saturates rather than failing, and
        // rather than landing on 13.
        PlayerExperienceView afterSecond = await service.ApplyPlayerStatAsync(
            new PlayerStatCommand("default", "player-1", "lucidite", 9, "grant-2"),
            CancellationToken.None);

        Assert.Equal(4, Single(afterFirst).Value);
        Assert.Equal(10, Single(afterSecond).Value);
    }

    [Fact]
    public async Task AReplayedGrantIsAppliedOnlyOnce()
    {
        PlayerExperienceService service = CreateExperience(out _);

        await service.ApplyPlayerStatAsync(new PlayerStatCommand("default", "player-1", "lucidite", 3, "grant-1"), CancellationToken.None);
        PlayerExperienceView replayed = await service.ApplyPlayerStatAsync(
            new PlayerStatCommand("default", "player-1", "lucidite", 3, "grant-1"),
            CancellationToken.None);

        Assert.Equal(3, Single(replayed).Value);
    }

    /// <summary>
    /// A scenario is authored independently of the instance running it, so a key this
    /// front does not publish must cost the player nothing — not their turn, and not an
    /// error. Same when the operator has switched the block off.
    /// </summary>
    [Theory]
    [InlineData(true, "inconnue")]
    [InlineData(false, "lucidite")]
    public async Task AGrantWithNoMatchingPublishedStatisticIsIgnored(bool enabled, string key)
    {
        PlayerExperienceService service = CreateExperience(out _, enabled: enabled);

        PlayerExperienceView view = await service.ApplyPlayerStatAsync(
            new PlayerStatCommand("default", "player-1", key, 5, "grant-1"),
            CancellationToken.None);

        Assert.All(view.Stats ?? [], stat => Assert.Equal(0, stat.Value));
    }

    /// <summary>
    /// Lowering a ceiling below what players already reached must not send a client a
    /// value above the maximum it is told to draw — and must not destroy the value
    /// either, so raising the ceiling again restores it.
    /// </summary>
    [Fact]
    public async Task AValueAboveALoweredCeilingIsClampedOnReadWithoutBeingLost()
    {
        PlayerExperienceService service = CreateExperience(out CatalogStub catalog);
        await service.ApplyPlayerStatAsync(new PlayerStatCommand("default", "player-1", "lucidite", 10, "grant-1"), CancellationToken.None);

        catalog.Maximum = 3;
        PlayerExperienceView lowered = await service.GetAsync("player-1", "default", CancellationToken.None);
        catalog.Maximum = 10;
        PlayerExperienceView restored = await service.GetAsync("player-1", "default", CancellationToken.None);

        Assert.Equal(3, Single(lowered).Value);
        Assert.Equal(10, Single(restored).Value);
    }

    /// <summary>
    /// Saturation asserted on the <em>stored</em> value, not through the experience
    /// view. The view clamps on read as well — for a ceiling an operator lowered after
    /// the fact — so reading it back there would pass whether or not the profile
    /// saturates, and would silently accept a stored value drifting above its ceiling.
    /// </summary>
    [Fact]
    public void TheStoredValueSaturatesAtTheCeiling()
    {
        PlayerProfile profile = PlayerProfile.Create("player-1", "default", 0, DateTimeOffset.UnixEpoch);

        Assert.True(profile.GrantStat("lucidite", 4, 10, "grant-1", DateTimeOffset.UnixEpoch));
        Assert.Equal(4, Stored(profile));

        Assert.True(profile.GrantStat("lucidite", 9, 10, "grant-2", DateTimeOffset.UnixEpoch));
        Assert.Equal(10, Stored(profile));

        // Already at the ceiling: a further grant is applied and changes nothing.
        Assert.True(profile.GrantStat("lucidite", 50, 10, "grant-3", DateTimeOffset.UnixEpoch));
        Assert.Equal(10, Stored(profile));

        static int Stored(PlayerProfile profile) => Assert.Single(profile.StatValues).Value;
    }

    /// <summary>A statistic the player was never granted has no row at all: absence is zero.</summary>
    [Fact]
    public void AProfileStartsWithNoStoredStatisticAtAll()
    {
        Assert.Empty(PlayerProfile.Create("player-1", "default", 0, DateTimeOffset.UnixEpoch).StatValues);
    }

    [Fact]
    public void AGrantOfZeroOrLessIsRefusedByTheProfile()
    {
        PlayerProfile profile = PlayerProfile.Create("player-1", "default", 0, DateTimeOffset.UnixEpoch);

        PlayerExperienceDomainException exception = Assert.Throws<PlayerExperienceDomainException>(() =>
            profile.GrantStat("lucidite", 0, 10, "grant-1", DateTimeOffset.UnixEpoch));

        Assert.Equal("invalid_amount", exception.Code);
    }

    // ------------------------------------------------------------- the whole path

    /// <summary>
    /// The journey of one point, from the narrative effect to the player's value:
    /// the engine records the crossing as an external event, Play turns it into an
    /// idempotent dispatch, and PlayerExperience applies and saturates it.
    /// </summary>
    [Fact]
    public async Task ANarrativeGrantTravelsFromTheEffectToThePlayerValue()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.PlayerStatSchema,
            "Player stat story",
            "opening",
            [
                new NarrativeNode("opening", "Vous détenez le fait qui manque.", null, [],
                [
                    new NarrativeChoice("speak", "Le dire", "ending", null, [new GrantPlayerStatEffect("lucidite", 4)]),
                ]),
                new NarrativeNode("ending", "Quelqu'un pourra le relire.", null, [], [], true),
            ]);
        Guid versionId = Guid.NewGuid();
        var play = new PlayService(
            new PlayRepositoryStub(),
            new SnapshotClientStub(new PublishedSnapshotContract(
                versionId,
                Guid.NewGuid(),
                "default",
                null,
                1,
                NarrativeJson.Serialize(scenario),
                CanonicalSnapshot.ComputeHash(scenario))),
            new AccessClientStub(),
            TimeProvider.System);
        PlayerExperienceService experience = CreateExperience(out _);

        SessionView session = await play.StartAsync("player-1", versionId, 42, true, CancellationToken.None);
        Guid commandId = Guid.NewGuid();
        InputResult result = await play.SubmitChoiceAsync(session.Id, "player-1", commandId, session.Revision, "speak", CancellationToken.None);
        InputResult replay = await play.SubmitChoiceAsync(session.Id, "player-1", commandId, session.Revision, "speak", CancellationToken.None);

        PlayerStatDispatch dispatch = Assert.Single(result.PlayerStats);
        Assert.Equal("lucidite", dispatch.Stat);
        Assert.Equal(4, dispatch.Amount);
        Assert.StartsWith($"session:{session.Id}:external:", dispatch.IdempotencyKey);

        // A retried command replays the very same dispatch, which the receiving side
        // then recognises: the point is granted once, not twice.
        Assert.True(replay.Replayed);
        Assert.Equal(result.PlayerStats, replay.PlayerStats);

        foreach (PlayerStatDispatch relayed in result.PlayerStats.Concat(replay.PlayerStats))
        {
            await experience.ApplyPlayerStatAsync(
                new PlayerStatCommand("default", "player-1", relayed.Stat, relayed.Amount, relayed.IdempotencyKey),
                CancellationToken.None);
        }

        PlayerExperienceView view = await experience.GetAsync("player-1", "default", CancellationToken.None);
        Assert.Equal(4, Single(view).Value);
    }

    // ------------------------------------------------------------------- fixtures

    private static PlayerStatView Single(PlayerExperienceView view) =>
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PlayerStatView>>(view.Stats));

    private static PlayerExperienceService CreateExperience(out CatalogStub catalog, bool enabled = true)
    {
        catalog = new CatalogStub { Enabled = enabled };
        return new PlayerExperienceService(new ExperienceRepositoryStub(), catalog, TimeProvider.System, new InertHelp(), new InertAi());
    }

    private sealed class CatalogStub : IPlayerExperienceCatalogProvider
    {
        public bool Enabled { get; set; } = true;
        public int Maximum { get; set; } = 10;

        public Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken) =>
            Task.FromResult(new PlayerExperienceCatalog(
                "ACCORD", "Accords", "♪", 0,
                [new FamiliarOption(FamiliarId, "Tierce", "Une voix.", "spark", "Warm", "Socratic", "amber", 2, [], ["spark"], ["Warm"], null, null, null, null, null)],
                [],
                [],
                new OnboardingTutorial(Guid.Parse("9cccf7f7-fba6-45ff-a3be-42d8993bb8cc"), 1, true, true, false, []),
                new AssistantPolicy(true, false, true, true, 2, []),
                [],
                [],
                null,
                new PlayerStatPlan(Enabled, [new PlayerStatPlanEntry(StatId, "lucidite", "Lucidité", "Ce que vous avez su voir avant d'interpréter.", Maximum)])));
    }

    private sealed class ExperienceRepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile?.Id);
        public Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new JournalPage([], 0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }

    private sealed class InertHelp : IScenarioHelpProvider
    {
        public Task<ScenarioHelpSnapshot?> GetAsync(Guid scenarioVersionId, string? nodeId, string? choiceId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioHelpSnapshot?>(null);
    }

    private sealed class InertAi : IAssistantAiClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SnapshotClientStub(PublishedSnapshotContract snapshot) : IAuthoringSnapshotClient
    {
        public Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class AccessClientStub : IContentAccessClient
    {
        public Task EnsureCanStartAsync(Guid userId, string frontId, Guid scenarioId, Guid? categoryId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class PlayRepositoryStub : IPlayRepository
    {
        private GameSession? session;
        private readonly Dictionary<Guid, ProcessedCommand> commands = [];

        public Task AddAsync(GameSession value, CancellationToken cancellationToken) { session = value; return Task.CompletedTask; }
        public Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == id && session.OwnerId == ownerId ? session : null);
        public Task<ProcessedCommand?> GetProcessedCommandAsync(Guid sessionId, Guid commandId, CancellationToken cancellationToken) =>
            Task.FromResult(commands.GetValueOrDefault(commandId));
        public Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken) { commands[command.CommandId] = command; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}