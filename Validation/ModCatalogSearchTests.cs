using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class ModCatalogSearchTests
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var type = typeof(App).Assembly.GetType("MistikLauncher.ModCatalogSearch")!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        string? Slug(string value) => (string?)type.GetMethod("ProjectSlug", flags)!.Invoke(null, new object[] { value });
        Check(Slug("https://modrinth.com/mod/new-mod/version/abc12345") == "new-mod" &&
            Slug("https://modrinth.com/project/abcd1234") == "abcd1234", "new projects accept official Modrinth links without requiring search indexing");
        Check(new[] { "https://modrinth.com.attacker.invalid/mod/test", "https://user@modrinth.com/mod/test", "file:///mod/test", "https://modrinth.com:444/mod/test", "https://modrinth.com/mod/a%2Fb" }
            .All(value => Slug(value) == null), "project links reject untrusted hosts, credentials, ports and encoded path separators");
        using var handler = new Handler(); using var client = new HttpClient(handler);
        Task<JObject> Search(string query, int offset = 0) => (Task<JObject>)type.GetMethod("SearchAsync", flags)!.Invoke(null,
            new object[] { client, query, new JArray(new JArray("project_type:mod")), "newest", offset, CancellationToken.None })!;
        var response = await Search("https://modrinth.com/mod/new-mod");
        var hit = response["hits"]![0]!;
        Check(handler.Requests.Single().AbsolutePath == "/v2/project/new-mod" && (string?)hit["project_id"] == "abcd1234" &&
            hit["versions"]!.Values<string>().Contains("1.21.11") && hit["categories"]!.Values<string>().Contains("fabric"),
            "direct lookup maps canonical project metadata to the existing catalog compatibility checks");
        handler.Status = HttpStatusCode.NotFound;
        bool refused = false; try { await Search("https://modrinth.com/mod/unpublished"); } catch (InvalidDataException) { refused = true; }
        Check(refused, "unpublished or inaccessible projects show an explicit error rather than a false empty catalog");
        handler.Status = HttpStatusCode.OK; handler.ProjectType = "datapack";
        refused = false; try { await Search("https://modrinth.com/project/data-pack"); } catch (InvalidDataException) { refused = true; }
        Check(refused, "datapacks are not presented as installable mod JARs");
        await Search("new mod", 20);
        var query = Uri.UnescapeDataString(handler.Requests.Last().Query);
        Check(query.Contains("query=new mod") && query.Contains("offset=20") && query.Contains("index=newest") && query.Contains("project_type:mod"),
            "ordinary search remains live, paginated and sorted using the Modrinth API");
        return checks;
    }

    sealed class Handler : HttpMessageHandler
    {
        public List<Uri> Requests = new();
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string ProjectType = "mod";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var body = request.RequestUri!.AbsolutePath == "/v2/search" ? new JObject { ["hits"] = new JArray(), ["total_hits"] = 0 } :
                new JObject { ["id"] = "abcd1234", ["project_type"] = ProjectType, ["game_versions"] = new JArray("1.21.11"),
                    ["categories"] = new JArray("magic"), ["loaders"] = new JArray("fabric") };
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body.ToString()) });
        }
    }
}
