using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed record LauncherMetricsOptions(string ApiKey, string DatabaseUrl)
{
    public static LauncherMetricsOptions? Load(string path)
    {
        try {
            if (new FileInfo(path).Length > 4096) return null;
            var json = JObject.Parse(File.ReadAllText(path));
            var result = new LauncherMetricsOptions((string?)json["apiKey"] ?? "", (string?)json["databaseUrl"] ?? "");
            return result.Valid ? result : null;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or ArgumentException) { return null; }
    }
    public bool Valid => Regex.IsMatch(ApiKey, @"^[A-Za-z0-9_-]{20,100}$") &&
        Uri.TryCreate(DatabaseUrl, UriKind.Absolute, out var url) && url.Scheme == "https" && url.IsDefaultPort &&
        url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0 && url.AbsolutePath == "/" &&
        (url.Host == "mistiklauncher-9eb4b-default-rtdb.firebaseio.com" ||
         Regex.IsMatch(url.Host, @"^mistiklauncher-9eb4b-default-rtdb\.[a-z0-9-]+\.firebasedatabase\.app$"));
}

// Anonymous Firebase IDs identify consenting installations, never Minecraft accounts.
public sealed class LauncherMetrics
{
    public static readonly string[] ErrorCodes = { "MLU-INTEGRITY", "MLU-MEMORY", "MLU-JAVA", "MLU-MOD-DEPENDENCY", "MLU-MOD-MIXIN", "MLU-CLASSPATH", "MLU-FILE", "MLU-EXIT", "MLU-LAUNCH", "MLU-APP" };
    readonly LauncherMetricsOptions options;
    readonly HttpClient http;
    readonly string tokenPath, version;
    readonly SemaphoreSlim gate = new(1, 1);
    CancellationTokenSource? running;
    Task? loop;
    string? uid, token, refresh;
    DateTime expires;
    bool enabled;
    public bool Enabled => enabled;
    public LauncherMetrics(LauncherMetricsOptions settings, HttpClient client, string dataDirectory, string launcherVersion)
    {
        if (!settings.Valid || !Regex.IsMatch(launcherVersion, @"^\d+\.\d+\.\d+(?:-preview\.\d+)?$")) throw new ArgumentException("Invalid metrics configuration.");
        options = settings; http = client; version = launcherVersion;
        tokenPath = Path.Combine(dataDirectory, "metrics.refresh.dpapi");
    }
    static JObject Timestamp() => new() { [".sv"] = "timestamp" };
    JObject Presence() => new() { ["lastSeen"] = Timestamp(), ["version"] = version };
    async Task<JObject> JsonAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        using (request) {
            using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            if (text.Length > 16384) throw new IOException("Metrics response exceeds limit.");
            return text == "null" ? new JObject() : JObject.Parse(text);
        }
    }
    async Task AuthenticateAsync(CancellationToken cancellation)
    {
        if (token != null && expires > DateTime.UtcNow) return;
        if (refresh == null && File.Exists(tokenPath)) {
            var bytes = File.ReadAllBytes(tokenPath);
            if (bytes.Length > 16384) throw new IOException("Invalid metrics credential.");
            refresh = Encoding.UTF8.GetString(WindowsSecret.Transform(bytes, false));
        }
        JObject auth;
        if (refresh != null) {
            auth = await JsonAsync(new(HttpMethod.Post, "https://securetoken.googleapis.com/v1/token?key=" + options.ApiKey) {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh })
            }, cancellation).ConfigureAwait(false);
            uid = (string?)auth["user_id"]; token = (string?)auth["id_token"]; refresh = (string?)auth["refresh_token"];
        } else {
            auth = await JsonAsync(new(HttpMethod.Post, "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + options.ApiKey) {
                Content = new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json")
            }, cancellation).ConfigureAwait(false);
            uid = (string?)auth["localId"]; token = (string?)auth["idToken"]; refresh = (string?)auth["refreshToken"];
        }
        if (!Regex.IsMatch(uid ?? "", @"^[A-Za-z0-9_-]{1,128}$") || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refresh)) throw new IOException("Invalid anonymous authentication response.");
        expires = DateTime.UtcNow.AddMinutes(50);
        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        var temp = tokenPath + ".tmp";
        File.WriteAllBytes(temp, WindowsSecret.Transform(Encoding.UTF8.GetBytes(refresh), true));
        File.Move(temp, tokenPath, true);
    }
    string Url(string path) => options.DatabaseUrl.TrimEnd('/') + "/launcherMetrics/" + path + ".json?auth=" + Uri.EscapeDataString(token!);
    async Task WriteAsync(HttpMethod method, string path, JObject? value, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, Url(path));
        if (value != null) request.Content = new StringContent(value.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
    public async Task SetEnabledAsync(bool value)
    {
        // Cancel before waiting for the gate so disabling cannot race a heartbeat.
        if (!value) { enabled = false; running?.Cancel(); }
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            if (value) {
                if (enabled) return;
                enabled = true;
                running = new CancellationTokenSource();
                loop = RunAsync(running.Token);
            } else {
                enabled = false; running?.Cancel();
                if (refresh != null || File.Exists(tokenPath)) {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    try {
                        await AuthenticateAsync(cleanup.Token).ConfigureAwait(false);
                        foreach (var branch in new[] { "sessions", "errors", "installations" })
                            await WriteAsync(HttpMethod.Delete, branch + "/" + uid, null, cleanup.Token).ConfigureAwait(false);
                    } catch (Exception ex) { App.Log("Metrics deletion unavailable: " + ex.GetType().Name); }
                }
                if (File.Exists(tokenPath)) File.Delete(tokenPath);
                if (File.Exists(tokenPath + ".tmp")) File.Delete(tokenPath + ".tmp");
                uid = token = refresh = null; expires = default;
            }
        } finally { gate.Release(); }
    }
    async Task RunAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested) {
            await HeartbeatAsync(cancellation).ConfigureAwait(false);
            try { await Task.Delay(TimeSpan.FromSeconds(60), cancellation).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
    public async Task HeartbeatAsync(CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try {
            if (!enabled) return;
            await AuthenticateAsync(cancellation).ConfigureAwait(false);
            var existing = await JsonAsync(new(HttpMethod.Get, Url("installations/" + uid)), cancellation).ConfigureAwait(false);
            var installation = Presence();
            installation["firstSeen"] = existing["firstSeen"]?.DeepClone() ?? Timestamp();
            await WriteAsync(HttpMethod.Put, "installations/" + uid, installation, cancellation).ConfigureAwait(false);
            await WriteAsync(HttpMethod.Put, "sessions/" + uid, Presence(), cancellation).ConfigureAwait(false);
        } catch (Exception ex) { App.Log("Metrics heartbeat unavailable: " + ex.GetType().Name); }
        finally { gate.Release(); }
    }
    public async Task ReportErrorAsync(string code)
    {
        if (!enabled || !ErrorCodes.Contains(code)) return;
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            if (!enabled) return;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            await AuthenticateAsync(cancellation.Token).ConfigureAwait(false);
            var error = Presence(); error["code"] = code; error["count"] = new JObject { [".sv"] = new JObject { ["increment"] = 1 } };
            await WriteAsync(HttpMethod.Patch, "errors/" + uid + "/" + code, error, cancellation.Token).ConfigureAwait(false);
        } catch (Exception ex) { App.Log("Metrics error recording unavailable: " + ex.GetType().Name); }
        finally { gate.Release(); }
    }
    public async Task StopAsync()
    {
        enabled = false; running?.Cancel();
        if (loop != null) { try { await loop.ConfigureAwait(false); } catch (OperationCanceledException) { } }
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            enabled = false; running?.Cancel();
            if (token == null || uid == null) return;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await AuthenticateAsync(cancellation.Token).ConfigureAwait(false);
            await WriteAsync(HttpMethod.Delete, "sessions/" + uid, null, cancellation.Token).ConfigureAwait(false);
        } catch (Exception ex) { App.Log("Metrics session cleanup unavailable: " + ex.GetType().Name); }
        finally { gate.Release(); }
    }
}
