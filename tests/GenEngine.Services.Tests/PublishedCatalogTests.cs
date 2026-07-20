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
    public async Task ImportWithSlugUpsertsExistingDraftInsteadOfCreatingADuplicate()
    {
        var repository = new CatalogRepository();
        var service = new AuthoringService(repository, TimeProvider.System);
        ScenarioDocument first = CreateDocument("Première rédaction", "Opening");
        ScenarioDocument second = CreateDocument("Rédaction corrigée", "Opening revu");

        ScenarioView created = await service.ImportAsync(
            "owner",
            NarrativeJson.Serialize(first),
            CancellationToken.None,
            slug: "la-note-de-service");
        ScenarioView updated = await service.ImportAsync(
            "owner",
            NarrativeJson.Serialize(second),
            CancellationToken.None,
            slug: "la-note-de-service");

        // Un seul ajout : le second import a réutilisé le brouillon du premier.
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("la-note-de-service", updated.Slug);
        Assert.Equal("Rédaction corrigée", updated.Title);
        Assert.Equal(created.Revision + 1, updated.Revision);
    }

    [Fact]
    public async Task ImportWithoutSlugKeepsCreatingDistinctDraftsByGuid()
    {
        var repository = new CatalogRepository();
        var service = new AuthoringService(repository, TimeProvider.System);
        ScenarioDocument document = CreateDocument("Sans slug", "Opening");

        ScenarioView first = await service.ImportAsync(
            "owner", NarrativeJson.Serialize(document), CancellationToken.None);
        ScenarioView second = await service.ImportAsync(
            "owner", NarrativeJson.Serialize(document), CancellationToken.None);

        Assert.Equal(2, repository.AddCount);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.Slug);
        Assert.Null(second.Slug);
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

        PagedView<PublishedScenarioView> catalog =
            await service.ListPublishedAsync(null, null, 1, 12, CancellationToken.None);
        PublishedScenarioView result = Assert.Single(catalog.Items);

        Assert.Equal(12, repository.RequestedLimit);
        Assert.Equal(0, repository.RequestedOffset);
        Assert.Equal(1, catalog.Total);
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

    [Fact]
    public async Task CatalogReachesTheHundredAndFiftiethPublishedScenario()
    {
        // Régression : `limit` était plafonné à 100 sans `offset`, ce qui rendait tout scénario
        // au-delà du centième définitivement inatteignable.
        var repository = new CatalogRepository(CreatePublishedScenarios(200));
        var service = new AuthoringService(repository, TimeProvider.System);

        // 150ᵉ scénario du catalogue : page 3 d'une pagination par 50, dernier élément.
        PagedView<PublishedScenarioView> page = await service.ListPublishedAsync(null, null, 3, 50, CancellationToken.None);

        Assert.Equal(200, page.Total);
        Assert.Equal(50, page.Items.Count);
        Assert.Equal("Scénario 150", page.Items[^1].Title);
    }

    [Fact]
    public async Task CatalogPaginationHandlesPartialLastPageAndOutOfRangePages()
    {
        var repository = new CatalogRepository(CreatePublishedScenarios(105));
        var service = new AuthoringService(repository, TimeProvider.System);

        PagedView<PublishedScenarioView> lastPage = await service.ListPublishedAsync(null, null, 3, 50, CancellationToken.None);
        PagedView<PublishedScenarioView> beyondLastPage = await service.ListPublishedAsync(null, null, 4, 50, CancellationToken.None);

        // Dernière page partielle : 105 = 50 + 50 + 5.
        Assert.Equal(5, lastPage.Items.Count);
        Assert.Equal(105, lastPage.Total);

        // Page vide au-delà du dernier élément : le total reste celui de l'ensemble.
        Assert.Empty(beyondLastPage.Items);
        Assert.Equal(105, beyondLastPage.Total);
        Assert.Equal(4, beyondLastPage.Page);
    }

    [Fact]
    public async Task CatalogClampsPageAndPageSizeToTheDocumentedBounds()
    {
        var repository = new CatalogRepository(CreatePublishedScenarios(300));
        var service = new AuthoringService(repository, TimeProvider.System);

        PagedView<PublishedScenarioView> oversized = await service.ListPublishedAsync(null, null, 1, 5_000, CancellationToken.None);
        PagedView<PublishedScenarioView> negative = await service.ListPublishedAsync(null, null, -3, 0, CancellationToken.None);
        PagedView<PublishedScenarioView> defaults = await service.ListPublishedAsync(null, null, null, null, CancellationToken.None);

        Assert.Equal(Pagination.MaxPageSize, oversized.PageSize);
        Assert.Equal(Pagination.MaxPageSize, oversized.Items.Count);

        Assert.Equal(1, negative.Page);
        Assert.Equal(Pagination.MinPageSize, negative.PageSize);

        Assert.Equal(1, defaults.Page);
        Assert.Equal(Pagination.DefaultPageSize, defaults.PageSize);
    }

    [Fact]
    public async Task ScenarioVersionsArePaginated()
    {
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        ScenarioDocument document = CreateDocument("Versioned", "Opening");
        Scenario scenario = Scenario.Create("owner", document.Title, NarrativeJson.Serialize(document), now);
        for (int index = 0; index < 12; index++)
        {
            scenario.Publish(
                NarrativeJson.Serialize(document),
                CanonicalSnapshot.ComputeHash(document),
                scenario.Revision,
                now.AddMinutes(index));
            scenario.UpdateDraft(document.Title, NarrativeJson.Serialize(document), scenario.Revision, now.AddMinutes(index));
        }

        var repository = new CatalogRepository(scenario);
        var service = new AuthoringService(repository, TimeProvider.System);

        PagedView<ScenarioVersionView> secondPage = await service.ListVersionsAsync(scenario.Id, "owner", 2, 5, CancellationToken.None);

        Assert.Equal(12, secondPage.Total);
        Assert.Equal(5, secondPage.Items.Count);
        Assert.Equal(6, secondPage.Items[0].Number);
    }

    private static Scenario[] CreatePublishedScenarios(int count)
    {
        DateTimeOffset origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Scenario[] scenarios = new Scenario[count];
        for (int index = 0; index < count; index++)
        {
            // Publication du plus récent au plus ancien : « Scénario 1 » est en tête de catalogue.
            DateTimeOffset publishedAt = origin.AddMinutes(count - index);
            ScenarioDocument document = CreateDocument($"Scénario {index + 1}", "Opening");
            Scenario scenario = Scenario.Create("owner", document.Title, NarrativeJson.Serialize(document), publishedAt);
            scenario.Publish(NarrativeJson.Serialize(document), CanonicalSnapshot.ComputeHash(document), 1, publishedAt);
            scenarios[index] = scenario;
        }

        return scenarios;
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
        private readonly List<Scenario> store = [.. scenarios];

        public Scenario? AddedScenario { get; private set; }

        public int AddCount { get; private set; }

        public int RequestedLimit { get; private set; }

        public int RequestedOffset { get; private set; }

        public Task AddAsync(Scenario scenario, CancellationToken cancellationToken)
        {
            AddedScenario = scenario;
            AddCount++;
            store.Add(scenario);
            return Task.CompletedTask;
        }

        public Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(store.SingleOrDefault(scenario => scenario.Id == id));

        public Task<Scenario?> GetBySlugAsync(string frontId, string slug, CancellationToken cancellationToken) =>
            Task.FromResult(store.SingleOrDefault(
                scenario => scenario.FrontId == frontId && scenario.Slug == slug));

        public Task<(IReadOnlyList<PublishedScenarioRecord> Items, int Total)> ListPublishedAsync(
            Guid? categoryId,
            string? query,
            int offset,
            int limit,
            CancellationToken cancellationToken)
        {
            RequestedOffset = offset;
            RequestedLimit = limit;
            Scenario[] published = [.. store
                .Where(static scenario => !scenario.IsArchived && scenario.Versions.Count != 0)
                .Where(scenario => categoryId is null || scenario.CategoryId == categoryId)
                .Where(scenario => query is null || scenario.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static scenario => scenario.Versions.Max(version => version.PublishedAt))
                .ThenByDescending(static scenario => scenario.Id)];
            PublishedScenarioRecord[] page = [.. published.Skip(offset).Take(limit).Select(ToRecord)];
            return Task.FromResult<(IReadOnlyList<PublishedScenarioRecord>, int)>((page, published.Length));
        }

        public Task<(IReadOnlyList<ScenarioVersion> Items, int Total)> ListVersionsAsync(Guid scenarioId, string ownerId, int offset, int limit, CancellationToken cancellationToken)
        {
            RequestedOffset = offset;
            RequestedLimit = limit;
            ScenarioVersion[] versions = [.. store
                .Where(scenario => scenario.Id == scenarioId)
                .SelectMany(static scenario => scenario.Versions)
                .OrderBy(static version => version.Number)];
            return Task.FromResult<(IReadOnlyList<ScenarioVersion>, int)>(
                ([.. versions.Skip(offset).Take(limit)], versions.Length));
        }

        public Task<(IReadOnlyList<Scenario> Items, int Total)> ListOwnedAsync(string ownerId, string? query, Guid? categoryId, bool includeArchived, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Scenario>, int)>(([.. store.Skip(offset).Take(limit)], store.Count));

        private static PublishedScenarioRecord ToRecord(Scenario scenario)
        {
            ScenarioVersion version = scenario.Versions.MaxBy(static candidate => candidate.Number)!;
            return new PublishedScenarioRecord(
                scenario.Id,
                scenario.FrontId,
                scenario.CategoryId,
                version.Id,
                version.Number,
                version.PublishedAt,
                version.SnapshotJson,
                version.SnapshotHash);
        }

        public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
            Task.FromResult<ScenarioVersion?>(null);
        public Task<Scenario?> GetScenarioByIdAsync(Guid scenarioId, CancellationToken cancellationToken) =>
            Task.FromResult<Scenario?>(null);

        public Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}