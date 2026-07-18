using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

namespace GenEngine.Services.Tests;

public sealed class ScenarioGenerationTests
{
    [Fact]
    public async Task GenerationPersistsTheGlobalGameAndCategoryContext()
    {
        Guid categoryId = Guid.NewGuid();
        var repository = new RepositoryStub();
        var experience = new StoryExperienceContext(
            "default",
            "Chroniques",
            "Description",
            "Une histoire globale",
            [new StoryCategoryContext(categoryId, "Mystères", "Enquêtes")]);
        var service = new AuthoringService(
            repository,
            new ExperienceStub(experience),
            [new GeneratorStub()],
            TimeProvider.System);

        ScenarioView result = await service.GenerateAsync(
            "creator",
            new ScenarioGenerationRequest("default", categoryId, "Explorer une tour où les souvenirs disparaissent."),
            CancellationToken.None);

        Assert.Equal("default", result.FrontId);
        Assert.Equal(categoryId, result.CategoryId);
        Assert.Equal("Generated mystery", result.Title);
        Assert.Equal("Explorer une tour où les souvenirs disparaissent.", result.CreationBrief);
    }

    private sealed class ExperienceStub(StoryExperienceContext context) : IStoryExperienceProvider
    {
        public Task<StoryExperienceContext> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(context);
    }

    private sealed class GeneratorStub : IScenarioDraftGenerator
    {
        public string Provider => "offline";
        public Task<ScenarioDocument> GenerateAsync(StoryExperienceContext experience, StoryCategoryContext category, ScenarioGenerationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ScenarioDocument(
                NarrativeVersions.LatestSchema,
                "Generated mystery",
                "opening",
                [
                    new NarrativeNode("opening", "Opening", null, [], [new NarrativeChoice("go", "Go", "ending", null, [])]),
                    new NarrativeNode("ending", "Ending", null, [], [], true),
                ]));
    }

    private sealed class RepositoryStub : IAuthoringRepository
    {
        public Scenario? Added { get; private set; }
        public Task AddAsync(Scenario scenario, CancellationToken cancellationToken) { Added = scenario; return Task.CompletedTask; }
        public Task<Scenario?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) => Task.FromResult<Scenario?>(null);
        public Task<IReadOnlyList<Scenario>> ListPublishedAsync(int limit, Guid? categoryId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Scenario>>([]);
        public Task<ScenarioVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) => Task.FromResult<ScenarioVersion?>(null);
        public Task AddVersionAsync(ScenarioVersion version, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}