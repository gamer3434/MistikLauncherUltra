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
        const string metadata="{\"tag_name\":\"v6.0.4\",\"draft\":false,\"prerelease\":false}";
        using var first=new HttpClient(new Response(HttpStatusCode.OK,metadata,"\"cache-1\""));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(first,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache writes successful response");
        using var notModified=new HttpClient(new Response(HttpStatusCode.NotModified,"",null));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(notModified,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache handles ETag response");
        using var limited=new HttpClient(new Response(HttpStatusCode.Forbidden,"rate limit",null));
        Check(await MistikLauncher.GitHubReleaseCache.GetAsync(limited,"https://example.invalid/releases/latest",path,CancellationToken.None)==metadata,"release metadata cache survives API rate limits");
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
