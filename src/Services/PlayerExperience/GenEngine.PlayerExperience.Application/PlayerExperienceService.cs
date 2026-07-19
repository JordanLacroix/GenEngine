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
public sealed record JournalView(IReadOnlyList<JournalEntryView> Items, int Page, int PageSize, int Total, IReadOnlyDictionary<string, int> TotalsByType);
public sealed record JournalFilter(string? Type, Guid? JourneyId, Guid? CategoryId, Guid? ScenarioId);
public sealed record JournalPage(IReadOnlyList<PlayerJournalEntry> Items, int Total, IReadOnlyDictionary<string, int> TotalsByType);

/// <summary>Bornes de pagination partagées par toutes les routes de l'API PlayerExperience.</summary>
public static class Pagination
{
    public const int DefaultPageSize = 25;

    public const int MinPageSize = 1;

    public const int MaxPageSize = 100;

    public static (int Page, int PageSize, int Offset) Normalize(int? page, int? pageSize)
    {
        int normalizedPage = Math.Max(1, page ?? 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, MinPageSize, MaxPageSize);
        return (normalizedPage, normalizedPageSize, (normalizedPage - 1) * normalizedPageSize);
    }
}
public sealed record RewardCommand(string FrontId, string UserId, string Trigger, string ReferenceId, string IdempotencyKey);
public sealed record ProgressEventCommand(string FrontId, string UserId, string IdempotencyKey, string Type, string Title, string Summary, Guid? JourneyId, Guid? CategoryId, Guid? ScenarioId, Guid? ScenarioVersionId, Guid? SessionId, string? ReferenceId, string? ChoiceId, string? TargetNodeId, string? EndingId, bool Completed, int TotalObjectives, DateTimeOffset? OccurredAt = null);
public sealed record ContextualHelpRequest(string Context, Guid? ScenarioVersionId, string? ChoiceId, bool AlreadyExplored, string? AuthorHint, string? NodeId = null, bool Proactive = false);

/// <summary>
/// <paramref name="Source"/> names where <paramref name="Message"/> actually came
/// from — one of <see cref="HelpSources"/> — and never a source that was merely
/// consulted. <paramref name="IsFallback"/> is true only when the message is the
/// built-in generic text, that is when nothing scenario-specific, authored or
/// generated could be served.
/// </summary>
public sealed record ContextualHelpView(string Source, string Message, bool IsFallback, string FamiliarName, string? AvatarUrl, string Modality);

public interface IPlayerExperienceRepository
{
    Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken);
    Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Résout l'identifiant du profil sans matérialiser ses collections. Retourne <c>null</c>
    /// lorsque le profil n'existe pas encore.
    /// </summary>
    Task<Guid?> FindProfileIdAsync(string userId, string frontId, CancellationToken cancellationToken);

    /// <summary>
    /// Pagine, filtre et agrège le journal côté base : ni la page, ni le total, ni les totaux
    /// par type ne doivent matérialiser l'historique complet du profil.
    /// </summary>
    Task<JournalPage> ListJournalAsync(Guid profileId, JournalFilter filter, int offset, int limit, CancellationToken cancellationToken);
}

public interface IPlayerExperienceCatalogProvider
{
    Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken);
}

public sealed class PlayerExperienceService(
    IPlayerExperienceRepository repository,
    IPlayerExperienceCatalogProvider catalogs,
    TimeProvider timeProvider,
    IScenarioHelpProvider scenarioHelp,
    IAssistantAiClient aiClient)
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

    public async Task<JournalView> GetJournalAsync(string userId, string frontId, string? type, Guid? journeyId, Guid? categoryId, Guid? scenarioId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        (int normalizedPage, int normalizedPageSize, int offset) = Pagination.Normalize(page, pageSize);

        // Le profil n'est résolu qu'à l'identifiant : charger l'agrégat matérialiserait tout le journal.
        Guid? profileId = await repository.FindProfileIdAsync(userId, frontId, cancellationToken).ConfigureAwait(false);
        if (profileId is null)
        {
            PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
            PlayerProfile created = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
            profileId = created.Id;
        }

        JournalFilter filter = new(string.IsNullOrWhiteSpace(type) ? null : type.Trim(), journeyId, categoryId, scenarioId);
        JournalPage result = await repository.ListJournalAsync(profileId.Value, filter, offset, normalizedPageSize, cancellationToken).ConfigureAwait(false);
        return new JournalView(
            result.Items.Select(MapJournal).ToArray(),
            normalizedPage,
            normalizedPageSize,
            result.Total,
            result.TotalsByType);
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

    /// <summary>
    /// Contextual help is a presentation overlay, never a move. It reads the
    /// profile and the published scenario content, and writes nothing: no session
    /// state, no turn, no journal entry, no wallet mutation and no hash input. The
    /// method never calls <see cref="IPlayerExperienceRepository.SaveChangesAsync"/>.
    /// <para>
    /// Resolution order: the known-path warning required by the policy, then the
    /// AI provider, then the client override, then the author help carried by the
    /// scenario version, and finally the built-in offline rules. Every step above
    /// the last degrades silently into the next one.
    /// </para>
    /// </summary>
    public async Task<ContextualHelpView> GetContextualHelpAsync(string userId, string frontId, ContextualHelpRequest request, CancellationToken cancellationToken)
    {
        PlayerExperienceCatalog catalog = await catalogs.GetAsync(frontId, cancellationToken).ConfigureAwait(false);
        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        FamiliarOption? definition = catalog.Familiars.FirstOrDefault(item => item.Id == profile.FamiliarId);
        definition ??= catalog.Familiars.Count > 0 ? catalog.Familiars[0] : null;
        string name = string.IsNullOrWhiteSpace(profile.FamiliarCustomName) ? definition?.Name ?? "Compagnon" : profile.FamiliarCustomName;
        string? avatar = definition?.AvatarUrl ?? definition?.PortraitUrl;

        ContextualHelpView Result(string source, string message, HelpModality modality, bool isFallback) =>
            new(source, message, isFallback, name, avatar, modality.ToString());

        // A disabled assistant, or a familiar asked to stay quiet, must not speak
        // on its own initiative. A help the player explicitly asked for is still
        // served: silence is about proactivity, not about refusing an answer.
        if (!catalog.Assistant.Enabled
            || (request.Proactive && (!profile.FamiliarProactive || profile.FamiliarInterventionFrequency <= 0)))
        {
            return Result(HelpSources.Suppressed, string.Empty, HelpModality.None, false);
        }

        // Re-reading a branch does not make the author's hint useless — in a
        // teaching context, replay is an expected use. The warning is therefore
        // prepended to whatever help resolves below, and only stands alone when
        // nothing else does. The AI path is deliberately excluded: its context
        // already carries AlreadyExplored, so prepending would say it twice.
        string? knownPathWarning = request.AlreadyExplored && catalog.Assistant.WarnOnKnownPath
            ? "Vous avez déjà emprunté ce chemin. Vous pouvez le reprendre, ou tenter une option encore inconnue."
            : null;

        string Combine(string message) =>
            knownPathWarning is null ? message : $"{knownPathWarning} {message}";

        int helpLevel = profile.FamiliarId is null ? catalog.Assistant.DefaultFrequency : profile.FamiliarHelpLevel;
        HelpModality preferred = HelpModalityPolicy.ForHelpLevel(helpLevel);

        // Server-side resolution from the published content. Authoring being down
        // must never fail a help request, so the provider yields null instead.
        ScenarioHelpSnapshot? snapshot = null;
        if (request.ScenarioVersionId is Guid versionId)
        {
            snapshot = await scenarioHelp.GetAsync(versionId, request.NodeId, request.ChoiceId, cancellationToken).ConfigureAwait(false);
        }

        // Choice help is more specific than step help, so it wins when the request
        // names a choice and the author wrote something for it.
        (HelpModality Modality, string Text)? authored =
            HelpModalityPolicy.Resolve(snapshot?.ChoiceHelp, preferred)
            ?? HelpModalityPolicy.Resolve(snapshot?.NodeHelp, preferred);

        if (aiClient.IsConfigured && catalog.Assistant.Enabled)
        {
            AssistantAiContext context = new(
                frontId,
                request.Context,
                authored?.Modality ?? preferred,
                helpLevel,
                snapshot?.Title ?? string.Empty,
                snapshot?.NodeId,
                snapshot?.NodeText,
                snapshot?.VisibleChoiceTexts ?? [],
                authored?.Text ?? request.AuthorHint,
                request.AlreadyExplored,
                name,
                profile.FamiliarTone,
                profile.FamiliarWritingStyle);

            // Invariant: AI is optional. A provider that is unreachable, erroring,
            // timing out or simply slow must degrade to the offline path, never
            // surface to the player. The port is documented to return null rather
            // than throw, but the guarantee is enforced here rather than trusted.
            string? generated = null;
            try
            {
                generated = await aiClient.GenerateAsync(context, cancellationToken).ConfigureAwait(false);
            }
            // A provider timeout surfaces as OperationCanceledException too, so the
            // caller's own token is what separates the two: if the player did not
            // cancel, the cancellation came from the provider and must degrade.
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                generated = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                generated = null;
            }

            if (!string.IsNullOrWhiteSpace(generated))
            {
                return Result(HelpSources.Ai, generated, context.Modality, false);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AuthorHint))
        {
            return Result(HelpSources.AuthorHint, Combine(request.AuthorHint), preferred, false);
        }

        if (authored is { } resolved)
        {
            return Result(HelpSources.ScenarioHelp, Combine(resolved.Text), resolved.Modality, false);
        }

        if (knownPathWarning is not null)
        {
            return Result(HelpSources.KnownPathWarning, knownPathWarning, HelpModality.KnownPathWarning, false);
        }

        string generic = request.Context switch
        {
            "map" => "Choisissez une catégorie pour découvrir les scénarios et leur progression.",
            "completion" => "Votre arbre conserve cette découverte. Rejouez pour révéler les branches encore inconnues.",
            _ => "Relisez votre objectif et observez ce qui a changé depuis votre dernier choix.",
        };
        return Result(HelpSources.OfflineRule, generic, HelpModality.Objective, true);
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

    /// <summary>Progression of one scenario, folded over every published version played.</summary>
    private readonly record struct ScenarioProgress(int MasteryPercent, bool Completed);

    /// <summary>
    /// Progression per scenario. A scenario can be played across several published
    /// versions and <see cref="ScenarioMastery"/> is keyed by version, so the two criteria
    /// are folded independently: the percentage is the best reached on any version, and
    /// the scenario counts as completed as soon as <em>any</em> version reached an ending.
    /// Reading completion off the highest-percentage version alone would un-complete a
    /// scenario the moment the player explores a newer version without finishing it —
    /// curiosity would lock the journeys downstream, which is the opposite of the intent.
    /// </summary>
    private static Dictionary<Guid, ScenarioProgress> ProgressByScenario(PlayerProfile profile)
    {
        Dictionary<Guid, ScenarioProgress> progress = [];
        foreach (ScenarioMastery mastery in profile.ScenarioMasteries)
        {
            bool completed = mastery.EndingIds.Count > 0;
            progress[mastery.ScenarioId] = progress.TryGetValue(mastery.ScenarioId, out ScenarioProgress current)
                ? new ScenarioProgress(Math.Max(current.MasteryPercent, mastery.MasteryPercent), current.Completed || completed)
                : new ScenarioProgress(mastery.MasteryPercent, completed);
        }

        return progress;
    }

    /// <summary>
    /// Aggregates <see cref="ScenarioMastery"/> per journey and per category. Mastery is
    /// never recomputed here: it is the source of truth and is only summed.
    /// A scenario counts as started as soon as a mastery exists for it, and as completed
    /// once at least one ending was reached on any of its versions. The percentage is the mean mastery over every
    /// scenario of the scope, scenarios never played counting as zero, so a scope that
    /// carries no scenario yet reports zero instead of a misleading hundred.
    /// </summary>
    private static JourneyProgressView[] BuildJourneys(PlayerProfile profile, PlayerExperienceCatalog catalog)
    {
        IReadOnlyList<JourneyCatalogEntry> journeys = catalog.Journeys ?? [];
        Dictionary<Guid, CategoryCatalogEntry> categories = (catalog.Categories ?? []).ToDictionary(static category => category.Id);
        Dictionary<Guid, ScenarioProgress> progress = ProgressByScenario(profile);

        Dictionary<Guid, CategoryProgressView> categoryProgress = categories.Values.ToDictionary(
            static category => category.Id,
            category =>
            {
                (int started, int completed, int percent) = Aggregate(category.ScenarioIds, progress);
                return new CategoryProgressView(category.Id, category.Name, category.Accent, category.Order, category.IsVisible, category.ImageUrl, category.ScenarioIds.Count, started, completed, percent);
            });

        // An empty scope counts as completed, so it never gates anything. A journey ends up
        // with no scenario in ways nobody can undo from the outside — a milestone published
        // without content, emptied categories, or categoryIds pointing at categories deleted
        // after publication, which JourneyScenarioIds silently filters out. Requiring it to
        // be completed would lock every downstream journey permanently, for every player,
        // with no action able to recover: exactly the dead end ValidateJourneyPrerequisiteGraph
        // exists to prevent. Failing open is recoverable — an operator adds the scenarios and
        // the gate starts working; failing closed is not. Note the deliberate asymmetry with
        // the percentage, which stays at zero: completion measures outstanding requirements,
        // the percentage measures work done, and showing 100 % on an empty journey would lie.
        Dictionary<Guid, (string Name, bool Completed)> completion = journeys.ToDictionary(
            static journey => journey.Id,
            journey =>
            {
                Guid[] scenarioIds = JourneyScenarioIds(journey, categories);
                (int _, int completed, int _) = Aggregate(scenarioIds, progress);
                return (journey.Name, completed == scenarioIds.Length);
            });

        return journeys.OrderBy(static journey => journey.Order).Select(journey =>
        {
            Guid[] scenarioIds = JourneyScenarioIds(journey, categories);
            (int started, int completed, int percent) = Aggregate(scenarioIds, progress);
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

    private static (int Started, int Completed, int Percent) Aggregate(IReadOnlyList<Guid> scenarioIds, Dictionary<Guid, ScenarioProgress> progress)
    {
        if (scenarioIds.Count == 0) return (0, 0, 0);
        int started = 0;
        int completed = 0;
        int total = 0;
        foreach (Guid scenarioId in scenarioIds)
        {
            if (!progress.TryGetValue(scenarioId, out ScenarioProgress scenario)) continue;
            started++;
            if (scenario.Completed) completed++;
            total += scenario.MasteryPercent;
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