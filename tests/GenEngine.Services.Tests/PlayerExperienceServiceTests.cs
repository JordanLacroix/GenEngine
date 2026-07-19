using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

public sealed class PlayerExperienceServiceTests
{
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");
    private static readonly Guid OfferId = Guid.Parse("370b6f82-a264-45cc-a0d0-2d71e58be15e");
    private static readonly Guid TutorialId = Guid.Parse("91f09c08-5418-46c4-91c6-9160cd79edb4");
    private static readonly Guid StepId = Guid.Parse("93363c15-d853-4db3-a378-d808ca2ddf25");
    private static readonly Guid SharedCategoryId = Guid.Parse("1a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Guid AdvancedCategoryId = Guid.Parse("1a2b3c4d-0000-4000-8000-000000000002");
    private static readonly Guid FoundationJourneyId = Guid.Parse("2a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Guid AdvancedJourneyId = Guid.Parse("2a2b3c4d-0000-4000-8000-000000000002");
    private static readonly Guid FirstScenarioId = Guid.Parse("3a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Guid SecondScenarioId = Guid.Parse("3a2b3c4d-0000-4000-8000-000000000002");
    private static readonly Guid ThirdScenarioId = Guid.Parse("3a2b3c4d-0000-4000-8000-000000000003");
    private static readonly Guid FirstScenarioVersionId = Guid.Parse("4a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Guid SecondScenarioVersionId = Guid.Parse("4a2b3c4d-0000-4000-8000-000000000002");

    [Fact]
    public async Task RewardsAreIdempotentAndPurchasesDebitTheWallet()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        var reward = new RewardCommand("default", "player-1", "ScenarioCompleted", "*", "session-1:completed");

        PlayerExperienceView first = await service.ApplyRewardAsync(reward, CancellationToken.None);
        PlayerExperienceView replay = await service.ApplyRewardAsync(reward, CancellationToken.None);
        PlayerExperienceView purchased = await service.PurchaseAsync(
            "player-1",
            "default",
            OfferId,
            "purchase-1",
            CancellationToken.None);

        Assert.Equal(25, first.Balance);
        Assert.Equal(25, replay.Balance);
        Assert.Equal(5, purchased.Balance);
        Assert.Contains(OfferId, purchased.OwnedOfferIds);
    }

    [Fact]
    public async Task FamiliarConfigurationIsValidatedAgainstThePublishedCatalog()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        PlayerExperienceView updated = await service.ConfigureFamiliarAsync(
            "player-1",
            "default",
            new FamiliarSelection(FamiliarId, "owl", "Playful", "Socratic", "amber", 3),
            current.Revision,
            CancellationToken.None);

        Assert.Equal("owl", updated.Familiar?.Form);
        Assert.Equal("Playful", updated.Familiar?.Tone);
        Assert.Equal(3, updated.Familiar?.HelpLevel);
    }

    [Fact]
    public async Task BootstrapPersistsOnboardingAndProgressJournal()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);

        PlayerBootstrapView bootstrap = await service.GetBootstrapAsync("player-1", "default", CancellationToken.None);
        OnboardingStateView completed = await service.CompleteOnboardingStepAsync(
            "player-1", "default", StepId, Guid.NewGuid().ToString(), CancellationToken.None);
        PlayerExperienceView progress = await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", "session:choice:one", "ChoiceSelected", "Une piste suivie", "Vous avez suivi une nouvelle piste.",
            null, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "choice-one", "choice-one", "node-two", null, false, 4), CancellationToken.None);

        Assert.Equal("ConfigureFamiliar", bootstrap.NextAction);
        Assert.Equal("Completed", completed.Status);
        Assert.Single(progress.RecentJournal);
        Assert.Equal(25, progress.Masteries[0].MasteryPercent);
    }

    [Fact]
    public async Task JourneyProgressAggregatesMasteryPerJourneyAndPerCategory()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        await PlayFoundationScenariosAsync(service, completeSecond: false);

        PlayerJourneysView journeys = await service.GetJourneysAsync("player-1", "default", CancellationToken.None);
        JourneyProgressView foundation = journeys.Items.Single(journey => journey.Id == FoundationJourneyId);
        JourneyProgressView advanced = journeys.Items.Single(journey => journey.Id == AdvancedJourneyId);

        Assert.Equal(2, foundation.ScenarioCount);
        Assert.Equal(2, foundation.StartedCount);
        Assert.Equal(1, foundation.CompletedCount);
        Assert.Equal(63, foundation.ProgressPercent);
        CategoryProgressView shared = foundation.Categories.Single(category => category.Id == SharedCategoryId);
        Assert.Equal(2, shared.ScenarioCount);
        Assert.Equal(1, shared.CompletedCount);
        Assert.Equal(63, shared.ProgressPercent);

        // The same category feeds both journeys: sharing is a product requirement.
        Assert.Contains(advanced.Categories, category => category.Id == SharedCategoryId);
        Assert.Equal(3, advanced.ScenarioCount);
        Assert.False(advanced.IsUnlocked);
        Assert.Equal([FoundationJourneyId], advanced.BlockedByJourneyIds);
    }

    [Fact]
    public async Task DefaultJourneyIsRefusedUntilItsPrerequisitesAreSatisfied()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        await PlayFoundationScenariosAsync(service, completeSecond: false);

        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);
        PlayerExperienceException locked = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.SelectDefaultJourneyAsync("player-1", "default", AdvancedJourneyId, current.Revision, CancellationToken.None));
        Assert.Equal("journey_locked", locked.Code);

        PlayerExperienceView chosen = await service.SelectDefaultJourneyAsync(
            "player-1", "default", FoundationJourneyId, current.Revision, CancellationToken.None);
        Assert.Equal(FoundationJourneyId, chosen.DefaultJourneyId);
        Assert.Equal(FoundationJourneyId, chosen.EffectiveJourney?.Id);

        await CompleteSecondFoundationScenarioAsync(service);
        PlayerExperienceView completed = await service.GetAsync("player-1", "default", CancellationToken.None);
        PlayerExperienceView promoted = await service.SelectDefaultJourneyAsync(
            "player-1", "default", AdvancedJourneyId, completed.Revision, CancellationToken.None);

        Assert.Equal(AdvancedJourneyId, promoted.DefaultJourneyId);
        Assert.True(promoted.EffectiveJourney?.IsUnlocked);
    }

    [Fact]
    public async Task AFrontWithoutJourneysKeepsAProfileWithoutDefault()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(withJourneys: false), TimeProvider.System);

        PlayerExperienceView experience = await service.GetAsync("player-1", "default", CancellationToken.None);
        PlayerJourneysView journeys = await service.GetJourneysAsync("player-1", "default", CancellationToken.None);
        PlayerExperienceException unknown = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.SelectDefaultJourneyAsync("player-1", "default", FoundationJourneyId, experience.Revision, CancellationToken.None));

        Assert.Null(experience.DefaultJourneyId);
        Assert.Null(experience.EffectiveJourney);
        Assert.Empty(journeys.Items);
        Assert.Equal("journey_not_found", unknown.Code);
    }

    /// <summary>
    /// First scenario fully mastered (one choice and one ending out of two objectives),
    /// second one only started, so the foundation journey sits at (100 + 25) / 2.
    /// </summary>
    private static async Task PlayFoundationScenariosAsync(PlayerExperienceService service, bool completeSecond)
    {
        _ = await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", "s1:end", "ScenarioCompleted", "Fin atteinte", "Vous avez terminé.",
            null, SharedCategoryId, FirstScenarioId, FirstScenarioVersionId, Guid.NewGuid(), null, "c1", "node-end", "e1", true, 2), CancellationToken.None);
        _ = await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", "s2:choice", "ChoiceSelected", "Une piste", "Vous avez suivi une piste.",
            null, SharedCategoryId, SecondScenarioId, SecondScenarioVersionId, Guid.NewGuid(), null, "c1", "node-two", null, false, 4), CancellationToken.None);
        if (completeSecond) await CompleteSecondFoundationScenarioAsync(service);
    }

    private static async Task CompleteSecondFoundationScenarioAsync(PlayerExperienceService service) =>
        _ = await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", "s2:end", "ScenarioCompleted", "Fin atteinte", "Vous avez terminé.",
            null, SharedCategoryId, SecondScenarioId, SecondScenarioVersionId, Guid.NewGuid(), null, "c1", "node-end", "e2", true, 4), CancellationToken.None);

    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CatalogStub(bool withJourneys = true) : IPlayerExperienceCatalogProvider
    {
        private static readonly CategoryCatalogEntry[] Categories =
        [
            new(SharedCategoryId, "Socle", "Les bases.", "encre", 1, true, null, [FirstScenarioId, SecondScenarioId]),
            new(AdvancedCategoryId, "Suite", "La suite.", "or", 2, true, null, [ThirdScenarioId]),
        ];

        // Both journeys reference the shared category on purpose: journeys may overlap.
        private static readonly JourneyCatalogEntry[] Journeys =
        [
            new(FoundationJourneyId, "Fondations", "Le premier palier.", "encre", null, 1, true, [SharedCategoryId], [], ["socle"]),
            new(AdvancedJourneyId, "Approfondissement", "Le palier suivant.", "or", null, 2, true, [SharedCategoryId, AdvancedCategoryId], [FoundationJourneyId], ["suite"]),
        ];

        public Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken) =>
            Task.FromResult(new PlayerExperienceCatalog(
                "BRAISE",
                "Braises",
                "✦",
                0,
                [new FamiliarOption(FamiliarId, "Lueur", "Un compagnon curieux.", "spark", "Warm", "Socratic", "amber", 2, ["hint"], ["spark", "owl"], ["Warm", "Playful"], null, null, null, null, null)],
                [new RewardRule("ScenarioCompleted", "*", 25, "Terminer un scénario")],
                [new ShopOffer(OfferId, "Plumage", "Cosmétique", 20, "FamiliarCosmetic", "plumage", true)],
                new OnboardingTutorial(TutorialId, 1, true, true, false, [new OnboardingStep(StepId, "Bienvenue", "Découvrez la carte.", "map", "open", 1, true)]),
                new AssistantPolicy(true, true, true, true, 2, ["hint"]),
                withJourneys ? Journeys : null,
                withJourneys ? Categories : null));
    }
}