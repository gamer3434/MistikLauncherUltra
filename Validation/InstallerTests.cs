using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher.Installation;
using MistikLauncher.Updates;
static class InstallerTests
{
    public static void Run(string temp,Action<bool,string> check)
    {
        var payload=Path.Combine(temp,"setup-payload"); Directory.CreateDirectory(payload);
        var names=new[]{"MistikLauncher.exe","MistikLauncher.dll","MistikUpdater.exe","MistikUninstall.exe"};
        foreach(var name in names) File.WriteAllText(Path.Combine(payload,name),"test "+name);
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),JsonSerializer.Serialize(new UpdateManifest("MistikLauncher","6.0.0-test",names.Select(n=>new UpdateFile(n,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(payload,n)))))).ToArray())));
        var root=Path.Combine(temp,"MistikLauncherUltra"); InstallEngine.Install(payload,root,false,shell:false);
        check(File.ReadAllText(Path.Combine(root,names[0]))=="test "+names[0] && InstallEngine.ReadState(root).Version=="6.0.0-test","installer copies verified bytes and writes ownership record");
        var added="added.dll"; File.WriteAllText(Path.Combine(payload,added),"new library");
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),JsonSerializer.Serialize(new UpdateManifest("MistikLauncher","6.0.1",names.Append(added).Select(n=>new UpdateFile(n,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(payload,n)))))).ToArray())));
        UpdateEngine.Apply(payload,root);
        check(InstallEngine.ReadState(root).Version=="6.0.1" && InstallEngine.ReadState(root).Files.Contains(added),"update refreshes installation ownership and version");
        void Reject(Action action,string name) { bool rejected=false; try { action(); } catch { rejected=true; } check(rejected,name); }
        Reject(()=>InstallEngine.Install(payload,root,false,shell:false),"installer refuses existing nonempty target");
        Reject(()=>InstallEngine.ValidateRoot(temp),"installer rejects broad target directory");
        File.WriteAllText(Path.Combine(root,"user-notes.txt"),"keep");
        var marker=Path.Combine(root,InstallEngine.Marker); var valid=File.ReadAllText(marker);
        using(var locked=File.Open(marker,FileMode.Open,FileAccess.Read,FileShare.None)) Reject(()=>UpdateEngine.Apply(payload,root),"locked ownership record prevents partial update");
        check(File.ReadAllText(marker)==valid && File.ReadAllText(Path.Combine(root,names[0]))=="test "+names[0],"failed ownership update preserves installed bytes and record");
        File.WriteAllText(marker,JsonSerializer.Serialize(new InstallState("MistikLauncher",root,"test",new[]{"../escape"})));
        Reject(()=>InstallEngine.Uninstall(root,false),"uninstaller rejects traversal before deleting any files");
        check(File.Exists(Path.Combine(root,names[0])),"malformed uninstall record leaves installation intact"); File.WriteAllText(marker,valid);
        using(var locked=File.Open(Path.Combine(root,names[0]),FileMode.Open,FileAccess.Read,FileShare.None)) Reject(()=>InstallEngine.Uninstall(root,false),"uninstaller rejects running/locked app before deletion");
        InstallEngine.Uninstall(root,false);
        check(!File.Exists(Path.Combine(root,names[0])) && File.ReadAllText(Path.Combine(root,"user-notes.txt"))=="keep","uninstaller removes owned files and preserves unknown user files");
        check(!File.Exists(Path.Combine(root,added)),"uninstall removes files introduced by updates");
        var cancelled=Path.Combine(temp,"cancelled","MistikLauncherUltra"); using var token=new CancellationTokenSource(); token.Cancel();
        var failedCommit=Path.Combine(temp,"failed-commit","MistikLauncherUltra");
        Reject(()=>InstallEngine.Install(payload,failedCommit,false,shell:false,finalize:()=>throw new IOException("Simulated shell integration failure")),"post-commit installation failure is reported");
        check(!Directory.Exists(failedCommit),"post-commit failure rolls back destination and permits retry");
        InstallEngine.Install(payload,failedCommit,false,shell:false); InstallEngine.Uninstall(failedCommit,false);
        check(!Directory.Exists(failedCommit),"retry after post-commit failure succeeds");
        var repairRoot=Path.Combine(temp,"repair","MistikLauncherUltra");
        InstallEngine.Install(payload,repairRoot,false,shell:false);
        File.WriteAllText(Path.Combine(repairRoot,"config.json"),"preferences");
        Directory.CreateDirectory(Path.Combine(repairRoot,"game","saves"));
        File.WriteAllText(Path.Combine(repairRoot,"game","saves","world.dat"),"world");
        Directory.CreateDirectory(Path.Combine(repairRoot,"game","mods"));
        File.WriteAllText(Path.Combine(repairRoot,"game","mods","user.jar"),"mod");
        File.WriteAllText(Path.Combine(repairRoot,"user-notes.txt"),"notes");
        File.Delete(Path.Combine(repairRoot,names[0]));
        File.WriteAllText(Path.Combine(repairRoot,names[1]),"broken");
        File.WriteAllText(Path.Combine(repairRoot,"update-manifest.json"),"{broken");
        InstallEngine.Repair(payload,repairRoot,false,shell:false);
        check(names.Append(added).All(name=>File.ReadAllBytes(Path.Combine(repairRoot,name)).SequenceEqual(File.ReadAllBytes(Path.Combine(payload,name)))),"repair restores a missing launcher and corrupt DLL despite a broken update manifest");
        check(File.ReadAllText(Path.Combine(repairRoot,"config.json"))=="preferences" && File.ReadAllText(Path.Combine(repairRoot,"game","saves","world.dat"))=="world" && File.ReadAllText(Path.Combine(repairRoot,"game","mods","user.jar"))=="mod" && File.ReadAllText(Path.Combine(repairRoot,"user-notes.txt"))=="notes","repair preserves settings, worlds, mods and unrelated files");
        using(var locked=File.Open(Path.Combine(repairRoot,names[1]),FileMode.Open,FileAccess.Read,FileShare.None)) Reject(()=>InstallEngine.Repair(payload,repairRoot,false,shell:false),"repair refuses a running or locked application before changing files");
        check(File.ReadAllBytes(Path.Combine(repairRoot,names[0])).SequenceEqual(File.ReadAllBytes(Path.Combine(payload,names[0]))),"locked-file repair leaves previous installation intact");
        File.WriteAllText(Path.Combine(repairRoot,InstallEngine.Marker),JsonSerializer.Serialize(new InstallState("MistikLauncher",repairRoot,"9.0.0",names)));
        Reject(()=>InstallEngine.Repair(payload,repairRoot,false,shell:false),"old repair tool refuses to downgrade a newer installation");
        var unknownRoot=Path.Combine(temp,"unknown","MistikLauncherUltra"); Directory.CreateDirectory(unknownRoot);
        File.WriteAllText(Path.Combine(unknownRoot,"keep.txt"),"keep");
        Reject(()=>InstallEngine.Repair(payload,unknownRoot,false,shell:false),"repair rejects an unrecognized installation folder");
        check(File.ReadAllText(Path.Combine(unknownRoot,"keep.txt"))=="keep","unrecognized repair target is untouched");
        Reject(()=>InstallEngine.Install(payload,cancelled,false,token.Token,false),"cancelled installer rejects commit"); check(!Directory.Exists(cancelled),"cancelled install leaves no destination");
        File.WriteAllText(Path.Combine(payload,names[0]),"corrupted");
        Reject(()=>InstallEngine.Install(payload,cancelled,false,shell:false),"installer refuses corrupted manifest payload"); check(!Directory.Exists(cancelled),"corrupted payload leaves no destination");
    }
}
