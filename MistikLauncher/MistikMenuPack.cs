using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public static class MistikMenuPack
{
    public const string Filename = "Mistik-Menu-1.21.zip";
    const string Marker = "Mistik menu resource pack v1";

    public static void Retire(string gameDirectory)
    {
        var root = Path.GetFullPath(gameDirectory);
        var path = Path.Combine(root, "resourcepacks", Filename);
        if (!File.Exists(path)) return;
        var options = Path.Combine(root, "options.txt");
        CheckPath(path); CheckPath(options);
        using (var archive = ZipFile.OpenRead(path))
        {
            var marker = archive.GetEntry("mistik-menu.txt");
            if (marker == null || marker.Length > 128) return;
            using var reader = new StreamReader(marker.Open());
            if (reader.ReadToEnd() != Marker) return;
        }
        var lines = File.Exists(options) ? File.ReadAllLines(options).ToList() : new List<string>();
        var selection = lines.FindIndex(line => line.StartsWith("resourcePacks:", StringComparison.Ordinal));
        if (lines.Count(line => line.StartsWith("resourcePacks:", StringComparison.Ordinal)) > 1)
            throw new IOException("Duplicate resource pack settings.");
        var optionsChanged = false;
        if (selection >= 0)
        {
            var packs = JArray.Parse(lines[selection]["resourcePacks:".Length..]);
            if (packs.Any(pack => pack.Type != JTokenType.String)) throw new IOException("Invalid resource pack settings.");
            foreach (var pack in packs.Where(pack => pack.ToString() == "file/" + Filename).ToArray())
            {
                pack.Remove(); optionsChanged = true;
            }
            if (optionsChanged) lines[selection] = "resourcePacks:" + packs.ToString(Formatting.None);
        }
        var retiredDirectory = Path.Combine(root, "resourcepacks", ".retired");
        CheckPath(retiredDirectory);
        Directory.CreateDirectory(retiredDirectory);
        var retired = Path.Combine(retiredDirectory, Guid.NewGuid().ToString("N") + "-" + Filename);
        var temporary = options + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (optionsChanged)
            {
                var backup = options + ".mistik-menu-retired-backup";
                CheckPath(backup);
                if (!File.Exists(backup)) File.Copy(options, backup);
                File.WriteAllLines(temporary, lines, new UTF8Encoding(false));
            }
            File.Move(path, retired);
            try { if (optionsChanged) File.Move(temporary, options, overwrite: true); }
            catch { File.Move(retired, path); throw; }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    static void CheckPath(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Mistik menu cannot modify linked paths.");
    }
}