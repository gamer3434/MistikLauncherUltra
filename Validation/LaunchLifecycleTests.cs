using System.Reflection;
using System.IO;
using System.IO.Compression;
using System.Windows.Controls;
using MistikLauncher;

static class LaunchLifecycleTests
{
    public static int Run(MainWindow window)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        bool ChangeMods(string profile, Func<string, bool> change)
        {
            try { return (bool)typeof(MainWindow).GetMethod("WithModChanges", flags)!.MakeGenericMethod(typeof(bool)).Invoke(window, new object[] { profile, change })!; }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
        string ChangePool(string game, string loader)
        {
            try { return (string)typeof(MainWindow).GetMethod("WithModPoolChanges", flags)!.MakeGenericMethod(typeof(string)).Invoke(window, new object[] { game, loader, (Func<string, string>)(directory => directory) })!; }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
        var selected=(ComboBox)typeof(MainWindow).GetField("VerBox",flags)!.GetValue(window)!;
        var handler=typeof(MainWindow).GetMethod("HandleLaunch",flags)!;
        var original=window.Config.Version;
        var previous=selected.SelectedItem;
        var previousSynced=window.Config.LastSyncedVersion;
        int checks=0;
        try
        {
            var queue=typeof(MainWindow).GetMethod("QueueBackgroundModSync",flags)!;
            Task.Run(()=>queue.Invoke(window,null)).GetAwaiter().GetResult();
            var frame=new System.Windows.Threading.DispatcherFrame();
            window.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Console.WriteLine("PASS worker mod synchronization retry returns without accessing WPF controls"); checks++;
            foreach(var state in new[]{"launchPreparing","gameRunning"})
            {
                var field=typeof(MainWindow).GetField(state,flags)!;
                field.SetValue(window,true);
                try
                {
                    handler.Invoke(window,null);
                    if(window.Config.Version!=original || !(bool)field.GetValue(window)!) throw new Exception("Repeated launch altered an active launch.");
                    Console.WriteLine("PASS repeated launch is ignored while "+state); checks++;
                    var invoked = false;
                    bool rejected = false;
                    try { ChangeMods(original, _ => invoked = true); }
                    catch (IOException error) when (error.Message == Localization.T("modChangesBusy")) { rejected = true; }
                    if (!rejected || invoked || window.Config.LastSyncedVersion != previousSynced)
                        throw new Exception("Mod edits must stop before synchronization or writes while " + state);
                    Console.WriteLine("PASS mod changes are blocked before synchronization while " + state); checks++;
                    rejected = false;
                    try { ChangePool("1.21.1", "fabric"); }
                    catch (IOException error) when (error.Message == Localization.T("modChangesBusy")) { rejected = true; }
                    if (!rejected) throw new Exception("Queued pool writes must be blocked while " + state);
                    Console.WriteLine("PASS queued mod pool changes are blocked while " + state); checks++;
                }
                finally { field.SetValue(window,false); }
            }
            bool started=false;
            try { window.WithSyncedMods(original+"-changed",_=>started=true); }
            catch(IOException) { }
            if(started) throw new Exception("A changed profile reached process start.");
            Console.WriteLine("PASS changed profile cannot reach game process start"); checks++;
            var ensureJava = typeof(MainWindow).GetMethod("EnsureLaunchJavaAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (int required in new[] { 21, 25 })
            {
                var requested = new List<int>();
                Func<int, Task<string?>> installer = major => { requested.Add(major); return Task.FromResult<string?>("installed-java-" + major); };
                var result = ((Task<string?>)ensureJava.Invoke(null, new object?[] { null, required, installer })!).GetAwaiter().GetResult();
                if (requested.Count != 1 || requested[0] != required || result != "installed-java-" + required)
                    throw new Exception("Missing Java must request the profile's runtime once without downloading an intermediate runtime.");
            }
            Console.WriteLine("PASS missing Java21/25 installs the required runtime exactly once"); checks++;
            var priorFps = window.Config.OptFps;
            try
            {
                window.Config.OptFps = true;
                var tuningDirectory = Path.Combine(App.GameDir,"versions","startup-tuning");
                Directory.CreateDirectory(tuningDirectory);
                File.WriteAllText(Path.Combine(tuningDirectory,"startup-tuning.json"),"{\"id\":\"startup-tuning\",\"libraries\":[],\"mainClass\":\"net.fabricmc.loader.impl.launch.knot.KnotClient\"}");
                foreach (var ram in new[] { 1024, 4096, 8192 })
                {
                    var command = (string)typeof(MainWindow).GetMethod("BuildLaunchArgs", flags)!.Invoke(window, new object?[] { "startup-tuning", ram, Path.Combine(App.GameDir,"natives"), null, null })!;
                    if (!command.Contains("-Xmx" + ram + "m") || !command.Contains("-XX:+TieredCompilation") ||
                        new[] { "DontCompileHugeMethods", "MaxInlineSize", "MaxInlineLevel", "UseNUMA", "UseLargePages" }.Any(command.Contains))
                        throw new Exception("Launch tuning must preserve memory limits and use adaptive JIT defaults.");
                }
            }
            finally { window.Config.OptFps = priorFps; }
            Console.WriteLine("PASS launch tuning retains heap limits and tiered compilation without forcing large-method, inline, NUMA or large-page policies"); checks++;
            Directory.CreateDirectory(App.ModsDir);
            var runningJar = Path.Combine(App.ModsDir, "running-pool-guard.jar");
            File.WriteAllText(runningJar, "active game mod");
            var running = typeof(MainWindow).GetField("gameRunning", flags)!;
            running.SetValue(window, true);
            try
            {
                window.Config.Version = "fabric-1.21.1";
                window.Config.LastSyncedVersion = "forge-1.20.1";
                if (window.SyncModsForCurrentVersion() || File.ReadAllText(runningJar) != "active game mod" || window.Config.LastSyncedVersion != "forge-1.20.1")
                    throw new Exception("An active game must prevent selected-profile mod pool changes.");
            }
            finally { running.SetValue(window, false); File.Delete(runningJar); }
            Console.WriteLine("PASS active game preserves mods and defers profile synchronization"); checks++;
            window.Config.Version = "fabric-1.21.1";
            window.Config.LastSyncedVersion = window.Config.Version;
            var wrongLoader = Path.Combine(App.ModsDir, "late-wrong-loader.jar");
            using (var jar = ZipFile.Open(wrongLoader, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(jar.CreateEntry("META-INF/mods.toml").Open()))
                writer.Write("modLoader=\"javafml\"\nloaderVersion=\"[47,)\"\n[[mods]]\nmodId=\"late_wrong_loader\"");
            if (!window.SyncModsForCurrentVersion(verifyCompatibility: true) || File.Exists(wrongLoader))
                throw new Exception("Launch compatibility scan must suspend newly added wrong-loader jars even in an already synchronized profile.");
            var quarantined = Path.Combine(App.AppData, "mods_pool", "incompatible", "late-wrong-loader.jar");
            if (!File.Exists(quarantined)) throw new Exception("Wrong-loader mod must remain recoverable in its quarantine pool.");
            File.Delete(quarantined);
            if (!ChangeMods(window.Config.Version, directory => directory == App.ModsDir))
                throw new Exception("Idle mod actions must retain selected-profile access.");
            Console.WriteLine("PASS idle mod changes retain access to the synchronized selected profile"); checks++;
            if (ChangePool("1.21.1", "fabric") != App.ModsDir ||
                ChangePool("1.20.1", "forge") != Path.Combine(App.AppData, "mods_pool", "1.20.1_forge"))
                throw new Exception("Queued mod writes must resolve selected pools to the active folder and other profiles to their own pool.");
            Console.WriteLine("PASS queued writes follow a newly selected active pool and isolate other profiles"); checks++;
            Console.WriteLine("PASS launch rechecks same-profile wrong-loader mods and preserves quarantined bytes"); checks++;
            var serverPage=new MistikLauncher.Pages.ServerManagerPage(window);
            var serverType=serverPage.GetType();
            serverType.GetField("launching",flags)!.SetValue(serverPage,true);
            ((Task)serverType.GetMethod("Launch",flags)!.Invoke(serverPage,null)!).GetAwaiter().GetResult();
            serverType.GetMethod("Refresh",flags)!.Invoke(serverPage,null);
            if(((Button)serverType.GetField("launch",flags)!.GetValue(serverPage)!).IsEnabled || ((Button)serverType.GetField("update",flags)!.GetValue(serverPage)!).IsEnabled) throw new Exception("Refresh enabled a second server manager launch.");
            Console.WriteLine("PASS server manager refresh preserves in-flight launch and update guards"); checks++;
        }
        finally { window.Config.Version=original; window.Config.LastSyncedVersion=previousSynced; selected.SelectedItem=previous; }
        return checks;
    }
}
