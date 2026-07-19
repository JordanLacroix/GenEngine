namespace GenEngine.Configuration.Application;

/// <summary>
/// Published view of an asset pack.
///
/// A pack is data, not code: Configuration owns the control plane, so it is the
/// service that tells a client which assets an instance considers published and
/// where their bytes live. The shape mirrors
/// <c>assets/&lt;pack&gt;/asset-manifest.json</c>, with one deliberate difference:
/// <see cref="AssetPackEntry.Path"/> is rewritten into an absolute request path
/// served by this API, so a client never has to know the repository layout.
/// </summary>
public sealed record AssetPackNineSliceInsets(int Left, int Right, int Top, int Bottom);

public sealed record AssetPackImage(
    int Width,
    int Height,
    bool Scalable,
    bool Transparency,
    bool Recolorable,
    AssetPackNineSliceInsets? NineSliceInsets);

public sealed record AssetPackAudio(
    string Codec,
    int SampleRate,
    int Channels,
    double DurationSeconds,
    bool Loop);

public sealed record AssetPackEntry(
    string Id,
    string Kind,
    string Role,
    string Path,
    long Bytes,
    string Sha256,
    string SourceId,
    string Usage,
    string MediaType,
    AssetPackImage? Image,
    AssetPackAudio? Audio);

/// <summary>A category the pack declares it does not provide, with its reason.</summary>
public sealed record AssetPackGap(string Kind, string Status, string Reason);

public sealed record AssetPackSource(
    string SourceId,
    string Name,
    string Author,
    string PageUrl,
    string License,
    string LicenseUrl,
    string Attribution);

public sealed record AssetPackManifest(
    int SchemaVersion,
    string PackId,
    string PackVersion,
    string ConfigurationKey,
    string Description,
    string FilesBaseUrl,
    IReadOnlyDictionary<string, string> Palette,
    string Recoloring,
    IReadOnlyList<AssetPackGap> Gaps,
    IReadOnlyList<AssetPackSource> Sources,
    IReadOnlyList<AssetPackEntry> Assets);

/// <summary>Listing entry: enough to choose a pack without downloading it whole.</summary>
public sealed record AssetPackSummary(
    string PackId,
    string PackVersion,
    string ConfigurationKey,
    string Description,
    int AssetCount,
    string FilesBaseUrl);

/// <summary>
/// Source of the packs shipped with the instance. Reading files is an
/// infrastructure concern; the application only consumes an immutable snapshot
/// resolved once at startup.
/// </summary>
public interface IAssetPackCatalog
{
    IReadOnlyList<AssetPackManifest> Manifests { get; }
}

/// <summary>
/// Read-only surface over the shipped packs. There is no write path: a pack is
/// versioned with the repository, never uploaded at runtime.
/// </summary>
public sealed class AssetPackService(IAssetPackCatalog catalog)
{
    public IReadOnlyList<AssetPackSummary> List() =>
        [.. catalog.Manifests.Select(static manifest => new AssetPackSummary(
            manifest.PackId,
            manifest.PackVersion,
            manifest.ConfigurationKey,
            manifest.Description,
            manifest.Assets.Count,
            manifest.FilesBaseUrl))];

    /// <summary>
    /// An unknown pack is a 404 rather than an empty manifest: a client must be
    /// able to tell "this instance publishes nothing" from "you asked for a pack
    /// that does not exist", instead of degrading on a fabricated empty list.
    /// </summary>
    public AssetPackManifest Get(string packId)
    {
        string normalized = (packId ?? string.Empty).Trim();
        return catalog.Manifests.FirstOrDefault(manifest =>
                string.Equals(manifest.PackId, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConfigurationException("asset_pack_not_found", $"No asset pack named '{normalized}' is published by this instance.");
    }
}