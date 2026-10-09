using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class LaunchReadinessTests
{
    public static int Run(string root)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }

        string game=Path.Combine(root,"game");
        string version="1.21-ready";
        string versionRoot=Path.Combine(game,"versions",version);
        Directory.CreateDirectory(versionRoot);
        File.WriteAllText(Path.Combine(versionRoot,version+".json"),"{\"javaVersion\":{\"majorVersion\":21}}");
        File.WriteAllText(Path.Combine(versionRoot,version+".jar"),"client");
        string mods=Path.Combine(game,"mods"); Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods,"enabled.jar"),"enabled");
        File.WriteAllText(Path.Combine(mods,"disabled.jar.disabled"),"disabled");

        ulong sixteenGb=16UL*1024*1024*1024;
        long tenGb=10L*1024*1024*1024;
        var ready=LaunchReadiness.Evaluate(game,version,6,"java.exe",21,sixteenGb,tenGb);
        Check(ready.Ready && ready.VersionInstalled && ready.JavaReady && ready.EnabledMods==1 && ready.DisabledMods==1,"launch readiness summarizes a healthy local setup and mod states");

        var oldJava=LaunchReadiness.Evaluate(game,version,6,"java.exe",17,sixteenGb,tenGb);
        Check(!oldJava.Ready && !oldJava.JavaReady && oldJava.RecommendedPage=="Settings","launch readiness routes an outdated Java runtime to Settings");

        var excessiveMemory=LaunchReadiness.Evaluate(game,version,15,"java.exe",21,sixteenGb,tenGb);
        Check(!excessiveMemory.MemoryReady && excessiveMemory.RecommendedPage=="Settings","launch readiness leaves memory for Windows");
        foreach (int totalGb in new[] { 2, 3, 4, 8 })
        {
            int heap=LaunchReadiness.ClampRamMb(totalGb,(ulong)totalGb*1024*1024*1024);
            Check(heap==Math.Max(1,totalGb-2)*1024 && heap<totalGb*1024,"RAM clamp reserves Windows memory on a "+totalGb+" GB system");
        }
        Check(LaunchReadiness.ClampRamMb(2,sixteenGb)==2048 && LaunchReadiness.ClampRamMb(6,0)==6144,"RAM clamp never raises a valid request and keeps it when physical memory detection is unavailable");

        var missing=LaunchReadiness.Evaluate(game,"missing",4,"java.exe",21,sixteenGb,tenGb);
        Check(!missing.VersionInstalled && missing.RecommendedPage=="Vers","launch readiness routes a missing profile to Versions");

        Check(CrashDiagnostics.Category("MLU-INTEGRITY: sha1 mismatch")=="MLU-INTEGRITY","integrity failures receive a dedicated diagnostic code");
        var suspects=CrashDiagnostics.SuspectedMods("ERROR failed mod file: enabled.jar\nLoaded mods: disabled.jar.disabled",mods);
        Check(suspects.Length==1 && Path.GetFileName(suspects[0])=="enabled.jar","safe recovery selects only enabled mods named by error evidence");
        var disabled=ModFiles.Toggle(mods,suspects[0]);
        Check(File.Exists(disabled) && !File.Exists(Path.Combine(mods,"enabled.jar")),"safe recovery disables a suspected mod without deleting its bytes");
        return checks;
    }

    public static int RunHome(MainWindow window)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(ModernHomePage page,string name)=>(T)typeof(ModernHomePage).GetField(name,flags)!.GetValue(page)!;
        Task Refresh(ModernHomePage page,Func<string,int,Task<LaunchReadinessSnapshot>> evaluate)=>(Task)typeof(ModernHomePage).GetMethod("RefreshReadinessAsync",flags,null,new[]{evaluate.GetType()},null)!.Invoke(page,new object[]{evaluate})!;
        Task Repair(ModernHomePage page,Func<string,Action<string>,Task<GameRuntimeHealthResult>> verify)=>(Task)typeof(ModernHomePage).GetMethod("VerifyAndRepairAsync",flags,null,new[]{verify.GetType()},null)!.Invoke(page,new object[]{verify})!;
        LaunchReadinessSnapshot Ready(string version,int ram)=>new(version,true,21,"fixture-java",21,ram,16,10,0,0);
        var previous=window.Config;
        try
        {
            window.Config=Newtonsoft.Json.JsonConvert.DeserializeObject<LauncherConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(previous))!;
            window.Config.Ram=4;
            foreach(bool success in new[]{false,true})
            {
                window.Config.Version="readiness-first";
                var page=new ModernHomePage(window);
                Refresh(page,(version,ram)=>Task.FromResult(Ready(version,ram))).GetAwaiter().GetResult();
                var completion=new TaskCompletionSource<GameRuntimeHealthResult>();
                Action<string>? progress=null; string? repairedVersion=null;
                var pending=Repair(page,(version,report)=> { repairedVersion=version; progress=report; return completion.Task; });
                Check(!pending.IsCompleted && repairedVersion=="readiness-first" && Field<LaunchReadinessSnapshot?>(page,"readiness")==null,"in-flight home repair captures its profile and invalidates the old readiness action");
                window.Config.Version="readiness-second";
                page.InvalidateReadiness();
                Refresh(page,(version,ram)=>Task.FromResult(Ready(version,ram))).GetAwaiter().GetResult();
                string state=Field<TextBlock>(page,"readinessState").Text;
                string details=Field<TextBlock>(page,"readinessDetails").Text;
                progress!("old profile progress");
                Check(Field<TextBlock>(page,"readinessDetails").Text==details,"late repair progress cannot overwrite the newly selected profile");
                completion.SetResult(new GameRuntimeHealthResult(success,0,"fixture",FailedPath:"old-profile.jar",FailureReason:"old profile failure"));
                pending.GetAwaiter().GetResult();
                Check(Field<TextBlock>(page,"readinessState").Text==state && Field<TextBlock>(page,"readinessDetails").Text==details && Field<LaunchReadinessSnapshot>(page,"readiness").Version=="readiness-second","late "+(success?"successful":"failed")+" repair cannot replace current profile readiness");
            }

            var retryPage=new ModernHomePage(window);
            Action<string>? lateProgress=null;
            Repair(retryPage,(_,report)=> { lateProgress=report; return Task.FromException<GameRuntimeHealthResult>(new IOException("fixture repair failure")); }).GetAwaiter().GetResult();
            lateProgress!("queued progress after failure");
            var retry=Field<Button>(retryPage,"readinessAction");
            Check(retry.IsEnabled && Equals(retry.Content,Localization.T("readinessRefresh")) && System.Windows.Automation.AutomationProperties.GetName(retry)==Localization.T("readinessRefresh") && Field<LaunchReadinessSnapshot?>(retryPage,"readiness")==null && Field<TextBlock>(retryPage,"readinessDetails").Text=="fixture repair failure","repair exceptions restore an accessible check-again action without stale routing or queued progress");

            var memoryPage=new ModernHomePage(window);
            var oldCheck=new TaskCompletionSource<LaunchReadinessSnapshot>();
            int capturedRam=0;
            var checking=Refresh(memoryPage,(version,ram)=> { capturedRam=ram; return oldCheck.Task; });
            window.Config.Ram=7;
            memoryPage.InvalidateReadiness();
            Refresh(memoryPage,(version,ram)=>Task.FromResult(Ready(version,ram))).GetAwaiter().GetResult();
            oldCheck.SetResult(Ready(window.Config.Version,capturedRam));
            checking.GetAwaiter().GetResult();
            Check(capturedRam==4 && Field<LaunchReadinessSnapshot>(memoryPage,"readiness").AllocatedRamGb==7,"same-profile settings invalidation rejects a delayed readiness result even while the page is hidden");

            window.Config.Version="1.20.1-forge-47.4.10";
            var compact=new ModernHomePage(window);
            Refresh(compact,(version,ram)=>Task.FromResult(Ready(version,ram))).GetAwaiter().GetResult();
            var scroll=(ScrollViewer)compact.Content;
            scroll.Measure(new Size(960,403)); scroll.Arrange(new Rect(0,0,960,403)); scroll.UpdateLayout();
            var layout=(StackPanel)scroll.Content;
            var card=layout.Children.OfType<Border>().Single(border=>border.Name=="HomeReadinessCard");
            var actions=layout.Children.OfType<WrapPanel>().Single(panel=>panel.Name=="HomeActions");
            Check(card.TranslatePoint(new Point(0,0),scroll).Y<actions.TranslatePoint(new Point(0,0),scroll).Y && card.TranslatePoint(new Point(0,card.ActualHeight),scroll).Y<=scroll.ViewportHeight && actions.TranslatePoint(new Point(0,actions.ActualHeight),scroll).Y<=scroll.ViewportHeight,"complete readiness card and quick actions fit the compact home viewport");
        }
        finally { window.Config=previous; }
        return checks;
    }
}
