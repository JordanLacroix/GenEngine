using System.Xml.Linq;

namespace GenEngine.Architecture.Tests;

public sealed class ProjectDependencyTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedDependencies =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["GenEngine.Observability"] = new HashSet<string>(StringComparer.Ordinal),
            ["GenEngine.Narrative"] = new HashSet<string>(StringComparer.Ordinal),
            ["GenEngine.Authoring.Domain"] = None(),
            ["GenEngine.Authoring.Application"] = Only(
                "GenEngine.Authoring.Domain", "GenEngine.Narrative"),
            ["GenEngine.Authoring.Infrastructure"] = Only("GenEngine.Authoring.Application"),
            ["GenEngine.Authoring.Api"] = Only(
                "GenEngine.Authoring.Application", "GenEngine.Authoring.Infrastructure",
                "GenEngine.Observability"),
            ["GenEngine.Play.Domain"] = None(),
            ["GenEngine.Play.Application"] = Only("GenEngine.Play.Domain", "GenEngine.Narrative"),
            ["GenEngine.Play.Infrastructure"] = Only("GenEngine.Play.Application"),
            ["GenEngine.Play.Api"] = Only(
                "GenEngine.Play.Application", "GenEngine.Play.Infrastructure",
                "GenEngine.Observability"),
            ["GenEngine.Identity.Domain"] = None(),
            ["GenEngine.Identity.Application"] = Only("GenEngine.Identity.Domain"),
            ["GenEngine.Identity.Infrastructure"] = Only("GenEngine.Identity.Application"),
            ["GenEngine.Identity.Api"] = Only(
                "GenEngine.Identity.Application", "GenEngine.Identity.Infrastructure",
                "GenEngine.Observability"),
            ["GenEngine.Configuration.Domain"] = None(),
            ["GenEngine.Configuration.Application"] = Only("GenEngine.Configuration.Domain"),
            ["GenEngine.Configuration.Infrastructure"] = Only("GenEngine.Configuration.Application"),
            ["GenEngine.Configuration.Api"] = Only(
                "GenEngine.Configuration.Application", "GenEngine.Configuration.Infrastructure",
                "GenEngine.Observability"),
            ["GenEngine.PlayerExperience.Domain"] = None(),
            ["GenEngine.PlayerExperience.Application"] = Only("GenEngine.PlayerExperience.Domain"),
            ["GenEngine.PlayerExperience.Infrastructure"] = Only("GenEngine.PlayerExperience.Application"),
            ["GenEngine.PlayerExperience.Api"] = Only(
                "GenEngine.PlayerExperience.Application", "GenEngine.PlayerExperience.Infrastructure",
                "GenEngine.Observability"),
            ["GenEngine.Organization.Domain"] = None(),
            ["GenEngine.Organization.Application"] = Only("GenEngine.Organization.Domain"),
            ["GenEngine.Organization.Infrastructure"] = Only("GenEngine.Organization.Application"),
            ["GenEngine.Organization.Api"] = Only(
                "GenEngine.Organization.Application", "GenEngine.Organization.Infrastructure",
                "GenEngine.Observability"),
        };

    [Fact]
    public void SourceProjectReferencesRespectTheDependencyPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceDirectory = Path.Combine(repositoryRoot, "src");
        var projects = Directory
            .EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path)
                    ?? throw new InvalidDataException($"Invalid project path: {path}"),
                StringComparer.Ordinal);

        Assert.Equal(
            AllowedDependencies.Keys.Order(StringComparer.Ordinal),
            projects.Keys.Order(StringComparer.Ordinal));

        foreach (var (projectName, projectPath) in projects)
        {
            var actualDependencies = ReadProjectReferences(projectPath);
            var expectedDependencies = AllowedDependencies[projectName];

            Assert.True(
                actualDependencies.SetEquals(expectedDependencies),
                $"{projectName} references [{string.Join(", ", actualDependencies.Order())}] " +
                $"but the policy allows [{string.Join(", ", expectedDependencies.Order())}].");
        }
    }

    private static HashSet<string> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!.Replace('\\', '/')))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> None() => new(StringComparer.Ordinal);

    private static HashSet<string> Only(params string[] projectNames) =>
        projectNames.ToHashSet(StringComparer.Ordinal);

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