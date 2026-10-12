using System.IO;
using System.Reflection;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

public static class ModMigrationTargetTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        const string gameVersion = "1.21.1";
        void Profile(string id, string? loaderLibrary = null, string? inherits = gameVersion)
        {
            var folder = Path.Combine(root, "versions", id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, id + ".json"), new JObject
            {
                ["inheritsFrom"] = inherits,
                ["libraries"] = loaderLibrary == null ? new JArray() : new JArray(new JObject { ["name"] = loaderLibrary })
            }.ToString());
            if (inherits == null) File.WriteAllBytes(Path.Combine(folder, id + ".jar"), new byte[] { 1 });
        }

        Profile(gameVersion, inherits: null);
        const string fabric = "fabric-loader-0.19.5-1.21.1";
        const string forge = "forge-52.0.0-1.21.1";
        const string neoForge = "neoforge-21.1.1-1.21.1";
        const string quilt = "quilt-loader-0.29.0-1.21.1";
        Profile(fabric, "net.fabricmc:fabric-loader:0.19.5");
        Profile(forge, "net.minecraftforge:forge:52.0.0");
        Profile(neoForge, "net.neoforged:neoforge:21.1.1");
        Profile(quilt, "org.quiltmc:quilt-loader:0.29.0");

        var method = typeof(ModManagerPage).GetMethod("GetMigrationTargets", BindingFlags.NonPublic | BindingFlags.Static)!;
        var targets = (IEnumerable<object>)method.Invoke(null, new object[] {
            root, new[] { gameVersion, fabric, forge, neoForge, quilt, "missing-profile", "../outside" }
        })!;
        string Value(object target, string property) => target.GetType().GetProperty(property)!.GetValue(target)!.ToString()!;
        var profileIds = targets.Select(target => Value(target, "ProfileId")).ToArray();
        var targetArray = targets.ToArray();
        Check(profileIds.SequenceEqual(new[] { fabric, forge, neoForge, quilt }),
            "migration choices include only installed mod-loader profiles and exclude vanilla, missing, and unsafe IDs");
        Check(targetArray.All(target => Value(target, "GameVersion") == gameVersion) &&
            targetArray.Select(target => Value(target, "Loader")).SequenceEqual(new[] { "fabric", "forge", "neoforge", "quilt" }),
            "target choices resolve the exact Minecraft version and all four supported loaders");
        Check(targetArray[2].ToString()!.Contains("1.21.1 · neoforge") && targetArray[2].ToString()!.Contains(neoForge),
            "profile selector labels show the game version, loader, and installed profile ID");
        return checks;
    }
}
