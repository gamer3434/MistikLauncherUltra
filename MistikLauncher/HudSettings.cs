using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public static class HudSettings
{
    const string Profile = "==Section:TopLeft==\n&bMISTIK &7| &f{fps} FPS\n&7XYZ &f{bx} {by} {bz}\n&7Ping &f{ping} ms\n";
    static readonly string[] ModIds = ["custom_hud", "keystrokesxvelo", "ukus-armor-hud"];

    internal static string? ModId(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("HUD mod cannot be a link.");
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("fabric.mod.json");
            if (entry == null || entry.Length > 64 * 1024) return null;
            using var reader = new StreamReader(entry.Open());
            return JObject.Parse(reader.ReadToEnd(), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error })["id"]?.ToString();
        }
        catch (InvalidDataException) { return null; }
        catch (JsonException) { return null; }
    }

    internal static string[] LegacyMods(string gameDirectory) => AllMods(gameDirectory).Where(path => ModIds.Contains(ModId(path))).ToArray();

    static string[] AllMods(string gameDirectory)
    {
        var root = Path.Combine(gameDirectory, "mods");
        if (!Directory.Exists(root)) return [];
        ModFiles.EnsureRoot(root);
        return ModFiles.List(root).ToArray();
    }

    static string[] Mods(string gameDirectory)
    {
        var all = AllMods(gameDirectory);
        var own = all.Where(path => ModId(path) == "mistik_hud").ToArray();
        return own.Length > 0 ? own : all.Where(path => ModIds.Contains(ModId(path))).ToArray();
    }

    static string ConfigPath(string gameDirectory) => Path.Combine(gameDirectory, "config", "custom-hud", "config.json");
    public static bool IsInstalled(string gameDirectory) => Mods(gameDirectory).Length > 0;
    public static bool IsEnabled(string gameDirectory)
    {
        var mods = Mods(gameDirectory);
        if (mods.Length == 0 || mods.Any(path => !ModFiles.Enabled(path))) return false;
        if (mods.Any(path => ModId(path) == "mistik_hud"))
        {
            var ownConfig = Path.Combine(gameDirectory, "config", "mistik-hud.json");
            return !File.Exists(ownConfig) || ReadConfig(ownConfig)["enabled"]?.Value<bool>() != false;
        }
        var config = ConfigPath(gameDirectory);
        if (File.Exists(config) && ReadConfig(config)["enabled"]?.Value<bool>() == false) return false;
        var armor = Path.Combine(gameDirectory, "config", "ukus-armor-hud.toml");
        return !File.Exists(armor) || !Regex.IsMatch(Read(armor), @"(?m)^\s*enabled\s*=\s*false\s*(?:#.*)?$");
    }

    public static void EnsureProfile(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "config", "custom-hud", "profile1.txt");
        ModFiles.EnsureRoot(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(Read(path))) Write(path, Profile);
    }

    public static void SetEnabled(string gameDirectory, bool enabled)
    {
        var mods = Mods(gameDirectory);
        if (mods.Length == 0) throw new IOException("Install the HUD pack first.");
        if (mods.Any(path => ModId(path) == "mistik_hud"))
        {
            SetOwnEnabled(gameDirectory, mods, enabled);
            return;
        }
        var moved = new List<string>();
        var originals = new Dictionary<string, string?>();
        void Change(string path, string text)
        {
            originals[path] = File.Exists(path) ? Read(path) : null;
            Write(path, text);
        }
        try
        {
            var profile = Path.Combine(gameDirectory, "config", "custom-hud", "profile1.txt");
            if (enabled && (!File.Exists(profile) || string.IsNullOrWhiteSpace(Read(profile)))) Change(profile, Profile);
            var configPath = ConfigPath(gameDirectory);
            var config = File.Exists(configPath) ? ReadConfig(configPath) : new JObject();
            config["enabled"] = enabled;
            var active = config["activeProfile"]?.Value<int>() ?? 1;
            var activePath = Path.Combine(Path.GetDirectoryName(configPath)!, $"profile{active}.txt");
            if (active < 1 || active > 3 || !File.Exists(activePath) || string.IsNullOrWhiteSpace(Read(activePath))) active = 1;
            config["activeProfile"] = active;
            Change(configPath, config.ToString());
            var armor = Path.Combine(gameDirectory, "config", "ukus-armor-hud.toml");
            if (File.Exists(armor))
            {
                var text = Read(armor);
                var pattern = @"(?m)^(\s*enabled\s*=\s*)(true|false)(\s*(?:#.*)?\r?)$";
                if (Regex.Matches(text, pattern).Count != 1) throw new InvalidDataException("Armor HUD enabled setting is missing or ambiguous.");
                Change(armor, Regex.Replace(text, pattern, match => match.Groups[1].Value + (enabled ? "true" : "false") + match.Groups[3].Value));
            }
            foreach (var mod in mods)
                if (ModFiles.Enabled(mod) != enabled) moved.Add(ModFiles.Toggle(Path.Combine(gameDirectory, "mods"), mod));
        }
        catch
        {
            foreach (var path in moved.AsEnumerable().Reverse()) ModFiles.Toggle(Path.Combine(gameDirectory, "mods"), path);
            foreach (var item in originals.Reverse())
                if (item.Value == null) { if (File.Exists(item.Key)) File.Delete(item.Key); }
                else Write(item.Key, item.Value);
            throw;
        }
    }

    static void SetOwnEnabled(string gameDirectory, string[] mods, bool enabled)
    {
        var path = Path.Combine(gameDirectory, "config", "mistik-hud.json");
        var original = File.Exists(path) ? Read(path) : null;
        var config = original == null ? new JObject() : JObject.Parse(original,
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        config["enabled"] = enabled;
        var moved = new List<string>();
        try
        {
            Write(path, config.ToString());
            foreach (var mod in mods)
                if (ModFiles.Enabled(mod) != enabled) moved.Add(ModFiles.Toggle(Path.Combine(gameDirectory, "mods"), mod));
        }
        catch
        {
            foreach (var mod in moved.AsEnumerable().Reverse()) ModFiles.Toggle(Path.Combine(gameDirectory, "mods"), mod);
            if (original == null) { if (File.Exists(path)) File.Delete(path); }
            else Write(path, original);
            throw;
        }
    }

    static JObject ReadConfig(string path) => JObject.Parse(Read(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    static string Read(string path)
    {
        ModFiles.EnsureRoot(Path.GetDirectoryName(path)!);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > 1024 * 1024)
            throw new IOException("Invalid HUD configuration file.");
        return File.ReadAllText(path);
    }
    static void Write(string path, string text)
    {
        ModFiles.EnsureRoot(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) Read(path);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                var backup = path + ".mistik-hud-backup";
                if (!File.Exists(backup)) File.Copy(path, backup);
                File.Move(temporary, path, true);
            }
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
