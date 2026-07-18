using System.Text.Json;

namespace GenEngine.PlayerExperience.Domain;

public sealed class PlayerProfile
{
    private readonly List<WalletEntry> walletEntries = [];
    private readonly List<OwnedItem> ownedItems = [];
    private readonly List<OnboardingState> onboardingStates = [];
    private readonly List<PlayerJournalEntry> journalEntries = [];
    private readonly List<ScenarioMastery> scenarioMasteries = [];

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
    public int Balance { get; private set; }
    public int Revision { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<WalletEntry> WalletEntries => walletEntries;
    public IReadOnlyList<OwnedItem> OwnedItems => ownedItems;
    public IReadOnlyList<OnboardingState> OnboardingStates => onboardingStates;
    public IReadOnlyList<PlayerJournalEntry> JournalEntries => journalEntries;
    public IReadOnlyList<ScenarioMastery> ScenarioMasteries => scenarioMasteries;

    public static PlayerProfile Create(string userId, string frontId, int initialBalance, DateTimeOffset now) =>
        new(Guid.NewGuid(), userId, frontId, initialBalance, now);

    public void ConfigureFamiliar(
        Guid familiarId,
        string form,
        string tone,
        string writingStyle,
        string accent,
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

        FamiliarId = familiarId;
        FamiliarForm = form.Trim();
        FamiliarTone = tone.Trim();
        FamiliarWritingStyle = writingStyle.Trim();
        FamiliarAccent = accent.Trim();
        FamiliarHelpLevel = helpLevel;
        FamiliarCustomName = customName?.Trim() ?? string.Empty;
        FamiliarInterventionFrequency = interventionFrequency;
        FamiliarProactive = proactive;
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
    public int DiscoveredObjectives => ChoiceIds.Count + DeserializeStrings(EndingIdsJson).Length;
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
