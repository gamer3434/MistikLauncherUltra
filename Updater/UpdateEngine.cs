using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace MistikLauncher.Updates;
public sealed record UpdateFile(string Path,string Hash);
public sealed record UpdateManifest(string Product,string Version,UpdateFile[] Files);
public sealed record UpdatePlan(string Target,string Payload,int ParentId,long ParentStarted,string Language);
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
        if(manifest.Product!="MistikLauncher" || manifest.Files==null || manifest.Files.Length==0) throw new InvalidDataException("Wrong update product.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in manifest.Files)
        {
            if(!names.Add(item.Path) || item.Path=="update-manifest.json") throw new InvalidDataException("Duplicate manifest entry.");
            string path=SafePath(payload,item.Path);
            using var file=File.OpenRead(path);
            if(!Convert.ToHexString(SHA256.HashData(file)).Equals(item.Hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Payload digest mismatch.");
        }
        if(!new[] {"MistikLauncher.exe","MistikLauncher.dll","MistikUpdater.exe"}.All(names.Contains)) throw new InvalidDataException("Incomplete launcher package.");
        var actual=Directory.EnumerateFiles(payload,"*",SearchOption.AllDirectories).Select(path=>System.IO.Path.GetRelativePath(payload,path).Replace('\\','/')).Where(path=>path!="update-manifest.json");
        if(!actual.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(names)) throw new InvalidDataException("Unlisted payload file.");
        return manifest;
    }
    public static void Apply(string payload,string target)
    {
        var manifest=Verify(payload);
        if(!File.Exists(SafePath(target,"MistikLauncher.exe"))) throw new InvalidDataException("Launcher installation not found.");
        // Validate every destination before modifying any installed file.
        foreach(var item in manifest.Files) SafePath(target,item.Path);
        string backup=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(payload)!,"backup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(backup);
        var completed=new List<(string Destination,string? Backup)>();
        try
        {
            foreach(var item in manifest.Files)
            {
                string destination=SafePath(target,item.Path), source=SafePath(payload,item.Path);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                string? saved=null;
                if(File.Exists(destination)) { saved=SafePath(backup,item.Path); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(saved)!); File.Copy(destination,saved); }
                string temporary=destination+".update-"+Guid.NewGuid().ToString("N");
                try { File.Copy(source,temporary); File.Move(temporary,destination,true); }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
                completed.Add((destination,saved));
            }
        }
        catch
        {
            foreach(var item in completed.AsEnumerable().Reverse())
                if(item.Backup!=null) File.Copy(item.Backup,item.Destination,true); else File.Delete(item.Destination);
            throw;
        }
    }
}
