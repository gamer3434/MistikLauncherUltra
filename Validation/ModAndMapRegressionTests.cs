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

        var profileRoot = Path.Combine(root, "migration-profiles");
        const string sourceProfile = "fabric-loader-0.16.10-26.1";
        var profileFolder = Path.Combine(profileRoot, "versions", sourceProfile); Directory.CreateDirectory(profileFolder);
        File.WriteAllText(Path.Combine(profileFolder, sourceProfile + ".json"), new JObject {
            ["inheritsFrom"] = "26.1", ["libraries"] = new JArray(new JObject { ["name"] = "net.fabricmc:fabric-loader:0.16.10" })
        }.ToString());
        bool SameMigration(string target, string loader) => Call<bool>(typeof(ModManagerPage), "IsSameMigrationPool", profileRoot, sourceProfile, target, loader);
        Check(SameMigration("26.1", "Fabric"), "migration to the current inherited Minecraft and loader pool is a no-op");
        Check(!SameMigration("26.1", "forge") && !SameMigration("26.1.1", "fabric"),
            "migration still permits a different exact loader or Minecraft version");

        var importSource = Path.Combine(root, "import-source"); Directory.CreateDirectory(importSource);
        var importTarget = Path.Combine(root, "import-target"); Directory.CreateDirectory(importTarget);
        var localJar = Path.Combine(importSource, "local.jar"); File.WriteAllText(localJar, "incoming local bytes");
        var disabledLocal = Path.Combine(importTarget, "local.jar.disabled"); File.WriteAllText(disabledLocal, "disabled original bytes");
        Check(!ModFiles.Import(importTarget, localJar) && File.ReadAllText(disabledLocal) == "disabled original bytes" &&
            !File.Exists(Path.Combine(importTarget, "local.jar")), "local import preserves disabled state and never creates an active duplicate");
        File.Delete(disabledLocal);
        using (var locked = new FileStream(localJar, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rejected = false; try { ModFiles.Import(importTarget, localJar); } catch (IOException) { rejected = true; }
            Check(rejected && !File.Exists(Path.Combine(importTarget, "local.jar")) && !Directory.GetFiles(importTarget, "*.tmp").Any(),
                "failed local mod copy never exposes a partial jar and cleans staging");
        }
        Check(ModFiles.Import(importTarget, localJar) && File.ReadAllText(Path.Combine(importTarget, "local.jar")) == "incoming local bytes",
            "local import installs complete bytes from the source");
        File.WriteAllText(localJar, "new incoming bytes");
        Check(!ModFiles.Import(importTarget, localJar) && File.ReadAllText(Path.Combine(importTarget, "local.jar")) == "incoming local bytes",
            "repeated local import preserves the existing active mod");

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

    public static int SyncFailure(MainWindow window, string root)
    {
        Directory.CreateDirectory(root);
        var originalVersion = window.Config.Version;
        var originalSynced = window.Config.LastSyncedVersion;
        const string previousVersion = "96.101-fabric", nextVersion = "96.102-fabric";
        var previous = Path.Combine(App.AppData, "mods_pool", GameProfiles.VersionPoolKey("96.101", "fabric"));
        var next = Path.Combine(App.AppData, "mods_pool", GameProfiles.VersionPoolKey("96.102", "fabric"));
        var holding = Path.Combine(root, "existing-mods");
        ModFiles.SyncPools(App.ModsDir, holding, null);
        Directory.CreateDirectory(next);
        var oldMod = Path.Combine(App.ModsDir, "sync-old.jar");
        var newMod = Path.Combine(next, "sync-new.jar.disabled");
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        try
        {
            File.WriteAllText(oldMod, "original active bytes"); File.WriteAllText(newMod, "target disabled bytes");
            window.Config.Version = nextVersion; window.Config.LastSyncedVersion = previousVersion;
            ConfigManager.Save(window.Config);
            using (var locked = new FileStream(Path.Combine(App.AppData, "config.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Check(!window.SyncModsForCurrentVersion() && window.Config.LastSyncedVersion == previousVersion &&
                    File.ReadAllText(oldMod) == "original active bytes" && File.ReadAllText(newMod) == "target disabled bytes" &&
                    !File.Exists(Path.Combine(previous, "sync-old.jar")) && !File.Exists(Path.Combine(App.ModsDir, "sync-new.jar.disabled")),
                    "failed config save restores previous sync identity and both complete mod sets");
            }
            Check(ConfigManager.Load().LastSyncedVersion == previousVersion, "failed sync never changes persisted pool identity");
            Check(window.SyncModsForCurrentVersion() && window.Config.LastSyncedVersion == nextVersion &&
                ConfigManager.Load().LastSyncedVersion == nextVersion && File.ReadAllText(Path.Combine(previous, "sync-old.jar")) == "original active bytes" &&
                File.ReadAllText(Path.Combine(App.ModsDir, "sync-new.jar.disabled")) == "target disabled bytes",
                "retry after failed config save performs and persists the full transfer");
            window.Config.Version = previousVersion;
            string imported = window.WithSyncedMods(previousVersion, destination => {
                Check(File.ReadAllText(Path.Combine(destination, "sync-old.jar")) == "original active bytes" &&
                    !File.Exists(Path.Combine(destination, "sync-new.jar.disabled")), "mod writer first synchronizes the selected profile instead of writing into the previous active set");
                string path = Path.Combine(destination, "sync-import.jar"); File.WriteAllText(path, "selected profile import"); return path;
            });
            Check(File.ReadAllText(imported) == "selected profile import" && window.Config.LastSyncedVersion == previousVersion,
                "guarded mod write belongs to the synchronized selected profile");
            bool wrote = false, rejected = false;
            try { window.WithSyncedMods(nextVersion, _ => { wrote = true; return true; }); } catch (IOException) { rejected = true; }
            Check(rejected && !wrote && File.ReadAllText(imported) == "selected profile import",
                "stale mod operation cannot execute its write after profile selection changes");
            checks += ProfileContext(window).GetAwaiter().GetResult();
            return checks;
        }
        finally
        {
            foreach (var folder in new[] { App.ModsDir, previous, next })
                foreach (var name in new[] { "sync-old.jar", "sync-new.jar.disabled", "sync-import.jar" })
                    File.Delete(Path.Combine(folder, name));
            ModFiles.SyncPools(App.ModsDir, next, holding);
            window.Config.Version = originalVersion; window.Config.LastSyncedVersion = originalSynced;
            ConfigManager.Save(window.Config);
        }
    }

    sealed class FixtureModHandler(MainWindow window) : System.Net.Http.HttpMessageHandler
    {
        public readonly List<string> Requests = new();
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            if (Requests.Count != 1 || Requests[0] != "/v2/project/parent-mod/version")
                throw new InvalidOperationException("A changed profile must prevent dependency metadata and mod file requests.");
            window.Config.Version = "97.2-fabric";
            var parent = new JObject {
                ["id"] = "parent-a", ["project_id"] = "parent-mod", ["version_type"] = "release",
                ["game_versions"] = new JArray("97.1"), ["loaders"] = new JArray("fabric"),
                ["dependencies"] = new JArray(new JObject { ["dependency_type"] = "required", ["project_id"] = "required-library" }),
                ["files"] = new JArray(new JObject { ["primary"] = true, ["filename"] = "parent-mod.jar",
                    ["url"] = "https://cdn.modrinth.com/data/fixture/parent-mod.jar", ["hashes"] = new JObject { ["sha512"] = new string('0', 128) } })
            };
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new System.Net.Http.StringContent(new JArray(parent).ToString())
            });
        }
    }

    public static async Task<int> ProfileContext(MainWindow window)
    {
        var originalVersion = window.Config.Version;
        var page = (ModManagerPage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModManagerPage));
        typeof(ModManagerPage).GetField("_main", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, window);
        using var handler = new FixtureModHandler(window); using var client = new System.Net.Http.HttpClient(handler);
        var installed = new List<string>(); var visited = new HashSet<string>();
        var activeBefore = ModFiles.List(App.ModsDir).ToDictionary(path => path, File.ReadAllBytes);
        try
        {
            window.Config.Version = "97.1-fabric";
            var operation = (Task)typeof(ModManagerPage).GetMethod("DownloadModAndDependencies", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, new object?[] { "parent-mod", "parent-mod", installed, true, visited, null, null, null, null, client })!;
            bool rejected = false; try { await operation; } catch (IOException) { rejected = true; }
            if (!rejected || !handler.Requests.SequenceEqual(new[] { "/v2/project/parent-mod/version" }) || installed.Count != 0 || visited.Count != 0 ||
                !ModFiles.List(App.ModsDir).Order().SequenceEqual(activeBefore.Keys.Order()) ||
                activeBefore.Any(pair => !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)))
                throw new Exception("profile change during parent metadata download must prevent all required dependency requests and file writes");
            Console.WriteLine("PASS profile change during parent metadata download rejects required dependency resolution and preserves active mod bytes");
            var dependency=(Task)typeof(ModManagerPage).GetMethod("DownloadModAndDependencies",BindingFlags.Instance|BindingFlags.NonPublic)!
                .Invoke(page,new object?[] { "required-library","required-library",installed,true,visited,null,null,null,"97.1-fabric",client })!;
            rejected=false; try { await dependency; } catch(IOException) { rejected=true; }
            if(!rejected || handler.Requests.Count!=1 || installed.Count!=0)
                throw new Exception("A subsequent required dependency must retain the original profile before requesting metadata.");
            Console.WriteLine("PASS subsequent dependency refuses the stale original profile before metadata or writes");
            return 2;
        }
        finally { window.Config.Version = originalVersion; }
    }
}
