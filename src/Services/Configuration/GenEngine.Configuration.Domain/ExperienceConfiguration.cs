namespace GenEngine.Configuration.Domain;

public sealed class ExperienceConfiguration
{
    private ExperienceConfiguration() { }

    private ExperienceConfiguration(Guid id, string frontId, string documentJson, DateTimeOffset now)
    {
        Id = id;
        FrontId = frontId;
        DocumentJson = documentJson;
        Revision = 1;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string FrontId { get; private set; } = string.Empty;
    public string DocumentJson { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public int PublishedVersion { get; private set; }
    public string? PublishedJson { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static ExperienceConfiguration Create(string frontId, string documentJson, DateTimeOffset now)
    {
        EnsureFrontId(frontId);
        EnsureDocument(documentJson);
        return new ExperienceConfiguration(Guid.NewGuid(), frontId.Trim().ToLowerInvariant(), documentJson, now);
    }

    public void Update(string documentJson, int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        EnsureDocument(documentJson);
        DocumentJson = documentJson;
        Revision = checked(Revision + 1);
        UpdatedAt = now;
    }

    public void Publish(int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        PublishedJson = DocumentJson;
        PublishedVersion = checked(PublishedVersion + 1);
        PublishedAt = now;
        Revision = checked(Revision + 1);
        UpdatedAt = now;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (Revision != expectedRevision)
        {
            throw new ConfigurationDomainException("revision_conflict", $"Expected revision {expectedRevision}, but the current revision is {Revision}.");
        }
    }

    private static void EnsureFrontId(string frontId)
    {
        if (string.IsNullOrWhiteSpace(frontId) || frontId.Trim().Length > 80)
        {
            throw new ConfigurationDomainException("invalid_front_id", "A front identifier of at most 80 characters is required.");
        }
    }

    private static void EnsureDocument(string documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new ConfigurationDomainException("invalid_configuration", "A configuration document is required.");
        }
    }
}

public sealed class ConfigurationDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}