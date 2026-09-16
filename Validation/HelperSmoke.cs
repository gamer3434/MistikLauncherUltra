using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher.Updates;
static class HelperSmoke
{
    public static async Task Run(string root,string helper,string fixture)
    {
        string target=Path.Combine(root,"helper-target"),payload=Path.Combine(root,"helper-payload"); Directory.CreateDirectory(target); Directory.CreateDirectory(payload);
        File.Copy(fixture,Path.Combine(target,"MistikLauncher.exe"));
        File.Copy(fixture,Path.Combine(payload,"MistikLauncher.exe"));
        File.Copy(helper,Path.Combine(payload,"MistikUpdater.exe"));
        File.WriteAllText(Path.Combine(payload,"MistikLauncher.dll"),"updated fixture");
        var files=Directory.EnumerateFiles(payload).Select(path=>new UpdateFile(Path.GetFileName(path),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),JsonSerializer.Serialize(new UpdateManifest("MistikLauncher","7.0.0",files)));
        string plan=Path.Combine(root,"helper-plan.json");
        var start=new ProcessStartInfo(Path.Combine(target,"MistikLauncher.exe")) { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden }; start.ArgumentList.Add(plan);
        using var parent=Process.Start(start)!;
        try
        {
            for(int i=0;i<200&&!File.Exists(plan+".started");i++) await Task.Delay(100);
            if(!File.Exists(plan+".started")) throw new Exception("Fixture launcher did not start.");
            File.WriteAllText(plan,JsonSerializer.Serialize(new UpdatePlan(target,payload,parent.Id,parent.StartTime.ToUniversalTime().Ticks,"en")));
            var childStart=new ProcessStartInfo(helper) { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden }; childStart.ArgumentList.Add(plan);
            using var child=Process.Start(childStart)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await child.WaitForExitAsync(timeout.Token);
            if(child.ExitCode!=0) throw new Exception("Update helper failed.");
            for(int i=0;i<100&&!File.Exists(Path.Combine(target,"fixture-restarted"));i++) await Task.Delay(100);
            if(!File.Exists(plan+".ready") || !File.Exists(Path.Combine(target,"fixture-restarted")) || File.ReadAllText(Path.Combine(target,"MistikLauncher.dll"))!="updated fixture")
                throw new Exception("Update helper handoff/apply/restart incomplete.");
            Console.WriteLine("PASS real update helper handshake, parent exit, file replacement and restart");
        }
        finally { if(!parent.HasExited) parent.Kill(); }
    }
}
