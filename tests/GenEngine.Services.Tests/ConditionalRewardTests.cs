using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;
using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

/// <summary>
/// Conditional rewards are the finale's condition model applied to something that is not
/// an ending. These tests cover the block's validation, the one condition kind the finale
/// never had — a statistic threshold — and the property the player actually feels: a
/// reward is stamped once, never re-dated, and never paid twice.
/// </summary>
public sealed class ConditionalRewardTests
{
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");
    private static readonly Guid CategoryA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid JourneyId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ScenarioOne = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
    private static readonly Guid ScenarioTwo = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000002");
    private static readonly Guid RewardId = Guid.Parse("cccccccc-0001-4000-8000-000000000001");
    private static readonly Guid StatRewardId = Guid.Parse("cccccccc-0002-4000-8000-000000000002");
    private static readonly Guid StatId = Guid.Parse("dddddddd-0001-4000-8000-000000000001");

    private static readonly CategoryCatalogEntry[] Categories =
        [new(CategoryA, "Catégorie", "", "encre", 1, true, null, [ScenarioOne, ScenarioTwo])];

    private static readonly JourneyCatalogEntry[] Journeys =
        [new(JourneyId, "Parcours", "", "encre", null, 1, true, [CategoryA], [], [])];

    // ---------------------------------------------------------------------------------
    // The new condition kind, evaluated directly. Asserting on the evaluator rather than
    // through the experience view is deliberate: the view maps and clamps, and a test
    // reading a threshold verdict through it could pass whether the logic exists or not.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void APlayerStatConditionComparesTheAccumulatedValueToItsThreshold()
    {
        ProgressCondition condition = StatCondition("lucidite", 50);

        Assert.False(Assert.Single(Evaluate([condition], stats: new() { ["lucidite"] = 49 })).Satisfied);
        Assert.True(Assert.Single(Evaluate([condition], stats: new() { ["lucidite"] = 50 })).Satisfied);
    }

    [Fact]
    public void AStatisticThePlayerNeverEarnedCountsAsZeroRatherThanFailing()
    {
        ProgressConditionProgress progress = Assert.Single(Evaluate([StatCondition("lucidite", 10)], stats: []));

        Assert.Equal(0, progress.Current);
        Assert.Equal(10, progress.Target);
        Assert.False(progress.Satisfied);
    }

    [Fact]
    public void AnUnknownConditionKindIsNeverSatisfiedRatherThanIgnored()
    {
        // A document published by a newer engine must never make a reward easier to earn.
        ProgressCondition unknown = new(Guid.NewGuid(), ProgressConditionKind.Unknown, "Inconnue", null, null, null, [], []);

        Assert.False(Assert.Single(Evaluate([unknown], scenarios: [Done(ScenarioOne), Done(ScenarioTwo)])).Satisfied);
    }

    [Fact]
    public void RewardConditionsCombineWithAllAndAny()
    {
        ProgressCondition scenarios = Condition(ProgressConditionKind.ScenariosCompleted, threshold: 3);
        ProgressCondition category = Condition(ProgressConditionKind.CategoryCompleted, categoryId: CategoryA);
        ProgressSnapshot snapshot = Snapshot([Done(ScenarioOne), Done(ScenarioTwo)], []);
        IReadOnlyList<ProgressConditionProgress> evaluated = ProgressConditionEvaluator.Evaluate([scenarios, category], snapshot);

        // The category is complete, the scenario count is not.
        Assert.False(ProgressConditionEvaluator.IsSatisfied(ProgressMode.All, evaluated));
        Assert.True(ProgressConditionEvaluator.IsSatisfied(ProgressMode.Any, evaluated));
    }

    [Fact]
    public void ARewardWithNoConditionIsNeverSatisfied()
    {
        Assert.False(ProgressConditionEvaluator.IsSatisfied(ProgressMode.All, []));
        Assert.False(ProgressConditionEvaluator.IsSatisfied(ProgressMode.Any, []));
    }

    // ---------------------------------------------------------------------------------
    // Earning, stamping and paying.
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task ARewardIsStampedOncePaidOnceAndNeverReDated()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        PlayerExperienceView before = await Complete(service, ScenarioOne, "fin-silence");
        Assert.False(Reward(before, RewardId).Earned);
        Assert.Equal(0, before.Balance);

        PlayerExperienceView earned = await Complete(service, ScenarioTwo, "fin-alerte");
        ConditionalRewardView view = Reward(earned, RewardId);
        Assert.True(view.Earned);
        Assert.NotNull(view.EarnedAt);
        Assert.Contains(earned.RecentJournal, entry => entry.Type == "RewardEarned");

        // Asserted on the aggregate, not on the projection: the view derives Earned from
        // the stamp, so a duplicate stamp or a duplicate credit would be invisible there.
        PlayerProfile profile = repository.Profile!;
        EarnedReward stamp = Assert.Single(profile.EarnedRewards);
        DateTimeOffset earnedAt = stamp.EarnedAt;
        int balance = profile.Balance;
        Assert.Equal(60, balance);

        // Replaying progress must not re-stamp, re-date, re-journal or re-credit.
        _ = await Complete(service, ScenarioOne, "fin-silence");
        _ = await Complete(service, ScenarioTwo, "fin-alerte");
        PlayerExperienceView again = await service.GetAsync("player-1", "default", CancellationToken.None);

        Assert.Single(profile.EarnedRewards);
        Assert.Equal(earnedAt, Assert.Single(profile.EarnedRewards).EarnedAt);
        Assert.Equal(balance, profile.Balance);
        Assert.Single(profile.JournalEntries, entry => entry.Type == "RewardEarned");
        Assert.Equal(earnedAt, Reward(again, RewardId).EarnedAt);
    }

    /// <summary>
    /// The stamp is asserted on the aggregate itself, without the service in the way.
    /// Above, the service's own "already earned" short-circuit hides whether this guard
    /// exists at all; here nothing stands between the second call and the field.
    /// </summary>
    [Fact]
    public void TheDomainRefusesToReStampARewardOnItsOwn()
    {
        DateTimeOffset first = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        PlayerProfile profile = PlayerProfile.Create("player-1", "default", 0, first);

        Assert.True(profile.MarkRewardEarned(RewardId, first));
        Assert.False(profile.MarkRewardEarned(RewardId, first.AddDays(3)));

        EarnedReward stamp = Assert.Single(profile.EarnedRewards);
        Assert.Equal(first, stamp.EarnedAt);
    }

    /// <summary>
    /// Validation forbids an achievement grant from carrying an amount, but the catalogue
    /// reader is deliberately tolerant of a document published by another engine. A
    /// non-currency grant must therefore be inert at the wallet even when it does carry
    /// one — the nature decides, never the presence of a number.
    /// </summary>
    [Fact]
    public async Task ANonCurrencyGrantNeverCreditsTheWalletEvenWhenItCarriesAnAmount()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(
            repository, new CatalogStub(AchievementAmount: 500), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        _ = await Complete(service, ScenarioOne, "fin-silence");
        _ = await Complete(service, ScenarioTwo, "fin-alerte");

        Assert.Single(repository.Profile!.EarnedRewards, stamp => stamp.RewardId == RewardId);
        Assert.Equal(60, repository.Profile!.Balance);
    }

    [Fact]
    public async Task AStatisticThresholdIsCrossedOnTheGrantPathAndNotOnlyOnProgress()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        PlayerExperienceView low = await Grant(service, 40, "grant-1");
        Assert.False(Reward(low, StatRewardId).Earned);

        PlayerExperienceView high = await Grant(service, 10, "grant-2");
        Assert.True(Reward(high, StatRewardId).Earned);
        Assert.Single(repository.Profile!.EarnedRewards, stamp => stamp.RewardId == StatRewardId);
    }

    [Fact]
    public async Task ReadingTheProfileNeverEarnsAReward()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        // The conditions of the statistic reward are satisfied by a direct grant applied
        // without the service, so only a read can be the thing that stamps it.
        _ = await Complete(service, ScenarioOne, "fin-silence");
        PlayerProfile profile = repository.Profile!;
        _ = profile.GrantStat("lucidite", 50, 100, "out-of-band", DateTimeOffset.UtcNow);

        PlayerExperienceView view = await service.GetAsync("player-1", "default", CancellationToken.None);

        // The progress is shown honestly, and nothing was stamped by looking at it.
        Assert.True(Assert.Single(Reward(view, StatRewardId).Conditions).Satisfied);
        Assert.False(Reward(view, StatRewardId).Earned);
        Assert.Empty(profile.EarnedRewards);
    }

    [Fact]
    public async Task TheProgressOfAnUnearnedRewardIsReadable()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new CatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        PlayerExperienceView view = await Complete(service, ScenarioOne, "fin-silence");

        ProgressConditionProgress condition = Assert.Single(Reward(view, RewardId).Conditions);
        Assert.Equal(1, condition.Current);
        Assert.Equal(2, condition.Target);
        Assert.False(condition.Satisfied);
        Assert.Equal("Terminer deux scénarios.", condition.Description);
    }

    [Fact]
    public async Task TheGrantsTravelWithTheRewardSoAClientNeverJoinsTwoContracts()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new CatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        ConditionalRewardView view = Reward(await Complete(service, ScenarioOne, "fin-silence"), RewardId);

        Assert.Equal(3, view.Grants.Count);
        Assert.Contains(view.Grants, grant => grant.Type == RewardGrantTypes.Achievement && grant.Reference == "deux-scenarios");
        Assert.Contains(view.Grants, grant => grant.Type == RewardGrantTypes.Title && grant.Reference == "opiniatre");
        Assert.Contains(view.Grants, grant => grant.Type == RewardGrantTypes.Currency && grant.Amount == 60);
    }

    [Fact]
    public async Task ADisabledBlockStampsNothing()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(RewardsEnabled: false), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        _ = await Complete(service, ScenarioOne, "fin-silence");
        _ = await Complete(service, ScenarioTwo, "fin-alerte");

        Assert.Empty(repository.Profile!.EarnedRewards);
        Assert.Equal(0, repository.Profile!.Balance);
    }

    // ---------------------------------------------------------------------------------
    // Configuration validation.
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task TheDefaultConfigurationPublishesAnEvaluableReward()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default", null, ConfigurationService.CreateDefault("default"), CancellationToken.None);

        RewardsDefinition rewards = Assert.IsType<RewardsDefinition>(created.Document.Rewards);
        ConditionalRewardDefinition reward = Assert.Single(rewards.Rewards);
        Assert.True(rewards.Enabled);
        Assert.NotEmpty(reward.Conditions);

        // The product request, literally: a feat, a title and a currency value.
        Assert.Contains(reward.Grants, grant => grant.Type == RewardGrantType.Achievement);
        Assert.Contains(reward.Grants, grant => grant.Type == RewardGrantType.Title);
        Assert.Contains(reward.Grants, grant => grant.Type == RewardGrantType.Currency);
    }

    [Fact]
    public async Task ADocumentWithoutTheBlockNormalizesToAnEmptyCatalogueAndStaysPublishable()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "legacy", null, ConfigurationService.CreateDefault("legacy") with { Rewards = null }, CancellationToken.None);
        await service.PublishAsync("legacy", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("legacy", CancellationToken.None);

        RewardsDefinition rewards = Assert.IsType<RewardsDefinition>(published.Document.Rewards);
        Assert.True(rewards.Enabled);
        Assert.Empty(rewards.Rewards);
    }

    [Fact]
    public async Task AStatisticConditionIsValidatedAgainstThePublishedCatalogue()
    {
        ExperienceDocument accepted = WithReward(
            ValidReward with { Conditions = [Definition(ProgressConditionType.PlayerStatReached, threshold: 50, statKey: "lucidite")] });
        ExperienceDocument refused = WithReward(
            ValidReward with { Conditions = [Definition(ProgressConditionType.PlayerStatReached, threshold: 50, statKey: "inexistante")] });

        _ = await Upsert(accepted);
        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(refused));

        Assert.Equal("invalid_reward_condition", exception.Code);
    }

    [Fact]
    public async Task ARewardConditionMissingItsOperandIsRejected()
    {
        ConfigurationException unknownCategory = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(WithReward(
            ValidReward with { Conditions = [Definition(ProgressConditionType.CategoryCompleted, categoryId: Guid.NewGuid())] })));
        ConfigurationException missingThreshold = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(WithReward(
            ValidReward with { Conditions = [Definition(ProgressConditionType.ScenariosCompleted)] })));
        ConfigurationException missingStatKey = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(WithReward(
            ValidReward with { Conditions = [Definition(ProgressConditionType.PlayerStatReached, threshold: 5)] })));

        Assert.Equal("invalid_reward_condition", unknownCategory.Code);
        Assert.Equal("invalid_reward_condition", missingThreshold.Code);
        Assert.Equal("invalid_reward_condition", missingStatKey.Code);
    }

    [Theory]
    [InlineData("no-grant")]
    [InlineData("no-condition")]
    [InlineData("blank-label")]
    [InlineData("achievement-without-reference")]
    [InlineData("achievement-with-amount")]
    [InlineData("currency-without-amount")]
    [InlineData("currency-with-zero-amount")]
    [InlineData("currency-with-reference")]
    [InlineData("uppercase-reference")]
    public async Task AMalformedRewardIsRejected(string flaw)
    {
        ConditionalRewardDefinition reward = flaw switch
        {
            "no-grant" => ValidReward with { Grants = [] },
            "no-condition" => ValidReward with { Conditions = [] },
            "blank-label" => ValidReward with { Label = "   " },
            "achievement-without-reference" => ValidReward with { Grants = [new(RewardGrantType.Achievement, "Haut fait")] },
            "achievement-with-amount" => ValidReward with { Grants = [new(RewardGrantType.Achievement, "Haut fait", "haut-fait", 10)] },
            "currency-without-amount" => ValidReward with { Grants = [new(RewardGrantType.Currency, "Accords")] },
            "currency-with-zero-amount" => ValidReward with { Grants = [new(RewardGrantType.Currency, "Accords", Amount: 0)] },
            "currency-with-reference" => ValidReward with { Grants = [new(RewardGrantType.Currency, "Accords", "accords", 10)] },
            _ => ValidReward with { Grants = [new(RewardGrantType.Title, "Titre", "Opiniatre")] },
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(WithReward(reward)));

        Assert.Equal("invalid_reward", exception.Code);
    }

    [Fact]
    public async Task TwoRewardsCannotShareAnIdentifier()
    {
        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() => Upsert(
            ConfigurationService.CreateDefault("default") with
            {
                Rewards = new RewardsDefinition(true, [ValidReward, ValidReward with { Label = "Autre" }]),
            }));

        Assert.Equal("invalid_reward", exception.Code);
    }

    [Fact]
    public async Task AFinaleConditionCanAlsoUseAStatisticThreshold()
    {
        // The extraction is only real if the finale gained the new kind for free.
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        ExperienceConfigurationView created = await Upsert(baseline with
        {
            Finale = baseline.Finale! with
            {
                Conditions = [Definition(ProgressConditionType.PlayerStatReached, threshold: 30, statKey: "courage")],
            },
        });

        ProgressConditionDefinition condition = Assert.Single(Assert.IsType<FinaleDefinition>(created.Document.Finale).Conditions);
        Assert.Equal(ProgressConditionType.PlayerStatReached, condition.Type);
        Assert.Equal("courage", condition.StatKey);
    }

    // ---------------------------------------------------------------------------------
    // Fixtures.
    // ---------------------------------------------------------------------------------

    private static readonly ConditionalRewardDefinition ValidReward = new(
        RewardId,
        true,
        "Deux fois plutôt qu'une",
        "Vous avez terminé deux scénarios.",
        ProgressConditionMode.All,
        [Definition(ProgressConditionType.ScenariosCompleted, threshold: 2)],
        [new RewardGrantDefinition(RewardGrantType.Achievement, "Deux fois plutôt qu'une", "deux-scenarios")]);

    private static ExperienceDocument WithReward(ConditionalRewardDefinition reward) =>
        ConfigurationService.CreateDefault("default") with { Rewards = new RewardsDefinition(true, [reward]) };

    private static Task<ExperienceConfigurationView> Upsert(ExperienceDocument document) =>
        new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System)
            .UpsertAsync(document.FrontId, null, document, CancellationToken.None);

    private static ProgressConditionDefinition Definition(
        ProgressConditionType type,
        int? threshold = null,
        Guid? categoryId = null,
        string? statKey = null) =>
        new(Guid.NewGuid(), type, type.ToString(), threshold, categoryId, null, null, null, statKey);

    private static IReadOnlyList<ProgressConditionProgress> Evaluate(
        IReadOnlyList<ProgressCondition> conditions,
        IReadOnlyList<ScenarioProgress>? scenarios = null,
        Dictionary<string, int>? stats = null) =>
        ProgressConditionEvaluator.Evaluate(conditions, Snapshot(scenarios ?? [], stats ?? []));

    private static ProgressSnapshot Snapshot(IReadOnlyList<ScenarioProgress> scenarios, Dictionary<string, int> stats) =>
        new(scenarios, Categories, Journeys, stats);

    private static ProgressCondition StatCondition(string key, int threshold) =>
        new(Guid.NewGuid(), ProgressConditionKind.PlayerStatReached, $"{key} ≥ {threshold}", threshold, null, null, [], [], key);

    private static ProgressCondition Condition(ProgressConditionKind kind, int? threshold = null, Guid? categoryId = null) =>
        new(Guid.NewGuid(), kind, kind.ToString(), threshold, categoryId, null, [], []);

    private static ScenarioProgress Done(Guid scenarioId, string endingId = "fin") => new(scenarioId, true, [endingId], 100);

    private static ConditionalRewardView Reward(PlayerExperienceView view, Guid rewardId) =>
        Assert.Single(view.Rewards!, reward => reward.Id == rewardId);

    private static Task<PlayerExperienceView> Complete(PlayerExperienceService service, Guid scenarioId, string endingId) =>
        service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", $"{scenarioId}:{endingId}", "ScenarioCompleted", "Fin atteinte", "Vous avez terminé un scénario.",
            null, null, scenarioId, scenarioId, Guid.NewGuid(), null, "choice", "node", endingId, true, 4), CancellationToken.None);

    private static Task<PlayerExperienceView> Grant(PlayerExperienceService service, int amount, string key) =>
        service.ApplyPlayerStatAsync(new PlayerStatCommand("default", "player-1", "lucidite", amount, key), CancellationToken.None);

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InertScenarioHelp : IScenarioHelpProvider
    {
        public Task<ScenarioHelpSnapshot?> GetAsync(Guid scenarioVersionId, string? nodeId, string? choiceId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioHelpSnapshot?>(null);
    }

    private sealed class InertAi : IAssistantAiClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        public PlayerProfile? Profile { get; private set; }
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(Profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { Profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(Profile?.Id);
        public Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new JournalPage([], 0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }

    private sealed record CatalogStub(bool RewardsEnabled = true, int? AchievementAmount = null) : IPlayerExperienceCatalogProvider
    {
        public Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken) =>
            Task.FromResult(new PlayerExperienceCatalog(
                "ACCORD", "Accords", "♪", 0,
                [new FamiliarOption(FamiliarId, "Tierce", "Une voix.", "spark", "Warm", "Socratic", "amber", 2, ["hint"], ["spark"], ["Warm"], null, null, null, null, null)],
                [new RewardRule("ScenarioCompleted", "*", 25, "Terminer un scénario")],
                [],
                new OnboardingTutorial(Guid.NewGuid(), 1, true, true, false, []),
                new AssistantPolicy(true, true, true, true, 2, ["hint"]),
                Journeys,
                Categories,
                null,
                new PlayerStatPlan(true, [new PlayerStatPlanEntry(StatId, "lucidite", "Lucidité", "Ce que vous avez su voir.", 100)]),
                new RewardsPlan(RewardsEnabled,
                [
                    new ConditionalRewardPlan(
                        RewardId, true, "Deux fois plutôt qu'une", "Vous avez terminé deux scénarios.",
                        ProgressMode.All,
                        [new ProgressCondition(Guid.Parse("cccccccc-1111-4000-8000-000000000001"), ProgressConditionKind.ScenariosCompleted, "Terminer deux scénarios.", 2, null, null, [], [])],
                        [
                            new RewardGrantPlan(RewardGrantTypes.Achievement, "Deux fois plutôt qu'une", "deux-scenarios", AchievementAmount),
                            new RewardGrantPlan(RewardGrantTypes.Title, "Opiniâtre", "opiniatre", null),
                            new RewardGrantPlan(RewardGrantTypes.Currency, "Deux scénarios terminés", null, 60),
                        ],
                        null, null),
                    new ConditionalRewardPlan(
                        StatRewardId, true, "L'œil avant le mot", "Votre lucidité a atteint la moitié de sa portée.",
                        ProgressMode.All,
                        [new ProgressCondition(Guid.Parse("cccccccc-2222-4000-8000-000000000002"), ProgressConditionKind.PlayerStatReached, "Atteindre 50 de Lucidité.", 50, null, null, [], [], "lucidite")],
                        [new RewardGrantPlan(RewardGrantTypes.Achievement, "L'œil avant le mot", "lucidite-50", null)],
                        null, null),
                ])));
    }
}