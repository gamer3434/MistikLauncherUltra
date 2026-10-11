using System.IO;
using System.Reflection;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class HudPresetTests
{
    static T? Call<T>(string method, params object[] arguments)
    {
        try { return (T?)typeof(ModManagerPage).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }
        Directory.CreateDirectory(root);
        Call<object>("CreateHudProfile", root);
        var path = Path.Combine(root, "config", "custom-hud", "profile1.txt");
        var content = File.ReadAllText(path);
        Check(content.Contains("{fps}") && content.Contains("{bx}") && content.Contains("{ping}"),
            "Mistik HUD profile exposes FPS coordinates and ping using CustomHUD variables");
        File.WriteAllText(path, "personal HUD");
        Call<object>("CreateHudProfile", root);
        Check(File.ReadAllText(path) == "personal HUD", "HUD preset preserves personal CustomHUD profile");

        var mods = Path.Combine(root, "mods");
        Directory.CreateDirectory(mods);
        JObject Version(string id, string game, string loader, string filename) => new()
        {
            ["id"] = id, ["game_versions"] = new JArray(game), ["loaders"] = new JArray(loader),
            ["files"] = new JArray(new JObject { ["filename"] = filename })
        };
        var latest = Version("latest", "1.21.1", "fabric", "library-new.jar");
        var existing = Version("existing", "1.21.1", "fabric", "library-old.jar");
        File.WriteAllText(Path.Combine(mods, "library-old.jar"), "existing");
        Check(Call<JToken>("FindInstalledCompatibleVersion", new JArray(latest, existing), mods, "1.21.1", "fabric") == existing,
            "HUD install reuses an installed compatible dependency instead of adding a duplicate version");
        Check(Call<JToken>("FindInstalledCompatibleVersion", new JArray(existing), mods, "1.21.2", "fabric") == null,
            "HUD dependency reuse rejects neighboring Minecraft versions");
        Check(Call<JToken>("FindInstalledCompatibleVersion", new JArray(existing), mods, "1.21.1", "forge") == null,
            "HUD dependency reuse rejects another loader");
        File.Move(Path.Combine(mods, "library-old.jar"), Path.Combine(mods, "library-old.jar.disabled"));
        Check(Call<JToken>("FindInstalledCompatibleVersion", new JArray(existing), mods, "1.21.1", "fabric") == null,
            "HUD installation never re-enables disabled mods");
        File.WriteAllText(Path.Combine(root, "outside.jar"), "outside");
        Check(Call<JToken>("FindInstalledCompatibleVersion", new JArray(Version("unsafe", "1.21.1", "fabric", "../outside.jar")), mods, "1.21.1", "fabric") == null,
            "HUD dependency reuse rejects path traversal in Modrinth filenames");
        return checks;
    }
}
