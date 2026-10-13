using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher;
using MistikLauncher.Updates;
static class LauncherUpdateTests
{
    public static async Task<int> Run(string root)
    {
        int checks=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        string LatestTag(Uri page) => (string)typeof(LauncherUpdater).GetMethod("LatestTagFromPage",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{page})!;
        string PackageChecksum(string sums,string name) => (string)typeof(LauncherUpdater).GetMethod("PackageChecksum",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{sums,name})!;
        const string fallbackHash="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        bool rejectedLatest=false;
        try { LatestTag(new Uri("https://evil.invalid/gamer3434/MistikLauncherUltra/releases/tag/v7.0.0")); } catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is InvalidDataException) { rejectedLatest=true; }
        Check(LatestTag(new Uri("https://github.com/gamer3434/MistikLauncherUltra/releases/tag/v7.0.0"))=="v7.0.0" && rejectedLatest && PackageChecksum(fallbackHash+"  MistikLauncher-7.0.0-win-x64.zip\r\n", "MistikLauncher-7.0.0-win-x64.zip")==fallbackHash,
            "rate-limit fallback accepts only this repository's stable release and exact package checksum");
        Check(LauncherUpdater.IsNewer("v7.0.0","6.0.0-preview.1") && LauncherUpdater.IsNewer("v6.0.0","6.0.0-preview.1"),"stable upgrade and preview promotion");
        Check(!LauncherUpdater.IsNewer("v5.5.2","6.0.0-preview.1") && !LauncherUpdater.IsNewer("v6.0.0","6.0.0"),"no downgrade or same-version loop");
        Check(!LauncherUpdater.IsNewer("v7.0.0junk","6.0.0") && !LauncherUpdater.IsNewer("v7.0.0","6.0.0junk") && !LauncherUpdater.IsNewer("v999999999999.0.0","6.0.0"),"malformed release version rejected");
        Check(LauncherUpdater.IsNewer("v6.0.0-preview.9","6.0.0-preview.8") && !LauncherUpdater.IsNewer("v6.0.0-preview.9","6.0.0-preview.9") && !LauncherUpdater.IsNewer("v6.0.0-preview.9","6.0.0"),"published preview comparison avoids repeat updates and stable downgrades");
        string payload=Path.Combine(root,"launcher-payload"); Directory.CreateDirectory(payload);
        var names=new[] {"MistikLauncher.exe","MistikLauncher.dll","MistikUpdater.exe"};
        foreach(var name in names) File.WriteAllText(Path.Combine(payload,name),"new "+name);
        var manifest=new UpdateManifest("MistikLauncher","7.0.0",names.Select(name=>new UpdateFile(name,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(payload,name)))))).ToArray());
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),JsonSerializer.Serialize(manifest));
        byte[] zip;
        using(var memory=new MemoryStream())
        {
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
                foreach(var name in names.Append("update-manifest.json")) { using var entry=archive.CreateEntry(name).Open(); entry.Write(File.ReadAllBytes(Path.Combine(payload,name))); }
            zip=memory.ToArray();
        }
        string metadata=JsonSerializer.Serialize(new {
            draft=false,prerelease=false,tag_name="v7.0.0",body="# Mistik 7\n## Türkçe\n- **Yeni** sürüm [indir](https://example.com)\n- Hatalar düzeltildi\n## English\n- **New** release [download](https://example.com)\n- Fixed issues",assets=new[] {new {
                name="MistikLauncher-7.0.0-win-x64.zip",browser_download_url="https://github.com/gamer3434/MistikLauncherUltra/releases/download/v7.0.0/MistikLauncher-7.0.0-win-x64.zip",
                digest="sha256:"+Convert.ToHexString(SHA256.HashData(zip)),size=zip.Length
            }}
        });
        string target=Path.Combine(root,"launcher-target"); Directory.CreateDirectory(target);
        using(var fallbackClient=new HttpClient(new LatestReleaseFallback(metadata,zip)))
        {
            var fallback=new LauncherUpdater(()=>true,fallbackClient,target,"6.0.0-preview.1");
            Check(!await fallback.CheckAsync(false) && fallback.LatestVersion=="v7.0.0" && fallback.StatusKey=="luAvailable",
                "launcher discovers the stable update from the official release page when GitHub API is throttled");
        }
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        var profileBytes=System.Text.Encoding.ASCII.GetBytes("MLUC1\n").Concat(WindowsSecret.Transform(System.Text.Encoding.UTF8.GetBytes("{\"user\":\"PersistedPlayer\",\"curseforge_api_key\":\"fake_update_fixture_key\"}"),true)).ToArray();
        File.WriteAllBytes(Path.Combine(target,"config.json"),profileBytes);
        Directory.CreateDirectory(Path.Combine(target,"game")); File.WriteAllText(Path.Combine(target,"game","world.dat"),"world");
        using var client=new HttpClient(new Stub(metadata,zip));
        var notified=new LauncherUpdater(()=>true,client,target,"6.0.0");
        int successfulNotifications=0;
        void BrokenNotification() => throw new InvalidOperationException("Notification fixture failure");
        void SuccessfulNotification() => successfulNotifications++;
        notified.Changed+=BrokenNotification; notified.Changed+=SuccessfulNotification;
        Check(!await notified.CheckAsync(false) && notified.StatusKey=="luAvailable" && !notified.Busy && successfulNotifications>0,
            "failed update notification does not interrupt work or prevent other observers");
        notified.Changed-=BrokenNotification;
        int beforeRetry=successfulNotifications;
        Check(!await notified.CheckAsync(false) && notified.StatusKey=="luAvailable" && !notified.Busy && successfulNotifications>beforeRetry,
            "update check releases its gate after a failed notification and allows retry");
        notified.Changed+=BrokenNotification;
        Check(!await notified.StartInstallerAsync() && notified.StatusKey=="luDeferred" && !notified.Busy,
            "installer notification failure cannot strand its busy state or gate");
        notified.Changed-=BrokenNotification;
        beforeRetry=successfulNotifications;
        Check(!await notified.CheckAsync(false) && notified.StatusKey=="luAvailable" && successfulNotifications>beforeRetry,
            "update check remains usable after installer notification failure");
        bool canUpdate=true;
        var service=new LauncherUpdater(()=>canUpdate,client,target,"6.0.0-preview.1");
        Check(!await service.CheckAsync(false) && service.StatusKey=="luAvailable" && service.PreparedPayload==null,"launcher check-only avoids download");
        Check(service.ReleaseNotes("tr").Contains("Yeni sürüm indir") && !service.ReleaseNotes("tr").Contains("Fixed") && !service.ReleaseNotes("tr").Contains("https://"),"Turkish release notes are short plain text");
        Check(service.ReleaseNotes("en").Contains("New release download") && !service.ReleaseNotes("en").Contains("Hatalar"),"English release notes follow selected language");
        Check(await service.CheckAsync(true) && service.PreparedPayload!=null && UpdateEngine.Verify(service.PreparedPayload).Version=="7.0.0","verified portable update prepared");
        canUpdate=false;
        Check(!await service.CheckAsync(true) && service.StatusKey=="luDeferred","prepared update stays deferred while launcher is busy");
        canUpdate=true;
        Check(await service.CheckAsync(true) && service.StatusKey=="luReady","prepared update resumes when launcher becomes idle");
        bool verifyingWasBusy=false;
        void DeferDuringVerify() { if(service.StatusKey=="luVerifying") { verifyingWasBusy=service.Busy; canUpdate=false; } }
        service.Changed+=DeferDuringVerify;
        var stagePlan=Path.Combine(Path.GetDirectoryName(service.PreparedPayload!)!,"plan.json");
        File.WriteAllText(Path.Combine(target,"MistikUpdater.exe"),"corrupt installed helper");
        Check(!await service.StartInstallerAsync() && verifyingWasBusy && !service.Busy && service.StatusKey=="luDeferred" && !File.Exists(stagePlan),"launcher becoming busy during payload verification prevents helper handoff");
        Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(stagePlan)!,"MistikUpdater.exe"))=="new MistikUpdater.exe","handoff copies the verified new package helper even when the installed helper is corrupt");
        service.Changed-=DeferDuringVerify;
        canUpdate=true;
        Check(File.ReadAllText(Path.Combine(target,names[0]))=="old "+names[0],"preparation leaves installed files untouched");
        File.WriteAllText(Path.Combine(target,"update-manifest.json"),"{\"Product\":\"MistikLauncher\",\"Version\":\"8.0.0\",\"Files\":[]}");
        bool downgradeBlocked=false;
        try { UpdateEngine.Apply(payload,target); } catch(InvalidDataException ex) { downgradeBlocked=ex.Message.Contains("Downgrade blocked",StringComparison.Ordinal); }
        Check(downgradeBlocked,"older update package is blocked before file replacement");
        Check(LauncherUpdater.ParseRelease(metadata,"8.0.0",true)?.Version=="v7.0.0" && LauncherUpdater.ParseRelease(metadata,"8.0.0")==null,"only explicit selection permits an older stable release");
        bool missingSelection=false;
        try { UpdateEngine.Apply(payload,target,allowDowngrade:true); } catch(InvalidDataException) { missingSelection=true; }
        Check(missingSelection,"explicit downgrade requires an expected release version");
        UpdateEngine.Apply(payload,target,"v7.0.0",allowDowngrade:true);
        Check(JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(Path.Combine(target,"update-manifest.json")))!.Version=="7.0.0" && File.ReadAllBytes(Path.Combine(target,"config.json")).SequenceEqual(profileBytes) && File.ReadAllText(Path.Combine(target,"game","world.dat"))=="world","explicit downgrade preserves account settings and worlds byte for byte");
        File.Delete(Path.Combine(target,"update-manifest.json"));
        UpdateEngine.Apply(payload,target);
        Check(names.All(name=>File.ReadAllText(Path.Combine(target,name))=="new "+name),"full package replacement");
        Check(File.ReadAllBytes(Path.Combine(target,"config.json")).SequenceEqual(profileBytes) && File.ReadAllText(Path.Combine(target,"game","world.dat"))=="world","launcher update preserves encrypted player name, API key and worlds byte for byte");
        Check(JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(Path.Combine(target,"update-manifest.json")))!.Version=="7.0.0","update replaces the installed manifest");
        Check(!Directory.EnumerateDirectories(root,"backup-*").Any(),"successful update removes its private backup directory");
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        bool failed=false;
        using(var locked=new FileStream(Path.Combine(target,names[2]),FileMode.Open,FileAccess.Read,FileShare.None))
            try { UpdateEngine.Apply(payload,target); } catch(IOException) { failed=true; }
        Check(failed && names.All(name=>File.ReadAllText(Path.Combine(target,name))=="old "+name),"locked-file failure rolls back earlier replacements");
        Check(!Directory.EnumerateDirectories(root,"backup-*").Any(),"completed update rollback removes its private backup directory");
        bool readOnlyRejected=false;
        File.SetAttributes(Path.Combine(target,names[0]),FileAttributes.ReadOnly);
        try { UpdateEngine.Apply(payload,target); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { readOnlyRejected=true; }
        finally { File.SetAttributes(Path.Combine(target,names[0]),FileAttributes.Normal); }
        Check(readOnlyRejected && names.All(name=>File.ReadAllText(Path.Combine(target,name))=="old "+name) && !Directory.EnumerateDirectories(root,"backup-*").Any(),"read-only installed file failure preserves the installation and cleans its read-only backup copy");
        bool rollbackFailed=false;
        File.SetAttributes(Path.Combine(payload,names[0]),FileAttributes.ReadOnly);
        try {
            using var locked=new FileStream(Path.Combine(target,names[1]),FileMode.Open,FileAccess.Read,FileShare.None);
            try { UpdateEngine.Apply(payload,target); } catch(AggregateException ex) { rollbackFailed=ex.InnerExceptions.Any(error=>error is UnauthorizedAccessException); }
        } finally { File.SetAttributes(Path.Combine(payload,names[0]),FileAttributes.Normal); File.SetAttributes(Path.Combine(target,names[0]),FileAttributes.Normal); }
        var recovery=Directory.EnumerateDirectories(root,"backup-*").SingleOrDefault();
        Check(rollbackFailed && recovery!=null && File.ReadAllText(Path.Combine(recovery,names[0]))=="old "+names[0],"failed update rollback retains the original executable in its recovery backup");
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        var priorRecovery=Directory.EnumerateDirectories(root,"backup-*").ToHashSet(StringComparer.OrdinalIgnoreCase);
        AggregateException? partialRollback=null;
        File.SetAttributes(Path.Combine(payload,names[1]),FileAttributes.ReadOnly);
        try {
            using var locked=new FileStream(Path.Combine(target,names[2]),FileMode.Open,FileAccess.Read,FileShare.None);
            try { UpdateEngine.Apply(payload,target); } catch(AggregateException ex) { partialRollback=ex; }
        } finally { File.SetAttributes(Path.Combine(payload,names[1]),FileAttributes.Normal); File.SetAttributes(Path.Combine(target,names[1]),FileAttributes.Normal); }
        var partialRecovery=Directory.EnumerateDirectories(root,"backup-*").SingleOrDefault(path=>!priorRecovery.Contains(path));
        Check(partialRollback?.InnerExceptions.Count>=2 && partialRollback.InnerExceptions[0] is IOException &&
            partialRecovery!=null && File.ReadAllText(Path.Combine(target,names[0]))=="old "+names[0] &&
            File.ReadAllText(Path.Combine(partialRecovery,names[1]))=="old "+names[1] && partialRollback.Message.Contains(partialRecovery,StringComparison.Ordinal),
            "rollback restores remaining files after one recovery fails and preserves original error plus recovery location");
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        bool rejected=false;
        try { UpdateEngine.SafePath(target,"../outside.exe"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"update path traversal rejected");
        rejected=false;
        try { UpdateEngine.SafePath(target,"config.json"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"user configuration overwrite rejected");
        using var deferredClient=new HttpClient(new Stub(metadata,zip));
        var deferred=new LauncherUpdater(()=>false,deferredClient,target,"6.0.0");
        Check(!await deferred.CheckAsync(true) && deferred.StatusKey=="luDeferred","busy launcher defers automatic update");
        string stagesRoot=Path.Combine(Path.GetTempPath(),"MistikLauncherUpdates");
        var stagesBeforeFailure=Directory.GetDirectories(stagesRoot).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var corruptClient=new HttpClient(new Stub(metadata,zip.Select(b=>(byte)(b^1)).ToArray()));
        var corrupt=new LauncherUpdater(()=>true,corruptClient,target,"6.0.0");
        Check(!await corrupt.CheckAsync(true) && corrupt.PreparedPayload==null && corrupt.StatusKey=="luError","corrupt launcher package rejected");
        using var stalledClient=new HttpClient(new Stub(metadata,zip,stall:true)) { Timeout=TimeSpan.FromMilliseconds(500) };
        var stalled=new LauncherUpdater(()=>true,stalledClient,target,"6.0.0");
        var stalledCheck=stalled.CheckAsync(true);
        Check(await Task.WhenAny(stalledCheck,Task.Delay(5000))==stalledCheck && !await stalledCheck && !stalled.Busy && stalled.PreparedPayload==null && stalled.StatusKey=="luError","stalled launcher download times out and releases busy state");
        Check(Directory.GetDirectories(stagesRoot).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(stagesBeforeFailure),"corrupt and stalled update downloads remove their abandoned staging directories");
        rejected=false;
        try { LauncherUpdater.ParseRelease(metadata.Replace("github.com/gamer3434","example.com/gamer3434"),"6.0.0"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"untrusted launcher release host rejected");
        File.WriteAllText(Path.Combine(payload,names[0]),"tampered"); rejected=false;
        try { UpdateEngine.Verify(payload); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"staged payload tampering rejected");
        File.WriteAllText(Path.Combine(service.PreparedPayload!,names[0]),"tampered staged helper payload");
        var damagedPayload=service.PreparedPayload;
        Check(!await service.StartInstallerAsync() && service.PreparedPayload==null && !Directory.Exists(Path.GetDirectoryName(damagedPayload!)) && !service.Busy && service.StatusKey=="luError" && !string.IsNullOrWhiteSpace(service.Error),"failed update handoff removes unusable stage and releases busy state");
        Check(await service.CheckAsync(true) && service.PreparedPayload!=null && service.PreparedPayload!=damagedPayload && UpdateEngine.Verify(service.PreparedPayload).Version=="7.0.0" && !service.Busy,"update handoff failure permits downloading and verifying a fresh package on retry");
        using var releasesClient=new HttpClient(new Stub("["+metadata+","+metadata.Replace("sha256:","invalid:")+"]",zip));
        var releasesService=new LauncherUpdater(()=>true,releasesClient,target,"8.0.0");
        Check((await releasesService.OlderVersionsAsync()).SequenceEqual(new[] {"v7.0.0"}),"older release selector excludes packages missing official digests");
        using var downgradeClient=new HttpClient(new Stub(metadata,zip));
        bool downgradeIdle=true;
        var downgradeService=new LauncherUpdater(()=>downgradeIdle,downgradeClient,target,"8.0.0");
        Check(await downgradeService.PrepareDowngradeAsync("v7.0.0") && UpdateEngine.Verify(downgradeService.PreparedPayload!).Version=="7.0.0","explicit downgrade downloads and verifies the selected older package");
        File.WriteAllText(Path.Combine(target,"MistikUpdater.exe"),"current downgrade-capable helper");
        var currentManifest=new UpdateManifest("MistikLauncher","8.0.0",names.Select(name=>new UpdateFile(name,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,name)))))).ToArray());
        File.WriteAllText(Path.Combine(target,"update-manifest.json"),JsonSerializer.Serialize(currentManifest));
        void PauseDowngrade() { if(downgradeService.StatusKey=="luVerifying") downgradeIdle=false; }
        downgradeService.Changed+=PauseDowngrade;
        string downgradeStage=Path.GetDirectoryName(downgradeService.PreparedPayload!)!;
        Check(!await downgradeService.StartInstallerAsync() && File.ReadAllText(Path.Combine(downgradeStage,"MistikUpdater.exe"))=="current downgrade-capable helper","downgrade handoff uses the verified current helper instead of an older incompatible helper");
        downgradeService.Changed-=PauseDowngrade; downgradeIdle=true;
        Check(!await downgradeService.CheckAsync(false) && downgradeService.PreparedPayload==null,"automatic release check cannot reuse a staged downgrade");
        bool wrongSelection=false;
        try { await downgradeService.PrepareDowngradeAsync("v9.0.0"); } catch(InvalidDataException) { wrongSelection=true; }
        Check(wrongSelection,"downgrade selection rejects newer releases");
        return checks;
    }
    sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        { await Task.Delay(Timeout.Infinite,cancellationToken); return 0; }
    }
    sealed class Stub(string metadata,byte[] bytes,bool stall=false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content=request.RequestUri!.Host=="api.github.com"?new StringContent(metadata):stall?new StreamContent(new StalledStream()):new ByteArrayContent(bytes)
        });
    }
    sealed class LatestReleaseFallback(string metadata,byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var uri=request.RequestUri!;
            HttpResponseMessage response;
            if(uri.Host=="api.github.com") response=new HttpResponseMessage(HttpStatusCode.Forbidden);
            else if(request.Method==HttpMethod.Get && uri.AbsolutePath.EndsWith("/releases/latest",StringComparison.Ordinal))
            {
                response=new HttpResponseMessage(HttpStatusCode.OK);
                response.RequestMessage=new HttpRequestMessage(HttpMethod.Get,"https://github.com/gamer3434/MistikLauncherUltra/releases/tag/v7.0.0");
            }
            else if(uri.AbsolutePath.EndsWith("/SHA256SUMS.txt",StringComparison.Ordinal))
            {
                using var document=JsonDocument.Parse(metadata);
                var asset=document.RootElement.GetProperty("assets")[0];
                response=new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(asset.GetProperty("digest").GetString()![7..]+"  "+asset.GetProperty("name").GetString()+"\n") };
            }
            else if(request.Method==HttpMethod.Head) { response=new HttpResponseMessage(HttpStatusCode.OK); response.Content=new ByteArrayContent(Array.Empty<byte>()); response.Content.Headers.ContentLength=bytes.Length; }
            else response=new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }
}
