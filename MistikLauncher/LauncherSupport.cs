using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed record SupportRequest(string Id, string ProjectId, string ProjectName, string Profile, long CreatedAt);

// Named support sharing has its own consent and credential; anonymous metrics remain independent.
public sealed class LauncherSupport
{
    readonly LauncherMetricsOptions options;
    readonly HttpClient http;
    readonly string tokenPath, consumedPath;
    readonly Func<CancellationToken, Task<JObject>> snapshot;
    readonly Func<SupportRequest, CancellationToken, Task> approve;
    readonly SemaphoreSlim gate = new(1, 1);
    CancellationTokenSource? running;
    Task? loop;
    string? uid, token, refresh;
    DateTime expires;
    volatile bool enabled;
    public bool Enabled => enabled;

    public LauncherSupport(LauncherMetricsOptions options, HttpClient http, string directory,
        Func<CancellationToken, Task<JObject>> snapshot, Func<SupportRequest, CancellationToken, Task> approve)
    {
        if (!options.Valid) throw new ArgumentException("Invalid support destination.");
        this.options = options; this.http = http; this.snapshot = snapshot; this.approve = approve;
        tokenPath = Path.Combine(directory, "support.refresh.dpapi");
        consumedPath = Path.Combine(directory, "support.request.last");
    }

    internal static JObject Inventory(string username, string version, string profile, string directory, bool allowRequests)
    {
        if (username.Length is < 1 or > 32 || username.Any(char.IsControl) || !GameProfiles.SafeId(profile) || profile.Length > 160)
            throw new InvalidDataException("Invalid support profile.");
        var files = directory.Length > 0 && Directory.Exists(directory) ? ModFiles.List(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<string>();
        var mods = new JArray(files.Take(300).Select(path => {
            var name = Path.GetFileName(path);
            if (name.Length > 180 || name.Any(char.IsControl)) throw new InvalidDataException("Invalid support mod filename.");
            return new JObject { ["name"] = name, ["enabled"] = ModFiles.Enabled(path) };
        }));
        return new JObject { ["username"] = username, ["version"] = version, ["profile"] = profile,
            ["lastSeen"] = new JObject { [".sv"] = "timestamp" }, ["mods"] = mods,
            ["modCount"] = files.Length, ["truncated"] = files.Length > 300, ["allowRequests"] = allowRequests };
    }

    async Task<JObject> JsonAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        using (request) {
            using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            if (text.Length > 131072) throw new IOException("Support response exceeds limit.");
            return text == "null" ? new JObject() : JObject.Parse(text);
        }
    }

    async Task AuthenticateAsync(CancellationToken cancellation)
    {
        if (token != null && expires > DateTime.UtcNow) return;
        if (refresh == null && File.Exists(tokenPath)) {
            if (new FileInfo(tokenPath).Length > 16384) throw new IOException("Invalid support credential.");
            refresh = Encoding.UTF8.GetString(WindowsSecret.Transform(File.ReadAllBytes(tokenPath), false));
        }
        var auth = refresh == null
            ? await JsonAsync(new(HttpMethod.Post, "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + options.ApiKey) {
                Content = new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json") }, cancellation).ConfigureAwait(false)
            : await JsonAsync(new(HttpMethod.Post, "https://securetoken.googleapis.com/v1/token?key=" + options.ApiKey) {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }) }, cancellation).ConfigureAwait(false);
        uid = (string?)(auth["localId"] ?? auth["user_id"]); token = (string?)(auth["idToken"] ?? auth["id_token"]); refresh = (string?)(auth["refreshToken"] ?? auth["refresh_token"]);
        if (!Regex.IsMatch(uid ?? "", @"^[A-Za-z0-9_-]{1,128}$") || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refresh)) throw new IOException("Invalid support authentication.");
        expires = DateTime.UtcNow.AddMinutes(50);
        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        File.WriteAllBytes(tokenPath + ".tmp", WindowsSecret.Transform(Encoding.UTF8.GetBytes(refresh), true));
        File.Move(tokenPath + ".tmp", tokenPath, true);
    }
    string Url(string branch) => options.DatabaseUrl.TrimEnd('/') + "/launcherSupport/" + branch + "/" + uid + ".json?auth=" + Uri.EscapeDataString(token!);
    async Task WriteAsync(HttpMethod method, string branch, JObject? value, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, Url(branch));
        if (value != null) request.Content = new StringContent(value.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetEnabledAsync(bool value)
    {
        if (!value) { enabled = false; running?.Cancel(); }
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            if (value) {
                if (enabled) return;
                enabled = true; running = new CancellationTokenSource(); loop = RunAsync(running.Token);
            } else {
                enabled = false; running?.Cancel();
                if (refresh != null || File.Exists(tokenPath)) {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    // Keep the credential when offline so a later opt-out retry can remove the shared data.
                    await AuthenticateAsync(cleanup.Token).ConfigureAwait(false);
                    await WriteAsync(HttpMethod.Delete, "devices", null, cleanup.Token).ConfigureAwait(false);
                    await WriteAsync(HttpMethod.Delete, "requests", null, cleanup.Token).ConfigureAwait(false);
                    File.Delete(tokenPath); File.Delete(tokenPath + ".tmp");
                    uid = token = refresh = null; expires = default;
                }
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
        SupportRequest? pending = null;
        CancellationTokenSource? lifetime = null;
        try {
            await gate.WaitAsync(cancellation).ConfigureAwait(false);
            try {
                if (!enabled) return;
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation, running?.Token ?? CancellationToken.None);
                using var bounded = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                bounded.CancelAfter(TimeSpan.FromSeconds(20));
                var data = await snapshot(bounded.Token).ConfigureAwait(false);
                if (!enabled) return;
                await AuthenticateAsync(bounded.Token).ConfigureAwait(false);
                await WriteAsync(HttpMethod.Put, "devices", data, bounded.Token).ConfigureAwait(false);
                if (data["allowRequests"]?.Value<bool>() != true) {
                    await WriteAsync(HttpMethod.Delete, "requests", null, bounded.Token).ConfigureAwait(false); return;
                }
                using var get = new HttpRequestMessage(HttpMethod.Get, Url("requests")); get.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
                using var response = await http.SendAsync(get, bounded.Token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync(bounded.Token).ConfigureAwait(false);
                if (text.Length > 4096 || text == "null") return;
                var request = JObject.Parse(text);
                var now = response.Headers.Date?.ToUnixTimeMilliseconds() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var candidate = ParseRequest(request, (string?)data["profile"] ?? "", now);
                if (response.Headers.ETag == null) return;
                if (candidate != null) {
                    var watermark = File.Exists(consumedPath) && new FileInfo(consumedPath).Length < 1024 ? JObject.Parse(File.ReadAllText(consumedPath)) : new JObject();
                    if (candidate.Id != (string?)watermark["id"] && candidate.CreatedAt > ((long?)watermark["createdAt"] ?? -1)) {
                        File.WriteAllText(consumedPath + ".tmp", new JObject { ["id"] = candidate.Id, ["createdAt"] = candidate.CreatedAt }.ToString());
                        File.Move(consumedPath + ".tmp", consumedPath, true);
                        pending = candidate;
                    }
                }
                using var delete = new HttpRequestMessage(HttpMethod.Delete, Url("requests")); delete.Headers.IfMatch.Add(response.Headers.ETag);
                using var removed = await http.SendAsync(delete, bounded.Token).ConfigureAwait(false);
                if (!removed.IsSuccessStatusCode) pending = null;
            } catch (OperationCanceledException) { }
            catch (Exception ex) { App.Log("Support sync unavailable: " + ex.GetType().Name); }
            finally { gate.Release(); }
            if (pending != null && enabled && lifetime != null && !lifetime.IsCancellationRequested) {
                try { await approve(pending, lifetime.Token).ConfigureAwait(false); }
                catch (Exception ex) { App.Log("Support request failed: " + ex.GetType().Name); }
            }
        } finally { lifetime?.Dispose(); }
    }
    internal static SupportRequest? ParseRequest(JObject data, string profile, long now)
    {
        if (data.Properties().Select(p => p.Name).Order().SequenceEqual(new[] { "createdAt", "id", "profile", "projectId", "projectName" }) &&
            data["createdAt"]?.Type == JTokenType.Integer && data["id"]?.Type == JTokenType.String && data["projectId"]?.Type == JTokenType.String &&
            data["projectName"]?.Type == JTokenType.String && data["profile"]?.Type == JTokenType.String &&
            Regex.IsMatch((string)data["id"]!, "^[a-f0-9]{32}$") && Regex.IsMatch((string)data["projectId"]!, "^[A-Za-z0-9]{8}$") &&
            (string)data["profile"]! == profile && ((string)data["projectName"]!).Length is > 0 and <= 120 && !((string)data["projectName"]!).Any(char.IsControl) &&
            (long)data["createdAt"]! >= now - 600000 && (long)data["createdAt"]! <= now + 60000)
            return new((string)data["id"]!, (string)data["projectId"]!, (string)data["projectName"]!, profile, (long)data["createdAt"]!);
        return null;
    }
    public async Task StopAsync()
    {
        enabled = false; running?.Cancel();
        // The bounded HTTP operation is cancelled immediately; do not wait for an open approval dialog on shutdown.
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            enabled = false; running?.Cancel();
            if (token == null) return;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try { await WriteAsync(HttpMethod.Delete, "devices", null, cleanup.Token).ConfigureAwait(false); }
            catch (Exception ex) { App.Log("Support presence cleanup unavailable: " + ex.GetType().Name); }
        } finally { gate.Release(); }
    }
}
