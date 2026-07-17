using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

namespace GenEngine.Services.Tests;

public sealed class PublishedCatalogTests
{
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
            await service.ListPublishedAsync(12, CancellationToken.None));

        Assert.Equal(12, repository.RequestedLimit);
        Assert.Equal(scenario.Id, result.ScenarioId);
        Assert.Equal(2, result.VersionNumber);
        Assert.Equal("Latest published title", result.Title);
        Assert.Equal("Latest opening text", result.Description);
        Assert.Equal(5, result.EstimatedMinutes);
        Assert.Equal(now.AddMinutes(2), result.PublishedAt);
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
        public int RequestedLimit { get; private set; }

        public Task AddAsync(Scenario scenario, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(scenarios.SingleOrDefault(scenario => scenario.Id == id));

        public Task<IReadOnlyList<Scenario>> ListPublishedAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            RequestedLimit = limit;
            return Task.FromResult<IReadOnlyList<Scenario>>(scenarios.Take(limit).ToArray());
        }

        public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioVersion?>(null);

        public Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}