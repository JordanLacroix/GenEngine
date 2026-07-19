using System.Security.Cryptography;
using System.Text.Json;

using GenEngine.Configuration.Application;
using GenEngine.Configuration.Infrastructure;

namespace GenEngine.Services.Tests;

/// <summary>
/// The pack shipped in the repository is the subject under test: no fixture
/// manifest stands in for it, so a drift between the committed bytes and the
/// published catalogue fails here rather than in a player's browser.
/// </summary>
public sealed class AssetPackCatalogTests
{
    private static readonly string[] ServableMediaTypes = ["image/svg+xml", "image/png", "audio/ogg"];

    [Fact]
    public void ShippedPackIsPublishedWithServableAndIntactPaths()
    {
        string root = Path.Combine(FindRepositoryRoot(), "assets");
        var catalog = new FileSystemAssetPackCatalog(root);
        var service = new AssetPackService(catalog);

        AssetPackSummary summary = Assert.Single(service.List());
        Assert.Equal("diapason-core", summary.PackId);
        Assert.Equal("/asset-packs/diapason-core/files", summary.FilesBaseUrl);

        AssetPackManifest manifest = service.Get("diapason-core");
        Assert.Equal(summary.AssetCount, manifest.Assets.Count);
        Assert.NotEmpty(manifest.Assets);
        Assert.NotEmpty(manifest.Gaps);

        string directory = catalog.DirectoryOf(manifest);
        foreach (AssetPackEntry asset in manifest.Assets)
        {
            Assert.StartsWith(manifest.FilesBaseUrl + "/", asset.Path, StringComparison.Ordinal);
            Assert.Contains(asset.MediaType, ServableMediaTypes);

            string relative = asset.Path[(manifest.FilesBaseUrl.Length + 1)..];
            string file = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(file), $"Asset '{asset.Id}' resolves to a missing file '{file}'.");
            Assert.Equal(asset.Bytes, new FileInfo(file).Length);
            Assert.Equal(asset.Sha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))));
        }
    }

    [Fact]
    public void UnknownPackIsNotFoundRatherThanEmpty()
    {
        var service = new AssetPackService(new FileSystemAssetPackCatalog(Path.Combine(FindRepositoryRoot(), "assets")));

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => service.Get("acme-brand"));

        Assert.Equal("asset_pack_not_found", exception.Code);
    }

    [Fact]
    public void AnEscapingPathIsRefusedInsteadOfMounted()
    {
        string root = Path.Combine(Path.GetTempPath(), $"genengine-pack-{Guid.NewGuid():n}");
        Directory.CreateDirectory(Path.Combine(root, "hostile"));
        File.WriteAllText(Path.Combine(root, "hostile", "asset-manifest.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            packId = "hostile",
            assets = new[] { new { id = "escape", kind = "icon", path = "../../appsettings.json" } },
        }));

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new FileSystemAssetPackCatalog(root));
            Assert.Contains("unsafe path", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
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