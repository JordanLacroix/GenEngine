using System.Net;
using System.Text.Json;

using GenEngine.Configuration.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GenEngine.Services.Tests;

/// <summary>
/// The bytes are served over a real pipeline rather than asserted on the
/// provider alone: the failure this guards against is a player hearing nothing,
/// and that only shows up as a status code and a content type on the wire.
/// </summary>
public sealed class AssetPackFileServingTests
{
    /// <summary>
    /// OGG Vorbis is decoded neither by Safari nor by <c>AVAudioPlayer</c>, so a
    /// pack limited to it is silent on every Apple device. AAC in an MP4
    /// container is the format that closes that hole.
    /// </summary>
    [Theory]
    [InlineData("sound.m4a", "audio/mp4")]
    [InlineData("sound.aac", "audio/aac")]
    [InlineData("sound.mp3", "audio/mpeg")]
    [InlineData("sound.ogg", "audio/ogg")]
    [InlineData("icon.png", "image/png")]
    [InlineData("frame.svg", "image/svg+xml")]
    public async Task AServableExtensionIsReturnedWithItsDeclaredContentType(string file, string expectedMediaType)
    {
        using TemporaryPack pack = TemporaryPack.WithFiles(file);
        using IHost host = await StartAsync(pack);

        using HttpResponseMessage response = await host.GetTestClient()
            .GetAsync($"/asset-packs/probe/files/{file}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    /// <summary>
    /// The mount stays an allow-list: an unlisted extension must not be guessed
    /// at, sniffed or served as <c>application/octet-stream</c>.
    /// </summary>
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("clip.wav")]
    public async Task AnExtensionOutsideTheAllowListIsNotServed(string file)
    {
        using TemporaryPack pack = TemporaryPack.WithFiles(file);
        using IHost host = await StartAsync(pack);

        using HttpResponseMessage response = await host.GetTestClient()
            .GetAsync($"/asset-packs/probe/files/{file}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void EveryDeclaredMediaTypeIsResolvedFromItsExtension()
    {
        var provider = AssetPackMediaTypes.CreateContentTypeProvider();

        foreach ((string extension, string mediaType) in AssetPackMediaTypes.ByExtension)
        {
            Assert.True(provider.TryGetContentType($"asset{extension}", out string? resolved));
            Assert.Equal(mediaType, resolved);
        }

        Assert.False(provider.TryGetContentType("asset.wav", out _));
    }

    private static async Task<IHost> StartAsync(TemporaryPack pack)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new FileSystemAssetPackCatalog(pack.Root));

        WebApplication app = builder.Build();
        app.MapAssetPackFiles();
        await app.StartAsync(CancellationToken.None);
        return app;
    }

    /// <summary>
    /// A throwaway pack on disk: the shipped pack contains no audio beyond OGG,
    /// so it cannot prove what the mount does with an AAC file.
    /// </summary>
    private sealed class TemporaryPack : IDisposable
    {
        private TemporaryPack(string root) => Root = root;

        public string Root { get; }

        public static TemporaryPack WithFiles(params string[] files)
        {
            string root = Path.Combine(Path.GetTempPath(), $"genengine-serving-{Guid.NewGuid():n}");
            string packDirectory = Path.Combine(root, "probe");
            Directory.CreateDirectory(packDirectory);

            foreach (string file in files)
            {
                File.WriteAllBytes(Path.Combine(packDirectory, file), [0x00, 0x01, 0x02, 0x03]);
            }

            File.WriteAllText(Path.Combine(packDirectory, "asset-manifest.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                packId = "probe",
                assets = files.Select(file => new { id = file, kind = "sfx", path = file }).ToArray(),
            }));

            return new TemporaryPack(root);
        }

        public void Dispose() => Directory.Delete(Root, true);
    }
}