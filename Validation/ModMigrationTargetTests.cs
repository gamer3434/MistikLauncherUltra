using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;
using Localization = MistikLauncher.Localization;

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

    public static int RunSource(MainWindow window,string root)
    {
        Directory.CreateDirectory(root);
        var saved=Path.Combine(root,"saved-active"); Directory.CreateDirectory(saved);
        var original=window.Config;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var gate=typeof(MainWindow).GetField("_modSyncGate",flags)!.GetValue(window)!;
        var sourcePool=Path.Combine(App.AppData,"mods_pool","95.7_fabric"); Directory.CreateDirectory(sourcePool);
        var previousPool=Path.Combine(App.AppData,"mods_pool","95.6_forge"); Directory.CreateDirectory(previousPool);
        const string sourceName="migration-selected-source.jar",previousName="migration-previous-source.jar";
        foreach(var path in ModFiles.List(App.ModsDir)) File.Move(path,Path.Combine(saved,Path.GetFileName(path)));
        var originalContext=SynchronizationContext.Current;
        using var paused=new PausedMigrationContext();
        using var entered=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        Task? competing=null;
        try
        {
            window.Config=new LauncherConfig { Version="fabric-95.7",LastSyncedVersion="forge-95.6" };
            File.WriteAllText(Path.Combine(App.ModsDir,previousName),"previous profile bytes");
            using(var archive=System.IO.Compression.ZipFile.Open(Path.Combine(sourcePool,sourceName),System.IO.Compression.ZipArchiveMode.Create))
                using(var writer=new StreamWriter(archive.CreateEntry("fabric.mod.json").Open())) writer.Write("{\"id\":\"migration_source\",\"version\":\"1\"}");
            foreach(var id in new[] { "95.8","forge-95.8" })
            {
                var folder=Path.Combine(App.GameDir,"versions",id); Directory.CreateDirectory(folder);
                var profile=id=="95.8"?new JObject { ["libraries"]=new JArray() }:new JObject { ["inheritsFrom"]="95.8",["libraries"]=new JArray(new JObject { ["name"]="net.minecraftforge:forge:95.8" }) };
                File.WriteAllText(Path.Combine(folder,id+".json"),profile.ToString());
                if(id=="95.8") File.WriteAllText(Path.Combine(folder,id+".jar"),"isolated base fixture");
            }
            using var client=new HttpClient(new EmptyCatalog());
            var page=new ModManagerPage(window,client);
            var migrate=(Button)typeof(ModManagerPage).GetField("_migrationButton",flags)!.GetValue(page)!;
            var method=typeof(ModManagerPage).GetMethod("MigrateAndDownloadMods",flags)!;
            Task Start()=>(Task)method.Invoke(page,new object[] { "forge-95.8",migrate })!;
            competing=Task.Run(()=> { lock(gate) { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); } });
            if(!entered.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Could not hold migration source synchronization gate.");
            SynchronizationContext.SetSynchronizationContext(paused);
            var first=Start();
            if(first.IsCompleted || migrate.IsEnabled || !Start().IsCompleted) throw new Exception("An awaiting migration must yield and reject duplicate starts before reading source mods.");
            var refresh=Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("modMigrationRefresh")));
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(migrate.IsEnabled || !Start().IsCompleted) throw new Exception("Refreshing migration targets must not enable a second migration.");
            Console.WriteLine("PASS migration source synchronization yields and target refresh cannot bypass its duplicate guard");
            release.Set(); competing.GetAwaiter().GetResult();
            if(!paused.Posted.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Migration did not finish its asynchronous source snapshot.");
            if(window.Config.LastSyncedVersion!="fabric-95.7" || !File.Exists(Path.Combine(App.ModsDir,sourceName)) || File.Exists(Path.Combine(App.ModsDir,previousName)) ||
                File.ReadAllText(Path.Combine(previousPool,previousName))!="previous profile bytes") throw new Exception("Migration must snapshot the selected source profile after synchronizing the previous active pool.");
            Console.WriteLine("PASS migration snapshots the selected source pool and preserves the previous profile before confirmation");
            return 2;
        }
        finally
        {
            release.Set(); competing?.GetAwaiter().GetResult();
            SynchronizationContext.SetSynchronizationContext(originalContext);
            foreach(var path in new[] { Path.Combine(App.ModsDir,sourceName),Path.Combine(App.ModsDir,previousName),Path.Combine(sourcePool,sourceName),Path.Combine(previousPool,previousName) })
                if(File.Exists(path)) File.Delete(path);
            foreach(var path in Directory.GetFiles(saved)) File.Move(path,Path.Combine(App.ModsDir,Path.GetFileName(path)));
            foreach(var id in new[] { "95.8","forge-95.8" })
            {
                var folder=Path.Combine(App.GameDir,"versions",id);
                File.Delete(Path.Combine(folder,id+".json")); File.Delete(Path.Combine(folder,id+".jar"));
                if(Directory.Exists(folder)) Directory.Delete(folder);
            }
            window.Config=original; ConfigManager.Save(original);
        }
    }

    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach(var descendant in Nodes(child)) yield return descendant;
    }
    sealed class PausedMigrationContext : SynchronizationContext,IDisposable
    {
        public readonly ManualResetEventSlim Posted=new();
        // Stop at the confirmation boundary; no modal dialog or download belongs in this source-snapshot test.
        public override void Post(SendOrPostCallback callback,object? state)=>Posted.Set();
        public void Dispose()=>Posted.Dispose();
    }
    sealed class EmptyCatalog : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
            =>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"hits\":[],\"total_hits\":0}") });
    }
}
