using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Security.Cryptography;
using MistikLauncher.Updates;
using Microsoft.Win32;
namespace MistikLauncher.Installation;

public sealed record InstallState(string Product,string Root,string Version,string[] Files);
public static class InstallEngine
{
    public const string Marker="install-state.json";
    public const string RegistryKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikLauncherUltra";
    public static string DefaultRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","MistikLauncherUltra");
    public static async Task<string> DownloadPayloadAsync(string work,string version,HttpClient http,IProgress<double>? progress=null,CancellationToken cancellation=default)
    {
        var zip=UpdateEngine.SafePath(work,"payload.zip");
        var partial=UpdateEngine.SafePath(work,"payload-"+Guid.NewGuid().ToString("N")+".part");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(http.Timeout==Timeout.InfiniteTimeSpan?TimeSpan.FromMinutes(15):http.Timeout);
        var token=deadline.Token;
        try {
            using var metadata=await http.GetAsync("https://api.github.com/repos/gamer3434/MistikLauncherUltra/releases/tags/v"+Uri.EscapeDataString(version),HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            metadata.EnsureSuccessStatusCode();
            if(metadata.Content.Headers.ContentLength>512*1024) throw new IOException("Release metadata too large / Sürüm bilgisi çok büyük.");
            using var stream=await metadata.Content.ReadAsStreamAsync(token).ConfigureAwait(false); using var buffer=new MemoryStream();
            var data=new byte[65536]; int read;
            while((read=await stream.ReadAsync(data,token).ConfigureAwait(false))>0) { if(buffer.Length+read>512*1024) throw new IOException("Release metadata too large"); await buffer.WriteAsync(data.AsMemory(0,read),token).ConfigureAwait(false); }
            using var json=JsonDocument.Parse(buffer.ToArray());
            var asset=json.RootElement.GetProperty("assets").EnumerateArray().Single(a=>a.GetProperty("name").GetString()=="MistikLauncher-"+version+"-win-x64.zip");
            var uri=new Uri(asset.GetProperty("browser_download_url").GetString()!); var size=asset.GetProperty("size").GetInt64(); var digest=asset.GetProperty("digest").GetString();
            if(uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/gamer3434/MistikLauncherUltra/releases/download/",StringComparison.Ordinal) || size<=0 || size>512L*1024*1024 || digest is null || !System.Text.RegularExpressions.Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$")) throw new IOException("Unverified release / Doğrulanamayan sürüm.");
            using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
            var final=response.RequestMessage!.RequestUri!; if(final.Scheme!="https" || !(final.Host=="github.com" || final.Host=="release-assets.githubusercontent.com" || final.Host=="objects.githubusercontent.com")) throw new IOException("Invalid download host");
            using var download=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false); using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long total=0;
            using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)) {
                while((read=await download.ReadAsync(data,token).ConfigureAwait(false))>0) { total+=read; if(total>size) throw new IOException("Download exceeds expected size"); hash.AppendData(data,0,read); await file.WriteAsync(data.AsMemory(0,read),token).ConfigureAwait(false); progress?.Report(total*100.0/size); }
                if(total!=size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(digest[7..],StringComparison.OrdinalIgnoreCase)) throw new IOException("Download checksum mismatch / İndirme doğrulaması başarısız.");
            }
            token.ThrowIfCancellationRequested(); File.Move(partial,zip,true); return zip;
        } catch(OperationCanceledException ex) when(!cancellation.IsCancellationRequested) { throw new IOException("Download timed out / İndirme zaman aşımına uğradı.",ex); }
        finally { if(File.Exists(partial)) File.Delete(partial); }
    }
    public static string ValidateRoot(string root)
    {
        root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if(Path.GetFileName(root)!="MistikLauncherUltra") throw new IOException("Choose a folder named MistikLauncherUltra / MistikLauncherUltra adlı klasörü seçin.");
        UpdateEngine.SafePath(root,"MistikLauncher.exe");
        return root;
    }
    public static void Install(string payload,string root,bool shortcuts,CancellationToken cancellation=default,bool shell=true,Action? finalize=null)
    {
        root=ValidateRoot(root); var manifest=UpdateEngine.Verify(payload);
        if(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("Folder is not empty. Use launcher update or uninstall first / Klasör boş değil. Launcher güncellemesini kullanın veya önce kaldırın.");
        if(shell) { using var old=Registry.CurrentUser.OpenSubKey(RegistryKey); if(old?.GetValue("InstallLocation") is string location && !Path.GetFullPath(location).Equals(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("Another installation exists / Başka bir kurulum var."); }
        var stage=Path.Combine(Path.GetDirectoryName(root)!,".mistik-install-"+Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(stage);
            foreach(var file in manifest.Files) { cancellation.ThrowIfCancellationRequested(); var dest=UpdateEngine.SafePath(stage,file.Path); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(UpdateEngine.SafePath(payload,file.Path),dest); }
            File.Copy(Path.Combine(payload,"update-manifest.json"),Path.Combine(stage,"update-manifest.json"));
            File.WriteAllText(Path.Combine(stage,Marker),JsonSerializer.Serialize(new InstallState("MistikLauncher",root,manifest.Version,manifest.Files.Select(f=>f.Path).Append("update-manifest.json").ToArray())));
            cancellation.ThrowIfCancellationRequested();
            if(Directory.Exists(root)) Directory.Delete(root,false);
            Directory.Move(stage,root);
            try { finalize?.Invoke(); if(shell) {
                using var key=Registry.CurrentUser.CreateSubKey(RegistryKey);
                key.SetValue("DisplayName","Mistik Launcher Ultra"); key.SetValue("DisplayVersion",manifest.Version); key.SetValue("Publisher","Mustafa Developer");
                key.SetValue("InstallLocation",root); key.SetValue("UninstallString","\""+Path.Combine(root,"MistikUninstall.exe")+"\""); key.SetValue("NoModify",1); key.SetValue("NoRepair",1);
                Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.Programs),false);
                if(shortcuts) Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),false);
            } } catch {
                // Undo only this installation's shell entries and move our committed files
                // back to the private staging directory before its normal cleanup.
                if(shell) {
                    try { Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.Programs),true); Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),true); } catch { }
                    try { using var key=Registry.CurrentUser.OpenSubKey(RegistryKey); var location=key?.GetValue("InstallLocation") as string; key?.Dispose(); if(location!=null && Path.GetFullPath(location).Equals(root,StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKeyTree(RegistryKey,false); } catch { }
                }
                Directory.Move(root,stage);
                throw;
            }
        } finally { CleanupStage(stage,Path.GetDirectoryName(root)!); }
    }
    public static InstallState ReadState(string root)
    {
        root=ValidateRoot(root);
        var state=JsonSerializer.Deserialize<InstallState>(File.ReadAllText(UpdateEngine.SafePath(root,Marker)))??throw new IOException("Missing installation record / Kurulum kaydı bulunamadı.");
        if(state.Product!="MistikLauncher" || string.IsNullOrWhiteSpace(state.Root) || !Path.GetFullPath(state.Root).Equals(root,StringComparison.OrdinalIgnoreCase) || state.Files is null || state.Files.Length>3000) throw new IOException("Invalid installation record / Geçersiz kurulum kaydı.");
        foreach(var file in state.Files) { UpdateEngine.SafePath(root,file); if(file.Equals(Marker,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid installation record"); }
        return state;
    }
    public static void Repair(string payload,string root,bool shortcuts,CancellationToken cancellation=default,bool shell=true)
    {
        root=ValidateRoot(root);
        if(!Directory.Exists(root)) throw new IOException("Installation folder not found / Kurulum klasörü bulunamadı.");
        var manifest=UpdateEngine.Verify(payload);
        static Version Number(string? value)=>Version.TryParse((value??"").TrimStart('v').Split('-')[0],out var version)?version:new Version(0,0,0);
        string? installedVersion=null;
        bool recognized=false;
        try { var state=ReadState(root); installedVersion=state.Version; recognized=true; }
        catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or ArgumentException) { }
        if(shell) {
            using var key=Registry.CurrentUser.OpenSubKey(RegistryKey);
            if(key?.GetValue("InstallLocation") is string location && Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar).Equals(root,StringComparison.OrdinalIgnoreCase)) {
                recognized=true;
                var registeredVersion=key.GetValue("DisplayVersion") as string;
                if(Number(registeredVersion)>Number(installedVersion)) installedVersion=registeredVersion;
            }
        }
        if(!recognized) throw new IOException("Installation record not found. Reinstall in a new folder / Kurulum kaydı bulunamadı. Yeni bir klasöre kurun.");
        if(Number(installedVersion)>Number(manifest.Version)) throw new IOException("Download the newer repair tool / Daha yeni onarıcıyı indirin.");
        foreach(var relative in manifest.Files.Select(file=>file.Path).Concat(new[]{"update-manifest.json",Marker})) {
            var path=UpdateEngine.SafePath(root,relative);
            if(File.Exists(path)) using(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None)) { }
        }
        cancellation.ThrowIfCancellationRequested();
        UpdateEngine.Apply(payload,root,manifest.Version,repair:true);
        UpdateEngine.VerifyInstalled(root,manifest);
        if(shell) {
            Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.Programs),false);
            if(shortcuts) Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),false);
        }
    }
    public static void Uninstall(string root,bool shell=true)
    {
        root=ValidateRoot(root); var state=ReadState(root);
        var files=state.Files.Select(f=>UpdateEngine.SafePath(root,f)).Append(Path.Combine(root,Marker)).ToArray();
        // Preflight locks before removing anything; never kill the running launcher.
        foreach(var file in files.Where(File.Exists)) using(File.Open(file,FileMode.Open,FileAccess.Read,FileShare.None)) { }
        foreach(var file in files) if(File.Exists(file)) File.Delete(file);
        foreach(var dir in state.Files.Select(f=>Path.GetDirectoryName(UpdateEngine.SafePath(root,f))!).Distinct().OrderByDescending(p=>p.Length)) if(Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir,false);
        if(Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root,false);
        if(shell) {
            Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.Programs),true); Shortcut(root,Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),true);
            using var key=Registry.CurrentUser.OpenSubKey(RegistryKey); var location=key?.GetValue("InstallLocation") as string; key?.Dispose();
            if(location!=null && Path.GetFullPath(location).Equals(root,StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKeyTree(RegistryKey,false);
        }
    }
    public static void CleanupStage(string stage,string parent)
    {
        var full=Path.GetFullPath(stage); var owner=Path.GetFullPath(parent);
        if(Path.GetDirectoryName(full)!=owner || !Path.GetFileName(full).StartsWith(".mistik-install-",StringComparison.Ordinal)) throw new IOException("Invalid temporary path");
        if(Directory.Exists(full)) { UpdateEngine.SafePath(full,"cleanup-check"); Directory.Delete(full,true); }
    }
    static void Shortcut(string root,string folder,bool remove)
    {
        if(string.IsNullOrEmpty(folder)) return;
        var path=Path.Combine(folder,"Mistik Launcher Ultra.lnk"); var type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Windows shortcuts unavailable / Windows kısayolları kullanılamıyor.");
        dynamic shell=Activator.CreateInstance(type)!; dynamic link=shell.CreateShortcut(path);
        try {
            string target=Path.Combine(root,"MistikLauncher.exe");
            if(File.Exists(path) && !((string)link.TargetPath).Equals(target,StringComparison.OrdinalIgnoreCase)) return;
            if(remove) { if(File.Exists(path)) File.Delete(path); }
            else { link.TargetPath=target; link.WorkingDirectory=root; link.Description="Mistik Launcher Ultra"; link.Save(); }
        } finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
    }
}
