using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public enum CatalogInstallState { Checking, Unknown, Available, Installed, Disabled }

public sealed class InstalledModCatalog
{
    readonly Dictionary<string, CatalogInstallState> projects = new(StringComparer.Ordinal);
    readonly Dictionary<string, CatalogInstallState> versions = new(StringComparer.Ordinal);
    bool complete;

    public CatalogInstallState State(string project, string? version = null) =>
        (version == null ? projects : versions).TryGetValue(version ?? project, out var state) ? state :
        complete ? CatalogInstallState.Available : CatalogInstallState.Unknown;

    internal static string ProfileDirectory(string gameRoot, string appRoot, string active, string selected, string synced)
    {
        if (!GameProfiles.SafeId(selected)) throw new InvalidDataException("Invalid selected profile.");
        string Pool(string profile) => GameProfiles.VersionPoolKey(GameProfiles.MinecraftVersion(gameRoot, profile), GameProfiles.Loader(gameRoot, profile));
        var selectedPool = Pool(selected);
        if (selectedPool == "vanilla") return "";
        return synced.Length > 0 && GameProfiles.SafeId(synced) && Pool(synced) == selectedPool
            ? active : Path.Combine(appRoot, "mods_pool", selectedPool);
    }

    public static async Task<InstalledModCatalog> ReadAsync(HttpClient client, string directory, CancellationToken token)
    {
        var result = new InstalledModCatalog();
        try
        {
            if (directory.Length == 0) { result.complete = true; return result; }
            if (!Directory.Exists(directory)) { result.complete = true; return result; }
            for (string? current = Path.GetFullPath(directory); current != null; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid mod directory.");
            var files = ModFiles.List(directory).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var hashes = new Dictionary<string, CatalogInstallState>(StringComparer.OrdinalIgnoreCase);
            var stamps = new List<(string Path, long Length, DateTime Written)>();
            foreach (var path in files)
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is <= 0 or > 128L * 1024 * 1024)
                    return result;
                var stamp = (path, info.Length, info.LastWriteTimeUtc);
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                var hash = Convert.ToHexString(await SHA512.HashDataAsync(file, token).ConfigureAwait(false)).ToLowerInvariant();
                stamps.Add(stamp);
                var state = ModFiles.Enabled(path) ? CatalogInstallState.Installed : CatalogInstallState.Disabled;
                if (!hashes.TryGetValue(hash, out var prior) || state == CatalogInstallState.Installed) hashes[hash] = state;
            }
            bool allMatched = true;
            foreach (var batch in hashes.Keys.Chunk(100))
            {
                using var body = new StringContent(new JObject { ["algorithm"] = "sha512", ["hashes"] = new JArray(batch) }.ToString(), System.Text.Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://api.modrinth.com/v2/version_files", body, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var metadata = JObject.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
                foreach (var hash in batch)
                {
                    var item = metadata[hash];
                    if (item == null) { allMatched = false; continue; }
                    if (item is not JObject version) throw new InvalidDataException("Invalid mod metadata.");
                    var projectId = version["project_id"]?.ToString();
                    var versionId = version["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(versionId) ||
                        !(version["files"] as JArray ?? new()).OfType<JObject>().Any(file => file["hashes"] is JObject proof && hash.Equals(proof["sha512"]?.ToString(), StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Mod metadata does not match the local file.");
                    var state = hashes[hash];
                    if (!result.projects.TryGetValue(projectId, out var prior) || state == CatalogInstallState.Installed) result.projects[projectId] = state;
                    if (!result.versions.TryGetValue(versionId, out prior) || state == CatalogInstallState.Installed) result.versions[versionId] = state;
                }
            }
            // A directory changed during the request must not publish stale installed labels.
            if (!files.SequenceEqual(ModFiles.List(directory).OrderBy(value => value, StringComparer.Ordinal)) ||
                stamps.Any(stamp => !File.Exists(stamp.Path) || new FileInfo(stamp.Path).Length != stamp.Length || File.GetLastWriteTimeUtc(stamp.Path) != stamp.Written))
                return new InstalledModCatalog();
            result.complete = allMatched;
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or Newtonsoft.Json.JsonException or OperationCanceledException)
        { App.Log("Installed mod status unavailable: " + ex.Message); return new InstalledModCatalog(); }
    }

    public static string Label(CatalogInstallState state) => Localization.T(state switch {
        CatalogInstallState.Installed => "modCatalogInstalled", CatalogInstallState.Disabled => "modCatalogDisabled",
        CatalogInstallState.Checking => "modCatalogChecking", CatalogInstallState.Unknown => "modCatalogUnknown", _ => "modInstall" });
}
