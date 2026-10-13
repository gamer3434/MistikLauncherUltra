using System.IO;
using System.IO.Compression;
using System.Reflection;
using MistikLauncher;
using MistikLauncher.Pages;

static class ModMutationSafetyTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var mods = Path.Combine(root, "mods"); Directory.CreateDirectory(mods);
        var enabled = Path.Combine(mods, "enabled.jar"); var disabled = Path.Combine(mods, "disabled.jar.disabled");
        File.WriteAllText(enabled, "enabled original"); File.WriteAllText(disabled, "disabled original");
        var removed = ModFiles.Remove(mods, enabled);
        Check(!File.Exists(enabled) && File.ReadAllText(removed) == "enabled original" && File.ReadAllText(disabled) == "disabled original",
            "single mod removal retains exact backup and leaves other enabled states unchanged");
        var outside = Path.Combine(root, "outside.jar"); File.WriteAllText(outside, "outside original");
        bool rejected = false; try { ModFiles.Remove(mods, outside); } catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(outside) == "outside original", "mod removal rejects paths outside its validated mod root");
        File.WriteAllText(enabled, "enabled restored");
        var backup = ModFiles.Clear(mods);
        Check(!ModFiles.List(mods).Any() && File.ReadAllText(Path.Combine(backup, "enabled.jar")) == "enabled restored" &&
            File.ReadAllText(Path.Combine(backup, "disabled.jar.disabled")) == "disabled original" && File.ReadAllText(removed) == "enabled original",
            "mod cleanup backs up both active and disabled JARs without touching earlier backups");
        File.WriteAllText(enabled, "rollback enabled"); File.WriteAllText(disabled, "rollback disabled");
        using (var locked = new FileStream(disabled, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rejected = false; try { ModFiles.Clear(mods); } catch (IOException) { rejected = true; }
            Check(rejected && File.Exists(enabled) && File.Exists(disabled) && File.ReadAllText(enabled) == "rollback enabled",
                "locked-file cleanup fails explicitly and rolls back already moved mods");
        }
        var saves = Path.Combine(root, "saves"); var existing = Path.Combine(saves, "OwnedWorld"); Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "level.dat"), "world progress");
        string Zip(string name, params string[] entries)
        {
            var path = Path.Combine(root, name);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var entry in entries) { using var writer = new StreamWriter(archive.CreateEntry(entry).Open()); writer.Write("fixture"); }
            return path;
        }
        void Extract(string path)
        {
            try { typeof(MapManagerPage).GetMethod("ExtractMap", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { path, saves, "OwnedWorld" }); }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
        foreach (var entry in new[] { "../escaped.txt", "world/level.dat:payload", "C:/outside/level.dat", "world/../level.dat" })
        {
            var zip = Zip("unsafe-" + checks + ".zip", entry); rejected = false;
            try { Extract(zip); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(Path.Combine(existing, "level.dat")) == "world progress" && !Directory.GetDirectories(saves, "temp_*").Any(),
                "map rejects unsafe archive path before writes: " + entry);
        }
        void DeclaredSize(string path, uint size)
        {
            var bytes = File.ReadAllBytes(path);
            for (int i = 0; i <= bytes.Length - 46; i++)
                if (BitConverter.ToUInt32(bytes, i) == 0x02014b50) BitConverter.GetBytes(size).CopyTo(bytes, i + 24);
            File.WriteAllBytes(path, bytes);
        }
        var oversized = Zip("oversized-map.zip", "level.dat"); DeclaredSize(oversized, 256u * 1024 * 1024 + 1);
        rejected = false; try { Extract(oversized); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && !Directory.GetDirectories(saves, "temp_*").Any() && File.ReadAllText(Path.Combine(existing, "level.dat")) == "world progress",
            "map rejects oversized declared file before decompression and keeps existing worlds");
        var total = Zip("total-map.zip", "level.dat", "region/a", "region/b", "region/c", "region/d"); DeclaredSize(total, 256u * 1024 * 1024);
        rejected = false; try { Extract(total); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && !Directory.GetDirectories(saves, "temp_*").Any(), "map rejects combined decompressed size above its safe limit before extracting");
        return checks;
    }
}