using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;
using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

/// <summary>
/// The finale is a threshold, not a terminal state. These tests cover each condition
/// type, a composition of several, and the property that matters most to the player:
/// after the finale fires, everything still works.
/// </summary>
public sealed class FinaleTests
{
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");
    private static readonly Guid CategoryA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid CategoryB = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid JourneyId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ScenarioOne = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
    private static readonly Guid ScenarioTwo = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000002");
    private static readonly Guid ScenarioThree = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000003");

    private static readonly CategoryCatalogEntry[] Categories =
    [
        Category(CategoryA, [ScenarioOne, ScenarioTwo]),
        Category(CategoryB, [ScenarioThree]),
    ];

    private static readonly JourneyCatalogEntry[] Journeys =
        [new(JourneyId, "Parcours", "", "encre", null, 1, true, [CategoryA, CategoryB], [], [])];

    private static CategoryCatalogEntry Category(Guid id, Guid[] scenarioIds) =>
        new(id, "Catégorie", "", "encre", 1, true, null, scenarioIds);

    [Fact]
    public void ScenariosCompletedCountsOnlyFinishedScenarios()
    {
        FinalePlan plan = Plan(FinaleMode.All, Condition(FinaleConditionKind.ScenariosCompleted, threshold: 2));

        FinaleConditionProgress started = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne), Started(ScenarioTwo)], Categories, Journeys));
        FinaleConditionProgress finished = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne), Done(ScenarioTwo)], Categories, Journeys));

        Assert.False(started.Satisfied);
        Assert.Equal(1, started.Current);
        Assert.Equal(2, started.Target);
        Assert.True(finished.Satisfied);
    }

    [Fact]
    public void CategoryCompletedRequiresEveryScenarioOfTheCategory()
    {
        FinalePlan plan = Plan(FinaleMode.All, Condition(FinaleConditionKind.CategoryCompleted, categoryId: CategoryA));

        Assert.False(Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne)], Categories, Journeys)).Satisfied);
        Assert.True(Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne), Done(ScenarioTwo)], Categories, Journeys)).Satisfied);
    }

    [Fact]
    public void AnEmptyCategoryIsNeverConsideredComplete()
    {
        // A freshly seeded instance has categories with no scenario attached yet.
        // Treating "nothing to do" as "done" would fire the finale immediately.
        FinalePlan plan = Plan(FinaleMode.All, Condition(FinaleConditionKind.CategoryCompleted, categoryId: CategoryA));
        CategoryCatalogEntry[] empty = [Category(CategoryA, [])];

        Assert.False(Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne)], empty, Journeys)).Satisfied);
    }

    [Fact]
    public void JourneyCompletedAggregatesTheScenariosOfEveryCategory()
    {
        FinalePlan plan = Plan(FinaleMode.All, Condition(FinaleConditionKind.JourneyCompleted, journeyId: JourneyId));

        FinaleConditionProgress partial = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne), Done(ScenarioTwo)], Categories, Journeys));
        FinaleConditionProgress complete = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne), Done(ScenarioTwo), Done(ScenarioThree)], Categories, Journeys));

        Assert.Equal(2, partial.Current);
        Assert.Equal(3, partial.Target);
        Assert.False(partial.Satisfied);
        Assert.True(complete.Satisfied);
    }

    [Fact]
    public void EndingsReachedCountsTheDistinctEndingsListed()
    {
        FinalePlan plan = Plan(FinaleMode.All, Condition(
            FinaleConditionKind.EndingsReached, threshold: 2, endingIds: ["fin-silence", "fin-alerte", "fin-retrait"]));

        FinaleConditionProgress one = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne, "fin-silence")], Categories, Journeys));
        FinaleConditionProgress two = Assert.Single(FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne, "fin-silence"), Done(ScenarioTwo, "fin-alerte")], Categories, Journeys));

        Assert.False(one.Satisfied);
        Assert.True(two.Satisfied);
    }

    [Fact]
    public void MasteryPercentReachedComparesTheAverageOverTheScopedScenarios()
    {
        FinalePlan plan = Plan(FinaleMode.All, Condition(
            FinaleConditionKind.MasteryPercentReached, threshold: 60, scenarioIds: [ScenarioOne, ScenarioTwo]));

        FinaleConditionProgress low = Assert.Single(FinaleEvaluator.Evaluate(
            plan, [Progress(ScenarioOne, 80), Progress(ScenarioTwo, 20)], Categories, Journeys));
        FinaleConditionProgress high = Assert.Single(FinaleEvaluator.Evaluate(
            plan, [Progress(ScenarioOne, 80), Progress(ScenarioTwo, 60)], Categories, Journeys));

        Assert.Equal(50, low.Current);
        Assert.False(low.Satisfied);
        Assert.True(high.Satisfied);
    }

    [Fact]
    public void ConditionsCombineWithAllAndAny()
    {
        FinaleCondition scenarios = Condition(FinaleConditionKind.ScenariosCompleted, threshold: 3);
        FinaleCondition category = Condition(FinaleConditionKind.CategoryCompleted, categoryId: CategoryA);
        FinaleEvaluator.ScenarioProgress[] progress = [Done(ScenarioOne), Done(ScenarioTwo)];

        FinalePlan all = Plan(FinaleMode.All, scenarios, category);
        FinalePlan any = Plan(FinaleMode.Any, scenarios, category);

        // The category is complete, the scenario count is not.
        Assert.False(FinaleEvaluator.IsSatisfied(all, FinaleEvaluator.Evaluate(all, progress, Categories, Journeys)));
        Assert.True(FinaleEvaluator.IsSatisfied(any, FinaleEvaluator.Evaluate(any, progress, Categories, Journeys)));
    }

    [Fact]
    public void ADisabledFinaleIsNeverSatisfied()
    {
        FinalePlan plan = Plan(FinaleMode.Any, Condition(FinaleConditionKind.ScenariosCompleted, threshold: 1)) with { Enabled = false };

        Assert.False(FinaleEvaluator.IsSatisfied(plan, FinaleEvaluator.Evaluate(plan, [Done(ScenarioOne)], Categories, Journeys)));
    }

    [Fact]
    public async Task ReachingTheFinaleIsStampedOnceAndTheGameKeepsGoing()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new FinaleCatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        PlayerExperienceView before = await Complete(service, ScenarioOne, "fin-silence");
        Assert.False(before.Finale?.Reached);

        PlayerExperienceView reached = await Complete(service, ScenarioTwo, "fin-alerte");
        Assert.True(reached.Finale?.Reached);
        Assert.NotNull(reached.Finale?.ReachedAt);
        Assert.Contains(reached.RecentJournal, entry => entry.Type == "FinaleReached");
        DateTimeOffset stamp = reached.Finale!.ReachedAt!.Value;

        // Everything still works afterwards: a third scenario is played, recorded and
        // rewarded exactly as before, and the finale stamp is neither cleared nor moved.
        PlayerExperienceView after = await Complete(service, ScenarioThree, "fin-retrait");
        PlayerExperienceView rewarded = await service.ApplyRewardAsync(
            new RewardCommand("default", "player-1", "ScenarioCompleted", "*", "reward-after-finale"), CancellationToken.None);

        Assert.True(after.Finale?.Reached);
        Assert.Equal(stamp, after.Finale?.ReachedAt);
        Assert.Equal(3, after.Masteries.Count);
        Assert.Equal(25, rewarded.Balance);
        Assert.Single(after.RecentJournal, entry => entry.Type == "FinaleReached");
    }

    [Fact]
    public async Task TheProgressTowardsTheFinaleIsReadableBeforeItIsReached()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new FinaleCatalogStub(), TimeProvider.System, new InertScenarioHelp(), new InertAi());

        PlayerExperienceView view = await Complete(service, ScenarioOne, "fin-silence");

        FinaleConditionProgress condition = Assert.Single(view.Finale!.Conditions);
        Assert.Equal(1, condition.Current);
        Assert.Equal(2, condition.Target);
        Assert.False(condition.Satisfied);
        Assert.Equal("Terminer deux scénarios.", condition.Description);
    }

    [Fact]
    public async Task TheDefaultConfigurationPublishesAnEvaluableFinale()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default", null, ConfigurationService.CreateDefault("default"), CancellationToken.None);

        FinaleDefinition finale = Assert.IsType<FinaleDefinition>(created.Document.Finale);
        Assert.True(finale.Enabled);
        Assert.NotEmpty(finale.Conditions);
        Assert.All(finale.Conditions, condition => Assert.False(string.IsNullOrWhiteSpace(condition.Description)));
    }

    [Fact]
    public async Task AConfigurationWithoutFinaleStaysPublishable()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("legacy") with { Finale = null };

        ExperienceConfigurationView created = await service.UpsertAsync("legacy", null, document, CancellationToken.None);
        await service.PublishAsync("legacy", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("legacy", CancellationToken.None);

        Assert.Null(published.Document.Finale);
    }

    [Fact]
    public async Task AFinaleConditionMissingItsOperandIsRejected()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");

        ConfigurationException unknownCategory = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, baseline with
            {
                Finale = baseline.Finale! with
                {
                    Conditions = [new FinaleConditionDefinition(Guid.NewGuid(), FinaleConditionType.CategoryCompleted, "Catégorie inconnue.", CategoryId: Guid.NewGuid())],
                },
            }, CancellationToken.None));

        ConfigurationException missingThreshold = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, baseline with
            {
                Finale = baseline.Finale! with
                {
                    Conditions = [new FinaleConditionDefinition(Guid.NewGuid(), FinaleConditionType.ScenariosCompleted, "Sans seuil.")],
                },
            }, CancellationToken.None));

        Assert.Equal("invalid_finale_condition", unknownCategory.Code);
        Assert.Equal("invalid_finale_condition", missingThreshold.Code);
    }

    private static async Task<PlayerExperienceView> Complete(PlayerExperienceService service, Guid scenarioId, string endingId) =>
        await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", $"{scenarioId}:{endingId}", "ScenarioCompleted", "Fin atteinte", "Vous avez terminé un scénario.",
            null, null, scenarioId, scenarioId, Guid.NewGuid(), null, "choice", "node", endingId, true, 4), CancellationToken.None);

    private static FinalePlan Plan(FinaleMode mode, params FinaleCondition[] conditions) =>
        new(Guid.NewGuid(), true, "Fin", "Résumé", "Corps", mode, conditions, null, null, null);

    private static FinaleCondition Condition(
        FinaleConditionKind kind,
        int? threshold = null,
        Guid? categoryId = null,
        Guid? journeyId = null,
        IReadOnlyList<string>? endingIds = null,
        IReadOnlyList<Guid>? scenarioIds = null) =>
        new(Guid.NewGuid(), kind, kind.ToString(), threshold, categoryId, journeyId, endingIds ?? [], scenarioIds ?? []);

    private static FinaleEvaluator.ScenarioProgress Done(Guid scenarioId, string endingId = "fin") =>
        new(scenarioId, true, [endingId], 100);

    private static FinaleEvaluator.ScenarioProgress Started(Guid scenarioId) => new(scenarioId, false, [], 25);

    private static FinaleEvaluator.ScenarioProgress Progress(Guid scenarioId, int mastery) =>
        new(scenarioId, true, ["fin"], mastery);

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Contextual help plays no part in these tests; the collaborators stay inert.</summary>
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
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile?.Id);

        // The paginated journal query is not exercised here: these tests read the recent
        // journal off the profile itself, which is what the experience view projects.
        public Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new JournalPage([], 0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }

    private sealed class FinaleCatalogStub : IPlayerExperienceCatalogProvider
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
                new FinalePlan(
                    Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d201"), true,
                    "Ce qui reste après vous", "Vous avez traversé les postures.", "Rien ne se ferme.",
                    FinaleMode.All,
                    [new FinaleCondition(Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d211"), FinaleConditionKind.ScenariosCompleted, "Terminer deux scénarios.", 2, null, null, [], [])],
                    null, null, null)));
    }
}