using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class GameDownloadTests
{
    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root); int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        byte[] original = "healthy existing Minecraft bytes"u8.ToArray();
        byte[] next = "verified replacement Minecraft bytes"u8.ToArray();
        var path = Path.Combine(root, "client.jar");
        var hash = Convert.ToHexString(SHA1.HashData(next));
        async Task<bool> Fails(HttpContent content, string? digest = null, TimeSpan? timeout = null)
        {
            File.WriteAllBytes(path, original);
            using var client = new HttpClient(new Response(content)) { Timeout = timeout ?? TimeSpan.FromSeconds(5) };
            try { await VersionManagerPage.DownloadFileWithProgressAsync("https://example.invalid/client.jar", path, (_, _) => { }, 0, 100, client: client, expectedSha1: digest); return false; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException) { return true; }
        }
        var shortBody = new ByteArrayContent(next); shortBody.Headers.ContentLength = next.Length + 10;
        Check(await Fails(shortBody) && File.ReadAllBytes(path).SequenceEqual(original),
            "truncated game download preserves an existing client instead of truncating it");
        Check(await Fails(new ByteArrayContent(original), hash) && File.ReadAllBytes(path).SequenceEqual(original),
            "game download SHA-1 mismatch preserves the previous client");
        Check(await Fails(new ByteArrayContent(Array.Empty<byte>())) && File.ReadAllBytes(path).SequenceEqual(original),
            "empty game download is rejected without replacing installed bytes");
        Check(await Fails(new StreamContent(new HangingStream()), timeout: TimeSpan.FromMilliseconds(80)) && File.ReadAllBytes(path).SequenceEqual(original),
            "game download timeout covers a stalled response body and preserves the installed client");
        using (var client = new HttpClient(new Response(new ByteArrayContent(next))))
            await VersionManagerPage.DownloadFileWithProgressAsync("https://example.invalid/client.jar", path, (_, _) => { }, 0, 100, client: client, expectedSha1: hash, expectedSize: next.Length);
        Check(File.ReadAllBytes(path).SequenceEqual(next) && !Directory.GetFiles(root, "*.tmp").Any(),
            "verified game download atomically replaces the old client and leaves no staging residue");
        double progress = 0;
        using (var client = new HttpClient(new Response(new StreamContent(new UnknownLengthStream(next)))))
            await VersionManagerPage.DownloadFileWithProgressAsync("https://example.invalid/client.jar", path, (value, _) => progress = value, 0, 100,
                client: client, expectedSha1: hash, expectedSize: next.Length);
        Check(progress == 100 && File.ReadAllBytes(path).SequenceEqual(next),
            "game downloads without Content-Length use official size metadata for complete progress and verification");
        return checks;
    }
    public static async Task<int> Live(string root)
    {
        Directory.CreateDirectory(root);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/Validation");
        var manifest = JObject.Parse(await client.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
        var version = ((JArray)manifest["versions"]!).Single(item => item["id"]?.ToString() == "1.21.1");
        var profile = JObject.Parse(await client.GetStringAsync(version["url"]!.ToString()));
        var metadata = profile["downloads"]!["client"]!; var path = Path.Combine(root, "1.21.1.jar");
        await VersionManagerPage.DownloadFileWithProgressAsync(metadata["url"]!.ToString(), path, (_, _) => { }, 0, 100,
            client: client, expectedSha1: metadata["sha1"]!.ToString(), expectedSize: metadata["size"]!.Value<long>());
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0 || Directory.GetFiles(root, "*.tmp").Any()) throw new Exception("Official Minecraft client must be a complete verified archive without staging residue.");
        Console.WriteLine("PASS live official Minecraft1.21.1 client streamed, verified and opened as a complete JAR");
        return 1;
    }
    sealed class Response(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
    sealed class HangingStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    }
    sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
