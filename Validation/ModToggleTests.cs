using System.IO;
using MistikLauncher;
public static class ModToggleTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var jar=Path.Combine(root,"sample.jar"); File.WriteAllText(jar,"original mod bytes");
        var disabled=ModFiles.Toggle(root,jar);
        Check(!File.Exists(jar) && File.ReadAllText(disabled)=="original mod bytes" && !ModFiles.Enabled(disabled),"disabling preserves mod bytes outside active jar scan");
        Check(ModFiles.List(root).Single()==disabled,"disabled mod remains in installed list");
        Check(ModFiles.Toggle(root,disabled)==jar && File.ReadAllText(jar)=="original mod bytes","enabling restores exact file without redownload");
        File.WriteAllText(disabled,"existing disabled copy"); bool rejected=false;
        try { ModFiles.Toggle(root,jar); } catch(IOException) { rejected=true; }
        Check(rejected && File.ReadAllText(jar)=="original mod bytes" && File.ReadAllText(disabled)=="existing disabled copy","name collision never overwrites either mod");
        using(var locked=new FileStream(jar,FileMode.Open,FileAccess.Read,FileShare.None)) {
            File.Delete(disabled); rejected=false; try { ModFiles.Toggle(root,jar); } catch(IOException) { rejected=true; }
            Check(rejected && File.Exists(jar),"locked running-game mod reports failure and remains intact");
        }
        var outside=Path.Combine(Path.GetDirectoryName(root)!,"outside.jar"); File.WriteAllText(outside,"outside"); rejected=false;
        try { ModFiles.Toggle(root,outside); } catch(IOException) { rejected=true; }
        Check(rejected && File.Exists(outside),"toggle rejects files outside managed mod directory");
        var active=Path.Combine(root,"active"); var previous=Path.Combine(root,"previous"); var next=Path.Combine(root,"next");
        Directory.CreateDirectory(active); Directory.CreateDirectory(previous); Directory.CreateDirectory(next);
        var original=Path.Combine(active,"a.jar"); var blocked=Path.Combine(active,"b.jar");
        File.WriteAllText(original,"first bytes"); File.WriteAllText(blocked,"second bytes");
        File.WriteAllText(Path.Combine(previous,"b.jar"),"existing pool bytes"); rejected=false;
        try { ModFiles.SyncPools(active,previous,next); } catch(IOException) { rejected=true; }
        Check(rejected && File.ReadAllText(original)=="first bytes" && File.ReadAllText(blocked)=="second bytes" && File.ReadAllText(Path.Combine(previous,"b.jar"))=="existing pool bytes" && !File.Exists(Path.Combine(previous,"a.jar")),"pool collision rolls back earlier moves without deleting either copy");
        File.Delete(Path.Combine(previous,"b.jar"));
        using(var locked=new FileStream(blocked,FileMode.Open,FileAccess.Read,FileShare.None)) {
            rejected=false; try { ModFiles.SyncPools(active,previous,next); } catch(IOException) { rejected=true; }
            Check(rejected && File.Exists(original) && File.Exists(blocked) && !File.Exists(Path.Combine(previous,"a.jar")),"locked mod transfer preserves active set and rolls back");
        }
        File.WriteAllText(Path.Combine(next,"new.jar.disabled"),"disabled bytes");
        ModFiles.SyncPools(active,previous,next);
        Check(File.ReadAllText(Path.Combine(active,"new.jar.disabled"))=="disabled bytes" && File.ReadAllText(Path.Combine(previous,"a.jar"))=="first bytes" && File.ReadAllText(Path.Combine(previous,"b.jar"))=="second bytes","successful pool transfer preserves active and disabled mod bytes");
        ModFiles.SyncPools(active,next,null);
        Check(!ModFiles.List(active).Any() && File.ReadAllText(Path.Combine(next,"new.jar.disabled"))=="disabled bytes","Vanilla transition archives mods without deletion");
        byte[] update=System.Text.Encoding.UTF8.GetBytes("verified replacement bytes");
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(update));
        var installRoot=Path.Combine(root,"install"); var installed=ModFiles.Install(installRoot,"verified.jar",update,hash);
        Check(File.ReadAllBytes(installed).SequenceEqual(update),"verified mod installation writes exact bytes");
        rejected=false; try { ModFiles.Install(installRoot,"verified.jar",update,new string('0',128)); } catch(IOException) { rejected=true; }
        Check(rejected && File.ReadAllBytes(installed).SequenceEqual(update),"mod digest mismatch preserves installed file");
        rejected=false; try { ModFiles.Install(installRoot,"../escaped.jar",update,hash); } catch(IOException) { rejected=true; }
        Check(rejected && !File.Exists(Path.Combine(root,"escaped.jar")),"download filename traversal rejected");
        var disabledInstalled=ModFiles.Toggle(installRoot,installed);
        byte[] newer=System.Text.Encoding.UTF8.GetBytes("new verified bytes"); var newerHash=Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(newer));
        Check(ModFiles.Install(installRoot,"verified.jar",newer,newerHash)==disabledInstalled && !File.Exists(installed) && Directory.GetFiles(Path.Combine(installRoot,".backups")).Any(p=>File.ReadAllBytes(p).SequenceEqual(update)),"mod update preserves disabled state and backs up previous bytes");
        using(var locked=new FileStream(disabledInstalled,FileMode.Open,FileAccess.Read,FileShare.None)) {
            rejected=false; try { ModFiles.Install(installRoot,"verified.jar",update,hash); } catch(IOException) { rejected=true; }
            Check(rejected && !Directory.GetFiles(installRoot,"*.tmp").Any(),"locked mod update leaves existing file intact and cleans staging");
        }
        Check(File.ReadAllBytes(disabledInstalled).SequenceEqual(newer),"failed mod update preserves latest disabled bytes");
        rejected=false; try { ModFiles.DownloadUrl("https://cdn.modrinth.com.attacker.invalid/file.jar"); } catch(IOException) { rejected=true; }
        Check(rejected && ModFiles.DownloadUrl("https://cdn.modrinth.com/data/test.jar").Scheme=="https","mod downloads enforce official HTTPS CDN");
        return checks;
    }
    public static async Task<int> Live(string root)
    {
        using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromSeconds(30),MaxResponseContentBufferSize=128*1024*1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncherValidation/6.0 (gamer3434/MistikLauncherUltra)");
        var versions=Newtonsoft.Json.Linq.JArray.Parse(await http.GetStringAsync("https://api.modrinth.com/v2/project/modmenu/version"));
        var file=versions[0]!["files"]![0]!;
        var bytes=await http.GetByteArrayAsync(ModFiles.DownloadUrl(file["url"]!.ToString()));
        string path=ModFiles.Install(root,file["filename"]!.ToString(),bytes,file["hashes"]!["sha512"]!.ToString());
        if(new FileInfo(path).Length!=(long)file["size"]!) throw new IOException("Official mod size mismatch");
        Console.WriteLine("PASS live official Modrinth metadata, CDN download and SHA-512 installation without execution"); return 1;
    }
}
