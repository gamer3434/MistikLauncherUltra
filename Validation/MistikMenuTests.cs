using System.IO;
using System.IO.Compression;
using MistikLauncher;
using Newtonsoft.Json.Linq;

public static class MistikMenuTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        string Pack(string game, string? marker)
        {
            Directory.CreateDirectory(Path.Combine(game, "resourcepacks"));
            var path = Path.Combine(game, "resourcepacks", MistikMenuPack.Filename);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            if (marker != null) { using var writer = new StreamWriter(archive.CreateEntry("mistik-menu.txt").Open()); writer.Write(marker); }
            archive.CreateEntry("preserved.txt"); return path;
        }
        var options = Path.Combine(root, "options.txt");
        var original = "renderDistance:9\nresourcePacks:[\"vanilla\",\"file/Personal.zip\",\"file/" + MistikMenuPack.Filename + "\"]\nkey_key.jump:key.keyboard.space\n";
        File.WriteAllText(options, original);
        var archivePath = Pack(root, "Mistik menu resource pack v1");
        var bytes = File.ReadAllBytes(archivePath);
        MistikMenuPack.Retire(root);
        var lines = File.ReadAllLines(options);
        var packs = JArray.Parse(lines.Single(line => line.StartsWith("resourcePacks:"))["resourcePacks:".Length..]);
        Check(packs.Select(pack => pack.ToString()).SequenceEqual(new[] { "vanilla", "file/Personal.zip" }) && lines.Contains("renderDistance:9") && lines.Contains("key_key.jump:key.keyboard.space"),
            "retired Mistik menu selection removed without losing personal packs or game options");
        Check(File.ReadAllText(options + ".mistik-menu-retired-backup") == original,
            "Mistik menu retirement preserves exact original Minecraft options backup");
        var retired = Path.Combine(root, "resourcepacks", ".retired");
        Check(!File.Exists(archivePath) && Directory.GetFiles(retired).Length == 1 && File.ReadAllBytes(Directory.GetFiles(retired).Single()).SequenceEqual(bytes),
            "owned Mistik menu pack is retained in exact backup outside the resource pack list");
        var before = File.ReadAllBytes(options);
        MistikMenuPack.Retire(root);
        Check(File.ReadAllBytes(options).SequenceEqual(before) && Directory.GetFiles(retired).Length == 1,
            "Mistik menu retirement is idempotent");
        foreach (var marker in new string?[] { null, "personal marker" })
        {
            var personalRoot = Path.Combine(root, "personal-" + checks);
            var personalPack = Pack(personalRoot, marker); var personalBytes = File.ReadAllBytes(personalPack);
            var personalOptions = Path.Combine(personalRoot, "options.txt"); File.WriteAllText(personalOptions, original);
            MistikMenuPack.Retire(personalRoot);
            Check(File.ReadAllBytes(personalPack).SequenceEqual(personalBytes) && File.ReadAllText(personalOptions) == original && !Directory.Exists(Path.Combine(personalRoot, "resourcepacks", ".retired")),
                "Mistik menu retirement preserves personal filename collisions and changed ownership markers");
        }
        var invalidRoot = Path.Combine(root, "invalid"); var invalidPack = Pack(invalidRoot, "Mistik menu resource pack v1");
        File.WriteAllText(Path.Combine(invalidRoot, "options.txt"), "resourcePacks:[1]");
        var rejected = false;
        try { MistikMenuPack.Retire(invalidRoot); } catch (IOException) { rejected = true; }
        Check(rejected && File.Exists(invalidPack) && File.ReadAllText(Path.Combine(invalidRoot, "options.txt")) == "resourcePacks:[1]",
            "Mistik menu retirement rejects invalid options before moving owned pack");
        var missing = Path.Combine(root, "missing"); MistikMenuPack.Retire(missing);
        Check(!Directory.Exists(missing), "menu retirement leaves absent game directories untouched");
        Check(typeof(MistikMenuPack).GetMethod("Install") == null, "retired Mistik menu installation API and bundled artwork removed");
        return checks;
    }
}