using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using GenEngine.Identity.Application;
using GenEngine.Identity.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

namespace GenEngine.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<CustomRole> Roles => Set<CustomRole>();
    public DbSet<RolePermissionGrant> RolePermissions => Set<RolePermissionGrant>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(static account => account.Id);
            entity.Property(static account => account.UserName).HasMaxLength(80).IsRequired();
            entity.Property(static account => account.NormalizedUserName).HasMaxLength(80).IsRequired();
            entity.Property(static account => account.PasswordHash).HasMaxLength(500);
            entity.Property(static account => account.ExternalProvider).HasMaxLength(40);
            entity.Property(static account => account.ExternalSubject).HasMaxLength(200);
            entity.Property(static account => account.IsActive).HasDefaultValue(true);
            entity.HasIndex(static account => account.NormalizedUserName).IsUnique();
            entity.HasIndex(static account => new { account.ExternalProvider, account.ExternalSubject }).IsUnique();
            entity.HasMany(static account => account.RoleAssignments)
                .WithOne()
                .HasForeignKey(static assignment => assignment.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomRole>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(static role => role.Id);
            entity.Property(static role => role.Name).HasMaxLength(80).IsRequired();
            entity.Property(static role => role.NormalizedName).HasMaxLength(80).IsRequired();
            entity.Property(static role => role.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(static role => role.NormalizedName).IsUnique();
            entity.HasMany(static role => role.Permissions)
                .WithOne()
                .HasForeignKey(static permission => permission.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermissionGrant>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(static permission => new { permission.RoleId, permission.PermissionCode });
            entity.Property(static permission => permission.PermissionCode).HasMaxLength(80);
        });

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("user_role_assignments");
            entity.HasKey(static assignment => new { assignment.UserId, assignment.RoleId, assignment.Scope });
            entity.Property(static assignment => assignment.Scope).HasMaxLength(120);
            entity.HasOne(static assignment => assignment.Role)
                .WithMany()
                .HasForeignKey(static assignment => assignment.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class IdentityRepository(IdentityDbContext dbContext) : IIdentityRepository
{
    public Task<UserAccount?> FindByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken) =>
        UsersWithAccess().SingleOrDefaultAsync(account => account.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task<UserAccount?> FindByExternalSubjectAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken) =>
        UsersWithAccess().SingleOrDefaultAsync(
            account => account.ExternalProvider == provider && account.ExternalSubject == subject,
            cancellationToken);

    public Task<UserAccount?> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        UsersWithAccess().SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

    public Task<CustomRole?> FindRoleByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken) =>
        dbContext.Roles.Include(static role => role.Permissions)
            .SingleOrDefaultAsync(role => role.NormalizedName == normalizedName, cancellationToken);

    public Task<CustomRole?> GetRoleAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Roles.Include(static role => role.Permissions)
            .SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<bool> HasAssignmentsAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.UserRoleAssignments.AnyAsync(
            assignment => assignment.RoleId == roleId,
            cancellationToken);

    public Task<int> CountActiveUsersWithPermissionAsync(string permissionCode, CancellationToken cancellationToken) =>
        dbContext.Users.CountAsync(account =>
            account.IsActive
            && account.DeletedAt == null
            && account.RoleAssignments.Any(assignment =>
                assignment.Role.Permissions.Any(permission => permission.PermissionCode == permissionCode)),
            cancellationToken);

    public async Task<IReadOnlyList<CustomRole>> ListRolesAsync(CancellationToken cancellationToken) =>
        await dbContext.Roles.Include(static role => role.Permissions)
            .OrderBy(static role => role.Name)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<UserAccount> Items, int Total)> ListUsersAsync(
        string? query,
        bool includeDeleted,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<UserAccount> filtered = UsersWithAccess()
            .AsNoTracking()
            .Where(account => includeDeleted || account.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim()}%";
            filtered = filtered.Where(account => EF.Functions.ILike(account.UserName, pattern));
        }

        int total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        UserAccount[] items = await filtered
            .OrderBy(static account => account.UserName)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(account, cancellationToken).ConfigureAwait(false);

    public async Task AddRoleAsync(CustomRole role, CancellationToken cancellationToken) =>
        await dbContext.Roles.AddAsync(role, cancellationToken).ConfigureAwait(false);

    public async Task AssignRoleAsync(UserRoleAssignment assignment, CancellationToken cancellationToken) =>
        await dbContext.UserRoleAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

    public async Task RemoveRoleAssignmentAsync(Guid userId, Guid roleId, string scope, CancellationToken cancellationToken)
    {
        UserRoleAssignment? assignment = await dbContext.UserRoleAssignments.SingleOrDefaultAsync(
            item => item.UserId == userId && item.RoleId == roleId && item.Scope == scope,
            cancellationToken).ConfigureAwait(false);
        if (assignment is not null)
        {
            dbContext.UserRoleAssignments.Remove(assignment);
        }
    }

    public void RemoveRole(CustomRole role) => dbContext.Roles.Remove(role);

    public async Task RemoveUserAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserRoleAssignment[] assignments = await dbContext.UserRoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.UserRoleAssignments.RemoveRange(assignments);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
        {
            throw new IdentityException("registration_failed", "Registration could not be completed.", exception);
        }
    }

    private IQueryable<UserAccount> UsersWithAccess() =>
        dbContext.Users
            .Include(static account => account.RoleAssignments)
            .ThenInclude(static assignment => assignment.Role)
            .ThenInclude(static role => role.Permissions);
}

internal sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<UserAccount> passwordHasher = new();

    public string Hash(UserAccount account, string password) => passwordHasher.HashPassword(account, password);

    public bool Verify(UserAccount account, string password) =>
        passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password)
        is not PasswordVerificationResult.Failed;
}

internal sealed class JwtTokenIssuer(IConfiguration configuration, TimeProvider timeProvider) : ITokenIssuer
{
    public AccessToken Issue(UserAccount account, IReadOnlyCollection<string> permissions)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddHours(1);
        string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
        string issuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity";
        string audience = configuration["Jwt:Audience"] ?? "GenEngine.Api";
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];
        claims.AddRange(permissions.Select(static permission => new Claim("permission", permission)));
        claims.AddRange(account.RoleAssignments
            .Where(assignment => assignment.ExpiresAt is null || assignment.ExpiresAt > now)
            .Select(static assignment => assignment.Scope)
            .Distinct(StringComparer.Ordinal)
            .Select(static scope => new Claim("scope", scope)));
        JwtSecurityToken token = new(
            issuer,
            audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

internal sealed class IdentityDatabaseHealthCheck(IdentityDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Identity database is unavailable.");
}

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Identity")
            ?? "Host=localhost;Port=5434;Database=genengine_identity;Username=postgres;Password=postgres";
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IdentityService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<IdentityDatabaseHealthCheck>("identity-database");
        return services;
    }

    public static async Task MigrateIdentityDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        if (!await dbContext.Roles.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset now = TimeProvider.System.GetUtcNow();
            dbContext.Roles.AddRange(
                CustomRole.Create("Player", "Joue, suit sa progression et personnalise son familier.", ["session.play", "shop.read", "assistant.use", "assistant.customize", "onboarding.use", "onboarding.reset.own", "progress.read.own", "journal.read.own", "journal.export.own", "help.read", "media.read", "journey.read"], now, true),
                CustomRole.Create("Creator", "Crée, prévisualise et publie des scénarios.", ["session.play", "shop.read", "assistant.use", "assistant.customize", "onboarding.use", "onboarding.reset.own", "progress.read.own", "journal.read.own", "journal.export.own", "help.read", "media.read", "journey.read", "scenario.author", "scenario.publish", "ai.generate"], now, true),
                CustomRole.Create("Administrator", "Administre l'expérience, les accès et les providers.", PermissionCatalog.All.Keys, now, true));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        CustomRole? player = await dbContext.Roles.Include(static role => role.Permissions)
            .SingleOrDefaultAsync(role => role.NormalizedName == "PLAYER", cancellationToken)
            .ConfigureAwait(false);
        if (player is not null)
        {
            string[] playerPermissions = ["session.play", "shop.read", "assistant.use", "assistant.customize", "onboarding.use", "onboarding.reset.own", "progress.read.own", "journal.read.own", "journal.export.own", "help.read", "media.read", "journey.read"];
            HashSet<string> existing = player.Permissions.Select(static permission => permission.PermissionCode).ToHashSet(StringComparer.Ordinal);
            foreach (string permissionCode in playerPermissions.Where(code => !existing.Contains(code)))
            {
                dbContext.RolePermissions.Add(RolePermissionGrant.Create(player.Id, permissionCode));
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        CustomRole? administrator = await dbContext.Roles.Include(static role => role.Permissions)
            .SingleOrDefaultAsync(role => role.NormalizedName == "ADMINISTRATOR", cancellationToken)
            .ConfigureAwait(false);
        if (administrator is not null)
        {
            HashSet<string> existing = administrator.Permissions.Select(static permission => permission.PermissionCode).ToHashSet(StringComparer.Ordinal);
            foreach (string permissionCode in PermissionCatalog.All.Keys.Where(code => !existing.Contains(code)))
            {
                dbContext.RolePermissions.Add(RolePermissionGrant.Create(administrator.Id, permissionCode));
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static void MapIdentityHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}