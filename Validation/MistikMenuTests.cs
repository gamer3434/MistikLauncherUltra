using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MistikLauncher;
using Newtonsoft.Json.Linq;

public static class MistikMenuTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var options = Path.Combine(root, "options.txt");
        const string original = "renderDistance:9\nresourcePacks:[\"vanilla\",\"file/Personal.zip\"]\nkey_key.jump:key.keyboard.space\n";
        File.WriteAllText(options, original);
        MistikMenuPack.Install(root, "1.21.1");
        var lines = File.ReadAllLines(options);
        var packs = JArray.Parse(lines.Single(line => line.StartsWith("resourcePacks:"))["resourcePacks:".Length..]);
        Check(packs.Select(pack => pack.ToString()).SequenceEqual(new[] { "vanilla", "file/Personal.zip", "file/" + MistikMenuPack.Filename }) && lines.Contains("renderDistance:9") && lines.Contains("key_key.jump:key.keyboard.space"), "Mistik menu enables its pack without losing personal packs or options");
        Check(File.ReadAllText(options + ".mistik-menu-backup") == original, "Mistik menu keeps original Minecraft options backup");
        var archivePath = Path.Combine(root, "resourcepacks", MistikMenuPack.Filename);
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            using var reader = new StreamReader(archive.GetEntry("pack.mcmeta")!.Open());
            var metadata = JObject.Parse(reader.ReadToEnd());
            Check(metadata["pack"]?["pack_format"]?.Value<int>() == 34 && Enumerable.Range(0, 6).All(index => archive.GetEntry($"assets/minecraft/textures/gui/title/background/panorama_{index}.png") != null) && archive.GetEntry("assets/minecraft/textures/gui/sprites/widget/button_highlighted.png.mcmeta") != null, "Mistik menu contains 1.21.1 metadata panorama and nine slice button assets");
            using var titleStream = archive.GetEntry("assets/minecraft/textures/gui/title/minecraft.png")!.Open();
            using var titleBytes = new MemoryStream(); titleStream.CopyTo(titleBytes); titleBytes.Position = 0;
            var title = new FormatConvertedBitmap(BitmapFrame.Create(titleBytes, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[title.PixelWidth * title.PixelHeight * 4]; title.CopyPixels(pixels, title.PixelWidth * 4, 0);
            Check(title.PixelWidth == 512 && title.PixelHeight == 128 && pixels.Where((value, index) => index % 4 == 3).Any(alpha => alpha != 0) &&
                !pixels.Skip(title.PixelWidth * 88 * 4).Where((value, index) => index % 4 == 3).Any(alpha => alpha != 0), "Mistik logo artwork fits Minecraft 44 of 64 logical pixel title crop");
        }
        var before = File.ReadAllBytes(options);
        var timestamp = File.GetLastWriteTimeUtc(archivePath);
        MistikMenuPack.Install(root, "1.21.1");
        Check(File.ReadAllBytes(options).SequenceEqual(before) && File.GetLastWriteTimeUtc(archivePath) == timestamp, "Mistik menu reinstall preserves files and avoids duplicate selection");
        var invalidRoot = Path.Combine(root, "invalid"); Directory.CreateDirectory(invalidRoot);
        File.WriteAllText(Path.Combine(invalidRoot, "options.txt"), "resourcePacks:[1]");
        var rejected = false;
        try { MistikMenuPack.Install(invalidRoot, "1.21.1"); } catch (IOException) { rejected = true; }
        Check(rejected && !Directory.Exists(Path.Combine(invalidRoot, "resourcepacks")), "Mistik menu rejects invalid existing pack list before writing pack");
        var collisionRoot = Path.Combine(root, "collision"); Directory.CreateDirectory(Path.Combine(collisionRoot, "resourcepacks"));
        var collision = Path.Combine(collisionRoot, "resourcepacks", MistikMenuPack.Filename);
        using (var archive = ZipFile.Open(collision, ZipArchiveMode.Create)) archive.CreateEntry("personal.txt");
        var personal = File.ReadAllBytes(collision); rejected = false;
        try { MistikMenuPack.Install(collisionRoot, "1.21.1"); } catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(collision).SequenceEqual(personal) && !File.Exists(Path.Combine(collisionRoot, "options.txt")), "Mistik menu filename collision preserves personal pack and settings");
        rejected = false;
        try { MistikMenuPack.Install(Path.Combine(root, "wrong-version"), "1.20.1"); } catch (IOException) { rejected = true; }
        Check(rejected && !Directory.Exists(Path.Combine(root, "wrong-version")), "Mistik menu rejects unsupported Minecraft versions");
        return checks;
    }
}
