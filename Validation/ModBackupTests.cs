using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using MistikLauncher;

public static class ModBackupTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        string mods = Path.Combine(root, "mods");
        Directory.CreateDirectory(mods);
        string enabled = Path.Combine(mods, "enabled.jar");
        string disabled = Path.Combine(mods, "disabled.jar.disabled");
        File.WriteAllText(enabled, "enabled original");
        File.WriteAllText(disabled, "disabled original");
        File.WriteAllText(Path.Combine(mods, "options.txt"), "not a mod");
        string emptyMod = Path.Combine(mods, "empty.jar");
        File.WriteAllBytes(emptyMod, Array.Empty<byte>());
        string emptyBackup = Path.Combine(root, "empty-mods.zip");
        bool rejected = false;
        try { ModBackup.Create(mods, emptyBackup); } catch (IOException) { rejected = true; }
        Check(rejected && !File.Exists(emptyBackup), "backup rejects empty mod files instead of creating an unrestorable archive");
        File.Delete(emptyMod);
        string backup = Path.Combine(root, "mods.zip");
        ModBackup.Create(mods, backup);
        var savedBackup = File.ReadAllBytes(backup);
        File.WriteAllText(enabled + ".disabled", "conflicting disabled duplicate");
        rejected = false;
        try { ModBackup.Create(mods, backup); } catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(backup).SequenceEqual(savedBackup) && File.ReadAllText(enabled) == "enabled original" &&
            File.ReadAllText(enabled + ".disabled") == "conflicting disabled duplicate" && !Directory.GetFiles(root, ".mistik-mod-backup-*.tmp").Any(),
            "backup rejects conflicting enabled/disabled copies before overwriting the previous archive or touching source mods");
        File.Delete(enabled + ".disabled");
        using (var lockedMod = new FileStream(disabled, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rejected = false;
            try { ModBackup.Create(mods, backup); } catch (IOException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(backup).SequenceEqual(savedBackup) && !Directory.GetFiles(root, ".mistik-mod-backup-*.tmp").Any(),
                "failed backup overwrite preserves the complete previous archive and cleans partial staging");
        }
        File.Delete(enabled);
        File.Delete(disabled);
        File.WriteAllText(Path.Combine(mods, "enabled.jar"), "keep installed copy");
        var restored = ModBackup.Restore(mods, backup);
        Check(restored.Restored == 1 && restored.Skipped == 1 && File.ReadAllText(enabled) == "keep installed copy" &&
            File.ReadAllText(disabled) == "disabled original" && !File.Exists(Path.Combine(mods, "enabled.jar.disabled")),
            "restore recovers missing enabled/disabled mods and skips same-name files without overwriting");

        string invalid = Path.Combine(root, "traversal.zip");
        using (var archive = ZipFile.Open(invalid, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("../escaped.jar").Open())) writer.Write("outside");
        rejected = false;
        try { ModBackup.Restore(mods, invalid); } catch (IOException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(root, "escaped.jar")) && File.ReadAllText(enabled) == "keep installed copy",
            "restore rejects archive path traversal without changing installed mods");

        string devicePath = Path.Combine(root, "device.zip");
        using (var archive = ZipFile.Open(devicePath, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("NUL.jar").Open())) writer.Write("device");
        rejected = false;
        try { ModBackup.Restore(mods, devicePath); } catch (IOException) { rejected = true; }
        Check(rejected && !Directory.GetDirectories(mods, ".restore-*", SearchOption.TopDirectoryOnly).Any(),
            "restore rejects reserved Windows device names before staging them");

        string collision = Path.Combine(root, "collision.zip");
        using (var archive = ZipFile.Open(collision, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("first.jar").Open())) writer.Write("first");
            using (var writer = new StreamWriter(archive.CreateEntry("blocked.jar").Open())) writer.Write("blocked");
        }
        Directory.CreateDirectory(Path.Combine(mods, "blocked.jar"));
        rejected = false;
        try { ModBackup.Restore(mods, collision); } catch (IOException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(mods, "first.jar")) && Directory.Exists(Path.Combine(mods, "blocked.jar")),
            "failed restore rolls back files already copied and retains the conflicting path");
        Check(!Directory.GetDirectories(mods, ".restore-*").Any(), "restore removes its staging directory after success and failure");
        foreach (bool disabledFirst in new[] { false, true })
        {
            var twinArchive = Path.Combine(root, "conflicting-states-" + disabledFirst + ".zip");
            using (var archive = ZipFile.Open(twinArchive, ZipArchiveMode.Create))
            {
                var entries = new[] { "twinned.jar", "TWINNED.jar.disabled" };
                foreach (var name in disabledFirst ? entries.Reverse() : entries)
                    using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write("conflicting archived state");
            }
            rejected = false;
            try { ModBackup.Restore(mods, twinArchive); } catch (IOException) { rejected = true; }
            Check(rejected && !File.Exists(Path.Combine(mods, "twinned.jar")) && !File.Exists(Path.Combine(mods, "twinned.jar.disabled")) &&
                File.ReadAllText(enabled) == "keep installed copy" && File.ReadAllText(disabled) == "disabled original" && !Directory.GetDirectories(mods, ".restore-*").Any(),
                "restore rejects contradictory archived enabled/disabled states before commit regardless of entry order: " + disabledFirst);
        }
        return checks;
    }
}
