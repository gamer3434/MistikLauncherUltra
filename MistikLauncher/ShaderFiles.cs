using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;
public static class ShaderFiles
{
    public const int MaximumBytes = 128 * 1024 * 1024;
    public static void EnsureRoot(string root)
    {
        Directory.CreateDirectory(root);
        for (string? path = Path.GetFullPath(root); path != null; path = Path.GetDirectoryName(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(Localization.T("shaderInvalid"));
    }
    public static bool Compatible(JToken version, string game) =>
        version["game_versions"] is JArray games && games.Any(x => x.ToString() == game) &&
        version["loaders"] is JArray loaders && loaders.Any(x => x.ToString() == "iris");
    public static string Install(string root, string filename, byte[] bytes, string? sha512)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || bytes.Length == 0 || bytes.Length > MaximumBytes ||
            sha512 == null || !Regex.IsMatch(sha512, "^[a-fA-F0-9]{128}$") || !Convert.ToHexString(SHA512.HashData(bytes)).Equals(sha512, StringComparison.OrdinalIgnoreCase))
            throw new IOException(Localization.T("shaderInvalid"));
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            long total = 0; bool shader = false;
            if (archive.Entries.Count > 20000) throw new IOException(Localization.T("shaderInvalid"));
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(x => x is ".." or ".") ||
                    ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || entry.Length > 512L * 1024 * 1024 - total)
                    throw new IOException(Localization.T("shaderInvalid"));
                total += entry.Length;
                shader |= name.StartsWith("shaders/", StringComparison.Ordinal) && !name.EndsWith('/') && entry.Length > 0;
            }
            if (!shader) throw new IOException(Localization.T("shaderInvalid"));
        }
        EnsureRoot(root);
        string target = Path.Combine(root, filename);
        if (File.Exists(target) && File.Exists(target + ".disabled")) throw new IOException(Localization.T("shaderConflict"));
        if (File.Exists(target + ".disabled")) target += ".disabled";
        if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException(Localization.T("shaderInvalid"));
        string temporary = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(target))
            {
                string backup = Path.Combine(root, ".backups"); EnsureRoot(backup);
                File.Replace(temporary, target, Path.Combine(backup, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(target)));
            }
            else File.Move(temporary, target);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static IEnumerable<string> List(string root) => Directory.Exists(root) ? Directory.GetFiles(root).Where(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase)) : Array.Empty<string>();
    public static string Toggle(string root, string path)
    {
        EnsureRoot(root); path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) ||
            !List(root).Contains(path, StringComparer.OrdinalIgnoreCase) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(Localization.T("shaderInvalid"));
        string target = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? path[..^9] : path + ".disabled";
        if (File.Exists(target)) throw new IOException(Localization.T("shaderConflict"));
        File.Move(path, target); return target;
    }
}
