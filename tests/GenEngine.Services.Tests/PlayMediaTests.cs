using GenEngine.Narrative;
using GenEngine.Play.Application;
using GenEngine.Play.Domain;

namespace GenEngine.Services.Tests;

/// <summary>
/// Media travel with the published snapshot: Play neither stores nor resolves
/// them, it only forwards what the scenario declares.
/// </summary>
public sealed class PlayMediaTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private const string Visual = "https://assets.example.org/scene-palace-atrium-v1.avif";
    private const string Signature = "https://assets.example.org/sfx-ui-choice-confirm-v1.ogg";

    [Fact]
    public async Task PublishedStructureCarriesTheMediaDeclaredByTheScenario()
    {
        PlayService service = CreateService(withMedia: true);

        NarrativeStructure structure = await service.GetStructureAsync(
            Guid.NewGuid().ToString(),
            VersionId,
            bypassAssignments: true,
            CancellationToken.None);

        NarrativeStructureNode opening = structure.Nodes.Single(node => node.Id == "opening");
        Assert.Equal(Visual, opening.Media?.VisualUrl);
        Assert.Null(structure.Nodes.Single(node => node.Id == "ending").Media);
    }

    [Fact]
    public async Task PublishedStructureStaysUsableWhenTheScenarioDeclaresNoMedia()
    {
        PlayService service = CreateService(withMedia: false);

        NarrativeStructure structure = await service.GetStructureAsync(
            Guid.NewGuid().ToString(),
            VersionId,
            bypassAssignments: true,
            CancellationToken.None);

        Assert.All(structure.Nodes, static node => Assert.Null(node.Media));
        Assert.NotEmpty(structure.Edges);
    }

    private static PlayService CreateService(bool withMedia)
    {
        ScenarioDocument document = new(
            NarrativeVersions.LatestSchema,
            "Media story",
            "opening",
            [
                new NarrativeNode(
                    "opening",
                    "Le hall écoute avant de répondre.",
                    null,
                    [],
                    [
                        new NarrativeChoice("listen", "Écouter", "ending", null, [])
                        {
                            Media = withMedia
                                ? new ChoiceMedia { SoundUrl = Signature, AnimationCue = "choice-confirm" }
                                : null,
                        },
                    ])
                {
                    Media = withMedia ? new StepMedia { VisualUrl = Visual } : null,
                },
                new NarrativeNode("ending", "L'intervalle se résout.", null, [], [], true),
            ]);

        return new PlayService(
            new RepositoryStub(),
            new SnapshotClientStub(new PublishedSnapshotContract(
                VersionId,
                Guid.NewGuid(),
                "default",
                null,
                1,
                NarrativeJson.Serialize(document),
                CanonicalSnapshot.ComputeHash(document))),
            new AllowingAccessClient(),
            TimeProvider.System);
    }

    private sealed class AllowingAccessClient : IContentAccessClient
    {
        public Task EnsureCanStartAsync(Guid userId, string frontId, Guid scenarioId, Guid? categoryId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SnapshotClientStub(PublishedSnapshotContract snapshot) : IAuthoringSnapshotClient
    {
        public Task<PublishedSnapshotContract> GetAsync(Guid versionId, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class RepositoryStub : IPlayRepository
    {
        public Task AddAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<ProcessedCommand?> GetProcessedCommandAsync(Guid sessionId, Guid commandId, CancellationToken cancellationToken) => Task.FromResult<ProcessedCommand?>(null);
        public Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}