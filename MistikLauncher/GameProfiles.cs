using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace MistikLauncher;
public static class GameProfiles
{
    public static bool SafeId(string id) => Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,150}$") && !id.Contains("..");
    public static JObject? Read(string root, string id)
    {
        if (!SafeId(id)) return null;
        try { return JObject.Parse(File.ReadAllText(Path.Combine(root,"versions",id,id+".json"))); } catch { return null; }
    }
    public static string Kind(JObject json) => (json["libraries"] as JArray)?.Any(x => x["name"]?.ToString().StartsWith("net.neoforged:",StringComparison.Ordinal)==true)==true ? "NeoForge" :
        (json["libraries"] as JArray)?.Any(x => x["name"]?.ToString().StartsWith("net.minecraftforge:",StringComparison.Ordinal)==true)==true ? "Forge" :
        (json["libraries"] as JArray)?.Any(x => x["name"]?.ToString().StartsWith("net.fabricmc:",StringComparison.Ordinal)==true)==true ? "Fabric" : "Vanilla";
    public static bool IsInstalled(string root,string id) => Installed(root,id,new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    static bool Installed(string root,string id,HashSet<string> seen)
    {
        if(seen.Count>=16 || !seen.Add(id)) return false;
        var json=Read(root,id); if(json==null) return false;
        // The old installer produced Vanilla copies with a nonexistent Forge bootstrap.
        if((id.Contains("forge",StringComparison.OrdinalIgnoreCase) || json["mainClass"]?.ToString().Contains("forge",StringComparison.OrdinalIgnoreCase)==true) && Kind(json) is not ("Forge" or "NeoForge")) return false;
        var parent=json["inheritsFrom"]?.ToString();
        return !string.IsNullOrEmpty(parent) ? Installed(root,parent,seen) : File.Exists(Path.Combine(root,"versions",id,id+".jar"));
    }
    public static IEnumerable<string> InstalledIds(string root) => Directory.Exists(Path.Combine(root,"versions")) ?
        Directory.GetDirectories(Path.Combine(root,"versions")).Select(Path.GetFileName).OfType<string>().Where(id=>IsInstalled(root,id)) : Array.Empty<string>();
    public static IEnumerable<string> Arguments(JToken? tokens)
    {
        if(tokens is not JArray array) yield break;
        foreach(var token in array)
        {
            if(token.Type==JTokenType.String) { yield return token.ToString(); continue; }
            bool allowed=false;
            foreach(var rule in token["rules"] as JArray ?? new JArray())
            {
                if(rule["features"]!=null) continue;
                if(rule["os"]!=null && rule["os"]?["name"]?.ToString()!="windows") continue;
                if(rule["os"]?["arch"]!=null && rule["os"]?["arch"]?.ToString()!="x86_64") continue;
                allowed=rule["action"]?.ToString()=="allow";
            }
            if(!allowed) continue;
            if(token["value"] is JArray values) { foreach(var value in values) yield return value.ToString(); }
            else if(token["value"]!=null) yield return token["value"]!.ToString();
        }
    }
}
