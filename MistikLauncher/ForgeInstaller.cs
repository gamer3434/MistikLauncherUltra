using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
namespace MistikLauncher;
public static class ForgeInstaller
{
    static readonly HttpClient Http=new() { Timeout=TimeSpan.FromMinutes(5), MaxResponseContentBufferSize=64*1024*1024 };
    public static async Task<JObject> PromotionsAsync() => JObject.Parse(await Http.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json"));
    public static async Task<string> InstallAsync(string game,Action<double,string> progress)
    {
        if(!GameProfiles.SafeId(game)) throw new InvalidOperationException(Localization.T("forgeUnsupported"));
        var promos=(await PromotionsAsync())["promos"]!;
        var build=(promos[game+"-recommended"] ?? promos[game+"-latest"])?.ToString();
        if(build==null || !GameProfiles.SafeId(build)) throw new InvalidOperationException(Localization.T("forgeUnsupported"));
        var java=await MainWindow.FindJavaAsync() ?? throw new InvalidOperationException(Localization.T("forgeJava"));
        var coordinate=game+"-"+build;
        // Older Forge releases include the Minecraft version twice in Maven coordinates.
        if(game=="1.8.9" || game=="1.7.10") coordinate+="-"+game;
        var url=$"https://maven.minecraftforge.net/net/minecraftforge/forge/{coordinate}/forge-{coordinate}-installer.jar";
        progress(60,Localization.T("forgeDownload")+" "+build);
        var bytes=await Http.GetByteArrayAsync(url);
        if(bytes.Length>64*1024*1024) throw new InvalidDataException(Localization.T("forgeInvalid"));
        var expected=(await Http.GetStringAsync(url+".sha1")).Trim().Split(' ')[0];
        if(!Convert.ToHexString(SHA1.HashData(bytes)).Equals(expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Localization.T("forgeInvalid"));
        var temp=Path.Combine(Path.GetTempPath(),"MistikForge",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var installer=Path.Combine(temp,"installer.jar"); await File.WriteAllBytesAsync(installer,bytes);
            Directory.CreateDirectory(App.GameDir);
            var profiles=Path.Combine(App.GameDir,"launcher_profiles.json");
            if(!File.Exists(profiles)) await File.WriteAllTextAsync(profiles,"{\"profiles\":{},\"selectedProfile\":null}");
            progress(70,Localization.T("forgeInstalling"));
            var start=new ProcessStartInfo(java) { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=App.GameDir,RedirectStandardOutput=true,RedirectStandardError=true };
            foreach(var arg in new[]{"-jar",installer,"--installClient",App.GameDir}) start.ArgumentList.Add(arg);
            using var process=Process.Start(start) ?? throw new InvalidOperationException(Localization.T("forgeFailed"));
            var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(15));
            try { await process.WaitForExitAsync(timeout.Token); } catch { if(!process.HasExited) process.Kill(true); throw; }
            App.Log(await output); App.Log(await error);
            if(process.ExitCode!=0) throw new InvalidOperationException(Localization.T("forgeFailed"));
            var id=GameProfiles.InstalledIds(App.GameDir).FirstOrDefault(x=> {
                var json=GameProfiles.Read(App.GameDir,x)!;
                return GameProfiles.Kind(json)=="Forge" && json["inheritsFrom"]?.ToString()==game && x.Contains(build,StringComparison.Ordinal);
            });
            if(id==null) throw new InvalidOperationException(Localization.T("forgeFailed"));
            progress(100,Localization.T("forgeComplete")); return id;
        }
        finally { try { Directory.Delete(temp,true); } catch { } }
    }
}
