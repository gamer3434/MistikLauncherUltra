using System.IO;
using MistikLauncher;

static class ModImportBoundaryTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var source = Path.Combine(root, "incoming.jar"); var target = Path.Combine(root, "mods");
        foreach (long size in new long[] { 0, 128L * 1024 * 1024 + 1 })
        {
            using (var file = File.Create(source)) file.SetLength(size);
            bool rejected = false; try { ModFiles.Import(target, source); } catch (IOException) { rejected = true; }
            Check(rejected && !Directory.Exists(target) && new FileInfo(source).Length == size,
                "local mod import rejects unsafe size before creating destination or staging: " + size);
        }
        Directory.CreateDirectory(target); var preserved = Path.Combine(target, "personal.jar.disabled"); File.WriteAllText(preserved, "personal disabled bytes");
        File.WriteAllBytes(source, Array.Empty<byte>());
        bool invalidRejected = false; try { ModFiles.Import(target, source); } catch (IOException) { invalidRejected = true; }
        Check(invalidRejected && File.ReadAllText(preserved) == "personal disabled bytes" && Directory.GetFiles(target).Length == 1,
            "invalid local mod import leaves existing mods and disabled state untouched with no staging residue");
        File.WriteAllText(source, "valid local bytes");
        Check(ModFiles.Import(target, source) && File.ReadAllText(Path.Combine(target, "incoming.jar")) == "valid local bytes" &&
            File.ReadAllText(source) == "valid local bytes" && !Directory.GetFiles(target, "*.tmp").Any(),
            "bounded local mod import preserves source and commits complete bytes without staging residue");
        return checks;
    }
}