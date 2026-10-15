using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using MistikLauncher;

static class LauncherMetricsTests
{
    sealed class Fake : HttpMessageHandler
    {
        public readonly List<(string Method, string Url, string Body)> Requests = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellation);
            lock (Requests) Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, body));
            string response = request.RequestUri!.Host switch {
                "identitytoolkit.googleapis.com" => "{\"localId\":\"anonymousFixture\",\"idToken\":\"fixture-token\",\"refreshToken\":\"fixture-refresh-secret\"}",
                "securetoken.googleapis.com" => "{\"user_id\":\"anonymousFixture\",\"id_token\":\"fixture-token\",\"refresh_token\":\"fixture-refresh-secret\"}",
                _ => request.Method == HttpMethod.Get ? "{\"firstSeen\":777,\"lastSeen\":888,\"version\":\"6.2.1\"}" : "null"
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }
    public static async Task<int> Run(string directory)
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
        var options = new LauncherMetricsOptions("fixture_public_api_key_123456", "https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com/");
        Check(options.Valid && !new LauncherMetricsOptions(options.ApiKey, "https://attacker.firebaseio.com/").Valid && !new LauncherMetricsOptions(options.ApiKey, options.DatabaseUrl + "other").Valid, "metrics configuration restricts destination to project root");
        Check(!new LauncherConfig().UsageMetricsEnabled, "anonymous metrics require explicit opt-in by default");
        var fake = new Fake(); using var http = new HttpClient(fake);
        var service = new LauncherMetrics(options, http, directory, "6.2.14");
        await service.HeartbeatAsync(); await service.ReportErrorAsync("MLU-APP"); await service.SetEnabledAsync(false);
        Check(fake.Requests.Count == 0 && !Directory.Exists(directory), "disabled metrics produce no authentication, files or requests");
        await service.SetEnabledAsync(true); await service.HeartbeatAsync();
        var installation = fake.Requests.Last(r => r.Method == "PUT" && r.Url.Contains("/installations/"));
        var json = JObject.Parse(installation.Body);
        Check((long?)json["firstSeen"] == 777 && (string?)json["version"] == "6.2.14" && json.Properties().Select(p => p.Name).Order().SequenceEqual(new[] { "firstSeen", "lastSeen", "version" }), "anonymous installation preserves first seen and sends only aggregate fields");
        var credential = Path.Combine(directory, "metrics.refresh.dpapi");
        Check(File.Exists(credential) && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(credential)).Contains("fixture-refresh-secret"), "metrics refresh token uses Windows DPAPI at rest");
        int before = fake.Requests.Count;
        await service.ReportErrorAsync("MLU-APP\nusername=Player"); await service.ReportErrorAsync("unknown");
        Check(fake.Requests.Count == before, "metrics reject unknown or injected diagnostic codes without requests");
        await service.ReportErrorAsync("MLU-MEMORY");
        var error = JObject.Parse(fake.Requests.Last(r => r.Method == "PATCH").Body);
        Check(error.Properties().Select(p => p.Name).Order().SequenceEqual(new[] { "code", "count", "lastSeen", "version" }) && (string?)error["code"] == "MLU-MEMORY" && (int?)error["count"]?[".sv"]?["increment"] == 1, "error telemetry sends category and atomic count without report text");
        await service.StopAsync();
        Check(fake.Requests.Last().Method == "DELETE" && fake.Requests.Last().Url.Contains("/sessions/") && File.Exists(credential), "normal shutdown removes online presence and preserves consenting installation identity");
        var reopened = new LauncherMetrics(options, http, directory, "6.2.14");
        await reopened.SetEnabledAsync(true); await reopened.HeartbeatAsync();
        Check(fake.Requests.Count(r => r.Url.Contains("accounts:signUp")) == 1 && fake.Requests.Any(r => r.Url.Contains("securetoken.googleapis.com")), "restart refreshes existing anonymous identity instead of counting another installation");
        await reopened.SetEnabledAsync(false);
        Check(!File.Exists(credential) && new[] { "/sessions/", "/errors/", "/installations/" }.All(branch => fake.Requests.Any(r => r.Method == "DELETE" && r.Url.Contains(branch))), "opt-out stops reporting and deletes own online, error and installation records plus local token");
        before = fake.Requests.Count; await reopened.HeartbeatAsync(); await reopened.ReportErrorAsync("MLU-MEMORY");
        Check(fake.Requests.Count == before, "opt-out prevents further heartbeat and diagnostic writes");
        var queuedDirectory = Path.Combine(directory, "queued-consent");
        var queuedFake = new Fake(); using var queuedHttp = new HttpClient(queuedFake);
        var queued = new LauncherMetrics(options, queuedHttp, queuedDirectory, "6.2.14");
        var gate = (SemaphoreSlim)typeof(LauncherMetrics).GetField("gate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(queued)!;
        await gate.WaitAsync();
        var pendingEnable = queued.SetEnabledAsync(true);
        var pendingDisable = queued.SetEnabledAsync(false);
        gate.Release();
        await Task.WhenAll(pendingEnable, pendingDisable);
        await queued.HeartbeatAsync();
        await queued.ReportErrorAsync("MLU-APP");
        bool consentStopped = !queued.Enabled && queuedFake.Requests.Count == 0 && !Directory.Exists(queuedDirectory);
        await queued.StopAsync();
        Check(consentStopped, "queued opt-out overrides an earlier pending enable without authentication, files or telemetry writes");
        var shutdown = new LauncherMetrics(options, queuedHttp, Path.Combine(directory, "queued-shutdown"), "6.2.14");
        var shutdownGate = (SemaphoreSlim)typeof(LauncherMetrics).GetField("gate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(shutdown)!;
        await shutdownGate.WaitAsync();
        pendingEnable = shutdown.SetEnabledAsync(true);
        var pendingStop = shutdown.StopAsync();
        shutdownGate.Release();
        await Task.WhenAll(pendingEnable, pendingStop);
        await shutdown.HeartbeatAsync();
        Check(!shutdown.Enabled && queuedFake.Requests.Count == 0 && !Directory.Exists(Path.Combine(directory, "queued-shutdown")),
            "shutdown stops an earlier queued enable before any telemetry authentication or file creation");
        return count;
    }
}
