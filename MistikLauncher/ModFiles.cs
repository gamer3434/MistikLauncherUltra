using System.IO;
namespace MistikLauncher;
public static class ModFiles
{
    public static IEnumerable<string> List(string root)=>Directory.Exists(root)?Directory.GetFiles(root).Where(p=>p.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jar.disabled",StringComparison.OrdinalIgnoreCase)):Array.Empty<string>();
    public static bool Enabled(string path)=>path.EndsWith(".jar",StringComparison.OrdinalIgnoreCase);
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
