using GenEngine.Organization.Application;
using GenEngine.Organization.Domain;

namespace GenEngine.Services.Tests;

public sealed class OrganizationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlayerContextOnlyContainsEffectiveAssignmentsFromTheRequestedFront()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        Guid userId = Guid.NewGuid();
        Guid defaultUnit = Guid.NewGuid();
        Guid otherUnit = Guid.NewGuid();
        Guid assignedScenario = Guid.NewGuid();
        Guid foreignScenario = Guid.NewGuid();

        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        _ = await service.UpsertUnitAsync("default", defaultUnit, null, "Classe A", "Class", "A", true, null, default);
        _ = await service.UpsertMembershipAsync("default", Guid.NewGuid(), defaultUnit, userId, null, MembershipKind.Participant, Now.AddDays(-1), null, true, null, default);
        _ = await service.UpsertAssignmentAsync("default", Guid.NewGuid(), defaultUnit, AssignedContentType.Scenario, assignedScenario, "Mission A", true, Now.AddDays(-1), Now.AddDays(1), true, null, default);

        _ = await service.UpsertFrontAsync("other", "Entreprise", "Company", true, null, default);
        _ = await service.UpsertUnitAsync("other", otherUnit, null, "Équipe B", "Team", "B", true, null, default);
        _ = await service.UpsertAssignmentAsync("other", Guid.NewGuid(), otherUnit, AssignedContentType.Scenario, foreignScenario, "Mission B", true, null, null, true, null, default);

        PlayerOrganizationContextView context = await service.ResolvePlayerContextAsync("default", userId, default);

        Assert.True(context.IsMember);
        Assert.Equal([defaultUnit], context.UnitIds);
        AssignmentView assignment = Assert.Single(context.Assignments);
        Assert.Equal(assignedScenario, assignment.ContentId);
        Assert.DoesNotContain(context.Assignments, item => item.ContentId == foreignScenario);
    }

    [Fact]
    public async Task ExpiredMembershipRemovesCatalogAccess()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        Guid userId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        _ = await service.UpsertUnitAsync("default", unitId, null, "Classe A", "Class", "A", true, null, default);
        _ = await service.UpsertMembershipAsync("default", Guid.NewGuid(), unitId, userId, null, MembershipKind.Participant, Now.AddDays(-2), Now.AddDays(-1), true, null, default);

        PlayerOrganizationContextView context = await service.ResolvePlayerContextAsync("default", userId, default);

        Assert.False(context.IsMember);
        Assert.Empty(context.UnitIds);
        Assert.Empty(context.Assignments);
    }

    [Fact]
    public async Task HierarchyRejectsACycleWithinAFront()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        Guid parentId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        UnitView parent = await service.UpsertUnitAsync("default", parentId, null, "Parent", "Group", "P", true, null, default);
        _ = await service.UpsertUnitAsync("default", childId, parentId, "Child", "Group", "C", true, null, default);

        OrganizationException error = await Assert.ThrowsAsync<OrganizationException>(() =>
            service.UpsertUnitAsync("default", parentId, childId, "Parent", "Group", "P", true, parent.Revision, default));

        Assert.Equal("unit_cycle", error.Code);
    }

    [Fact]
    public async Task MembershipImportCanBePreviewedThenAppliedIdempotently()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        Guid unitId = Guid.NewGuid();
        Guid periodId = Guid.NewGuid();
        MembershipImportRow row = new(Guid.NewGuid(), unitId, Guid.NewGuid(), periodId, MembershipKind.Participant, Now, Now.AddMonths(6));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        _ = await service.UpsertUnitAsync("default", unitId, null, "Classe A", "Class", "A", true, null, default);
        _ = await service.UpsertPeriodAsync("default", periodId, "Année 2026-2027", "2026-27", Now, Now.AddYears(1), true, null, default);

        MembershipImportView preview = await service.ImportMembershipsAsync("default", [row], true, default);
        MembershipImportView applied = await service.ImportMembershipsAsync("default", [row], false, default);
        MembershipImportView replay = await service.ImportMembershipsAsync("default", [row], false, default);
        MembershipImportView conflictingNaturalKey = await service.ImportMembershipsAsync("default", [row with { Id = Guid.NewGuid() }], true, default);

        Assert.Equal(1, preview.Created);
        Assert.Equal(1, applied.Created);
        Assert.Equal(1, replay.Unchanged);
        Assert.Empty(replay.Errors);
        Assert.Equal("import_membership_conflict", Assert.Single(conflictingNaturalKey.Errors).Code);
    }

    [Fact]
    public async Task DisabledMembershipImportRejectsTheOperation()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now), new MembershipImportPolicy(false, 500));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);

        OrganizationException error = await Assert.ThrowsAsync<OrganizationException>(() => service.ImportMembershipsAsync(
            "default",
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, MembershipKind.Participant, Now, null)],
            true,
            default));

        Assert.Equal("membership_import_disabled", error.Code);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task UnitsArePaginatedFlatAndKeepTheParentIdSoTheClientCanRebuildTheTree()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        Guid rootId = Guid.NewGuid();
        _ = await service.UpsertUnitAsync("default", rootId, null, "Académie centrale", "Campus", "ROOT", true, null, default);
        for (int index = 0; index < 60; index++)
        {
            _ = await service.UpsertUnitAsync("default", Guid.NewGuid(), rootId, $"Classe {index:D2}", "Class", $"C{index:D2}", true, null, default);
        }

        PagedView<UnitView> firstPage = await service.ListUnitsAsync("default", null, 1, 25, default);
        PagedView<UnitView> lastPage = await service.ListUnitsAsync("default", null, 3, 25, default);

        Assert.Equal(61, firstPage.Total);
        Assert.Equal(25, firstPage.Items.Count);

        // Dernière page partielle : 61 = 25 + 25 + 11.
        Assert.Equal(11, lastPage.Items.Count);

        // La pagination est à plat : chaque unité porte son parent, l'arbre reste reconstructible.
        Assert.All(firstPage.Items.Where(item => item.Id != rootId), item => Assert.Equal(rootId, item.ParentId));
    }

    [Fact]
    public async Task UnitAndPeriodSearchFiltersOnNameAndCode()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        _ = await service.UpsertUnitAsync("default", Guid.NewGuid(), null, "Classe Alpha", "Class", "ALPHA", true, null, default);
        _ = await service.UpsertUnitAsync("default", Guid.NewGuid(), null, "Classe Beta", "Class", "BETA", true, null, default);
        _ = await service.UpsertPeriodAsync("default", Guid.NewGuid(), "Trimestre Alpha", "T-ALPHA", Now, Now.AddDays(90), true, null, default);
        _ = await service.UpsertPeriodAsync("default", Guid.NewGuid(), "Trimestre Beta", "T-BETA", Now, Now.AddDays(90), true, null, default);

        PagedView<UnitView> byName = await service.ListUnitsAsync("default", "alpha", null, null, default);
        PagedView<UnitView> byCode = await service.ListUnitsAsync("default", "BETA", null, null, default);
        PagedView<PeriodView> periods = await service.ListPeriodsAsync("default", "t-alpha", null, null, default);

        Assert.Equal("Classe Alpha", Assert.Single(byName.Items).Name);
        Assert.Equal(1, byName.Total);
        Assert.Equal("Classe Beta", Assert.Single(byCode.Items).Name);
        Assert.Equal("Trimestre Alpha", Assert.Single(periods.Items).Name);
    }

    [Fact]
    public async Task MembershipSearchFiltersOnTheAttachedUnit()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        Guid alpha = Guid.NewGuid();
        Guid beta = Guid.NewGuid();
        _ = await service.UpsertUnitAsync("default", alpha, null, "Classe Alpha", "Class", "ALPHA", true, null, default);
        _ = await service.UpsertUnitAsync("default", beta, null, "Classe Beta", "Class", "BETA", true, null, default);
        _ = await service.UpsertMembershipAsync("default", Guid.NewGuid(), alpha, Guid.NewGuid(), null, MembershipKind.Participant, Now, null, true, null, default);
        _ = await service.UpsertMembershipAsync("default", Guid.NewGuid(), beta, Guid.NewGuid(), null, MembershipKind.Participant, Now, null, true, null, default);

        PagedView<MembershipView> matching = await service.ListMembershipsAsync("default", null, null, null, "alpha", null, null, default);
        PagedView<MembershipView> all = await service.ListMembershipsAsync("default", null, null, null, null, null, null, default);

        Assert.Equal(alpha, Assert.Single(matching.Items).UnitId);
        Assert.Equal(1, matching.Total);
        Assert.Equal(2, all.Total);
    }

    [Fact]
    public async Task OrganizationListsClampPageSizeAndReturnEmptyPagesBeyondTheLastOne()
    {
        MemoryOrganizationRepository repository = new();
        OrganizationService service = new(repository, new FixedTimeProvider(Now));
        _ = await service.UpsertFrontAsync("default", "Académie", "School", true, null, default);
        for (int index = 0; index < 5; index++)
        {
            _ = await service.UpsertUnitAsync("default", Guid.NewGuid(), null, $"Classe {index}", "Class", $"C{index}", true, null, default);
        }

        PagedView<UnitView> oversized = await service.ListUnitsAsync("default", null, 1, 10_000, default);
        PagedView<UnitView> beyondLast = await service.ListUnitsAsync("default", null, 12, 10, default);
        PagedView<UnitView> defaults = await service.ListUnitsAsync("default", null, null, null, default);

        Assert.Equal(OrganizationService.MaxPageSize, oversized.PageSize);
        Assert.Equal(5, oversized.Total);
        Assert.Empty(beyondLast.Items);
        Assert.Equal(5, beyondLast.Total);
        Assert.Equal(OrganizationService.DefaultPageSize, defaults.PageSize);
    }

    private sealed class MemoryOrganizationRepository : IOrganizationRepository
    {
        private readonly List<OrganizationFront> fronts = [];
        private readonly List<OperatingPeriod> periods = [];
        private readonly List<OrganizationUnit> units = [];
        private readonly List<Membership> memberships = [];
        private readonly List<ContentAssignment> assignments = [];
        private static string Normalize(string frontId) => frontId.Trim().ToLowerInvariant();

        public Task<OrganizationFront?> GetFrontAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(fronts.SingleOrDefault(item => item.FrontId == Normalize(frontId)));
        public Task<OperatingPeriod?> GetPeriodAsync(string frontId, Guid id, CancellationToken cancellationToken) => Task.FromResult(periods.SingleOrDefault(item => item.FrontId == Normalize(frontId) && item.Id == id));
        public Task<OrganizationUnit?> GetUnitAsync(string frontId, Guid id, CancellationToken cancellationToken) => Task.FromResult(units.SingleOrDefault(item => item.FrontId == Normalize(frontId) && item.Id == id));
        public Task<Membership?> GetMembershipAsync(string frontId, Guid id, CancellationToken cancellationToken) => Task.FromResult(memberships.SingleOrDefault(item => item.FrontId == Normalize(frontId) && item.Id == id));
        public Task<Membership?> GetMembershipByNaturalKeyAsync(string frontId, Guid userId, Guid unitId, DateTimeOffset startsAt, CancellationToken cancellationToken) => Task.FromResult(memberships.SingleOrDefault(item => item.FrontId == Normalize(frontId) && item.UserId == userId && item.UnitId == unitId && item.StartsAt == startsAt));
        public Task<ContentAssignment?> GetAssignmentAsync(string frontId, Guid id, CancellationToken cancellationToken) => Task.FromResult(assignments.SingleOrDefault(item => item.FrontId == Normalize(frontId) && item.Id == id));
        public Task<IReadOnlyList<OrganizationUnit>> ListUnitsAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrganizationUnit>>(units.Where(item => item.FrontId == Normalize(frontId)).ToArray());
        public Task<(IReadOnlyList<OrganizationUnit> Items, int Total)> SearchUnitsAsync(string frontId, string? query, int offset, int limit, CancellationToken cancellationToken)
        {
            OrganizationUnit[] found = [.. units
                .Where(item => item.FrontId == Normalize(frontId))
                .Where(item => query is null || Matches(item.Name, query) || Matches(item.Code, query))
                .OrderBy(static item => item.Name)
                .ThenBy(static item => item.Id)];
            return Task.FromResult(((IReadOnlyList<OrganizationUnit>)[.. found.Skip(offset).Take(limit)], found.Length));
        }
        public Task<(IReadOnlyList<OperatingPeriod> Items, int Total)> SearchPeriodsAsync(string frontId, string? query, int offset, int limit, CancellationToken cancellationToken)
        {
            OperatingPeriod[] found = [.. periods
                .Where(item => item.FrontId == Normalize(frontId))
                .Where(item => query is null || Matches(item.Name, query) || Matches(item.Code, query))
                .OrderByDescending(static item => item.StartsAt)
                .ThenBy(static item => item.Id)];
            return Task.FromResult(((IReadOnlyList<OperatingPeriod>)[.. found.Skip(offset).Take(limit)], found.Length));
        }
        public Task<(IReadOnlyList<Membership> Items, int Total)> ListMembershipsAsync(string frontId, Guid? unitId, Guid? userId, MembershipKind? kind, string? query, int offset, int limit, CancellationToken cancellationToken)
        {
            Membership[] found = [.. memberships
                .Where(item => item.FrontId == Normalize(frontId) && (unitId is null || item.UnitId == unitId) && (userId is null || item.UserId == userId) && (kind is null || item.Kind == kind))
                .Where(item => query is null || units.Any(unit => unit.Id == item.UnitId && unit.FrontId == Normalize(frontId) && (Matches(unit.Name, query) || Matches(unit.Code, query))))
                .OrderByDescending(static item => item.IsActive)
                .ThenBy(static item => item.UserId)
                .ThenBy(static item => item.Id)];
            return Task.FromResult(((IReadOnlyList<Membership>)[.. found.Skip(offset).Take(limit)], found.Length));
        }
        private static bool Matches(string value, string query) => value.Contains(query, StringComparison.OrdinalIgnoreCase);
        public Task<(IReadOnlyList<ContentAssignment> Items, int Total)> ListAssignmentsAsync(string frontId, Guid? unitId, AssignedContentType? contentType, int offset, int limit, CancellationToken cancellationToken)
        {
            ContentAssignment[] found = assignments.Where(item => item.FrontId == Normalize(frontId) && (unitId is null || item.UnitId == unitId) && (contentType is null || item.ContentType == contentType)).ToArray();
            return Task.FromResult(((IReadOnlyList<ContentAssignment>)found.Skip(offset).Take(limit).ToArray(), found.Length));
        }
        public Task<IReadOnlyList<Membership>> ListEffectiveMembershipsAsync(string frontId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Membership>>(memberships.Where(item => item.FrontId == Normalize(frontId) && item.UserId == userId && item.IsEffectiveAt(now)).ToArray());
        public Task<IReadOnlyList<ContentAssignment>> ListEffectiveAssignmentsAsync(string frontId, IReadOnlyCollection<Guid> unitIds, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ContentAssignment>>(assignments.Where(item => item.FrontId == Normalize(frontId) && unitIds.Contains(item.UnitId) && item.IsAvailableAt(now)).ToArray());
        public Task AddFrontAsync(OrganizationFront front, CancellationToken cancellationToken) { fronts.Add(front); return Task.CompletedTask; }
        public Task AddPeriodAsync(OperatingPeriod period, CancellationToken cancellationToken) { periods.Add(period); return Task.CompletedTask; }
        public Task AddUnitAsync(OrganizationUnit unit, CancellationToken cancellationToken) { units.Add(unit); return Task.CompletedTask; }
        public Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken) { memberships.Add(membership); return Task.CompletedTask; }
        public Task AddAssignmentAsync(ContentAssignment assignment, CancellationToken cancellationToken) { assignments.Add(assignment); return Task.CompletedTask; }
        public void RemoveMembership(Membership membership) => memberships.Remove(membership);
        public void RemoveAssignment(ContentAssignment assignment) => assignments.Remove(assignment);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}