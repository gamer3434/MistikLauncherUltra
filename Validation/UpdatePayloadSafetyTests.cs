using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher.Updates;

static class UpdatePayloadSafetyTests
{
    public static int Run(string root)
    {
        var payload=Path.Combine(root,"linked-payload"); Directory.CreateDirectory(payload);
        var names=new[] { "MistikLauncher.exe", "MistikLauncher.dll", "MistikUpdater.exe" };
        foreach(var name in names) File.WriteAllText(Path.Combine(payload,name),"verified fixture "+name);
        var manifest=new UpdateManifest("MistikLauncher","7.0.0",names.Select(name=>new UpdateFile(name,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(payload,name)))))).ToArray());
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),JsonSerializer.Serialize(manifest));
        var outside=Path.Combine(root,"outside-payload"); Directory.CreateDirectory(outside);
        var link=Path.Combine(payload,"unlisted-directory");
        var command=new ProcessStartInfo("cmd.exe") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardError=true, RedirectStandardOutput=true };
        foreach(var argument in new[] { "/d", "/c", "mklink", "/J", link, outside }) command.ArgumentList.Add(argument);
        using(var process=Process.Start(command)!)
        {
            process.WaitForExit();
            if(process.ExitCode!=0) throw new Exception("Could not create isolated payload junction: "+process.StandardError.ReadToEnd());
        }
        try
        {
            bool rejected=false;
            try { UpdateEngine.Verify(payload); } catch(InvalidDataException) { rejected=true; }
            if(!rejected || Directory.GetFileSystemEntries(outside).Length!=0) throw new Exception("Payload verification must reject an unlisted directory junction without traversing it.");
            Console.WriteLine("PASS payload verification rejects unlisted directory links before recursive inventory");
        }
        finally { Directory.Delete(link); }
        if(UpdateEngine.Verify(payload).Version!="7.0.0") throw new Exception("Removing the junction must leave the valid payload intact.");
        Console.WriteLine("PASS rejected linked payload preserves all verified package files and the external directory");
        return 2;
    }
}
