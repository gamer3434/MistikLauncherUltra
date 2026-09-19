using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MistikLauncher.Updates;
namespace MistikLauncher;
public sealed record LauncherRelease(string Version,string Url,string Digest,long Size);
public sealed class LauncherUpdater
{
    public const string Endpoint="https://api.github.com/repos/gamer3434/MistikLauncherUltra/releases/latest";
    readonly HttpClient http;
    readonly SemaphoreSlim gate=new(1,1);
    readonly Func<bool> idle;
    readonly string directory;
    public string CurrentVersion { get; }
    public string LatestVersion { get; private set; }="—";
    public string StatusKey { get; private set; }="luIdle";
    public string? Error { get; private set; }
    public string? PreparedPayload { get; private set; }
    public bool Busy { get; private set; }
    public double Progress { get; private set; }
    public event Action? Changed;
    public LauncherUpdater(Func<bool> canUpdate,HttpClient? client=null,string? target=null,string? currentVersion=null)
    {
        idle=canUpdate; http=client??new HttpClient { Timeout=TimeSpan.FromMinutes(10) };
        if(!http.DefaultRequestHeaders.UserAgent.Any()) http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/6.0");
        directory=target??AppContext.BaseDirectory;
        CurrentVersion=currentVersion??(typeof(LauncherUpdater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"6.0.0-preview.1");
    }
    void Publish(string key,double progress=0) { StatusKey=key; Progress=progress; Changed?.Invoke(); }
    public static bool IsNewer(string offered,string installed)
    {
        var a=Regex.Match(offered,@"^v?(\d+\.\d+\.\d+)(?:-preview\.(\d+))?$");
        var b=Regex.Match(installed,@"^v?(\d+\.\d+\.\d+)(?:-preview\.(\d+))?$");
        if(!a.Success||!b.Success) return false;
        var latest=Version.Parse(a.Groups[1].Value); var current=Version.Parse(b.Groups[1].Value);
        if(latest!=current) return latest>current;
        bool offeredPreview=a.Groups[2].Success, installedPreview=b.Groups[2].Success;
        if(offeredPreview!=installedPreview) return !offeredPreview;
        return offeredPreview && System.Numerics.BigInteger.Parse(a.Groups[2].Value)>System.Numerics.BigInteger.Parse(b.Groups[2].Value);
    }
    public static LauncherRelease? ParseRelease(string json,string installed)
    {
        using var document=JsonDocument.Parse(json); var root=document.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean()) return null;
        string version=root.GetProperty("tag_name").GetString()!;
        if(!IsNewer(version,installed)) return null;
        var assets=root.GetProperty("assets").EnumerateArray().Where(item=> {
            string name=item.GetProperty("name").GetString()??"";
            return name.StartsWith("MistikLauncher-",StringComparison.Ordinal) && name.EndsWith("-win-x64.zip",StringComparison.Ordinal);
        }).ToArray();
        if(assets.Length!=1) throw new InvalidDataException("Release must contain one portable Windows ZIP.");
        var asset=assets[0]; string url=asset.GetProperty("browser_download_url").GetString()!;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/gamer3434/MistikLauncherUltra/releases/download/",StringComparison.Ordinal)) throw new InvalidDataException("Untrusted update URL.");
        string digest=asset.TryGetProperty("digest",out var value)?value.GetString()??"":"";
        if(!Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$")) throw new InvalidDataException("Official package digest missing.");
        long size=asset.GetProperty("size").GetInt64(); if(size<=0||size>512L*1024*1024) throw new InvalidDataException("Invalid update size.");
        return new(version,url,digest[7..],size);
    }
    public async Task<bool> CheckAsync(bool prepare)
    {
        if(!await gate.WaitAsync(0)) return false;
        try
        {
            Busy=true; Error=null; Publish("luChecking");
            if(PreparedPayload!=null) { Publish("luReady",100); return true; }
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            string json=await GitHubReleaseCache.GetAsync(http,Endpoint,Path.Combine(App.AppData,"launcher-release-cache.json"),timeout.Token);
            using(var doc=JsonDocument.Parse(json)) LatestVersion=doc.RootElement.GetProperty("tag_name").GetString()??"—";
            var release=ParseRelease(json,CurrentVersion);
            if(release==null) { Publish("luCurrent"); return false; }
            if(!prepare) { Publish("luAvailable"); return false; }
            if(!idle()) { Publish("luDeferred"); return false; }
            string stage=Path.Combine(Path.GetTempPath(),"MistikLauncherUpdates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            string zip=Path.Combine(stage,"package.zip"); Publish("luDownloading");
            using(var response=await http.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode(); using var input=await response.Content.ReadAsStreamAsync();
                using var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
                var buffer=new byte[81920]; long total=0; int read;
                while((read=await input.ReadAsync(buffer))>0) { total+=read; if(total>release.Size) throw new InvalidDataException("Oversized update."); await output.WriteAsync(buffer.AsMemory(0,read)); Publish("luDownloading",85d*total/release.Size); }
                if(total!=release.Size) throw new InvalidDataException("Incomplete update download.");
            }
            Publish("luVerifying",90);
            using(var stream=File.OpenRead(zip))
                if(!Convert.ToHexString(await SHA256.HashDataAsync(stream)).Equals(release.Digest,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update SHA-256 mismatch.");
            string payload=Path.Combine(stage,"payload"); Directory.CreateDirectory(payload);
            var manifest=await Task.Run(()=> { UpdateEngine.Extract(zip,payload); return UpdateEngine.Verify(payload); });
            if(manifest.Version!=release.Version.TrimStart('v')) throw new InvalidDataException("Package version does not match release.");
            PreparedPayload=payload; Publish(idle()?"luReady":"luDeferred",100); return idle();
        }
        catch(Exception ex) { Error=ex.Message; Publish("luError"); App.Log("Launcher update: "+ex.Message); return false; }
        finally { Busy=false; Changed?.Invoke(); gate.Release(); }
    }
    public async Task<bool> StartInstallerAsync()
    {
        if(PreparedPayload==null||!idle()) { Publish("luDeferred"); return false; }
        UpdateEngine.Verify(PreparedPayload);
        string helper=UpdateEngine.SafePath(directory,"MistikUpdater.exe");
        if(!File.Exists(helper)) { Publish("luManual"); return false; }
        string stage=Path.GetDirectoryName(PreparedPayload)!;
        string externalHelper=Path.Combine(stage,"MistikUpdater.exe"); File.Copy(helper,externalHelper,true);
        using var current=Process.GetCurrentProcess();
        string plan=Path.Combine(stage,"plan.json");
        File.WriteAllText(plan,JsonSerializer.Serialize(new UpdatePlan(directory,PreparedPayload,current.Id,current.StartTime.ToUniversalTime().Ticks,Localization.Language)));
        var start=new ProcessStartInfo(externalHelper) { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden };
        start.ArgumentList.Add(plan); using var child=Process.Start(start)??throw new IOException("Cannot start update helper.");
        for(int count=0;count<100;count++)
        {
            if(File.Exists(plan+".ready")) { Publish("luRestarting",100); return true; }
            if(child.HasExited) break;
            await Task.Delay(100);
        }
        Publish("luError"); return false;
    }
}
