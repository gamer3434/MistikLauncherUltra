using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed record CurseForgeModFile(string Filename, byte[] Bytes)
{
    public IReadOnlyList<string> CompatibleFilenames { get; init; } = Array.Empty<string>();
}

public sealed class CurseForgeMods : IDisposable
{
    const long FileLimit = 128L * 1024 * 1024, BundleLimit = 256L * 1024 * 1024;
    readonly string _apiKey;
    readonly HttpClient _client;
    readonly bool _ownsClient;

    public CurseForgeMods(string apiKey, HttpClient? client = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 512 || apiKey.Any(character => character is < ' ' or > '~'))
            throw new ArgumentException("A CurseForge API key is required.", nameof(apiKey));
        if (client?.DefaultRequestHeaders.Contains("x-api-key") == true)
            throw new ArgumentException("The CurseForge API key must not be a shared default header.", nameof(client));
        _apiKey = apiKey.Trim();
        _ownsClient = client == null;
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<JArray> SearchAsync(string query, string gameVersion, string loader, CancellationToken token = default)
    {
        int loaderType = LoaderType(loader);
        ValidateVersion(gameVersion);
        string url = $"mods/search?gameId=432&classId=6&searchFilter={Uri.EscapeDataString(query ?? "")}&gameVersion={Uri.EscapeDataString(gameVersion)}&modLoaderType={loaderType}&pageSize=40&sortField=2&sortOrder=desc";
        var data = (await MetadataAsync(url, token))["data"] as JArray ?? throw new InvalidDataException("Invalid CurseForge search response.");
        var hits = new JArray();
        foreach (var project in data.OfType<JObject>())
        {
            if ((int?)project["id"] is not int id || id <= 0 || (int?)project["gameId"] != 432 ||
                project["classId"] != null && (int?)project["classId"] != 6) continue;
            string website = Website(project);
            hits.Add(new JObject {
                ["project_id"] = id.ToString(CultureInfo.InvariantCulture), ["title"] = (string?)project["name"] ?? "",
                ["description"] = (string?)project["summary"] ?? "", ["downloads"] = (long?)project["downloadCount"] ?? 0,
                ["source"] = "CurseForge", ["website"] = website
            });
        }
        return hits;
    }

    public async Task<IReadOnlyList<CurseForgeModFile>> DownloadAsync(int modId, string gameVersion, string loader, CancellationToken token = default)
    {
        if (modId <= 0) throw new ArgumentOutOfRangeException(nameof(modId));
        int loaderType = LoaderType(loader);
        ValidateVersion(gameVersion);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(RequestBudget(TimeSpan.FromMinutes(3)));
        var files = new List<CurseForgeModFile>();
        var resolved = new HashSet<int>();
        var incompatible = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        try { await ResolveAsync(modId); return files; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("CurseForge download timed out."); }

        async Task ResolveAsync(int id)
        {
            if (incompatible.Contains(id)) throw new InvalidDataException("CurseForge dependency bundle contains incompatible mods.");
            if (!resolved.Add(id)) return;
            if (resolved.Count > 32) throw new InvalidDataException("CurseForge dependency bundle exceeds 32 mods.");
            var project = (await MetadataAsync($"mods/{id}", budget.Token))["data"] as JObject ?? throw new InvalidDataException("Invalid CurseForge mod response.");
            if ((int?)project["id"] != id || (int?)project["gameId"] != 432 ||
                project["classId"] != null && (int?)project["classId"] != 6 || (bool?)project["isAvailable"] != true)
                throw new InvalidDataException("CurseForge mod is unavailable or belongs to a different game.");
            string website = Website(project);
            if ((bool?)project["allowModDistribution"] == false) throw new IOException("The author disabled third-party downloads. Open " + website);
            var candidates = await FileCandidatesAsync(id, gameVersion, loaderType, budget.Token);
            var compatible = candidates.Where(candidate =>
                (int?)candidate["modId"] == id && (int?)candidate["gameId"] == 432 && (bool?)candidate["isAvailable"] == true && (bool?)candidate["isServerPack"] != true &&
                candidate["gameVersions"] is JArray versions && versions.Values<string>().Contains(gameVersion, StringComparer.Ordinal) && versions.Values<string>().Contains(loader, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var file = compatible
                .OrderByDescending(candidate => (DateTimeOffset?)candidate["fileDate"]).ThenByDescending(candidate => (int?)candidate["id"])
                .FirstOrDefault() ?? throw new IOException("No CurseForge file matches the selected Minecraft version and loader.");
            string filename = (string?)file["fileName"] ?? "";
            if (!SafeFilename(filename) || !names.Add(filename))
                throw new InvalidDataException("Unsafe or conflicting CurseForge mod filename.");
            long size = (long?)file["fileLength"] ?? 0;
            if (size <= 0 || size > FileLimit || size > BundleLimit - total) throw new InvalidDataException("CurseForge mod bundle exceeds its size limit.");
            total += size;
            string hash = file["hashes"]?.OfType<JObject>().FirstOrDefault(item => (int?)item["algo"] == 1)?["value"]?.Value<string>() ?? "";
            if (hash.Length != 40 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("A valid CurseForge SHA1 is required.");
            string? download = (string?)file["downloadUrl"];
            if (string.IsNullOrWhiteSpace(download)) throw new IOException("The author does not allow this file to be downloaded here. Open " + website);
            Uri asset = AssetUri(download);
            if (file["dependencies"] is JArray dependencies)
            {
                foreach (var conflict in dependencies.OfType<JObject>().Where(item => (int?)item["relationType"] == 5))
                {
                    int other = (int?)conflict["modId"] ?? 0;
                    if (other <= 0 || resolved.Contains(other)) throw new InvalidDataException("CurseForge dependency bundle contains incompatible mods.");
                    incompatible.Add(other);
                }
                foreach (var dependency in dependencies.OfType<JObject>().Where(item => (int?)item["relationType"] == 3))
                {
                    int required = (int?)dependency["modId"] ?? 0;
                    if (required <= 0) throw new InvalidDataException("Invalid CurseForge required dependency.");
                    await ResolveAsync(required);
                }
            }
            using var response = await AssetResponseAsync(asset, budget.Token);
            using var memory = new MemoryStream();
            if (await CopyBoundedAsync(response, memory, size, budget.Token) != size) throw new InvalidDataException("Incomplete CurseForge mod download.");
            byte[] bytes = memory.ToArray();
            if (!Convert.ToHexString(SHA1.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("CurseForge mod checksum mismatch.");
            ValidateJar(bytes);
            files.Add(new CurseForgeModFile(filename, bytes) {
                CompatibleFilenames = new[] { filename }.Concat(compatible.Select(candidate => (string?)candidate["fileName"] ?? ""))
                    .Where(SafeFilename).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }
    }

    static bool SafeFilename(string filename) => !string.IsNullOrWhiteSpace(filename) && filename == Path.GetFileName(filename) &&
        filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !filename.EndsWith(' ') && !filename.EndsWith('.') && filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);

    async Task<IReadOnlyList<JObject>> FileCandidatesAsync(int modId, string gameVersion, int loaderType, CancellationToken token)
    {
        var files = new List<JObject>();
        var seen = new HashSet<int>();
        for (int index = 0; index < 10000;)
        {
            var response = await MetadataAsync($"mods/{modId}/files?gameVersion={Uri.EscapeDataString(gameVersion)}&modLoaderType={loaderType}&pageSize=50&index={index}", token);
            var page = response["data"] as JArray ?? throw new InvalidDataException("Invalid CurseForge file response.");
            if (page.Count > 50) throw new InvalidDataException("CurseForge file page exceeds its limit.");
            int next = index + page.Count;
            long? total = null;
            if (response["pagination"] is JObject pagination)
            {
                total = (long?)pagination["totalCount"];
                if ((int?)pagination["index"] != index || (int?)pagination["resultCount"] != page.Count ||
                    (int?)pagination["pageSize"] is not int pageSize || pageSize is < 1 or > 50 || page.Count > pageSize ||
                    total is null || total < next || total > 10000 || page.Count == 0 && total > index)
                    throw new InvalidDataException("Incomplete or invalid CurseForge file pagination.");
            }
            int added = 0;
            foreach (var value in page)
            {
                if (value is not JObject file || (int?)file["id"] is not int id || id <= 0) throw new InvalidDataException("Invalid CurseForge file metadata.");
                if (seen.Add(id)) { files.Add(file); added++; }
            }
            if (page.Count > 0 && added == 0) throw new InvalidDataException("CurseForge file pagination made no progress.");
            if (total == next || total == null && page.Count < 50) return files;
            index = next;
        }
        throw new InvalidDataException("CurseForge file history exceeds the API pagination limit.");
    }

    static int LoaderType(string loader) => loader?.ToLowerInvariant() switch
    { "forge" => 1, "fabric" => 4, "quilt" => 5, "neoforge" => 6, _ => throw new ArgumentException("Select a supported mod loader.", nameof(loader)) };

    static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 80 || version.Any(char.IsControl)) throw new ArgumentException("Select a Minecraft version.", nameof(version));
    }

    static string Website(JObject project)
    {
        string slug = (string?)project["slug"] ?? "";
        if (slug.Length == 0 || slug.Length > 200 || slug.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidDataException("Invalid CurseForge project URL.");
        return "https://www.curseforge.com/minecraft/mc-mods/" + slug;
    }

    async Task<JObject> MetadataAsync(string relative, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestBudget(TimeSpan.FromSeconds(30)));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.curseforge.com/v1/" + relative);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400 || response.RequestMessage?.RequestUri is Uri final && final.Host != "api.curseforge.com")
                throw new InvalidDataException("CurseForge API redirects are not allowed.");
            response.EnsureSuccessStatusCode();
            using var memory = new MemoryStream();
            await CopyBoundedAsync(response, memory, 4 * 1024 * 1024, timeout.Token);
            return JObject.Parse(Encoding.UTF8.GetString(memory.ToArray()));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("CurseForge metadata request timed out."); }
    }

    static Uri AssetUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            !(uri.Host.Equals("mediafilez.forgecdn.net", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("media.forgecdn.net", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("edge.forgecdn.net", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Untrusted CurseForge download URL.");
        return uri;
    }

    TimeSpan RequestBudget(TimeSpan maximum) => _client.Timeout != Timeout.InfiniteTimeSpan && _client.Timeout < maximum ? _client.Timeout : maximum;

    async Task<HttpResponseMessage> AssetResponseAsync(Uri uri, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (uri.Host.Equals("edge.forgecdn.net", StringComparison.OrdinalIgnoreCase)) request.Headers.Add("x-api-key", _apiKey);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            try
            {
                if (response.RequestMessage?.RequestUri is Uri final) AssetUri(final.AbsoluteUri);
                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    Uri next = response.Headers.Location ?? throw new InvalidDataException("Missing CurseForge CDN redirect.");
                    uri = AssetUri((next.IsAbsoluteUri ? next : new Uri(uri, next)).AbsoluteUri);
                    response.Dispose(); continue;
                }
                response.EnsureSuccessStatusCode(); return response;
            }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many CurseForge CDN redirects.");
    }

    static async Task<long> CopyBoundedAsync(HttpResponseMessage response, Stream destination, long limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("CurseForge response exceeds its expected size.");
        using var stream = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0)
        {
            total += read;
            if (total > limit) throw new InvalidDataException("CurseForge response exceeds its expected size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return total;
    }

    static void ValidateJar(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, false);
        using var jar = new ZipArchive(memory, ZipArchiveMode.Read);
        if (jar.Entries.Count == 0 || jar.Entries.Count > 100000 || !jar.Entries.Any(entry => entry.FullName.EndsWith(".class", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName is "fabric.mod.json" or "quilt.mod.json" or "META-INF/mods.toml" or "META-INF/neoforge.mods.toml" or "META-INF/MANIFEST.MF"))
            throw new InvalidDataException("The CurseForge download is not a mod JAR.");
    }

    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
