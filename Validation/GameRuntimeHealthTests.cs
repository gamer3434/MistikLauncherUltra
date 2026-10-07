using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
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

        string blocked = Path.Combine(root, "blocked-directory");
        Profile(blocked, "1.21", ProfileJson("1.21", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(blocked, "versions", "1.21", "1.21.jar"), clientJar);
        Write(Path.Combine(blocked, "libraries", "fixture"), "preserve this file"u8.ToArray());
        result = await GameRuntimeHealth.VerifyAndRepairAsync(blocked, "1.21", new HttpClient(noRequest));
        Check(!result.CanLaunch && result.FailedArtifact == "library:fixture:lib:1" && result.FailureReason!.Contains("Repair failed") && File.ReadAllText(Path.Combine(blocked, "libraries", "fixture")) == "preserve this file", "repair directory failure reports the artifact and preserves existing files");

        byte[] dll = "native fixture"u8.ToArray();
        byte[] nativeJar = Jar(("lwjgl64.dll", dll), ("META-INF/fixture.txt", "excluded"u8.ToArray()));
        string nativeRoot = Path.Combine(root, "native-classifier");
        NativeProfile(nativeRoot, "base", clientJar, nativeJar);
        Write(Path.Combine(nativeRoot, "versions", "base", "base.jar"), clientJar);
        Profile(nativeRoot, "child", "{\"inheritsFrom\":\"base\",\"libraries\":[]}");
        var nativeHandler = new FakeHandler(_ => Bytes(nativeJar));
        string dllPath = Path.Combine(nativeRoot, "versions", "child", "natives", "lwjgl64.dll");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(nativeRoot, "child", new HttpClient(nativeHandler));
        Check(result.CanLaunch && nativeHandler.Requests == 1 && File.ReadAllBytes(dllPath).SequenceEqual(dll) && !Directory.Exists(Path.Combine(nativeRoot, "versions", "child", "natives", "META-INF")), "Windows classifier-only library downloads and extracts into selected inherited profile, respecting exclusions");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(nativeRoot, "child", new HttpClient(noRequest));
        Check(result.CanLaunch && result.RepairedCount == 0, "healthy native classifier and extracted DLL verify offline without rewriting files");
        File.WriteAllText(dllPath, "broken DLL");
        result = await GameRuntimeHealth.VerifyAndRepairAsync(nativeRoot, "child", new HttpClient(noRequest));
        Check(result.CanLaunch && result.RepairedCount == 1 && File.ReadAllBytes(dllPath).SequenceEqual(dll), "corrupt extracted native DLL repairs from verified local classifier archive");

        string modernNativeRoot = Path.Combine(root, "modern-native-artifact");
        Profile(modernNativeRoot, "base", ProfileJson("base", clientJar, ("org.lwjgl:lwjgl:3.3.3:natives-windows", "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar", nativeJar)));
        Write(Path.Combine(modernNativeRoot, "versions", "base", "base.jar"), clientJar);
        result = await GameRuntimeHealth.VerifyAndRepairAsync(modernNativeRoot, "base", new HttpClient(new FakeHandler(_ => Bytes(nativeJar))));
        string modernDll = Path.Combine(modernNativeRoot, "versions", "base", "natives", "lwjgl64.dll");
        Check(result.CanLaunch && File.ReadAllBytes(modernDll).SequenceEqual(dll) && !Directory.Exists(Path.Combine(Path.GetDirectoryName(modernDll)!, "META-INF")), "modern Windows native artifact is verified and extracted with metadata excluded");
        File.Delete(modernDll);
        result = await GameRuntimeHealth.VerifyAndRepairAsync(modernNativeRoot, "base", new HttpClient(noRequest));
        Check(result.CanLaunch && result.RepairedCount == 1 && File.ReadAllBytes(modernDll).SequenceEqual(dll), "missing modern native DLL repairs from the verified local archive offline");

        string invalidNativeRoot = Path.Combine(root, "native-digest-mismatch");
        NativeProfile(invalidNativeRoot, "base", clientJar, nativeJar);
        Write(Path.Combine(invalidNativeRoot, "versions", "base", "base.jar"), clientJar);
        string preservedDll = Path.Combine(invalidNativeRoot, "versions", "base", "natives", "lwjgl64.dll");
        Write(preservedDll, dll);
        result = await GameRuntimeHealth.VerifyAndRepairAsync(invalidNativeRoot, "base", new HttpClient(new FakeHandler(_ => Bytes(new byte[nativeJar.Length]))));
        Check(!result.CanLaunch && result.FailedArtifact == "native:fixture:natives:1" && File.ReadAllBytes(preservedDll).SequenceEqual(dll), "native archive digest mismatch blocks extraction and preserves previous DLL");

        byte[] traversalJar = Jar(("lwjgl64.dll", "replacement"u8.ToArray()), ("../outside.dll", dll));
        string traversalRoot = Path.Combine(root, "native-traversal");
        NativeProfile(traversalRoot, "base", clientJar, traversalJar);
        Write(Path.Combine(traversalRoot, "versions", "base", "base.jar"), clientJar);
        string existingDll = Path.Combine(traversalRoot, "versions", "base", "natives", "lwjgl64.dll");
        Write(existingDll, dll);
        result = await GameRuntimeHealth.VerifyAndRepairAsync(traversalRoot, "base", new HttpClient(new FakeHandler(_ => Bytes(traversalJar))));
        Check(!result.CanLaunch && result.FailedArtifact == "native:fixture:natives:1" && File.ReadAllBytes(existingDll).SequenceEqual(dll) && !File.Exists(Path.Combine(traversalRoot, "versions", "base", "outside.dll")), "native ZIP traversal is rejected before replacing any existing DLL");

        string stalledRoot = Path.Combine(root, "stalled-runtime");
        Profile(stalledRoot, "base", ProfileJson("base", clientJar, ("fixture:lib:1", "fixture/lib/1/lib-1.jar", libraryJar)));
        Write(Path.Combine(stalledRoot, "versions", "base", "base.jar"), clientJar);
        string stalledPath = Path.Combine(stalledRoot, "libraries", "fixture", "lib", "1", "lib-1.jar");
        Write(stalledPath, "preserve existing file"u8.ToArray());
        using var stalledClient = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) })) { Timeout = TimeSpan.FromMilliseconds(100) };
        var stalledRepair = GameRuntimeHealth.VerifyAndRepairAsync(stalledRoot, "base", stalledClient);
        Check(await Task.WhenAny(stalledRepair, Task.Delay(5000)) == stalledRepair, "stalled runtime response body respects the repair timeout");
        result = await stalledRepair;
        Check(!result.CanLaunch && result.FailedArtifact == "library:fixture:lib:1" && result.FailureReason!.Contains("timed out") && File.ReadAllText(stalledPath) == "preserve existing file" && !Directory.EnumerateFiles(Path.GetDirectoryName(stalledPath)!, ".*.mistik-*.tmp").Any(), "runtime timeout reports the artifact, preserves existing bytes and removes its temporary file");

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

    static void NativeProfile(string root, string id, byte[] client, byte[] natives)
    {
        var json = JObject.Parse(ProfileJson(id, client));
        json["libraries"] = new JArray(new JObject {
            ["name"] = "fixture:natives:1", ["natives"] = new JObject { ["windows"] = "natives-windows-${arch}" },
            ["downloads"] = new JObject { ["classifiers"] = new JObject { ["natives-windows-64"] = new JObject {
                ["path"] = "fixture/natives/1/natives-1-windows-64.jar", ["url"] = "https://fixture.invalid/natives.jar", ["size"] = natives.Length, ["sha1"] = Sha1(natives)
            } } }, ["extract"] = new JObject { ["exclude"] = new JArray("META-INF/") }
        });
        Profile(root, id, json.ToString());
    }

    static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    static byte[] Jar(string text) => Jar(("fixture.txt", System.Text.Encoding.UTF8.GetBytes(text)));
    static byte[] Jar(params (string Path, byte[] Bytes)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var entry in entries) { using var file = zip.CreateEntry(entry.Path).Open(); file.Write(entry.Bytes); }
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

    sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
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
