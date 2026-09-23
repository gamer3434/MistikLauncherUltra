using System;
using System.IO;

namespace MistikLauncher;

public static class GameLogCleaner
{
    public static long CleanOldLogs(string gameDirectory, DateTime olderThanUtc)
    {
        var logs = Path.Combine(gameDirectory, "logs");
        if (!Directory.Exists(logs)) return 0;
        if ((File.GetAttributes(logs) & FileAttributes.ReparsePoint) != 0) return 0;
        long freedBytes = 0;
        foreach (var path in Directory.EnumerateFiles(logs))
        {
            var name = Path.GetFileName(path);
            if (name.Equals("latest.log", StringComparison.OrdinalIgnoreCase) ||
                !(name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                  name.EndsWith(".log.gz", StringComparison.OrdinalIgnoreCase))) continue;
            try
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc >= olderThanUtc) continue;
                var bytes = info.Length;
                info.Delete();
                freedBytes += bytes;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return freedBytes;
    }
}
