using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

namespace GenEngine.Authoring.Application;

public interface IAuthoringRepository
{
    Task AddAsync(Scenario scenario, CancellationToken cancellationToken);

    Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Scenario>> ListPublishedAsync(int limit, Guid? categoryId, string? query, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Scenario> Items, int Total)> ListOwnedAsync(string ownerId, string? query, Guid? categoryId, bool includeArchived, int offset, int limit, CancellationToken cancellationToken);

    Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken);

    Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ScenarioView(
    Guid Id,
    string Title,
    int Revision,
    string DraftJson,
    string FrontId,
    Guid? CategoryId,
    string CreationBrief,
    bool IsArchived,
    DateTimeOffset UpdatedAt);
public sealed record PagedScenariosView(IReadOnlyList<ScenarioView> Items, int Page, int PageSize, int Total);

public sealed record ScenarioVersionView(
    Guid Id,
    Guid ScenarioId,
    int Number,
    string SnapshotHash,
    DateTimeOffset PublishedAt);

public sealed record PublishedScenarioView(
    Guid ScenarioId,
    Guid VersionId,
    int VersionNumber,
    string FrontId,
    Guid? CategoryId,
    string Title,
    string Description,
    int EstimatedMinutes,
    DateTimeOffset PublishedAt,
    string SnapshotHash);

public sealed record StoryCategoryContext(Guid Id, string Name, string Description);
public sealed record StoryExperienceContext(
    string FrontId,
    string GameName,
    string GameDescription,
    string GlobalStory,
    IReadOnlyList<StoryCategoryContext> Categories);
public sealed record ScenarioGenerationRequest(
    string FrontId,
    Guid CategoryId,
    string Prompt,
    string Provider = "offline",
    int TargetMinutes = 10,
    string Tone = "immersive");

public interface IStoryExperienceProvider
{
    Task<StoryExperienceContext> GetAsync(string frontId, CancellationToken cancellationToken);
}

public interface IScenarioDraftGenerator
{
    string Provider { get; }
    Task<ScenarioDocument> GenerateAsync(
        StoryExperienceContext experience,
        StoryCategoryContext category,
        ScenarioGenerationRequest request,
        CancellationToken cancellationToken);
}

public sealed record PublishedSnapshot(
    Guid Id,
    Guid ScenarioId,
    int Number,
    string SnapshotJson,
    string SnapshotHash);

public sealed record ScenarioPreview(GameState State, CurrentStep CurrentStep);

public sealed class AuthoringService(
    IAuthoringRepository repository,
    IStoryExperienceProvider experienceProvider,
    IEnumerable<IScenarioDraftGenerator> generators,
    TimeProvider timeProvider)
{
    public AuthoringService(IAuthoringRepository repository, TimeProvider timeProvider)
        : this(repository, new UnavailableExperienceProvider(), [], timeProvider)
    {
    }

    public async Task<ScenarioView> ImportAsync(
        string ownerId,
        string draftJson,
        CancellationToken cancellationToken)
    {
        ScenarioDocument document = MigrateDraft(draftJson);
        Scenario scenario = Scenario.Create(ownerId, document.Title, NarrativeJson.Serialize(document), GetUtcNow());
        await repository.AddAsync(scenario, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(scenario);
    }

    public async Task<ScenarioView> GenerateAsync(
        string ownerId,
        ScenarioGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Trim().Length is < 20 or > 4000)
        {
            throw new AuthoringException("invalid_generation_prompt", "The creation prompt must contain between 20 and 4000 characters.");
        }

        StoryExperienceContext experience = await experienceProvider.GetAsync(request.FrontId, cancellationToken)
            .ConfigureAwait(false);
        StoryCategoryContext category = experience.Categories.SingleOrDefault(item => item.Id == request.CategoryId)
            ?? throw new AuthoringException("category_not_found", "The selected story category was not found.");
        IScenarioDraftGenerator generator = generators.SingleOrDefault(
            item => string.Equals(item.Provider, request.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthoringException("provider_not_available", "The selected AI provider is not available.");
        ScenarioDocument document = await generator.GenerateAsync(experience, category, request, cancellationToken)
            .ConfigureAwait(false);
        ValidationReport report = ScenarioValidator.Validate(document);
        if (!report.IsValid)
        {
            throw new AuthoringException("generated_scenario_invalid", "The generated scenario does not satisfy narrative invariants.");
        }

        Scenario scenario = Scenario.Create(
            ownerId,
            document.Title,
            NarrativeJson.Serialize(document),
            GetUtcNow(),
            experience.FrontId,
            category.Id,
            request.Prompt.Trim());
        await repository.AddAsync(scenario, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(scenario);
    }

    public async Task<ScenarioView> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return Map(scenario);
    }

    public async Task<PagedScenariosView> ListOwnedAsync(
        string ownerId,
        string? query,
        Guid? categoryId,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        (IReadOnlyList<Scenario> items, int total) = await repository.ListOwnedAsync(
            ownerId,
            string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            categoryId,
            includeArchived,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        return new PagedScenariosView(items.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task ArchiveAsync(Guid id, string ownerId, int expectedRevision, CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        scenario.Archive(expectedRevision, GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScenarioView> UpdateDraftAsync(
        Guid id,
        string ownerId,
        int expectedRevision,
        string draftJson,
        CancellationToken cancellationToken)
    {
        ScenarioDocument document = MigrateDraft(draftJson);
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        scenario.UpdateDraft(document.Title, NarrativeJson.Serialize(document), expectedRevision, GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(scenario);
    }

    public async Task<ValidationReport> ValidateAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return ScenarioValidator.Validate(Deserialize(scenario.DraftJson));
    }

    public async Task<NarrativeStructureReport> AnalyzeStructureAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return NarrativeStructureAnalyzer.Analyze(Deserialize(scenario.DraftJson));
    }

    public async Task<ScenarioPreview> PreviewAsync(
        Guid id,
        string ownerId,
        string nodeId,
        WorldState injectedWorld,
        int turn,
        CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        ScenarioDocument document = Deserialize(scenario.DraftJson);
        GameState state = NarrativeRuntime.PreviewAt(document, nodeId, injectedWorld, turn);
        return new ScenarioPreview(state, NarrativeRuntime.GetCurrentStep(document, state));
    }

    public async Task<ScenarioVersionView> PublishAsync(
        Guid id,
        string ownerId,
        int expectedRevision,
        CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        ScenarioDocument document = Deserialize(scenario.DraftJson);
        ValidationReport report = ScenarioValidator.Validate(document);
        if (!report.IsValid)
        {
            throw new AuthoringException("invalid_scenario", "The scenario contains validation errors.");
        }

        string snapshotJson = NarrativeJson.Serialize(document);
        string hash = CanonicalSnapshot.ComputeHash(document);
        ScenarioVersion version = scenario.Publish(snapshotJson, hash, expectedRevision, GetUtcNow());
        await repository.AddVersionAsync(version, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(version);
    }

    public async Task<IReadOnlyList<ScenarioVersionView>> ListVersionsAsync(
        Guid id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        Scenario scenario = await GetRequiredAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return scenario.Versions.OrderBy(static version => version.Number).Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<PublishedScenarioView>> ListPublishedAsync(
        int limit,
        Guid? categoryId,
        string? query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Scenario> scenarios = await repository.ListPublishedAsync(limit, categoryId, query, cancellationToken)
            .ConfigureAwait(false);
        return scenarios.Select(MapPublished).ToArray();
    }

    public async Task<PublishedSnapshot> GetPublishedSnapshotAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        ScenarioVersion version = await repository.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false)
            ?? throw new AuthoringException("version_not_found", "The published scenario version was not found.");
        return new PublishedSnapshot(
            version.Id,
            version.ScenarioId,
            version.Number,
            version.SnapshotJson,
            version.SnapshotHash);
    }

    private async Task<Scenario> GetRequiredAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false)
        ?? throw new AuthoringException("scenario_not_found", "The scenario was not found.");

    private static ScenarioDocument Deserialize(string json)
    {
        try
        {
            return NarrativeJson.Deserialize<ScenarioDocument>(json);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new AuthoringException("invalid_json", "The scenario JSON is invalid.", exception);
        }
    }

    private static ScenarioDocument MigrateDraft(string json)
    {
        try
        {
            return ScenarioMigrationPipeline.MigrateToLatest(json).Document;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new AuthoringException("invalid_json", "The scenario JSON is invalid.", exception);
        }
    }

    private DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();

    private static ScenarioView Map(Scenario scenario) =>
        new(
            scenario.Id,
            scenario.Title,
            scenario.Revision,
            scenario.DraftJson,
            scenario.FrontId,
            scenario.CategoryId,
            scenario.CreationBrief,
            scenario.IsArchived,
            scenario.UpdatedAt);

    private static ScenarioVersionView Map(ScenarioVersion version) =>
        new(version.Id, version.ScenarioId, version.Number, version.SnapshotHash, version.PublishedAt);

    private static PublishedScenarioView MapPublished(Scenario scenario)
    {
        ScenarioVersion version = scenario.Versions.MaxBy(static candidate => candidate.Number)
            ?? throw new AuthoringException("version_not_found", "The published scenario version was not found.");
        ScenarioDocument document = Deserialize(version.SnapshotJson);
        NarrativeNode? openingNode = document.Nodes.FirstOrDefault(
            node => string.Equals(node.Id, document.InitialNodeId, StringComparison.Ordinal));
        string description = openingNode?.Text ?? string.Empty;
        if (description.Length > 240)
        {
            description = string.Concat(description.AsSpan(0, 237), "...");
        }

        int estimatedMinutes = Math.Max(3, (int)Math.Ceiling(document.Nodes.Count * 1.5));
        return new PublishedScenarioView(
            scenario.Id,
            version.Id,
            version.Number,
            scenario.FrontId,
            scenario.CategoryId,
            document.Title,
            description,
            estimatedMinutes,
            version.PublishedAt,
            version.SnapshotHash);
    }

    private sealed class UnavailableExperienceProvider : IStoryExperienceProvider
    {
        public Task<StoryExperienceContext> GetAsync(string frontId, CancellationToken cancellationToken) =>
            throw new AuthoringException("experience_unavailable", "The published game configuration is unavailable.");
    }
}

public sealed class AuthoringException : InvalidOperationException
{
    public AuthoringException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public AuthoringException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}