using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher;

static class AutoMcsTests
{
    public static async Task<int> Run(string root)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var executable=new byte[8192]; executable[0]=0x4d; executable[1]=0x5a;
        byte[] zip;
        using(var memory=new MemoryStream())
        {
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
            { using var entry=archive.CreateEntry("auto-mcs.exe").Open(); entry.Write(executable); }
            zip=memory.ToArray();
        }
        string digest=Convert.ToHexString(SHA256.HashData(zip));
        string Manifest(string hash) => JsonSerializer.Serialize(new {
            draft=false, prerelease=false, tag_name="v9.1.0",
            assets=new[] { new { name="auto-mcs-windows-9.1.0.zip", browser_download_url="https://github.com/macarooni-man/auto-mcs/releases/download/v9.1.0/auto-mcs-windows-9.1.0.zip", digest="sha256:"+hash, size=zip.Length } }
        });
        var transport=new Stub(Manifest(digest),zip);
        using var client=new HttpClient(transport);
        var folder=Path.Combine(root,"mcs-success");
        var updater=new AutoMcsUpdater(client,folder,()=>false);
        Check(!await updater.CheckAsync(false) && !File.Exists(updater.ExecutablePath),"check-only never installs");
        Check(await updater.CheckAsync(true) && updater.InstalledVersion=="v9.1.0", "official Windows package installation");
        Check(File.ReadAllBytes(updater.ExecutablePath).SequenceEqual(executable), "verified executable extracted");
        Check(await updater.CheckAsync(true) && transport.AssetRequests==1,"current version avoids repeat downloads");
        File.WriteAllText(updater.ExecutablePath,"damaged executable");
        Check(await updater.CheckAsync(true) && transport.AssetRequests==2,"damaged installed executable repaired");

        var failureFolder=Path.Combine(root,"mcs-failure"); Directory.CreateDirectory(failureFolder);
        string installed=Path.Combine(failureFolder,"auto-mcs.exe"); File.WriteAllText(installed,"existing installation");
        using var badClient=new HttpClient(new Stub(Manifest(new string('0',64)),zip));
        var bad=new AutoMcsUpdater(badClient,failureFolder,()=>false);
        Check(!await bad.CheckAsync(true) && File.ReadAllText(installed)=="existing installation","digest mismatch preserves installed executable");
        Check(!Directory.EnumerateDirectories(failureFolder,".auto-mcs-*").Any(),"failed download staging cleaned");
        var runningTransport=new Stub(Manifest(digest),zip);
        using var runningClient=new HttpClient(runningTransport);
        var running=new AutoMcsUpdater(runningClient,failureFolder,()=>true);
        Check(!await running.CheckAsync(true) && running.StatusKey=="mcsRunning" && runningTransport.AssetRequests==0,"running Auto-MCS defers update");
        using var offlineClient=new HttpClient(new Stub("",zip,true));
        var offline=new AutoMcsUpdater(offlineClient,failureFolder,()=>false);
        Check(!await offline.CheckAsync(true) && File.ReadAllText(installed)=="existing installation","offline update preserves installed executable");
        bool rejected=false;
        try { AutoMcsUpdater.ParseRelease(Manifest(digest).Replace("github.com/macarooni-man","example.com/macarooni-man")); }
        catch(InvalidDataException) { rejected=true; }
        Check(rejected,"third-party download URL rejected");
        rejected=false;
        try { AutoMcsUpdater.ParseRelease(Manifest(digest).Replace("sha256:"+digest,"md5:obsolete")); }
        catch(InvalidDataException) { rejected=true; }
        Check(rejected,"missing official SHA-256 rejected");
        return checks;
    }
    sealed class Stub(string manifest,byte[] package,bool fail=false) : HttpMessageHandler
    {
        public int AssetRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(fail) throw new HttpRequestException("Simulated offline fixture");
            bool metadata=request.RequestUri!.Host=="api.github.com";
            if(!metadata) AssetRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content=metadata?new StringContent(manifest):new ByteArrayContent(package)
            });
        }
    }
}
