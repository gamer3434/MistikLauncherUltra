using System.IO;
using System.IO.Compression;
using MistikLauncher;
using Newtonsoft.Json.Linq;

public static class BundledHudTests
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        void Jar(string game, string filename, string id)
        {
            Directory.CreateDirectory(Path.Combine(game, "mods"));
            using var zip = ZipFile.Open(Path.Combine(game, "mods", filename), ZipArchiveMode.Create);
            using var writer = new StreamWriter(zip.CreateEntry("fabric.mod.json").Open());
            writer.Write(new JObject { ["id"] = id }.ToString());
        }
        var game = Path.Combine(root, "install");
        Jar(game, "custom.jar", "custom_hud"); Jar(game, "keys.jar", "keystrokesxvelo"); Jar(game, "armor.jar", "ukus-armor-hud");
        Jar(game, "other.jar", "another_mod");
        var config = Path.Combine(game, "config", "mistik-hud.json");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, "{\"enabled\":false,\"scale\":1.25,\"widgets\":[{\"enabled\":false,\"x\":42,\"y\":64}],\"personal\":\"kept\"}");
        var profile = Path.Combine(game, "config", "custom-hud", "profile1.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        File.WriteAllText(profile, "personal legacy HUD");
        BundledHud.Install(game);
        var target = Path.Combine(game, "mods", BundledHud.Filename);
        using (var zip = ZipFile.OpenRead(target))
        using (var reader = new StreamReader(zip.GetEntry("fabric.mod.json")!.Open()))
        {
            var metadata = JObject.Parse(reader.ReadToEnd());
            Check(metadata["id"]?.ToString() == "mistik_hud" && metadata["depends"]?["minecraft"]?.ToString() == "1.21.1",
                "Bundled HUD contains the own Fabric 1.21.1 mod");
        }
        Check(new[] { "custom", "keys", "armor" }.All(name => File.Exists(Path.Combine(game, "mods", name + ".jar.disabled"))) && File.Exists(Path.Combine(game, "mods", "other.jar")),
            "Own HUD disables legacy components reversibly and preserves unrelated mods");
        var saved = JObject.Parse(File.ReadAllText(config));
        Check(HudSettings.IsInstalled(game) && HudSettings.IsEnabled(game) && saved["scale"]!.Value<double>() == 1.25 && saved["widgets"]![0]!["x"]!.Value<int>() == 42 && saved["personal"]!.ToString() == "kept" && File.ReadAllText(profile) == "personal legacy HUD",
            "Own HUD preserves widget positions and previous custom profiles");
        HudSettings.SetEnabled(game, false);
        Check(!HudSettings.IsEnabled(game) && File.Exists(target + ".disabled") && JObject.Parse(File.ReadAllText(config))["enabled"]!.Value<bool>() == false,
            "Own HUD master switch disables its JAR and config");
        HudSettings.SetEnabled(game, true);
        BundledHud.Install(game);
        Check(HudSettings.IsEnabled(game) && !File.Exists(Path.Combine(game, "mods", "custom.jar")) && File.Exists(config + ".mistik-hud-backup"),
            "Own HUD reinstall keeps previous HUD disabled and original config backup");
        var collision = Path.Combine(root, "collision");
        Jar(collision, BundledHud.Filename, "personal_mod");
        var original = File.ReadAllBytes(Path.Combine(collision, "mods", BundledHud.Filename));
        bool rejected = false;
        try { BundledHud.Install(collision); } catch (IOException) { rejected = true; }
        Check(rejected && original.SequenceEqual(File.ReadAllBytes(Path.Combine(collision, "mods", BundledHud.Filename))),
            "Own HUD installation refuses to overwrite a personal mod filename");
        var malformed = Path.Combine(root, "malformed");
        Jar(malformed, "custom.jar", "custom_hud");
        var badConfig = Path.Combine(malformed, "config", "mistik-hud.json");
        Directory.CreateDirectory(Path.GetDirectoryName(badConfig)!);
        File.WriteAllText(badConfig, "{invalid");
        rejected = false;
        try { BundledHud.Install(malformed); } catch (Newtonsoft.Json.JsonException) { rejected = true; }
        Check(rejected && File.Exists(Path.Combine(malformed, "mods", "custom.jar")) && !File.Exists(Path.Combine(malformed, "mods", BundledHud.Filename)) && File.ReadAllText(badConfig) == "{invalid",
            "Own HUD installation rolls back mod changes when personal config is malformed");
        return checks;
    }
}
