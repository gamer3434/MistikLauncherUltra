using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class ModAndMapRegressionTests
{
    static T Call<T>(Type owner, string name, params object?[] arguments)
    {
        try { return (T)owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!; }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name); checks++;
        }
        JObject Version(string id, string game, string loader, string type = "release") => new()
        {
            ["id"] = id, ["game_versions"] = new JArray(game), ["loaders"] = new JArray(loader), ["version_type"] = type
        };
        var nearby = new JArray(Version("nearby", "1.21.2", "fabric"));
        Check(ModManagerPage.FindCompatibleVersionSmart(nearby, "1.21.1", true, false) == null,
            "modpack never treats a neighboring Minecraft patch as compatible");
        Check(ModManagerPage.FindCompatibleVersionSmart(new JArray(Version("minor", "1.21", "fabric")), "1.21.1", true, false) == null,
            "minor-only metadata does not imply compatibility with later Minecraft patches");
        Check(ModManagerPage.FindCompatibleVersion(new JArray(Version("neo", "1.21.1", "neoforge")), "1.21.1", false, true) == null,
            "Forge rejects NeoForge-only mod files");
        Check(ModManagerPage.FindCompatibleVersion(new JArray(Version("quilt", "1.21.1", "quilt")), "1.21.1", true, false) == null,
            "Fabric rejects Quilt-only mod files");
        var forge = Version("forge", "26.1", "forge");
        var forgeVersions = new JArray(forge);
        Check(ModManagerPage.FindCompatibleVersion(forgeVersions, "26.1", "neoforge") == null &&
            ModManagerPage.FindCompatibleVersion(forgeVersions, "26.1", "forge") == forge,
            "exact loader selection supports year-based Minecraft versions");
        Check(ModManagerPage.FindCompatibleVersion(new JArray(Version("fabric", "26.1", "fabric")), "26.1", false, false) == null,
            "Vanilla does not bypass the mod loader compatibility check");
        var beta = Version("beta", "26.1", "fabric", "beta");
        var release = Version("release", "26.1", "fabric");
        Check(ModManagerPage.FindCompatibleVersion(new JArray(beta, release), "26.1", "fabric") == release,
            "exact compatible stable mod is preferred over beta");

        var dependencyParent = new JObject { ["dependencies"] = new JArray(
            new JObject { ["dependency_type"] = "required", ["project_id"] = "library", ["version_id"] = "pinned-old" },
            new JObject { ["dependency_type"] = "optional", ["project_id"] = "optional" },
            new JObject { ["dependency_type"] = "required", ["version_id"] = "version-only" }) };
        var requested = new List<(string project, string? version)>();
        Task Install(JToken version, Func<string, string?, Task> action) =>
            Call<Task>(typeof(ModManagerPage), "InstallRequiredDependencies", version, action);
        await Install(dependencyParent, (project, version) => { requested.Add((project, version)); return Task.CompletedTask; });
        Check(requested.SequenceEqual(new[] { ("library", (string?)"pinned-old"), ("", (string?)"version-only") }),
            "required dependency resolver retains pinned version IDs and ignores optional dependencies");
        bool rejected = false;
        try { await Install(dependencyParent, (_, _) => Task.FromException(new IOException("download failed"))); }
        catch (IOException) { rejected = true; }
        Check(rejected, "required dependency failure reaches the parent installer");
        rejected = false;
        try { await Install(new JObject { ["dependencies"] = new JArray(new JObject { ["dependency_type"] = "required" }) }, (_, _) => Task.CompletedTask); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "required dependency without identity cannot be reported as installed");

        byte[] modBytes = System.Text.Encoding.UTF8.GetBytes("verified mod fixture");
        var modFile = new JObject { ["filename"] = "library.jar", ["hashes"] = new JObject { ["sha512"] = Convert.ToHexString(SHA512.HashData(modBytes)) } };
        var mods = Path.Combine(root, "mods"); Directory.CreateDirectory(mods);
        var disabled = Path.Combine(mods, "library.jar.disabled"); File.WriteAllText(disabled, "disabled original");
        rejected = false;
        try { Call<string>(typeof(ModManagerPage), "InstallDownloadedMod", mods, modFile, modBytes, true, null); }
        catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(disabled) == "disabled original" && !File.Exists(Path.Combine(mods, "library.jar")),
            "disabled required dependency stays disabled and fails installation instead of false success");
        File.Delete(disabled);
        var other = Path.Combine(mods, "library-new.jar"); File.WriteAllText(other, "existing newer library");
        var alternatives = new JArray(new JObject { ["files"] = new JArray(new JObject { ["filename"] = "library-new.jar" }, modFile) });
        rejected = false;
        try { Call<string>(typeof(ModManagerPage), "InstallDownloadedMod", mods, modFile, modBytes, true, alternatives); }
        catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(other) == "existing newer library" && !File.Exists(Path.Combine(mods, "library.jar")),
            "pinned dependency cannot create duplicate active versions of an installed project");
        File.Delete(other);
        var pool = Path.Combine(root, "mods_pool", GameProfiles.VersionPoolKey("26.1", "fabric"));
        Call<string>(typeof(ModManagerPage), "InstallDownloadedMod", pool, modFile, modBytes, false, null);
        ModFiles.SyncPools(mods, Path.Combine(root, "mods_pool", "previous"), pool);
        Check(File.ReadAllBytes(Path.Combine(mods, "library.jar")).SequenceEqual(modBytes),
            "queued mod activates through the same version and loader pool used by launcher synchronization");

        var saves = Path.Combine(root, "saves");
        var existing = Path.Combine(saves, "SkyBlock"); Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "level.dat"), "player progress");
        string Archive(string name, string entryName)
        {
            string path = Path.Combine(root, name);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("fresh world"); return path;
        }
        string Extract(string path) => Call<string>(typeof(MapManagerPage), "ExtractMap", path, saves, "SkyBlock");
        var zip = Archive("world.zip", "wrapper/world/level.dat");
        string installed = Extract(zip);
        string another = Extract(zip);
        Check(installed != existing && another != installed && File.ReadAllText(Path.Combine(existing, "level.dat")) == "player progress" &&
            File.ReadAllText(Path.Combine(installed, "level.dat")) == "fresh world" && File.Exists(Path.Combine(another, "level.dat")),
            "repeated map installs create separate worlds and preserve all player progress");
        rejected = false;
        try { Extract(Archive("missing-world.zip", "readme.txt")); } catch (FileNotFoundException) { rejected = true; }
        Check(rejected && !Directory.GetDirectories(saves, "temp_*").Any() && File.ReadAllText(Path.Combine(existing, "level.dat")) == "player progress",
            "failed map extraction cleans staging and retains existing worlds");
        string rootWorld = Extract(Archive("root-world.zip", "level.dat"));
        Check(File.Exists(Path.Combine(rootWorld, "level.dat")) && !Directory.GetDirectories(saves, "temp_*").Any(),
            "map archive with level.dat at its root installs and cleans staging");
        return checks;
    }
}
