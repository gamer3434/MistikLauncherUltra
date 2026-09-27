using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using MistikLauncher;

public static class GameRuntimeHealthTests
{
    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        byte[] clientJar = Jar("client");
        byte[] libraryJar = Jar("library");

        string healthy = Path.Combine(root, "healthy");
        Profile(healthy, "1.21", ProfileJson("1.21", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(healthy, "versions", "1.21", "1.21.jar"), clientJar);
        Write(Path.Combine(healthy, "libraries", "fixture", "lib", "1", "lib-1.jar"), libraryJar);
        var noRequest = new FakeHandler(_ => throw new Exception("healthy runtime requested the network"));
        var result = await GameRuntimeHealth.VerifyAndRepairAsync(healthy, "1.21", new HttpClient(noRequest));
        Check(result.CanLaunch && result.RepairedCount == 0 && noRequest.Requests == 0, "healthy runtime performs no network request");

        string repair = Path.Combine(root, "repair");
        Profile(repair, "1.21", ProfileJson("1.21", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(repair, "versions", "1.21", "1.21.jar"), clientJar);
        string repairPath = Path.Combine(repair, "libraries", "fixture", "lib", "1", "lib-1.jar");
        Write(repairPath, "corrupt"u8.ToArray());
        var repairHandler = new FakeHandler(_ => Bytes(libraryJar));
        result = await GameRuntimeHealth.VerifyAndRepairAsync(repair, "1.21", new HttpClient(repairHandler));
        Check(result.CanLaunch && result.RepairedCount == 1 && File.ReadAllBytes(repairPath).SequenceEqual(libraryJar), "corrupt artifact is downloaded, verified and replaced");

        string failed = Path.Combine(root, "failed-stream");
        Profile(failed, "1.21", ProfileJson("1.21", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(failed, "versions", "1.21", "1.21.jar"), clientJar);
        string failedPath = Path.Combine(failed, "libraries", "fixture", "lib", "1", "lib-1.jar");
        byte[] previous = "keep previous bytes"u8.ToArray();
        Write(failedPath, previous);
        var failedHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ThrowingStream(libraryJar, 8)) });
        result = await GameRuntimeHealth.VerifyAndRepairAsync(failed, "1.21", new HttpClient(failedHandler));
        Check(!result.CanLaunch && result.FailedArtifact == "library:fixture:lib:1" && result.FailedPath == failedPath && result.FailureReason!.Contains("Repair failed") && File.ReadAllBytes(failedPath).SequenceEqual(previous), "failed streamed repair reports exact artifact and preserves prior file");
        Check(!Directory.EnumerateFiles(Path.GetDirectoryName(failedPath)!, ".*.mistik-*.tmp").Any(), "failed streamed repair removes sibling temporary file");

        string mismatch = Path.Combine(root, "digest-mismatch");
        Profile(mismatch, "1.21", ProfileJson("1.21", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(mismatch, "versions", "1.21", "1.21.jar"), clientJar);
        byte[] wrongDigest = (byte[])libraryJar.Clone();
        wrongDigest[^1] ^= 1;
        var mismatchHandler = new FakeHandler(_ => Bytes(wrongDigest));
        result = await GameRuntimeHealth.VerifyAndRepairAsync(mismatch, "1.21", new HttpClient(mismatchHandler));
        Check(!result.CanLaunch && result.FailureReason!.Contains("mismatch", StringComparison.OrdinalIgnoreCase), "download digest mismatch blocks launch");

        string inherited = Path.Combine(root, "inherited");
        Profile(inherited, "base", ProfileJson("base", clientJar, ("fixture:base:1", "fixture/base/1/base-1.jar", libraryJar)));
        Write(Path.Combine(inherited, "versions", "base", "base.jar"), clientJar);
        Write(Path.Combine(inherited, "libraries", "fixture", "base", "1", "base-1.jar"), libraryJar);
        Profile(inherited, "forge-child", "{\"id\":\"forge-child\",\"inheritsFrom\":\"base\",\"libraries\":[]}");
        var inheritedHandler = new FakeHandler(_ => throw new Exception("inherited runtime requested the network"));
        result = await GameRuntimeHealth.VerifyAndRepairAsync(inherited, "forge-child", new HttpClient(inheritedHandler));
        Check(result.CanLaunch && inheritedHandler.Requests == 0, "inherited profile validates parent client and libraries");

        string legacy = Path.Combine(root, "legacy");
        Profile(legacy, "legacy", "{\"id\":\"legacy\"}");
        Write(Path.Combine(legacy, "versions", "legacy", "legacy.jar"), "not a zip"u8.ToArray());
        result = await GameRuntimeHealth.VerifyAndRepairAsync(legacy, "legacy", new HttpClient(new FakeHandler(_ => throw new Exception())));
        Check(!result.CanLaunch && result.FailedArtifact == "client:legacy" && result.FailureReason!.Contains("corrupt", StringComparison.OrdinalIgnoreCase), "legacy no-digest client must be a readable nonempty ZIP");

        string guards = Path.Combine(root, "guards");
        Profile(guards, "a", "{\"inheritsFrom\":\"b\"}");
        Profile(guards, "b", "{\"inheritsFrom\":\"a\"}");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(guards, "a", new HttpClient(new FakeHandler(_ => throw new Exception())));
        Check(!result.CanLaunch && result.FailureReason!.Contains("cycle", StringComparison.OrdinalIgnoreCase), "profile inheritance cycle is rejected");
        Profile(guards, "escape", "{\"inheritsFrom\":\"../../outside\"}");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(guards, "escape", new HttpClient(new FakeHandler(_ => throw new Exception())));
        Check(!result.CanLaunch && result.FailureReason!.Contains("identifier", StringComparison.OrdinalIgnoreCase), "profile inheritance traversal is rejected");
        Profile(guards, "broken", "{not-json");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(guards, "broken", new HttpClient(new FakeHandler(_ => throw new Exception())));
        Check(!result.CanLaunch && result.FailedArtifact == "profile:broken" && result.FailedPath!.EndsWith("broken.json") && result.FailureReason!.Contains("invalid", StringComparison.OrdinalIgnoreCase), "invalid profile reports its exact path and reason");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool cancellationObserved = false;
        try { await GameRuntimeHealth.VerifyAndRepairAsync(healthy, "1.21", new HttpClient(noRequest), cancelled.Token); }
        catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved, "runtime verification supports cancellation");

        return checks;
    }

    static string ProfileJson(string id, byte[] client, params (string Name, string Path, byte[] Bytes)[] libraries)
    {
        string libs = string.Join(",", libraries.Select(x => $"{{\"name\":\"{x.Name}\",\"downloads\":{{\"artifact\":{{\"path\":\"{x.Path}\",\"url\":\"https://fixture.invalid/{Path.GetFileName(x.Path)}\",\"size\":{x.Bytes.Length},\"sha1\":\"{Sha1(x.Bytes)}\"}}}}}}"));
        return $"{{\"id\":\"{id}\",\"downloads\":{{\"client\":{{\"url\":\"https://fixture.invalid/client.jar\",\"size\":{client.Length},\"sha1\":\"{Sha1(client)}\"}}}},\"libraries\":[{libs}]}}";
    }

    static void Profile(string root, string id, string json)
    {
        string directory = Path.Combine(root, "versions", id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, id + ".json"), json);
    }

    static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    static byte[] Jar(string text)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("fixture.txt").Open())) writer.Write(text);
        return memory.ToArray();
    }

    static string Sha1(byte[] bytes) => Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
    static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(response(request));
        }
    }

    sealed class ThrowingStream(byte[] bytes, int bytesBeforeFailure) : Stream
    {
        int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= bytesBeforeFailure) throw new IOException("fixture stream failed");
            int take = Math.Min(count, Math.Min(bytesBeforeFailure - position, bytes.Length - position));
            Array.Copy(bytes, position, buffer, offset, take); position += take; return take;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (position >= bytesBeforeFailure) return ValueTask.FromException<int>(new IOException("fixture stream failed"));
            int take = Math.Min(buffer.Length, Math.Min(bytesBeforeFailure - position, bytes.Length - position));
            bytes.AsMemory(position, take).CopyTo(buffer); position += take; return ValueTask.FromResult(take);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
