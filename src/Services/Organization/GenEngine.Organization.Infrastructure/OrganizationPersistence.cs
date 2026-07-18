using GenEngine.Organization.Application;
using GenEngine.Organization.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenEngine.Organization.Infrastructure;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<OrganizationFront> Fronts => Set<OrganizationFront>();
    public DbSet<OrganizationUnit> Units => Set<OrganizationUnit>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<ContentAssignment> Assignments => Set<ContentAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationFront>(entity =>
        {
            entity.ToTable("organization_fronts");
            entity.HasKey(static item => item.Id);
            entity.Property(static item => item.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(static item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(static item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(static item => item.FrontId).IsUnique();
        });
        modelBuilder.Entity<OrganizationUnit>(entity =>
        {
            entity.ToTable("organization_units");
            entity.HasKey(static item => item.Id);
            entity.Property(static item => item.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(static item => item.Type).HasMaxLength(60).IsRequired();
            entity.Property(static item => item.Code).HasMaxLength(60).IsRequired();
            entity.Property(static item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(static item => new { item.FrontId, item.Code }).IsUnique();
            entity.HasIndex(static item => new { item.FrontId, item.ParentId });
        });
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(static item => item.Id);
            entity.Property(static item => item.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static item => item.Kind).HasConversion<string>().HasMaxLength(30);
            entity.Property(static item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(static item => new { item.FrontId, item.UserId, item.UnitId }).IsUnique();
            entity.HasIndex(static item => new { item.FrontId, item.UnitId, item.IsActive });
        });
        modelBuilder.Entity<ContentAssignment>(entity =>
        {
            entity.ToTable("content_assignments");
            entity.HasKey(static item => item.Id);
            entity.Property(static item => item.FrontId).HasMaxLength(80).IsRequired();
            entity.Property(static item => item.ContentType).HasConversion<string>().HasMaxLength(30);
            entity.Property(static item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(static item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(static item => new { item.FrontId, item.UnitId, item.ContentType, item.ContentId }).IsUnique();
            entity.HasIndex(static item => new { item.FrontId, item.UnitId, item.IsActive });
        });
    }
}

internal sealed class OrganizationRepository(OrganizationDbContext dbContext) : IOrganizationRepository
{
    private static string Normalize(string frontId) => frontId.Trim().ToLowerInvariant();
    public Task<OrganizationFront?> GetFrontAsync(string frontId, CancellationToken cancellationToken) => dbContext.Fronts.SingleOrDefaultAsync(item => item.FrontId == Normalize(frontId), cancellationToken);
    public Task<OrganizationUnit?> GetUnitAsync(string frontId, Guid id, CancellationToken cancellationToken) => dbContext.Units.SingleOrDefaultAsync(item => item.FrontId == Normalize(frontId) && item.Id == id, cancellationToken);
    public Task<Membership?> GetMembershipAsync(string frontId, Guid id, CancellationToken cancellationToken) => dbContext.Memberships.SingleOrDefaultAsync(item => item.FrontId == Normalize(frontId) && item.Id == id, cancellationToken);
    public Task<ContentAssignment?> GetAssignmentAsync(string frontId, Guid id, CancellationToken cancellationToken) => dbContext.Assignments.SingleOrDefaultAsync(item => item.FrontId == Normalize(frontId) && item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<OrganizationUnit>> ListUnitsAsync(string frontId, CancellationToken cancellationToken) => await dbContext.Units.AsNoTracking().Where(item => item.FrontId == Normalize(frontId)).OrderBy(static item => item.Name).ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<Membership> Items, int Total)> ListMembershipsAsync(string frontId, Guid? unitId, Guid? userId, MembershipKind? kind, int offset, int limit, CancellationToken cancellationToken)
    {
        IQueryable<Membership> query = dbContext.Memberships.AsNoTracking().Where(item => item.FrontId == Normalize(frontId));
        if (unitId is not null) query = query.Where(item => item.UnitId == unitId);
        if (userId is not null) query = query.Where(item => item.UserId == userId);
        if (kind is not null) query = query.Where(item => item.Kind == kind);
        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        Membership[] items = await query.OrderByDescending(static item => item.IsActive).ThenBy(static item => item.UserId).Skip(offset).Take(limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<(IReadOnlyList<ContentAssignment> Items, int Total)> ListAssignmentsAsync(string frontId, Guid? unitId, AssignedContentType? contentType, int offset, int limit, CancellationToken cancellationToken)
    {
        IQueryable<ContentAssignment> query = dbContext.Assignments.AsNoTracking().Where(item => item.FrontId == Normalize(frontId));
        if (unitId is not null) query = query.Where(item => item.UnitId == unitId);
        if (contentType is not null) query = query.Where(item => item.ContentType == contentType);
        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        ContentAssignment[] items = await query.OrderByDescending(static item => item.Required).ThenBy(static item => item.Name).Skip(offset).Take(limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<Membership>> ListEffectiveMembershipsAsync(string frontId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Memberships.AsNoTracking().Where(item => item.FrontId == Normalize(frontId) && item.UserId == userId && item.IsActive && item.StartsAt <= now && (item.EndsAt == null || item.EndsAt > now)).ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentAssignment>> ListEffectiveAssignmentsAsync(string frontId, IReadOnlyCollection<Guid> unitIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Assignments.AsNoTracking().Where(item => item.FrontId == Normalize(frontId) && unitIds.Contains(item.UnitId) && item.IsActive && (item.AvailableFrom == null || item.AvailableFrom <= now) && (item.DueAt == null || item.DueAt >= now)).OrderByDescending(static item => item.Required).ThenBy(static item => item.DueAt).ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddFrontAsync(OrganizationFront front, CancellationToken cancellationToken) => await dbContext.Fronts.AddAsync(front, cancellationToken).ConfigureAwait(false);
    public async Task AddUnitAsync(OrganizationUnit unit, CancellationToken cancellationToken) => await dbContext.Units.AddAsync(unit, cancellationToken).ConfigureAwait(false);
    public async Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken) => await dbContext.Memberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
    public async Task AddAssignmentAsync(ContentAssignment assignment, CancellationToken cancellationToken) => await dbContext.Assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
    public void RemoveMembership(Membership membership) => dbContext.Memberships.Remove(membership);
    public void RemoveAssignment(ContentAssignment assignment) => dbContext.Assignments.Remove(assignment);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException exception) { throw new OrganizationException("revision_conflict", "The resource was modified concurrently.", exception); }
        catch (DbUpdateException exception) { throw new OrganizationException("organization_conflict", "The organization change conflicts with existing data.", exception); }
    }
}

internal sealed class OrganizationDatabaseHealthCheck(OrganizationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Organization database is unavailable.");
}

public static class OrganizationInfrastructureExtensions
{
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Organization") ?? "Host=localhost;Port=5437;Database=genengine_organization;Username=postgres;Password=postgres";
        services.AddDbContext<OrganizationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<OrganizationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<OrganizationDatabaseHealthCheck>("organization-database");
        return services;
    }

    public static async Task MigrateAndSeedOrganizationDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        OrganizationDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        if (!await dbContext.Fronts.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            OrganizationService service = scope.ServiceProvider.GetRequiredService<OrganizationService>();
            _ = await service.UpsertFrontAsync("default", "Organisation principale", "Custom", true, null, cancellationToken).ConfigureAwait(false);
            _ = await service.UpsertUnitAsync("default", Guid.Parse("efc447ef-fdd6-42e6-b3d8-5de6841d9bce"), null, "Structure principale", "Organization", "ROOT", true, null, cancellationToken).ConfigureAwait(false);
        }
    }

    public static void MapOrganizationHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
        app.MapHealthChecks("/health/ready");
    }
}
