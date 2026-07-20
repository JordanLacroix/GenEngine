using System.Text.Json;

namespace GenEngine.PlayerExperience.Domain;

public sealed class PlayerProfile
{
    private readonly List<WalletEntry> walletEntries = [];
    private readonly List<OwnedItem> ownedItems = [];
    private readonly List<OnboardingState> onboardingStates = [];
    private readonly List<PlayerJournalEntry> journalEntries = [];
    private readonly List<ScenarioMastery> scenarioMasteries = [];
    private readonly List<PlayerStatValue> statValues = [];

    private PlayerProfile() { }

    private PlayerProfile(Guid id, string userId, string frontId, int initialBalance, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        FrontId = frontId;
        Balance = initialBalance;
        Revision = 1;
        CreatedAt = now;
        UpdatedAt = now;
        if (initialBalance > 0)
        {
            walletEntries.Add(WalletEntry.Create(id, "initial-balance", initialBalance, "Initial balance", initialBalance, now));
        }
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string FrontId { get; private set; } = string.Empty;
    public Guid? FamiliarId { get; private set; }
    public string FamiliarForm { get; private set; } = string.Empty;
    public string FamiliarTone { get; private set; } = string.Empty;
    public string FamiliarWritingStyle { get; private set; } = string.Empty;
    public string FamiliarAccent { get; private set; } = string.Empty;
    public int FamiliarHelpLevel { get; private set; }
    public string FamiliarCustomName { get; private set; } = string.Empty;
    public int FamiliarInterventionFrequency { get; private set; } = 2;
    public bool FamiliarProactive { get; private set; } = true;

    /// <summary>
    /// Every personalisation axis the player has chosen, keyed by stable axis key.
    /// </summary>
    /// <remarks>
    /// Held as one JSON document rather than one column per axis so adding an axis to
    /// the catalogue never requires a schema migration. The four historical columns
    /// above are kept and mirrored from this map: a profile written before the axes
    /// existed still reads correctly, and a client that only knows form/tone/style/accent
    /// keeps working.
    /// </remarks>
    public string FamiliarAxisSelectionsJson { get; private set; } = "{}";

    /// <summary>The finale the player has crossed, and when. Never cleared: crossing it is a memory, not a state machine.</summary>
    public Guid? FinaleId { get; private set; }
    public DateTimeOffset? FinaleReachedAt { get; private set; }

    public IReadOnlyDictionary<string, string> FamiliarAxisSelections =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(FamiliarAxisSelectionsJson) ?? [];

    /// <summary>
    /// Journey the player browses by default. Nullable on purpose: a profile created
    /// before journeys existed, or a front publishing none, stays fully playable and
    /// simply has no default. Validity against the published catalog is enforced by the
    /// application layer, which is the only one that can read it.
    /// </summary>
    public Guid? DefaultJourneyId { get; private set; }
    public int Balance { get; private set; }
    public int Revision { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<WalletEntry> WalletEntries => walletEntries;
    public IReadOnlyList<OwnedItem> OwnedItems => ownedItems;
    public IReadOnlyList<OnboardingState> OnboardingStates => onboardingStates;
    public IReadOnlyList<PlayerJournalEntry> JournalEntries => journalEntries;
    public IReadOnlyList<ScenarioMastery> ScenarioMasteries => scenarioMasteries;

    /// <summary>
    /// Configurable statistics the player has accumulated, keyed by the configuration
    /// slug. A stat the player has never been granted simply has no row: absence
    /// <em>is</em> zero, which is why nothing has to be seeded when the catalogue grows.
    /// </summary>
    public IReadOnlyList<PlayerStatValue> StatValues => statValues;

    public static PlayerProfile Create(string userId, string frontId, int initialBalance, DateTimeOffset now) =>
        new(Guid.NewGuid(), userId, frontId, initialBalance, now);

    /// <summary>
    /// Applies a familiar personalisation. <paramref name="axisSelections"/> is the
    /// authoritative map; the four legacy columns are mirrored from it so nothing that
    /// reads them has to change.
    /// </summary>
    public void ConfigureFamiliar(
        Guid familiarId,
        IReadOnlyDictionary<string, string> axisSelections,
        int helpLevel,
        string? customName,
        int interventionFrequency,
        bool proactive,
        int expectedRevision,
        DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        if (helpLevel is < 0 or > 5)
        {
            throw new PlayerExperienceDomainException("invalid_help_level", "Help level must be between 0 and 5.");
        }

        if (interventionFrequency is < 0 or > 5)
        {
            throw new PlayerExperienceDomainException("invalid_intervention_frequency", "Intervention frequency must be between 0 and 5.");
        }

        string trimmedName = customName?.Trim() ?? string.Empty;
        if (trimmedName.Length > 80 || trimmedName.Any(IsUnsafeNameCharacter))
        {
            throw new PlayerExperienceDomainException("invalid_custom_name", "The familiar name must be at most 80 printable characters without markup.");
        }

        Dictionary<string, string> selections = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> selection in axisSelections)
        {
            selections[selection.Key] = selection.Value.Trim();
        }

        FamiliarId = familiarId;
        FamiliarAxisSelectionsJson = JsonSerializer.Serialize(selections.OrderBy(static item => item.Key, StringComparer.Ordinal).ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal));
        FamiliarForm = Selected(selections, "form");
        FamiliarTone = Selected(selections, "tone");
        FamiliarWritingStyle = Selected(selections, "writingStyle");
        FamiliarAccent = Selected(selections, "accent");
        FamiliarHelpLevel = helpLevel;
        FamiliarCustomName = trimmedName;
        FamiliarInterventionFrequency = interventionFrequency;
        FamiliarProactive = proactive;
        Touch(now);
    }

    /// <summary>
    /// Records that the player crossed the finale. Idempotent, and deliberately
    /// one-way: nothing in this aggregate reads the stamp to deny an action, so the
    /// player keeps playing exactly as before.
    /// </summary>
    public bool MarkFinaleReached(Guid finaleId, DateTimeOffset now)
    {
        if (FinaleId == finaleId && FinaleReachedAt is not null) return false;
        FinaleId = finaleId;
        FinaleReachedAt = now;
        Touch(now);
        return true;
    }

    private static string Selected(Dictionary<string, string> selections, string axis) =>
        selections.TryGetValue(axis, out string? value) ? value : string.Empty;

    /// <summary>
    /// The custom name is free text, so it must not be able to carry active content
    /// into a client that renders it. Angle brackets and ampersands are refused
    /// outright rather than escaped, because escaping is the renderer's job and we
    /// cannot audit every renderer.
    /// </summary>
    private static bool IsUnsafeNameCharacter(char character) =>
        char.IsControl(character) || character is '<' or '>' or '&';

    /// <summary>
    /// Selects the journey the player browses by default, or clears it when
    /// <paramref name="journeyId"/> is null. Whether the journey exists, is visible and
    /// has its prerequisites satisfied is decided against the published configuration
    /// before this call.
    /// </summary>
    public void SelectDefaultJourney(Guid? journeyId, int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        DefaultJourneyId = journeyId;
        Touch(now);
    }

    public OnboardingState GetOrStartOnboarding(Guid tutorialId, int version, DateTimeOffset now)
    {
        OnboardingState? state = onboardingStates.SingleOrDefault(item => item.TutorialId == tutorialId && item.Version == version);
        if (state is not null) return state;
        state = OnboardingState.Start(Id, tutorialId, version, now);
        onboardingStates.Add(state);
        Touch(now);
        return state;
    }

    public void CompleteOnboardingStep(Guid tutorialId, int version, Guid stepId, string idempotencyKey, IReadOnlyCollection<Guid> requiredStepIds, DateTimeOffset now)
    {
        OnboardingState state = GetOrStartOnboarding(tutorialId, version, now);
        if (!state.CompleteStep(stepId, idempotencyKey, requiredStepIds, now)) return;
        Touch(now);
    }

    public void SkipOnboarding(Guid tutorialId, int version, string idempotencyKey, DateTimeOffset now)
    {
        OnboardingState state = GetOrStartOnboarding(tutorialId, version, now);
        if (!state.Skip(idempotencyKey, now)) return;
        Touch(now);
    }

    public void ResetOnboarding(Guid tutorialId, int version, DateTimeOffset now)
    {
        OnboardingState state = GetOrStartOnboarding(tutorialId, version, now);
        state.Reset(now);
        Touch(now);
    }

    public bool RecordJournalEntry(
        string idempotencyKey,
        string type,
        string title,
        string summary,
        Guid? journeyId,
        Guid? categoryId,
        Guid? scenarioId,
        Guid? scenarioVersionId,
        Guid? sessionId,
        string? referenceId,
        DateTimeOffset occurredAt,
        DateTimeOffset now)
    {
        if (journalEntries.Any(item => item.IdempotencyKey == idempotencyKey)) return false;
        journalEntries.Add(PlayerJournalEntry.Create(Id, idempotencyKey, type, title, summary, journeyId, categoryId, scenarioId, scenarioVersionId, sessionId, referenceId, occurredAt));
        Touch(now);
        return true;
    }

    public void RecordExploration(Guid scenarioId, Guid scenarioVersionId, Guid sessionId, string choiceId, string targetNodeId, bool completed, string? endingId, int totalObjectives, string idempotencyKey, DateTimeOffset now)
    {
        ScenarioMastery? mastery = scenarioMasteries.SingleOrDefault(item => item.ScenarioVersionId == scenarioVersionId);
        if (mastery is null)
        {
            mastery = ScenarioMastery.Create(Id, scenarioId, scenarioVersionId, totalObjectives, now);
            scenarioMasteries.Add(mastery);
        }

        if (!mastery.Record(sessionId, choiceId, targetNodeId, completed, endingId, totalObjectives, idempotencyKey, now)) return;
        Touch(now);
    }

    /// <summary>
    /// Grants <paramref name="amount"/> points of the statistic <paramref name="key"/>,
    /// saturating at <paramref name="maximum"/>.
    /// </summary>
    /// <remarks>
    /// Two decisions are load-bearing. A stat the player has never earned starts at
    /// <em>zero</em>: the row is created on the first grant, so a statistic added to the
    /// catalogue later needs no backfill. And a grant that would exceed the ceiling
    /// <em>saturates</em> instead of failing — the scenario author who wrote the effect
    /// cannot know the player's current value, so making the grant conditional on it
    /// would make the same effect succeed or fail depending on the order in which the
    /// player happened to play the scenarios.
    /// </remarks>
    /// <returns><c>false</c> when the grant was already applied under this key.</returns>
    public bool GrantStat(string key, int amount, int maximum, string idempotencyKey, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new PlayerExperienceDomainException("invalid_amount", "A statistic grant must be positive.");
        }

        if (maximum <= 0)
        {
            throw new PlayerExperienceDomainException("invalid_maximum", "A statistic ceiling must be positive.");
        }

        PlayerStatValue? stat = statValues.SingleOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (stat is null)
        {
            stat = PlayerStatValue.Create(Id, key, now);
            statValues.Add(stat);
        }

        if (!stat.Grant(amount, maximum, idempotencyKey, now)) return false;
        Touch(now);
        return true;
    }

    public bool Credit(string idempotencyKey, int amount, string reason, DateTimeOffset now)
    {
        if (walletEntries.Any(entry => entry.IdempotencyKey == idempotencyKey))
        {
            return false;
        }

        if (amount <= 0)
        {
            throw new PlayerExperienceDomainException("invalid_amount", "A credit must be positive.");
        }

        Balance = checked(Balance + amount);
        walletEntries.Add(WalletEntry.Create(Id, idempotencyKey, amount, reason, Balance, now));
        Touch(now);
        return true;
    }

    public void Purchase(Guid offerId, int price, string idempotencyKey, DateTimeOffset now)
    {
        if (walletEntries.Any(entry => entry.IdempotencyKey == idempotencyKey))
        {
            return;
        }

        if (price < 0 || Balance < price)
        {
            throw new PlayerExperienceDomainException("insufficient_balance", "The wallet balance is insufficient.");
        }

        if (ownedItems.Any(item => item.OfferId == offerId))
        {
            throw new PlayerExperienceDomainException("offer_already_owned", "This offer is already owned.");
        }

        Balance -= price;
        walletEntries.Add(WalletEntry.Create(Id, idempotencyKey, -price, "Shop purchase", Balance, now));
        ownedItems.Add(OwnedItem.Create(Id, offerId, now));
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        Revision = checked(Revision + 1);
        UpdatedAt = now;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (Revision != expectedRevision)
        {
            throw new PlayerExperienceDomainException("revision_conflict", "The player profile was modified concurrently.");
        }
    }
}

public enum OnboardingStatus { NotStarted, InProgress, Completed, Skipped }

public sealed class OnboardingState
{
    private OnboardingState() { }
    private OnboardingState(Guid profileId, Guid tutorialId, int version, DateTimeOffset now)
    {
        ProfileId = profileId; TutorialId = tutorialId; Version = version; Status = OnboardingStatus.InProgress;
        StartedAt = now; LastActivityAt = now; Revision = 1;
    }
    public Guid ProfileId { get; private set; }
    public Guid TutorialId { get; private set; }
    public int Version { get; private set; }
    public OnboardingStatus Status { get; private set; }
    public string CompletedStepIdsJson { get; private set; } = "[]";
    public string ProcessedCommandIdsJson { get; private set; } = "[]";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? SkippedAt { get; private set; }
    public int Revision { get; private set; }
    public IReadOnlyList<Guid> CompletedStepIds => DeserializeGuids(CompletedStepIdsJson);

    internal static OnboardingState Start(Guid profileId, Guid tutorialId, int version, DateTimeOffset now) => new(profileId, tutorialId, version, now);
    internal bool CompleteStep(Guid stepId, string commandId, IReadOnlyCollection<Guid> required, DateTimeOffset now)
    {
        HashSet<Guid> commands = DeserializeGuids(ProcessedCommandIdsJson).ToHashSet();
        if (!Guid.TryParse(commandId, out Guid parsedCommand) || !commands.Add(parsedCommand)) return false;
        HashSet<Guid> completed = CompletedStepIds.ToHashSet();
        _ = completed.Add(stepId);
        CompletedStepIdsJson = JsonSerializer.Serialize(completed.Order());
        ProcessedCommandIdsJson = JsonSerializer.Serialize(commands.Order());
        Status = required.All(completed.Contains) ? OnboardingStatus.Completed : OnboardingStatus.InProgress;
        CompletedAt = Status == OnboardingStatus.Completed ? now : null;
        SkippedAt = null; LastActivityAt = now; Revision++;
        return true;
    }
    internal bool Skip(string commandId, DateTimeOffset now)
    {
        HashSet<Guid> commands = DeserializeGuids(ProcessedCommandIdsJson).ToHashSet();
        if (!Guid.TryParse(commandId, out Guid parsedCommand) || !commands.Add(parsedCommand)) return false;
        ProcessedCommandIdsJson = JsonSerializer.Serialize(commands.Order());
        Status = OnboardingStatus.Skipped; SkippedAt = now; CompletedAt = null; LastActivityAt = now; Revision++;
        return true;
    }
    internal void Reset(DateTimeOffset now)
    {
        Status = OnboardingStatus.InProgress; CompletedStepIdsJson = "[]"; ProcessedCommandIdsJson = "[]";
        StartedAt = now; LastActivityAt = now; CompletedAt = null; SkippedAt = null; Revision++;
    }
    private static Guid[] DeserializeGuids(string json) => JsonSerializer.Deserialize<Guid[]>(json) ?? [];
}

public sealed class PlayerJournalEntry
{
    private PlayerJournalEntry() { }
    private PlayerJournalEntry(Guid profileId, string key, string type, string title, string summary, Guid? journeyId, Guid? categoryId, Guid? scenarioId, Guid? scenarioVersionId, Guid? sessionId, string? referenceId, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid(); ProfileId = profileId; IdempotencyKey = key; Type = type; Title = title; Summary = summary;
        JourneyId = journeyId; CategoryId = categoryId; ScenarioId = scenarioId; ScenarioVersionId = scenarioVersionId; SessionId = sessionId;
        ReferenceId = referenceId; OccurredAt = occurredAt;
    }
    public Guid Id { get; private set; }
    public Guid ProfileId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public Guid? JourneyId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? ScenarioId { get; private set; }
    public Guid? ScenarioVersionId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string? ReferenceId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    internal static PlayerJournalEntry Create(Guid profileId, string key, string type, string title, string summary, Guid? journeyId, Guid? categoryId, Guid? scenarioId, Guid? scenarioVersionId, Guid? sessionId, string? referenceId, DateTimeOffset occurredAt) =>
        new(profileId, key, type, title, summary, journeyId, categoryId, scenarioId, scenarioVersionId, sessionId, referenceId, occurredAt);
}

public sealed class ScenarioMastery
{
    private ScenarioMastery() { }
    private ScenarioMastery(Guid profileId, Guid scenarioId, Guid versionId, int total, DateTimeOffset now)
    { ProfileId = profileId; ScenarioId = scenarioId; ScenarioVersionId = versionId; TotalObjectives = Math.Max(1, total); UpdatedAt = now; }
    public Guid ProfileId { get; private set; }
    public Guid ScenarioId { get; private set; }
    public Guid ScenarioVersionId { get; private set; }
    public string ChoiceIdsJson { get; private set; } = "[]";
    public string NodeIdsJson { get; private set; } = "[]";
    public string EndingIdsJson { get; private set; } = "[]";
    public string SessionIdsJson { get; private set; } = "[]";
    public string ProcessedCommandIdsJson { get; private set; } = "[]";
    public int TotalObjectives { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<string> ChoiceIds => DeserializeStrings(ChoiceIdsJson);
    public IReadOnlyList<string> NodeIds => DeserializeStrings(NodeIdsJson);
    public IReadOnlyList<string> EndingIds => DeserializeStrings(EndingIdsJson);
    public int DiscoveredObjectives => ChoiceIds.Count + EndingIds.Count;
    public int MasteryPercent => Math.Min(100, (int)Math.Round(DiscoveredObjectives * 100d / Math.Max(1, TotalObjectives)));
    internal static ScenarioMastery Create(Guid profileId, Guid scenarioId, Guid versionId, int total, DateTimeOffset now) => new(profileId, scenarioId, versionId, total, now);
    internal bool Record(Guid sessionId, string choiceId, string targetNodeId, bool completed, string? endingId, int total, string key, DateTimeOffset now)
    {
        HashSet<string> commands = DeserializeStrings(ProcessedCommandIdsJson).ToHashSet(StringComparer.Ordinal);
        if (!commands.Add(key)) return false;
        HashSet<string> choices = ChoiceIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> nodes = DeserializeStrings(NodeIdsJson).ToHashSet(StringComparer.Ordinal);
        HashSet<string> endings = DeserializeStrings(EndingIdsJson).ToHashSet(StringComparer.Ordinal);
        HashSet<string> sessions = DeserializeStrings(SessionIdsJson).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(choiceId)) _ = choices.Add(choiceId);
        if (!string.IsNullOrWhiteSpace(targetNodeId)) _ = nodes.Add(targetNodeId);
        if (completed && !string.IsNullOrWhiteSpace(endingId)) _ = endings.Add(endingId);
        _ = sessions.Add(sessionId.ToString());
        ChoiceIdsJson = JsonSerializer.Serialize(choices.Order()); NodeIdsJson = JsonSerializer.Serialize(nodes.Order());
        EndingIdsJson = JsonSerializer.Serialize(endings.Order()); SessionIdsJson = JsonSerializer.Serialize(sessions.Order());
        ProcessedCommandIdsJson = JsonSerializer.Serialize(commands.Order()); TotalObjectives = Math.Max(TotalObjectives, Math.Max(1, total)); UpdatedAt = now;
        return true;
    }
    private static string[] DeserializeStrings(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}

/// <summary>
/// The value of one configurable statistic for one player. It persists across sessions
/// and scenarios, which is precisely what <c>WorldState.Variables</c> cannot do.
/// </summary>
public sealed class PlayerStatValue
{
    private PlayerStatValue() { }

    private PlayerStatValue(Guid profileId, string key, DateTimeOffset now)
    {
        ProfileId = profileId;
        Key = key;
        Value = 0;
        UpdatedAt = now;
    }

    public Guid ProfileId { get; private set; }
    public string Key { get; private set; } = string.Empty;

    /// <summary>Points accumulated. Always starts at zero and never decreases.</summary>
    public int Value { get; private set; }

    /// <summary>Grants already applied, so a retried command never counts twice.</summary>
    public string ProcessedCommandIdsJson { get; private set; } = "[]";
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static PlayerStatValue Create(Guid profileId, string key, DateTimeOffset now) => new(profileId, key, now);

    internal bool Grant(int amount, int maximum, string idempotencyKey, DateTimeOffset now)
    {
        HashSet<string> commands = (JsonSerializer.Deserialize<string[]>(ProcessedCommandIdsJson) ?? []).ToHashSet(StringComparer.Ordinal);
        if (!commands.Add(idempotencyKey)) return false;

        // Saturation, not rejection: the ceiling is a display bound, not a precondition
        // the scenario had any way of checking.
        Value = Math.Min(maximum, checked(Value + amount));
        ProcessedCommandIdsJson = JsonSerializer.Serialize(commands.Order(StringComparer.Ordinal));
        UpdatedAt = now;
        return true;
    }
}

public sealed class WalletEntry
{
    private WalletEntry() { }
    private WalletEntry(Guid id, Guid profileId, string key, int amount, string reason, int balanceAfter, DateTimeOffset createdAt)
    { Id = id; ProfileId = profileId; IdempotencyKey = key; Amount = amount; Reason = reason; BalanceAfter = balanceAfter; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid ProfileId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public int Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int BalanceAfter { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    internal static WalletEntry Create(Guid profileId, string key, int amount, string reason, int balanceAfter, DateTimeOffset now) =>
        new(Guid.NewGuid(), profileId, key, amount, reason, balanceAfter, now);
}

public sealed class OwnedItem
{
    private OwnedItem() { }
    private OwnedItem(Guid profileId, Guid offerId, DateTimeOffset acquiredAt) { ProfileId = profileId; OfferId = offerId; AcquiredAt = acquiredAt; }
    public Guid ProfileId { get; private set; }
    public Guid OfferId { get; private set; }
    public DateTimeOffset AcquiredAt { get; private set; }
    internal static OwnedItem Create(Guid profileId, Guid offerId, DateTimeOffset now) => new(profileId, offerId, now);
}

public sealed class PlayerExperienceDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}