using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using MistikLauncher.Installation;
using MistikLauncher.Updates;
static class InstallerTests
{
    public static void Run(string temp,Action<bool,string> check)
    {
        DownloadTests(Path.Combine(temp,"installer-downloads"),check).GetAwaiter().GetResult();
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
        File.WriteAllText(marker,JsonSerializer.Serialize(new InstallState("MistikLauncher",root,"test",names.Append("../escape").ToArray())));
        Reject(()=>InstallEngine.Uninstall(root,false),"uninstaller rejects traversal before deleting any files");
        check(File.Exists(Path.Combine(root,names[0])),"malformed uninstall record leaves installation intact"); File.WriteAllText(marker,valid);
        File.WriteAllText(marker,JsonSerializer.Serialize(new InstallState("MistikLauncher",null!,"test",names)));
        bool nullRootRejected=false; try { InstallEngine.ReadState(root); } catch(IOException) { nullRootRejected=true; }
        check(nullRootRejected && File.Exists(Path.Combine(root,names[0])),"null installation root is rejected as a recoverable invalid record");
        File.WriteAllText(marker,JsonSerializer.Serialize(new InstallState("MistikLauncher",root,"test",names.Append("INSTALL-STATE.JSON").ToArray())));
        Reject(()=>InstallEngine.ReadState(root),"ownership record rejects its marker regardless of Windows filename casing");
        foreach(var incomplete in new[]{Array.Empty<string>(),new[]{"MistikLauncher.exe"}}) {
            var incompleteRecord=JsonSerializer.Serialize(new InstallState("MistikLauncher",root,"test",incomplete)); File.WriteAllText(marker,incompleteRecord);
            Reject(()=>InstallEngine.Uninstall(root,false),"empty or incomplete ownership record is rejected before uninstall");
            check(names.Append(added).All(name=>File.Exists(Path.Combine(root,name))) && File.ReadAllText(marker)==incompleteRecord,"incomplete ownership record failure preserves the program and its record");
        }
        File.WriteAllText(marker,valid);
        using(var locked=File.Open(Path.Combine(root,names[0]),FileMode.Open,FileAccess.Read,FileShare.None)) Reject(()=>InstallEngine.Uninstall(root,false),"uninstaller rejects running/locked app before deletion");
        var readOnlyOwned=Path.Combine(root,added); var ownedAttributes=File.GetAttributes(readOnlyOwned);
        File.SetAttributes(readOnlyOwned,ownedAttributes|FileAttributes.ReadOnly);
        try {
            Reject(()=>InstallEngine.Uninstall(root,false),"uninstaller rejects a later read-only owned file before deletion");
            check(names.Append(added).All(name=>File.Exists(Path.Combine(root,name))) && File.ReadAllText(marker)==valid && File.ReadAllText(Path.Combine(root,"user-notes.txt"))=="keep","read-only uninstall failure preserves every owned file, ownership record and user data");
        } finally { File.SetAttributes(readOnlyOwned,ownedAttributes); }
        var userFile=Path.Combine(root,"user-notes.txt"); var userAttributes=File.GetAttributes(userFile);
        File.SetAttributes(userFile,userAttributes|FileAttributes.ReadOnly);
        try {
            InstallEngine.Uninstall(root,false);
            check(File.Exists(userFile) && (File.GetAttributes(userFile)&FileAttributes.ReadOnly)!=0,"read-only unowned user files do not block uninstall and keep their attributes");
        } finally { File.SetAttributes(userFile,userAttributes); }
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
    static async Task DownloadTests(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        var zip=Path.Combine(root,"payload.zip");
        var bytes="verified payload fixture"u8.ToArray();
        var digest="sha256:"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using(var client=new HttpClient(new DownloadHandler(bytes,digest))) {
            check(await InstallEngine.DownloadPayloadAsync(root,"6.2.1",client)==zip && File.ReadAllBytes(zip).SequenceEqual(bytes),"online installer commits only a fully downloaded checksum-verified payload");
        }
        File.WriteAllText(zip,"previous verified payload");
        using(var client=new HttpClient(new DownloadHandler(bytes,"sha256:"+new string('0',64)))) {
            bool rejected=false; try { await InstallEngine.DownloadPayloadAsync(root,"6.2.1",client); } catch(IOException) { rejected=true; }
            check(rejected && File.ReadAllText(zip)=="previous verified payload" && !Directory.EnumerateFiles(root,"*.part").Any(),"installer checksum failure preserves previous payload and removes only its partial download");
        }
        foreach(bool metadata in new[]{true,false}) {
            using var client=new HttpClient(new DownloadHandler(bytes,digest,stall:metadata?"metadata":"download")) { Timeout=TimeSpan.FromMilliseconds(150) };
            var download=InstallEngine.DownloadPayloadAsync(root,"6.2.1",client);
            check(await Task.WhenAny(download,Task.Delay(5000))==download,"installer "+(metadata?"metadata":"payload")+" body cannot stall beyond its deadline");
            bool timedOut=false; try { await download; } catch(IOException error) { timedOut=error.Message.Contains("timed out"); }
            check(timedOut && File.ReadAllText(zip)=="previous verified payload" && !Directory.EnumerateFiles(root,"*.part").Any(),"installer "+(metadata?"metadata":"payload")+" timeout reports failure while preserving previous bytes and cleaning staging");
        }
        using(var client=new HttpClient(new DownloadHandler(bytes,digest,stall:"download")) { Timeout=Timeout.InfiniteTimeSpan })
        using(var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
            bool cancelled=false; try { await InstallEngine.DownloadPayloadAsync(root,"6.2.1",client,cancellation:cancellation.Token); } catch(OperationCanceledException) { cancelled=true; }
            check(cancelled && File.ReadAllText(zip)=="previous verified payload" && !Directory.EnumerateFiles(root,"*.part").Any(),"explicit installer cancellation remains cancellation and preserves the previous payload");
        }
    }
    sealed class DownloadHandler(byte[] bytes,string digest,string? stall=null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            bool metadata=request.RequestUri!.Host=="api.github.com";
            HttpContent content;
            if(stall==(metadata?"metadata":"download")) content=new StreamContent(new StalledStream());
            else if(metadata) content=new StringContent(JsonSerializer.Serialize(new { assets=new[]{new { name="MistikLauncher-6.2.1-win-x64.zip",browser_download_url="https://github.com/gamer3434/MistikLauncherUltra/releases/download/v6.2.1/payload.zip",size=bytes.Length,digest }} }));
            else content=new ByteArrayContent(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content,RequestMessage=request });
        }
    }
    sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        { await Task.Delay(Timeout.Infinite,cancellationToken); return 0; }
    }
}
