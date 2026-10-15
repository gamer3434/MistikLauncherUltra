using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;

static class ReleaseCacheTests
{
    public static async Task<int> Run(string root)
    {
        int checks=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        string path=Path.Combine(root,"release-cache.json");
        const string metadata="{\"tag_name\":\"v6.0.4\",\"draft\":false,\"prerelease\":false,\"assets\":[]}";
        using var first=new HttpClient(new Response(HttpStatusCode.OK,metadata,"\"cache-1\""));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(first,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache writes successful response");
        using var notModified=new HttpClient(new Response(HttpStatusCode.NotModified,"",null));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(notModified,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache handles ETag response");
        using var limited=new HttpClient(new Response(HttpStatusCode.Forbidden,"rate limit",null));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache survives API rate limits");
        foreach (var invalidBody in new[] { "<html>Temporary proxy error</html>", "null", "[]", "{}", "{\"message\":\"temporary API error\"}" })
        {
            var saved=File.ReadAllBytes(path);
            using var invalid=new HttpClient(new Response(HttpStatusCode.OK,invalidBody,"\"invalid-cache\""));
            bool rejected=false;
            try { await MistikLauncher.GitHubReleaseCache.GetAsync(invalid,"https://example.invalid/releases/latest",path,CancellationToken.None); }
            catch (InvalidDataException) { rejected=true; }
            Check(rejected && File.ReadAllBytes(path).SequenceEqual(saved),"malformed release responses preserve the healthy cached metadata and ETag");
        }
        await File.WriteAllTextAsync(path,$"{DateTimeOffset.UtcNow.AddDays(-8).UtcTicks}\n\"cache-1\"\n{metadata}");
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(notModified,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata &&
            await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,
            "ETag validation renews old metadata for fallback during a subsequent API outage");
        bool surfaced=false;
        try { await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,CancellationToken.None,false); }
        catch(HttpRequestException) { surfaced=true; }
        Check(surfaced,"update checks can bypass stale metadata and use an independent release source");
        await File.WriteAllTextAsync(path,$"{DateTimeOffset.UtcNow.AddDays(8).UtcTicks}\n\"cache-1\"\n{metadata}");
        surfaced=false;
        try { await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,CancellationToken.None); }
        catch(HttpRequestException) { surfaced=true; }
        Check(surfaced,"future-dated release metadata cannot bypass the maximum stale age");
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        bool stopped=false;
        try { await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,cancelled.Token); }
        catch(OperationCanceledException) { stopped=true; }
        Check(stopped,"cancelled release request cannot succeed from cached metadata");
        return checks;
    }

    sealed class Response(HttpStatusCode status,string body,string? etag) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var response=new HttpResponseMessage(status) { Content=new StringContent(body) };
            if(etag!=null) response.Headers.ETag=new EntityTagHeaderValue(etag);
            return Task.FromResult(response);
        }
    }
}
