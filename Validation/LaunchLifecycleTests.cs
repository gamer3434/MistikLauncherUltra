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
