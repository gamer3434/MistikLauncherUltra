using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace MistikLauncher.Updates;
public sealed record UpdateFile(string Path,string Hash);
public sealed record UpdateManifest(string Product,string Version,UpdateFile[] Files);
public sealed record UpdatePlan(string Target,string Payload,int ParentId,long ParentStarted,string Language,string? ExpectedVersion=null);
public sealed record UpdateInstallState(string Product,string Root,string Version,string[] Files);
public static class UpdateEngine
{
    public static string SafePath(string directory,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative) || relative.Contains(':') || relative.Contains('\\') || relative.Split('/').Any(part=>part is "." or ".." or "" || part.TrimEnd(' ','.')!=part))
            throw new InvalidDataException("Unsafe package path.");
        string root=System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        string result=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
        if(!result.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes installation.");
        for(string? current=result;current!=null;current=System.IO.Path.GetDirectoryName(current))
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Directory links are not supported for automatic updates.");
        string first=relative.Split('/')[0];
        if(new[] {"game","servers","config.json","auto-mcs.exe","auto-mcs.version.json"}.Contains(first,StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Update cannot overwrite user data.");
        return result;
    }
    public static void Extract(string zip,string payload)
    {
        using var archive=ZipFile.OpenRead(zip);
        long total=0; var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(archive.Entries.Count>3000) throw new InvalidDataException("Too many package entries.");
        foreach(var entry in archive.Entries)
        {
            if(entry.FullName.EndsWith('/')) continue;
            total+=entry.Length;
            if(total>1024L*1024*1024 || !names.Add(entry.FullName)) throw new InvalidDataException("Invalid package structure.");
            string destination=SafePath(payload,entry.FullName);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination);
        }
        Verify(payload);
    }
    public static UpdateManifest Verify(string payload)
    {
        var manifest=JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(SafePath(payload,"update-manifest.json"))) ?? throw new InvalidDataException("Missing manifest.");
        if(manifest.Product!="MistikLauncher" || manifest.Files==null || manifest.Files.Length==0 || manifest.Files.Length>3000) throw new InvalidDataException("Wrong update product.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in manifest.Files)
        {
            if(!names.Add(item.Path) || item.Path.Equals("update-manifest.json",StringComparison.OrdinalIgnoreCase) || item.Path.Equals("install-state.json",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Duplicate or reserved manifest entry.");
            string path=SafePath(payload,item.Path);
            using var file=File.OpenRead(path);
            if(!Convert.ToHexString(SHA256.HashData(file)).Equals(item.Hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Payload digest mismatch.");
        }
        if(!new[] {"MistikLauncher.exe","MistikLauncher.dll","MistikUpdater.exe"}.All(names.Contains)) throw new InvalidDataException("Incomplete launcher package.");
        var actual=Directory.EnumerateFiles(payload,"*",SearchOption.AllDirectories).Select(path=>System.IO.Path.GetRelativePath(payload,path).Replace('\\','/')).Where(path=>path!="update-manifest.json");
        if(!actual.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(names)) throw new InvalidDataException("Unlisted payload file.");
        return manifest;
    }
    public static void Apply(string payload,string target,string? expectedVersion=null)
    {
        var manifest=Verify(payload);
        if(!string.IsNullOrWhiteSpace(expectedVersion) && !string.Equals(manifest.Version,expectedVersion.TrimStart('v'),StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update package version does not match the selected release.");
        if(!File.Exists(SafePath(target,"MistikLauncher.exe"))) throw new InvalidDataException("Launcher installation not found.");
        string installedManifest=SafePath(target,"update-manifest.json");
        if(File.Exists(installedManifest))
        {
            try
            {
                using var current=JsonDocument.Parse(File.ReadAllText(installedManifest));
                string? currentVersion=current.RootElement.GetProperty("Version").GetString();
                if(ReleaseNumber(manifest.Version) < ReleaseNumber(currentVersion))
                    throw new InvalidDataException("Downgrade blocked; the installed launcher is newer.");
            }
            catch(JsonException) { throw new InvalidDataException("Installed update manifest is invalid."); }
        }
        // Validate every destination before modifying any installed file.
        foreach(var item in manifest.Files) SafePath(target,item.Path);
        string backup=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(payload)!,"backup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(backup);
        var completed=new List<(string Destination,string? Backup)>();
        try
        {
            var replacements=manifest.Files.Select(item=>(item.Path,Source:SafePath(payload,item.Path))).ToList();
            replacements.Add(("update-manifest.json",SafePath(payload,"update-manifest.json")));
            string marker=SafePath(target,"install-state.json");
            if(File.Exists(marker))
            {
                using var state=JsonDocument.Parse(File.ReadAllText(marker));
                var record=state.RootElement;
                if(record.GetProperty("Product").GetString()!="MistikLauncher" || !System.IO.Path.GetFullPath(record.GetProperty("Root").GetString()!).Equals(System.IO.Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid installation record.");
                var owned=record.GetProperty("Files").EnumerateArray().Select(entry=>entry.GetString()!).ToArray();
                if(owned.Length>3000) throw new InvalidDataException("Invalid ownership record.");
                foreach(var path in owned) { SafePath(target,path); if(path.Equals("install-state.json",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid ownership record."); }
                string updated=backup+".install-state.json";
                File.WriteAllText(updated,JsonSerializer.Serialize(new UpdateInstallState("MistikLauncher",System.IO.Path.GetFullPath(target),manifest.Version,owned.Concat(manifest.Files.Select(item=>item.Path)).Append("update-manifest.json").Distinct(StringComparer.OrdinalIgnoreCase).ToArray())));
                replacements.Add(("install-state.json",updated));
            }
            foreach(var item in replacements)
            {
                string destination=SafePath(target,item.Path), source=item.Source;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                string? saved=null;
                if(File.Exists(destination)) { saved=SafePath(backup,item.Path); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(saved)!); File.Copy(destination,saved); }
                string temporary=destination+".update-"+Guid.NewGuid().ToString("N");
                try { File.Copy(source,temporary); File.Move(temporary,destination,true); }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
                completed.Add((destination,saved));
            }
            using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikLauncherUltra",true);
            if(key?.GetValue("InstallLocation") is string location && System.IO.Path.GetFullPath(location).Equals(System.IO.Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase)) key.SetValue("DisplayVersion",manifest.Version);
        }
        catch
        {
            foreach(var item in completed.AsEnumerable().Reverse())
                if(item.Backup!=null) File.Copy(item.Backup,item.Destination,true); else File.Delete(item.Destination);
            throw;
        }
        finally { if(File.Exists(backup+".install-state.json")) File.Delete(backup+".install-state.json"); }
    }

    static Version ReleaseNumber(string? value)
    {
        var number=(value??"").Trim().TrimStart('v');
        var dash=number.IndexOf('-'); if(dash>=0) number=number[..dash];
        return Version.TryParse(number,out var parsed)?parsed:new Version(0,0,0);
    }
}
