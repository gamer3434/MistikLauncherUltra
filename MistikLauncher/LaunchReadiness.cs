using System.IO;

namespace MistikLauncher;

public sealed record LaunchReadinessSnapshot(
    string Version,
    bool VersionInstalled,
    int RequiredJava,
    string? JavaPath,
    int JavaMajor,
    int AllocatedRamGb,
    int TotalRamGb,
    long FreeDiskGb,
    int EnabledMods,
    int DisabledMods)
{
    public bool JavaReady => JavaPath != null && JavaMajor >= RequiredJava;
    public bool MemoryReady => TotalRamGb <= 0 || AllocatedRamGb <= Math.Max(1, TotalRamGb - 2);
    public bool DiskReady => FreeDiskGb < 0 || FreeDiskGb >= 2;
    public bool Ready => VersionInstalled && JavaReady && MemoryReady && DiskReady;
    public string RecommendedPage => !VersionInstalled ? "Vers" : !JavaReady || !MemoryReady ? "Settings" : "";
}

public static class LaunchReadiness
{
    public static int ClampRamMb(int requestedGb, ulong totalMemoryBytes)
    {
        int requestedMb = Math.Clamp(requestedGb, 1, 32) * 1024;
        if (totalMemoryBytes == 0) return requestedMb;
        ulong totalMb = totalMemoryBytes / (1024 * 1024);
        ulong availableMb = totalMb > 2048 ? totalMb - 2048 : totalMb / 2;
        return (int)Math.Min((ulong)requestedMb, Math.Max(256UL, availableMb));
    }

    public static LaunchReadinessSnapshot Evaluate(
        string gameRoot,
        string version,
        int allocatedRamGb,
        string? javaPath,
        int javaMajor,
        ulong totalMemoryBytes,
        long freeDiskBytes)
    {
        int totalRamGb = totalMemoryBytes == 0 ? 0 : (int)Math.Max(1, totalMemoryBytes / 1024d / 1024d / 1024d);
        long freeDiskGb = freeDiskBytes < 0 ? -1 : (long)(freeDiskBytes / 1024d / 1024d / 1024d);
        int enabled = 0, disabled = 0;
        var mods = Path.Combine(gameRoot, "mods");
        try
        {
            foreach (var file in ModFiles.List(mods))
            {
                if (ModFiles.Enabled(file)) enabled++;
                else disabled++;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        bool installed = !string.IsNullOrWhiteSpace(version) && GameProfiles.IsInstalled(gameRoot, version);
        int requiredJava = installed ? GameProfiles.RequiredJava(gameRoot, version) : 0;
        return new LaunchReadinessSnapshot(version, installed, requiredJava, javaPath, javaMajor,
            Math.Max(1, allocatedRamGb), totalRamGb, freeDiskGb, enabled, disabled);
    }

    public static long FreeDiskBytes(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return string.IsNullOrWhiteSpace(root) ? -1 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (IOException) { return -1; }
        catch (UnauthorizedAccessException) { return -1; }
        catch (ArgumentException) { return -1; }
    }
}
