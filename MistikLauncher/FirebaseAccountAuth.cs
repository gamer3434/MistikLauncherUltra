using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed record FirebaseAccountIdentity(string Uid, string Email);

public sealed class FirebaseAccountAuth
{
    const string ApiKey = "AIzaSyCP7R_Q8Ai_b_zKTCBRk9d1kkZY3iilENg";
    const string DatabaseUrl = "https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com";
    const string AuthUrl = "https://identitytoolkit.googleapis.com/v1/";
    const string TokenUrl = "https://securetoken.googleapis.com/v1/token";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    static readonly string RefreshTokenPath = Path.Combine(App.AppData, "account.refresh.dpapi");

    readonly SemaphoreSlim _gate = new(1, 1);
    AuthSession? _session;
    string? _refreshToken;

    public FirebaseAccountIdentity? CurrentAccount => _session is null
        ? null
        : new FirebaseAccountIdentity(_session.Uid, _session.Email);

    public async Task SignInAsync(string email, string password) => await AuthenticateAsync("signInWithPassword", email, password);

    public async Task RegisterAsync(string email, string password) => await AuthenticateAsync("signUp", email, password);

    async Task AuthenticateAsync(string method, string email, string password)
    {
        await _gate.WaitAsync();
        try
        {
            var body = new JObject { ["email"] = email.Trim(), ["password"] = password, ["returnSecureToken"] = true };
            JObject response = await PostJsonAsync(AuthUrl + "accounts:" + method + "?key=" + ApiKey, body);
            var session = new AuthSession(
                Required(response, "localId"),
                response.Value<string>("email") ?? email.Trim(),
                Required(response, "idToken"),
                Required(response, "refreshToken"),
                Expiry(response.Value<string>("expiresIn")));
            if (method == "signUp")
            {
                await SendEmailVerificationAsync(session.IdToken, session.Email);
                throw new FirebaseAccountAuthException("EMAIL_NOT_VERIFIED");
            }
            if (!await IsEmailVerifiedAsync(session.IdToken))
            {
                throw new FirebaseAccountAuthException("EMAIL_NOT_VERIFIED");
            }
            SetSession(session);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RestoreSessionAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_session is not null && _session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return true;
            string? token = _refreshToken ?? ReadRefreshToken();
            if (string.IsNullOrWhiteSpace(token)) return false;
            try
            {
                var restored = await RefreshAsync(token, "");
                if (!await IsEmailVerifiedAsync(restored.IdToken))
                {
                    ClearLocalSession();
                    throw new FirebaseAccountAuthException("EMAIL_NOT_VERIFIED");
                }
                _session = restored;
                _refreshToken = _session.RefreshToken;
                PersistRefreshToken(_refreshToken);
                return true;
            }
            catch (FirebaseAccountAuthException ex) when (IsInvalidRefreshToken(ex.Code))
            {
                ClearLocalSession();
                return false;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task SignOutAsync()
    {
        await _gate.WaitAsync();
        try { ClearLocalSession(); }
        finally { _gate.Release(); }
    }

    public async Task BackupLauncherPreferencesAsync(LauncherConfig config)
    {
        string idToken = await GetIdTokenAsync();
        var account = CurrentAccount ?? throw new FirebaseAccountAuthException("AUTH_REQUIRED");
        string path = Uri.EscapeDataString(account.Uid);
        var profile = new JObject
        {
            ["schema"] = 1,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["user"] = SafePlayerName(config.User),
            ["version"] = SafeVersion(config.Version),
            ["ram"] = Math.Clamp(config.Ram, 1, 32),
            ["lang"] = config.Lang == "English" ? "English" : "Turkce",
            ["accent"] = ColorThemes.Names.Contains(config.Accent) ? config.Accent : "Amber"
        };
        var url = $"{DatabaseUrl}/launcherProfiles/{path}.json?auth={Uri.EscapeDataString(idToken)}";
        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(profile.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };
        using var response = await SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new FirebaseAccountAuthException(await ReadErrorCodeAsync(response));
    }

    async Task<string> GetIdTokenAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_session is null) throw new FirebaseAccountAuthException("AUTH_REQUIRED");
            if (_session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return _session.IdToken;
            _session = await RefreshAsync(_session.RefreshToken, _session.Email);
            _refreshToken = _session.RefreshToken;
            PersistRefreshToken(_refreshToken);
            return _session.IdToken;
        }
        finally { _gate.Release(); }
    }

    void SetSession(AuthSession session)
    {
        PersistRefreshToken(session.RefreshToken);
        _refreshToken = session.RefreshToken;
        _session = session;
    }

    static async Task<AuthSession> RefreshAsync(string refreshToken, string previousEmail)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl + "?key=" + ApiKey) { Content = content };
        using var response = await SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new FirebaseAccountAuthException(await ReadErrorCodeAsync(response));
        JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
        string idToken = Required(json, "access_token");
        string uid = Required(json, "user_id");
        string email = previousEmail;
        if (string.IsNullOrWhiteSpace(email)) email = await LookupEmailAsync(idToken) ?? uid;
        return new AuthSession(uid, email, idToken, Required(json, "refresh_token"), Expiry(json.Value<string>("expires_in")));
    }

    static async Task<string?> LookupEmailAsync(string idToken)
    {
        try
        {
            JObject response = await PostJsonAsync(AuthUrl + "accounts:lookup?key=" + ApiKey, new JObject { ["idToken"] = idToken });
            return (response["users"] as JArray)?.FirstOrDefault()?.Value<string>("email");
        }
        catch (FirebaseAccountAuthException) { return null; }
    }

    static async Task<bool> IsEmailVerifiedAsync(string idToken)
    {
        JObject response = await PostJsonAsync(AuthUrl + "accounts:lookup?key=" + ApiKey, new JObject { ["idToken"] = idToken });
        return (response["users"] as JArray)?.FirstOrDefault()?.Value<bool?>("emailVerified") == true;
    }

    static async Task SendEmailVerificationAsync(string idToken, string email)
    {
        await PostJsonAsync(AuthUrl + "accounts:sendOobCode?key=" + ApiKey,
            new JObject { ["requestType"] = "VERIFY_EMAIL", ["idToken"] = idToken, ["email"] = email });
    }

    static async Task<JObject> PostJsonAsync(string url, JObject body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };
        using var response = await SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new FirebaseAccountAuthException(await ReadErrorCodeAsync(response));
        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

    static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        try { return await Http.SendAsync(request); }
        catch (HttpRequestException) { throw new FirebaseAccountAuthException("NETWORK"); }
        catch (TaskCanceledException) { throw new FirebaseAccountAuthException("NETWORK"); }
    }

    static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        try
        {
            var root = JObject.Parse(await response.Content.ReadAsStringAsync());
            string? message = root["error"] switch
            {
                JObject error => error.Value<string>("message"),
                JValue error => error.ToString(),
                _ => null
            };
            if (message?.Contains("permission", StringComparison.OrdinalIgnoreCase) == true) return "PERMISSION_DENIED";
            return string.IsNullOrWhiteSpace(message) ? "REQUEST_FAILED" : message.Split(':')[0];
        }
        catch { return response.StatusCode == HttpStatusCode.Unauthorized ? "INVALID_LOGIN_CREDENTIALS" : "REQUEST_FAILED"; }
    }

    static string Required(JObject json, string key) =>
        !string.IsNullOrWhiteSpace(json.Value<string>(key)) ? json.Value<string>(key)! : throw new FirebaseAccountAuthException("INVALID_RESPONSE");

    static DateTimeOffset Expiry(string? seconds) =>
        DateTimeOffset.UtcNow.AddSeconds(int.TryParse(seconds, out int value) ? Math.Max(value, 60) : 3600);

    static bool IsInvalidRefreshToken(string code) => code is "INVALID_REFRESH_TOKEN" or "INVALID_GRANT" or "TOKEN_EXPIRED" or "USER_DISABLED";

    static string SafePlayerName(string? value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^[A-Za-z0-9_]{3,16}$") ? value! : "Player";

    static string SafeVersion(string? value) => GameProfiles.SafeId(value ?? "") && value!.Length <= 120 ? value : "1.21";

    string? ReadRefreshToken()
    {
        if (!File.Exists(RefreshTokenPath)) return null;
        byte[] encrypted = File.ReadAllBytes(RefreshTokenPath);
        try
        {
            byte[] plain = WindowsSecret.Transform(encrypted, false);
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch
        {
            try { File.Delete(RefreshTokenPath); } catch { }
            return null;
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }

    static void PersistRefreshToken(string refreshToken)
    {
        byte[] plain = Encoding.UTF8.GetBytes(refreshToken);
        try
        {
            byte[] encrypted = WindowsSecret.Transform(plain, true);
            try
            {
                Directory.CreateDirectory(App.AppData);
                string temporary = RefreshTokenPath + ".tmp";
                File.WriteAllBytes(temporary, encrypted);
                File.Move(temporary, RefreshTokenPath, true);
            }
            finally { CryptographicOperations.ZeroMemory(encrypted); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    void ClearLocalSession()
    {
        _session = null;
        _refreshToken = null;
        try { File.Delete(RefreshTokenPath); }
        catch { throw new FirebaseAccountAuthException("LOCAL_STORAGE"); }
    }

    sealed record AuthSession(string Uid, string Email, string IdToken, string RefreshToken, DateTimeOffset ExpiresAt);
}

public sealed class FirebaseAccountAuthException(string code) : Exception("Firebase account request failed.")
{
    public string Code { get; } = code;
}
