using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace MistikLauncher;
public sealed class CloudProfiles
{
    public const string Project="mistiklauncher-9eb4b";
    // Firebase client API keys identify the project; authorization is enforced by Auth and database rules.
    const string Key="AIzaSyCP7R_Q8Ai_b_zKTCBRk9d1kkZY3iilENg";
    const string Database="https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com";
    readonly HttpClient http=new() { Timeout=TimeSpan.FromSeconds(20),MaxResponseContentBufferSize=256*1024 };
    readonly SemaphoreSlim gate=new(1,1);
    readonly string directory;
    JObject? session;
    CancellationTokenSource? pending;
    public string? Email=>session?["email"]?.ToString();
    public bool SignedIn=>session!=null;
    public string Status { get; private set; }="cloudOffline";
    public event Action? Changed;
    public CloudProfiles(string? dataDirectory=null)
    {
        directory=dataDirectory??App.AppData;
        try {
            string path=Path.Combine(directory,"cloud-session.dat");
            if(new FileInfo(path).Length>65536) return;
            byte[] plain=WindowsSecret.Transform(File.ReadAllBytes(path),false);
            try { var saved=JObject.Parse(Encoding.UTF8.GetString(plain)); ValidateSession(saved); session=saved; Status="cloudReady"; }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain); }
        } catch { }
    }
    static void ValidateSession(JObject value)
    {
        if(value["expiresAt"]?.Type!=JTokenType.Integer || !System.Text.RegularExpressions.Regex.IsMatch(value["localId"]?.ToString()??"",@"^[A-Za-z0-9_-]{1,128}$") ||
            new[]{"idToken","refreshToken"}.Any(k=>value[k]?.Type!=JTokenType.String || string.IsNullOrWhiteSpace(value[k]!.ToString()))) throw new InvalidDataException(Localization.T("cloudSignInNeeded"));
    }
    void SaveSession(JObject value)
    {
        ValidateSession(value);
        Directory.CreateDirectory(directory); string path=Path.Combine(directory,"cloud-session.dat");
        byte[] plain=Encoding.UTF8.GetBytes(value.ToString(Formatting.None));
        try { File.WriteAllBytes(path+".tmp",WindowsSecret.Transform(plain,true)); File.Move(path+".tmp",path,true); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain); }
    }
    public async Task SignInAsync(string? email,string? password,bool create)
    {
        pending?.Cancel();
        await gate.WaitAsync();
        try {
            var body=new JObject { ["returnSecureToken"]=true };
            if(email!=null) { body["email"]=email; body["password"]=password; }
            using var response=await http.PostAsync($"https://identitytoolkit.googleapis.com/v1/accounts:{(create?"signUp":"signInWithPassword")}?key={Key}",new StringContent(body.ToString(),Encoding.UTF8,"application/json"));
            var result=JObject.Parse(await response.Content.ReadAsStringAsync());
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException(Localization.T("cloudAuthFailed"));
            result["expiresAt"]=DateTimeOffset.UtcNow.AddSeconds(int.Parse(result["expiresIn"]!.ToString())-60).ToUnixTimeSeconds();
            SaveSession(result); session=result; Status="cloudReady"; Changed?.Invoke();
        } finally { gate.Release(); }
    }
    async Task<string> ProfileUrl()
    {
        if(session==null) throw new InvalidOperationException(Localization.T("cloudSignInNeeded"));
        if(session["expiresAt"]!.Value<long>()<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
            using var response=await http.PostAsync($"https://securetoken.googleapis.com/v1/token?key={Key}",new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"]="refresh_token",["refresh_token"]=session["refreshToken"]!.ToString() }));
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException(Localization.T("cloudSignInNeeded"));
            var result=JObject.Parse(await response.Content.ReadAsStringAsync());
            var refreshed=(JObject)session.DeepClone();
            refreshed["idToken"]=result["id_token"]; refreshed["refreshToken"]=result["refresh_token"]; refreshed["expiresAt"]=DateTimeOffset.UtcNow.AddSeconds(int.Parse(result["expires_in"]!.ToString())-60).ToUnixTimeSeconds(); SaveSession(refreshed); session=refreshed;
        }
        return $"{Database}/launcherProfiles/{Uri.EscapeDataString(session["localId"]!.ToString())}.json?auth={Uri.EscapeDataString(session["idToken"]!.ToString())}";
    }
    public static JObject Snapshot(LauncherConfig config)
    {
        ConfigManager.Normalize(config);
        var json=JObject.FromObject(config);
        var result=new JObject { ["schema"]=1,["updatedAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        foreach(string name in Fields) result[name]=json[name];
        // Realtime Database removes empty arrays; a string preserves an intentionally empty toolbar.
        result["quick_links"]=string.Join(",",config.QuickLinks);
        result["skin_user"]=config.SkinType=="username"?config.SkinUser:"";
        result["skin_png"]="";
        if(config.SkinType=="local" && !File.Exists(config.SkinUser)) result["skin_type"]="default";
        if(config.SkinType=="local" && File.Exists(config.SkinUser)) {
            if(new FileInfo(config.SkinUser).Length>65536) throw new InvalidDataException(Localization.T("cloudSkinInvalid"));
            var bytes=File.ReadAllBytes(config.SkinUser); ValidateSkin(bytes); result["skin_png"]=Convert.ToBase64String(bytes);
        }
        return result;
    }
    static readonly string[] Fields={"user","version","ram","lang","accent","window_buttons","close_lighting","close_rgb","quick_links","skin_type","auth_type","opt_turbo","opt_fps","auto_close","auto_mcs_update","launcher_auto_update"};
    public static void ValidateSkin(byte[] bytes)
    {
        if(bytes.Length<33 || bytes.Length>65536 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4))!=64 || !new[]{32,64}.Contains(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)))) throw new InvalidDataException(Localization.T("cloudSkinInvalid"));
        try {
            using var stream=new MemoryStream(bytes);
            var image=System.Windows.Media.Imaging.BitmapDecoder.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            if(image.Frames[0].PixelWidth!=64 || !new[]{32,64}.Contains(image.Frames[0].PixelHeight)) throw new InvalidDataException();
        } catch { throw new InvalidDataException(Localization.T("cloudSkinInvalid")); }
    }
    public LauncherConfig Apply(JObject profile,LauncherConfig local)
    {
        if(profile["schema"]?.Value<int>()!=1) throw new InvalidDataException(Localization.T("cloudFailed"));
        var merged=JObject.FromObject(local);
        foreach(string name in Fields) if(name!="quick_links" && profile[name]!=null) merged[name]=profile[name];
        if(profile["quick_links"]?.Type==JTokenType.String) merged["quick_links"]=new JArray(profile["quick_links"]!.ToString().Split(',',StringSplitOptions.RemoveEmptyEntries));
        else if(profile["quick_links"] is JArray links) merged["quick_links"]=links;
        if(profile["skin_type"]?.ToString()=="username") merged["skin_user"]=profile["skin_user"];
        byte[]? skin=null;
        if(profile["skin_type"]?.ToString()=="local") {
            string png=profile["skin_png"]?.ToString()??"";
            if(png.Length>87384) throw new InvalidDataException(Localization.T("cloudSkinInvalid"));
            skin=Convert.FromBase64String(png); ValidateSkin(skin); merged["skin_user"]=Path.Combine(directory,"cloud-skin.png");
        }
        // Validate the entire profile before replacing any existing skin.
        var restored=ConfigManager.Normalize(merged.ToObject<LauncherConfig>()!);
        if(skin!=null) { Directory.CreateDirectory(directory); string path=restored.SkinUser; File.WriteAllBytes(path+".tmp",skin); File.Move(path+".tmp",path,true); }
        return restored;
    }
    public async Task UploadAsync(LauncherConfig config,CancellationToken cancellation=default)
    {
        var body=Snapshot(config); await gate.WaitAsync(cancellation);
        try { cancellation.ThrowIfCancellationRequested(); using var response=await http.PutAsync(await ProfileUrl(),new StringContent(body.ToString(),Encoding.UTF8,"application/json"),cancellation); if(!response.IsSuccessStatusCode) throw new InvalidOperationException(Localization.T("cloudFailed")); Status="cloudSaved"; Changed?.Invoke(); }
        finally { gate.Release(); }
    }
    public async Task<LauncherConfig?> RestoreAsync(LauncherConfig local)
    {
        pending?.Cancel();
        await gate.WaitAsync();
        try {
            using var response=await http.GetAsync(await ProfileUrl()); if(!response.IsSuccessStatusCode) throw new InvalidOperationException(Localization.T("cloudFailed"));
            string json=await response.Content.ReadAsStringAsync(); if(json=="null") return null;
            var result=Apply(JObject.Parse(json),local); Status="cloudRestored"; Changed?.Invoke(); return result;
        } finally { gate.Release(); }
    }
    public void Schedule(LauncherConfig config)
    {
        if(!SignedIn) return;
        pending?.Cancel(); pending=new CancellationTokenSource(); var cancellation=pending.Token;
        var copy=JsonConvert.DeserializeObject<LauncherConfig>(JsonConvert.SerializeObject(config))!;
        _=Task.Run(async()=> {
            try { await Task.Delay(1500,cancellation); await UploadAsync(copy,cancellation); }
            catch(OperationCanceledException) { } catch { Status="cloudFailed"; Changed?.Invoke(); }
        });
    }
    public async Task SignOutAsync()
    {
        pending?.Cancel(); await gate.WaitAsync();
        try { File.Delete(Path.Combine(directory,"cloud-session.dat")); session=null; Status="cloudOffline"; Changed?.Invoke(); } finally { gate.Release(); }
    }
}
