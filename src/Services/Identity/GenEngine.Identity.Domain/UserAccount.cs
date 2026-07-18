namespace GenEngine.Identity.Domain;

public sealed class UserAccount
{
    private readonly List<UserRoleAssignment> roleAssignments = [];
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

    public string? ExternalProvider { get; private set; }

    public string? ExternalSubject { get; private set; }

    public IReadOnlyList<UserRoleAssignment> RoleAssignments => roleAssignments;

    public static UserAccount Create(string userName, DateTimeOffset createdAt)
    {
        string trimmed = userName.Trim();
        if (trimmed.Length is < 3 or > 80)
        {
            throw new IdentityDomainException("invalid_username", "Username length must be between 3 and 80 characters.");
        }

        return new UserAccount(Guid.NewGuid(), trimmed, trimmed.ToUpperInvariant(), createdAt);
    }

    public static UserAccount CreateExternal(
        string userName,
        string provider,
        string subject,
        DateTimeOffset createdAt)
    {
        UserAccount account = Create(userName, createdAt);
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
        {
            throw new IdentityDomainException("invalid_external_identity", "Provider and subject are required.");
        }

        account.ExternalProvider = provider.Trim();
        account.ExternalSubject = subject.Trim();
        return account;
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

public sealed class CustomRole
{
    private readonly List<RolePermissionGrant> permissions = [];

    private CustomRole() { }

    private CustomRole(Guid id, string name, string description, bool isSystem, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = description.Trim();
        IsSystem = isSystem;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<RolePermissionGrant> Permissions => permissions;

    public static CustomRole Create(
        string name,
        string description,
        IEnumerable<string> permissionCodes,
        DateTimeOffset createdAt,
        bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
        {
            throw new IdentityDomainException("invalid_role", "A role name between 1 and 80 characters is required.");
        }

        CustomRole role = new(Guid.NewGuid(), name, description, isSystem, createdAt);
        role.ReplacePermissions(permissionCodes);
        return role;
    }

    public void Update(string name, string description, IEnumerable<string> permissionCodes)
    {
        if (IsSystem)
        {
            throw new IdentityDomainException("system_role_immutable", "System roles cannot be changed.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
        {
            throw new IdentityDomainException("invalid_role", "A role name between 1 and 80 characters is required.");
        }

        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = description.Trim();
        ReplacePermissions(permissionCodes);
    }

    private void ReplacePermissions(IEnumerable<string> permissionCodes)
    {
        string[] codes = permissionCodes
            .Select(static code => code.Trim().ToLowerInvariant())
            .Where(static code => code.Length != 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (codes.Length == 0)
        {
            throw new IdentityDomainException("invalid_role", "A role must contain at least one permission.");
        }

        permissions.Clear();
        permissions.AddRange(codes.Select(code => RolePermissionGrant.Create(Id, code)));
    }
}

public sealed class RolePermissionGrant
{
    private RolePermissionGrant() { }
    private RolePermissionGrant(Guid roleId, string permissionCode) { RoleId = roleId; PermissionCode = permissionCode; }
    public Guid RoleId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    internal static RolePermissionGrant Create(Guid roleId, string permissionCode) => new(roleId, permissionCode);
}

public sealed class UserRoleAssignment
{
    private UserRoleAssignment() { }
    private UserRoleAssignment(
        Guid userId,
        Guid roleId,
        string scope,
        DateTimeOffset? expiresAt,
        DateTimeOffset assignedAt)
    {
        UserId = userId;
        RoleId = roleId;
        Scope = scope;
        ExpiresAt = expiresAt;
        AssignedAt = assignedAt;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public string Scope { get; private set; } = "*";
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public CustomRole Role { get; private set; } = null!;

    public static UserRoleAssignment Create(
        Guid userId,
        Guid roleId,
        string? scope,
        DateTimeOffset? expiresAt,
        DateTimeOffset assignedAt) =>
        new(userId, roleId, string.IsNullOrWhiteSpace(scope) ? "*" : scope.Trim(), expiresAt, assignedAt);
}

public sealed class IdentityDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}