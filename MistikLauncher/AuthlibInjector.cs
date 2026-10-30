using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace MistikLauncher;

public static class AuthlibInjector
{
    public const string Version = "1.2.8";
    const string Digest = "9c7f4343e6c82034958ffb48c14a2cb0c85928be7283103ce17da00c6d5a7b10";
    static readonly string[] Sources = {
        "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.8/authlib-injector-1.2.8.jar",
        "https://authlib-injector.yushi.moe/artifact/56/authlib-injector-1.2.8.jar"
    };

    static bool Verified(string path)
    {
        if (!File.Exists(path)) return false;
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).Equals(Digest, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> EnsureAsync(string directory, HttpClient client, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "authlib-injector.jar");
        if (Verified(target)) return target;
        // Serialize replacement across launcher processes; never overwrite the working JAR with a partial download.
        using var installLock = File.Open(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Verified(target)) return target;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            foreach (string source in Sources)
            {
                try
                {
                    using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    const int limit = 8 * 1024 * 1024;
                    if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Injector download is too large.");
                    await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    await using (var output = File.Create(temporary))
                    {
                        byte[] buffer = new byte[81920]; int read, total = 0;
                        while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                        {
                            total += read;
                            if (total > limit) throw new InvalidDataException("Injector download is too large.");
                            await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                        }
                    }
                    if (!Verified(temporary)) throw new InvalidDataException("Injector SHA-256 mismatch.");
                    token.ThrowIfCancellationRequested();
                    if (File.Exists(target)) File.Replace(temporary, target, target + ".bak");
                    else File.Move(temporary, target);
                    return target;
                }
                catch (HttpRequestException) when (source != Sources[^1]) { }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            throw new IOException("Injector download failed.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
