using GenEngine.Organization.Domain;

namespace GenEngine.Organization.Application;

public sealed record FrontView(Guid Id, string FrontId, string Name, string Type, bool IsActive, int Revision, DateTimeOffset UpdatedAt);
public sealed record PeriodView(Guid Id, string FrontId, string Name, string Code, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsActive, int Revision, DateTimeOffset UpdatedAt);
public sealed record UnitView(Guid Id, string FrontId, Guid? ParentId, string Name, string Type, string Code, bool IsActive, int Revision, DateTimeOffset UpdatedAt);
public sealed record MembershipView(Guid Id, string FrontId, Guid UnitId, Guid UserId, Guid? PeriodId, MembershipKind Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool IsActive, int Revision, DateTimeOffset UpdatedAt);
public sealed record AssignmentView(Guid Id, string FrontId, Guid UnitId, AssignedContentType ContentType, Guid ContentId, string Name, bool Required, DateTimeOffset? AvailableFrom, DateTimeOffset? DueAt, bool IsActive, int Revision, DateTimeOffset UpdatedAt);
public sealed record PagedView<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record PlayerOrganizationContextView(string FrontId, bool IsMember, IReadOnlyList<Guid> UnitIds, IReadOnlyList<Guid> SupervisedUnitIds, IReadOnlyList<AssignmentView> Assignments, bool HasGlobalScope = false);
public sealed record MembershipImportRow(Guid Id, Guid UnitId, Guid UserId, Guid? PeriodId, MembershipKind Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
public sealed record MembershipImportError(int Row, string Code, string Message);
public sealed record MembershipImportView(bool DryRun, int Received, int Created, int Unchanged, IReadOnlyList<MembershipImportError> Errors);
public sealed record MembershipImportPolicy(bool Enabled, int MaxRows)
{
    public static MembershipImportPolicy Default { get; } = new(true, 500);
}

public interface IOrganizationRepository
{
    Task<OrganizationFront?> GetFrontAsync(string frontId, CancellationToken cancellationToken);
    Task<OperatingPeriod?> GetPeriodAsync(string frontId, Guid id, CancellationToken cancellationToken);
    Task<OrganizationUnit?> GetUnitAsync(string frontId, Guid id, CancellationToken cancellationToken);
    Task<Membership?> GetMembershipAsync(string frontId, Guid id, CancellationToken cancellationToken);
    Task<Membership?> GetMembershipByNaturalKeyAsync(string frontId, Guid userId, Guid unitId, DateTimeOffset startsAt, CancellationToken cancellationToken);
    Task<ContentAssignment?> GetAssignmentAsync(string frontId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationUnit>> ListUnitsAsync(string frontId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatingPeriod>> ListPeriodsAsync(string frontId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Membership> Items, int Total)> ListMembershipsAsync(string frontId, Guid? unitId, Guid? userId, MembershipKind? kind, int offset, int limit, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ContentAssignment> Items, int Total)> ListAssignmentsAsync(string frontId, Guid? unitId, AssignedContentType? contentType, int offset, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<Membership>> ListEffectiveMembershipsAsync(string frontId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentAssignment>> ListEffectiveAssignmentsAsync(string frontId, IReadOnlyCollection<Guid> unitIds, DateTimeOffset now, CancellationToken cancellationToken);
    Task AddFrontAsync(OrganizationFront front, CancellationToken cancellationToken);
    Task AddPeriodAsync(OperatingPeriod period, CancellationToken cancellationToken);
    Task AddUnitAsync(OrganizationUnit unit, CancellationToken cancellationToken);
    Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken);
    Task AddAssignmentAsync(ContentAssignment assignment, CancellationToken cancellationToken);
    void RemoveMembership(Membership membership);
    void RemoveAssignment(ContentAssignment assignment);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class OrganizationService(IOrganizationRepository repository, TimeProvider timeProvider, MembershipImportPolicy? configuredImportPolicy = null)
{
    private readonly MembershipImportPolicy importPolicy = Validate(configuredImportPolicy ?? MembershipImportPolicy.Default);
    public async Task<FrontView> GetFrontAsync(string frontId, CancellationToken cancellationToken) => Map(await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false));

    public async Task<FrontView> UpsertFrontAsync(string frontId, string name, string type, bool isActive, int? expectedRevision, CancellationToken cancellationToken)
    {
        OrganizationFront? front = await repository.GetFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (front is null)
        {
            if (expectedRevision is not null) throw new OrganizationException("front_not_found", "The organization front was not found.");
            front = OrganizationFront.Create(frontId, name, type, now);
            await repository.AddFrontAsync(front, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            front.Update(name, type, isActive, expectedRevision ?? 0, now);
        }
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(front);
    }

    public async Task<IReadOnlyList<PeriodView>> ListPeriodsAsync(string frontId, CancellationToken cancellationToken)
    {
        _ = await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        return (await repository.ListPeriodsAsync(frontId, cancellationToken).ConfigureAwait(false)).Select(Map).ToArray();
    }

    public async Task<PeriodView> UpsertPeriodAsync(string frontId, Guid id, string name, string code, DateTimeOffset startsAt, DateTimeOffset endsAt, bool isActive, int? expectedRevision, CancellationToken cancellationToken)
    {
        _ = await RequireActiveFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        OperatingPeriod? period = await repository.GetPeriodAsync(frontId, id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (period is null)
        {
            if (expectedRevision is not null) throw new OrganizationException("period_not_found", "The operating period was not found.");
            period = OperatingPeriod.Create(id, frontId, name, code, startsAt, endsAt, now);
            await repository.AddPeriodAsync(period, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            period.Update(name, code, startsAt, endsAt, isActive, expectedRevision ?? 0, now);
        }
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(period);
    }

    public async Task<IReadOnlyList<UnitView>> ListUnitsAsync(string frontId, CancellationToken cancellationToken)
    {
        _ = await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        return (await repository.ListUnitsAsync(frontId, cancellationToken).ConfigureAwait(false)).Select(Map).ToArray();
    }

    public async Task<UnitView> UpsertUnitAsync(string frontId, Guid id, Guid? parentId, string name, string type, string code, bool isActive, int? expectedRevision, CancellationToken cancellationToken)
    {
        _ = await RequireActiveFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        OrganizationUnit? unit = await repository.GetUnitAsync(frontId, id, cancellationToken).ConfigureAwait(false);
        if (parentId is Guid parent && await repository.GetUnitAsync(frontId, parent, cancellationToken).ConfigureAwait(false) is null)
            throw new OrganizationException("parent_not_found", "The parent unit does not belong to this front.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (unit is null)
        {
            if (expectedRevision is not null) throw new OrganizationException("unit_not_found", "The organization unit was not found.");
            unit = OrganizationUnit.Create(id, frontId, parentId, name, type, code, now);
            await repository.AddUnitAsync(unit, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            unit.Update(parentId, name, type, code, isActive, expectedRevision ?? 0, now);
        }
        await EnsureAcyclicAsync(frontId, unit, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(unit);
    }

    public async Task<PagedView<MembershipView>> ListMembershipsAsync(string frontId, Guid? unitId, Guid? userId, MembershipKind? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        _ = await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        (page, pageSize) = NormalizePage(page, pageSize);
        (IReadOnlyList<Membership> items, int total) = await repository.ListMembershipsAsync(frontId, unitId, userId, kind, (page - 1) * pageSize, pageSize, cancellationToken).ConfigureAwait(false);
        return new(items.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<MembershipView> UpsertMembershipAsync(string frontId, Guid id, Guid unitId, Guid userId, Guid? periodId, MembershipKind kind, DateTimeOffset startsAt, DateTimeOffset? endsAt, bool isActive, int? expectedRevision, CancellationToken cancellationToken)
    {
        _ = await RequireActiveFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        _ = await repository.GetUnitAsync(frontId, unitId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrganizationException("unit_not_found", "The organization unit does not belong to this front.");
        if (periodId is Guid period)
        {
            OperatingPeriod operatingPeriod = await repository.GetPeriodAsync(frontId, period, cancellationToken).ConfigureAwait(false)
                ?? throw new OrganizationException("period_not_found", "The operating period does not belong to this front.");
            if (!operatingPeriod.Contains(startsAt) || endsAt > operatingPeriod.EndsAt) throw new OrganizationException("membership_outside_period", "Membership dates must stay within the selected operating period.");
        }
        Membership? membership = await repository.GetMembershipAsync(frontId, id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (membership is null)
        {
            if (expectedRevision is not null) throw new OrganizationException("membership_not_found", "The membership was not found.");
            membership = Membership.Create(id, frontId, unitId, userId, periodId, kind, startsAt, endsAt, now);
            await repository.AddMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (membership.UnitId != unitId || membership.UserId != userId || membership.PeriodId != periodId) throw new OrganizationException("membership_identity_immutable", "Membership unit, user and period cannot be changed.");
            membership.Update(kind, startsAt, endsAt, isActive, expectedRevision ?? 0, now);
        }
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(membership);
    }

    public async Task<MembershipImportView> ImportMembershipsAsync(string frontId, IReadOnlyList<MembershipImportRow> rows, bool dryRun, CancellationToken cancellationToken)
    {
        _ = await RequireActiveFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (!importPolicy.Enabled) throw new OrganizationException("membership_import_disabled", "Membership import is disabled.");
        if (rows.Count < 1 || rows.Count > importPolicy.MaxRows) throw new OrganizationException("invalid_import_size", FormattableString.Invariant($"A membership import requires between 1 and {importPolicy.MaxRows} rows."));
        List<MembershipImportError> errors = [];
        List<Membership> created = [];
        int unchanged = 0;
        HashSet<Guid> rowIds = [];
        HashSet<(Guid UserId, Guid UnitId, DateTimeOffset StartsAt)> membershipKeys = [];
        for (int index = 0; index < rows.Count; index++)
        {
            MembershipImportRow row = rows[index];
            try
            {
                if (row.Id == Guid.Empty || !rowIds.Add(row.Id)) throw new OrganizationException("duplicate_import_id", "Each imported row requires a unique non-empty identifier.");
                if (!membershipKeys.Add((row.UserId, row.UnitId, row.StartsAt))) throw new OrganizationException("duplicate_import_membership", "The import contains the same user, unit and start date more than once.");
                if (await repository.GetUnitAsync(frontId, row.UnitId, cancellationToken).ConfigureAwait(false) is null) throw new OrganizationException("unit_not_found", "The organization unit does not belong to this front.");
                if (row.PeriodId is Guid periodId)
                {
                    OperatingPeriod period = await repository.GetPeriodAsync(frontId, periodId, cancellationToken).ConfigureAwait(false)
                        ?? throw new OrganizationException("period_not_found", "The operating period does not belong to this front.");
                    if (!period.Contains(row.StartsAt) || row.EndsAt > period.EndsAt) throw new OrganizationException("membership_outside_period", "Membership dates must stay within the selected operating period.");
                }
                Membership? existing = await repository.GetMembershipAsync(frontId, row.Id, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    if (existing.UnitId != row.UnitId || existing.UserId != row.UserId || existing.PeriodId != row.PeriodId || existing.Kind != row.Kind || existing.StartsAt != row.StartsAt || existing.EndsAt != row.EndsAt)
                        throw new OrganizationException("import_identity_conflict", "The row identifier already belongs to a different membership.");
                    unchanged++;
                    continue;
                }
                if (await repository.GetMembershipByNaturalKeyAsync(frontId, row.UserId, row.UnitId, row.StartsAt, cancellationToken).ConfigureAwait(false) is not null)
                    throw new OrganizationException("import_membership_conflict", "A membership already exists for this user, unit and start date.");
                created.Add(Membership.Create(row.Id, frontId, row.UnitId, row.UserId, row.PeriodId, row.Kind, row.StartsAt, row.EndsAt, timeProvider.GetUtcNow()));
            }
            catch (Exception exception) when (exception is OrganizationException or OrganizationDomainException)
            {
                string code = exception is OrganizationException application ? application.Code : ((OrganizationDomainException)exception).Code;
                errors.Add(new(index + 1, code, exception.Message));
            }
        }
        if (errors.Count == 0 && !dryRun)
        {
            foreach (Membership membership in created) await repository.AddMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return new(dryRun, rows.Count, errors.Count == 0 ? created.Count : 0, unchanged, errors);
    }

    public async Task DeleteMembershipAsync(string frontId, Guid id, CancellationToken cancellationToken)
    {
        Membership membership = await repository.GetMembershipAsync(frontId, id, cancellationToken).ConfigureAwait(false)
            ?? throw new OrganizationException("membership_not_found", "The membership was not found.");
        repository.RemoveMembership(membership);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedView<AssignmentView>> ListAssignmentsAsync(string frontId, Guid? unitId, AssignedContentType? contentType, int page, int pageSize, CancellationToken cancellationToken)
    {
        _ = await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        (page, pageSize) = NormalizePage(page, pageSize);
        (IReadOnlyList<ContentAssignment> items, int total) = await repository.ListAssignmentsAsync(frontId, unitId, contentType, (page - 1) * pageSize, pageSize, cancellationToken).ConfigureAwait(false);
        return new(items.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<AssignmentView> UpsertAssignmentAsync(string frontId, Guid id, Guid unitId, AssignedContentType contentType, Guid contentId, string name, bool required, DateTimeOffset? availableFrom, DateTimeOffset? dueAt, bool isActive, int? expectedRevision, CancellationToken cancellationToken)
    {
        _ = await RequireActiveFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        _ = await repository.GetUnitAsync(frontId, unitId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrganizationException("unit_not_found", "The organization unit does not belong to this front.");
        ContentAssignment? assignment = await repository.GetAssignmentAsync(frontId, id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (assignment is null)
        {
            if (expectedRevision is not null) throw new OrganizationException("assignment_not_found", "The assignment was not found.");
            assignment = ContentAssignment.Create(id, frontId, unitId, contentType, contentId, name, required, availableFrom, dueAt, now);
            await repository.AddAssignmentAsync(assignment, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (assignment.UnitId != unitId || assignment.ContentType != contentType || assignment.ContentId != contentId) throw new OrganizationException("assignment_identity_immutable", "Assignment unit and content cannot be changed.");
            assignment.Update(name, required, availableFrom, dueAt, isActive, expectedRevision ?? 0, now);
        }
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(assignment);
    }

    public async Task DeleteAssignmentAsync(string frontId, Guid id, CancellationToken cancellationToken)
    {
        ContentAssignment assignment = await repository.GetAssignmentAsync(frontId, id, cancellationToken).ConfigureAwait(false)
            ?? throw new OrganizationException("assignment_not_found", "The assignment was not found.");
        repository.RemoveAssignment(assignment);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlayerOrganizationContextView> ResolvePlayerContextAsync(string frontId, Guid userId, CancellationToken cancellationToken)
    {
        OrganizationFront? front = await repository.GetFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (front is null || !front.IsActive) return new(frontId.Trim().ToLowerInvariant(), false, [], [], []);
        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<Membership> memberships = await repository.ListEffectiveMembershipsAsync(frontId, userId, now, cancellationToken).ConfigureAwait(false);
        Guid[] unitIds = memberships.Select(static item => item.UnitId).Distinct().ToArray();
        IReadOnlyList<ContentAssignment> assignments = unitIds.Length == 0
            ? []
            : await repository.ListEffectiveAssignmentsAsync(frontId, unitIds, now, cancellationToken).ConfigureAwait(false);
        return new(front.FrontId, memberships.Count > 0, unitIds, memberships.Where(static item => item.Kind == MembershipKind.Supervisor).Select(static item => item.UnitId).Distinct().ToArray(), assignments.Select(Map).ToArray());
    }

    private async Task EnsureAcyclicAsync(string frontId, OrganizationUnit changed, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid?> parents = (await repository.ListUnitsAsync(frontId, cancellationToken).ConfigureAwait(false)).ToDictionary(static unit => unit.Id, static unit => unit.ParentId);
        parents[changed.Id] = changed.ParentId;
        HashSet<Guid> visited = [];
        Guid? current = changed.Id;
        while (current is Guid id && parents.TryGetValue(id, out Guid? parent))
        {
            if (!visited.Add(id)) throw new OrganizationException("unit_cycle", "The organization hierarchy cannot contain a cycle.");
            current = parent;
        }
    }

    private async Task<OrganizationFront> RequireFrontAsync(string frontId, CancellationToken cancellationToken) =>
        await repository.GetFrontAsync(frontId, cancellationToken).ConfigureAwait(false) ?? throw new OrganizationException("front_not_found", "The organization front was not found.");

    private async Task<OrganizationFront> RequireActiveFrontAsync(string frontId, CancellationToken cancellationToken)
    {
        OrganizationFront front = await RequireFrontAsync(frontId, cancellationToken).ConfigureAwait(false);
        if (!front.IsActive) throw new OrganizationException("front_inactive", "The organization front is inactive.");
        return front;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 10, 100));
    private static MembershipImportPolicy Validate(MembershipImportPolicy policy) => policy.MaxRows is >= 1 and <= 5000 ? policy : throw new OrganizationException("invalid_import_policy", "Membership import MaxRows must be between 1 and 5000.");
    private static FrontView Map(OrganizationFront item) => new(item.Id, item.FrontId, item.Name, item.Type, item.IsActive, item.Revision, item.UpdatedAt);
    private static PeriodView Map(OperatingPeriod item) => new(item.Id, item.FrontId, item.Name, item.Code, item.StartsAt, item.EndsAt, item.IsActive, item.Revision, item.UpdatedAt);
    private static UnitView Map(OrganizationUnit item) => new(item.Id, item.FrontId, item.ParentId, item.Name, item.Type, item.Code, item.IsActive, item.Revision, item.UpdatedAt);
    private static MembershipView Map(Membership item) => new(item.Id, item.FrontId, item.UnitId, item.UserId, item.PeriodId, item.Kind, item.StartsAt, item.EndsAt, item.IsActive, item.Revision, item.UpdatedAt);
    private static AssignmentView Map(ContentAssignment item) => new(item.Id, item.FrontId, item.UnitId, item.ContentType, item.ContentId, item.Name, item.Required, item.AvailableFrom, item.DueAt, item.IsActive, item.Revision, item.UpdatedAt);
}

public sealed class OrganizationException(string code, string message, Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public string Code { get; } = code;
}