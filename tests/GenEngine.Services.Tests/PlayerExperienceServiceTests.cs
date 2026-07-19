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
    public async Task JourneyProgressAggregatesMasteryPerJourneyAndPerCategory()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());
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
        var service = new PlayerExperienceService(repository, new CatalogStub(withJourneys: false), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

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
    /// A scenario finished on an earlier version stays finished when a newer version is
    /// explored without being completed. Reading completion off the highest-percentage
    /// version alone reported completed=0 here, and locked the downstream journey.
    /// </summary>
    [Fact]
    public async Task AScenarioCompletedOnAnEarlierVersionStaysCompleted()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        // v1 finished: one choice and one ending out of four objectives, so 50 %.
        _ = await service.RecordProgressEventAsync(new ProgressEventCommand(
            "default", "player-1", "s1:v1:end", "ScenarioCompleted", "Fin atteinte", "Vous avez terminé.",
            null, SharedCategoryId, FirstScenarioId, FirstScenarioVersionId, Guid.NewGuid(), null, "c1", "node-end", "e1", true, 4), CancellationToken.None);
        // v2 explored further but never finished: three choices out of four, so 75 %.
        foreach (string choiceId in new[] { "c1", "c2", "c3" })
        {
            _ = await service.RecordProgressEventAsync(new ProgressEventCommand(
                "default", "player-1", $"s1:v2:{choiceId}", "ChoiceSelected", "Une piste", "Vous avez suivi une piste.",
                null, SharedCategoryId, FirstScenarioId, SecondScenarioVersionId, Guid.NewGuid(), null, choiceId, "node-two", null, false, 4), CancellationToken.None);
        }

        PlayerJourneysView journeys = await service.GetJourneysAsync("player-1", "default", CancellationToken.None);
        CategoryProgressView shared = journeys.Items
            .Single(journey => journey.Id == FoundationJourneyId)
            .Categories.Single(category => category.Id == SharedCategoryId);

        Assert.Equal(1, shared.StartedCount);
        Assert.Equal(1, shared.CompletedCount);
        // The percentage keeps the best version, completion the union of every version.
        Assert.Equal(38, shared.ProgressPercent);
    }

    /// <summary>
    /// A prerequisite journey carrying no scenario must not gate anything. It used to lock
    /// its successors permanently, for every player, with no action able to recover.
    /// </summary>
    [Fact]
    public async Task AnEmptyPrerequisiteJourneyDoesNotLockItsSuccessors()
    {
        var repository = new RepositoryStub();
        var service = new PlayerExperienceService(repository, new CatalogStub(emptyGate: true), TimeProvider.System, new ScenarioHelpStub(), new AiStub());

        PlayerJourneysView journeys = await service.GetJourneysAsync("player-1", "default", CancellationToken.None);
        JourneyProgressView gate = journeys.Items.Single(journey => journey.Id == FoundationJourneyId);
        JourneyProgressView downstream = journeys.Items.Single(journey => journey.Id == AdvancedJourneyId);

        Assert.Equal(0, gate.ScenarioCount);
        // Zero percent and yet not blocking: completion measures outstanding requirements,
        // the percentage measures work done.
        Assert.Equal(0, gate.ProgressPercent);
        Assert.True(downstream.IsUnlocked);
        Assert.Empty(downstream.BlockedByJourneyIds);

        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);
        PlayerExperienceView chosen = await service.SelectDefaultJourneyAsync(
            "player-1", "default", AdvancedJourneyId, current.Revision, CancellationToken.None);
        Assert.Equal(AdvancedJourneyId, chosen.DefaultJourneyId);
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

    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public int SaveCount { get; private set; }
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
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

    private sealed class CatalogStub(bool withJourneys = true, bool emptyGate = false) : IPlayerExperienceCatalogProvider
    {
        private static readonly CategoryCatalogEntry[] Categories =
        [
            new(SharedCategoryId, "Socle", "Les bases.", "encre", 1, true, null, [FirstScenarioId, SecondScenarioId]),
            new(AdvancedCategoryId, "Suite", "La suite.", "or", 2, true, null, [ThirdScenarioId]),
        ];

        /// <summary>A gate journey whose only category carries no scenario at all.</summary>
        private static readonly CategoryCatalogEntry[] GateCategories =
        [
            new(SharedCategoryId, "Jalon", "Sans contenu.", "encre", 1, true, null, []),
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
                withJourneys ? (emptyGate ? GateCategories : Categories) : null));
    }
}