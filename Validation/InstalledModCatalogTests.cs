using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class InstalledModCatalogTests
{
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
    static HttpResponseMessage Json(JObject value) => new(HttpStatusCode.OK) { Content = new StringContent(value.ToString()) };
    public static async Task<int> Run(string root)
    {
        Directory.CreateDirectory(root); int checks = 0, requests = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        var active = Path.Combine(root, "active"); Directory.CreateDirectory(active);
        var bytes = System.Text.Encoding.UTF8.GetBytes("exact project content, unrelated file name");
        var hash = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
        var jar = Path.Combine(active, "unrelated-name.jar"); File.WriteAllBytes(jar, bytes);
        JObject Version(string proof) => new() { ["id"] = "voice-version", ["project_id"] = "voice-project", ["files"] = new JArray(new JObject { ["hashes"] = new JObject { ["sha512"] = proof } }) };
        using var client = new HttpClient(new Handler(async (request, token) => {
            requests++;
            var body = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
            if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/v2/version_files" || body["algorithm"]?.ToString() != "sha512" || !(body["hashes"] as JArray)!.Any(value => value.ToString() == hash))
                throw new Exception("Catalog must resolve a batch of content hashes rather than names or per-card lookups.");
            return Json(new JObject { [hash] = Version(hash) });
        }));
        var snapshot = await InstalledModCatalog.ReadAsync(client, active, default);
        Check(snapshot.State("voice-project") == CatalogInstallState.Installed && snapshot.State("voice-project", "voice-version") == CatalogInstallState.Installed && requests == 1,
            "catalog identifies manually-added exact project and version with one SHA512 batch");
        File.Move(jar, jar + ".disabled");
        snapshot = await InstalledModCatalog.ReadAsync(client, active, default);
        Check(snapshot.State("voice-project") == CatalogInstallState.Disabled, "catalog distinguishes disabled installed mods");
        File.WriteAllBytes(jar, bytes);
        snapshot = await InstalledModCatalog.ReadAsync(client, active, default);
        Check(snapshot.State("voice-project") == CatalogInstallState.Installed && snapshot.State("voice-project", "voice-version") == CatalogInstallState.Installed,
            "enabled copy takes precedence over disabled duplicate");
        File.Delete(jar); File.Delete(jar + ".disabled");
        snapshot = await InstalledModCatalog.ReadAsync(client, active, default);
        Check(snapshot.State("voice-project") == CatalogInstallState.Available && requests == 3, "deleted mods no longer retain installed state and empty folders need no request");
        File.WriteAllBytes(jar, bytes);
        using var forged = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new JObject { [hash] = Version(new string('0', 128)) }))));
        Check((await InstalledModCatalog.ReadAsync(forged, active, default)).State("voice-project") == CatalogInstallState.Unknown, "forged hash metadata cannot assert a project is installed");
        using var offline = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        Check((await InstalledModCatalog.ReadAsync(offline, active, default)).State("voice-project") == CatalogInstallState.Unknown, "offline lookup remains unknown rather than claiming absent");
        using var delayed = new HttpClient(new Handler((_, _) => { File.Delete(jar); return Task.FromResult(Json(new JObject { [hash] = Version(hash) })); }));
        Check((await InstalledModCatalog.ReadAsync(delayed, active, default)).State("voice-project") == CatalogInstallState.Unknown, "file deletion during response invalidates stale installed status");
        string Profile(string selected, string synced) => (string)typeof(InstalledModCatalog).GetMethod("ProfileDirectory", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { root, root, active, selected, synced })!;
        Check(Profile("fabric-loader-0.18.0-1.21.1", "fabric-loader-0.17.0-1.21.1") == active, "profiles sharing Minecraft version and loader read the same active mod pool");
        var other = Profile("fabric-loader-0.18.0-1.20.1", "fabric-loader-0.17.0-1.21.1");
        Check(other == Path.Combine(root, "mods_pool", "1.20.1_fabric") && !Directory.Exists(other), "different profile reads its own pool without creating or synchronizing folders");
        Check(Profile("1.21.1", "1.21.1") == "" && (await InstalledModCatalog.ReadAsync(client, "", default)).State("voice-project") == CatalogInstallState.Available,
            "Vanilla never presents leftover active JARs as installed loadable mods");
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); File.WriteAllBytes(jar, bytes);
        bool canceled = false; try { await InstalledModCatalog.ReadAsync(client, active, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "obsolete installed-file scans honor cancellation before hashing or networking");
        var removalRoot = Path.Combine(root, "removal"); Directory.CreateDirectory(removalRoot);
        var enabled = Path.Combine(removalRoot, "voice.jar"); var disabled = Path.Combine(removalRoot, "voice-old.jar.disabled");
        var dependency = Path.Combine(removalRoot, "dependency.jar");
        var older = System.Text.Encoding.UTF8.GetBytes("older project version");
        var olderHash = Convert.ToHexString(SHA512.HashData(older)).ToLowerInvariant();
        File.WriteAllBytes(enabled, bytes); File.WriteAllBytes(disabled, older); File.WriteAllText(dependency, "unrelated dependency");
        using var removalClient = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new JObject { [hash] = Version(hash), [olderHash] = Version(olderHash) }))));
        var removal = await InstalledModCatalog.ReadAsync(removalClient, removalRoot, default);
        Check(removal.CanRemove("voice-project") && !removal.CanRemove("unknown-project"), "remove inventory is available only for hash-verified project files");
        File.WriteAllBytes(disabled, System.Text.Encoding.UTF8.GetBytes("replaced same filename"));
        bool refused = false; try { removal.RemoveVerified(removalRoot, "voice-project"); } catch (IOException) { refused = true; }
        Check(refused && File.Exists(enabled) && File.Exists(disabled) && !Directory.Exists(Path.Combine(removalRoot, ".backups")),
            "all selected project hashes are preflighted before any move so replaced second file preserves the entire set");
        File.WriteAllBytes(disabled, older);
        var wrongRoot = Path.Combine(root, "wrong-profile"); Directory.CreateDirectory(wrongRoot);
        File.WriteAllText(Path.Combine(wrongRoot, "voice.jar"), "different profile bytes");
        refused = false; try { removal.RemoveVerified(wrongRoot, "voice-project"); } catch (IOException) { refused = true; }
        Check(refused && File.ReadAllText(Path.Combine(wrongRoot, "voice.jar")) == "different profile bytes" && !Directory.Exists(Path.Combine(wrongRoot, ".backups")),
            "same filename in a different profile does not authorize removing different content");
        var backups = removal.RemoveVerified(removalRoot, "voice-project");
        Check(backups.Count == 2 && !File.Exists(enabled) && !File.Exists(disabled) && File.ReadAllText(dependency) == "unrelated dependency" &&
            backups.All(File.Exists) && backups.Any(path => File.ReadAllBytes(path).SequenceEqual(bytes)) && backups.Any(path => File.ReadAllBytes(path).SequenceEqual(older)),
            "removal backs up enabled and disabled versions of exactly one project while retaining dependencies");
        Check((await InstalledModCatalog.ReadAsync(removalClient, removalRoot, default)).State("voice-project") != CatalogInstallState.Installed,
            "removed project is no longer installed and backup files are excluded from the catalog");
        return checks;
    }
}
