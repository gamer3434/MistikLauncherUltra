using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class LauncherSupportTests
{
    sealed class Metadata : HttpMessageHandler
    {
        public int Requests;
        public string Project = "{\"id\":\"AABB0011\",\"project_type\":\"mod\"}";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/version") ? "[]" : Project) });
        }
    }
    sealed class Fake : HttpMessageHandler
    {
        public readonly List<(string Method, string Url, string Body, bool Conditional)> Requests = new();
        public string Mailbox = "null";
        public bool RejectDelete;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellation);
            lock (Requests) Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, body, request.Headers.IfMatch.Count > 0));
            var url = request.RequestUri!.AbsoluteUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var text = "null";
            if (url.Contains("accounts:signUp")) text = "{\"localId\":\"supportFixture\",\"idToken\":\"fixture-token\",\"refreshToken\":\"fixture-support-secret\"}";
            else if (url.Contains("securetoken.googleapis.com")) text = "{\"user_id\":\"supportFixture\",\"id_token\":\"fixture-token\",\"refresh_token\":\"fixture-support-secret\"}";
            else if (url.Contains("/requests/") && request.Method == HttpMethod.Get) {
                text = Mailbox; response.Headers.ETag = new EntityTagHeaderValue("\"mailbox-fixture\"");
            } else if (url.Contains("/requests/") && request.Method == HttpMethod.Delete && request.Headers.IfMatch.Count > 0 && RejectDelete)
                response.StatusCode = HttpStatusCode.PreconditionFailed;
            response.Headers.Date = DateTimeOffset.UtcNow;
            response.Content = new StringContent(text); return response;
        }
    }
    public static async Task<int> Run(string directory)
    {
        int count = 0;
        void Check(bool okay, string name) { if (!okay) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
        Check(!new LauncherConfig().SupportSharingEnabled && !new LauncherConfig().SupportRequestsEnabled,
            "named support and remote installation suggestions each require separate opt-in");
        var options = new LauncherMetricsOptions("fixture_public_api_key_123456", "https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com/");
        var fake = new Fake(); using var http = new HttpClient(fake);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject Request(long time, string id = "0123456789abcdef0123456789abcdef") => new() {
            ["id"] = id, ["projectId"] = "AABB0011", ["projectName"] = "Fixture mod", ["profile"] = "fabric-test", ["createdAt"] = time };
        var parse = typeof(LauncherSupport).GetMethod("ParseRequest", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? Parse(JObject request, string profile = "fabric-test") => parse.Invoke(null, new object[] { request, profile, now });
        Check(Parse(Request(now)) is SupportRequest && Parse(Request(now - 600001)) == null && Parse(Request(now + 60001)) == null && Parse(Request(now), "other-profile") == null,
            "support requests reject stale, future or changed-profile deliveries");
        var injected = Request(now); injected["url"] = "https://example.invalid/mod.jar";
        var badId = Request(now); badId["projectId"] = "../../bad";
        Check(Parse(injected) == null && Parse(badId) == null, "support requests accept only the bounded ID protocol and reject injected download URLs");
        JObject Snapshot() => new() { ["username"] = "FixturePlayer", ["profile"] = "fabric-test", ["version"] = "6.2.38", ["lastSeen"] = new JObject { [".sv"] = "timestamp" }, ["mods"] = new JArray(), ["modCount"] = 0, ["truncated"] = false, ["allowRequests"] = true };
        int approvals = 0; var approved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new LauncherSupport(options, http, directory, _ => Task.FromResult(Snapshot()), (_, _) => { Interlocked.Increment(ref approvals); approved.TrySetResult(); return Task.CompletedTask; });
        await service.HeartbeatAsync(); await service.SetEnabledAsync(false);
        Check(fake.Requests.Count == 0 && !Directory.Exists(directory), "disabled support performs no authentication, filesystem writes or network calls");
        fake.Mailbox = Request(now).ToString();
        await service.SetEnabledAsync(true); await service.HeartbeatAsync();
        Check(await Task.WhenAny(approved.Task, Task.Delay(3000)) == approved.Task && approvals == 1,
            "a valid admin suggestion reaches the recipient approval callback once");
        await service.HeartbeatAsync();
        Check(approvals == 1 && fake.Requests.Any(r => r.Method == "DELETE" && r.Conditional),
            "persisted request watermark prevents replay and consumption uses an ETag conditional delete");
        var credential = Path.Combine(directory, "support.refresh.dpapi");
        Check(File.Exists(credential) && !File.Exists(Path.Combine(directory, "metrics.refresh.dpapi")) && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(credential)).Contains("fixture-support-secret"),
            "support uses its own DPAPI credential without altering anonymous metrics consent");
        fake.RejectDelete = true; fake.Mailbox = Request(now + 1, "1123456789abcdef0123456789abcdef").ToString();
        await service.HeartbeatAsync();
        Check(approvals == 1, "a replaced mailbox cannot be approved when conditional consumption fails");
        fake.RejectDelete = false; fake.Mailbox = Request(now - 700000).ToString();
        var deletes = fake.Requests.Count(r => r.Method == "DELETE" && r.Conditional);
        await service.HeartbeatAsync();
        Check(approvals == 1 && fake.Requests.Count(r => r.Method == "DELETE" && r.Conditional) == deletes + 1,
            "stale requests are conditionally discarded so the single mailbox does not remain blocked");
        await service.StopAsync();
        Check(File.Exists(credential) && fake.Requests.Last().Url.Contains("/devices/"), "shutdown removes named presence while retaining the opted-in support identity");
        await service.SetEnabledAsync(false);
        Check(!File.Exists(credential) && fake.Requests.Any(r => r.Method == "DELETE" && r.Url.Contains("/requests/") && !r.Conditional),
            "support opt-out removes shared presence, pending suggestions and its separate credential");
        foreach (bool stopOnly in new[] { true, false }) {
            var cancelFake = new Fake(); using var cancelHttp = new HttpClient(cancelFake);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tokenCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int continuedActions = 0;
            var cancelService = new LauncherSupport(options, cancelHttp, Path.Combine(directory, stopOnly ? "stop-during-approval" : "opt-out-during-approval"),
                _ => Task.FromResult(Snapshot()), async (_, approvalToken) => {
                    started.TrySetResult();
                    try { await Task.Delay(Timeout.Infinite, approvalToken); Interlocked.Increment(ref continuedActions); }
                    catch (OperationCanceledException) when (approvalToken.IsCancellationRequested) { tokenCancelled.TrySetResult(); }
                });
            await cancelService.SetEnabledAsync(true);
            await cancelService.HeartbeatAsync();
            cancelFake.Mailbox = Request(now).ToString();
            var pendingHeartbeat = cancelService.HeartbeatAsync(); // Deliberately no caller cancellation token.
            try {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                if (stopOnly) await cancelService.StopAsync(); else await cancelService.SetEnabledAsync(false);
                Check(await Task.WhenAny(tokenCancelled.Task, Task.Delay(3000)) == tokenCancelled.Task && continuedActions == 0,
                    stopOnly ? "shutdown cancels approval from a default-token manual heartbeat before any continuing action"
                        : "opt-out cancels approval from a default-token manual heartbeat before any continuing action");
                await pendingHeartbeat.WaitAsync(TimeSpan.FromSeconds(3));
            } finally { await cancelService.SetEnabledAsync(false); }
        }
        var mods = Path.Combine(directory, "inventory"); Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "first.jar"), "fixture"); File.WriteAllText(Path.Combine(mods, "second.jar.disabled"), "fixture");
        var inventory = typeof(LauncherSupport).GetMethod("Inventory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var payload = (JObject)inventory.Invoke(null, new object[] { "FixturePlayer", "6.2.38", "fabric-test", mods, false })!;
        Check((int)payload["modCount"]! == 2 && payload["mods"]!.Children().All(mod => !((string)mod["name"]!).Contains(Path.DirectorySeparatorChar)) &&
            payload["mods"]!.Children().Count(mod => (bool)mod["enabled"]!) == 1 && !payload.Properties().Any(p => p.Name is "email" or "path" or "packName"),
            "support inventory reports actual filenames and enabled state without local paths or inferred modpack names");
        for (int i = 0; i < 300; i++) File.WriteAllText(Path.Combine(mods, $"fixture-{i:000}.jar"), "fixture");
        payload = (JObject)inventory.Invoke(null, new object[] { "FixturePlayer", "6.2.38", "fabric-test", mods, false })!;
        Check((int)payload["modCount"]! == 302 && payload["mods"]!.Count() == 300 && (bool)payload["truncated"]!,
            "large support inventories expose the true count and explicitly mark the 300-file display limit");
        var metadata = new Metadata(); using var metadataHttp = new HttpClient(metadata);
        var owner = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        owner.Config = new LauncherConfig { Version = "1.21.1" };
        var page = (ModManagerPage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModManagerPage));
        typeof(ModManagerPage).GetField("_main", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, owner);
        typeof(ModManagerPage).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, metadataHttp);
        var installer = typeof(ModManagerPage).GetMethod("InstallSupportMod", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task<bool> Reject(string profile, CancellationToken cancellation = default) {
            try { await (Task)installer.Invoke(page, new object[] { "AABB0011", profile, cancellation })!; return false; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException) { return true; }
        }
        Check(await Reject("changed-profile") && metadata.Requests == 0, "support installer rejects a changed selected profile before any metadata request");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Check(await Reject("1.21.1", cancelled.Token) && metadata.Requests == 0, "cancelled support installation cannot fetch metadata or change files");
        metadata.Project = "{\"id\":\"AABB0011\",\"project_type\":\"modpack\"}";
        Check(await Reject("1.21.1") && metadata.Requests == 1, "support installer rejects a modpack disguised as a requested mod before reading versions");
        metadata.Project = "{\"id\":\"AABB0011\",\"project_type\":\"mod\"}";
        Check(await Reject("1.21.1") && metadata.Requests == 3, "support installer rejects projects without a compatible version before downloading files");
        return count;
    }
}
