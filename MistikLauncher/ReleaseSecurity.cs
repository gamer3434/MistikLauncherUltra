using System.IO;
namespace MistikLauncher;
public static class ReleaseSecurity
{
    public static bool AutomaticUpdatesEnabled => false;
    public static string ValidateUninstallTarget(string candidate)
    {
        var expected = Path.GetFullPath(App.AppData).TrimEnd(Path.DirectorySeparatorChar);
        var actual = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Uninstall target is outside the application directory.");
        var directory = new DirectoryInfo(actual);
        for (var parent = directory; parent != null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Uninstall through a junction is forbidden.");
        if (directory.Exists && directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Any(item => (item.Attributes & FileAttributes.ReparsePoint) != 0))
            throw new InvalidOperationException("Remove directory links before uninstalling.");
        return actual;
    }
}
