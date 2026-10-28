using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

internal static class ModCatalogSearch
{
    internal static string? ProjectSlug(string query)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != "https" || !uri.Host.Equals("modrinth.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0) return null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[0] is "mod" or "project" &&
            Regex.IsMatch(segments[1], "^[A-Za-z0-9_-]{1,64}$") ? segments[1] : null;
    }

    internal static async Task<JObject> SearchAsync(HttpClient client, string query, JArray facets,
        string sort, int offset, CancellationToken cancellation)
    {
        if (ProjectSlug(query) is string slug)
        {
            // A public project can be available before the search index catches up.
            using var response = await client.GetAsync("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(slug), cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidDataException(Localization.T("modProjectUnavailable"));
            response.EnsureSuccessStatusCode();
            var project = JObject.Parse(await response.Content.ReadAsStringAsync(cancellation));
            var id = (string?)project["id"] ?? "";
            if (!Regex.IsMatch(id, "^[A-Za-z0-9]{8}$") || (string?)project["project_type"] != "mod")
                throw new InvalidDataException(Localization.T("modProjectNotMod"));
            var hit = (JObject)project.DeepClone();
            hit["project_id"] = id;
            hit["versions"] = project["game_versions"]?.DeepClone() ?? new JArray();
            hit["categories"] = new JArray((project["categories"] as JArray ?? new())
                .Concat(project["loaders"] as JArray ?? new()).Select(value => value.ToString()).Distinct());
            // Keep the same install compatibility checks as ordinary search results.
            return new JObject { ["hits"] = new JArray(hit), ["total_hits"] = 1 };
        }
        var url = $"https://api.modrinth.com/v2/search?limit=20&offset={offset}&index={Uri.EscapeDataString(sort)}&facets={Uri.EscapeDataString(facets.ToString(Newtonsoft.Json.Formatting.None))}";
        if (query.Length > 0) url += "&query=" + Uri.EscapeDataString(query);
        return JObject.Parse(await client.GetStringAsync(url, cancellation));
    }
}
