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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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

    private static readonly Guid VersionId = Guid.Parse("5d9d8f4e-1a2b-4c3d-8e5f-6a7b8c9d0e1f");

    private static ScenarioHelpSnapshot Snapshot(AuthorHelpView? node, AuthorHelpView? choice) =>
        new("Le Diapason", "atrium", "Le diapason vibre.", ["Écouter", "Répondre"], node, choice);

    /// <summary>
    /// The two parameters that used to be dead. A help request must actually reach
    /// Authoring with the version and the choice it names, and the choice-level
    /// help must win over the step-level one.
    /// </summary>
    [Fact]
    public async Task ScenarioVersionAndChoiceAreForwardedAndChoiceHelpWins()
    {
        var repository = new RepositoryStub();
        var help = new ScenarioHelpStub(Snapshot(
            new AuthorHelpView("Indice du nœud", null, null, null),
            new AuthorHelpView("Indice du choix", null, null, null)));
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, help, new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, "answer", false, null, "atrium"),
            CancellationToken.None);

        Assert.Equal(VersionId, help.SeenVersionId);
        Assert.Equal("atrium", help.SeenNodeId);
        Assert.Equal("answer", help.SeenChoiceId);
        Assert.Equal(HelpSources.ScenarioHelp, view.Source);
        Assert.Equal("Indice du choix", view.Message);
        Assert.False(view.IsFallback);
    }

    /// <summary>The help level picks the modality, so it is not decorative.</summary>
    [Theory]
    [InlineData(1, "Indice", nameof(HelpModality.Hint))]
    [InlineData(3, "Conséquence", nameof(HelpModality.Consequence))]
    [InlineData(5, "Blocage", nameof(HelpModality.Blocker))]
    public async Task HelpLevelSelectsTheModalityActuallyServed(int helpLevel, string expected, string modality)
    {
        var repository = new RepositoryStub();
        var help = new ScenarioHelpStub(Snapshot(
            new AuthorHelpView("Indice", "Objectif", "Conséquence", "Blocage"),
            null));
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, help, new AiStub());
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);
        await service.ConfigureFamiliarAsync(
            "player-1",
            "default",
            new FamiliarSelection(FamiliarId, "owl", "Playful", "Socratic", "amber", helpLevel),
            current.Revision,
            CancellationToken.None);

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, null, false, null, "atrium"),
            CancellationToken.None);

        Assert.Equal(expected, view.Message);
        Assert.Equal(modality, view.Modality);
    }

    /// <summary>
    /// The known-path warning no longer suppresses the author's help: replaying a
    /// branch is an expected use in a teaching context, and knowing you have been
    /// here before does not make the hint useless. Both are returned, and
    /// <c>Source</c> names the help that carries the substance.
    /// </summary>
    [Fact]
    public async Task KnownPathWarningIsPrependedToTheAuthorHintRatherThanReplacingIt()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", null, null, true, "Indice fourni par le client"),
            CancellationToken.None);

        Assert.Equal(HelpSources.AuthorHint, view.Source);
        Assert.StartsWith("Vous avez déjà emprunté ce chemin.", view.Message, StringComparison.Ordinal);
        Assert.Contains("Indice fourni par le client", view.Message, StringComparison.Ordinal);
        Assert.False(view.IsFallback);
    }

    /// <summary>
    /// With nothing else to say, the warning still stands on its own — and keeps
    /// announcing itself as the source, since it is then the whole message.
    /// </summary>
    [Fact]
    public async Task KnownPathWarningStandsAloneWhenNoOtherHelpResolves()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", null, null, true, null),
            CancellationToken.None);

        Assert.Equal(HelpSources.KnownPathWarning, view.Source);
        Assert.StartsWith("Vous avez déjà emprunté ce chemin.", view.Message, StringComparison.Ordinal);
        Assert.False(view.IsFallback);
    }

    [Fact]
    public async Task AuthorHintIsHonouredAndNoLongerReportedAsAFallback()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", null, null, false, "Indice fourni par le client"),
            CancellationToken.None);

        Assert.Equal(HelpSources.AuthorHint, view.Source);
        Assert.Equal("Indice fourni par le client", view.Message);
        Assert.False(view.IsFallback);
    }

    [Fact]
    public async Task GenericRuleIsTheOnlyBranchReportedAsAFallback()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("map", null, null, false, null),
            CancellationToken.None);

        Assert.Equal(HelpSources.OfflineRule, view.Source);
        Assert.True(view.IsFallback);
    }

    [Fact]
    public async Task ConfiguredProviderAnswersWithTheRealScenarioContext()
    {
        var repository = new RepositoryStub();
        var ai = new AiStub("Écoutez le timbre avant de répondre.", configured: true);
        var help = new ScenarioHelpStub(Snapshot(new AuthorHelpView("Indice", null, null, null), null));
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, help, ai);

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, null, false, null, "atrium"),
            CancellationToken.None);

        Assert.Equal(HelpSources.Ai, view.Source);
        Assert.Equal("Écoutez le timbre avant de répondre.", view.Message);
        Assert.False(view.IsFallback);
        Assert.Equal("Le Diapason", ai.SeenContext?.ScenarioTitle);
        Assert.Equal("atrium", ai.SeenContext?.NodeId);
        Assert.Equal(["Écouter", "Répondre"], ai.SeenContext?.VisibleChoiceTexts);
        Assert.Equal("Indice", ai.SeenContext?.AuthorHelpText);
    }

    /// <summary>An erroring or timing-out provider must degrade, never surface.</summary>
    [Theory]
    [InlineData("error")]
    [InlineData("timeout")]
    public async Task FailingProviderFallsBackToTheAuthoredHelp(string kind)
    {
        Exception failure = kind == "timeout"
            ? new TaskCanceledException("The provider timed out.")
            : new HttpRequestException("The provider is unreachable.");
        var repository = new RepositoryStub();
        var help = new ScenarioHelpStub(Snapshot(new AuthorHelpView("Indice de repli", null, null, null), null));
        var service = new PlayerExperienceService(
            repository, new CatalogStub(), TimeProvider.System, help, new FailingAiStub(failure));

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, null, false, null, "atrium"),
            CancellationToken.None);

        Assert.Equal(HelpSources.ScenarioHelp, view.Source);
        Assert.Equal("Indice de repli", view.Message);
    }

    [Fact]
    public async Task UnavailableAuthoringDegradesToTheOfflineRule()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(
            repository, new CatalogStub(), TimeProvider.System, new UnavailableScenarioHelpStub(), new AiStub());

        ContextualHelpView view = await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, "answer", false, null, "atrium"),
            CancellationToken.None);

        Assert.Equal(HelpSources.OfflineRule, view.Source);
        Assert.True(view.IsFallback);
    }

    /// <summary>
    /// Help is a presentation overlay. Once the profile exists, asking for help
    /// must not write anything: no save, no turn, no journal entry, no wallet move.
    /// </summary>
    [Fact]
    public async Task ContextualHelpNeverMutatesPlayerState()
    {
        var repository = new RepositoryStub();
        var help = new ScenarioHelpStub(Snapshot(new AuthorHelpView("Indice", null, null, null), null));
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, help, new AiStub());
        PlayerExperienceView before = await service.GetAsync("player-1", "default", CancellationToken.None);
        int savesBefore = repository.SaveCount;

        await service.GetContextualHelpAsync(
            "player-1",
            "default",
            new ContextualHelpRequest("scenario", VersionId, "answer", false, null, "atrium"),
            CancellationToken.None);
        PlayerExperienceView after = await service.GetAsync("player-1", "default", CancellationToken.None);

        Assert.Equal(savesBefore, repository.SaveCount);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Balance, after.Balance);
        Assert.Empty(after.RecentJournal);
        Assert.Empty(after.RecentEntries);
    }

    /// <summary>Intervention frequency gates unsolicited help, not an explicit request.</summary>
    [Fact]
    public async Task SilentFamiliarSuppressesProactiveHelpButStillAnswersWhenAsked()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);
        await service.ConfigureFamiliarAsync(
            "player-1",
            "default",
            new FamiliarSelection(FamiliarId, "owl", "Playful", "Socratic", "amber", 2, null, 0, false),
            current.Revision,
            CancellationToken.None);

        ContextualHelpView proactive = await service.GetContextualHelpAsync(
            "player-1", "default",
            new ContextualHelpRequest("scenario", null, null, false, null, null, true),
            CancellationToken.None);
        ContextualHelpView asked = await service.GetContextualHelpAsync(
            "player-1", "default",
            new ContextualHelpRequest("scenario", null, null, false, null),
            CancellationToken.None);

        Assert.Equal(HelpSources.Suppressed, proactive.Source);
        Assert.Empty(proactive.Message);
        Assert.Equal(nameof(HelpModality.None), proactive.Modality);
        Assert.Equal(HelpSources.OfflineRule, asked.Source);
        Assert.NotEmpty(asked.Message);
    }

    /// <summary>
    /// Reproduit fidèlement la sémantique attendue du dépôt : filtres, tri, pagination et
    /// agrégats calculés sur l'ensemble filtré et non sur la page renvoyée.
    /// </summary>
    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public int SaveCount { get; private set; }
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
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

    private sealed class ScenarioHelpStub(ScenarioHelpSnapshot? snapshot = null) : IScenarioHelpProvider
    {
        public Guid? SeenVersionId { get; private set; }
        public string? SeenNodeId { get; private set; }
        public string? SeenChoiceId { get; private set; }

        public Task<ScenarioHelpSnapshot?> GetAsync(Guid scenarioVersionId, string? nodeId, string? choiceId, CancellationToken cancellationToken)
        {
            SeenVersionId = scenarioVersionId;
            SeenNodeId = nodeId;
            SeenChoiceId = choiceId;
            return Task.FromResult(snapshot);
        }
    }

    /// <summary>An Authoring that is down: the port degrades to null, it never throws.</summary>
    private sealed class UnavailableScenarioHelpStub : IScenarioHelpProvider
    {
        public Task<ScenarioHelpSnapshot?> GetAsync(Guid scenarioVersionId, string? nodeId, string? choiceId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioHelpSnapshot?>(null);
    }

    private sealed class AiStub(string? answer = null, bool configured = false) : IAssistantAiClient
    {
        public AssistantAiContext? SeenContext { get; private set; }
        public bool IsConfigured => configured;

        public Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken)
        {
            SeenContext = context;
            return Task.FromResult(answer);
        }
    }

    /// <summary>A provider that fails outright, standing in for an error or a timeout.</summary>
    private sealed class FailingAiStub(Exception failure) : IAssistantAiClient
    {
        public bool IsConfigured => true;
        public Task<string?> GenerateAsync(AssistantAiContext context, CancellationToken cancellationToken) =>
            Task.FromException<string?>(failure);
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