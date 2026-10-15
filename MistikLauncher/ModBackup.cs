using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MistikLauncher;

public readonly record struct ModBackupResult(int Restored, int Skipped);

public static class ModBackup
{
    const int MaxMods = 2048;
    const long MaxModBytes = 128L * 1024 * 1024;
    const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;

    public static void Create(string modsDirectory, string archivePath)
    {
        ModFiles.EnsureRoot(modsDirectory);
        archivePath = Path.GetFullPath(archivePath);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a .zip backup file.");
        string? parent = Path.GetDirectoryName(archivePath);
        if (parent is null) throw new IOException("Invalid backup path.");
        if (File.Exists(archivePath) && (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid backup path.");

        var mods = ModFiles.List(modsDirectory).ToArray();
        if (mods.Length > MaxMods) throw new IOException($"A backup can contain up to {MaxMods} mods.");
        long totalBytes = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            var name = Path.GetFileName(mod);
            var enabledName = name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ? name[..^9] : name;
            if (!names.Add(enabledName)) throw new IOException("The mod folder contains conflicting enabled/disabled copies. Resolve the duplicate before creating a backup.");
            if ((File.GetAttributes(mod) & FileAttributes.ReparsePoint) != 0) throw new IOException("A mod file is not a regular file.");
            long size = new FileInfo(mod).Length;
            if (size <= 0 || size > MaxModBytes || (totalBytes += size) > MaxTotalBytes) throw new IOException("The mod backup contains an empty mod or exceeds its safe size limit.");
        }

        string temporary = Path.Combine(parent, ".mistik-mod-backup-" + Guid.NewGuid().ToString("N") + ".tmp");
        long totalWritten = 0;
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var mod in mods)
                {
                    var entry = archive.CreateEntry(Path.GetFileName(mod), CompressionLevel.Optimal);
                    using var input = new FileStream(mod, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = entry.Open();
                    var buffer = new byte[81920];
                    long copied = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (copied + read > MaxModBytes || totalWritten + copied + read > MaxTotalBytes)
                            throw new IOException("The mod backup exceeds its safe size limit.");
                        output.Write(buffer, 0, read);
                        copied += read;
                    }
                    totalWritten += copied;
                }
            }
            File.Move(temporary, archivePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static ModBackupResult Restore(string modsDirectory, string archivePath)
    {
        ModFiles.EnsureRoot(modsDirectory);
        archivePath = Path.GetFullPath(archivePath);
        if ((File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid backup file.");

        string stage = Path.Combine(modsDirectory, ".restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var staged = new List<string>();
        try
        {
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > MaxMods * 2) throw new IOException("The backup contains too many entries.");
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long totalBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName;
                    if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name != Path.GetFileName(name) || name.Contains('/') || name.Contains('\\') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsReservedWindowsName(name) || entry.Length <= 0)
                        throw new IOException("The backup contains an invalid mod path.");
                    var enabledName = name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ? name[..^9] : name;
                    if (!names.Add(enabledName)) throw new IOException("The backup contains duplicate or conflicting enabled/disabled mod names.");
                    if (staged.Count >= MaxMods || entry.Length > MaxModBytes || (totalBytes += entry.Length) > MaxTotalBytes)
                        throw new IOException("The mod backup exceeds its safe size limit.");

                    string path = Path.Combine(stage, name);
                    using var input = entry.Open();
                    using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    var buffer = new byte[81920];
                    long copied = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        copied += read;
                        if (copied > MaxModBytes || copied > entry.Length) throw new IOException("The backup contains an oversized mod file.");
                        output.Write(buffer, 0, read);
                    }
                    if (copied != entry.Length) throw new IOException("The backup contains a truncated mod file.");
                    staged.Add(path);
                }
            }

            var moved = new List<string>();
            int skipped = 0;
            try
            {
                foreach (var stagedPath in staged)
                {
                    string name = Path.GetFileName(stagedPath);
                    bool disabled = name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
                    string enabledName = disabled ? name[..^9] : name;
                    if (File.Exists(Path.Combine(modsDirectory, enabledName)) || File.Exists(Path.Combine(modsDirectory, enabledName + ".disabled")))
                    {
                        skipped++;
                        continue;
                    }
                    string destination = Path.Combine(modsDirectory, name);
                    File.Move(stagedPath, destination);
                    moved.Add(destination);
                }
            }
            catch
            {
                foreach (var destination in moved.AsEnumerable().Reverse())
                    try { File.Delete(destination); } catch { }
                throw;
            }
            return new ModBackupResult(moved.Count, skipped);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
    }

    static bool IsReservedWindowsName(string name)
    {
        int dot = name.IndexOf('.');
        string device = (dot < 0 ? name : name[..dot]).TrimEnd(' ', '.');
        if (device.Equals("CON", StringComparison.OrdinalIgnoreCase) || device.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            device.Equals("AUX", StringComparison.OrdinalIgnoreCase) || device.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            device.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || device.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)) return true;
        if (device.Length != 4 || !(device.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || device.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))) return false;
        return device[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3';
    }
}
