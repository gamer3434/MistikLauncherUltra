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
        var owner = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        owner.Config = new LauncherConfig { Version = "fixture-profile" };
        using (var pending = new CancellationTokenSource())
        {
            typeof(MainWindow).GetField("_modUpdateCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, pending);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var ready = typeof(MainWindow).GetMethod("TakePreparedModUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, new object[] { "fixture-profile" });
            Check(ready == null && pending.IsCancellationRequested && timer.Elapsed < TimeSpan.FromSeconds(1),
                "launch cancels an unfinished background update and immediately uses existing mods without awaiting network work");
            typeof(MainWindow).GetField("_modUpdateCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, null);
        }
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
        foreach (var waitingScenario in new[] { "delayed", "delayed-update" })
        {
        var waitingDirectory = Path.Combine(root, "waiting-network-" + waitingScenario); Directory.CreateDirectory(waitingDirectory);
        var waitingBytes = Jar("leaf_mod", "1.0.0"); File.WriteAllBytes(Path.Combine(waitingDirectory, "old.jar"), waitingBytes);
        using (var handler = new Handler(waitingBytes, waitingScenario))
        using (var client = new HttpClient(handler))
        using (var pending = new CancellationTokenSource())
        {
            var preparation = Prepare(client, waitingDirectory, pending.Token);
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            typeof(MainWindow).GetField("_modUpdateCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, pending);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var ready = typeof(MainWindow).GetMethod("TakePreparedModUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, new object[] { "fixture-profile" });
            bool immediate = timer.Elapsed < TimeSpan.FromSeconds(1);
            bool cancelled = false; try { using var ignored = await preparation; } catch (OperationCanceledException) { cancelled = true; }
            Check(immediate && ready == null && cancelled && File.ReadAllBytes(Path.Combine(waitingDirectory, "old.jar")).SequenceEqual(waitingBytes),
                "Play cancels a genuinely blocked HTTP request without waiting or changing installed mods: " + waitingScenario);
            typeof(MainWindow).GetField("_modUpdateCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, null);
        }
        }
        var batchDirectory = Path.Combine(root, "batch-205"); Directory.CreateDirectory(batchDirectory);
        var fixtures = new Dictionary<string, JObject>();
        for (int i = 0; i < 205; i++)
        {
            var bytes = Jar("batch_" + i, "1.0.0");
            File.WriteAllBytes(Path.Combine(batchDirectory, i + ".jar"), bytes);
            string hash = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
            fixtures[hash] = new JObject { ["id"] = "V" + i.ToString("D7"), ["project_id"] = "P" + i.ToString("D7"),
                ["version_type"] = "release", ["game_versions"] = new JArray("1.21.11"), ["loaders"] = new JArray("fabric"),
                ["date_published"] = "2026-10-01T00:00:00Z", ["files"] = new JArray(new JObject { ["hashes"] = new JObject { ["sha512"] = hash } }) };
        }
        using (var batchHandler = new BatchHandler(fixtures))
        using (var batchClient = new HttpClient(batchHandler))
        using (var plan = await Prepare(batchClient, batchDirectory))
            Check((int)Type.GetProperty("Count")!.GetValue(plan)! == 0 && batchHandler.Lookups == 3 && batchHandler.Updates == 3 && batchHandler.ValidFilters,
                "205 installed projects use six bounded metadata requests with exact game, loader and release filters");
        foreach (bool failSecond in new[] { false, true })
        {
            var directory = Path.Combine(root, "paired-update-" + failSecond); Directory.CreateDirectory(directory);
            using var handler = new PairHandler(failSecond); using var client = new HttpClient(handler);
            File.WriteAllBytes(Path.Combine(directory, "leaf-old.jar"), handler.OldLeaf);
            File.WriteAllBytes(Path.Combine(directory, "dependent-old.jar"), handler.OldDependent);
            bool failed = false;
            try
            {
                using var plan = await Prepare(client, directory);
                Apply(plan);
            }
            catch (HttpRequestException) { failed = true; }
            Check(handler.Downloads == 2 && (failSecond ? failed && File.ReadAllBytes(Path.Combine(directory, "leaf-old.jar")).SequenceEqual(handler.OldLeaf) &&
                    File.ReadAllBytes(Path.Combine(directory, "dependent-old.jar")).SequenceEqual(handler.OldDependent) && ModFiles.List(directory).Count() == 2 :
                !failed && File.ReadAllBytes(Path.Combine(directory, "leaf-new.jar")).SequenceEqual(handler.NewLeaf) &&
                    File.ReadAllBytes(Path.Combine(directory, "dependent-new.jar")).SequenceEqual(handler.NewDependent) && ModFiles.List(directory).Count() == 2),
                "batched paired update honors pinned cross-project dependencies and retains the whole set on download failure: " + failSecond);
        }
        foreach (var scenario in new[] { "success", "unchanged", "missing-update", "wrong-game", "wrong-loader", "wrong-project", "beta", "older", "downgrade", "checksum", "new-java", "pin", "alias-pin", "missing-dependency", "stale", "collision", "network", "cancel", "locked" })
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
                else if (scenario is "unchanged" or "missing-update" or "wrong-game" or "wrong-loader" or "beta" or "older" or "downgrade" or "new-java")
                {
                    Check((int)Type.GetProperty("Count")!.GetValue(plan)! == 0 && originals.All(file => File.ReadAllBytes(Path.Combine(directory, file.Key!)).SequenceEqual(file.Value)),
                        "automatic update skips " + scenario + " without changing installed files");
                    var stage = (string)Type.GetField("stage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plan)!;
                    Check(!Directory.EnumerateFiles(stage, "*.jar").Any(), "no compatible update means no installed JARs are copied to staging: " + scenario);
                }
            }
            catch(Exception error) when (error is IOException or InvalidDataException or HttpRequestException or OperationCanceledException)
            { failed = true; }
            finally { plan?.Dispose(); }
            if (scenario is "wrong-project" or "checksum" or "pin" or "alias-pin" or "missing-dependency" or "collision" or "network" or "cancel" or "locked")
                Check(failed && originals.All(file => File.ReadAllBytes(Path.Combine(directory, file.Key!)).SequenceEqual(file.Value)),
                    "automatic update preserves all originals after " + scenario);
            if (scenario == "stale") Check(failed && File.ReadAllBytes(Path.Combine(directory,"old.jar")).SequenceEqual(local), "stale automatic update cannot overwrite a concurrent mod change");
            Check(handler.Requests.All(uri => uri.Host is "api.modrinth.com" or "cdn.modrinth.com"), "automatic updater uses official hosts: " + scenario);
            Check(handler.Requests.All(uri => !uri.AbsolutePath.Contains("/project/")), "automatic updater does not download per-project version history: " + scenario);
        }
        foreach (var mode in new[] { "ready", "wrong-profile", "disabled", "running" })
        {
            var directory = Path.Combine(root, "ready-" + mode); Directory.CreateDirectory(directory);
            var bytes = Jar("leaf_mod", "1.0.0"); File.WriteAllBytes(Path.Combine(directory, "old.jar"), bytes);
            using var handler = new Handler(bytes, "success"); using var client = new HttpClient(handler);
            using var plan = await Prepare(client, directory);
            owner.Config = new LauncherConfig { Version = "fixture-profile", ModAutoUpdate = mode != "disabled" };
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetField("_modUpdateProfile", flags)!.SetValue(owner, mode == "wrong-profile" ? "another-profile" : "fixture-profile");
            typeof(MainWindow).GetField("_preparedModUpdate", flags)!.SetValue(owner, plan);
            typeof(MainWindow).GetField("gameRunning", flags)!.SetValue(owner, mode == "running");
            int requests = handler.Requests.Count;
            var result = typeof(MainWindow).GetMethod("TakePreparedModUpdate", flags)!.Invoke(owner, new object[] { "fixture-profile" });
            Check((mode == "ready" ? ReferenceEquals(plan, result) : result == null) && handler.Requests.Count == requests &&
                typeof(MainWindow).GetField("_preparedModUpdate", flags)!.GetValue(owner) == null && File.ReadAllBytes(Path.Combine(directory, "old.jar")).SequenceEqual(bytes),
                "launch consumes only a prepared matching-profile update without network requests or live-file changes: " + mode);
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

    sealed class PairHandler(bool failSecond) : HttpMessageHandler
    {
        public int Downloads;
        public readonly byte[] OldLeaf = Jar("leaf_mod", "1.0.0"), NewLeaf = Jar("leaf_mod", "1.1.0");
        public readonly byte[] OldDependent = Jar("dependent_mod", "1.0.0", new JObject { ["leaf_mod"] = "1.0.0" });
        public readonly byte[] NewDependent = Jar("dependent_mod", "1.1.0", new JObject { ["leaf_mod"] = ">=1.1.0 <2.0" });
        static string Hash(byte[] value) => Convert.ToHexString(SHA512.HashData(value)).ToLowerInvariant();
        JObject Version(bool leaf, bool newer)
        {
            var bytes = leaf ? newer ? NewLeaf : OldLeaf : newer ? NewDependent : OldDependent;
            var filename = (leaf ? "leaf" : "dependent") + (newer ? "-new.jar" : "-old.jar");
            return new JObject {
                ["id"] = (leaf ? "LEAF" : "DEPN") + (newer ? "0002" : "0001"), ["project_id"] = leaf ? "LEAFPROJ" : "DEPNPROJ",
                ["version_type"] = "release", ["game_versions"] = new JArray("1.21.11"), ["loaders"] = new JArray("fabric"),
                ["date_published"] = newer ? "2026-10-28T00:00:00Z" : "2026-10-01T00:00:00Z",
                ["dependencies"] = !leaf ? new JArray(new JObject { ["dependency_type"] = "required", ["project_id"] = "LEAFPROJ", ["version_id"] = newer ? "LEAF0002" : "LEAF0001" }) : new JArray(),
                ["files"] = new JArray(new JObject { ["filename"] = filename, ["primary"] = true,
                    ["url"] = "https://cdn.modrinth.com/data/fixture/" + filename, ["hashes"] = new JObject { ["sha512"] = Hash(bytes) } })
            };
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "cdn.modrinth.com")
            {
                bool leaf = request.RequestUri.AbsolutePath.EndsWith("/leaf-new.jar");
                return new HttpResponseMessage(++Downloads == 2 && failSecond ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) {
                    Content = new ByteArrayContent(leaf ? NewLeaf : NewDependent)
                };
            }
            var body = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
            bool newer = request.RequestUri.AbsolutePath.EndsWith("/update");
            var matches = new JObject();
            foreach (var hash in ((JArray)body["hashes"]!).Values<string>())
                matches[hash!] = Version(hash == Hash(OldLeaf), newer);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(matches.ToString()) };
        }
    }

    sealed class BatchHandler(Dictionary<string, JObject> fixtures) : HttpMessageHandler
    {
        public int Lookups, Updates;
        public bool ValidFilters = true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
            var hashes = (JArray)body["hashes"]!;
            bool update = request.RequestUri!.AbsolutePath == "/v2/version_files/update";
            if (request.Method != HttpMethod.Post || request.RequestUri.Host != "api.modrinth.com" ||
                (!update && request.RequestUri.AbsolutePath != "/v2/version_files")) throw new Exception("Unexpected request.");
            if (update) Updates++; else Lookups++;
            ValidFilters &= (string?)body["algorithm"] == "sha512" && hashes.Count is > 0 and <= 100 && (!update ||
                JToken.DeepEquals(body["loaders"], new JArray("fabric")) && JToken.DeepEquals(body["game_versions"], new JArray("1.21.11")) &&
                JToken.DeepEquals(body["version_types"], new JArray("release")));
            var result = new JObject();
            foreach (var hash in hashes.Values<string>()) result[hash!] = fixtures[hash!].DeepClone();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(result.ToString()) };
        }
    }

    sealed class Handler : HttpMessageHandler
    {
        public readonly byte[] NewBytes;
        readonly byte[] old;
        readonly string scenario;
        public List<Uri> Requests = new();
        public TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Handler(byte[] old, string scenario)
        {
            this.old = old; this.scenario = scenario;
            NewBytes = Jar("leaf_mod", scenario == "downgrade" ? "0.9.0" : "1.1.0", scenario == "new-java" ? new JObject { ["minecraft"] = "1.21.11", ["java"] = ">=25" } : null, scenario == "alias-pin" ? "leaf_alias" : null);
        }
        JObject Version(bool newer)
        {
            var bytes = newer ? NewBytes : old;
            return new JObject { ["id"] = newer ? "NEXT0001" : "CURR0001", ["project_id"] = newer && scenario == "wrong-project" ? "WRONG001" : "LEAF0001", ["version_type"] = newer && scenario == "beta" ? "beta" : "release",
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
            if (scenario == "delayed" || scenario == "delayed-update" && request.RequestUri!.AbsolutePath.EndsWith("/update"))
            { Started.TrySetResult(true); return new TaskCompletionSource<HttpResponseMessage>().Task.WaitAsync(token); }
            if (scenario == "network") throw new HttpRequestException("Offline fixture");
            if (request.RequestUri!.Host == "cdn.modrinth.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NewBytes) });
            bool update = request.RequestUri.AbsolutePath.EndsWith("/update");
            JToken body = update && scenario == "missing-update" ? new JObject() : new JObject {
                [Convert.ToHexString(SHA512.HashData(old)).ToLowerInvariant()] = Version(update && scenario != "unchanged") };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString()) });
        }
    }
}
