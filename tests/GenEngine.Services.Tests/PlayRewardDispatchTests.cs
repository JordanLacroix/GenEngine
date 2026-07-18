using GenEngine.Narrative;
using GenEngine.Play.Application;
using GenEngine.Play.Domain;

namespace GenEngine.Services.Tests;

public sealed class PlayRewardDispatchTests
{
    [Fact]
    public async Task ExternalRewardEventsProduceStableIdempotentDispatches()
    {
        ScenarioDocument scenario = new(
            NarrativeVersions.LatestSchema,
            "Reward story",
            "opening",
            [
                new NarrativeNode("opening", "Choose", null, [],
                [
                    new NarrativeChoice("courageous-choice", "Go", "ending", null,
                    [
                        new EmitExternalEventEffect("economy.reward", new Dictionary<string, string>
                        {
                            ["trigger"] = "ChoiceSelected",
                            ["referenceId"] = "courageous-choice",
                        }),
                    ]),
                ]),
                new NarrativeNode("ending", "Done", null,
                [
                    new EmitExternalEventEffect("economy.reward", new Dictionary<string, string>
                    {
                        ["trigger"] = "ScenarioCompleted",
                        ["referenceId"] = "*",
                    }),
                ], [], true),
            ]);
        Guid versionId = Guid.NewGuid();
        string snapshot = NarrativeJson.Serialize(scenario);
        var repository = new RepositoryStub();
        var service = new PlayService(
            repository,
            new SnapshotClientStub(new PublishedSnapshotContract(
                versionId,
                Guid.NewGuid(),
                "default",
                null,
                1,
                snapshot,
                CanonicalSnapshot.ComputeHash(scenario))),
            new AccessClientStub(),
            TimeProvider.System);

        SessionView session = await service.StartAsync("player-1", versionId, 42, true, CancellationToken.None);
        Guid commandId = Guid.NewGuid();
        InputResult first = await service.SubmitChoiceAsync(
            session.Id,
            "player-1",
            commandId,
            session.Revision,
            "courageous-choice",
            CancellationToken.None);
        InputResult replay = await service.SubmitChoiceAsync(
            session.Id,
            "player-1",
            commandId,
            session.Revision,
            "courageous-choice",
            CancellationToken.None);

        Assert.Equal(2, first.Rewards.Count);
        Assert.Contains(first.Rewards, reward => reward.Trigger == "ChoiceSelected" && reward.ReferenceId == "courageous-choice");
        Assert.Contains(first.Rewards, reward => reward.Trigger == "ScenarioCompleted" && reward.ReferenceId == "*");
        Assert.Equal(first.Rewards, replay.Rewards);
        Assert.True(replay.Replayed);
        Assert.All(first.Rewards, reward => Assert.StartsWith($"session:{session.Id}:external:", reward.IdempotencyKey));
    }

    private sealed class SnapshotClientStub(PublishedSnapshotContract snapshot) : IAuthoringSnapshotClient
    {
        public Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class AccessClientStub : IContentAccessClient
    {
        public Task EnsureCanStartAsync(Guid userId, string frontId, Guid scenarioId, Guid? categoryId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RepositoryStub : IPlayRepository
    {
        private GameSession? session;
        private readonly Dictionary<Guid, ProcessedCommand> commands = [];

        public Task AddAsync(GameSession value, CancellationToken cancellationToken) { session = value; return Task.CompletedTask; }
        public Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == id && session.OwnerId == ownerId ? session : null);
        public Task<ProcessedCommand?> GetProcessedCommandAsync(Guid sessionId, Guid commandId, CancellationToken cancellationToken) =>
            Task.FromResult(commands.GetValueOrDefault(commandId));
        public Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken) { commands[command.CommandId] = command; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}