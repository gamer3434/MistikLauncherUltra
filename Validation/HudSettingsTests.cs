using System.IO;
using System.IO.Compression;
using MistikLauncher;
using Newtonsoft.Json.Linq;

public static class HudSettingsTests
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var mods = Path.Combine(root, "mods");
        Directory.CreateDirectory(mods);
        void Jar(string name, string id)
        {
            using var zip = ZipFile.Open(Path.Combine(mods, name), ZipArchiveMode.Create);
            using var writer = new StreamWriter(zip.CreateEntry("fabric.mod.json").Open());
            writer.Write(new JObject { ["id"] = id }.ToString());
        }
        Check(!HudSettings.IsInstalled(root) && !HudSettings.IsEnabled(root), "HUD settings detects missing pack without claiming it is enabled");
        Jar("custom.jar", "custom_hud"); Jar("keys.jar", "keystrokesxvelo"); Jar("armor.jar", "ukus-armor-hud"); Jar("menu.jar", "modmenu");
        var config = Path.Combine(root, "config", "custom-hud", "config.json");
        var profile = Path.Combine(root, "config", "custom-hud", "profile1.txt");
        var armor = Path.Combine(root, "config", "ukus-armor-hud.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, "{\"enabled\":false,\"activeProfile\":2,\"personal\":\"kept\"}");
        File.WriteAllText(profile, "personal HUD with {fps}");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(config)!, "profile2.txt"), "");
        File.WriteAllText(armor, "enabled = false # personal comment\noffsetX = 15\n");
        HudSettings.SetEnabled(root, true);
        var enabledConfig = JObject.Parse(File.ReadAllText(config));
        Check(HudSettings.IsEnabled(root) && enabledConfig["activeProfile"]!.Value<int>() == 1, "HUD enable selects nonempty profile instead of blank active profile");
        Check(enabledConfig["personal"]!.ToString() == "kept" && File.ReadAllText(profile) == "personal HUD with {fps}" && File.ReadAllText(armor).Contains("offsetX = 15"), "HUD settings preserves personal profiles and unknown config values");
        Check(File.Exists(config + ".mistik-hud-backup") && File.ReadAllText(armor).Contains("enabled = true # personal comment"), "HUD settings backs up original config and keeps armor comments");
        var originalBytes = File.ReadAllBytes(Path.Combine(mods, "keys.jar"));
        HudSettings.SetEnabled(root, false);
        Check(!HudSettings.IsEnabled(root) && new[] { "custom", "keys", "armor" }.All(name => File.Exists(Path.Combine(mods, name + ".jar.disabled"))) && File.Exists(Path.Combine(mods, "menu.jar")), "HUD disable turns off all three components and leaves Mod Menu enabled");
        HudSettings.SetEnabled(root, true);
        Check(HudSettings.IsEnabled(root) && File.ReadAllBytes(Path.Combine(mods, "keys.jar")).SequenceEqual(originalBytes), "HUD enable restores exact mod bytes");
        var savedConfig = File.ReadAllText(config);
        File.Copy(Path.Combine(mods, "keys.jar"), Path.Combine(mods, "keys.jar.disabled"));
        bool rejected = false;
        try { HudSettings.SetEnabled(root, false); } catch (IOException) { rejected = true; }
        Check(rejected && File.Exists(Path.Combine(mods, "custom.jar")) && File.ReadAllText(config) == savedConfig && File.ReadAllText(armor).Contains("enabled = true"), "HUD toggle collision rolls back previously changed mods and config");
        var emptyRoot = Path.Combine(root, "empty");
        HudSettings.EnsureProfile(emptyRoot);
        var emptyProfile = Path.Combine(emptyRoot, "config", "custom-hud", "profile1.txt");
        File.WriteAllText(emptyProfile, "   ");
        HudSettings.EnsureProfile(emptyRoot);
        Check(File.ReadAllText(emptyProfile).Contains("{fps}"), "HUD preset repairs blank profile without overwriting personal content");
        return checks;
    }
}
