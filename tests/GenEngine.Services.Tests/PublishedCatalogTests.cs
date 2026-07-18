using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

namespace GenEngine.Services.Tests;

public sealed class PublishedCatalogTests
{
    [Fact]
    public async Task ImportMigratesLegacyDraftWithoutHidingBusinessValidationIssues()
    {
        ScenarioDocument legacy = CreateDocument("Legacy draft", "Opening") with
        {
            InitialNodeId = "missing",
        };
        var repository = new CatalogRepository();
        var service = new AuthoringService(repository, TimeProvider.System);

        ScenarioView imported = await service.ImportAsync(
            "owner",
            NarrativeJson.Serialize(legacy),
            CancellationToken.None);
        ScenarioDocument stored = NarrativeJson.Deserialize<ScenarioDocument>(imported.DraftJson);

        Assert.Equal(NarrativeVersions.LatestSchema, stored.SchemaVersion);
        Assert.False(ScenarioValidator.Validate(stored).IsValid);
        Assert.NotNull(repository.AddedScenario);
    }

    [Fact]
    public async Task CatalogUsesLatestPublishedSnapshotInsteadOfCurrentDraft()
    {
        DateTimeOffset now = new(2026, 7, 17, 18, 0, 0, TimeSpan.Zero);
        ScenarioDocument first = CreateDocument("First published title", "First opening text");
        Scenario scenario = Scenario.Create("owner", first.Title, NarrativeJson.Serialize(first), now);
        scenario.Publish(NarrativeJson.Serialize(first), CanonicalSnapshot.ComputeHash(first), 1, now);

        ScenarioDocument latest = CreateDocument("Latest published title", "Latest opening text");
        scenario.UpdateDraft(latest.Title, NarrativeJson.Serialize(latest), 2, now.AddMinutes(1));
        scenario.Publish(NarrativeJson.Serialize(latest), CanonicalSnapshot.ComputeHash(latest), 3, now.AddMinutes(2));

        ScenarioDocument unpublished = CreateDocument("Unpublished draft title", "Private draft text");
        scenario.UpdateDraft(unpublished.Title, NarrativeJson.Serialize(unpublished), 4, now.AddMinutes(3));

        var repository = new CatalogRepository(scenario);
        var service = new AuthoringService(repository, TimeProvider.System);

        PublishedScenarioView result = Assert.Single(
            await service.ListPublishedAsync(12, null, null, CancellationToken.None));

        Assert.Equal(12, repository.RequestedLimit);
        Assert.Equal(scenario.Id, result.ScenarioId);
        Assert.Equal(2, result.VersionNumber);
        Assert.Equal("Latest published title", result.Title);
        Assert.Equal("Latest opening text", result.Description);
        Assert.Equal(5, result.EstimatedMinutes);
        Assert.Equal(now.AddMinutes(2), result.PublishedAt);
    }

    [Fact]
    public async Task ArchiveKeepsPublishedVersionsButMarksScenarioUnavailableForCatalogQueries()
    {
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        ScenarioDocument document = CreateDocument("Archive me", "Opening");
        Scenario scenario = Scenario.Create("owner", document.Title, NarrativeJson.Serialize(document), now);
        scenario.Publish(NarrativeJson.Serialize(document), CanonicalSnapshot.ComputeHash(document), 1, now);
        var repository = new CatalogRepository(scenario);
        var service = new AuthoringService(repository, TimeProvider.System);

        await service.ArchiveAsync(scenario.Id, "owner", 2, CancellationToken.None);

        Assert.True(scenario.IsArchived);
        Assert.Single(scenario.Versions);
    }

    private static ScenarioDocument CreateDocument(string title, string openingText) =>
        new(
            NarrativeVersions.Schema,
            title,
            "opening",
            [
                new NarrativeNode("opening", openingText, null, [], [], false),
                new NarrativeNode("middle", "Middle", null, [], [], false),
                new NarrativeNode("ending", "Ending", null, [], [], true),
            ]);

    private sealed class CatalogRepository(params Scenario[] scenarios) : IAuthoringRepository
    {
        public Scenario? AddedScenario { get; private set; }

        public int RequestedLimit { get; private set; }

        public Task AddAsync(Scenario scenario, CancellationToken cancellationToken)
        {
            AddedScenario = scenario;
            return Task.CompletedTask;
        }

        public Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(scenarios.SingleOrDefault(scenario => scenario.Id == id));

        public Task<IReadOnlyList<Scenario>> ListPublishedAsync(
            int limit,
            Guid? categoryId,
            string? query,
            CancellationToken cancellationToken)
        {
            RequestedLimit = limit;
            return Task.FromResult<IReadOnlyList<Scenario>>(scenarios.Take(limit).ToArray());
        }

        public Task<(IReadOnlyList<Scenario> Items, int Total)> ListOwnedAsync(string ownerId, string? query, Guid? categoryId, bool includeArchived, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Scenario>, int)>((scenarios, scenarios.Length));

        public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioVersion?>(null);
        public Task<Scenario?> GetScenarioByIdAsync(Guid scenarioId, CancellationToken cancellationToken) =>
            Task.FromResult<Scenario?>(null);

        public Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}