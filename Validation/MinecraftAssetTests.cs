using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class MinecraftAssetTests
{
    static string Hash(byte[] data) => Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
    static JObject Index(byte[] data) => new() { ["objects"] = new JObject { ["sound.ogg"] = new JObject { ["hash"] = Hash(data), ["size"] = data.Length } } };
    static string AssetPath(string root, byte[] data) => Path.Combine(root, "assets", "objects", Hash(data)[..2], Hash(data));
    public static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        byte[] data = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var index = Index(data); index["objects"]!["same-sound.ogg"] = index["objects"]!["sound.ogg"]!.DeepClone();
        var handler = new Fixture(data); using var client = new HttpClient(handler);
        Check(await GameRuntimeHealth.EnsureAssetsAsync(root, index, client) == 1 && handler.Requests == 1 && File.ReadAllBytes(AssetPath(root, data)).SequenceEqual(data),
            "asset installation validates content and shares one download for duplicate hashes");
        Check(await GameRuntimeHealth.EnsureAssetsAsync(root, index, client) == 0 && handler.Requests == 1,
            "healthy asset files are reused after size and SHA-1 verification without a network request");
        byte[] corrupt = data.Reverse().ToArray(); File.WriteAllBytes(AssetPath(root, data), corrupt);
        Check(await GameRuntimeHealth.EnsureAssetsAsync(root, index, client) == 1 && File.ReadAllBytes(AssetPath(root, data)).SequenceEqual(data),
            "same-size corrupted assets are repaired instead of being skipped merely because they exist");
        File.WriteAllBytes(AssetPath(root, data), corrupt); handler.Data = corrupt;
        bool rejected = false; try { await GameRuntimeHealth.EnsureAssetsAsync(root, index, client); } catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(AssetPath(root, data)).SequenceEqual(corrupt) && !Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Any(),
            "invalid downloaded asset surfaces failure, preserves previous bytes and removes staging files");
        int before = handler.Requests; var invalid = Index(data); invalid["objects"]!["sound.ogg"]!["hash"] = "../escape";
        rejected = false; try { await GameRuntimeHealth.EnsureAssetsAsync(root, invalid, client); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && handler.Requests == before, "invalid asset metadata is rejected before network or filesystem mutation");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        rejected = false; try { await GameRuntimeHealth.EnsureAssetsAsync(root, index, client, cancellationToken: cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected && handler.Requests == before, "cancelled asset installation cannot succeed from cache or start another request");
        var many = new JObject(); var byHash = new Dictionary<string, byte[]>();
        for (int item = 0; item < 32; item++) {
            var bytes = System.Text.Encoding.UTF8.GetBytes("parallel asset fixture " + item);
            many["asset-" + item] = new JObject { ["hash"] = Hash(bytes), ["size"] = bytes.Length }; byHash[Hash(bytes)] = bytes;
        }
        using var parallelHandler = new ParallelFixture(byHash); using var parallelClient = new HttpClient(parallelHandler);
        Check(await GameRuntimeHealth.EnsureAssetsAsync(Path.Combine(root, "parallel"), new JObject { ["objects"] = many }, parallelClient) == 32 &&
            parallelHandler.MaximumActive is > 1 and <= 8 && parallelHandler.Active == 0,
            "bulk asset installation keeps concurrent transfers bounded and releases them after completion");
        return checks;
    }
    public static async Task<int> Live(string root)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/Validation");
        var manifest = JObject.Parse(await client.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
        var version = ((JArray)manifest["versions"]!).Single(item => item["id"]?.ToString() == "1.21.1");
        var profile = JObject.Parse(await client.GetStringAsync(version["url"]!.ToString()));
        var official = JObject.Parse(await client.GetStringAsync(profile["assetIndex"]!["url"]!.ToString()));
        var selected = ((JObject)official["objects"]!).Properties().OrderBy(item => item.Value["size"]!.Value<long>()).Take(3).ToArray();
        var sample = new JObject { ["objects"] = new JObject(selected.Select(item => new JProperty(item.Name, item.Value.DeepClone()))) };
        int installed = await GameRuntimeHealth.EnsureAssetsAsync(root, sample, client);
        if (installed != 3 || await GameRuntimeHealth.EnsureAssetsAsync(root, sample, client) != 0) throw new Exception("Official assets must download once and verify from cache.");
        var first = selected[0].Value["hash"]!.ToString(); var path = Path.Combine(root, "assets", "objects", first[..2], first);
        File.WriteAllText(path, "damaged official asset");
        if (await GameRuntimeHealth.EnsureAssetsAsync(root, sample, client) != 1) throw new Exception("A damaged official asset must be repaired.");
        Console.WriteLine("PASS live official Minecraft1.21.1 assets install, cache verification and corruption repair");
        return 3;
    }
    sealed class Fixture(byte[] data) : HttpMessageHandler
    {
        public byte[] Data = data;
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Data) });
        }
    }
    sealed class ParallelFixture(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public int Active, MaximumActive;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            int active = Interlocked.Increment(ref Active), previous;
            do { previous = MaximumActive; } while (active > previous && Interlocked.CompareExchange(ref MaximumActive, active, previous) != previous);
            try {
                await Task.Delay(20, token);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(files[Path.GetFileName(request.RequestUri!.AbsolutePath)]) };
            }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
}
