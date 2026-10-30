using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

internal sealed class ModAutoUpdater : IDisposable
{
    readonly string root;
    readonly Dictionary<string, string> snapshot = new(StringComparer.OrdinalIgnoreCase);
    readonly string stage;
    bool staged;
    bool retain;
    public int Count { get; private set; }
    public string? Backup { get; private set; }
    ModAutoUpdater(string directory, CancellationToken token)
    {
        root = Path.GetFullPath(directory);
        stage = Path.Combine(Path.GetTempPath(), "MistikModUpdates", Guid.NewGuid().ToString("N"));
        ModFiles.EnsureRoot(root);
        var files = ModFiles.List(root).ToArray();
        if (files.Length > 2048 || files.Sum(path => new FileInfo(path).Length) > 2L * 1024 * 1024 * 1024)
            throw new IOException("Mod update snapshot exceeds its safe size limit.");
        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested();
            ValidateFile(path);
            snapshot.Add(Path.GetFileName(path), Hash(path));
        }
        ModFiles.EnsureRoot(stage);
    }

    void StageSnapshot(CancellationToken token)
    {
        if (staged) return;
        foreach (var file in snapshot)
        {
            token.ThrowIfCancellationRequested();
            var source = Path.Combine(root, file.Key);
            ValidateFile(source);
            var destination = Path.Combine(stage, file.Key);
            File.Copy(source, destination);
            if (Hash(destination) != file.Value) throw new IOException("Mod changed while preparing its update.");
        }
        staged = true;
    }

    static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA512.HashData(stream)).ToLowerInvariant(); }
    static void ValidateFile(string path)
    {
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is <= 0 or > 128L * 1024 * 1024)
            throw new IOException("Invalid installed mod file.");
    }
    static bool Id(string? value) => value != null && Regex.IsMatch(value, "^[A-Za-z0-9]{8}$");
    static DateTimeOffset? Published(JToken version)
    {
        var value = version["date_published"];
        if (value?.Type == JTokenType.Date) return value is JValue { Value: DateTimeOffset offset } ? offset : new DateTimeOffset(value.Value<DateTime>().ToUniversalTime());
        return DateTimeOffset.TryParse((string?)value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    }

    public static async Task<ModAutoUpdater> PrepareAsync(HttpClient client, string directory, string game, string loader, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var update = new ModAutoUpdater(directory, token);
        try
        {
            // shortcut: automatic runtime dependency checks currently cover Fabric/Quilt; other loaders keep their existing mods.
            if (loader is not ("fabric" or "quilt")) return update;
            var active = update.snapshot.Where(file => ModFiles.Enabled(file.Key)).ToArray();
            var installed = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var byFile = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var batch in active.Chunk(100))
            {
                using var body = new StringContent(new JObject { ["algorithm"] = "sha512", ["hashes"] = new JArray(batch.Select(file => file.Value)) }.ToString(), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://api.modrinth.com/v2/version_files", body, token);
                response.EnsureSuccessStatusCode();
                var matches = JObject.Parse(await response.Content.ReadAsStringAsync(token));
                foreach (var file in batch)
                {
                    if (matches[file.Value] == null) continue; // Local and unpublished mods stay untouched.
                    if (matches[file.Value] is not JObject version || !Id((string?)version["project_id"]) || !Id((string?)version["id"]) ||
                        !(version["files"] as JArray ?? new()).Any(item => string.Equals((string?)item["hashes"]?["sha512"], file.Value, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Mod lookup does not match its installed hash.");
                    var project = (string)version["project_id"]!;
                    if (!installed.TryAdd(project, version)) throw new IOException("Duplicate installed mod project.");
                    byFile.Add(file.Key, version);
                }
            }
            var latest = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var batch in byFile.Keys.Chunk(100))
            {
                using var body = new StringContent(new JObject {
                    ["algorithm"] = "sha512", ["hashes"] = new JArray(batch.Select(name => update.snapshot[name])),
                    ["loaders"] = new JArray(loader), ["game_versions"] = new JArray(game), ["version_types"] = new JArray("release")
                }.ToString(), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://api.modrinth.com/v2/version_files/update", body, token);
                response.EnsureSuccessStatusCode();
                var matches = JObject.Parse(await response.Content.ReadAsStringAsync(token));
                foreach (var name in batch)
                {
                    var match = matches[update.snapshot[name]];
                    if (match == null) continue;
                    if (match is not JObject version || !Id((string?)version["id"]) ||
                        (string?)version["project_id"] != (string?)byFile[name]["project_id"])
                        throw new InvalidDataException("Mod update lookup does not match its installed project.");
                    latest[name] = version;
                }
            }
            var final = new Dictionary<string, JObject>(installed, StringComparer.Ordinal);
            foreach (var entry in byFile)
            {
                token.ThrowIfCancellationRequested();
                var project = (string)entry.Value["project_id"]!;
                if (!latest.TryGetValue(entry.Key, out var candidate) ||
                    (string?)candidate["id"] == (string?)entry.Value["id"] ||
                    (string?)candidate["version_type"] != "release" || !ModManagerPage.CheckCompatibility(candidate, game, loader) ||
                    Published(candidate) is not DateTimeOffset published || Published(entry.Value) is not DateTimeOffset current || published <= current) continue;
                var file = ModManagerPage.SelectModFile(candidate);
                var name = (string)file["filename"]!;
                if (!name.Equals(entry.Key, StringComparison.OrdinalIgnoreCase) &&
                    (update.snapshot.ContainsKey(name) || update.snapshot.ContainsKey(name + ".disabled") || File.Exists(Path.Combine(update.stage, name))))
                    throw new IOException("Updated mod filename conflicts with another installed file.");
                var bytes = await client.GetByteArrayAsync(ModFiles.DownloadUrl((string)file["url"]!), token);
                if (bytes.Length > 128 * 1024 * 1024 || !Convert.ToHexString(SHA512.HashData(bytes)).Equals((string?)file["hashes"]?["sha512"], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Mod update checksum mismatch.");
                var oldMetadata = ReadMetadata(Path.Combine(update.root, entry.Key));
                string candidatePath = Path.Combine(update.stage, ".candidate-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllBytes(candidatePath, bytes);
                var newMetadata = ReadMetadata(candidatePath);
                if (oldMetadata == null || newMetadata == null || (string?)oldMetadata["id"] != (string?)newMetadata["id"])
                    continue;
                if (Version.TryParse(((string?)oldMetadata["version"] ?? "").Split('+')[0], out var oldNumber) &&
                    Version.TryParse(((string?)newMetadata["version"] ?? "").Split('+')[0], out var newNumber) && newNumber < oldNumber) continue;
                if (newMetadata["depends"]?["minecraft"] is JToken minecraft && !Accepts(minecraft, game))
                    continue;
                // Never silently raise the required Java or loader version.
                if (new[] { "java", "fabricloader", "quilt_loader" }.Any(builtin =>
                    !JToken.DeepEquals(oldMetadata["depends"]?[builtin], newMetadata["depends"]?[builtin]))) continue;
                update.StageSnapshot(token);
                File.Delete(Path.Combine(update.stage, entry.Key));
                File.Move(candidatePath, Path.Combine(update.stage, name));
                final[project] = candidate;
                update.Count++;
            }
            if (update.Count > 0)
            {
                ValidateDependencies(final);
                ValidateRuntimeDependencies(update.root, update.stage);
                ModConflictGuard.CheckDirectory(update.stage);
            }
            return update;
        }
        catch { update.Dispose(); throw; }
    }

    internal static void ValidateDependencies(IReadOnlyDictionary<string, JObject> versions)
    {
        foreach (var version in versions.Values)
            foreach (var dependency in version["dependencies"] as JArray ?? new())
            {
                if ((string?)dependency["dependency_type"] != "required") continue;
                var project = (string?)dependency["project_id"];
                var pin = (string?)dependency["version_id"];
                var present = project != null && versions.TryGetValue(project, out var found) ? found :
                    pin != null ? versions.Values.FirstOrDefault(item => (string?)item["id"] == pin) : null;
                if (present == null || (pin != null && (string?)present["id"] != pin))
                    throw new IOException("Mod update requires a missing or differently pinned dependency.");
            }
    }

    static JObject? ReadMetadata(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("fabric.mod.json");
        if (entry == null || entry.Length > 256 * 1024 || zip.Entries.Any(item => item.FullName is "META-INF/mods.toml" or "META-INF/neoforge.mods.toml")) return null;
        using var input = entry.Open(); using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, 256 * 1024 + 1 - bytes.Length))) > 0)
        {
            bytes.Write(buffer, 0, read);
            if (bytes.Length > 256 * 1024) throw new InvalidDataException("Oversized mod metadata.");
        }
        if (bytes.Length > 256 * 1024) throw new InvalidDataException("Oversized mod metadata.");
        return JObject.Parse(Encoding.UTF8.GetString(bytes.ToArray()));
    }

    static Dictionary<string, JObject> Metadata(string directory)
    {
        var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var item in ModFiles.List(directory).Where(ModFiles.Enabled).Select(ReadMetadata).OfType<JObject>())
            foreach (var id in new[] { (string?)item["id"] }.Concat((item["provides"] as JArray ?? new()).Select(value => (string?)value)))
            {
                if (string.IsNullOrWhiteSpace(id) || result.ContainsKey(id)) throw new InvalidDataException("Duplicate or invalid Fabric mod identity.");
                result.Add(id, item);
            }
        return result;
    }

    internal static void ValidateRuntimeDependencies(string beforeDirectory, string afterDirectory)
    {
        var before = Metadata(beforeDirectory); var after = Metadata(afterDirectory);
        foreach (var mod in after)
            foreach (var dependency in mod.Value["depends"] as JObject ?? new())
            {
                if (dependency.Key is "minecraft" or "java" or "fabricloader" or "quilt_loader") continue;
                bool changedRequirement = !before.TryGetValue(mod.Key, out var previous) || !JToken.DeepEquals(previous["depends"]?[dependency.Key], dependency.Value);
                bool changedTarget = after.TryGetValue(dependency.Key, out var target) &&
                    (!before.TryGetValue(dependency.Key, out var oldTarget) || (string?)oldTarget["version"] != (string?)target["version"]);
                bool removedTarget = before.ContainsKey(dependency.Key) && target == null;
                if ((changedRequirement || changedTarget || removedTarget) && (target == null || !Accepts(dependency.Value, (string?)target["version"] ?? "")))
                    throw new IOException("Mod update cannot verify runtime dependency: " + dependency.Key);
            }
        foreach (var mod in after)
            foreach (var constraint in mod.Value["breaks"] as JObject ?? new())
                if (after.TryGetValue(constraint.Key, out var target) &&
                    (!before.TryGetValue(constraint.Key, out var oldTarget) || (string?)target["version"] != (string?)oldTarget["version"] ||
                     !before.TryGetValue(mod.Key, out var oldMod) || !JToken.DeepEquals(oldMod["breaks"]?[constraint.Key], constraint.Value)))
                    throw new IOException("An updated mod has a compatibility exclusion requiring manual review: " + constraint.Key);
    }

    internal static bool Accepts(JToken? predicate, string version)
    {
        if (predicate is JArray alternatives) return alternatives.Any(value => Accepts(value, version));
        if (predicate?.Type != JTokenType.String) return false;
        var text = predicate.Value<string>()!.Trim();
        if (text == "*") return true;
        if (text == version) return true;
        var constraints = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return constraints.Length > 0 && constraints.All(constraint =>
        {
            var match = Regex.Match(constraint, @"^(>=|<=|>|<|=)?(\d+(?:\.\d+){0,3})$");
            if (!match.Success || !Version.TryParse(version.Split('+')[0].Contains('.') ? version.Split('+')[0] : version + ".0", out var actual)) return false;
            var raw = match.Groups[2].Value;
            if (!Version.TryParse(raw.Contains('.') ? raw : raw + ".0", out var expected)) return false;
            Version Normalize(Version value) => new(value.Major, Math.Max(0, value.Minor), Math.Max(0, value.Build), Math.Max(0, value.Revision));
            int comparison = Normalize(actual).CompareTo(Normalize(expected));
            return match.Groups[1].Value switch { ">=" => comparison >= 0, "<=" => comparison <= 0, ">" => comparison > 0, "<" => comparison < 0, _ => comparison == 0 };
        });
    }

    public void Apply()
    {
        if (Count == 0) return;
        ModFiles.EnsureRoot(root);
        var current = ModFiles.List(root).ToArray();
        if (current.Length != snapshot.Count || current.Any(path => !snapshot.TryGetValue(Path.GetFileName(path), out var hash) || Hash(path) != hash))
            throw new IOException("Installed mods changed while checking updates; originals preserved.");
        foreach (var path in current) ValidateFile(path);
        Backup = Path.Combine(root, ".backups", "auto-update-" + Guid.NewGuid().ToString("N"));
        try { ModFiles.SyncPools(root, Backup, stage); }
        catch { retain = true; throw; }
    }

    public void Dispose()
    {
        if (retain || !Directory.Exists(stage)) return;
        try { Directory.Delete(stage, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { App.Log("Temporary mod update cleanup deferred: " + error.Message); }
    }
}
