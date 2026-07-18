namespace GenEngine.PlayerExperience.Domain;

public sealed class PlayerProfile
{
    private readonly List<WalletEntry> walletEntries = [];
    private readonly List<OwnedItem> ownedItems = [];

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
    public int Balance { get; private set; }
    public int Revision { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<WalletEntry> WalletEntries => walletEntries;
    public IReadOnlyList<OwnedItem> OwnedItems => ownedItems;

    public static PlayerProfile Create(string userId, string frontId, int initialBalance, DateTimeOffset now) =>
        new(Guid.NewGuid(), userId, frontId, initialBalance, now);

    public void ConfigureFamiliar(
        Guid familiarId,
        string form,
        string tone,
        string writingStyle,
        string accent,
        int helpLevel,
        int expectedRevision,
        DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        if (helpLevel is < 0 or > 5)
        {
            throw new PlayerExperienceDomainException("invalid_help_level", "Help level must be between 0 and 5.");
        }

        FamiliarId = familiarId;
        FamiliarForm = form.Trim();
        FamiliarTone = tone.Trim();
        FamiliarWritingStyle = writingStyle.Trim();
        FamiliarAccent = accent.Trim();
        FamiliarHelpLevel = helpLevel;
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