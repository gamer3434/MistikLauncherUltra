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

        var setGpuPreference = typeof(KernelOptimizer).GetMethod("SetGpuPreferenceValue",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Func<string, string?, Func<string, string?>, Action<string, string?>, string?>>();
        var selectedJava = Path.Combine(root, "java21", "bin", "java.exe");
        var otherJava = Path.Combine(root, "java25", "bin", "java.exe");
        var otherLauncher = Path.Combine(root, "MinecraftLauncher.exe");
        var gpuValues = new System.Collections.Generic.Dictionary<string, string>
        {
            [selectedJava] = "GpuPreference=1;CustomPreference=keep;",
            [otherJava] = "GpuPreference=1;",
            [otherLauncher] = "GpuPreference=0;"
        };
        var touched = new System.Collections.Generic.List<string>();
        string? ReadGpu(string executable) => gpuValues.GetValueOrDefault(executable);
        void WriteGpu(string executable, string? value)
        {
            touched.Add(executable);
            if (value == null) gpuValues.Remove(executable);
            else gpuValues[executable] = value;
        }
        var originalGpu = setGpuPreference(selectedJava, "GpuPreference=2;", ReadGpu, WriteGpu);
        Check(originalGpu == "GpuPreference=1;CustomPreference=keep;" && gpuValues[selectedJava] == "GpuPreference=2;" &&
              touched.SequenceEqual(new[] { selectedJava }) && gpuValues[otherJava] == "GpuPreference=1;" && gpuValues[otherLauncher] == "GpuPreference=0;",
            "GPU optimization changes only the selected process executable and captures its original preference");
        setGpuPreference(selectedJava, originalGpu, ReadGpu, WriteGpu);
        Check(gpuValues[selectedJava] == "GpuPreference=1;CustomPreference=keep;" && touched.All(executable => executable == selectedJava),
            "GPU rollback restores the exact original preference without changing sibling Java or launchers");
        gpuValues.Remove(selectedJava); touched.Clear();
        originalGpu = setGpuPreference(selectedJava, "GpuPreference=2;", ReadGpu, WriteGpu);
        Check(originalGpu == null && gpuValues[selectedJava] == "GpuPreference=2;" && touched.SequenceEqual(new[] { selectedJava }),
            "GPU optimization records that the selected executable previously had no preference");
        setGpuPreference(selectedJava, originalGpu, ReadGpu, WriteGpu);
        Check(!gpuValues.ContainsKey(selectedJava) && gpuValues[otherJava] == "GpuPreference=1;" && gpuValues[otherLauncher] == "GpuPreference=0;",
            "GPU rollback removes only its newly created preference");
        touched.Clear(); bool relativeRejected = false;
        try { setGpuPreference("java.exe", "GpuPreference=2;", ReadGpu, WriteGpu); }
        catch (ArgumentException) { relativeRejected = true; }
        Check(relativeRejected && touched.Count == 0, "GPU preference rejects ambiguous relative executable paths before writing");
        return checks;
    }
}
