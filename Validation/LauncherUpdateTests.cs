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
        Check(LauncherUpdater.IsNewer("v7.0.0","6.0.0-preview.1") && LauncherUpdater.IsNewer("v6.0.0","6.0.0-preview.1"),"stable upgrade and preview promotion");
        Check(!LauncherUpdater.IsNewer("v5.5.2","6.0.0-preview.1") && !LauncherUpdater.IsNewer("v6.0.0","6.0.0"),"no downgrade or same-version loop");
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
            draft=false,prerelease=false,tag_name="v7.0.0",assets=new[] {new {
                name="MistikLauncher-7.0.0-win-x64.zip",browser_download_url="https://github.com/gamer3434/MistikLauncherUltra/releases/download/v7.0.0/MistikLauncher-7.0.0-win-x64.zip",
                digest="sha256:"+Convert.ToHexString(SHA256.HashData(zip)),size=zip.Length
            }}
        });
        string target=Path.Combine(root,"launcher-target"); Directory.CreateDirectory(target);
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        File.WriteAllText(Path.Combine(target,"config.json"),"user preferences");
        Directory.CreateDirectory(Path.Combine(target,"game")); File.WriteAllText(Path.Combine(target,"game","world.dat"),"world");
        using var client=new HttpClient(new Stub(metadata,zip));
        var service=new LauncherUpdater(()=>true,client,target,"6.0.0-preview.1");
        Check(!await service.CheckAsync(false) && service.StatusKey=="luAvailable" && service.PreparedPayload==null,"launcher check-only avoids download");
        Check(await service.CheckAsync(true) && service.PreparedPayload!=null && UpdateEngine.Verify(service.PreparedPayload).Version=="7.0.0","verified portable update prepared");
        Check(File.ReadAllText(Path.Combine(target,names[0]))=="old "+names[0],"preparation leaves installed files untouched");
        UpdateEngine.Apply(payload,target);
        Check(names.All(name=>File.ReadAllText(Path.Combine(target,name))=="new "+name),"full package replacement");
        Check(File.ReadAllText(Path.Combine(target,"config.json"))=="user preferences" && File.ReadAllText(Path.Combine(target,"game","world.dat"))=="world","launcher update preserves settings and worlds");
        Check(JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(Path.Combine(target,"update-manifest.json")))!.Version=="7.0.0","update replaces the installed manifest");
        foreach(var name in names) File.WriteAllText(Path.Combine(target,name),"old "+name);
        bool failed=false;
        using(var locked=new FileStream(Path.Combine(target,names[2]),FileMode.Open,FileAccess.Read,FileShare.None))
            try { UpdateEngine.Apply(payload,target); } catch(IOException) { failed=true; }
        Check(failed && names.All(name=>File.ReadAllText(Path.Combine(target,name))=="old "+name),"locked-file failure rolls back earlier replacements");
        bool rejected=false;
        try { UpdateEngine.SafePath(target,"../outside.exe"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"update path traversal rejected");
        rejected=false;
        try { UpdateEngine.SafePath(target,"config.json"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"user configuration overwrite rejected");
        using var deferredClient=new HttpClient(new Stub(metadata,zip));
        var deferred=new LauncherUpdater(()=>false,deferredClient,target,"6.0.0");
        Check(!await deferred.CheckAsync(true) && deferred.StatusKey=="luDeferred","busy launcher defers automatic update");
        using var corruptClient=new HttpClient(new Stub(metadata,zip.Select(b=>(byte)(b^1)).ToArray()));
        var corrupt=new LauncherUpdater(()=>true,corruptClient,target,"6.0.0");
        Check(!await corrupt.CheckAsync(true) && corrupt.PreparedPayload==null && corrupt.StatusKey=="luError","corrupt launcher package rejected");
        rejected=false;
        try { LauncherUpdater.ParseRelease(metadata.Replace("github.com/gamer3434","example.com/gamer3434"),"6.0.0"); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"untrusted launcher release host rejected");
        File.WriteAllText(Path.Combine(payload,names[0]),"tampered"); rejected=false;
        try { UpdateEngine.Verify(payload); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"staged payload tampering rejected");
        return checks;
    }
    sealed class Stub(string metadata,byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content=request.RequestUri!.Host=="api.github.com"?new StringContent(metadata):new ByteArrayContent(bytes)
        });
    }
}
