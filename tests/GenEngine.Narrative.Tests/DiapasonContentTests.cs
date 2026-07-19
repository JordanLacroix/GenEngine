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