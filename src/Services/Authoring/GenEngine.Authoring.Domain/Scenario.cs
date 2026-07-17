namespace GenEngine.Authoring.Domain;

public sealed class Scenario
{
    private readonly List<ScenarioVersion> versions = [];

    private Scenario()
    {
    }

    private Scenario(
        Guid id,
        string ownerId,
        string title,
        string draftJson,
        DateTimeOffset now,
        string frontId,
        Guid? categoryId,
        string creationBrief)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        DraftJson = draftJson;
        FrontId = frontId;
        CategoryId = categoryId;
        CreationBrief = creationBrief;
        Revision = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string DraftJson { get; private set; } = string.Empty;

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
    public string FrontId { get; private set; } = "default";
    public Guid? CategoryId { get; private set; }
    public string CreationBrief { get; private set; } = string.Empty;

    public IReadOnlyList<ScenarioVersion> Versions => versions;

    public static Scenario Create(
        string ownerId,
        string title,
        string draftJson,
        DateTimeOffset now,
        string frontId = "default",
        Guid? categoryId = null,
        string creationBrief = "") =>
        new(Guid.NewGuid(), ownerId, title, draftJson, now, frontId, categoryId, creationBrief);

    public void UpdateDraft(string title, string draftJson, int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        Title = title;
        DraftJson = draftJson;
        Revision = checked(Revision + 1);
        UpdatedAt = now;
    }

    public ScenarioVersion Publish(
        string snapshotJson,
        string snapshotHash,
        int expectedRevision,
        DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        int versionNumber = versions.Count == 0 ? 1 : versions.Max(static version => version.Number) + 1;
        ScenarioVersion version = ScenarioVersion.Create(Id, versionNumber, snapshotJson, snapshotHash, now);
        versions.Add(version);
        Revision = checked(Revision + 1);
        UpdatedAt = now;
        return version;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (Revision != expectedRevision)
        {
            throw new AuthoringDomainException(
                "revision_conflict",
                $"Expected revision {expectedRevision}, but the current revision is {Revision}.");
        }
    }
}

public sealed class ScenarioVersion
{
    private ScenarioVersion()
    {
    }

    private ScenarioVersion(
        Guid id,
        Guid scenarioId,
        int number,
        string snapshotJson,
        string snapshotHash,
        DateTimeOffset publishedAt)
    {
        Id = id;
        ScenarioId = scenarioId;
        Number = number;
        SnapshotJson = snapshotJson;
        SnapshotHash = snapshotHash;
        PublishedAt = publishedAt;
    }

    public Guid Id { get; private set; }

    public Guid ScenarioId { get; private set; }

    public int Number { get; private set; }

    public string SnapshotJson { get; private set; } = string.Empty;

    public string SnapshotHash { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    internal static ScenarioVersion Create(
        Guid scenarioId,
        int number,
        string snapshotJson,
        string snapshotHash,
        DateTimeOffset publishedAt) =>
        new(Guid.NewGuid(), scenarioId, number, snapshotJson, snapshotHash, publishedAt);
}

public sealed class AuthoringDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}