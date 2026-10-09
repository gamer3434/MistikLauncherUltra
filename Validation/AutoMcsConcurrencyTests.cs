using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher;

static class AutoMcsConcurrencyTests
{
    public static async Task<int> Run(string root)
    {
        string directory=Path.Combine(root,"mcs-concurrency"); Directory.CreateDirectory(directory);
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        byte[] Executable(byte marker) { var bytes=new byte[8192]; bytes[0]=0x4d; bytes[1]=0x5a; bytes[^1]=marker; return bytes; }
        byte[] Package(byte[] executable)
        {
            using var memory=new MemoryStream();
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
            { using var output=archive.CreateEntry("auto-mcs.exe").Open(); output.Write(executable); }
            return memory.ToArray();
        }
        string Manifest(string version,byte[] package,string? digest=null) => JsonSerializer.Serialize(new {
            draft=false,prerelease=false,tag_name=version,
            assets=new[] { new { name="auto-mcs-windows-fixture.zip",
                browser_download_url="https://github.com/macarooni-man/auto-mcs/releases/download/"+version+"/auto-mcs-windows-fixture.zip",
                digest="sha256:"+(digest??Convert.ToHexString(SHA256.HashData(package))),size=package.Length } }
        });
        var original=Executable(1); var firstBytes=Executable(2); var secondBytes=Executable(3);
        var firstPackage=Package(firstBytes); var secondPackage=Package(secondBytes);
        string executable=Path.Combine(directory,"auto-mcs.exe"),receipt=Path.Combine(directory,"auto-mcs.version.json");
        string oldReceipt=JsonSerializer.Serialize(new AutoMcsReceipt("v1",Convert.ToHexString(SHA256.HashData(original))));
        File.WriteAllBytes(executable,original); File.WriteAllText(receipt,oldReceipt);
        string world=Path.Combine(directory,"servers","world","level.dat"); Directory.CreateDirectory(Path.GetDirectoryName(world)!);
        File.WriteAllText(world,"player world bytes");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTransport=new FixtureTransport(Manifest("v2",firstPackage),firstPackage,entered,release);
        var secondTransport=new FixtureTransport(Manifest("v3",secondPackage),secondPackage);
        using var firstClient=new HttpClient(firstTransport); using var secondClient=new HttpClient(secondTransport);
        var first=new AutoMcsUpdater(firstClient,directory,()=>false);
        string alias=Path.Combine(directory,"..",Path.GetFileName(directory));
        var second=new AutoMcsUpdater(secondClient,alias,()=>false);
        Check(first.ExecutablePath==second.ExecutablePath && Path.IsPathRooted(second.ExecutablePath),"Auto-MCS updater instances canonicalize the same target directory");
        var firstCheck=first.CheckAsync(true);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(first.Busy && Directory.EnumerateDirectories(directory,".auto-mcs-*").Count()==1,"first Auto-MCS updater holds its transaction during an unfinished download");
            Check(!await second.CheckAsync(true) && !second.Busy && second.StatusKey=="mcsError" && secondTransport.Requests==0,
                "second Auto-MCS instance fails before metadata or file changes while the shared target is locked");
            Check(File.ReadAllBytes(executable).SequenceEqual(original) && File.ReadAllText(receipt)==oldReceipt &&
                !File.Exists(executable+".bak") && Directory.EnumerateDirectories(directory,".auto-mcs-*").Count()==1,
                "rejected concurrent update preserves executable receipt backup and the owning updater staging directory");
        }
        finally { release.TrySetResult(); await firstCheck; }
        Check(await firstCheck && File.ReadAllBytes(executable).SequenceEqual(firstBytes) &&
            JsonSerializer.Deserialize<AutoMcsReceipt>(File.ReadAllText(receipt))!.Version=="v2",
            "owning updater completes its executable and receipt transaction after download resumes");
        Check(!Directory.EnumerateDirectories(directory,".auto-mcs-*").Any(),"completed update removes only its own staging files");
        using var invalidClient=new HttpClient(new FixtureTransport(Manifest("v3",secondPackage,new string('0',64)),secondPackage));
        var invalid=new AutoMcsUpdater(invalidClient,directory,()=>false);
        Check(!await invalid.CheckAsync(true) && File.ReadAllBytes(executable).SequenceEqual(firstBytes) &&
            JsonSerializer.Deserialize<AutoMcsReceipt>(File.ReadAllText(receipt))!.Version=="v2",
            "failed verified update releases the shared target lock while preserving the current installation");
        Check(await second.CheckAsync(true) && File.ReadAllBytes(executable).SequenceEqual(secondBytes) &&
            File.ReadAllBytes(executable+".bak").SequenceEqual(firstBytes) &&
            JsonSerializer.Deserialize<AutoMcsReceipt>(File.ReadAllText(receipt))!.Version=="v3",
            "previously rejected updater can retry with an independent release after the shared lock is released");
        Check(File.ReadAllText(world)=="player world bytes" && !Directory.EnumerateDirectories(directory,".auto-mcs-*").Any(),
            "concurrent update and retry preserve server world data and leave no download staging");
        return checks;
    }

    sealed class PausingStream(byte[] bytes,TaskCompletionSource entered,TaskCompletionSource release) : MemoryStream(bytes)
    {
        bool paused;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(!paused) { paused=true; entered.TrySetResult(); await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
            return await base.ReadAsync(buffer,cancellationToken).ConfigureAwait(false);
        }
    }
    sealed class FixtureTransport(string manifest,byte[] package,TaskCompletionSource? entered=null,TaskCompletionSource? release=null) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;
            bool metadata=request.RequestUri!.Host=="api.github.com";
            HttpContent content=metadata ? new StringContent(manifest) : entered!=null && release!=null
                ? new StreamContent(new PausingStream(package,entered,release)) : new ByteArrayContent(package);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
        }
    }
}
