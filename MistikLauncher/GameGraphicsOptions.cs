using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MistikLauncher;

public static class GameGraphicsOptions
{
    private static readonly IReadOnlyDictionary<string, string> FastPreset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["enableVsync"] = "false",
        ["graphicsMode"] = "0",
        ["renderDistance"] = "6",
        ["simulationDistance"] = "6",
        ["particles"] = "2",
        ["ao"] = "0",
        ["clouds"] = "false",
        ["bobView"] = "false",
        ["mipmapLevels"] = "0",
        ["maxFps"] = "260"
    };

    public static bool ApplyFastPreset(string gameDirectory)
    {
        Directory.CreateDirectory(gameDirectory);
        return Update(Path.Combine(gameDirectory, "options.txt"), FastPreset, create: true);
    }

    public static bool EnsureStartupSettings(string gameDirectory, bool enabled)
    {
        // The switch must control the launch-time edits too. Never override a user's
        // Minecraft options (including their FPS limit) when it is off.
        if (!enabled) return false;
        var path = Path.Combine(gameDirectory, "options.txt");
        if (!File.Exists(path)) return false;

        var lines = File.ReadAllLines(path);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["syncChunkWrites"] = "false"
        };
        foreach (var line in lines)
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            if (key.Equals("renderDistance", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line[(colon + 1)..].Trim(), out var render) && render > 12)
                settings["renderDistance"] = "12";
            else if (key.Equals("simulationDistance", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(line[(colon + 1)..].Trim(), out var simulation) && simulation > 8)
                settings["simulationDistance"] = "8";
        }
        return Update(path, settings, create: false);
    }

    private static bool Update(string path, IReadOnlyDictionary<string, string> settings, bool create)
    {
        if (!create && !File.Exists(path)) return false;
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon < 0) continue;
            var key = lines[i][..colon].Trim();
            if (!settings.TryGetValue(key, out var value)) continue;
            seen.Add(key);
            var updated = $"{key}:{value}";
            if (lines[i] == updated) continue;
            lines[i] = updated;
            changed = true;
        }
        foreach (var setting in settings)
        {
            if (seen.Contains(setting.Key)) continue;
            lines.Add($"{setting.Key}:{setting.Value}");
            changed = true;
        }
        if (!changed) return false;

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Keep the first original settings as a recovery point.
            if (File.Exists(path) && !File.Exists(path + ".mistik-backup"))
                File.Copy(path, path + ".mistik-backup");
            File.WriteAllLines(temporary, lines);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
