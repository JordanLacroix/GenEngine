using GenEngine.Narrative;
using GenEngine.Play.Domain;

namespace GenEngine.Play.Application;

public interface IAuthoringSnapshotClient
{
    Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken);
}

public interface IPlayRepository
{
    Task AddAsync(GameSession session, CancellationToken cancellationToken);

    Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task<ProcessedCommand?> GetProcessedCommandAsync(
        Guid sessionId,
        Guid commandId,
        CancellationToken cancellationToken);

    Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record PublishedSnapshotContract(
    Guid Id,
    Guid ScenarioId,
    int Number,
    string SnapshotJson,
    string SnapshotHash);

public sealed record SessionView(
    Guid Id,
    Guid ScenarioId,
    Guid ScenarioVersionId,
    string SnapshotHash,
    SessionStatus Status,
    int Revision,
    int Turn);

public sealed record RewardDispatch(string Trigger, string ReferenceId, string IdempotencyKey);
public sealed record ProgressDispatch(string IdempotencyKey, string Type, string Title, string Summary, Guid ScenarioId, Guid ScenarioVersionId, Guid SessionId, string ReferenceId, string? ChoiceId, string TargetNodeId, string? EndingId, bool Completed, int TotalObjectives);
public sealed record InputResult(SessionView Session, CurrentStep CurrentStep, bool Replayed, IReadOnlyList<RewardDispatch> Rewards, ProgressDispatch Progress);

public interface IRewardDispatcher
{
    Task DispatchAsync(string userId, IReadOnlyList<RewardDispatch> rewards, CancellationToken cancellationToken);
    Task DispatchProgressAsync(string userId, ProgressDispatch progress, CancellationToken cancellationToken);
}

public sealed class PlayService(
    IPlayRepository repository,
    IAuthoringSnapshotClient authoringClient,
    TimeProvider timeProvider)
{
    public async Task<SessionView> StartAsync(
        string ownerId,
        Guid scenarioVersionId,
        ulong seed,
        CancellationToken cancellationToken)
    {
        PublishedSnapshotContract snapshot = await authoringClient.GetAsync(scenarioVersionId, cancellationToken)
            .ConfigureAwait(false);
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(snapshot.SnapshotJson);
        string actualHash = CanonicalSnapshot.ComputeHash(scenario);
        if (!string.Equals(actualHash, snapshot.SnapshotHash, StringComparison.Ordinal))
        {
            throw new PlayException("snapshot_hash_mismatch", "The published snapshot hash is invalid.");
        }

        GameState state = NarrativeRuntime.Start(scenario);
        DateTimeOffset now = GetUtcNow();
        GameSave save = GameSaveSerializer.Create(scenario.SchemaVersion, seed, now, state);
        GameSession session = GameSession.Create(
            ownerId,
            snapshot.ScenarioId,
            snapshot.Id,
            snapshot.SnapshotHash,
            snapshot.SnapshotJson,
            GameSaveSerializer.Serialize(save),
            state.Status.ToString(),
            seed,
            now);
        await repository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(session, state);
    }

    public async Task<SessionView> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return Map(session, DeserializeState(session));
    }

    public async Task<CurrentStep> GetCurrentStepAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return NarrativeRuntime.GetCurrentStep(DeserializeScenario(session), DeserializeState(session));
    }

    public async Task<NarrativeTree> GetTreeAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return NarrativeTreeBuilder.Build(DeserializeScenario(session), DeserializeState(session));
    }

    public async Task<PlayerProjection> GetPlayerProjectionAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return PlayerProjectionBuilder.Build(DeserializeState(session));
    }

    public async Task<InputResult> SubmitChoiceAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        string choiceId,
        CancellationToken cancellationToken) =>
        await SubmitInputAsync(
            id,
            ownerId,
            commandId,
            expectedRevision,
            choiceId,
            "ChoiceSelected",
            (scenario, state) => NarrativeRuntime.SubmitChoice(scenario, state, choiceId),
            cancellationToken).ConfigureAwait(false);

    public async Task<InputResult> ContinueAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        CancellationToken cancellationToken) =>
        await SubmitInputAsync(
            id,
            ownerId,
            commandId,
            expectedRevision,
            "continue",
            "NarrationContinued",
            NarrativeRuntime.Continue,
            cancellationToken).ConfigureAwait(false);

    public async Task<InputResult> SubmitAnswerAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        string answerId,
        CancellationToken cancellationToken) =>
        await SubmitInputAsync(
            id,
            ownerId,
            commandId,
            expectedRevision,
            answerId,
            "QuizAnswered",
            (scenario, state) => NarrativeRuntime.SubmitAnswer(scenario, state, answerId),
            cancellationToken).ConfigureAwait(false);

    public async Task<InputResult> SubmitTextAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        string text,
        CancellationToken cancellationToken) =>
        await SubmitInputAsync(
            id,
            ownerId,
            commandId,
            expectedRevision,
            "free-text",
            "TextSubmitted",
            (scenario, state) => NarrativeRuntime.SubmitText(scenario, state, text),
            cancellationToken).ConfigureAwait(false);

    public async Task<InputResult> ConfirmTextAnalysisAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        bool confirmed,
        CancellationToken cancellationToken) =>
        await SubmitInputAsync(
            id,
            ownerId,
            commandId,
            expectedRevision,
            confirmed ? "confirm" : "retry",
            "TextAnalysisConfirmed",
            (scenario, state) => NarrativeRuntime.ConfirmTextAnalysis(scenario, state, confirmed),
            cancellationToken).ConfigureAwait(false);

    private async Task<InputResult> SubmitInputAsync(
        Guid id,
        string ownerId,
        Guid commandId,
        int expectedRevision,
        string inputId,
        string eventType,
        Func<ScenarioDocument, GameState, GameState> transition,
        CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        ProcessedCommand? processed = await repository.GetProcessedCommandAsync(id, commandId, cancellationToken)
            .ConfigureAwait(false);
        if (processed is not null)
        {
            InputResult replayed = NarrativeJson.Deserialize<InputResult>(processed.ResponseJson);
            return replayed with { Replayed = true };
        }

        ScenarioDocument scenario = DeserializeScenario(session);
        GameState currentState = DeserializeState(session);
        GameState nextState = transition(scenario, currentState);
        DateTimeOffset now = GetUtcNow();
        GameSave save = GameSaveSerializer.Create(scenario.SchemaVersion, session.Seed, now, nextState);
        session.ChangeState(
            GameSaveSerializer.Serialize(save),
            nextState.Status.ToString(),
            expectedRevision,
            now);
        bool completed = nextState.Status == SessionStatus.Completed;
        string? endingId = completed ? nextState.CurrentNodeId : null;
        int totalObjectives = scenario.Nodes.Sum(static node =>
            node.Choices.Count
            + (node.Interactions?.OfType<ChoiceSetInteraction>().Sum(static interaction => interaction.Choices.Count) ?? 0)
            + (node.Interactions?.OfType<QuizInteraction>().Sum(static interaction => interaction.Answers.Count) ?? 0))
            + scenario.Nodes.Count(static node => node.IsEnding);
        InputResult result = new(
            Map(session, nextState),
            NarrativeRuntime.GetCurrentStep(scenario, nextState),
            false,
            nextState.World.ExternalEvents
                .Skip(currentState.World.ExternalEvents.Count)
                .Where(static item => string.Equals(item.EventName, "economy.reward", StringComparison.OrdinalIgnoreCase))
                .Where(static item => item.Attributes.ContainsKey("trigger") && item.Attributes.ContainsKey("referenceId"))
                .Select(item => new RewardDispatch(
                    item.Attributes["trigger"],
                    item.Attributes["referenceId"],
                    $"session:{id}:external:{item.Sequence}"))
                .ToArray(),
            new ProgressDispatch(
                $"session:{id}:command:{commandId}",
                completed ? "ScenarioCompleted" : eventType,
                completed ? $"{scenario.Title} terminé" : scenario.Title,
                completed ? "Vous avez atteint une fin. Votre chemin est conservé dans le journal." : "Votre choix a ouvert une nouvelle branche de l'histoire.",
                session.ScenarioId,
                session.ScenarioVersionId,
                session.Id,
                inputId,
                eventType == "ChoiceSelected" ? inputId : null,
                nextState.CurrentNodeId,
                endingId,
                completed,
                Math.Max(1, totalObjectives)));
        await repository.AddProcessedCommandAsync(
            ProcessedCommand.Create(commandId, id, NarrativeJson.Serialize(result), now),
            cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public Task<SessionView> PauseAsync(
        Guid id,
        string ownerId,
        int expectedRevision,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, ownerId, expectedRevision, true, cancellationToken);

    public Task<SessionView> ResumeAsync(
        Guid id,
        string ownerId,
        int expectedRevision,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, ownerId, expectedRevision, false, cancellationToken);

    private async Task<SessionView> ChangeStatusAsync(
        Guid id,
        string ownerId,
        int expectedRevision,
        bool pause,
        CancellationToken cancellationToken)
    {
        GameSession session = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        GameSave currentSave = DeserializeSave(session);
        GameState currentState = currentSave.State;
        GameState nextState = pause ? NarrativeRuntime.Pause(currentState) : NarrativeRuntime.Resume(currentState);
        DateTimeOffset now = GetUtcNow();
        session.ChangeState(
            GameSaveSerializer.Serialize(currentSave with { State = nextState, SavedAt = now }),
            nextState.Status.ToString(),
            expectedRevision,
            now);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(session, nextState);
    }

    private async Task<GameSession> GetRequiredAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false)
        ?? throw new PlayException("session_not_found", "The game session was not found.");

    private static ScenarioDocument DeserializeScenario(GameSession session) =>
        NarrativeJson.Deserialize<ScenarioDocument>(session.SnapshotJson);

    private static GameSave DeserializeSave(GameSession session) =>
        GameSaveSerializer.Deserialize(session.StateJson, session.Seed, session.UpdatedAt);

    private static GameState DeserializeState(GameSession session) => DeserializeSave(session).State;

    private DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();

    private static SessionView Map(GameSession session, GameState state) =>
        new(
            session.Id,
            session.ScenarioId,
            session.ScenarioVersionId,
            session.SnapshotHash,
            state.Status,
            session.Revision,
            state.Turn);
}

public sealed class PlayException : InvalidOperationException
{
    public PlayException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public PlayException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
