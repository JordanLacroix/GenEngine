using GenEngine.Identity.Domain;

namespace GenEngine.Identity.Application;

public interface IIdentityRepository
{
    Task<UserAccount?> FindByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(UserAccount account, string password);

    bool Verify(UserAccount account, string password);
}

public interface ITokenIssuer
{
    AccessToken Issue(UserAccount account);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt, string TokenType = "Bearer");

public sealed record UserView(Guid Id, string UserName, DateTimeOffset CreatedAt);

public sealed class IdentityService(
    IIdentityRepository repository,
    IPasswordService passwordService,
    ITokenIssuer tokenIssuer,
    TimeProvider timeProvider)
{
    public async Task<UserView> RegisterAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        EnsureUserName(userName);
        EnsurePassword(password);
        UserAccount account = UserAccount.Create(userName, timeProvider.GetUtcNow());
        UserAccount? existing = await repository.FindByNormalizedUserNameAsync(
            account.NormalizedUserName,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new IdentityException("registration_failed", "Registration could not be completed.");
        }

        account.SetPasswordHash(passwordService.Hash(account, password));
        await repository.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new UserView(account.Id, account.UserName, account.CreatedAt);
    }

    public async Task<AccessToken> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        EnsureUserName(userName);
        if (string.IsNullOrEmpty(password))
        {
            throw new IdentityException("invalid_request", "Username and password are required.");
        }

        string normalized = userName.Trim().ToUpperInvariant();
        UserAccount? account = await repository.FindByNormalizedUserNameAsync(normalized, cancellationToken)
            .ConfigureAwait(false);
        if (account is null || !passwordService.Verify(account, password))
        {
            throw new IdentityException("invalid_credentials", "Invalid username or password.");
        }

        return tokenIssuer.Issue(account);
    }

    private static void EnsureUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new IdentityException("invalid_request", "Username and password are required.");
        }
    }

    private static void EnsurePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new IdentityException("invalid_password", "Password length must be between 12 and 128 characters.");
        }

        if (password.Length is < 12 or > 128)
        {
            throw new IdentityException("invalid_password", "Password length must be between 12 and 128 characters.");
        }
    }
}

public sealed class IdentityException : InvalidOperationException
{
    public IdentityException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public IdentityException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}