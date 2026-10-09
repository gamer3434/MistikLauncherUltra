using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class CurseForgeTests
{
    public static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        async Task<bool> Reject(Stub transport, string version = "1.20.1", string loader = "fabric", int id = 1)
        {
            using var client = new HttpClient(transport);
            using var service = new CurseForgeMods("fixture-api-key", client);
            try { await service.DownloadAsync(id, version, loader); return false; }
            catch (Exception error) when (error is IOException or InvalidDataException or TimeoutException) { return true; }
        }
        var basic = new Stub();
        using (var client = new HttpClient(basic))
        using (var service = new CurseForgeMods("fixture-api-key", client))
        {
            var hits = await service.SearchAsync("test & mod", "1.20.1", "fabric");
            Check(hits.Count == 1 && (string?)hits[0]["project_id"] == "1" && (string?)hits[0]["title"] == "Fixture Mod" &&
                (string?)hits[0]["description"] == "Fixture summary" && (long?)hits[0]["downloads"] == 123 &&
                (string?)hits[0]["source"] == "CurseForge" && (string?)hits[0]["website"] == "https://www.curseforge.com/minecraft/mc-mods/fixture-mod",
                "CurseForge search normalizes mod data and excludes other games");
            Check(basic.Requests[0].Url.Contains("gameId=432") && basic.Requests[0].Url.Contains("classId=6") && basic.Requests[0].Url.Contains("searchFilter=test%20%26%20mod") &&
                basic.Requests[0].Url.Contains("gameVersion=1.20.1") && basic.Requests[0].Url.Contains("modLoaderType=4"), "CurseForge search escapes its query and filters the exact game and loader");
            foreach (var item in new[] { ("forge", 1), ("quilt", 5), ("neoforge", 6) })
                await service.SearchAsync("test", "1.20.1", item.Item1);
            Check(basic.Requests.Skip(1).Select(request => request.Url.Split("modLoaderType=")[1].Split('&')[0]).SequenceEqual(new[] { "1", "5", "6" }), "CurseForge Forge Quilt and NeoForge API loader values match official metadata");
            var files = await service.DownloadAsync(1, "1.20.1", "fabric");
            Check(files.Count == 2 && files[0].Filename == "dependency.jar" && files[1].Filename == "fixture.jar" && files.All(file => file.Bytes.SequenceEqual(basic.Jar)), "CurseForge verifies the full required dependency bundle before returning files");
            Check(!basic.Requests.Any(request => request.Url.Contains("/mods/3")) && basic.Requests.Where(request => new Uri(request.Url).Host == "api.curseforge.com").All(request => request.HasKey) &&
                basic.Requests.Where(request => new Uri(request.Url).Host.EndsWith("forgecdn.net")).All(request => !request.HasKey), "CurseForge skips optional dependencies and sends the API key only to the API host");
        }
        var history = new Stub();
        int alternateId = 1000;
        JObject Alternate(int project, string filename)
        {
            var value = (JObject)history.Files[project].DeepClone(); value["id"] = alternateId++; value["fileName"] = filename; value["fileDate"] = "2025-01-01T00:00:00Z"; return value;
        }
        var oldRoot = Alternate(1, "fixture-old.jar"); var oldDependency = Alternate(2, "dependency-old.jar");
        var wrongLoader = Alternate(1, "fixture-forge.jar"); wrongLoader["gameVersions"] = new JArray("1.20.1", "Forge");
        var wrongVersion = Alternate(2, "dependency-other-version.jar"); wrongVersion["gameVersions"] = new JArray("1.20", "Fabric");
        var differentProject = Alternate(1, "other-project.jar"); differentProject["modId"] = 99;
        history.AlternateFiles[1] = new JArray(oldRoot, wrongLoader, differentProject, Alternate(1, "../unsafe.jar"), Alternate(1, "FIXTURE-OLD.JAR"));
        history.AlternateFiles[2] = new JArray(oldDependency, wrongVersion);
        using (var client = new HttpClient(history))
        using (var service = new CurseForgeMods("fixture-api-key", client))
        {
            var bundle = await service.DownloadAsync(1, "1.20.1", "fabric");
            Check(bundle.Single(file => file.Filename == "fixture.jar").CompatibleFilenames.SequenceEqual(new[] { "fixture.jar", "fixture-old.jar" }),
                "CurseForge carries validated same-project filenames so an old root mod can be rejected before installation");
            Check(bundle.Single(file => file.Filename == "dependency.jar").CompatibleFilenames.SequenceEqual(new[] { "dependency.jar", "dependency-old.jar" }),
                "CurseForge carries old required dependency filenames with exact Minecraft and loader matching");
            Check(bundle.SelectMany(file => file.CompatibleFilenames).All(filename => !filename.Contains('/') && !filename.Contains("forge") && !filename.Contains("other-version") && !filename.Contains("other-project")),
                "CurseForge omits unsafe alternate names and unrelated or incompatible file candidates");
        }
        var paged = new Stub();
        for (int project = 1; project <= 2; project++)
        {
            var alternatives = new JArray();
            for (int index = 0; index < 49; index++)
            {
                var candidate = (JObject)paged.Files[project].DeepClone(); candidate["id"] = project * 1000 + index;
                candidate["fileName"] = $"project-{project}-old-{index}.jar"; candidate["fileDate"] = "2025-01-01T00:00:00Z"; alternatives.Add(candidate);
            }
            var later = (JObject)paged.Files[project].DeepClone(); later["id"] = project * 1000 + 50;
            later["fileName"] = project == 1 ? "fixture-new.jar" : "dependency-old-page-two.jar";
            later["fileDate"] = project == 1 ? "2027-01-01T00:00:00Z" : "2024-01-01T00:00:00Z"; alternatives.Add(later);
            paged.AlternateFiles[project] = alternatives;
        }
        using (var client = new HttpClient(paged))
        using (var service = new CurseForgeMods("fixture-api-key", client))
        {
            var bundle = await service.DownloadAsync(1, "1.20.1", "fabric");
            Check(bundle.Single(file => file.Filename == "fixture-new.jar").CompatibleFilenames.Contains("fixture.jar") && paged.Requests.Any(request => request.Url.Contains("/mods/1/files?") && request.Url.EndsWith("index=50")),
                "CurseForge selects the newest compatible file across all official API pages");
            Check(bundle.Single(file => file.Filename == "dependency.jar").CompatibleFilenames.Contains("dependency-old-page-two.jar") && paged.Requests.Any(request => request.Url.Contains("/mods/2/files?") && request.Url.EndsWith("index=50")),
                "CurseForge carries old dependency conflict filenames from later API pages");
        }
        paged.Files[1]["isAvailable"] = false;
        foreach (var candidate in paged.AlternateFiles[1].Take(49)) candidate["isAvailable"] = false;
        using (var client = new HttpClient(paged))
        using (var service = new CurseForgeMods("fixture-api-key", client))
            Check((await service.DownloadAsync(1, "1.20.1", "fabric")).Any(file => file.Filename == "fixture-new.jar"),
                "CurseForge continues to later pages when the first page has no available compatible file");
        var yearBoundary = new Stub();
        var previousYear = (JObject)yearBoundary.Files[1].DeepClone(); previousYear["id"] = 100; previousYear["fileName"] = "previous-december.jar"; previousYear["fileDate"] = "2025-12-31T23:59:59Z";
        yearBoundary.AlternateFiles[1] = new JArray(previousYear);
        using (var client = new HttpClient(yearBoundary))
        using (var service = new CurseForgeMods("fixture-api-key", client))
            Check((await service.DownloadAsync(1, "1.20.1", "fabric")).Any(file => file.Filename == "fixture.jar"),
                "CurseForge compares parsed file dates chronologically across the December January boundary");
        var invalidPagination = new Stub { EmptyFilePage = true };
        Check(await Reject(invalidPagination) && !invalidPagination.AssetRequests, "CurseForge refuses incomplete zero-progress pagination before downloading mods");
        invalidPagination = new Stub { InvalidPageIndex = true };
        Check(await Reject(invalidPagination) && !invalidPagination.AssetRequests, "CurseForge refuses API file pages whose index does not match the request");
        invalidPagination = new Stub { FileTotalCount = 10001 };
        Check(await Reject(invalidPagination) && !invalidPagination.AssetRequests, "CurseForge refuses truncated history beyond the documented 10000-result API limit");
        var repeatedPage = new Stub { RepeatFilePage = true, FileTotalCount = 100 };
        repeatedPage.AlternateFiles[1] = new JArray(Enumerable.Range(1, 49).Select(index => { var candidate = (JObject)repeatedPage.Files[1].DeepClone(); candidate["id"] = 1000 + index; candidate["fileName"] = "repeat-" + index + ".jar"; return candidate; }));
        Check(await Reject(repeatedPage) && !repeatedPage.AssetRequests && repeatedPage.Requests.Count(request => request.Url.Contains("/files?")) == 2,
            "CurseForge detects repeated file pages instead of accepting an incomplete conflict history");
        var incompatibleBundle = new Stub(); incompatibleBundle.Files[1]["dependencies"]!.Last!.AddAfterSelf(new JObject { ["modId"] = 2, ["relationType"] = 5 });
        Check(await Reject(incompatibleBundle) && !incompatibleBundle.AssetRequests, "CurseForge refuses a required dependency explicitly incompatible with the selected mod");
        incompatibleBundle = new Stub(); incompatibleBundle.Files[2]["dependencies"] = new JArray(new JObject { ["modId"] = 1, ["relationType"] = 5 });
        Check(await Reject(incompatibleBundle) && !incompatibleBundle.AssetRequests, "CurseForge refuses dependency conflicts pointing back at an already resolved root mod");
        incompatibleBundle = new Stub(); incompatibleBundle.Files[1]["dependencies"]!.Last!.AddAfterSelf(new JObject { ["modId"] = 3, ["relationType"] = 5 });
        using (var client = new HttpClient(incompatibleBundle))
        using (var service = new CurseForgeMods("fixture-api-key", client))
            Check((await service.DownloadAsync(1, "1.20.1", "fabric")).Count == 2, "CurseForge does not install optional incompatible mods and preserves the valid required bundle");
        var incompatible = new Stub(); incompatible.Files[1]["gameVersions"] = new JArray("1.20", "Fabric");
        Check(await Reject(incompatible) && !incompatible.AssetRequests, "CurseForge refuses a neighboring Minecraft version despite API filters");
        incompatible = new Stub(); incompatible.Files[1]["gameVersions"] = new JArray("1.20.1", "Forge");
        Check(await Reject(incompatible) && !incompatible.AssetRequests, "CurseForge refuses a file for another mod loader");
        var wrongClass = new Stub(); wrongClass.Projects[1]["classId"] = 4471;
        Check(await Reject(wrongClass) && !wrongClass.AssetRequests, "CurseForge rejects modpacks instead of installing them as individual mods");
        var serverPack = new Stub(); serverPack.Files[1]["isServerPack"] = true;
        Check(await Reject(serverPack) && !serverPack.AssetRequests, "CurseForge excludes server-pack archives from client mod selection");
        var cyclic = new Stub(); cyclic.Files[2]["dependencies"] = new JArray(new JObject { ["modId"] = 1, ["relationType"] = 3 });
        using (var client = new HttpClient(cyclic))
        using (var service = new CurseForgeMods("fixture-api-key", client))
            Check((await service.DownloadAsync(1, "1.20.1", "fabric")).Count == 2 && cyclic.Requests.Count(request => new Uri(request.Url).Host.EndsWith("forgecdn.net")) == 2,
                "CurseForge cyclic required dependencies are resolved once and downloaded once");
        foreach (string host in new[] { "edge.forgecdn.net", "media.forgecdn.net" })
        {
            var officialCdn = new Stub(); officialCdn.Files[1]["downloadUrl"] = $"https://{host}/files/1/1/fixture.jar";
            using var client = new HttpClient(officialCdn); using var service = new CurseForgeMods("fixture-api-key", client);
            Check((await service.DownloadAsync(1, "1.20.1", "fabric")).Count == 2 && officialCdn.Requests.Where(request => new Uri(request.Url).Host.EndsWith("forgecdn.net")).All(request => !request.HasKey),
                "CurseForge accepts the official " + host + " CDN without sending the API key");
        }
        var badHash = new Stub(); badHash.Files[2]["hashes"]![0]!["value"] = new string('0', 40);
        Check(await Reject(badHash) && !badHash.Requests.Any(request => request.Url.EndsWith("fixture.jar")), "CurseForge bad required dependency checksum aborts the bundle before the main mod is downloaded");
        var noHash = new Stub(); noHash.Files[1]["hashes"] = new JArray(new JObject { ["algo"] = 2, ["value"] = new string('0', 32) });
        Check(await Reject(noHash) && !noHash.AssetRequests, "CurseForge requires the author-provided SHA1 rather than accepting only MD5");
        var distribution = new Stub(); distribution.Projects[1]["allowModDistribution"] = false;
        Check(await Reject(distribution) && distribution.Requests.Count == 1, "CurseForge respects authors who disable third-party distribution");
        var noUrl = new Stub(); noUrl.Files[1]["downloadUrl"] = JValue.CreateNull();
        Check(await Reject(noUrl) && !noUrl.AssetRequests, "CurseForge does not invent a CDN URL for distribution-restricted files");
        var unsafeUrl = new Stub(); unsafeUrl.Files[1]["downloadUrl"] = "https://mediafilez.forgecdn.net.attacker.invalid/fixture.jar";
        Check(await Reject(unsafeUrl) && !unsafeUrl.AssetRequests, "CurseForge rejects spoofed CDN hosts before sending any asset request");
        var unsafeName = new Stub(); unsafeName.Files[1]["fileName"] = "../escaped.jar";
        Check(await Reject(unsafeName) && !unsafeName.AssetRequests, "CurseForge rejects filename traversal before resolving or downloading dependencies");
        var redirect = new Stub { ApiRedirect = true };
        Check(await Reject(redirect) && redirect.Requests.Count == 1 && redirect.Requests[0].HasKey, "CurseForge refuses API redirects and never forwards the key to their destinations");
        redirect = new Stub { AssetRedirect = true };
        Check(await Reject(redirect) && redirect.Requests.All(request => new Uri(request.Url).Host != "attacker.invalid"), "CurseForge validates asset redirects before following them");
        var tooLarge = new Stub(); tooLarge.Files[1]["fileLength"] = 129L * 1024 * 1024;
        Check(await Reject(tooLarge) && !tooLarge.AssetRequests, "CurseForge refuses files beyond its 128MB limit before downloading");
        var bundleLimit = new Stub(); bundleLimit.Files[1]["fileLength"] = 128L * 1024 * 1024; bundleLimit.Files[2]["fileLength"] = 128L * 1024 * 1024;
        bundleLimit.Files[2]["dependencies"] = new JArray(new JObject { ["modId"] = 3, ["relationType"] = 3 });
        Check(await Reject(bundleLimit) && !bundleLimit.AssetRequests, "CurseForge enforces the 256MB limit across all required dependencies");
        var many = new Stub();
        for (int id = 1; id <= 33; id++) { many.Add(id, "fixture-" + id + ".jar"); many.Files[id]["dependencies"] = id == 33 ? new JArray() : new JArray(new JObject { ["modId"] = id + 1, ["relationType"] = 3 }); }
        Check(await Reject(many) && !many.AssetRequests, "CurseForge bounds recursive dependency resolution to 32 mods");
        var invalidJar = new Stub(Encoding.UTF8.GetBytes("not a JAR"));
        Check(await Reject(invalidJar), "CurseForge refuses invalid JAR bytes even when the declared digest matches");
        var stalledTransport = new Stub { StallAsset = true };
        using (var client = new HttpClient(stalledTransport) { Timeout = TimeSpan.FromMilliseconds(100) })
        using (var service = new CurseForgeMods("fixture-api-key", client))
        {
            var task = service.DownloadAsync(1, "1.20.1", "fabric");
            bool timedOut = false;
            Check(await Task.WhenAny(task, Task.Delay(5000)) == task, "CurseForge stalled asset body finishes within its bounded timeout");
            try { await task; } catch (TimeoutException) { timedOut = true; }
            Check(timedOut, "CurseForge stalled download returns a timeout rather than an incomplete bundle");
        }
        using (var client = new HttpClient(new Stub { StallAsset = true }))
        using (var service = new CurseForgeMods("fixture-api-key", client))
        using (var cancel = new CancellationTokenSource(100))
        {
            bool cancelled = false;
            try { await service.DownloadAsync(1, "1.20.1", "fabric", cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "CurseForge preserves caller cancellation during asset reads");
        }
        bool rejected = false; try { using var service = new CurseForgeMods(new string('a', 513)); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "CurseForge rejects oversized API keys without making requests");
        using (var client = new HttpClient(new Stub()))
        {
            client.DefaultRequestHeaders.Add("x-api-key", "fixture-api-key"); rejected = false;
            try { using var service = new CurseForgeMods("fixture-api-key", client); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "CurseForge refuses shared default API key headers that could leak to CDN requests");
        }
        return checks;
    }

    sealed class Stub : HttpMessageHandler
    {
        public byte[] Jar;
        public Dictionary<int, JObject> Projects = new();
        public Dictionary<int, JObject> Files = new();
        public Dictionary<int, JArray> AlternateFiles = new();
        public List<(string Url, bool HasKey)> Requests = new();
        public bool ApiRedirect, AssetRedirect, StallAsset;
        public bool EmptyFilePage, InvalidPageIndex, RepeatFilePage;
        public long? FileTotalCount;
        public bool AssetRequests => Requests.Any(request => new Uri(request.Url).Host.EndsWith("forgecdn.net"));
        public Stub(byte[]? jar = null)
        {
            if (jar == null)
            {
                using var memory = new MemoryStream();
                using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true)) { using var entry = archive.CreateEntry("fabric.mod.json").Open(); entry.Write(Encoding.UTF8.GetBytes("{}")); }
                jar = memory.ToArray();
            }
            Jar = jar; Add(1, "fixture.jar"); Add(2, "dependency.jar"); Add(3, "optional.jar");
            Files[1]["dependencies"] = new JArray(new JObject { ["modId"] = 2, ["relationType"] = 3 }, new JObject { ["modId"] = 3, ["relationType"] = 2 });
        }
        public void Add(int id, string filename)
        {
            Projects[id] = new JObject { ["id"] = id, ["gameId"] = 432, ["classId"] = 6, ["isAvailable"] = true, ["slug"] = "fixture-mod", ["name"] = "Fixture Mod", ["summary"] = "Fixture summary", ["downloadCount"] = 123, ["allowModDistribution"] = true };
            Files[id] = new JObject { ["id"] = id * 10, ["modId"] = id, ["gameId"] = 432, ["isAvailable"] = true, ["fileName"] = filename, ["fileLength"] = Jar.Length,
                ["downloadUrl"] = "https://mediafilez.forgecdn.net/files/1/1/" + filename, ["fileDate"] = "2026-01-01T00:00:00Z", ["gameVersions"] = new JArray("1.20.1", "Fabric"),
                ["hashes"] = new JArray(new JObject { ["algo"] = 1, ["value"] = Convert.ToHexString(SHA1.HashData(Jar)) }), ["dependencies"] = new JArray() };
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var uri = request.RequestUri!; Requests.Add((uri.AbsoluteUri, request.Headers.Contains("x-api-key")));
            if (uri.Host == "api.curseforge.com")
            {
                if (ApiRedirect) return Task.FromResult(Redirect());
                if (uri.AbsolutePath.EndsWith("/search")) return Task.FromResult(Json(new JArray(Projects[1].DeepClone(), new JObject { ["id"] = 99, ["gameId"] = 1 })));
                var parts = uri.AbsolutePath.Split('/'); int id = int.Parse(parts[3]);
                if (uri.AbsolutePath.EndsWith("/files"))
                {
                    var candidates = new JArray(Files[id].DeepClone());
                    if (AlternateFiles.TryGetValue(id, out var alternatives)) foreach (var alternative in alternatives) candidates.Add(alternative.DeepClone());
                    int index = int.Parse(uri.Query.TrimStart('?').Split('&').FirstOrDefault(value => value.StartsWith("index="))?.Split('=')[1] ?? "0");
                    var page = EmptyFilePage ? new JArray() : new JArray(candidates.Skip(RepeatFilePage ? 0 : index).Take(50).Select(value => value.DeepClone()));
                    var body = new JObject { ["data"] = page, ["pagination"] = new JObject { ["index"] = InvalidPageIndex ? index + 1 : index, ["pageSize"] = 50, ["resultCount"] = page.Count, ["totalCount"] = FileTotalCount ?? candidates.Count } };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString()) });
                }
                return Task.FromResult(Json(Projects[id].DeepClone()));
            }
            if (AssetRedirect) return Task.FromResult(Redirect());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = StallAsset ? new StreamContent(new StalledStream()) : new ByteArrayContent(Jar) });
        }
        static HttpResponseMessage Json(JToken value) => new(HttpStatusCode.OK) { Content = new StringContent(new JObject { ["data"] = value }.ToString()) };
        static HttpResponseMessage Redirect() { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri("https://attacker.invalid/file.jar"); return response; }
    }

    sealed class StalledStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { await Task.Delay(Timeout.Infinite, token); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
