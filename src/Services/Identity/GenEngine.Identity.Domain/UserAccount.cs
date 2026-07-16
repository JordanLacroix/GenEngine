namespace GenEngine.Identity.Domain;

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    private UserAccount(Guid id, string userName, string normalizedUserName, DateTimeOffset createdAt)
    {
        Id = id;
        UserName = userName;
        NormalizedUserName = normalizedUserName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string UserName { get; private set; } = string.Empty;

    public string NormalizedUserName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static UserAccount Create(string userName, DateTimeOffset createdAt)
    {
        string trimmed = userName.Trim();
        if (trimmed.Length is < 3 or > 80)
        {
            throw new IdentityDomainException("invalid_username", "Username length must be between 3 and 80 characters.");
        }

        return new UserAccount(Guid.NewGuid(), trimmed, trimmed.ToUpperInvariant(), createdAt);
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new IdentityDomainException("invalid_password_hash", "A password hash is required.");
        }

        PasswordHash = passwordHash;
    }
}

public sealed class IdentityDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}