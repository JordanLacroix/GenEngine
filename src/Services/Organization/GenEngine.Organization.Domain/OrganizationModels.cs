namespace GenEngine.Organization.Domain;

public enum MembershipKind { Participant, Supervisor }
public enum AssignedContentType { Journey, Category, Scenario }

public sealed class OperatingPeriod
{
    private OperatingPeriod() { }
    private OperatingPeriod(Guid id, string frontId, string name, string code, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FrontId = OrganizationFront.NormalizeFrontId(frontId);
        Apply(name, code, startsAt, endsAt, true, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static OperatingPeriod Create(Guid id, string frontId, string name, string code, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now) => new(id, frontId, name, code, startsAt, endsAt, now);

    public void Update(string name, string code, DateTimeOffset startsAt, DateTimeOffset endsAt, bool isActive, int expectedRevision, DateTimeOffset now)
    {
        if (Revision != expectedRevision) throw new OrganizationDomainException("revision_conflict", "The operating period was modified concurrently.");
        Apply(name, code, startsAt, endsAt, isActive, now);
        Revision++;
    }

    public bool Contains(DateTimeOffset instant) => IsActive && StartsAt <= instant && EndsAt >= instant;

    private void Apply(string name, string code, DateTimeOffset startsAt, DateTimeOffset endsAt, bool isActive, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160 || string.IsNullOrWhiteSpace(code) || code.Trim().Length > 60 || endsAt <= startsAt)
            throw new OrganizationDomainException("invalid_period", "An operating period requires a name, a code and a valid date range.");
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class OrganizationFront
{
    private OrganizationFront() { }
    private OrganizationFront(string frontId, string name, string type, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FrontId = NormalizeFrontId(frontId);
        Rename(name, type, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrganizationFront Create(string frontId, string name, string type, DateTimeOffset now) => new(frontId, name, type, now);

    public void Update(string name, string type, bool isActive, int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        Rename(name, type, now);
        IsActive = isActive;
        Revision++;
    }

    private void Rename(string name, string type, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160 || string.IsNullOrWhiteSpace(type) || type.Trim().Length > 40)
            throw new OrganizationDomainException("invalid_front", "Front name and type are required and must fit their limits.");
        Name = name.Trim();
        Type = type.Trim();
        UpdatedAt = now;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (Revision != expectedRevision) throw new OrganizationDomainException("revision_conflict", "The front was modified concurrently.");
    }

    internal static string NormalizeFrontId(string frontId)
    {
        string value = frontId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length is < 1 or > 80 || value.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new OrganizationDomainException("invalid_front_id", "Front identifiers accept 1 to 80 letters, digits, dashes or underscores.");
        return value;
    }
}

public sealed class OrganizationUnit
{
    private OrganizationUnit() { }
    private OrganizationUnit(Guid id, string frontId, Guid? parentId, string name, string type, string code, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FrontId = OrganizationFront.NormalizeFrontId(frontId);
        ParentId = parentId;
        Apply(name, type, code, true, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrganizationUnit Create(Guid id, string frontId, Guid? parentId, string name, string type, string code, DateTimeOffset now) => new(id, frontId, parentId, name, type, code, now);

    public void Update(Guid? parentId, string name, string type, string code, bool isActive, int expectedRevision, DateTimeOffset now)
    {
        if (Revision != expectedRevision) throw new OrganizationDomainException("revision_conflict", "The unit was modified concurrently.");
        if (parentId == Id) throw new OrganizationDomainException("unit_cycle", "A unit cannot be its own parent.");
        ParentId = parentId;
        Apply(name, type, code, isActive, now);
        Revision++;
    }

    private void Apply(string name, string type, string code, bool isActive, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160 || string.IsNullOrWhiteSpace(type) || type.Trim().Length > 60 || string.IsNullOrWhiteSpace(code) || code.Trim().Length > 60)
            throw new OrganizationDomainException("invalid_unit", "Unit name, type and code are required and must fit their limits.");
        Name = name.Trim();
        Type = type.Trim();
        Code = code.Trim().ToUpperInvariant();
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class Membership
{
    private Membership() { }
    private Membership(Guid id, string frontId, Guid unitId, Guid userId, Guid? periodId, MembershipKind kind, DateTimeOffset startsAt, DateTimeOffset? endsAt, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FrontId = OrganizationFront.NormalizeFrontId(frontId);
        UnitId = unitId;
        UserId = userId;
        PeriodId = periodId;
        Apply(kind, startsAt, endsAt, true, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public Guid UnitId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? PeriodId { get; private set; }
    public MembershipKind Kind { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Membership Create(Guid id, string frontId, Guid unitId, Guid userId, Guid? periodId, MembershipKind kind, DateTimeOffset startsAt, DateTimeOffset? endsAt, DateTimeOffset now) => new(id, frontId, unitId, userId, periodId, kind, startsAt, endsAt, now);

    public void Update(MembershipKind kind, DateTimeOffset startsAt, DateTimeOffset? endsAt, bool isActive, int expectedRevision, DateTimeOffset now)
    {
        if (Revision != expectedRevision) throw new OrganizationDomainException("revision_conflict", "The membership was modified concurrently.");
        Apply(kind, startsAt, endsAt, isActive, now);
        Revision++;
    }

    public bool IsEffectiveAt(DateTimeOffset now) => IsActive && StartsAt <= now && (EndsAt is null || EndsAt > now);

    private void Apply(MembershipKind kind, DateTimeOffset startsAt, DateTimeOffset? endsAt, bool isActive, DateTimeOffset now)
    {
        if (UnitId == Guid.Empty || UserId == Guid.Empty || endsAt <= startsAt)
            throw new OrganizationDomainException("invalid_membership", "Membership requires a user, a unit and a valid period.");
        Kind = kind;
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class ContentAssignment
{
    private ContentAssignment() { }
    private ContentAssignment(Guid id, string frontId, Guid unitId, AssignedContentType contentType, Guid contentId, string name, bool required, DateTimeOffset? availableFrom, DateTimeOffset? dueAt, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FrontId = OrganizationFront.NormalizeFrontId(frontId);
        UnitId = unitId;
        ContentType = contentType;
        ContentId = contentId;
        Apply(name, required, availableFrom, dueAt, true, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public Guid UnitId { get; private set; }
    public AssignedContentType ContentType { get; private set; }
    public Guid ContentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool Required { get; private set; }
    public DateTimeOffset? AvailableFrom { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ContentAssignment Create(Guid id, string frontId, Guid unitId, AssignedContentType contentType, Guid contentId, string name, bool required, DateTimeOffset? availableFrom, DateTimeOffset? dueAt, DateTimeOffset now) => new(id, frontId, unitId, contentType, contentId, name, required, availableFrom, dueAt, now);

    public void Update(string name, bool required, DateTimeOffset? availableFrom, DateTimeOffset? dueAt, bool isActive, int expectedRevision, DateTimeOffset now)
    {
        if (Revision != expectedRevision) throw new OrganizationDomainException("revision_conflict", "The assignment was modified concurrently.");
        Apply(name, required, availableFrom, dueAt, isActive, now);
        Revision++;
    }

    public bool IsAvailableAt(DateTimeOffset now) => IsActive && (AvailableFrom is null || AvailableFrom <= now) && (DueAt is null || DueAt >= now);

    private void Apply(string name, bool required, DateTimeOffset? availableFrom, DateTimeOffset? dueAt, bool isActive, DateTimeOffset now)
    {
        if (UnitId == Guid.Empty || ContentId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160 || dueAt < availableFrom)
            throw new OrganizationDomainException("invalid_assignment", "Assignment requires content, a unit, a name and a valid availability window.");
        Name = name.Trim();
        Required = required;
        AvailableFrom = availableFrom;
        DueAt = dueAt;
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class OrganizationDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}