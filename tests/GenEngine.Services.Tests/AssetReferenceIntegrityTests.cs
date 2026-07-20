using System.Text.Json;

using GenEngine.Configuration.Application;
using GenEngine.Configuration.Infrastructure;

namespace GenEngine.Services.Tests;

/// <summary>
/// The guarantee this pack calls "zéro référence morte", turned into a build
/// failure instead of a blank square discovered on screen: every asset reference
/// of the shape <c>packId:assetId</c> — whether it lives in the Diapason
/// configuration document or in one of the ten shipped scenarios — must resolve
/// to an asset the manifest actually publishes.
/// </summary>
/// <remarks>
/// Only pack references are checked. Absolute HTTPS URLs are external and cannot
/// be resolved offline; anything else (label keys, secret references, endpoints,
/// times such as <c>06:47</c>) is not a pack reference and is left alone. To keep
/// a time string from being mistaken for a reference, the walk only inspects the
/// values of the media-bearing keys the contract defines, never every string.
/// </remarks>
public sealed class AssetReferenceIntegrityTests
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    // The property names that carry an asset reference, across both the
    // configuration document (camelCase, as served) and the scenario schema.
    private static readonly HashSet<string> ReferenceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "soundUrl", "visualUrl", "ambienceUrl", "musicUrl", "backgroundUrl",
        "portraitUrl", "avatarUrl", "iconUrl", "brandIconUrl", "clientIconUrl",
        "logoUrl", "faviconUrl", "imageUrl", "assetReference",
    };

    [Fact]
    public void EveryPackReferenceInTheDiapasonConfigurationResolvesToAShippedAsset()
    {
        Dictionary<string, HashSet<string>> packs = LoadPackAssetIds();

        using JsonDocument document = JsonSerializer.SerializeToDocument(
            ConfigurationService.CreateDefault("diapason"), PayloadOptions);

        List<string> references = [];
        Collect(document.RootElement, propertyName: null, references);

        AssertAllResolve(references, packs, "the Diapason configuration document");
    }

    [Fact]
    public void EveryPackReferenceInTheShippedScenariosResolvesToAShippedAsset()
    {
        Dictionary<string, HashSet<string>> packs = LoadPackAssetIds();
        string scenariosRoot = Path.Combine(FindRepositoryRoot(), "content", "diapason", "scenarios");

        string[] files = Directory.GetFiles(scenariosRoot, "*.json");
        Assert.Equal(10, files.Length);

        foreach (string file in files)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            List<string> references = [];
            Collect(document.RootElement, propertyName: null, references);
            AssertAllResolve(references, packs, $"scenario '{Path.GetFileName(file)}'");
        }
    }

    private static void AssertAllResolve(
        IEnumerable<string> references,
        Dictionary<string, HashSet<string>> packs,
        string origin)
    {
        foreach (string reference in references)
        {
            (string packId, string assetId) = SplitPackReference(reference);
            Assert.True(
                packs.TryGetValue(packId, out HashSet<string>? assetIds),
                $"{origin} references pack '{packId}' ('{reference}'), which this instance does not publish.");
            Assert.True(
                assetIds!.Contains(assetId),
                $"{origin} references '{reference}', absent from the '{packId}' manifest — a dead reference.");
        }
    }

    /// <summary>
    /// Walks a JSON tree and collects the values of the reference-bearing keys
    /// that are pack references. Restricting to those keys keeps a time like
    /// <c>06:47</c> — whose key is <c>value</c> — from being read as a reference.
    /// </summary>
    private static void Collect(JsonElement element, string? propertyName, List<string> references)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    Collect(property.Value, property.Name, references);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, propertyName, references);
                }

                break;
            case JsonValueKind.String:
                string? value = element.GetString();
                if (propertyName is not null
                    && ReferenceKeys.Contains(propertyName)
                    && value is not null
                    && IsPackReference(value))
                {
                    references.Add(value);
                }

                break;
            default:
                break;
        }
    }

    private static Dictionary<string, HashSet<string>> LoadPackAssetIds()
    {
        var catalog = new FileSystemAssetPackCatalog(Path.Combine(FindRepositoryRoot(), "assets"));
        Dictionary<string, HashSet<string>> packs = new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetPackManifest manifest in catalog.Manifests)
        {
            packs[manifest.PackId] = manifest.Assets.Select(static asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        }

        return packs;
    }

    // The reference grammar mirrors ConfigurationService.IsPackReference and the
    // narrative engine's: a lowercase "packId:assetId" whose two segments hold only
    // a-z, 0-9, '.', '-' and '_'. An HTTPS URL fails it on the '//' after the scheme.
    private static bool IsPackReference(string value)
    {
        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        return IsSegment(value.AsSpan(0, separator)) && IsSegment(value.AsSpan(separator + 1));
    }

    private static bool IsSegment(ReadOnlySpan<char> segment)
    {
        foreach (char character in segment)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character is '.' or '-' or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return !segment.IsEmpty;
    }

    private static (string PackId, string AssetId) SplitPackReference(string reference)
    {
        int separator = reference.IndexOf(':', StringComparison.Ordinal);
        return (reference[..separator], reference[(separator + 1)..]);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GenEngine.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GenEngine repository root.");
    }
}