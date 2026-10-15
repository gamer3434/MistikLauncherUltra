using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class ModVersionSelectionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Download = typeof(ModManagerPage).GetMethod("DownloadModAndDependencies", Private)!;
    static JToken Select(JObject version)
    {
        try { return (JToken)typeof(ModManagerPage).GetMethod("SelectModFile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { version })!; }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    sealed class Handler(JToken metadata) : HttpMessageHandler
    {
        public readonly List<Uri> Requests = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests.Add(request.RequestUri!);
            if (Requests.Count > 1 || request.RequestUri!.Host != "api.modrinth.com")
                throw new HttpRequestException("Wrong-version metadata must not trigger dependencies or CDN downloads.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata.ToString()) });
        }
    }
    static JObject Artifact(string name, bool primary = false) => new() { ["filename"] = name, ["primary"] = primary,
        ["url"] = "https://cdn.modrinth.com/data/fixture/" + name, ["hashes"] = new JObject { ["sha512"] = new string('a', 128) } };
    static JObject Version(string game, string loader) => new() { ["id"] = "fixture-version", ["project_id"] = "fixture-project", ["version_type"] = "release",
        ["game_versions"] = new JArray(game), ["loaders"] = new JArray(loader), ["files"] = new JArray(Artifact("fixture.jar", true)),
        ["dependencies"] = new JArray(new JObject { ["dependency_type"] = "required", ["project_id"] = "wrong-version-library" }) };

    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root); var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var sentinel = Path.Combine(root, "existing-mod.jar"); File.WriteAllText(sentinel, "existing profile mod bytes");
        foreach (var sample in new[] { (game: "1.21.11", requested: "fabric", available: "fabric"),
            (game: "1.21.1", requested: "fabric", available: "forge"), (game: "1.21.1", requested: "forge", available: "neoforge"),
            (game: "1.21.1", requested: "fabric", available: "quilt") })
        {
            foreach (var pinned in new[] { false, true })
            {
                var version = Version(sample.game, sample.available);
                using var handler = new Handler(pinned ? version : new JArray(version)); using var client = new HttpClient(handler);
                var page = (ModManagerPage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModManagerPage));
                var owner = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
                owner.Config = new LauncherConfig { Version = sample.requested + "-1.21.1-selection-" + Guid.NewGuid().ToString("N"), LastSyncedVersion = "untouched-previous-profile" };
                typeof(ModManagerPage).GetField("_main", Private)!.SetValue(page, owner);
                var installed = new List<string>(); var visited = new HashSet<string>();
                var operation = (Task)Download.Invoke(page, new object?[] { "fixture-project", "Fixture project", installed, pinned, visited,
                    pinned ? "fixture-version" : null, null, null, owner.Config.Version, client, CancellationToken.None })!;
                bool refused = false; try { await operation; } catch (ModConflictException) { refused = true; }
                Check(refused && handler.Requests.Count == 1 && handler.Requests[0].Host == "api.modrinth.com" && installed.Count == 0 && visited.Count == 0 &&
                    owner.Config.LastSyncedVersion == "untouched-previous-profile" && File.ReadAllText(sentinel) == "existing profile mod bytes",
                    $"{(pinned ? "pinned dependency" : "catalog installation")} rejects {sample.game}/{sample.available} for1.21.1/{sample.requested} before synchronization, dependency lookup or downloads");
            }
        }
        var selected = Artifact("chosen.jar", true);
        Check(ReferenceEquals(Select(new JObject { ["files"] = new JArray(Artifact("attachment.zip"), selected) }), selected),
            "mod installation selects the declared primary JAR rather than the first attachment");
        selected = Artifact("single.jar");
        Check(ReferenceEquals(Select(new JObject { ["files"] = new JArray(Artifact("attachment.zip"), selected) }), selected),
            "a single unambiguous JAR remains supported when primary metadata is omitted");
        foreach (var files in new[] { new JArray(Artifact("fabric.jar"), Artifact("forge.jar")), new JArray(Artifact("fabric.jar", true), Artifact("forge.jar", true)),
            new JArray(Artifact("attachment.zip", true), Artifact("mod.jar")), new JArray() })
        {
            var version = Version("1.21.1", "fabric"); version["files"] = files;
            using var handler = new Handler(new JArray(version)); using var client = new HttpClient(handler);
            var page = (ModManagerPage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModManagerPage));
            var operation = (Task)Download.Invoke(page, new object?[] { "fixture-project", "Fixture project", new List<string>(), false, new HashSet<string>(), null,
                "1.21.1", "fabric", "fixture-profile", client, CancellationToken.None })!;
            bool refused = false; try { await operation; } catch (InvalidDataException) { refused = true; }
            Check(refused && handler.Requests.Count == 1, "ambiguous or non-JAR release is rejected before required dependency downloads: " + files.Count + " files");
        }
        return checks;
    }
}
