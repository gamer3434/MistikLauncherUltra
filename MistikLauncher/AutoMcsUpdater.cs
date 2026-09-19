using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace MistikLauncher;

public sealed record AutoMcsRelease(string Version, string Url, string Digest, long Size);
public sealed record AutoMcsReceipt(string Version, string ExecutableHash);

public sealed class AutoMcsUpdater
{
    public const string Repository = "https://github.com/macarooni-man/auto-mcs";
    public const string ReleaseEndpoint = "https://api.github.com/repos/macarooni-man/auto-mcs/releases/latest";
    readonly HttpClient http;
    readonly string directory;
    readonly Func<bool> isRunning;
    readonly SemaphoreSlim gate = new(1,1);
    AutoMcsRelease? cached;
    DateTime checkedAt;
    public string ExecutablePath => Path.Combine(directory,"auto-mcs.exe");
    string ReceiptPath => Path.Combine(directory,"auto-mcs.version.json");
    public string InstalledVersion { get; private set; } = "—";
    public string LatestVersion { get; private set; } = "—";
    public string StatusKey { get; private set; } = "mcsIdle";
    public string? Error { get; private set; }
    public double Progress { get; private set; }
    public bool Busy { get; private set; }
    public bool IsRunning => isRunning();
    public event Action? Changed;
    public AutoMcsUpdater(HttpClient? client=null, string? dataDirectory=null, Func<bool>? running=null)
    {
        http=client ?? new HttpClient { Timeout=TimeSpan.FromMinutes(10) };
        if (!http.DefaultRequestHeaders.UserAgent.Any()) http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/6.0");
        directory=dataDirectory ?? App.GameDir;
        isRunning=running ?? (() => {
            var processes=Process.GetProcessesByName("auto-mcs");
            try { return processes.Length>0; } finally { foreach(var process in processes) process.Dispose(); }
        });
        try { InstalledVersion=ReadReceipt()?.Version ?? (File.Exists(ExecutablePath)?"?":"—"); } catch { }
    }
    AutoMcsReceipt? ReadReceipt() => File.Exists(ReceiptPath) ? JsonSerializer.Deserialize<AutoMcsReceipt>(File.ReadAllText(ReceiptPath)) : null;
    void Publish(string key, double progress=0)
    { StatusKey=key; Progress=progress; Changed?.Invoke(); }
    public static AutoMcsRelease ParseRelease(string json)
    {
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("Stable release required.");
        var version=root.GetProperty("tag_name").GetString()!;
        var assets=root.GetProperty("assets").EnumerateArray().Where(asset => {
            var name=asset.GetProperty("name").GetString() ?? "";
            return name.StartsWith("auto-mcs-windows-",StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        if (assets.Length!=1) throw new InvalidDataException("Expected one official Windows package.");
        var asset=assets[0];
        string url=asset.GetProperty("browser_download_url").GetString()!;
        if (!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com" ||
            !uri.AbsolutePath.StartsWith("/macarooni-man/auto-mcs/releases/download/",StringComparison.Ordinal))
            throw new InvalidDataException("Untrusted release URL.");
        string digest=asset.TryGetProperty("digest",out var value)?value.GetString() ?? "":"";
        if (!System.Text.RegularExpressions.Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("Official SHA-256 digest is missing.");
        long size=asset.GetProperty("size").GetInt64();
        if(size<=0 || size>512L*1024*1024) throw new InvalidDataException("Invalid package size.");
        return new(version,url,digest[7..],size);
    }
    static async Task<string> HashFile(string path)
    { using var stream=File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream)); }
    public async Task<bool> CheckAsync(bool install, bool force=false)
    {
        await gate.WaitAsync();
        string? staging=null;
        try
        {
            Busy=true; Error=null; Publish("mcsChecking");
            if(cached==null || force || DateTime.UtcNow-checkedAt>TimeSpan.FromMinutes(10))
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var metadata=await GitHubReleaseCache.GetAsync(http,ReleaseEndpoint,Path.Combine(directory,"auto-mcs-release-cache.json"),timeout.Token);
                cached=ParseRelease(metadata); checkedAt=DateTime.UtcNow;
            }
            LatestVersion=cached.Version;
            AutoMcsReceipt? receipt=null;
            try { receipt=ReadReceipt(); } catch { }
            if (receipt?.Version==cached.Version && File.Exists(ExecutablePath) &&
                string.Equals(receipt.ExecutableHash,await HashFile(ExecutablePath),StringComparison.OrdinalIgnoreCase))
            { InstalledVersion=receipt.Version; Publish("mcsCurrent",100); return true; }
            if(!install) { Publish(File.Exists(ExecutablePath)?"mcsAvailable":"mcsNotInstalled"); return false; }
            if(isRunning()) { Publish("mcsRunning"); return false; }
            Directory.CreateDirectory(directory);
            staging=Path.Combine(directory,".auto-mcs-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
            string zip=Path.Combine(staging,"package.zip");
            Publish("mcsDownloading");
            using(var response=await http.GetAsync(cached.Url,HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var input=await response.Content.ReadAsStreamAsync();
                using var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
                var buffer=new byte[81920]; long total=0; int read;
                while((read=await input.ReadAsync(buffer))>0)
                {
                    total+=read; if(total>cached.Size) throw new InvalidDataException("Package exceeds declared size.");
                    await output.WriteAsync(buffer.AsMemory(0,read)); Publish("mcsDownloading",total*85d/cached.Size);
                }
                if(total!=cached.Size) throw new InvalidDataException("Incomplete download.");
            }
            Publish("mcsVerifying",90);
            if(!string.Equals(await HashFile(zip),cached.Digest,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 mismatch.");
            string prepared=Path.Combine(staging,"auto-mcs.exe");
            using(var archive=ZipFile.OpenRead(zip))
            {
                var executables=archive.Entries.Where(entry=>entry.FullName=="auto-mcs.exe").ToArray();
                if(executables.Length!=1 || archive.Entries.Count!=1 || executables[0].Length<4096 || executables[0].Length>512L*1024*1024)
                    throw new InvalidDataException("Unexpected Windows package structure.");
                executables[0].ExtractToFile(prepared);
            }
            using(var executable=File.OpenRead(prepared))
                if(executable.ReadByte()!=0x4d || executable.ReadByte()!=0x5a) throw new InvalidDataException("Invalid executable.");
            string executableHash=await HashFile(prepared);
            if(isRunning()) { Publish("mcsRunning"); return false; }
            // Replacement is atomic; download, digest and extraction failures preserve the installed EXE.
            if(File.Exists(ExecutablePath)) File.Replace(prepared,ExecutablePath,ExecutablePath+".bak");
            else File.Move(prepared,ExecutablePath);
            string receiptTemp=Path.Combine(staging,"receipt.json");
            File.WriteAllText(receiptTemp,JsonSerializer.Serialize(new AutoMcsReceipt(cached.Version,executableHash)));
            File.Move(receiptTemp,ReceiptPath,true);
            InstalledVersion=cached.Version; Publish("mcsUpdated",100); return true;
        }
        catch(Exception ex)
        { Error=ex.Message; Publish("mcsError"); App.Log("Auto-MCS update: "+ex.Message); return false; }
        finally
        {
            if(staging!=null) { try { foreach(var file in Directory.EnumerateFiles(staging)) File.Delete(file); Directory.Delete(staging); } catch { } }
            Busy=false; Changed?.Invoke(); gate.Release();
        }
    }
}
