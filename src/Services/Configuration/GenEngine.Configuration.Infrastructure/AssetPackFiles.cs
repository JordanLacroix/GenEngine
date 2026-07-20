using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Application;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace GenEngine.Configuration.Infrastructure;

/// <summary>
/// Raw shape of <c>asset-manifest.json</c> as it is committed under
/// <c>assets/</c>. It is deserialized as-is and then projected, so a change in
/// the published contract never forces a change in the shipped files.
/// </summary>
internal sealed record RawAssetPackManifest(
    int SchemaVersion,
    string? PackId,
    string? PackVersion,
    string? ConfigurationKey,
    string? Description,
    string? BasePath,
    Dictionary<string, string>? Palette,
    string? Recoloring,
    List<AssetPackGap>? Gaps,
    List<AssetPackSource>? Sources,
    List<RawAssetPackEntry>? Assets);

internal sealed record RawAssetPackEntry(
    string? Id,
    string? Kind,
    string? Role,
    string? Path,
    long Bytes,
    string? Sha256,
    string? SourceId,
    string? Usage,
    string? MediaType,
    AssetPackImage? Image,
    AssetPackAudio? Audio);

/// <summary>
/// Catalogue resolved once from the packs shipped in the image. The packs are
/// immutable content: reading them at startup keeps every request free of disk
/// I/O and makes a malformed pack a startup failure rather than a runtime
/// surprise on a player's first sound.
/// </summary>
public sealed class FileSystemAssetPackCatalog : IAssetPackCatalog
{
    public const string RequestPathRoot = "/asset-packs";

    private static readonly JsonSerializerOptions ManifestOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, string> directories = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemAssetPackCatalog(string rootPath)
    {
        RootPath = System.IO.Path.GetFullPath(rootPath);
        List<AssetPackManifest> manifests = [];
        if (Directory.Exists(RootPath))
        {
            foreach (string manifestPath in Directory
                .EnumerateFiles(RootPath, "asset-manifest.json", SearchOption.AllDirectories)
                .OrderBy(static file => file, StringComparer.Ordinal))
            {
                AssetPackManifest manifest = Read(manifestPath);
                if (!directories.TryAdd(manifest.PackId, System.IO.Path.GetDirectoryName(manifestPath)!))
                {
                    throw new InvalidOperationException($"Asset pack '{manifest.PackId}' is declared twice under '{RootPath}'.");
                }

                manifests.Add(manifest);
            }
        }

        Manifests = manifests;
    }

    public string RootPath { get; }

    public IReadOnlyList<AssetPackManifest> Manifests { get; }

    /// <summary>
    /// Physical directory holding the bytes of a published pack. It is the
    /// directory the manifest was found in, never a path derived from the pack
    /// identifier: the two differ today (<c>assets/diapason</c> ships
    /// <c>diapason-core</c>) and the identifier is the stable key, not the layout.
    /// </summary>
    public string DirectoryOf(AssetPackManifest manifest) => directories[manifest.PackId];

    private static AssetPackManifest Read(string manifestPath)
    {
        RawAssetPackManifest raw = JsonSerializer.Deserialize<RawAssetPackManifest>(File.ReadAllText(manifestPath), ManifestOptions)
            ?? throw new InvalidOperationException($"Asset manifest '{manifestPath}' is not a JSON object.");
        if (raw.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Asset manifest '{manifestPath}' declares unsupported schema version {raw.SchemaVersion}.");
        }

        string packId = raw.PackId ?? throw new InvalidOperationException($"Asset manifest '{manifestPath}' has no packId.");
        string filesBaseUrl = $"{RequestPathRoot}/{packId}/files";
        List<AssetPackEntry> assets = [];
        foreach (RawAssetPackEntry entry in raw.Assets ?? [])
        {
            string relative = entry.Path ?? throw new InvalidOperationException($"Asset '{entry.Id}' in '{manifestPath}' has no path.");
            // The manifest contract says a path is always relative and never
            // escapes the pack. Enforcing it here keeps a hand-edited manifest
            // from turning the static file mount into a directory traversal.
            if (System.IO.Path.IsPathRooted(relative) || relative.Split('/', '\\').Contains(".."))
            {
                throw new InvalidOperationException($"Asset '{entry.Id}' in '{manifestPath}' declares an unsafe path '{relative}'.");
            }

            assets.Add(new AssetPackEntry(
                entry.Id ?? throw new InvalidOperationException($"An asset in '{manifestPath}' has no id."),
                entry.Kind ?? string.Empty,
                entry.Role ?? string.Empty,
                $"{filesBaseUrl}/{relative.Replace('\\', '/')}",
                entry.Bytes,
                entry.Sha256 ?? string.Empty,
                entry.SourceId ?? string.Empty,
                entry.Usage ?? string.Empty,
                entry.MediaType ?? string.Empty,
                entry.Image,
                entry.Audio));
        }

        return new AssetPackManifest(
            raw.SchemaVersion,
            packId,
            raw.PackVersion ?? "0.0.0",
            raw.ConfigurationKey ?? string.Empty,
            raw.Description ?? string.Empty,
            filesBaseUrl,
            raw.Palette ?? new Dictionary<string, string>(StringComparer.Ordinal),
            raw.Recoloring ?? string.Empty,
            raw.Gaps ?? [],
            raw.Sources ?? [],
            assets);
    }
}

/// <summary>
/// The allow-list of media types a pack may serve, keyed by file extension.
/// It is published here rather than inlined in the static file mount so the
/// contract has a single definition that specs, tests and the mount all read.
/// </summary>
/// <remarks>
/// Audio is deliberately not limited to OGG Vorbis: neither Safari nor
/// <c>AVAudioPlayer</c> decodes it, so a pack shipping only OGG is silent on
/// every Apple device. AAC (in an MP4 container or raw) and MP3 are the two
/// families decoded everywhere, which is why they are servable even though the
/// packs shipped today contain none.
/// </remarks>
public static class AssetPackMediaTypes
{
    public static readonly IReadOnlyDictionary<string, string> ByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".ogg"] = "audio/ogg",
            [".m4a"] = "audio/mp4",
            [".aac"] = "audio/aac",
            [".mp3"] = "audio/mpeg",
        };

    /// <summary>
    /// Provider restricted to <see cref="ByExtension"/>. Types are declared
    /// rather than inferred: an unlisted extension must 404, and a listed one
    /// must never fall back to <c>application/octet-stream</c>, which a browser
    /// refuses to decode as audio.
    /// </summary>
    public static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        FileExtensionContentTypeProvider provider = new();
        provider.Mappings.Clear();
        foreach ((string extension, string mediaType) in ByExtension)
        {
            provider.Mappings[extension] = mediaType;
        }

        return provider;
    }
}

public static class AssetPackInfrastructureExtensions
{
    /// <summary>
    /// Packs live next to the application in the image. The default matches the
    /// repository layout so a developer run and a container run read the same
    /// files.
    /// </summary>
    public static IServiceCollection AddAssetPacks(this IServiceCollection services, IConfiguration configuration)
    {
        string root = configuration["AssetPacks:RootPath"] ?? "assets";
        FileSystemAssetPackCatalog catalog = new(System.IO.Path.IsPathRooted(root)
            ? root
            : System.IO.Path.Combine(AppContext.BaseDirectory, root));
        services.AddSingleton(catalog);
        services.AddSingleton<IAssetPackCatalog>(catalog);
        services.AddSingleton<AssetPackService>();
        return services;
    }

    /// <summary>
    /// Serves the bytes of every published pack. The files are read-only content
    /// baked into the image, which keeps the container filesystem read-only and
    /// the process non-root. They are immutable by construction — a change ships
    /// a new <c>packVersion</c> and new paths are not required because each
    /// response is content-addressed by the manifest's SHA-256 — so a long
    /// immutable cache is safe and keeps a session from refetching them.
    /// </summary>
    public static void MapAssetPackFiles(this WebApplication app)
    {
        FileSystemAssetPackCatalog catalog = app.Services.GetRequiredService<FileSystemAssetPackCatalog>();
        // Declared explicitly rather than relying on the framework defaults, and
        // kept an allow-list: an extension outside it is a 404, never a guess.
        FileExtensionContentTypeProvider contentTypes = AssetPackMediaTypes.CreateContentTypeProvider();

        foreach (AssetPackManifest manifest in catalog.Manifests)
        {
            string directory = catalog.DirectoryOf(manifest);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(directory),
                RequestPath = manifest.FilesBaseUrl,
                ContentTypeProvider = contentTypes,
                ServeUnknownFileTypes = false,
                OnPrepareResponse = static context =>
                {
                    context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                },
            });
        }
    }
}