using System.Text.Json;

namespace GenEngine.Narrative.Tests;

public sealed class DiapasonContentTests
{
    public static TheoryData<string> ScenarioFiles()
    {
        TheoryData<string> data = [];
        foreach (string path in EnumerateScenarioPaths())
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void ReferenceConfigurationShipsTheExpectedScenarioCount()
    {
        Assert.Equal(10, EnumerateScenarioPaths().Length);
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void EveryDiapasonScenarioMigratesAndValidates(string fileName)
    {
        string json = File.ReadAllText(Path.Combine(ScenarioDirectory(), fileName));

        ScenarioMigrationResult migration = ScenarioMigrationPipeline.MigrateToLatest(json);
        ValidationReport report = ScenarioValidator.Validate(migration.Document);

        string errors = string.Join(
            Environment.NewLine,
            report.Issues.Where(static issue => issue.IsError).Select(static issue => $"{issue.Code} @ {issue.Path}: {issue.Message}"));

        Assert.True(report.IsValid, $"{fileName} is invalid:{Environment.NewLine}{errors}");
        Assert.Equal(NarrativeVersions.LatestSchema, migration.Document.SchemaVersion);
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void EveryDiapasonScenarioReachesAnEndingAndExposesAFailureEnding(string fileName)
    {
        string json = File.ReadAllText(Path.Combine(ScenarioDirectory(), fileName));
        ScenarioDocument document = ScenarioMigrationPipeline.MigrateToLatest(json).Document;

        SimulationReport simulation = ScenarioAnalyzer.Explore(document);

        Assert.NotEmpty(simulation.EndingNodeIds);
        Assert.Empty(simulation.DeadEnds);
        Assert.Contains(document.Nodes, static node => node.IsEnding && node.Id.StartsWith("fin-rupture", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestDescribesEveryShippedScenarioExactlyOnce()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ContentDirectory(), "manifest.json")));

        string[] declared = manifest.RootElement
            .GetProperty("scenarios")
            .EnumerateArray()
            .Select(static scenario => scenario.GetProperty("slug").GetString()!)
            .ToArray();

        string[] shipped = EnumerateScenarioPaths()
            .Select(static path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(shipped, declared.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(declared.Length, declared.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ManifestRotationIsDeterministicAndCoversEveryEligibleScenario()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ContentDirectory(), "manifest.json")));

        JsonElement rotation = manifest.RootElement.GetProperty("dailyRotation");
        string[] pool = rotation.GetProperty("pool").EnumerateArray().Select(static slug => slug.GetString()!).ToArray();
        int size = rotation.GetProperty("size").GetInt32();

        Assert.True(size > 0 && size < pool.Length, "The daily selection must be a strict subset of the pool.");

        // Same day must always yield the same selection, and a full cycle must cover the pool.
        HashSet<string> covered = new(StringComparer.Ordinal);
        for (var day = 0; day < pool.Length; day++)
        {
            string[] first = SelectForDay(pool, size, day);
            string[] second = SelectForDay(pool, size, day);
            Assert.Equal(first, second);
            foreach (string slug in first)
            {
                covered.Add(slug);
            }
        }

        Assert.Equal(pool.Length, covered.Count);
    }

    // Mirrors the rule documented in specs/domain/diapason/daily-rotation.md.
    private static string[] SelectForDay(string[] pool, int size, int dayIndex) =>
        Enumerable.Range(0, size)
            .Select(offset => pool[((dayIndex * size) + offset) % pool.Length])
            .ToArray();

    /// <summary>
    /// The reference configuration must actually exercise the document mechanic,
    /// and against three genuinely different natures — a memo, a diff and a large
    /// sampled table. Three shapes is what makes the model credible rather than a
    /// container fitted to a single example.
    /// </summary>
    [Theory]
    [InlineData("la-note-de-service.json", "la-note", DocumentNature.Memo)]
    [InlineData("la-revue-automatique.json", "le-diff", DocumentNature.Diff)]
    [InlineData("le-tri-des-candidatures.json", "le-classement", DocumentNature.Table)]
    public void DiapasonPresentsTheDocumentsItOnlyEverAlludedTo(
        string fileName,
        string interactionId,
        DocumentNature expectedNature)
    {
        ScenarioDocument scenario = ScenarioMigrationPipeline
            .MigrateToLatest(File.ReadAllText(Path.Combine(ScenarioDirectory(), fileName)))
            .Document;

        DocumentInteraction interaction = scenario.Nodes
            .SelectMany(static node => node.Interactions ?? [])
            .OfType<DocumentInteraction>()
            .Single(candidate => candidate.Id == interactionId);

        Assert.Equal(expectedNature, interaction.Document.Nature);

        // Consulting is never compulsory...
        Assert.True(interaction.IsOptional);

        // ...but it must unlock something, otherwise the document teaches nothing.
        Assert.Contains(
            scenario.Nodes
                .SelectMany(static node => node.Interactions ?? [])
                .OfType<ChoiceSetInteraction>()
                .SelectMany(static choiceSet => choiceSet.Choices),
            choice => choice.Condition is ConsultedDocumentCondition consulted
                && consulted.InteractionId == interactionId);
    }

    /// <summary>
    /// The table is the case that justifies the excerpt disclosure: 412 rows
    /// cannot be shown, and what is shown must say so.
    /// </summary>
    [Fact]
    public void TheApplicationTableDeclaresItsSampleHonestly()
    {
        ScenarioDocument scenario = ScenarioMigrationPipeline
            .MigrateToLatest(File.ReadAllText(Path.Combine(ScenarioDirectory(), "le-tri-des-candidatures.json")))
            .Document;

        DocumentInteraction interaction = scenario.Nodes
            .SelectMany(static node => node.Interactions ?? [])
            .OfType<DocumentInteraction>()
            .Single(static candidate => candidate.Id == "le-classement");

        DocumentExcerpt excerpt = Assert.IsType<DocumentExcerpt>(interaction.Document.Excerpt);
        Assert.Equal(412, excerpt.TotalUnits);
        Assert.Equal(DocumentUnit.Rows, excerpt.Unit);
        Assert.True(excerpt.ShownUnits < excerpt.TotalUnits);
    }

    private static string[] EnumerateScenarioPaths() =>
        Directory.GetFiles(ScenarioDirectory(), "*.json").Order(StringComparer.Ordinal).ToArray();

    private static string ScenarioDirectory() => Path.Combine(ContentDirectory(), "scenarios");

    private static string ContentDirectory() => Path.Combine(FindRepositoryRoot(), "content", "diapason");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GenEngine.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GenEngine repository root.");
    }
}