using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MistikLauncher.Updates;

namespace MistikLauncher;

public static class JavaRuntimeInstaller
{
    public static bool IsUsableRuntime(string directory, int major)
    {
        try
        {
            string release = UpdateEngine.SafePath(directory, "release");
            string? version = File.ReadLines(release).FirstOrDefault(line => line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal));
            if (version == null || !int.TryParse(version.Split('=')[1].Trim('"').Split('.')[0], out int actual) || actual != major) return false;
            return IsWindowsExecutable(UpdateEngine.SafePath(directory, "bin/java.exe")) &&
                IsWindowsExecutable(UpdateEngine.SafePath(directory, "bin/server/jvm.dll")) &&
                IsWindowsExecutable(UpdateEngine.SafePath(directory, "bin/java.dll")) &&
                IsWindowsExecutable(UpdateEngine.SafePath(directory, "bin/jli.dll")) &&
                new FileInfo(UpdateEngine.SafePath(directory, "lib/modules")).Length > 0;
        }
        catch { return false; }
    }

    static bool IsWindowsExecutable(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.BaseStream.Length < 256 || reader.ReadUInt16() != 0x5a4d) return false;
        reader.BaseStream.Position = 0x3c;
        int offset = reader.ReadInt32();
        if (offset < 64 || offset > reader.BaseStream.Length - 24) return false;
        reader.BaseStream.Position = offset;
        return reader.ReadUInt32() == 0x4550 && reader.ReadUInt16() == 0x8664;
    }

    public static async Task<string> InstallAsync(string javaDirectory, int major, Action<double, string>? progress = null,
        HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        if (major is not (21 or 25)) throw new ArgumentOutOfRangeException(nameof(major));
        string root = Path.GetFullPath(javaDirectory);
        string target = UpdateEngine.SafePath(root, "jre" + major);
        Directory.CreateDirectory(root);
        // A file lock also serializes installers in separate launcher processes.
        using var installLock = File.Open(UpdateEngine.SafePath(root, ".java-install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (IsUsableRuntime(target, major)) return Path.Combine(target, "bin", "java.exe");
        string stage = UpdateEngine.SafePath(root, ".java-stage-" + Guid.NewGuid().ToString("N"));
        string backup = UpdateEngine.SafePath(root, ".java-backup-" + Guid.NewGuid().ToString("N"));
        bool ownsClient = client == null;
        client ??= new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(client.Timeout == Timeout.InfiniteTimeSpan ? TimeSpan.FromMinutes(10) : client.Timeout);
        var token = timeout.Token;
        bool movedOld = false, committed = false;
        try
        {
            Directory.CreateDirectory(stage);
            progress?.Invoke(5, $"Java {major} indiriliyor...");
            string metadataUrl = $"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse&release_type=ga";
            using var metadata = await client.GetAsync(metadataUrl, HttpCompletionOption.ResponseHeadersRead, token);
            metadata.EnsureSuccessStatusCode();
            using var metadataStream = new MemoryStream();
            await CopyBoundedAsync(metadata, metadataStream, 2 * 1024 * 1024, token);
            using var json = JsonDocument.Parse(metadataStream.ToArray());
            var asset = json.RootElement.EnumerateArray().First(item => item.GetProperty("version").GetProperty("major").GetInt32() == major);
            var binary = asset.GetProperty("binary");
            if (binary.GetProperty("architecture").GetString() != "x64" || binary.GetProperty("os").GetString() != "windows" || binary.GetProperty("image_type").GetString() != "jre")
                throw new InvalidDataException("Adoptium returned an incompatible Java runtime.");
            var package = binary.GetProperty("package");
            string hash = package.GetProperty("checksum").GetString() ?? "";
            long size = package.GetProperty("size").GetInt64();
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || size <= 0 || size > 512L * 1024 * 1024)
                throw new InvalidDataException("Invalid Java archive checksum or size.");
            var uri = new Uri(package.GetProperty("link").GetString() ?? "");
            ValidateDownloadUri(uri, major, false);
            string zip = UpdateEngine.SafePath(stage, "runtime.zip");
            using (var response = await DownloadResponseAsync(client, uri, major, token))
            using (var output = File.Create(zip))
                if (await CopyBoundedAsync(response, output, size, token) != size) throw new InvalidDataException("Incomplete Java archive.");
            using (var file = File.OpenRead(zip))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Java archive checksum mismatch.");
            progress?.Invoke(80, $"Java {major} kuruluyor...");
            string extracted = UpdateEngine.SafePath(stage, "extracted");
            Directory.CreateDirectory(extracted);
            await Task.Run(() => Extract(zip, extracted, token), token);
            string[] candidates = Directory.GetDirectories(extracted);
            if (candidates.Length != 1 || Directory.GetFiles(extracted).Length != 0 || !IsUsableRuntime(candidates[0], major))
                throw new InvalidDataException("Java archive does not contain the requested complete Windows runtime.");
            token.ThrowIfCancellationRequested();
            if (Directory.Exists(target))
            {
                EnsureIdle(target);
                Directory.Move(target, backup);
                movedOld = true;
            }
            try { Directory.Move(candidates[0], target); committed = true; }
            catch
            {
                if (movedOld) { Directory.Move(backup, target); movedOld = false; }
                throw;
            }
            progress?.Invoke(100, $"Java {major} başarıyla kuruldu!");
            return Path.Combine(target, "bin", "java.exe");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("Java download or extraction timed out."); }
        finally
        {
            Cleanup(stage);
            // A failed rollback keeps the original runtime available for recovery.
            if (committed && movedOld) Cleanup(backup);
            if (ownsClient) client.Dispose();
        }
    }

    static void ValidateDownloadUri(Uri uri, int major, bool redirected)
    {
        bool official = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith($"/adoptium/temurin{major}-binaries/releases/download/", StringComparison.Ordinal);
        bool storage = redirected && (uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase));
        if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || (!official && !storage))
            throw new InvalidDataException("Untrusted Java download URL.");
    }

    static async Task<HttpResponseMessage> DownloadResponseAsync(HttpClient client, Uri uri, int major, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            ValidateDownloadUri(uri, major, redirects > 0);
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            // Injected clients may follow redirects; validate the final response as well.
            try { if (response.RequestMessage?.RequestUri is Uri final) ValidateDownloadUri(final, major, true); }
            catch { response.Dispose(); throw; }
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location == null) throw new InvalidDataException("Missing Java download redirect.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many Java download redirects.");
    }

    static async Task<long> CopyBoundedAsync(HttpResponseMessage response, Stream output, long limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Java response exceeds its expected size.");
        using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) != 0)
        {
            total += read;
            if (total > limit) throw new InvalidDataException("Java response exceeds its expected size.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return total;
    }

    static void Extract(string zip, string directory, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(zip);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        if (archive.Entries.Count > 10000) throw new InvalidDataException("Too many Java archive entries.");
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            string relative = entry.FullName.TrimEnd('/');
            string destination = UpdateEngine.SafePath(directory, relative);
            if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || !paths.Add(relative))
                throw new InvalidDataException("Java archive contains links or duplicate paths.");
            total += entry.Length;
            if (total > 1024L * 1024 * 1024) throw new InvalidDataException("Java archive is too large.");
            if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(destination);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920]; int read;
                while ((read = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
            }
        }
    }

    static void EnsureIdle(string target)
    {
        string prefix = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string name in new[] { "java", "javaw" })
            foreach (var process in Process.GetProcessesByName(name))
                using (process)
                {
                    string? executable = null;
                    try { executable = process.MainModule?.FileName; } catch { }
                    if (executable?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                        throw new IOException("Java is running. Close the game before repairing this runtime.");
                }
        foreach (string file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
        {
            string safe = UpdateEngine.SafePath(target, Path.GetRelativePath(target, file).Replace('\\', '/'));
            using var unlocked = File.Open(safe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }

    static void Cleanup(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            UpdateEngine.SafePath(directory, "cleanup-check");
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                UpdateEngine.SafePath(directory, Path.GetRelativePath(directory, file).Replace('\\', '/'));
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(directory, true);
        }
        catch (Exception ex) { App.Log($"Java temporary directory cleanup failed: {ex.Message}"); }
    }
}
