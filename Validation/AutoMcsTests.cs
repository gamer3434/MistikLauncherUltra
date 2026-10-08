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

        var commitFolder=Path.Combine(root,"mcs-receipt-locked"); Directory.CreateDirectory(commitFolder);
        byte[] oldExecutable=(byte[])executable.Clone(); oldExecutable[^1]=42;
        string oldReceipt=JsonSerializer.Serialize(new AutoMcsReceipt("v8.0.0",Convert.ToHexString(SHA256.HashData(oldExecutable))));
        string receiptPath=Path.Combine(commitFolder,"auto-mcs.version.json");
        File.WriteAllBytes(Path.Combine(commitFolder,"auto-mcs.exe"),oldExecutable); File.WriteAllText(receiptPath,oldReceipt);
        using var commitClient=new HttpClient(new Stub(Manifest(digest),zip));
        var commitUpdater=new AutoMcsUpdater(commitClient,commitFolder,()=>false);
        using(var locked=File.Open(receiptPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Check(!await commitUpdater.CheckAsync(true) && commitUpdater.StatusKey=="mcsError" && commitUpdater.InstalledVersion=="v8.0.0" && File.ReadAllBytes(commitUpdater.ExecutablePath).SequenceEqual(oldExecutable) && File.ReadAllText(receiptPath)==oldReceipt,"receipt commit failure restores the previous Auto-MCS executable and leaves its receipt unchanged");
        Check(!Directory.EnumerateDirectories(commitFolder,".auto-mcs-*").Any(),"receipt commit rollback removes its staging directory");
        Check(await commitUpdater.CheckAsync(true) && File.ReadAllBytes(commitUpdater.ExecutablePath).SequenceEqual(executable) && commitUpdater.InstalledVersion=="v9.1.0","Auto-MCS receipt commit retry succeeds after the lock is released");

        var firstFolder=Path.Combine(root,"mcs-first-receipt-conflict"); Directory.CreateDirectory(Path.Combine(firstFolder,"auto-mcs.version.json"));
        using var firstClient=new HttpClient(new Stub(Manifest(digest),zip));
        var firstUpdater=new AutoMcsUpdater(firstClient,firstFolder,()=>false);
        Check(!await firstUpdater.CheckAsync(true) && !File.Exists(firstUpdater.ExecutablePath) && Directory.Exists(Path.Combine(firstFolder,"auto-mcs.version.json")) && !Directory.EnumerateDirectories(firstFolder,".auto-mcs-*").Any(),"failed first Auto-MCS receipt commit removes only its newly installed executable and staging files");

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
        using var stalledClient=new HttpClient(new Stub(Manifest(digest),zip,stall:true)) { Timeout=TimeSpan.FromMilliseconds(500) };
        var stalled=new AutoMcsUpdater(stalledClient,failureFolder,()=>false);
        var stalledCheck=stalled.CheckAsync(true);
        Check(await Task.WhenAny(stalledCheck,Task.Delay(5000))==stalledCheck && !await stalledCheck && !stalled.Busy && stalled.StatusKey=="mcsError" && File.ReadAllText(installed)=="existing installation","stalled Auto-MCS download times out and preserves installed executable");
        Check(!Directory.EnumerateDirectories(failureFolder,".auto-mcs-*").Any(),"timed-out Auto-MCS staging cleaned");
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
    sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        { await Task.Delay(Timeout.Infinite,cancellationToken); return 0; }
    }
    sealed class Stub(string manifest,byte[] package,bool fail=false,bool stall=false) : HttpMessageHandler
    {
        public int AssetRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(fail) throw new HttpRequestException("Simulated offline fixture");
            bool metadata=request.RequestUri!.Host=="api.github.com";
            if(!metadata) AssetRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content=metadata?new StringContent(manifest):stall?new StreamContent(new StalledStream()):new ByteArrayContent(package)
            });
        }
    }
}
