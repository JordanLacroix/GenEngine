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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(static account => account.Id);
            entity.Property(static account => account.UserName).HasMaxLength(80).IsRequired();
            entity.Property(static account => account.NormalizedUserName).HasMaxLength(80).IsRequired();
            entity.Property(static account => account.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(static account => account.NormalizedUserName).IsUnique();
        });
    }
}

internal sealed class IdentityRepository(IdentityDbContext dbContext) : IIdentityRepository
{
    public Task<UserAccount?> FindByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(
            account => account.NormalizedUserName == normalizedUserName,
            cancellationToken);

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(account, cancellationToken).ConfigureAwait(false);

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
    public AccessToken Issue(UserAccount account)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddHours(1);
        string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
        string issuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity";
        string audience = configuration["Jwt:Audience"] ?? "GenEngine.Api";
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];
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
    }

    public static void MapIdentityHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}