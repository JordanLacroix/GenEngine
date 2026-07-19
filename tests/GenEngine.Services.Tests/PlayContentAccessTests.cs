using GenEngine.Narrative;
using GenEngine.Play.Application;
using GenEngine.Play.Domain;

namespace GenEngine.Services.Tests;

public sealed class PlayContentAccessTests
{
    [Fact]
    public async Task StartPassesPublishedFrontAndCategoryToAccessControl()
    {
        Guid userId = Guid.NewGuid();
        Guid scenarioId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        var access = new RecordingAccessClient();
        var repository = new RepositoryStub();
        PlayService service = CreateService(repository, access, scenarioId, categoryId, "enterprise-eu");

        SessionView session = await service.StartAsync(userId.ToString(), VersionId, 42, false, CancellationToken.None);

        Assert.Equal((userId, "enterprise-eu", scenarioId, categoryId), access.LastRequest);
        Assert.Equal("enterprise-eu", session.FrontId);
        Assert.NotNull(repository.Session);
    }

    [Fact]
    public async Task DeniedAccessDoesNotCreateSession()
    {
        var access = new RecordingAccessClient { Deny = true };
        var repository = new RepositoryStub();
        PlayService service = CreateService(repository, access, Guid.NewGuid(), null, "school-fr");

        PlayException error = await Assert.ThrowsAsync<PlayException>(() => service.StartAsync(
            Guid.NewGuid().ToString(), VersionId, 42, false, CancellationToken.None));

        Assert.Equal("content_not_assigned", error.Code);
        Assert.Null(repository.Session);
    }

    [Fact]
    public async Task GlobalScopeBypassesAssignmentLookup()
    {
        var access = new RecordingAccessClient { Deny = true };
        var repository = new RepositoryStub();
        PlayService service = CreateService(repository, access, Guid.NewGuid(), null, "default");

        _ = await service.StartAsync("administrator", VersionId, 42, true, CancellationToken.None);

        Assert.Null(access.LastRequest);
        Assert.NotNull(repository.Session);
    }

    [Fact]
    public void JourneyAssignmentGrantsAccessToScenariosInItsCategoriesOnly()
    {
        Guid journeyId = Guid.NewGuid();
        Guid assignedCategory = Guid.NewGuid();
        AssignedContentAccess[] assignments = [new("Journey", journeyId)];
        JourneyCatalogAccess[] journeys = [new(journeyId, [assignedCategory])];

        Assert.True(ContentAssignmentEvaluator.IsAssigned(Guid.NewGuid(), assignedCategory, assignments, journeys));
        Assert.False(ContentAssignmentEvaluator.IsAssigned(Guid.NewGuid(), Guid.NewGuid(), assignments, journeys));
    }

    [Fact]
    public async Task StructureReturnsTheTopologyOfAPublishedVersion()
    {
        var repository = new RepositoryStub();
        var access = new RecordingAccessClient();
        Guid scenarioId = Guid.NewGuid();
        PlayService service = CreateService(repository, access, scenarioId, null, "default");

        NarrativeStructure structure = await service.GetStructureAsync(
            Guid.NewGuid().ToString(),
            VersionId,
            bypassAssignments: false,
            CancellationToken.None);

        Assert.Equal("opening", structure.InitialNodeId);
        NarrativeStructureNode node = Assert.Single(structure.Nodes);
        Assert.Equal("opening", node.Id);
        Assert.True(node.IsEnding);
        Assert.Empty(structure.Edges);
        Assert.Equal(scenarioId, access.LastRequest?.ScenarioId);
    }

    [Fact]
    public async Task StructureIsRefusedWhenTheScenarioIsNotAssigned()
    {
        var repository = new RepositoryStub();
        var access = new RecordingAccessClient { Deny = true };
        PlayService service = CreateService(repository, access, Guid.NewGuid(), null, "default");

        PlayException exception = await Assert.ThrowsAsync<PlayException>(
            () => service.GetStructureAsync(
                Guid.NewGuid().ToString(),
                VersionId,
                bypassAssignments: false,
                CancellationToken.None));

        Assert.Equal("content_not_assigned", exception.Code);
    }

    [Fact]
    public async Task StructureSkipsAssignmentsForAnUnscopedOperator()
    {
        var repository = new RepositoryStub();
        var access = new RecordingAccessClient { Deny = true };
        PlayService service = CreateService(repository, access, Guid.NewGuid(), null, "default");

        NarrativeStructure structure = await service.GetStructureAsync(
            "not-a-guid",
            VersionId,
            bypassAssignments: true,
            CancellationToken.None);

        Assert.NotEmpty(structure.Nodes);
        Assert.Null(access.LastRequest);
    }

    private static readonly Guid VersionId = Guid.NewGuid();

    private static PlayService CreateService(
        RepositoryStub repository,
        RecordingAccessClient access,
        Guid scenarioId,
        Guid? categoryId,
        string frontId)
    {
        ScenarioDocument document = new(
            NarrativeVersions.LatestSchema,
            "Access story",
            "opening",
            [new NarrativeNode("opening", "Welcome", null, [], [], true)]);
        return new PlayService(
            repository,
            new SnapshotClientStub(new PublishedSnapshotContract(
                VersionId,
                scenarioId,
                frontId,
                categoryId,
                1,
                NarrativeJson.Serialize(document),
                CanonicalSnapshot.ComputeHash(document))),
            access,
            TimeProvider.System);
    }

    private sealed class RecordingAccessClient : IContentAccessClient
    {
        public bool Deny { get; init; }
        public (Guid UserId, string FrontId, Guid ScenarioId, Guid? CategoryId)? LastRequest { get; private set; }

        public Task EnsureCanStartAsync(Guid userId, string frontId, Guid scenarioId, Guid? categoryId, CancellationToken cancellationToken)
        {
            LastRequest = (userId, frontId, scenarioId, categoryId);
            return Deny
                ? Task.FromException(new PlayException("content_not_assigned", "Denied for test."))
                : Task.CompletedTask;
        }
    }

    private sealed class SnapshotClientStub(PublishedSnapshotContract snapshot) : IAuthoringSnapshotClient
    {
        public Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class RepositoryStub : IPlayRepository
    {
        public GameSession? Session { get; private set; }
        public Task AddAsync(GameSession session, CancellationToken cancellationToken) { Session = session; return Task.CompletedTask; }
        public Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<ProcessedCommand?> GetProcessedCommandAsync(Guid sessionId, Guid commandId, CancellationToken cancellationToken) => Task.FromResult<ProcessedCommand?>(null);
        public Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}