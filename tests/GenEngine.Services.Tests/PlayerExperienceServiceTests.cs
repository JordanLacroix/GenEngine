using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

public sealed class PlayerExperienceServiceTests
{
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");
    private static readonly Guid OfferId = Guid.Parse("370b6f82-a264-45cc-a0d0-2d71e58be15e");
    private static readonly Guid TutorialId = Guid.Parse("91f09c08-5418-46c4-91c6-9160cd79edb4");
    private static readonly Guid StepId = Guid.Parse("93363c15-d853-4db3-a378-d808ca2ddf25");

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

    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CatalogStub : IPlayerExperienceCatalogProvider
    {
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
                new AssistantPolicy(true, true, true, true, 2, ["hint"])));
    }
}
