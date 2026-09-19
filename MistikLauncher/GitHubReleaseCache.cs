using System.Net;
using System.Net.Http;
using System.IO;

namespace MistikLauncher;

/// <summary>Reduces GitHub API calls and keeps the last verified public release metadata.</summary>
public static class GitHubReleaseCache
{
    sealed class Entry
    {
        public required string Json;
        public string? ETag;
        public DateTimeOffset SavedAt;
    }
    static readonly TimeSpan StaleLimit = TimeSpan.FromDays(7);

    public static async Task<string> GetAsync(HttpClient client, string endpoint, string cachePath, CancellationToken cancellationToken)
    {
        var cached = Read(cachePath);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(cached?.ETag))
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
                return cached.Json;

            if (IsTransientLimit(response.StatusCode) && Usable(cached))
                return cached!.Json;

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (json.Length == 0 || json.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("GitHub release metadata is invalid.");
            Write(cachePath, new Entry { Json=json, ETag=response.Headers.ETag?.Tag, SavedAt=DateTimeOffset.UtcNow });
            return json;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (Usable(cached)) return cached!.Json;
            throw;
        }
    }

    static bool IsTransientLimit(HttpStatusCode status) =>
        status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests || (int)status >= 500;

    static bool Usable(Entry? entry) => entry is not null && DateTimeOffset.UtcNow - entry.SavedAt <= StaleLimit;

    static Entry? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var text=File.ReadAllText(path);
            int first=text.IndexOf('\n');
            int second=first<0 ? -1 : text.IndexOf('\n',first+1);
            if(first<=0 || second<=first) return null;
            if(!long.TryParse(text[..first],out var ticks)) return null;
            var json=text[(second+1)..];
            if(json.Length==0 || json.Length>8*1024*1024) return null;
            return new Entry { SavedAt=new DateTimeOffset(ticks,TimeSpan.Zero), ETag=text[(first+1)..second], Json=json };
        }
        catch { return null; }
    }

    static void Write(string path, Entry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, $"{entry.SavedAt.UtcTicks}\n{entry.ETag ?? string.Empty}\n{entry.Json}");
            File.Move(temporary, path, true);
        }
        catch { /* Cache failure must never block a release check. */ }
    }
}
