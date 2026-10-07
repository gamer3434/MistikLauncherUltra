using System.IO;
using MistikLauncher;

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
}
