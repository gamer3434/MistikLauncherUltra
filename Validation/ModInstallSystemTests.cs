using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class ModInstallSystemTests
{
    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public readonly List<Uri> Requests = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        { Requests.Add(request.RequestUri!); return Task.FromResult(respond(request)); }
    }
    static HttpResponseMessage Json(JToken value) => new(HttpStatusCode.OK) { Content = new StringContent(value.ToString()) };
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
    static Task<string?> Resolve(HttpClient client, string path) => (Task<string?>)typeof(ModManagerPage)
        .GetMethod("ResolveModrinthProject", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { client, path })!;

    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root); var checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var source = Path.Combine(root, "fabric_api-local.jar");
        var original = System.Text.Encoding.UTF8.GetBytes("local fixture runtime id: fabric_api"); File.WriteAllBytes(source, original);
        var hash = Hash(original);
        using (var handler = new Handler(_ => Json(new JObject { ["project_id"] = "canonical-project", ["files"] = new JArray(new JObject { ["hashes"] = new JObject { ["sha512"] = hash } }) })))
        using (var client = new HttpClient(handler))
            Check(await Resolve(client, source) == "canonical-project" && handler.Requests.Single().AbsolutePath == "/v2/version_file/" + hash &&
                handler.Requests.Single().Query == "?algorithm=sha512" && File.ReadAllBytes(source).SequenceEqual(original),
                "migration resolves canonical project by exact SHA512 rather than runtime mod ID or filename");
        using (var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))))
            Check(await Resolve(client, source) == null && File.ReadAllBytes(source).SequenceEqual(original),
                "unrecognized migration source remains untouched and is skipped instead of guessed");
        using (var client = new HttpClient(new Handler(_ => Json(new JObject { ["project_id"] = "wrong-project", ["files"] = new JArray(new JObject { ["hashes"] = new JObject { ["sha512"] = new string('0', 128) } }) }))))
        {
            bool rejected = false; try { await Resolve(client, source); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(source).SequenceEqual(original), "migration rejects metadata for a different local file before profile changes");
        }
        var bytes = new Dictionary<string, byte[]>();
        foreach (var project in new[] { "parent", "branch-b", "branch-c", "shared" }) bytes[project] = System.Text.Encoding.UTF8.GetBytes("fixture mod " + project);
        JObject Version(string project) => new() {
            ["id"] = project + "-version", ["project_id"] = project, ["version_type"] = "release", ["game_versions"] = new JArray("94.1"), ["loaders"] = new JArray("fabric"),
            ["dependencies"] = new JArray((project == "parent" ? new[] { "branch-b", "branch-c" } : project is "branch-b" or "branch-c" ? new[] { "shared" } : Array.Empty<string>())
                .Select(dependency => new JObject { ["project_id"] = dependency, ["dependency_type"] = "required" })),
            ["files"] = new JArray(new JObject { ["primary"] = true, ["filename"] = project + ".jar", ["url"] = "https://cdn.modrinth.com/data/fixture/" + project + ".jar", ["hashes"] = new JObject { ["sha512"] = Hash(bytes[project]) } })
        };
        using var graph = new Handler(request => {
            var uri = request.RequestUri!;
            if (uri.Host == "api.modrinth.com") return Json(new JArray(Version(uri.AbsolutePath.Split('/')[3])));
            var project = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes[project]) };
        });
        using var graphClient = new HttpClient(graph);
        var page = (ModManagerPage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModManagerPage));
        Task Install() => (Task)typeof(ModManagerPage).GetMethod("DownloadModAndDependencies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, new object?[] { "parent", "parent", new List<string>(), true, new HashSet<string>(), null, "94.1", "fabric", "fixture-profile", graphClient })!;
        var pool = Path.Combine(App.AppData, "mods_pool", GameProfiles.VersionPoolKey("94.1", "fabric"));
        Directory.CreateDirectory(pool); var existing = Path.Combine(pool, "existing-user.jar"); File.WriteAllText(existing, "existing target mod");
        await Install();
        Check(graph.Requests.Count(uri => uri.Host == "cdn.modrinth.com" && uri.AbsolutePath.EndsWith("shared.jar")) == 1 && !Directory.Exists(Path.Combine(pool, ".backups")),
            "diamond dependencies reuse verified installed shared file without repeated download or backup growth");
        await Install();
        Check(graph.Requests.Count(uri => uri.Host == "cdn.modrinth.com") == 4 && !Directory.Exists(Path.Combine(pool, ".backups")),
            "repeated install resolves dependencies but reuses all hash-verified JAR files");
        var shared = Path.Combine(pool, "shared.jar"); File.WriteAllText(shared, "corrupt shared mod");
        await Install();
        Check(File.ReadAllBytes(shared).SequenceEqual(bytes["shared"]) && Directory.GetFiles(Path.Combine(pool, ".backups")).Length == 1,
            "corrupt installed dependency is downloaded again and exact corrupt bytes backed up during repair");
        File.Move(shared, shared + ".disabled"); var requestCount = graph.Requests.Count(uri => uri.Host == "cdn.modrinth.com");
        bool disabledRejected = false; try { await Install(); } catch (IOException) { disabledRejected = true; }
        Check(disabledRejected && File.Exists(shared + ".disabled") && !File.Exists(shared) && graph.Requests.Count(uri => uri.Host == "cdn.modrinth.com") == requestCount &&
            File.ReadAllText(existing) == "existing target mod" && File.ReadAllBytes(Path.Combine(pool, "parent.jar")).SequenceEqual(bytes["parent"]),
            "failed required dependency keeps target mods and disabled state intact without downloads");
        return checks;
    }
}