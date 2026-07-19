using GenEngine.PlayerExperience.Domain;

namespace GenEngine.PlayerExperience.Application;

/// <summary>One previewable value of a familiar personalisation axis, as published.</summary>
public sealed record FamiliarAxisOption(string Value, string Label, string Description, string? AccentToken, string? AssetReference, int Order);

/// <summary>A closed personalisation axis. No axis accepts free text: everything a player picks is catalogued and therefore previewable.</summary>
public sealed record FamiliarAxis(string Axis, string Label, string Description, string DefaultValue, IReadOnlyList<FamiliarAxisOption> Options);

public sealed record FamiliarOption(Guid Id, string Name, string Description, string Form, string Tone, string WritingStyle, string Accent, int HelpLevel, IReadOnlyList<string> Capabilities, IReadOnlyList<string> AvailableForms, IReadOnlyList<string> AvailableTones, string? PortraitUrl, string? AvatarUrl, string? BackgroundUrl, string? License, string? Attribution, IReadOnlyList<FamiliarAxis>? Axes = null);
public sealed record RewardRule(string Trigger, string ReferenceId, int Amount, string Description);
public sealed record ShopOffer(Guid Id, string Name, string Description, int Price, string RewardType, string RewardReference, bool Enabled);
public sealed record OnboardingStep(Guid Id, string Title, string Body, string Target, string Action, int Order, bool Required);
public sealed record OnboardingTutorial(Guid Id, int Version, bool Enabled, bool AllowSkip, bool RequiredAfterUpgrade, IReadOnlyList<OnboardingStep> Steps);
public sealed record AssistantPolicy(bool Enabled, bool RequireFirstRunConfiguration, bool Proactive, bool WarnOnKnownPath, int DefaultFrequency, IReadOnlyList<string> OfflineCapabilities);
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
    FinalePlan? Finale = null,
    IReadOnlyList<CategoryPlan>? Categories = null,
    IReadOnlyList<JourneyPlan>? Journeys = null);

/// <summary>
/// A familiar personalisation submitted by a player.
/// </summary>
/// <remarks>
/// <paramref name="Axes"/> is the current form: a map of axis key to catalogued value.
/// The four positional fields are the historical shape and remain accepted, so a client
/// that has not been updated keeps configuring its familiar. When both are supplied,
/// <paramref name="Axes"/> wins for the keys it carries.
/// </remarks>
public sealed record FamiliarSelection(Guid FamiliarId, string Form, string Tone, string WritingStyle, string Accent, int HelpLevel, string? CustomName = null, int InterventionFrequency = 2, bool Proactive = true, IReadOnlyDictionary<string, string>? Axes = null);
public sealed record WalletEntryView(Guid Id, int Amount, string Reason, int BalanceAfter, DateTimeOffset CreatedAt);
public sealed record OnboardingStateView(Guid TutorialId, int Version, string Status, IReadOnlyList<Guid> CompletedStepIds, DateTimeOffset? CompletedAt, DateTimeOffset? SkippedAt, int Revision);
public sealed record JournalEntryView(Guid Id, string Type, string Title, string Summary, Guid? JourneyId, Guid? CategoryId, Guid? ScenarioId, Guid? ScenarioVersionId, Guid? SessionId, string? ReferenceId, DateTimeOffset OccurredAt);
public sealed record ScenarioMasteryView(Guid ScenarioId, Guid ScenarioVersionId, IReadOnlyList<string> ChoiceIds, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EndingIds, int DiscoveredObjectives, int TotalObjectives, int MasteryPercent, DateTimeOffset UpdatedAt);
public sealed record PlayerExperienceView(Guid Id, string FrontId, int Revision, int Balance, string CurrencyCode, string CurrencyName, string CurrencyIcon, FamiliarSelection? Familiar, FamiliarOption? FamiliarDefinition, OnboardingStateView Onboarding, IReadOnlyList<ScenarioMasteryView> Masteries, IReadOnlyList<Guid> OwnedOfferIds, IReadOnlyList<WalletEntryView> RecentEntries, IReadOnlyList<JournalEntryView> RecentJournal, FinaleView? Finale = null);
public sealed record PlayerBootstrapView(string NextAction, PlayerExperienceView Experience, OnboardingTutorial Tutorial, AssistantPolicy Assistant);
public sealed record JournalView(IReadOnlyList<JournalEntryView> Items, int Total, IReadOnlyDictionary<string, int> TotalsByType);
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
        IReadOnlyDictionary<string, string> axisSelections = ResolveAxisSelections(familiar, selection);

        PlayerProfile profile = await GetOrCreateAsync(userId, frontId, catalog, cancellationToken).ConfigureAwait(false);
        profile.ConfigureFamiliar(selection.FamiliarId, axisSelections, selection.HelpLevel, selection.CustomName, selection.InterventionFrequency, selection.Proactive, expectedRevision, timeProvider.GetUtcNow());
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

        EvaluateFinale(profile, catalog, now);
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

    /// <summary>
    /// Resolves the axis values a player submitted against the published catalogue.
    /// </summary>
    /// <remarks>
    /// Every value must belong to its axis: an out-of-catalogue value is refused rather
    /// than stored, which is precisely what <c>writingStyle</c> and <c>accent</c> never
    /// enforced. An axis the player did not answer falls back to the axis default, so a
    /// profile configured before the axis existed stays valid without any migration of
    /// its data. An axis key the catalogue does not declare is refused too, otherwise a
    /// typo would silently persist a preference nothing ever reads.
    /// </remarks>
    private static Dictionary<string, string> ResolveAxisSelections(FamiliarOption familiar, FamiliarSelection selection)
    {
        IReadOnlyList<FamiliarAxis> axes = familiar.Axes ?? [];
        if (axes.Count == 0)
        {
            // Defensive: a catalogue served by an engine older than the axes. Fall back
            // to the historical two-list check rather than accepting anything.
            if (!familiar.AvailableForms.Contains(selection.Form, StringComparer.OrdinalIgnoreCase)
                || !familiar.AvailableTones.Contains(selection.Tone, StringComparer.OrdinalIgnoreCase))
            {
                throw new PlayerExperienceException("invalid_familiar_configuration", "The selected form or tone is unavailable.");
            }

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["form"] = selection.Form,
                ["tone"] = selection.Tone,
                ["writingStyle"] = selection.WritingStyle,
                ["accent"] = selection.Accent,
            };
        }

        Dictionary<string, string> submitted = new(StringComparer.Ordinal);
        AddLegacy(submitted, "form", selection.Form);
        AddLegacy(submitted, "tone", selection.Tone);
        AddLegacy(submitted, "writingStyle", selection.WritingStyle);
        AddLegacy(submitted, "accent", selection.Accent);
        foreach (KeyValuePair<string, string> axis in selection.Axes ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            submitted[axis.Key.Trim()] = axis.Value;
        }

        HashSet<string> known = axes.Select(static axis => axis.Axis).ToHashSet(StringComparer.Ordinal);
        if (submitted.Keys.Any(key => !known.Contains(key)))
        {
            throw new PlayerExperienceException("unknown_familiar_axis", "The selection references an axis this familiar does not offer.");
        }

        Dictionary<string, string> resolved = new(StringComparer.Ordinal);
        foreach (FamiliarAxis axis in axes)
        {
            if (!submitted.TryGetValue(axis.Axis, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                resolved[axis.Axis] = axis.DefaultValue;
                continue;
            }

            FamiliarAxisOption option = axis.Options.FirstOrDefault(item => string.Equals(item.Value, value.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new PlayerExperienceException("invalid_familiar_configuration", $"The value selected for axis '{axis.Axis}' is unavailable.");
            resolved[axis.Axis] = option.Value;
        }

        return resolved;

        static void AddLegacy(Dictionary<string, string> target, string axis, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target[axis] = value;
        }
    }

    /// <summary>
    /// Evaluates the finale after progress was recorded and stamps it the first time it
    /// is satisfied. Nothing is locked afterwards — the stamp is only ever read to
    /// display the ending, never to deny a command.
    /// </summary>
    private static void EvaluateFinale(PlayerProfile profile, PlayerExperienceCatalog catalog, DateTimeOffset now)
    {
        if (catalog.Finale is not FinalePlan plan || !plan.Enabled || profile.FinaleReachedAt is not null) return;
        IReadOnlyList<FinaleConditionProgress> conditions = FinaleEvaluator.Evaluate(plan, BuildProgress(profile), catalog.Categories ?? [], catalog.Journeys ?? []);
        if (!FinaleEvaluator.IsSatisfied(plan, conditions)) return;
        if (profile.MarkFinaleReached(plan.Id, now))
        {
            _ = profile.RecordJournalEntry($"finale:{plan.Id}", "FinaleReached", plan.Title, plan.Summary, null, null, null, null, null, plan.Id.ToString(), now, now);
        }
    }

    private static FinaleView? MapFinale(PlayerProfile profile, PlayerExperienceCatalog catalog)
    {
        if (catalog.Finale is not FinalePlan plan) return null;
        IReadOnlyList<FinaleConditionProgress> conditions = FinaleEvaluator.Evaluate(plan, BuildProgress(profile), catalog.Categories ?? [], catalog.Journeys ?? []);
        return new FinaleView(
            plan.Id,
            plan.Title,
            plan.Summary,
            plan.Body,
            profile.FinaleReachedAt is not null && profile.FinaleId == plan.Id,
            profile.FinaleId == plan.Id ? profile.FinaleReachedAt : null,
            plan.Mode.ToString(),
            conditions,
            plan.VisualUrl,
            plan.MusicUrl,
            plan.LabelKey);
    }

    /// <summary>
    /// Projects the recorded mastery into the shape the evaluator needs. A scenario is
    /// completed as soon as one of its versions produced an ending: replaying it can
    /// only ever add endings, never take one back.
    /// </summary>
    private static FinaleEvaluator.ScenarioProgress[] BuildProgress(PlayerProfile profile) =>
        profile.ScenarioMasteries
            .GroupBy(static mastery => mastery.ScenarioId)
            .Select(static group =>
            {
                string[] endings = group
                    .SelectMany(static mastery => System.Text.Json.JsonSerializer.Deserialize<string[]>(mastery.EndingIdsJson) ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new FinaleEvaluator.ScenarioProgress(group.Key, endings.Length > 0, endings, group.Max(static mastery => mastery.MasteryPercent));
            })
            .ToArray();

    private async Task<PlayerProfile> GetOrCreateAsync(string userId, string frontId, PlayerExperienceCatalog catalog, CancellationToken cancellationToken)
    {
        PlayerProfile? profile = await repository.GetAsync(userId, frontId, cancellationToken).ConfigureAwait(false);
        if (profile is not null) return profile;
        profile = PlayerProfile.Create(userId, frontId, catalog.InitialBalance, timeProvider.GetUtcNow());
        await repository.AddAsync(profile, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profile;
    }

    private static PlayerExperienceView Map(PlayerProfile profile, PlayerExperienceCatalog catalog)
    {
        FamiliarOption? familiarDefinition = profile.FamiliarId is Guid familiarId ? catalog.Familiars.FirstOrDefault(item => item.Id == familiarId) : null;
        return new(profile.Id, profile.FrontId, profile.Revision, profile.Balance, catalog.CurrencyCode, catalog.CurrencyName, catalog.CurrencyIcon,
            profile.FamiliarId is null ? null : new FamiliarSelection(profile.FamiliarId.Value, profile.FamiliarForm, profile.FamiliarTone, profile.FamiliarWritingStyle, profile.FamiliarAccent, profile.FamiliarHelpLevel, profile.FamiliarCustomName, profile.FamiliarInterventionFrequency, profile.FamiliarProactive, profile.FamiliarAxisSelections),
            familiarDefinition,
            MapOnboarding(profile, catalog.Onboarding),
            profile.ScenarioMasteries.OrderByDescending(static item => item.UpdatedAt).Select(MapMastery).ToArray(),
            profile.OwnedItems.Select(static item => item.OfferId).ToArray(),
            profile.WalletEntries.OrderByDescending(static entry => entry.CreatedAt).Take(20).Select(static entry => new WalletEntryView(entry.Id, entry.Amount, entry.Reason, entry.BalanceAfter, entry.CreatedAt)).ToArray(),
            profile.JournalEntries.OrderByDescending(static entry => entry.OccurredAt).Take(20).Select(MapJournal).ToArray(),
            MapFinale(profile, catalog));
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
        new(mastery.ScenarioId, mastery.ScenarioVersionId, mastery.ChoiceIds, System.Text.Json.JsonSerializer.Deserialize<string[]>(mastery.NodeIdsJson) ?? [], System.Text.Json.JsonSerializer.Deserialize<string[]>(mastery.EndingIdsJson) ?? [], mastery.DiscoveredObjectives, mastery.TotalObjectives, mastery.MasteryPercent, mastery.UpdatedAt);
}

public sealed class PlayerExperienceException : InvalidOperationException
{
    public PlayerExperienceException(string code, string message) : base(message) => Code = code;
    public PlayerExperienceException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;
    public string Code { get; }
}