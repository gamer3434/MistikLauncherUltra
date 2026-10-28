using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class ModAutoUpdateTests
{
    static readonly Type Type = typeof(App).Assembly.GetType("MistikLauncher.ModAutoUpdater")!;
    const BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;
    public static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        bool Accept(string constraint, string version) => (bool)Type.GetMethod("Accepts", Methods)!.Invoke(null, new object[] { JToken.FromObject(constraint), version })!;
        Check(Accept(">=1.1 <2.0", "1.5.0") && !Accept(">=1.1 <2.0", "2.0.0") && Accept("1.11.4-p2", "1.11.4-p2") &&
            !Accept("1.11.4-p2", "1.11.8") && !Accept("^1.0", "1.2.0"), "automatic mod updates enforce numeric ranges and exact prerelease pins, declining unsupported predicates");
        Check(new LauncherConfig().ModAutoUpdate && Newtonsoft.Json.JsonConvert.DeserializeObject<LauncherConfig>("{\"mod_auto_update\":false}")!.ModAutoUpdate == false,
            "automatic mod updates default on and the saved opt-out is retained");
        async Task<IDisposable> Prepare(HttpClient client, string directory, CancellationToken token = default)
        {
            var task = (Task)Type.GetMethod("PrepareAsync")!.Invoke(null, new object[] { client, directory, "1.21.11", "fabric", token })!;
            await task;
            return (IDisposable)task.GetType().GetProperty("Result")!.GetValue(task)!;
        }
        void Apply(IDisposable plan)
        {
            try { Type.GetMethod("Apply")!.Invoke(plan, null); }
            catch(TargetInvocationException error) { throw error.InnerException!; }
        }
        foreach (var scenario in new[] { "success", "wrong-game", "wrong-loader", "beta", "older", "downgrade", "checksum", "new-java", "pin", "alias-pin", "missing-dependency", "stale", "collision", "network", "cancel", "locked" })
        {
            var directory = Path.Combine(root, scenario); Directory.CreateDirectory(directory);
            var old = Jar("leaf_mod", "1.0.0", alias: scenario == "alias-pin" ? "leaf_alias" : null);
            File.WriteAllBytes(Path.Combine(directory, "old.jar"), old);
            var local = Jar("private_mod", "1.0.0"); File.WriteAllBytes(Path.Combine(directory, "local.jar"), local);
            var disabled = Jar("disabled_mod", "1.0.0"); File.WriteAllBytes(Path.Combine(directory, "disabled.jar.disabled"), disabled);
            if (scenario == "pin") File.WriteAllBytes(Path.Combine(directory, "dependent.jar"), Jar("dependent_mod", "1.0.0", new JObject { ["leaf_mod"] = "1.0.0" }));
            if (scenario == "alias-pin") File.WriteAllBytes(Path.Combine(directory, "dependent.jar"), Jar("dependent_mod", "1.0.0", new JObject { ["leaf_alias"] = "1.0.0" }));
            if (scenario == "collision") File.WriteAllBytes(Path.Combine(directory, "new.jar"), local);
            var originals = ModFiles.List(directory).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
            using var handler = new Handler(old, scenario); using var client = new HttpClient(handler);
            using var cancellation = new CancellationTokenSource(); if (scenario == "cancel") cancellation.Cancel();
            IDisposable? plan = null;
            bool failed = false;
            try
            {
                plan = await Prepare(client, directory, cancellation.Token);
                if (scenario == "stale") File.WriteAllBytes(Path.Combine(directory, "old.jar"), local);
                using var locked = scenario == "locked" ? new FileStream(Path.Combine(directory, "old.jar"), FileMode.Open, FileAccess.Read, FileShare.None) : null;
                Apply(plan);
                if (scenario == "success")
                {
                    string backup = (string)Type.GetProperty("Backup")!.GetValue(plan)!;
                    Check(!File.Exists(Path.Combine(directory, "old.jar")) && File.ReadAllBytes(Path.Combine(directory, "new.jar")).SequenceEqual(handler.NewBytes) &&
                        File.ReadAllBytes(Path.Combine(backup, "old.jar")).SequenceEqual(old) && File.ReadAllBytes(Path.Combine(directory, "disabled.jar.disabled")).SequenceEqual(disabled) &&
                        File.ReadAllBytes(Path.Combine(directory, "local.jar")).SequenceEqual(local),
                        "automatic update replaces the old filename once, verifies downloads, backs up originals and preserves disabled/private mods; count=" + Type.GetProperty("Count")!.GetValue(plan) + "; requests=" + string.Join(",",handler.Requests));
                }
                else if (scenario is "wrong-game" or "wrong-loader" or "beta" or "older" or "downgrade" or "new-java")
                    Check((int)Type.GetProperty("Count")!.GetValue(plan)! == 0 && originals.All(file => File.ReadAllBytes(Path.Combine(directory, file.Key!)).SequenceEqual(file.Value)),
                        "automatic update skips " + scenario + " without changing installed files");
            }
            catch(Exception error) when (error is IOException or InvalidDataException or HttpRequestException or OperationCanceledException)
            { failed = true; }
            finally { plan?.Dispose(); }
            if (scenario is "checksum" or "pin" or "alias-pin" or "missing-dependency" or "collision" or "network" or "cancel" or "locked")
                Check(failed && originals.All(file => File.ReadAllBytes(Path.Combine(directory, file.Key!)).SequenceEqual(file.Value)),
                    "automatic update preserves all originals after " + scenario);
            if (scenario == "stale") Check(failed && File.ReadAllBytes(Path.Combine(directory,"old.jar")).SequenceEqual(local), "stale automatic update cannot overwrite a concurrent mod change");
            Check(handler.Requests.All(uri => uri.Host is "api.modrinth.com" or "cdn.modrinth.com"), "automatic updater uses official hosts: " + scenario);
        }
        return checks;
    }

    static byte[] Jar(string id, string version, JObject? depends = null, string? alias = null)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("fabric.mod.json").Open());
            var metadata = new JObject { ["schemaVersion"] = 1, ["id"] = id, ["version"] = version, ["depends"] = depends ?? new JObject { ["minecraft"] = "1.21.11", ["java"] = ">=21" } };
            if(alias != null) metadata["provides"] = new JArray(alias);
            writer.Write(metadata);
        }
        return output.ToArray();
    }

    sealed class Handler : HttpMessageHandler
    {
        public readonly byte[] NewBytes;
        readonly byte[] old;
        readonly string scenario;
        public List<Uri> Requests = new();
        public Handler(byte[] old, string scenario)
        {
            this.old = old; this.scenario = scenario;
            NewBytes = Jar("leaf_mod", scenario == "downgrade" ? "0.9.0" : "1.1.0", scenario == "new-java" ? new JObject { ["minecraft"] = "1.21.11", ["java"] = ">=25" } : null, scenario == "alias-pin" ? "leaf_alias" : null);
        }
        JObject Version(bool newer)
        {
            var bytes = newer ? NewBytes : old;
            return new JObject { ["id"] = newer ? "NEXT0001" : "CURR0001", ["project_id"] = "LEAF0001", ["version_type"] = newer && scenario == "beta" ? "beta" : "release",
                ["date_published"] = newer && scenario != "older" ? "2026-10-28T00:00:00Z" : "2026-10-01T00:00:00Z",
                ["game_versions"] = new JArray(newer && scenario == "wrong-game" ? "1.21.1" : "1.21.11"),
                ["loaders"] = new JArray(newer && scenario == "wrong-loader" ? "forge" : "fabric"),
                ["dependencies"] = newer && scenario == "missing-dependency" ? new JArray(new JObject { ["dependency_type"] = "required", ["project_id"] = "MISSING1" }) : new JArray(),
                ["files"] = new JArray(new JObject { ["filename"] = newer ? "new.jar" : "old.jar", ["primary"] = true,
                    ["url"] = "https://cdn.modrinth.com/data/LEAF0001/versions/NEXT0001/new.jar",
                    ["hashes"] = new JObject { ["sha512"] = newer && scenario == "checksum" ? new string('0',128) : Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant() } }) };
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Requests.Add(request.RequestUri!);
            if (scenario == "network") throw new HttpRequestException("Offline fixture");
            if (request.RequestUri!.Host == "cdn.modrinth.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NewBytes) });
            JToken body = request.Method == HttpMethod.Post ? new JObject { [Convert.ToHexString(SHA512.HashData(old)).ToLowerInvariant()] = Version(false) } : new JArray(Version(true));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString()) });
        }
    }
}
