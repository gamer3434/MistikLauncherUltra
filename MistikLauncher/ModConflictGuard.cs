using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public sealed class ModConflictException(string message) : IOException(message);

public static class ModConflictGuard
{
    const int MetadataLimit = 256 * 1024;
    sealed record Metadata(string Filename, HashSet<string> Ids, HashSet<string> Breaks);

    public static void CheckDirectory(string root)
    {
        if (!Directory.Exists(root)) return;
        var mods = ReadDirectory(root, null);
        for (var i = 0; i < mods.Count; i++)
            for (var j = i + 1; j < mods.Count; j++) CheckPair(mods[i], mods[j]);
    }

    public static void CheckInstall(string root, string filename, Stream candidate)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new IOException(Localization.T("modToggleInvalid"));
        ModFiles.EnsureRoot(root);
        long? position = candidate.CanSeek ? candidate.Position : null;
        try {
            var incoming = Read(candidate, filename);
            if (incoming == null) return;
            foreach (var existing in ReadDirectory(root, filename)) CheckPair(incoming, existing);
        } finally { if (position.HasValue) candidate.Position = position.Value; }
    }

    static List<Metadata> ReadDirectory(string root, string? replacing)
    {
        ModFiles.EnsureRoot(root);
        var result = new List<Metadata>();
        foreach (var file in ModFiles.List(root).Where(ModFiles.Enabled)) {
            if (Path.GetFileName(file).Equals(replacing, StringComparison.OrdinalIgnoreCase)) continue;
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(Localization.T("modToggleInvalid"));
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var metadata = Read(input, Path.GetFileName(file));
            if (metadata != null) result.Add(metadata);
        }
        return result;
    }

    static bool ValidId(string value) => Regex.IsMatch(value, "^[a-z][a-z0-9_-]{1,63}$");
    static Metadata? Read(Stream input, string filename)
    {
        bool recognized = false;
        try {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            // shortcut: hybrid jars need loader-specific resolution; Fabric rules must not reject Forge installs.
            if (zip.Entries.Any(entry => entry.FullName is "META-INF/mods.toml" or "META-INF/neoforge.mods.toml")) return null;
            var entries = zip.Entries.Where(entry => entry.FullName == "fabric.mod.json").ToArray();
            if (entries.Length == 0) return null;
            recognized = true;
            if (entries.Length != 1 || entries[0].Length > MetadataLimit) throw new InvalidDataException();
            using var source = entries[0].Open();
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, MetadataLimit + 1 - bounded.Length))) != 0) {
                bounded.Write(buffer, 0, read);
                if (bounded.Length > MetadataLimit) throw new InvalidDataException();
            }
            var text = new UTF8Encoding(false, true).GetString(bounded.ToArray());
            var data = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (data["id"]?.Type != JTokenType.String || !ValidId((string)data["id"]!)) throw new InvalidDataException();
            if (data["environment"] != null) {
                if (data["environment"]?.Type != JTokenType.String || (string?)data["environment"] is not ("*" or "client" or "server")) throw new InvalidDataException();
                if ((string?)data["environment"] == "server") return null;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal) { (string)data["id"]! };
            if (data["provides"] != null) {
                if (data["provides"] is not JArray aliases) throw new InvalidDataException();
                foreach (var alias in aliases) {
                    if (alias.Type != JTokenType.String || !ValidId((string)alias!)) throw new InvalidDataException();
                    ids.Add((string)alias!);
                }
            }
            var breaks = new HashSet<string>(StringComparer.Ordinal);
            if (data["breaks"] != null) {
                if (data["breaks"] is not JObject constraints) throw new InvalidDataException();
                foreach (var constraint in constraints.Properties()) {
                    if (!ValidId(constraint.Name)) throw new InvalidDataException();
                    IEnumerable<JToken> alternatives = constraint.Value is JArray values ? values : new[] { constraint.Value };
                    foreach (var value in alternatives) {
                        if (value.Type != JTokenType.String) throw new InvalidDataException();
                        if (((string)value!).Trim() == "*") breaks.Add(constraint.Name);
                    }
                }
            }
            // shortcut: only unconditional top-level breaks are provable here; Loader resolves versions and nested alternatives.
            return new(filename, ids, breaks);
        } catch (Exception ex) when (ex is InvalidDataException or JsonException or DecoderFallbackException) {
            if (!recognized) return null;
            var safeName = new string(filename.Where(c => !char.IsControl(c)).Take(180).ToArray());
            throw new InvalidDataException("Invalid or oversized fabric.mod.json: " + safeName, ex);
        }
    }

    static void CheckPair(Metadata first, Metadata second)
    {
        if (first.Breaks.Overlaps(second.Ids) || second.Breaks.Overlaps(first.Ids))
            throw new ModConflictException(string.Format(Localization.T("modDeclaredConflict"), first.Filename, second.Filename));
    }
}
