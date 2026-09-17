using System.IO;
namespace MistikLauncher;
public static class ModFiles
{
    static void EnsureRoot(string root)
    {
        Directory.CreateDirectory(root);
        for(string? current=Path.GetFullPath(root);current!=null;current=Path.GetDirectoryName(current))
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException(Localization.T("modToggleInvalid"));
    }
    public static Uri DownloadUrl(string raw)
    {
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var url) || url.Scheme!="https" || !url.IsDefaultPort || url.UserInfo.Length!=0 || !url.Host.Equals("cdn.modrinth.com",StringComparison.OrdinalIgnoreCase)) throw new IOException(Localization.T("modToggleInvalid"));
        return url;
    }
    public static string Install(string root,string filename,byte[] bytes,string? sha512)
    {
        if(string.IsNullOrWhiteSpace(filename) || filename!=Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || !filename.EndsWith(".jar",StringComparison.OrdinalIgnoreCase) || bytes.Length==0 || bytes.Length>128*1024*1024 ||
            sha512==null || !System.Text.RegularExpressions.Regex.IsMatch(sha512,@"^[a-fA-F0-9]{128}$") || !Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(bytes)).Equals(sha512,StringComparison.OrdinalIgnoreCase)) throw new IOException(Localization.T("modDownloadInvalid"));
        EnsureRoot(root);
        string target=Path.Combine(root,filename);
        if(File.Exists(target) && File.Exists(target+".disabled")) throw new IOException(Localization.T("modToggleConflict"));
        if(File.Exists(target+".disabled")) target+=".disabled";
        if(File.Exists(target) && (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0) throw new IOException(Localization.T("modToggleInvalid"));
        string temporary=Path.Combine(root,".download-"+Guid.NewGuid().ToString("N")+".tmp");
        try {
            File.WriteAllBytes(temporary,bytes);
            if(File.Exists(target)) {
                string backup=Path.Combine(root,".backups"); EnsureRoot(backup);
                File.Replace(temporary,target,Path.Combine(backup,Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(target)));
            } else File.Move(temporary,target);
            return target;
        } finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    public static IEnumerable<string> List(string root)=>Directory.Exists(root)?Directory.GetFiles(root).Where(p=>p.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jar.disabled",StringComparison.OrdinalIgnoreCase)):Array.Empty<string>();
    public static bool Enabled(string path)=>path.EndsWith(".jar",StringComparison.OrdinalIgnoreCase);
    public static void SyncPools(string active,string previous,string? next)
    {
        if(next!=null && string.Equals(Path.GetFullPath(previous),Path.GetFullPath(next),StringComparison.OrdinalIgnoreCase)) return;
        foreach(var root in new[]{active,previous,next}.OfType<string>()) EnsureRoot(root);
        var moved=new List<(string from,string to)>();
        void Move(string from,string to) { if((File.GetAttributes(from)&FileAttributes.ReparsePoint)!=0) throw new IOException(Localization.T("modToggleInvalid")); File.Move(from,to); moved.Add((from,to)); }
        try {
            foreach(var file in List(active)) Move(file,Path.Combine(previous,Path.GetFileName(file)));
            if(next!=null) foreach(var file in List(next)) Move(file,Path.Combine(active,Path.GetFileName(file)));
        } catch(Exception original) {
            var failures=new List<Exception>{original};
            foreach(var move in moved.AsEnumerable().Reverse()) { try { File.Move(move.to,move.from); } catch(Exception error) { failures.Add(error); } }
            if(failures.Count>1) throw new AggregateException("Mod transfer rollback incomplete; all remaining files are retained.",failures);
            throw;
        }
    }
    public static string Toggle(string root,string path)
    {
        root=Path.GetFullPath(root); path=Path.GetFullPath(path);
        if(!string.Equals(Path.GetDirectoryName(path),root,StringComparison.OrdinalIgnoreCase) || !List(root).Contains(path,StringComparer.OrdinalIgnoreCase)) throw new IOException(Localization.T("modToggleInvalid"));
        for(string? current=path;current!=null;current=Path.GetDirectoryName(current))
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException(Localization.T("modToggleInvalid"));
        string target=Enabled(path)?path+".disabled":path[..^9];
        if(File.Exists(target)) throw new IOException(Localization.T("modToggleConflict"));
        File.Move(path,target); return target;
    }
}
