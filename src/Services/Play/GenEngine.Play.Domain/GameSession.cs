namespace GenEngine.Play.Domain;

public sealed class GameSession
{
    private GameSession()
    {
    }

    private GameSession(
        Guid id,
        string ownerId,
        Guid scenarioId,
        Guid scenarioVersionId,
        string snapshotHash,
        string snapshotJson,
        string stateJson,
        string status,
        ulong seed,
        DateTimeOffset now)
    {
        Id = id;
        OwnerId = ownerId;
        ScenarioId = scenarioId;
        ScenarioVersionId = scenarioVersionId;
        SnapshotHash = snapshotHash;
        SnapshotJson = snapshotJson;
        StateJson = stateJson;
        Status = status;
        Seed = seed;
        Revision = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid ScenarioVersionId { get; private set; }

    public Guid ScenarioId { get; private set; }

    public string SnapshotHash { get; private set; } = string.Empty;

    public string SnapshotJson { get; private set; } = string.Empty;

    public string StateJson { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public ulong Seed { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static GameSession Create(
        string ownerId,
        Guid scenarioId,
        Guid scenarioVersionId,
        string snapshotHash,
        string snapshotJson,
        string stateJson,
        string status,
        ulong seed,
        DateTimeOffset now) =>
        new(Guid.NewGuid(), ownerId, scenarioId, scenarioVersionId, snapshotHash, snapshotJson, stateJson, status, seed, now);

    public void ChangeState(string stateJson, string status, int expectedRevision, DateTimeOffset now)
    {
        EnsureRevision(expectedRevision);
        StateJson = stateJson;
        Status = status;
        Revision = checked(Revision + 1);
        UpdatedAt = now;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (Revision != expectedRevision)
        {
            throw new PlayDomainException(
                "revision_conflict",
                $"Expected revision {expectedRevision}, but the current revision is {Revision}.");
        }
    }
}

public sealed class ProcessedCommand
{
    private ProcessedCommand()
    {
    }

    private ProcessedCommand(Guid commandId, Guid sessionId, string responseJson, DateTimeOffset processedAt)
    {
        CommandId = commandId;
        SessionId = sessionId;
        ResponseJson = responseJson;
        ProcessedAt = processedAt;
    }

    public Guid CommandId { get; private set; }

    public Guid SessionId { get; private set; }

    public string ResponseJson { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedCommand Create(
        Guid commandId,
        Guid sessionId,
        string responseJson,
        DateTimeOffset processedAt) =>
        new(commandId, sessionId, responseJson, processedAt);
}

public sealed class PlayDomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
