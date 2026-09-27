using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed record GameRuntimeHealthProgress(string Status, string Artifact, string Path);

public sealed record GameRuntimeHealthResult(
    bool CanLaunch,
    int RepairedCount,
    string Status,
    string? FailedArtifact = null,
    string? FailedPath = null,
    string? FailureReason = null);

public static class GameRuntimeHealth
{
    const int MaxProfileDepth = 16;
    const long MaxUnspecifiedDownloadBytes = 1024L * 1024 * 1024;
    static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static Task<GameRuntimeHealthResult> VerifyAndRepairAsync(
        string gameDirectory,
        string versionId,
        CancellationToken cancellationToken = default,
        IProgress<GameRuntimeHealthProgress>? progress = null) =>
        VerifyAndRepairAsync(gameDirectory, versionId, SharedHttp, cancellationToken, progress);

    public static async Task<GameRuntimeHealthResult> VerifyAndRepairAsync(
        string gameDirectory,
        string versionId,
        HttpClient httpClient,
        CancellationToken cancellationToken = default,
        IProgress<GameRuntimeHealthProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentNullException.ThrowIfNull(httpClient);
        cancellationToken.ThrowIfCancellationRequested();

        string root = Path.GetFullPath(gameDirectory);
        if (!GameProfiles.SafeId(versionId))
            return Failed(0, "profile:" + versionId, null, "Invalid version identifier.");

        var profiles = new List<ProfileSpec>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string currentId = versionId;
        for (int depth = 0; ; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth >= MaxProfileDepth)
                return Failed(0, "profile:" + currentId, ProfilePath(root, currentId), "Profile inheritance is too deep.");
            if (!GameProfiles.SafeId(currentId))
                return Failed(0, "profile:" + currentId, null, "Invalid inherited version identifier.");
            if (!seen.Add(currentId))
                return Failed(0, "profile:" + currentId, ProfilePath(root, currentId), "Profile inheritance cycle detected.");

            string path = ProfilePath(root, currentId);
            progress?.Report(new("Checking profile", "profile:" + currentId, path));
            JObject json;
            try
            {
                if (!File.Exists(path))
                    return Failed(0, "profile:" + currentId, path, "Version profile is missing.");
                json = JObject.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                return Failed(0, "profile:" + currentId, path, "Version profile is invalid: " + ex.Message);
            }

            profiles.Add(new(currentId, path, json));
            var parent = json["inheritsFrom"]?.Type == JTokenType.Null ? null : json["inheritsFrom"]?.ToString();
            if (string.IsNullOrWhiteSpace(parent)) break;
            if (!GameProfiles.SafeId(parent))
                return Failed(0, "profile:" + currentId, path, "Invalid inherited version identifier: " + parent);
            currentId = parent;
        }

        int repaired = 0;
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var client = profile.Json["downloads"]?["client"] as JObject;
            bool terminal = string.IsNullOrWhiteSpace(profile.Json["inheritsFrom"]?.ToString());
            string jarPath = Path.Combine(root, "versions", profile.Id, profile.Id + ".jar");
            if (client != null)
            {
                var specResult = CreateArtifact("client:" + profile.Id, jarPath, client);
                if (specResult.Error != null)
                    return Failed(repaired, "client:" + profile.Id, jarPath, specResult.Error);
                var outcome = await EnsureArtifactAsync(specResult.Spec!, httpClient, true, cancellationToken, progress).ConfigureAwait(false);
                if (!outcome.Success) return Failed(repaired, outcome.Artifact, outcome.Path, outcome.Reason!);
                if (outcome.Repaired) repaired++;
            }
            else if (terminal || File.Exists(jarPath))
            {
                var legacy = new ArtifactSpec("client:" + profile.Id, jarPath, null, null, null);
                var outcome = await EnsureArtifactAsync(legacy, httpClient, true, cancellationToken, progress).ConfigureAwait(false);
                if (!outcome.Success) return Failed(repaired, outcome.Artifact, outcome.Path, outcome.Reason!);
                if (outcome.Repaired) repaired++;
            }
        }

        var libraries = new Dictionary<string, ArtifactSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles.AsEnumerable().Reverse())
        {
            if (profile.Json["libraries"] is not JArray entries) continue;
            foreach (var token in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (token is not JObject library)
                    return Failed(repaired, "library", profile.Path, "Library metadata is invalid.");
                if (!AppliesToWindows(library)) continue;

                string name = library["name"]?.ToString() ?? "library";
                var artifact = library["downloads"]?["artifact"] as JObject;
                string? relativePath = artifact?["path"]?.ToString();
                string? url = artifact?["url"]?.ToString();
                long? size = ReadSize(artifact?["size"]);
                string? sha1 = artifact?["sha1"]?.ToString();

                if (artifact?["size"] != null && size == null)
                    return Failed(repaired, "library:" + name, profile.Path, "Library size metadata is invalid.");
                if (!string.IsNullOrWhiteSpace(sha1) && !IsSha1(sha1))
                    return Failed(repaired, "library:" + name, profile.Path, "Library SHA-1 metadata is invalid.");

                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    if (!TryMavenArtifact(name, out relativePath))
                        return Failed(repaired, "library:" + name, profile.Path, "Library path metadata is invalid.");
                    string baseUrl = library["url"]?.ToString() ?? "https://libraries.minecraft.net/";
                    url = baseUrl.TrimEnd('/') + "/" + relativePath;
                }

                if (!TryChildPath(Path.Combine(root, "libraries"), relativePath!, out string fullPath))
                    return Failed(repaired, "library:" + name, relativePath, "Library path escapes the libraries directory.");

                var created = CreateArtifact("library:" + name, fullPath, url, size, sha1);
                if (created.Error != null)
                    return Failed(repaired, "library:" + name, fullPath, created.Error);
                libraries[fullPath] = created.Spec!;
            }
        }

        foreach (var library in libraries.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await EnsureArtifactAsync(library, httpClient, false, cancellationToken, progress).ConfigureAwait(false);
            if (!outcome.Success) return Failed(repaired, outcome.Artifact, outcome.Path, outcome.Reason!);
            if (outcome.Repaired) repaired++;
        }

        return new(true, repaired, repaired == 0 ? "Runtime files are healthy." : $"Runtime repaired ({repaired} file(s)).");
    }

    static async Task<ArtifactOutcome> EnsureArtifactAsync(
        ArtifactSpec artifact,
        HttpClient httpClient,
        bool requireZipWithoutDigest,
        CancellationToken cancellationToken,
        IProgress<GameRuntimeHealthProgress>? progress)
    {
        var healthy = await ValidateFileAsync(artifact, requireZipWithoutDigest, cancellationToken).ConfigureAwait(false);
        if (healthy == null)
        {
            progress?.Report(new("Verified", artifact.Name, artifact.Path));
            return ArtifactOutcome.Ok(artifact, false);
        }

        if (artifact.Url == null)
            return ArtifactOutcome.Fail(artifact, healthy + " No secure repair URL is available.");

        Directory.CreateDirectory(Path.GetDirectoryName(artifact.Path)!);
        string temp = Path.Combine(Path.GetDirectoryName(artifact.Path)!, "." + Path.GetFileName(artifact.Path) + ".mistik-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            progress?.Report(new("Repairing", artifact.Name, artifact.Path));
            using var response = await httpClient.GetAsync(artifact.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                    long limit = artifact.Size ?? MaxUnspecifiedDownloadBytes;
                    if (total > limit) throw new InvalidDataException("Download exceeds the expected size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var downloadedError = await ValidateFileAsync(artifact with { Path = temp }, requireZipWithoutDigest, cancellationToken).ConfigureAwait(false);
            if (downloadedError != null) throw new InvalidDataException(downloadedError);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temp, artifact.Path, true);
            progress?.Report(new("Repaired", artifact.Name, artifact.Path));
            return ArtifactOutcome.Ok(artifact, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return ArtifactOutcome.Fail(artifact, "Repair failed: " + ex.Message);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    static async Task<string?> ValidateFileAsync(ArtifactSpec artifact, bool requireZipWithoutDigest, CancellationToken cancellationToken)
    {
        if (!File.Exists(artifact.Path)) return "File is missing.";
        try
        {
            var info = new FileInfo(artifact.Path);
            if (artifact.Size.HasValue && info.Length != artifact.Size.Value)
                return $"Size mismatch (expected {artifact.Size.Value}, found {info.Length}).";
            if (info.Length == 0) return "File is empty.";
            if (artifact.Sha1 != null)
            {
                string actual = await Sha1Async(artifact.Path, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, artifact.Sha1, StringComparison.OrdinalIgnoreCase)) return "SHA-1 mismatch.";
            }
            else if (requireZipWithoutDigest || artifact.Path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(artifact.Path);
                _ = archive.Entries.Count;
            }
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return "File is unreadable or corrupt: " + ex.Message;
        }
    }

    static async Task<string> Sha1Async(string path, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    static (ArtifactSpec? Spec, string? Error) CreateArtifact(string name, string path, JObject metadata)
    {
        long? size = ReadSize(metadata["size"]);
        if (metadata["size"] != null && size == null) return (null, "Artifact size metadata is invalid.");
        string? sha1 = metadata["sha1"]?.ToString();
        if (!string.IsNullOrWhiteSpace(sha1) && !IsSha1(sha1)) return (null, "Artifact SHA-1 metadata is invalid.");
        return CreateArtifact(name, path, metadata["url"]?.ToString(), size, sha1);
    }

    static (ArtifactSpec? Spec, string? Error) CreateArtifact(string name, string path, string? url, long? size, string? sha1)
    {
        Uri? uri = null;
        if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps))
            return (null, "Artifact download URL must use HTTPS.");
        return (new(name, path, uri, size, string.IsNullOrWhiteSpace(sha1) ? null : sha1.ToLowerInvariant()), null);
    }

    static long? ReadSize(JToken? token) => token == null ? null : token.Type == JTokenType.Integer && token.Value<long>() >= 0 ? token.Value<long>() : null;
    static bool IsSha1(string value) => Regex.IsMatch(value, "^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);
    static string ProfilePath(string root, string id) => Path.Combine(root, "versions", id, id + ".json");

    static bool TryChildPath(string root, string relativePath, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':')) return false;
        try
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static bool TryMavenArtifact(string coordinate, out string path)
    {
        path = "";
        var parts = coordinate.Split(':');
        if (parts.Length is < 3 or > 4) return false;
        string versionAndExtension = parts[2];
        string extension = "jar";
        int at = versionAndExtension.LastIndexOf('@');
        if (at >= 0)
        {
            extension = versionAndExtension[(at + 1)..];
            versionAndExtension = versionAndExtension[..at];
        }
        if (new[] { parts[0], parts[1], versionAndExtension, extension }.Any(x => !Regex.IsMatch(x, "^[A-Za-z0-9_.+\\-]+$", RegexOptions.CultureInvariant))) return false;
        string classifier = parts.Length == 4 ? "-" + parts[3] : "";
        if (parts.Length == 4 && !Regex.IsMatch(parts[3], "^[A-Za-z0-9_.+\\-]+$", RegexOptions.CultureInvariant)) return false;
        path = parts[0].Replace('.', '/') + "/" + parts[1] + "/" + versionAndExtension + "/" + parts[1] + "-" + versionAndExtension + classifier + "." + extension;
        return true;
    }

    static bool AppliesToWindows(JObject library)
    {
        if (library["rules"] is not JArray rules || rules.Count == 0) return true;
        bool allowed = false;
        foreach (var token in rules)
        {
            if (token is not JObject rule || !RuleMatchesWindows(rule)) continue;
            allowed = string.Equals(rule["action"]?.ToString(), "allow", StringComparison.OrdinalIgnoreCase);
        }
        return allowed;
    }

    static bool RuleMatchesWindows(JObject rule)
    {
        if (rule["features"] is JObject features && features.Properties().Any(p => p.Value.Type == JTokenType.Boolean && p.Value.Value<bool>())) return false;
        if (rule["os"] is not JObject os) return true;
        string? name = os["name"]?.ToString();
        if (!string.IsNullOrWhiteSpace(name) && !name.Equals("windows", StringComparison.OrdinalIgnoreCase)) return false;
        string? arch = os["arch"]?.ToString();
        if (!string.IsNullOrWhiteSpace(arch) && arch is not ("x86_64" or "amd64")) return false;
        string? version = os["version"]?.ToString();
        if (!string.IsNullOrWhiteSpace(version))
        {
            try { if (!Regex.IsMatch(Environment.OSVersion.VersionString, version, RegexOptions.CultureInvariant)) return false; }
            catch (ArgumentException) { return false; }
        }
        return true;
    }

    static GameRuntimeHealthResult Failed(int repaired, string artifact, string? path, string reason) =>
        new(false, repaired, "Runtime verification failed.", artifact, path, reason);

    sealed record ProfileSpec(string Id, string Path, JObject Json);
    sealed record ArtifactSpec(string Name, string Path, Uri? Url, long? Size, string? Sha1);
    sealed record ArtifactOutcome(bool Success, bool Repaired, string Artifact, string Path, string? Reason)
    {
        public static ArtifactOutcome Ok(ArtifactSpec artifact, bool repaired) => new(true, repaired, artifact.Name, artifact.Path, null);
        public static ArtifactOutcome Fail(ArtifactSpec artifact, string reason) => new(false, false, artifact.Name, artifact.Path, reason);
    }
}
