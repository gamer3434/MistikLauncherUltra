using System.Net;
using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

internal static class MistikPresence
{
    private const string ApiKey = "REMOVED_PRIVATE_CONFIGURATION";
    private const string DatabaseUrl = "https://REMOVED_PRIVATE_CONFIGURATION-default-rtdb.firebaseio.com";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly string TokenPath = Path.Combine(App.AppData, "presence.auth");
    private static readonly CancellationTokenSource Lifetime = new();
    private static Task? _runner;
    private static string? _uid, _idToken, _refreshToken;
    private static DateTimeOffset _idTokenExpiresAt;

    public static void Start() => _runner ??= RunAsync(Lifetime.Token);

    public static async Task StopAsync()
    {
        Lifetime.Cancel();
        if (_runner != null) try { await _runner; } catch { }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        string? sessionId = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try { if (await AuthenticateAsync(cancellationToken)) break; }
                catch (Exception ex) { App.Log("Presence connection: " + ex.GetType().Name); }
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            if (_uid == null || cancellationToken.IsCancellationRequested) return;
            sessionId = Guid.NewGuid().ToString("N");
            await RemoveStaleSessionsAsync(cancellationToken);
            await WriteSessionAsync(sessionId, cancellationToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try { await WriteSessionAsync(sessionId, cancellationToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { App.Log("Presence heartbeat: " + ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { App.Log("Presence stopped: " + ex.GetType().Name); }
        finally
        {
            if (_uid != null && sessionId != null)
            {
                try { await SendPresenceRequestAsync(HttpMethod.Delete, sessionId, null, CancellationToken.None); }
                catch { }
            }
        }
    }

    private static async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var refreshToken = ReadRefreshToken();
        if (refreshToken != null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://securetoken.googleapis.com/v1/token?key={ApiKey}")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                })
            };
            using var response = await Http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                ApplyAuth(await response.Content.ReadAsStringAsync(cancellationToken), "user_id", "id_token", "refresh_token", "expires_in");
                return _uid != null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode != HttpStatusCode.BadRequest ||
                !(body.Contains("INVALID_REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase) ||
                  body.Contains("TOKEN_EXPIRED", StringComparison.OrdinalIgnoreCase))) return false;
            try { File.Delete(TokenPath); } catch { }
        }

        using var signUp = new HttpRequestMessage(HttpMethod.Post,
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}")
        {
            Content = new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json")
        };
        using var created = await Http.SendAsync(signUp, cancellationToken);
        if (!created.IsSuccessStatusCode)
        {
            App.Log("Presence sign-in HTTP " + (int)created.StatusCode);
            return false;
        }
        ApplyAuth(await created.Content.ReadAsStringAsync(cancellationToken), "localId", "idToken", "refreshToken", "expiresIn");
        return _uid != null;
    }

    private static void ApplyAuth(string json, string userKey, string idKey, string refreshKey, string expiryKey)
    {
        var auth = JObject.Parse(json);
        _uid = (string?)auth[userKey];
        _idToken = (string?)auth[idKey];
        _refreshToken = (string?)auth[refreshKey];
        var expires = int.TryParse((string?)auth[expiryKey], out var seconds) ? seconds : 3600;
        _idTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expires));
        if (string.IsNullOrWhiteSpace(_uid) || string.IsNullOrWhiteSpace(_idToken) || string.IsNullOrWhiteSpace(_refreshToken))
            throw new InvalidDataException("Firebase presence sign-in returned incomplete credentials.");
        SaveRefreshToken(_refreshToken);
    }

    private static async Task WriteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_idTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5) && !await AuthenticateAsync(cancellationToken))
            throw new HttpRequestException("Firebase presence authentication failed.");
        var body = "{\"lastSeen\":{\".sv\":\"timestamp\"}}";
        using var response = await SendPresenceRequestAsync(HttpMethod.Put, sessionId, body, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task RemoveStaleSessionsAsync(CancellationToken cancellationToken)
    {
        var url = PresenceUrl(null);
        using var response = await Http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return;
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return;
        var sessions = JObject.Parse(json);
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 120_000;
        foreach (var session in sessions.Properties())
        {
            if ((long?)session.Value["lastSeen"] is long timestamp && timestamp < cutoff)
            {
                using var deleted = await SendPresenceRequestAsync(HttpMethod.Delete, session.Name, null, cancellationToken);
                if (!deleted.IsSuccessStatusCode) App.Log("Stale presence cleanup HTTP " + (int)deleted.StatusCode);
            }
        }
    }

    private static async Task<HttpResponseMessage> SendPresenceRequestAsync(HttpMethod method, string? sessionId, string? body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_uid) || string.IsNullOrWhiteSpace(_idToken))
            throw new InvalidOperationException("Presence is not authenticated.");
        var url = PresenceUrl(sessionId);
        using var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await Http.SendAsync(request, cancellationToken);
    }

    private static string PresenceUrl(string? sessionId)
    {
        var path = $"activeSessions/{Uri.EscapeDataString(_uid!)}" +
                   (sessionId == null ? "" : $"/{Uri.EscapeDataString(sessionId)}") + ".json";
        return $"{DatabaseUrl}/{path}?auth={Uri.EscapeDataString(_idToken!)}";
    }

    private static string? ReadRefreshToken()
    {
        try
        {
            if (!File.Exists(TokenPath)) return null;
            var encrypted = File.ReadAllBytes(TokenPath);
            try
            {
                var plain = WindowsSecret.Transform(encrypted, false);
                try { return Encoding.UTF8.GetString(plain); }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            finally { CryptographicOperations.ZeroMemory(encrypted); }
        }
        catch { try { File.Delete(TokenPath); } catch { } return null; }
    }

    private static void SaveRefreshToken(string refreshToken)
    {
        var plain = Encoding.UTF8.GetBytes(refreshToken);
        byte[]? encrypted = null;
        try
        {
            Directory.CreateDirectory(App.AppData);
            encrypted = WindowsSecret.Transform(plain, true);
            var temporary = TokenPath + ".tmp";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, TokenPath, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (encrypted != null) CryptographicOperations.ZeroMemory(encrypted);
        }
    }
}
