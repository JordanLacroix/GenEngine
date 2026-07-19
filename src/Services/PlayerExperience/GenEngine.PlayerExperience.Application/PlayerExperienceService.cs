using GenEngine.PlayerExperience.Domain;

namespace GenEngine.PlayerExperience.Application;

public sealed record FamiliarOption(Guid Id, string Name, string Description, string Form, string Tone, string WritingStyle, string Accent, int HelpLevel, IReadOnlyList<string> Capabilities, IReadOnlyList<string> AvailableForms, IReadOnlyList<string> AvailableTones, string? PortraitUrl, string? AvatarUrl, string? BackgroundUrl, string? License, string? Attribution);
public sealed record RewardRule(string Trigger, string ReferenceId, int Amount, string Description);
public sealed record ShopOffer(Guid Id, string Name, string Description, int Price, string RewardType, string RewardReference, bool Enabled);
public sealed record OnboardingStep(Guid Id, string Title, string Body, string Target, string Action, int Order, bool Required);
public sealed record OnboardingTutorial(Guid Id, int Version, bool Enabled, bool AllowSkip, bool RequiredAfterUpgrade, IReadOnlyList<OnboardingStep> Steps);
public sealed record AssistantPolicy(bool Enabled, bool RequireFirstRunConfiguration, bool Proactive, bool WarnOnKnownPath, int DefaultFrequency, IReadOnlyList<string> OfflineCapabilities);
public sealed record CategoryCatalogEntry(Guid Id, string Name, string Description, string Accent, int Order, bool IsVisible, string? ImageUrl, IReadOnlyList<Guid> ScenarioIds);
public sealed record JourneyCatalogEntry(Guid Id, string Name, string Description, string Accent, string? ImageUrl, int Order, bool IsVisible, IReadOnlyList<Guid> CategoryIds, IReadOnlyList<Guid> PrerequisiteJourneyIds, IReadOnlyList<string> Tags);

/// <summary>
/// Published catalog of a front, translated from the configuration document. Journeys
/// and categories are optional so a front published before journeys existed keeps
/// working: the player simply has no journey to choose from.
/// </summary>
public sealed record PlayerExperienceCatalog(
    string CurrencyCode,
    string CurrencyName,
    string CurrencyIcon,
    int InitialBalance,
    IReadOnlyList<FamiliarOption> Familiars,
    IReadOnlyList<RewardRule> RewardRules,
    IReadOnlyList<ShopOffer> Offers,
    OnboardingTutorial Onboarding,
    AssistantPolicy Assistant,
    IReadOnlyList<JourneyCatalogEntry>? Journeys = null,
    IReadOnlyList<CategoryCatalogEntry>? Categories = null);
public sealed record FamiliarSelection(Guid FamiliarId, string Form, string Tone, string WritingStyle, string Accent, int HelpLevel, string? CustomName = null, int InterventionFrequency = 2, bool Proactive = true);
public sealed record WalletEntryView(Guid Id, int Amount, string Reason, int BalanceAfter, DateTimeOffset CreatedAt);
public sealed record OnboardingStateView(Guid TutorialId, int Version, string Status, IReadOnlyList<Guid> CompletedStepIds, DateTimeOffset? CompletedAt, DateTimeOffset? SkippedAt, int Revision);
public sealed record JournalEntryView(Guid Id, string Type, string Title, string Summary, Guid? JourneyId, Guid? CategoryId, Guid? ScenarioId, Guid? ScenarioVersionId, Guid? SessionId, string? ReferenceId, DateTimeOffset OccurredAt);
public sealed record ScenarioMasteryView(Guid ScenarioId, Guid ScenarioVersionId, IReadOnlyList<string> ChoiceIds, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EndingIds, int DiscoveredObjectives, int TotalObjectives, int MasteryPercent, DateTimeOffset UpdatedAt);
/// <summary>Progression of one category, aggregated from <see cref="ScenarioMasteryView"/>.</summary>
public sealed record CategoryProgressView(
    Guid Id,
    string Name,
    string Accent,
    int Order,
    bool IsVisible,
    string? ImageUrl,
    int ScenarioCount,
    int StartedCount,
    int CompletedCount,
    int ProgressPercent);

/// <summary>
/// Progression and unlock state of one journey. <see cref="BlockedByJourneyIds"/> and
/// <see cref="BlockedByJourneyNames"/> name the prerequisites that are not satisfied yet,
/// so a client can explain a locked door instead of only greying it out.
/// </summary>
public sealed record JourneyProgressView(
    Guid Id,
    string Name,
    string Description,
    string Accent,
    string? ImageUrl,
    int Order,
    bool IsVisible,
    IReadOnlyList<string> Tags,
    bool IsUnlocked,
    IReadOnlyList<Guid> BlockedByJourneyIds,
    IReadOnlyList<string> BlockedByJourneyNames,
    int ScenarioCount,
    int StartedCount,
    int CompletedCount,
    int ProgressPercent,
    IReadOnlyList<CategoryProgressView> Categories);

/// <summary>
/// Journeys visible to the caller, with the journey the player selected and the journey
/// that actually applies once validity and unlocking are taken into account.
/// </summary>
public sealed record PlayerJourneysView(Guid? DefaultJourneyId, Guid? EffectiveJourneyId, int Revision, IReadOnlyList<JourneyProgressView> Items);

public sealed record PlayerExperienceView(Guid Id, string FrontId, int Revision, int Balance, string CurrencyCode, string CurrencyName, string CurrencyIcon, FamiliarSelection? Familiar, FamiliarOption? FamiliarDefinition, OnboardingStateView Onboarding, IReadOnlyList<ScenarioMasteryView> Masteries, IReadOnlyList<Guid> OwnedOfferIds, IReadOnlyList<WalletEntryView> RecentEntries, IReadOnlyList<JournalEntryView> RecentJournal, Guid? DefaultJourneyId = null, JourneyProgressView? EffectiveJourney = null);
public sealed record PlayerBootstrapView(string NextAction, PlayerExperienceView Experience, OnboardingTutorial Tutorial, AssistantPolicy Assistant);
public sealed record JournalView(IReadOnlyList<JournalEntryView> Items, int Total, IReadOnlyDictionary<string, int> TotalsByType);
public sealed record RewardCommand(string FrontId, string UserId, string Trigger, string ReferenceId, string IdempotencyKey);
public sealed record ProgressEventCommand(string FrontId, string UserId, string IdempotencyKey, string Type, string Title, string Summary, Guid? JourneyId, Guid? CategoryId, Guid? ScenarioId, Guid? ScenarioVersionId, Guid? SessionId, string? ReferenceId, string? ChoiceId, string? TargetNodeId, string? EndingId, bool Completed, int TotalObjectives, DateTimeOffset? OccurredAt = null);
public sealed record ContextualHelpRequest(string Context, Guid? ScenarioVersionId, string? ChoiceId, bool AlreadyExplored, string? AuthorHint);
public sealed record ContextualHelpView(string Source, string Message, bool IsFallback, string FamiliarName, string? AvatarUrl);

public interface IPlayerExperienceRepository
{
    Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken);
    Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPlayerExperienceCatalogProvider
{
    Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken);
}

public sealed class PlayerExperienceService(IPlayerExperienceRepository repository, IPlayerExperienceCatalogProvider catalogs, TimeProvider timeProvider)
{
    public async Task<PlayerExperienceView> GetAsync(string userId, string frontId, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    public async Task<PlayerExperienceView> ConfigureFamiliarAsync(string userId, string frontId, FamiliarSelection selection, int expectedRevision, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        FamiliarOption familiar = catalog.Familiars.SingleOrDefault(item => item.Id == selection.FamiliarId)
            ?? throw new PlayerExperienceException("familiar_not_found", "The selected familiar is unavailable.");
        if (!familiar.AvailableForms.Contains(selection.Form, StringComparer.OrdinalIgnoreCase)
            || !familiar.AvailableTones.Contains(selection.Tone, StringComparer.OrdinalIgnoreCase))
        {
            throw new PlayerExperienceException("invalid_familiar_configuration", "The selected form or tone is unavailable.");
        }

        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.ConfigureFamiliar(selection.FamiliarId, selection.Form, selection.Tone, selection.WritingStyle, selection.Accent, selection.HelpLevel, selection.CustomName, selection.InterventionFrequency, selection.Proactive, expectedRevision, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    /// <summary>
    /// Journeys the caller may see, each with its unlock state and its progression.
    /// Invisible journeys are never listed: visibility is a publishing decision.
    /// </summary>
    public async Task<PlayerJourneysView> GetJourneysAsync(string userId, string frontId, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        JourneyProgressView[] journeys = BuildJourneys(profile, catalog);
        return new PlayerJourneysView(
            profile.DefaultJourneyId,
            ResolveEffective(profile, journeys)?.Id,
            profile.Revision,
            journeys.Where(static journey => journey.IsVisible).ToArray());
    }

    /// <summary>
    /// Sets the journey the player browses by default, or clears it with a null identifier.
    /// The journey is validated against the published document: an unknown, hidden or
    /// still-locked journey is refused rather than silently ignored, otherwise a client
    /// would show a map the server does not agree with.
    /// </summary>
    public async Task<PlayerExperienceView> SelectDefaultJourneyAsync(string userId, string frontId, Guid? journeyId, int expectedRevision, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        if (journeyId is Guid requested)
        {
            JourneyProgressView journey = BuildJourneys(profile, catalog).SingleOrDefault(item => item.Id == requested)
                ?? throw new PlayerExperienceException("journey_not_found", "The selected journey is unavailable.");
            if (!journey.IsVisible)
            {
                throw new PlayerExperienceException("journey_not_found", "The selected journey is unavailable.");
            }

            if (!journey.IsUnlocked)
            {
                throw new PlayerExperienceException(
                    "journey_locked",
                    $"The journey requires completing first: {string.Join(", ", journey.BlockedByJourneyNames)}.");
            }
        }

        profile.SelectDefaultJourney(journeyId, expectedRevision, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    public async Task<PlayerBootstrapView> GetBootstrapAsync(string userId, string frontId, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        PlayerExperienceView experience = Map(profile, catalog);
        string nextAction = catalog.Assistant.RequireFirstRunConfiguration && profile.FamiliarId is null
            ? "ConfigureFamiliar"
            : experience.Onboarding.Status is "Completed" or "Skipped" || !catalog.Onboarding.Enabled
                ? "OpenMap"
                : "ResumeOnboarding";
        return new PlayerBootstrapView(nextAction, experience, catalog.Onboarding, catalog.Assistant);
    }

    public async Task<OnboardingStateView> CompleteOnboardingStepAsync(string userId, string frontId, Guid stepId, string idempotencyKey, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        OnboardingStep step = catalog.Onboarding.Steps.SingleOrDefault(item => item.Id == stepId)
            ?? throw new PlayerExperienceException("onboarding_step_not_found", "The onboarding step is unavailable.");
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.CompleteOnboardingStep(catalog.Onboarding.Id, catalog.Onboarding.Version, step.Id, idempotencyKey, catalog.Onboarding.Steps.Where(static item => item.Required).Select(static item => item.Id).ToArray(), timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapOnboarding(profile, catalog.Onboarding);
    }

    public async Task<OnboardingStateView> SkipOnboardingAsync(string userId, string frontId, string idempotencyKey, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (!catalog.Onboarding.AllowSkip) throw new PlayerExperienceException("onboarding_skip_forbidden", "This onboarding cannot be skipped.");
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.SkipOnboarding(catalog.Onboarding.Id, catalog.Onboarding.Version, idempotencyKey, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapOnboarding(profile, catalog.Onboarding);
    }

    public async Task<OnboardingStateView> ResetOnboardingAsync(string userId, string frontId, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.ResetOnboarding(catalog.Onboarding.Id, catalog.Onboarding.Version, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapOnboarding(profile, catalog.Onboarding);
    }

    public async Task<JournalView> GetJournalAsync(string userId, string frontId, string? type, Guid? journeyId, Guid? categoryId, Guid? scenarioId, int offset, int limit, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        IEnumerable<PlayerJournalEntry> query = profile.JournalEntries;
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase));
        if (journeyId is not null) query = query.Where(item => item.JourneyId == journeyId);
        if (categoryId is not null) query = query.Where(item => item.CategoryId == categoryId);
        if (scenarioId is not null) query = query.Where(item => item.ScenarioId == scenarioId);
        PlayerJournalEntry[] matching = query.OrderByDescending(static item => item.OccurredAt).ToArray();
        return new JournalView(matching.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100)).Select(MapJournal).ToArray(), matching.Length, matching.GroupBy(static item => item.Type).ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase));
    }

    public async Task<PlayerExperienceView> RecordProgressEventAsync(ProgressEventCommand command, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(command.FrontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(command.UserId, command.FrontId, catalog, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        _ = profile.RecordJournalEntry(command.IdempotencyKey, command.Type, command.Title, command.Summary, command.JourneyId, command.CategoryId, command.ScenarioId, command.ScenarioVersionId, command.SessionId, command.ReferenceId, command.OccurredAt ?? now, now);
        if (command.ScenarioId is Guid scenarioId && command.ScenarioVersionId is Guid versionId && command.SessionId is Guid sessionId)
        {
            profile.RecordExploration(scenarioId, versionId, sessionId, command.ChoiceId ?? string.Empty, command.TargetNodeId ?? string.Empty, command.Completed, command.EndingId, command.TotalObjectives, command.IdempotencyKey, now);
        }
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    public async Task<ContextualHelpView> GetContextualHelpAsync(string userId, string frontId, ContextualHelpRequest request, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        FamiliarOption? definition = catalog.Familiars.FirstOrDefault(item => item.Id == profile.FamiliarId);
        definition ??= catalog.Familiars.Count > 0 ? catalog.Familiars[0] : null;
        string name = string.IsNullOrWhiteSpace(profile.FamiliarCustomName) ? definition?.Name ?? "Compagnon" : profile.FamiliarCustomName;
        string message = request.AlreadyExplored && catalog.Assistant.WarnOnKnownPath
            ? "Vous avez déjà emprunté ce chemin. Vous pouvez le reprendre, ou tenter une option encore inconnue."
            : !string.IsNullOrWhiteSpace(request.AuthorHint)
                ? request.AuthorHint
                : request.Context switch
                {
                    "map" => "Choisissez une catégorie pour découvrir les scénarios et leur progression.",
                    "completion" => "Votre arbre conserve cette découverte. Rejouez pour révéler les branches encore inconnues.",
                    _ => "Relisez votre objectif et observez ce qui a changé depuis votre dernier choix.",
                };
        return new ContextualHelpView(request.AuthorHint is null ? "OfflineRule" : "AuthorHint", message, true, name, definition?.AvatarUrl ?? definition?.PortraitUrl);
    }

    public async Task<PlayerExperienceView> ApplyRewardAsync(RewardCommand command, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(command.FrontId, cancellationToken).ConfigureAwait(false);
        RewardRule rule = catalog.RewardRules.FirstOrDefault(item =>
                string.Equals(item.Trigger, command.Trigger, StringComparison.OrdinalIgnoreCase)
                && (item.ReferenceId == "*" || string.Equals(item.ReferenceId, command.ReferenceId, StringComparison.OrdinalIgnoreCase)))
            ?? throw new PlayerExperienceException("reward_rule_not_found", "No reward rule matches this event.");
        PlayerProfile profile = await GetOrCreateAsync(command.UserId, command.FrontId, catalog, cancellationToken).ConfigureAwait(false);
        _ = profile.Credit(command.IdempotencyKey, rule.Amount, rule.Description, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    public async Task<PlayerExperienceView> PurchaseAsync(string userId, string frontId, Guid offerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        ShopOffer offer = catalog.Offers.SingleOrDefault(item => item.Id == offerId && item.Enabled)
            ?? throw new PlayerExperienceException("offer_not_found", "The selected offer is unavailable.");
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.Purchase(offer.Id, offer.Price, idempotencyKey, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(profile, catalog);
    }

    private async Task<PlayerProfile> GetOrCreateAsync(string userId, string frontId, PlayerExperienceCatalog catalog, CancellationToken cancellationToken)
    {
        PlayerProfile? profile = await repository.GetAsync(userId, frontId, cancellationToken).ConfigureAwait(false);
        if (profile is not null) return profile;
        profile = PlayerProfile.Create(userId, frontId, catalog.InitialBalance, timeProvider.GetUtcNow());
        await repository.AddAsync(profile, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profile;
    }

    /// <summary>
    /// Best mastery known per scenario. A scenario can be played across several published
    /// versions, and <see cref="ScenarioMastery"/> is keyed by version; the player's
    /// progression on a scenario is the furthest they ever went on any of them.
    /// </summary>
    private static Dictionary<Guid, ScenarioMastery> BestMasteryByScenario(PlayerProfile profile)
    {
        Dictionary<Guid, ScenarioMastery> best = [];
        foreach (ScenarioMastery mastery in profile.ScenarioMasteries)
        {
            if (!best.TryGetValue(mastery.ScenarioId, out ScenarioMastery? current) || mastery.MasteryPercent > current.MasteryPercent)
            {
                best[mastery.ScenarioId] = mastery;
            }
        }

        return best;
    }

    /// <summary>
    /// Aggregates <see cref="ScenarioMastery"/> per journey and per category. Mastery is
    /// never recomputed here: it is the source of truth and is only summed.
    /// A scenario counts as started as soon as a mastery exists for it, and as completed
    /// once at least one ending was reached. The percentage is the mean mastery over every
    /// scenario of the scope, scenarios never played counting as zero, so a scope that
    /// carries no scenario yet reports zero instead of a misleading hundred.
    /// </summary>
    private static JourneyProgressView[] BuildJourneys(PlayerProfile profile, PlayerExperienceCatalog catalog)
    {
        IReadOnlyList<JourneyCatalogEntry> journeys = catalog.Journeys ?? [];
        Dictionary<Guid, CategoryCatalogEntry> categories = (catalog.Categories ?? []).ToDictionary(static category => category.Id);
        Dictionary<Guid, ScenarioMastery> masteries = BestMasteryByScenario(profile);

        Dictionary<Guid, CategoryProgressView> categoryProgress = categories.Values.ToDictionary(
            static category => category.Id,
            category =>
            {
                (int started, int completed, int percent) = Aggregate(category.ScenarioIds, masteries);
                return new CategoryProgressView(category.Id, category.Name, category.Accent, category.Order, category.IsVisible, category.ImageUrl, category.ScenarioIds.Count, started, completed, percent);
            });

        Dictionary<Guid, (string Name, bool Completed)> completion = journeys.ToDictionary(
            static journey => journey.Id,
            journey =>
            {
                Guid[] scenarioIds = JourneyScenarioIds(journey, categories);
                (int _, int completed, int _) = Aggregate(scenarioIds, masteries);
                return (journey.Name, scenarioIds.Length > 0 && completed == scenarioIds.Length);
            });

        return journeys.OrderBy(static journey => journey.Order).Select(journey =>
        {
            Guid[] scenarioIds = JourneyScenarioIds(journey, categories);
            (int started, int completed, int percent) = Aggregate(scenarioIds, masteries);
            Guid[] blocking = journey.PrerequisiteJourneyIds
                .Where(prerequisiteId => completion.TryGetValue(prerequisiteId, out (string Name, bool Completed) prerequisite) && !prerequisite.Completed)
                .ToArray();
            return new JourneyProgressView(
                journey.Id,
                journey.Name,
                journey.Description,
                journey.Accent,
                journey.ImageUrl,
                journey.Order,
                journey.IsVisible,
                journey.Tags,
                blocking.Length == 0,
                blocking,
                blocking.Select(prerequisiteId => completion[prerequisiteId].Name).ToArray(),
                scenarioIds.Length,
                started,
                completed,
                percent,
                journey.CategoryIds
                    .Where(categoryProgress.ContainsKey)
                    .Select(categoryId => categoryProgress[categoryId])
                    .OrderBy(static category => category.Order)
                    .ToArray());
        }).ToArray();
    }

    private static Guid[] JourneyScenarioIds(JourneyCatalogEntry journey, Dictionary<Guid, CategoryCatalogEntry> categories) =>
        journey.CategoryIds
            .Where(categories.ContainsKey)
            .SelectMany(categoryId => categories[categoryId].ScenarioIds)
            .Distinct()
            .ToArray();

    private static (int Started, int Completed, int Percent) Aggregate(IReadOnlyList<Guid> scenarioIds, Dictionary<Guid, ScenarioMastery> masteries)
    {
        if (scenarioIds.Count == 0) return (0, 0, 0);
        int started = 0;
        int completed = 0;
        int total = 0;
        foreach (Guid scenarioId in scenarioIds)
        {
            if (!masteries.TryGetValue(scenarioId, out ScenarioMastery? mastery)) continue;
            started++;
            if (mastery.EndingIds.Count > 0) completed++;
            total += mastery.MasteryPercent;
        }

        return (started, completed, (int)Math.Round(total / (double)scenarioIds.Count, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Journey that actually applies. The stored preference wins when it is still visible
    /// and unlocked; otherwise the first visible unlocked journey takes over, so a player
    /// whose default disappeared from a new publication still opens a usable map.
    /// </summary>
    private static JourneyProgressView? ResolveEffective(PlayerProfile profile, IReadOnlyList<JourneyProgressView> journeys)
    {
        JourneyProgressView? selected = profile.DefaultJourneyId is Guid journeyId
            ? journeys.FirstOrDefault(journey => journey.Id == journeyId && journey.IsVisible && journey.IsUnlocked)
            : null;
        return selected ?? journeys.FirstOrDefault(static journey => journey.IsVisible && journey.IsUnlocked);
    }

    private static PlayerExperienceView Map(PlayerProfile profile, PlayerExperienceCatalog catalog)
    {
        FamiliarOption? familiarDefinition = profile.FamiliarId is Guid familiarId ? catalog.Familiars.FirstOrDefault(item => item.Id == familiarId) : null;
        JourneyProgressView[] journeys = BuildJourneys(profile, catalog);
        return new(profile.Id, profile.FrontId, profile.Revision, profile.Balance, catalog.CurrencyCode, catalog.CurrencyName, catalog.CurrencyIcon,
            profile.FamiliarId is null ? null : new FamiliarSelection(profile.FamiliarId.Value, profile.FamiliarForm, profile.FamiliarTone, profile.FamiliarWritingStyle, profile.FamiliarAccent, profile.FamiliarHelpLevel, profile.FamiliarCustomName, profile.FamiliarInterventionFrequency, profile.FamiliarProactive),
            familiarDefinition,
            MapOnboarding(profile, catalog.Onboarding),
            profile.ScenarioMasteries.OrderByDescending(static item => item.UpdatedAt).Select(MapMastery).ToArray(),
            profile.OwnedItems.Select(static item => item.OfferId).ToArray(),
            profile.WalletEntries.OrderByDescending(static entry => entry.CreatedAt).Take(20).Select(static entry => new WalletEntryView(entry.Id, entry.Amount, entry.Reason, entry.BalanceAfter, entry.CreatedAt)).ToArray(),
            profile.JournalEntries.OrderByDescending(static entry => entry.OccurredAt).Take(20).Select(MapJournal).ToArray(),
            profile.DefaultJourneyId,
            ResolveEffective(profile, journeys));
    }

    private static OnboardingStateView MapOnboarding(PlayerProfile profile, OnboardingTutorial tutorial)
    {
        OnboardingState? state = profile.OnboardingStates.SingleOrDefault(item => item.TutorialId == tutorial.Id && item.Version == tutorial.Version);
        return state is null
            ? new OnboardingStateView(tutorial.Id, tutorial.Version, "NotStarted", [], null, null, 0)
            : new OnboardingStateView(state.TutorialId, state.Version, state.Status.ToString(), state.CompletedStepIds, state.CompletedAt, state.SkippedAt, state.Revision);
    }

    private static JournalEntryView MapJournal(PlayerJournalEntry entry) =>
        new(entry.Id, entry.Type, entry.Title, entry.Summary, entry.JourneyId, entry.CategoryId, entry.ScenarioId, entry.ScenarioVersionId, entry.SessionId, entry.ReferenceId, entry.OccurredAt);

    private static ScenarioMasteryView MapMastery(ScenarioMastery mastery) =>
        new(mastery.ScenarioId, mastery.ScenarioVersionId, mastery.ChoiceIds, mastery.NodeIds, mastery.EndingIds, mastery.DiscoveredObjectives, mastery.TotalObjectives, mastery.MasteryPercent, mastery.UpdatedAt);
}

public sealed class PlayerExperienceException : InvalidOperationException
{
    public PlayerExperienceException(string code, string message) : base(message) => Code = code;
    public PlayerExperienceException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;
    public string Code { get; }
}