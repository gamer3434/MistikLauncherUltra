using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MistikLauncher;

static class JavaRuntimeInstallerTests
{
    public static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        async Task<bool> Rejected(string folder, Stub transport, CancellationToken token = default, double timeout = 1000)
        {
            using var client = new HttpClient(transport) { Timeout = TimeSpan.FromMilliseconds(timeout) };
            try { await JavaRuntimeInstaller.InstallAsync(folder, 21, client: client, cancellationToken: token); return false; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or TimeoutException or OperationCanceledException) { return true; }
        }
        bool Clean(string folder) => !Directory.EnumerateDirectories(folder, ".java-*").Any();
        byte[] package = Package(21);
        foreach (int major in new[] { 21, 25 })
        {
            string folder = Path.Combine(root, "java-success-" + major);
            var transport = new Stub(Package(major), major);
            using var client = new HttpClient(transport);
            string executable = await JavaRuntimeInstaller.InstallAsync(folder, major, client: client);
            Check(File.Exists(executable) && JavaRuntimeInstaller.IsUsableRuntime(Path.GetDirectoryName(Path.GetDirectoryName(executable)!)!, major), "verified Java " + major + " runtime installation");
            await JavaRuntimeInstaller.InstallAsync(folder, major, client: client);
            Check(transport.AssetRequests == 1 && Clean(folder), "valid Java " + major + " reused without a download and staging cleaned");
            string runtime = Path.Combine(folder, "jre" + major);
            File.Delete(Path.Combine(runtime, "bin", "jli.dll"));
            Check(!JavaRuntimeInstaller.IsUsableRuntime(runtime, major), "missing Java launcher dependency is detected for Java " + major);
            await JavaRuntimeInstaller.InstallAsync(folder, major, client: client);
            Check(transport.AssetRequests == 2 && JavaRuntimeInstaller.IsUsableRuntime(runtime, major), "incomplete Java " + major + " runtime is repaired");
        }

        string preserved = Path.Combine(root, "java-preserve");
        string old = Path.Combine(preserved, "jre21", "bin", "java.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllBytes(old, new byte[60000]);
        string sibling = Path.Combine(preserved, "jre25", "user-marker.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!); File.WriteAllText(sibling, "other Java and game remain untouched");
        Check(!JavaRuntimeInstaller.IsUsableRuntime(Path.Combine(preserved, "jre21"), 21), "large garbage java.exe does not count as a usable runtime");
        Check(await Rejected(preserved, new Stub(package, 21, badHash: true)) && new FileInfo(old).Length == 60000 && Clean(preserved), "Java checksum failure preserves the old runtime and cleans staging");
        Check(await Rejected(preserved, new Stub(Package(25), 21)) && new FileInfo(old).Length == 60000 && Clean(preserved), "wrong Java major is rejected before replacing an existing runtime");
        Check(await Rejected(preserved, new Stub(Package(21, "../../outside.txt"), 21)) && new FileInfo(old).Length == 60000 && !File.Exists(Path.Combine(root, "outside.txt")) && Clean(preserved), "Java ZIP traversal is rejected before commit");
        Check(await Rejected(preserved, new Stub(package, 21, untrusted: true)) && new FileInfo(old).Length == 60000 && Clean(preserved), "untrusted Java archive host rejected");
        Check(await Rejected(preserved, new Stub(package, 21, redirect: true)) && new FileInfo(old).Length == 60000 && Clean(preserved), "untrusted Java redirect rejected");
        using (var held = File.Open(old, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(await Rejected(preserved, new Stub(package, 21)) && new FileInfo(old).Length == 60000 && Clean(preserved), "locked existing Java remains intact and is never terminated");
        Check(File.ReadAllText(sibling) == "other Java and game remain untouched", "Java21 repairs preserve the Java25 runtime");

        var stalled = Rejected(preserved, new Stub(package, 21, stall: true), timeout: 100);
        Check(await Task.WhenAny(stalled, Task.Delay(5000)) == stalled && await stalled && new FileInfo(old).Length == 60000 && Clean(preserved), "Java stalled body times out and leaves the old runtime and no staging");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.CancelAfter(100);
            using var client = new HttpClient(new Stub(package, 21, stall: true)) { Timeout = TimeSpan.FromSeconds(10) };
            bool cancelled = false;
            try { await JavaRuntimeInstaller.InstallAsync(preserved, 21, client: client, cancellationToken: cancel.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && Clean(preserved) && new FileInfo(old).Length == 60000, "Java caller cancellation propagates and cleans staging");
        }
        using (var client = new HttpClient(new Stub(package, 21))) await JavaRuntimeInstaller.InstallAsync(preserved, 21, client: client);
        Check(JavaRuntimeInstaller.IsUsableRuntime(Path.Combine(preserved, "jre21"), 21) && Clean(preserved), "Java repair succeeds after a failed attempt and removes the obsolete backup");
        return checks;
    }

    static byte[] Package(int major, string? malicious = null)
    {
        var pe = new byte[256]; pe[0] = 0x4d; pe[1] = 0x5a; pe[0x3c] = 64; pe[64] = 0x50; pe[65] = 0x45; pe[68] = 0x64; pe[69] = 0x86;
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, byte[] bytes) { using var entry = zip.CreateEntry(name).Open(); entry.Write(bytes); }
            Entry("jdk-jre/release", Encoding.UTF8.GetBytes($"JAVA_VERSION=\"{major}.0.3\"\n"));
            Entry("jdk-jre/bin/java.exe", pe); Entry("jdk-jre/bin/server/jvm.dll", pe); Entry("jdk-jre/bin/java.dll", pe); Entry("jdk-jre/bin/jli.dll", pe); Entry("jdk-jre/lib/modules", new byte[] { 1, 2, 3 });
            if (malicious != null) Entry(malicious, new byte[] { 9 });
        }
        return memory.ToArray();
    }

    sealed class Stub(byte[] package, int major, bool badHash = false, bool untrusted = false, bool redirect = false, bool stall = false) : HttpMessageHandler
    {
        public int AssetRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "api.adoptium.net")
            {
                string metadata = JsonSerializer.Serialize(new[] { new { version = new { major }, binary = new { architecture = "x64", os = "windows", image_type = "jre", package = new { checksum = badHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(package)), size = package.Length, link = $"https://{(untrusted ? "example.com" : "github.com")}/adoptium/temurin{major}-binaries/releases/download/test/runtime.zip" } } } });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) });
            }
            AssetRequests++;
            if (redirect)
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri("https://example.com/runtime.zip"); return Task.FromResult(response);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = stall ? new StreamContent(new StalledStream()) : new ByteArrayContent(package) });
        }
    }

    sealed class StalledStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
