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

    [Fact]
    public async Task JournalAggregatesCoverTheWholeFilteredSetNotThePage()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        await RecordJournalEntriesAsync(service, 30, 12);

        JournalView firstPage = await service.GetJournalAsync("player-1", "default", null, null, null, null, 1, 10, CancellationToken.None);
        JournalView secondPage = await service.GetJournalAsync("player-1", "default", null, null, null, null, 2, 10, CancellationToken.None);

        // La page ne contient que 10 entrées, mais total et totaux par type portent sur les 42.
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(42, firstPage.Total);
        Assert.Equal(30, firstPage.TotalsByType["ScenarioCompleted"]);
        Assert.Equal(12, firstPage.TotalsByType["ChoiceMade"]);

        // Les agrégats sont identiques d'une page à l'autre.
        Assert.Equal(firstPage.Total, secondPage.Total);
        Assert.Equal(firstPage.TotalsByType, secondPage.TotalsByType);
        Assert.Empty(firstPage.Items.Select(static item => item.Id).Intersect(secondPage.Items.Select(static item => item.Id)));
    }

    [Fact]
    public async Task JournalFilterRestrictsAggregatesAndReportsTheExactTotal()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        await RecordJournalEntriesAsync(service, 30, 12);

        JournalView filtered = await service.GetJournalAsync("player-1", "default", "ChoiceMade", null, null, null, 1, 100, CancellationToken.None);

        Assert.Equal(12, filtered.Total);
        Assert.Equal(12, filtered.Items.Count);
        Assert.Equal(new Dictionary<string, int> { ["ChoiceMade"] = 12 }, filtered.TotalsByType);
    }

    [Fact]
    public async Task JournalPaginationClampsBoundsAndReturnsAnEmptyLastPage()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System);
        await RecordJournalEntriesAsync(service, 25, 0);

        JournalView partialLastPage = await service.GetJournalAsync("player-1", "default", null, null, null, null, 3, 10, CancellationToken.None);
        JournalView beyondLastPage = await service.GetJournalAsync("player-1", "default", null, null, null, null, 9, 10, CancellationToken.None);
        JournalView clamped = await service.GetJournalAsync("player-1", "default", null, null, null, null, 0, 10_000, CancellationToken.None);

        Assert.Equal(5, partialLastPage.Items.Count);
        Assert.Empty(beyondLastPage.Items);
        Assert.Equal(25, beyondLastPage.Total);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(Pagination.MaxPageSize, clamped.PageSize);
    }

    private static async Task RecordJournalEntriesAsync(PlayerExperienceService service, int completed, int choices)
    {
        DateTimeOffset origin = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        for (int index = 0; index < completed; index++)
        {
            await service.RecordProgressEventAsync(
                new ProgressEventCommand("default", "player-1", $"completed-{index}", "ScenarioCompleted", $"Scénario {index}", "Terminé", null, null, null, null, null, null, null, null, null, true, 0, origin.AddMinutes(index)),
                CancellationToken.None);
        }

        for (int index = 0; index < choices; index++)
        {
            await service.RecordProgressEventAsync(
                new ProgressEventCommand("default", "player-1", $"choice-{index}", "ChoiceMade", $"Choix {index}", "Choisi", null, null, null, null, null, null, null, null, null, false, 0, origin.AddHours(1).AddMinutes(index)),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Reproduit fidèlement la sémantique attendue du dépôt : filtres, tri, pagination et
    /// agrégats calculés sur l'ensemble filtré et non sur la page renvoyée.
    /// </summary>
    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile?.Id);

        public Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken)
        {
            IEnumerable<PlayerJournalEntry> entries = profile?.JournalEntries ?? [];
            if (filter.Type is not null) entries = entries.Where(entry => string.Equals(entry.Type, filter.Type, StringComparison.OrdinalIgnoreCase));
            if (filter.JourneyId is Guid journeyId) entries = entries.Where(entry => entry.JourneyId == journeyId);
            if (filter.CategoryId is Guid categoryId) entries = entries.Where(entry => entry.CategoryId == categoryId);
            if (filter.ScenarioId is Guid scenarioId) entries = entries.Where(entry => entry.ScenarioId == scenarioId);
            PlayerJournalEntry[] matching = [.. entries
                .OrderByDescending(static entry => entry.OccurredAt)
                .ThenByDescending(static entry => entry.Id)];
            Dictionary<string, int> totalsByType = new(StringComparer.OrdinalIgnoreCase);
            foreach (PlayerJournalEntry entry in matching)
            {
                totalsByType[entry.Type] = totalsByType.TryGetValue(entry.Type, out int existing) ? existing + 1 : 1;
            }

            return Task.FromResult(new JournalPage(
                [.. matching.Skip(offset).Take(limit)],
                matching.Length,
                totalsByType));
        }
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
