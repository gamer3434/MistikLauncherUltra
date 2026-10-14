using System.IO;
using System.Reflection;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class VersionRemovalTests
{
    public static int Run(string root, MainWindow window)
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        void Profile(string location, string id, string? parent = null)
        {
            var directory = Path.Combine(location, "versions", id); Directory.CreateDirectory(directory);
            var json = new JObject { ["id"] = id }; if (parent != null) json["inheritsFrom"] = parent;
            File.WriteAllText(Path.Combine(directory, id + ".json"), json.ToString());
            File.WriteAllText(Path.Combine(directory, id + ".jar"), "original profile bytes");
        }
        bool Rejected(Action remove) { try { remove(); return false; } catch (IOException) { return true; } }
        Profile(root, "base"); Profile(root, "fabric-child", "BASE"); Profile(root, "custom-child", "fabric-child");
        Check(Rejected(() => GameProfiles.Remove(root, "base")) && GameProfiles.IsInstalled(root, "custom-child"),
            "version removal preserves a shared base used by direct and inherited profiles, including Windows case differences");
        Check(Rejected(() => GameProfiles.Remove(root, "fabric-child")) && GameProfiles.IsInstalled(root, "custom-child"),
            "version removal preserves an intermediate profile used by another installed profile");
        var outside = Path.Combine(root, "keep.txt"); File.WriteAllText(outside, "personal data");
        Check(Rejected(() => GameProfiles.Remove(root, "../outside")) && File.ReadAllText(outside) == "personal data",
            "version removal rejects path traversal before touching personal files");
        GameProfiles.Remove(root, "custom-child"); GameProfiles.Remove(root, "fabric-child"); GameProfiles.Remove(root, "base");
        Check(!Directory.GetDirectories(Path.Combine(root, "versions")).Any() && File.ReadAllText(outside) == "personal data",
            "leaf profiles can be removed in dependency order without touching unrelated data");
        const string guarded = "removal-busy-fixture"; Profile(App.GameDir, guarded);
        foreach (string state in new[] { "launchPreparing", "gameRunning" })
        {
            var field = typeof(MainWindow).GetField(state, BindingFlags.Instance | BindingFlags.NonPublic)!;
            var previous = field.GetValue(window); field.SetValue(window, true);
            try { Check(Rejected(() => window.RemoveInstalledVersion(guarded)) && GameProfiles.IsInstalled(App.GameDir, guarded),
                "active launch state blocks version removal: " + state); }
            finally { field.SetValue(window, previous); }
        }
        window.RemoveInstalledVersion(guarded);
        Check(!Directory.Exists(Path.Combine(App.GameDir, "versions", guarded)), "idle launcher can remove an unreferenced installed version");
        return checks;
    }
}
