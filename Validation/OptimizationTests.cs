using System;
using System.IO;
using System.Linq;
using MistikLauncher;

public static class OptimizationTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "options.txt");
        const string original = "maxFps:300\nrenderDistance:20\nsimulationDistance:12\ncustomSetting:keep-me\n";
        File.WriteAllText(path, original);
        var originalBytes = File.ReadAllBytes(path);
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        Check(!GameGraphicsOptions.EnsureStartupSettings(root, enabled: false) &&
              File.ReadAllBytes(path).SequenceEqual(originalBytes),
            "disabled FPS optimization leaves all Minecraft options untouched");
        Check(GameGraphicsOptions.EnsureStartupSettings(root, enabled: true),
            "enabled FPS optimization updates startup settings");
        var startup = File.ReadAllText(path);
        Check(startup.Contains("renderDistance:12") && startup.Contains("simulationDistance:8") &&
              startup.Contains("syncChunkWrites:false") && startup.Contains("maxFps:300") &&
              startup.Contains("customSetting:keep-me"),
            "startup optimization preserves FPS limit and unrelated options");
        var backup = path + ".mistik-backup";
        Check(File.ReadAllBytes(backup).SequenceEqual(originalBytes),
            "first Minecraft settings backup keeps exact original bytes");
        Check(GameGraphicsOptions.ApplyFastPreset(root), "fast graphics preset applies");
        var preset = File.ReadAllText(path);
        Check(preset.Contains("renderDistance:6") && preset.Contains("simulationDistance:6") &&
              preset.Contains("maxFps:260") && preset.Contains("customSetting:keep-me") &&
              File.ReadAllBytes(backup).SequenceEqual(originalBytes),
            "graphics preset keeps unknown options and original backup");
        Check(!GameGraphicsOptions.EnsureStartupSettings(root, enabled: true) &&
              File.ReadAllText(path).Contains("maxFps:260"),
            "game startup does not undo the selected graphics preset");
        Check(!GameGraphicsOptions.ApplyFastPreset(root),
            "reapplying graphics preset does not rewrite unchanged settings");
        var logs = Path.Combine(root, "logs");
        var reports = Path.Combine(root, "crash-reports");
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(reports);
        var old = Path.Combine(logs, "2020-01-01.log.gz");
        var recent = Path.Combine(logs, "recent.log");
        var latest = Path.Combine(logs, "latest.log");
        var crash = Path.Combine(reports, "crash-2020.txt");
        File.WriteAllText(old, "old log bytes");
        File.WriteAllText(recent, "recent log bytes");
        File.WriteAllText(latest, "latest log bytes");
        File.WriteAllText(crash, "crash evidence");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(latest, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(crash, DateTime.UtcNow.AddDays(-30));
        var freed = GameLogCleaner.CleanOldLogs(root, DateTime.UtcNow.AddDays(-7));
        Check(freed > 0 && !File.Exists(old) && File.Exists(recent) && File.Exists(latest) && File.Exists(crash),
            "log cleanup runs without deleting current logs or crash evidence");
        return checks;
    }
}
