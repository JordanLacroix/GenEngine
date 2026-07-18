using GenEngine.Identity.Domain;

namespace GenEngine.Identity.Application;

public static class PermissionCatalog
{
    public static readonly IReadOnlyDictionary<string, string> All =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["session.play"] = "Jouer aux scénarios publiés",
            ["scenario.author"] = "Créer et modifier des scénarios",
            ["scenario.publish"] = "Publier des scénarios",
            ["config.read"] = "Consulter l'administration",
            ["config.write"] = "Modifier la configuration",
            ["config.publish"] = "Publier la configuration",
            ["identity.user.read"] = "Consulter les utilisateurs",
            ["identity.user.manage"] = "Activer, désactiver et supprimer les utilisateurs",
            ["rbac.read"] = "Consulter les rôles et permissions",
            ["rbac.manage"] = "Gérer les rôles et affectations",
            ["front.read"] = "Consulter les organisations autorisées",
            ["front.manage"] = "Administrer un front d'organisation",
            ["unit.read"] = "Consulter les unités d'organisation",
            ["unit.manage"] = "Administrer les unités d'organisation",
            ["period.read"] = "Consulter les périodes métier",
            ["period.manage"] = "Administrer les périodes métier",
            ["membership.read"] = "Consulter les appartenances et encadrants",
            ["membership.manage"] = "Administrer les appartenances et encadrants",
            ["journey.read"] = "Consulter les parcours",
            ["journey.manage"] = "Gérer les parcours",
            ["category.read"] = "Consulter les catégories",
            ["category.manage"] = "Gérer les catégories",
            ["assignment.read"] = "Consulter les affectations de contenu",
            ["assignment.manage"] = "Gérer les affectations de contenu",
            ["assistant.use"] = "Utiliser le familier",
            ["assistant.customize"] = "Personnaliser son familier",
            ["assistant.manage"] = "Gérer le catalogue de familiers",
            ["assistant.import"] = "Importer des ressources de familier",
            ["onboarding.use"] = "Suivre le tutoriel",
            ["onboarding.reset.own"] = "Recommencer son tutoriel",
            ["onboarding.manage"] = "Configurer les tutoriels",
            ["progress.read.own"] = "Consulter sa progression",
            ["progress.read.any"] = "Consulter la progression des joueurs",
            ["journal.read.own"] = "Consulter son journal",
            ["journal.export.own"] = "Exporter son journal",
            ["journal.read.any"] = "Consulter les journaux des joueurs",
            ["help.read"] = "Consulter l'aide",
            ["help.manage"] = "Gérer l'aide et le glossaire",
            ["experience.manage"] = "Configurer l'introduction et l'expérience joueur",
            ["media.read"] = "Consulter la médiathèque",
            ["media.manage"] = "Gérer la médiathèque",
            ["shop.read"] = "Consulter le magasin",
            ["shop.manage"] = "Gérer les offres du magasin",
            ["economy.credit"] = "Créditer une monnaie",
            ["ai.configure"] = "Configurer les providers IA",
            ["ai.test"] = "Tester les providers IA",
            ["ai.generate"] = "Générer du contenu narratif",
        };
}

public interface IIdentityRepository
{
    Task<UserAccount?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken);
    Task<UserAccount?> FindByExternalSubjectAsync(string provider, string subject, CancellationToken cancellationToken);
    Task<UserAccount?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomRole?> FindRoleByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken);
    Task<CustomRole?> GetRoleAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasAssignmentsAsync(Guid roleId, CancellationToken cancellationToken);
    Task<int> CountActiveUsersWithPermissionAsync(string permissionCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomRole>> ListRolesAsync(CancellationToken cancellationToken);
    Task<(IReadOnlyList<UserAccount> Items, int Total)> ListUsersAsync(string? query, bool includeDeleted, int offset, int limit, CancellationToken cancellationToken);
    Task AddAsync(UserAccount account, CancellationToken cancellationToken);
    Task AddRoleAsync(CustomRole role, CancellationToken cancellationToken);
    Task AssignRoleAsync(UserRoleAssignment assignment, CancellationToken cancellationToken);
    Task RemoveRoleAssignmentAsync(Guid userId, Guid roleId, string scope, CancellationToken cancellationToken);
    void RemoveRole(CustomRole role);
    Task RemoveUserAssignmentsAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(UserAccount account, string password);
    bool Verify(UserAccount account, string password);
}

public interface ITokenIssuer
{
    AccessToken Issue(UserAccount account, IReadOnlyCollection<string> permissions);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt, string TokenType = "Bearer");
public sealed record UserView(Guid Id, string UserName, DateTimeOffset CreatedAt, bool IsActive, DateTimeOffset? DeletedAt);
public sealed record PermissionView(string Code, string Description);
public sealed record RoleView(Guid Id, string Name, string Description, bool IsSystem, IReadOnlyList<string> Permissions);
public sealed record UserAccessView(Guid Id, string UserName, IReadOnlyList<RoleView> Roles, IReadOnlyList<string> Permissions);
public sealed record RoleAssignmentView(Guid RoleId, string RoleName, string Scope, DateTimeOffset? ExpiresAt, DateTimeOffset AssignedAt);
public sealed record AdminUserView(Guid Id, string UserName, DateTimeOffset CreatedAt, bool IsActive, DateTimeOffset? DeletedAt, string? ExternalProvider, IReadOnlyList<RoleAssignmentView> RoleAssignments);
public sealed record PagedUsersView(IReadOnlyList<AdminUserView> Items, int Page, int PageSize, int Total);

public sealed class IdentityService(
    IIdentityRepository repository,
    IPasswordService passwordService,
    ITokenIssuer tokenIssuer,
    TimeProvider timeProvider)
{
    public async Task<UserView> RegisterAsync(string userName, string password, CancellationToken cancellationToken)
    {
        EnsureUserName(userName);
        EnsurePassword(password);
        UserAccount account = UserAccount.Create(userName, timeProvider.GetUtcNow());
        UserAccount? existing = await repository.FindByNormalizedUserNameAsync(account.NormalizedUserName, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new IdentityException("registration_failed", "Registration could not be completed.");
        }

        account.SetPasswordHash(passwordService.Hash(account, password));
        await repository.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await AssignDefaultRoleAsync(account.Id, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapUser(account);
    }

    public async Task<AccessToken> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        EnsureUserName(userName);
        if (string.IsNullOrEmpty(password))
        {
            throw new IdentityException("invalid_request", "Username and password are required.");
        }

        UserAccount? account = await repository.FindByNormalizedUserNameAsync(
            userName.Trim().ToUpperInvariant(),
            cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsActive || account.DeletedAt is not null || string.IsNullOrEmpty(account.PasswordHash) || !passwordService.Verify(account, password))
        {
            throw new IdentityException("invalid_credentials", "Invalid username or password.");
        }

        if (!account.IsActive || account.DeletedAt is not null)
        {
            throw new IdentityException("account_disabled", "This account is disabled.");
        }

        return Issue(account);
    }

    public async Task<AccessToken> ExchangeExternalAsync(
        string provider,
        string subject,
        string userName,
        CancellationToken cancellationToken)
    {
        UserAccount? account = await repository.FindByExternalSubjectAsync(provider, subject, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            EnsureUserName(userName);
            account = UserAccount.CreateExternal(userName, provider, subject, timeProvider.GetUtcNow());
            await repository.AddAsync(account, cancellationToken).ConfigureAwait(false);
            await AssignDefaultRoleAsync(account.Id, cancellationToken).ConfigureAwait(false);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            account = await repository.GetUserAsync(account.Id, cancellationToken).ConfigureAwait(false) ?? account;
        }

        return Issue(account);
    }

    public async Task<UserAccessView> GetAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserAccount account = await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found.");
        RoleView[] roles = ActiveRoles(account).Select(Map).ToArray();
        string[] permissions = roles.SelectMany(static role => role.Permissions).Distinct(StringComparer.Ordinal).Order().ToArray();
        return new UserAccessView(account.Id, account.UserName, roles, permissions);
    }

    public static IReadOnlyList<PermissionView> ListPermissions() =>
        PermissionCatalog.All.Select(static pair => new PermissionView(pair.Key, pair.Value)).ToArray();

    public async Task<IReadOnlyList<RoleView>> ListRolesAsync(CancellationToken cancellationToken) =>
        (await repository.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Select(Map).ToArray();

    public async Task<PagedUsersView> ListUsersAsync(
        string? query,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        (IReadOnlyList<UserAccount> items, int total) = await repository.ListUsersAsync(
            string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            includeDeleted,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        return new PagedUsersView(items.Select(MapAdminUser).ToArray(), page, pageSize, total);
    }

    public async Task<AdminUserView> GetAdminUserAsync(Guid userId, CancellationToken cancellationToken) =>
        MapAdminUser(await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found."));

    public async Task<RoleView> CreateRoleAsync(
        string name,
        string description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        ValidatePermissions(permissions);
        if (await repository.FindRoleByNormalizedNameAsync(name.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new IdentityException("role_exists", "A role with this name already exists.");
        }

        CustomRole role = CustomRole.Create(name, description, permissions, timeProvider.GetUtcNow());
        await repository.AddRoleAsync(role, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(role);
    }

    public async Task<RoleView> UpdateRoleAsync(
        Guid roleId,
        string name,
        string description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        ValidatePermissions(permissions);
        CustomRole role = await repository.GetRoleAsync(roleId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("role_not_found", "The role was not found.");
        role.Update(name, description, permissions);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(role);
    }

    public async Task AssignRoleAsync(
        Guid userId,
        Guid roleId,
        string? scope,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        _ = await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found.");
        _ = await repository.GetRoleAsync(roleId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("role_not_found", "The role was not found.");
        await repository.AssignRoleAsync(
            UserRoleAssignment.Create(userId, roleId, scope, expiresAt, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveRoleAssignmentAsync(
        Guid userId,
        Guid roleId,
        string? scope,
        CancellationToken cancellationToken)
    {
        UserAccount account = await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found.");
        UserRoleAssignment assignment = account.RoleAssignments.SingleOrDefault(item =>
                item.RoleId == roleId && string.Equals(item.Scope, string.IsNullOrWhiteSpace(scope) ? "*" : scope.Trim(), StringComparison.Ordinal))
            ?? throw new IdentityException("assignment_not_found", "The role assignment was not found.");
        if (assignment.Role.Permissions.Any(static permission => permission.PermissionCode == "rbac.manage")
            && await repository.CountActiveUsersWithPermissionAsync("rbac.manage", cancellationToken).ConfigureAwait(false) <= 1)
        {
            throw new IdentityException("last_administrator", "The last active access administrator cannot lose this role.");
        }

        await repository.RemoveRoleAssignmentAsync(userId, roleId, assignment.Scope, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        CustomRole role = await repository.GetRoleAsync(roleId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("role_not_found", "The role was not found.");
        role.Archive();
        if (await repository.HasAssignmentsAsync(roleId, cancellationToken).ConfigureAwait(false))
        {
            throw new IdentityException("role_in_use", "Remove all assignments before deleting this role.");
        }

        repository.RemoveRole(role);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminUserView> SetUserActiveAsync(
        Guid userId,
        Guid actorId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (userId == actorId && !isActive)
        {
            throw new IdentityException("self_lockout", "You cannot disable your own account.");
        }

        UserAccount account = await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found.");
        if (!isActive && HasPermission(account, "rbac.manage")
            && await repository.CountActiveUsersWithPermissionAsync("rbac.manage", cancellationToken).ConfigureAwait(false) <= 1)
        {
            throw new IdentityException("last_administrator", "The last active access administrator cannot be disabled.");
        }

        account.SetActive(isActive);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapAdminUser(account);
    }

    public async Task DeleteUserAsync(Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        if (userId == actorId)
        {
            throw new IdentityException("self_delete", "You cannot delete your own account.");
        }

        UserAccount account = await repository.GetUserAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("user_not_found", "The user was not found.");
        if (HasPermission(account, "rbac.manage")
            && await repository.CountActiveUsersWithPermissionAsync("rbac.manage", cancellationToken).ConfigureAwait(false) <= 1)
        {
            throw new IdentityException("last_administrator", "The last active access administrator cannot be deleted.");
        }

        account.SoftDelete(timeProvider.GetUtcNow());
        await repository.RemoveUserAssignmentsAsync(userId, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task BootstrapAdministratorAsync(Guid userId, CancellationToken cancellationToken)
    {
        CustomRole role = await repository.FindRoleByNormalizedNameAsync("ADMINISTRATOR", cancellationToken)
            .ConfigureAwait(false)
            ?? throw new IdentityException("role_not_found", "The Administrator role is not configured.");
        if (await repository.HasAssignmentsAsync(role.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new IdentityException("bootstrap_closed", "An administrator already exists.");
        }

        await repository.AssignRoleAsync(
            UserRoleAssignment.Create(userId, role.Id, "*", null, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private AccessToken Issue(UserAccount account) =>
        tokenIssuer.Issue(
            account,
            ActiveRoles(account)
                .SelectMany(static role => role.Permissions)
                .Select(static permission => permission.PermissionCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private IEnumerable<CustomRole> ActiveRoles(UserAccount account)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return account.RoleAssignments
            .Where(assignment => assignment.ExpiresAt is null || assignment.ExpiresAt > now)
            .Select(static assignment => assignment.Role);
    }

    private async Task AssignDefaultRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        CustomRole role = await repository.FindRoleByNormalizedNameAsync("PLAYER", cancellationToken).ConfigureAwait(false)
            ?? throw new IdentityException("role_not_found", "The default Player role is not configured.");
        await repository.AssignRoleAsync(
            UserRoleAssignment.Create(userId, role.Id, "front:default", null, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    private static RoleView Map(CustomRole role) =>
        new(role.Id, role.Name, role.Description, role.IsSystem, role.Permissions.Select(static item => item.PermissionCode).Order().ToArray());

    private static UserView MapUser(UserAccount account) =>
        new(account.Id, account.UserName, account.CreatedAt, account.IsActive, account.DeletedAt);

    private static AdminUserView MapAdminUser(UserAccount account) =>
        new(
            account.Id,
            account.UserName,
            account.CreatedAt,
            account.IsActive,
            account.DeletedAt,
            account.ExternalProvider,
            account.RoleAssignments
                .OrderBy(static assignment => assignment.Role.Name)
                .ThenBy(static assignment => assignment.Scope)
                .Select(static assignment => new RoleAssignmentView(
                    assignment.RoleId,
                    assignment.Role.Name,
                    assignment.Scope,
                    assignment.ExpiresAt,
                    assignment.AssignedAt))
                .ToArray());

    private static bool HasPermission(UserAccount account, string permission) =>
        account.RoleAssignments.Any(assignment => assignment.Role.Permissions.Any(grant => grant.PermissionCode == permission));

    private static void ValidatePermissions(IEnumerable<string> permissions)
    {
        string? unknown = permissions.FirstOrDefault(permission => !PermissionCatalog.All.ContainsKey(permission));
        if (unknown is not null)
        {
            throw new IdentityException("unknown_permission", $"Unknown permission '{unknown}'.");
        }
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
        if (string.IsNullOrEmpty(password) || password.Length is < 12 or > 128)
        {
            throw new IdentityException("invalid_password", "Password length must be between 12 and 128 characters.");
        }
    }
}

public sealed class IdentityException : InvalidOperationException
{
    public IdentityException(string code, string message) : base(message) => Code = code;
    public IdentityException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;
    public string Code { get; }
}