using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace MistikLauncher.Pages
{
    // Version Manager
    public class VersionManagerPage : Page
    {
        readonly MainWindow _main;
        StackPanel _listPanel = null!;
        string _filter = "Hepsi";

        static JArray? _mojangVersions = null;
        static bool _isLoadingVersions = false;

        public VersionManagerPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
            headerRow.Children.Add(PageHelpers.Lbl("Surum Yoneticisi", 24, "#FFFFFF", true));
            
            var refreshBtn = PageHelpers.MkBtn("\uD83D\uDD04 Yenile", "#00A3FF", 100);
            refreshBtn.Margin = new Thickness(15, 0, 0, 0);
            refreshBtn.Click += (_, _) => {
                refreshBtn.Content = "Yenileniyor...";
                refreshBtn.IsEnabled = false;
                _mojangVersions = null;
                _ = LoadMojangVersionsAsync().ContinueWith(_ => Dispatcher.Invoke(() => {
                    refreshBtn.Content = "\uD83D\uDD04 Yenile";
                    refreshBtn.IsEnabled = true;
                }));
            };
            headerRow.Children.Add(refreshBtn);
            sp.Children.Add(headerRow);

            // Filter buttons
            var fRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 12) };
            foreach (var f in new[] { "Hepsi", "Vanilla", "Fabric", "Forge", "Snapshot" })
            {
                var fv = f;
                var b = PageHelpers.MkBtn(f, fv == _filter ? "#00A3FF" : "#333333");
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Click += (_, _) => { _filter = fv; RenderList(); };
                fRow.Children.Add(b);
            }
            sp.Children.Add(fRow);

            _listPanel = new StackPanel();
            sp.Children.Add(_listPanel);
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            RenderList();

            // Background load the full list of Mojang releases and snapshots asynchronously
            _ = LoadMojangVersionsAsync();
        }

        async Task LoadMojangVersionsAsync()
        {
            if (_mojangVersions != null || _isLoadingVersions) return;
            _isLoadingVersions = true;
            try
            {
                var url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
                var manifestStr = await _http.GetStringAsync(url);
                var mj = JObject.Parse(manifestStr);
                _mojangVersions = mj["versions"] as JArray;
                
                // Cache the manifest in case we are offline next time
                var cachePath = Path.Combine(App.GameDir, "version_manifest_cache.json");
                Directory.CreateDirectory(App.GameDir);
                await File.WriteAllTextAsync(cachePath, manifestStr);
            }
            catch (Exception ex)
            {
                App.Log($"Failed to fetch Mojang versions online: {ex.Message}");
                // Load cached file if exists
                var cachePath = Path.Combine(App.GameDir, "version_manifest_cache.json");
                if (File.Exists(cachePath))
                {
                    try
                    {
                        var manifestStr = await File.ReadAllTextAsync(cachePath);
                        var mj = JObject.Parse(manifestStr);
                        _mojangVersions = mj["versions"] as JArray;
                    }
                    catch { }
                }
            }
            finally
            {
                _isLoadingVersions = false;
                Dispatcher.Invoke(() => RenderList());
            }
        }

        string? GetInstalledFolder(string id, HashSet<string> installed)
        {
            if (installed.Contains(id)) return id;

            if (id.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
            {
                var gameVer = id.Substring("fabric-".Length);
                var matched = installed.FirstOrDefault(x => x.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase) && x.EndsWith($"-{gameVer}", StringComparison.OrdinalIgnoreCase));
                if (matched != null) return matched;
            }

            if (id.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
            {
                var gameVer = id.Substring("forge-".Length);
                var prefix = $"forge-{gameVer}-";
                var matched = installed.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (matched != null) return matched;
            }

            return null;
        }

        void RenderList()
        {
            _listPanel.Children.Clear();
            var allVers = new List<(string id, string type, string? installedFolder)>();

            var verDir = Path.Combine(App.GameDir, "versions");
            var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(verDir))
            {
                foreach (var d in Directory.GetDirectories(verDir))
                {
                    var name = Path.GetFileName(d);
                    if (!string.IsNullOrEmpty(name))
                    {
                        var jarFile = Path.Combine(d, $"{name}.jar");
                        var jsonFile = Path.Combine(d, $"{name}.json");
                        if (File.Exists(jarFile) && File.Exists(jsonFile))
                        {
                            installedNames.Add(name);
                        }
                    }
                }
            }

            var versionsMap = new Dictionary<string, (string id, string type)>(StringComparer.OrdinalIgnoreCase);

            // Populate popular vanilla/snapshot list as instant startup fallback
            var fallbackVanillas = new[] { 
                "1.22", "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", 
                "1.20.6", "1.20.4", "1.20.1", "1.20", 
                "1.19.4", "1.19.2", "1.18.2", "1.16.5", "1.12.2", "1.8.9"
            };
            foreach (var v in fallbackVanillas) versionsMap[v] = (v, "Vanilla");

            // Popular Fabric versions (startup fallback)
            var fallbackFabrics = new[] { 
                "fabric-1.22", "fabric-1.21.4", "fabric-1.21.3", "fabric-1.21.2", "fabric-1.21.1", "fabric-1.21", 
                "fabric-1.20.6", "fabric-1.20.4", "fabric-1.20.1", 
                "fabric-1.19.4", "fabric-1.19.2", "fabric-1.18.2", "fabric-1.16.5" 
            };
            foreach (var f in fallbackFabrics) versionsMap[f] = (f, "Fabric");

            // Popular Forge versions (fully supported)
            var fallbackForges = new[] { 
                "forge-1.22", "forge-1.21.4", "forge-1.21.3", "forge-1.21.2", "forge-1.21", "forge-1.20.4", "forge-1.20.1", 
                "forge-1.19.4", "forge-1.19.2", 
                "forge-1.18.2", "forge-1.16.5", "forge-1.12.2" 
            };
            foreach (var fg in fallbackForges) versionsMap[fg] = (fg, "Forge");

            // If we have dynamic Mojang manifest, use all of them!
            if (_mojangVersions != null)
            {
                foreach (var v in _mojangVersions)
                {
                    var id = v["id"]?.ToString();
                    var typeStr = v["type"]?.ToString();
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(typeStr)) continue;

                    string mappedType = "Vanilla";
                    if (typeStr.Equals("snapshot", StringComparison.OrdinalIgnoreCase) || 
                        typeStr.Equals("old_beta", StringComparison.OrdinalIgnoreCase) ||
                        typeStr.Equals("old_alpha", StringComparison.OrdinalIgnoreCase))
                    {
                        mappedType = "Snapshot";
                    }
                    else if (typeStr.Equals("release", StringComparison.OrdinalIgnoreCase))
                    {
                        mappedType = "Vanilla";

                        // Automatically enable dynamic Fabric support for all releases >= 1.14!
                        var numParts = GetVersionNumbers(id);
                        if (numParts.Count >= 2 && ((numParts[0] == 1 && numParts[1] >= 14) || numParts[0] > 1))
                        {
                            var fabId = $"fabric-{id}";
                            versionsMap[fabId] = (fabId, "Fabric");
                        }
                    }
                    else
                    {
                        mappedType = "Snapshot";
                    }

                    versionsMap[id] = (id, mappedType);
                }
            }

            // Ensure all physically installed versions are always shown, even if they aren't in the manifest
            foreach (var name in installedNames)
            {
                // Check if this installed folder is already represented by a dynamic version card
                bool alreadyMapped = false;
                if (name.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = name.LastIndexOf('-');
                    if (idx != -1)
                    {
                        var gameVer = name.Substring(idx + 1);
                        if (versionsMap.ContainsKey($"fabric-{gameVer}")) alreadyMapped = true;
                    }
                }
                else if (name.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = name.Split('-');
                    if (parts.Length >= 2)
                    {
                        var gameVer = parts[1];
                        if (versionsMap.ContainsKey($"forge-{gameVer}")) alreadyMapped = true;
                    }
                }

                if (alreadyMapped) continue;

                if (!versionsMap.TryGetValue(name, out var existing))
                {
                    string mappedType = "Vanilla";
                    if (name.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
                    {
                        mappedType = "Fabric";
                    }
                    else if (name.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                    {
                        mappedType = "Forge";
                    }
                    else
                    {
                        // Default to Vanilla (installed versions show anyway)
                        mappedType = "Vanilla";
                    }
                    versionsMap[name] = (name, mappedType);
                }
            }

            // Build final list with accurate installation flag
            foreach (var item in versionsMap.Values)
            {
                var installedFolder = GetInstalledFolder(item.id, installedNames);
                allVers.Add((item.id, item.type, installedFolder));
            }

            // Sort all versions descending beautifully and robustly
            allVers.Sort((a, b) => {
                var partsA = GetVersionNumbers(a.id);
                var partsB = GetVersionNumbers(b.id);
                for (int i = 0; i < Math.Max(partsA.Count, partsB.Count); i++)
                {
                    int numA = i < partsA.Count ? partsA[i] : 0;
                    int numB = i < partsB.Count ? partsB[i] : 0;
                    if (numA != numB) return numB.CompareTo(numA);
                }
                return string.Compare(b.id, a.id, StringComparison.OrdinalIgnoreCase);
            });

            var filtered = _filter == "Hepsi" ? allVers : allVers.Where(v => v.type == _filter).ToList();

            foreach (var (id, type, installedFolder) in filtered)
            {
                var vid = id;
                bool installed = installedFolder != null;
                var card = PageHelpers.Card("#181818", 10, margin: new Thickness(0, 5, 0, 5));
                var grid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var typeColor = type == "Fabric" ? "#A349A4" : type == "Forge" ? "#FFB100" : type == "Snapshot" ? "#E040FB" : "#00A3FF";
                var info = new StackPanel();
                info.Children.Add(PageHelpers.Lbl(vid, 14, "#FFFFFF", true));
                info.Children.Add(PageHelpers.Lbl(type, 11, typeColor));
                if (installed) info.Children.Add(PageHelpers.Lbl("Kurulu", 10, "#2EB82E"));
                Grid.SetColumn(info, 0); grid.Children.Add(info);

                var btnSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (installed && !string.IsNullOrEmpty(installedFolder))
                {
                    bool isCurrent = _main.Config.Version == installedFolder;
                    var selBtn = PageHelpers.MkBtn(isCurrent ? "SECILDI" : "SEC", isCurrent ? "#555555" : "#2EB82E", 80);
                    if (isCurrent)
                    {
                        selBtn.IsEnabled = false;
                    }
                    else
                    {
                        selBtn.Click += (_, _) => {
                            _main.Config.Version = installedFolder;
                            ConfigManager.Save(_main.Config);
                            _main.PopulateVersionBox();
                            RenderList();
                            MessageBox.Show($"{vid} secildi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
                        };
                    }
                    var delBtn = PageHelpers.MkBtn("SIL", "#FF4B4B", 60); delBtn.Margin = new Thickness(8, 0, 0, 0);
                    delBtn.Click += (_, _) => {
                        if (MessageBox.Show($"{vid} silinsin mi?", "Onay", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            var path = Path.Combine(App.GameDir, "versions", installedFolder);
                            if (Directory.Exists(path)) Directory.Delete(path, true);
                            RenderList(); _main.PopulateVersionBox();
                        }
                    };
                    btnSp.Children.Add(selBtn); btnSp.Children.Add(delBtn);
                }
                else
                {
                    var dlBtn = PageHelpers.MkBtn("INDIR", "#00A3FF", 80);
                    dlBtn.Click += async (_, _) => {
                        dlBtn.IsEnabled = false; dlBtn.Content = "..."; _main.SetProgress(10, "İndirme başlatılıyor...");
                        var installedId = await DownloadVersionAsync(vid, (p, status) => Dispatcher.Invoke(() => _main.SetProgress(p, status)));
                        _main.SetProgress(0, installedId != null ? $"Sürüm: {installedId}" : $"Sürüm: {vid}"); 
                        dlBtn.Content = "INDIR"; dlBtn.IsEnabled = true;
                        
                        if (!string.IsNullOrEmpty(installedId))
                        {
                            var path = Path.Combine(App.GameDir, "versions", installedId);
                            var jarFile = Path.Combine(path, $"{installedId}.jar");
                            var jsonFile = Path.Combine(path, $"{installedId}.json");
                            if (File.Exists(jarFile) && File.Exists(jsonFile))
                            {
                                _main.Config.Version = installedId;
                                ConfigManager.Save(_main.Config);
                                MessageBox.Show($"{installedId} başarıyla kuruldu ve seçildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        
                        _main.PopulateVersionBox();
                        RenderList();
                    };
                    btnSp.Children.Add(dlBtn);
                }

                Grid.SetColumn(btnSp, 1); grid.Children.Add(btnSp);
                card.Child = grid; _listPanel.Children.Add(card);
            }
        }

        // Pure C# HTTP download - no Python
        static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/1.0 (contact@mistik.com)");
            return client;
        }

        static async Task<string?> DownloadVersionAsync(string version, Action<double, string> progress)
        {
            try
            {
                progress(5, "Mojang manifestosu alınıyor...");
                if (version.StartsWith("fabric-"))
                {
                    var gameVer = version.Substring("fabric-".Length);
                    return await InstallFabricAsync(gameVer, progress);
                }
                if (version.StartsWith("forge-"))
                {
                    var gameVer = version.Substring("forge-".Length);
                    return await InstallForgeAsync(gameVer, progress);
                }

                // Fetch manifest
                var manifest = await _http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                var mj = JObject.Parse(manifest);
                var versions = mj["versions"] as JArray;
                var ver = versions?.FirstOrDefault(v => v["id"]?.ToString() == version);
                if (ver == null) 
                {
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"{version} henüz Mojang tarafından yayınlanmamış veya bulunamayan bir sürümdür.\n\nLütfen listenin daha aşağılarından şu anki güncel (örn: 1.21.4) bir sürümü seçin.", "Sürüm Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Warning));
                    return null;
                }

                var verUrl = ver["url"]?.ToString();
                if (string.IsNullOrEmpty(verUrl)) return null;

                progress(10, "Sürüm detayları sorgulanıyor...");
                var verJson = JObject.Parse(await _http.GetStringAsync(verUrl));
                var clientUrl = verJson["downloads"]?["client"]?["url"]?.ToString();
                if (string.IsNullOrEmpty(clientUrl)) return null;

                var verDir = Path.Combine(App.GameDir, "versions", version);
                Directory.CreateDirectory(verDir);

                // Download jar with dynamic speed reporting
                var jarPath = Path.Combine(verDir, $"{version}.jar");
                await DownloadFileWithProgressAsync(clientUrl, jarPath, progress, 10, 65);

                // Download assets index
                var assetsId  = verJson["assetIndex"]?["id"]?.ToString() ?? version;
                var assetsUrl = verJson["assetIndex"]?["url"]?.ToString();
                if (!string.IsNullOrEmpty(assetsUrl))
                {
                    progress(80, "Varlık indeksleri kuruluyor...");
                    var assetsDir = Path.Combine(App.GameDir, "assets", "indexes");
                    Directory.CreateDirectory(assetsDir);
                    var assetsJson = await _http.GetStringAsync(assetsUrl);
                    await File.WriteAllTextAsync(Path.Combine(assetsDir, $"{assetsId}.json"), assetsJson);

                    // Download asset objects (language files, sounds, textures)
                    progress(82, "Oyun varlıkları (diller, sesler) indiriliyor...");
                    try
                    {
                        var assetIndexObj = JObject.Parse(assetsJson);
                        var objects = assetIndexObj["objects"] as JObject;
                        if (objects != null)
                        {
                            var objectsDir = Path.Combine(App.GameDir, "assets", "objects");
                            var entries = objects.Properties().ToList();
                            int total = entries.Count, done = 0;
                            
                            // Using a Semaphore to run up to 8 downloads concurrently
                            var sem = new System.Threading.SemaphoreSlim(8);
                            var tasks = entries.Select(async prop =>
                            {
                                var hash = prop.Value["hash"]?.ToString();
                                if (string.IsNullOrEmpty(hash)) return;
                                var prefix = hash.Substring(0, 2);
                                var destDir = Path.Combine(objectsDir, prefix);
                                var destFile = Path.Combine(destDir, hash);
                                if (File.Exists(destFile))
                                {
                                    System.Threading.Interlocked.Increment(ref done);
                                    return;
                                }
                                
                                await sem.WaitAsync();
                                try
                                {
                                    Directory.CreateDirectory(destDir);
                                    var url2 = $"https://resources.download.minecraft.net/{prefix}/{hash}";
                                    var data = await _http.GetByteArrayAsync(url2);
                                    await File.WriteAllBytesAsync(destFile, data);
                                }
                                catch (Exception ex)
                                {
                                    App.Log($"Failed to download asset {hash}: {ex.Message}");
                                }
                                finally
                                {
                                    sem.Release();
                                    int d = System.Threading.Interlocked.Increment(ref done);
                                    if (d % 100 == 0 || d == total)
                                    {
                                        double pct = 82.0 + ((double)d / total) * 12.0;
                                        progress(pct, $"Varlıklar indiriliyor: {d}/{total}");
                                    }
                                }
                            });
                            await Task.WhenAll(tasks);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"Varlık indirme hatası: {ex.Message}");
                    }
                }
                progress(95, "Yapılandırma dosyaları yazılıyor...");

                // Write version JSON
                var versionJson = verJson.ToString();
                await File.WriteAllTextAsync(Path.Combine(verDir, $"{version}.json"), versionJson);
                progress(100, "Tamamlandı!");

                App.Log($"Version downloaded: {version}");
                return version;
            }
            catch (Exception ex)
            {
                App.Log($"Download error: {ex.Message}");
                MessageBox.Show($"Indirme hatasi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static async Task DownloadFileWithProgressAsync(string url, string destinationPath, Action<double, string> progress, double progressStart, double progressWeight)
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalReadBytes = 0L;
            int readBytes;
            var sw = Stopwatch.StartNew();
            
            long lastReportBytes = 0L;
            var lastReportTime = sw.ElapsedMilliseconds;

            while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, readBytes);
                totalReadBytes += readBytes;

                var now = sw.ElapsedMilliseconds;
                if (now - lastReportTime >= 200 || totalReadBytes == totalBytes)
                {
                    double pct = totalBytes > 0 ? (double)totalReadBytes / totalBytes * 100.0 : 0.0;
                    double currentProgress = progressStart + pct * (progressWeight / 100.0);

                    // Speed calculation
                    double elapsedSec = (now - lastReportTime) / 1000.0;
                    if (elapsedSec <= 0) elapsedSec = 0.001;
                    
                    long bytesInInterval = totalReadBytes - lastReportBytes;
                    double bytesPerSec = bytesInInterval / elapsedSec;
                    double mbPerSec = bytesPerSec / (1024.0 * 1024.0);

                    string speedStr = mbPerSec >= 1.0 
                        ? $"{mbPerSec:F2} MB/s" 
                        : $"{bytesPerSec / 1024.0:F1} KB/s";

                    string sizeStr = totalBytes > 0 
                        ? $"{totalReadBytes / (1024.0 * 1024.0):F1} MB / {totalBytes / (1024.0 * 1024.0):F1} MB"
                        : $"{totalReadBytes / (1024.0 * 1024.0):F1} MB";

                    progress(currentProgress, $"İndiriliyor... {pct:F1}% ({sizeStr}) - Hız: {speedStr}");

                    lastReportBytes = totalReadBytes;
                    lastReportTime = now;
                }
            }
        }

        static async Task<string> InstallFabricAsync(string gameVersion, Action<double, string> progress)
        {
            try
            {
                progress(10, "[Fabric] Temel oyun sürümü indiriliyor...");
                // Ensure vanilla base version is installed first
                await DownloadVersionAsync(gameVersion, (p, status) => progress(10 + p * 0.45, $"[Fabric] {status}")); // takes up to 55%

                progress(60, "[Fabric] Fabric yükleyici bilgileri alınıyor...");
                // Fetch loaders
                var metaUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}";
                var metaResp = await _http.GetStringAsync(metaUrl);
                var loaders = JArray.Parse(metaResp);
                if (loaders.Count == 0)
                {
                    throw new Exception($"Minecraft {gameVersion} surumu icin Fabric yukleyicisi bulunamadi!");
                }

                var loaderVer = loaders[0]["loader"]?["version"]?.ToString() ?? "0.15.11";

                progress(75, "[Fabric] Fabric profil ayarları kuruluyor...");
                // Fetch profile JSON
                var profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVer}/profile/json";
                var profileJson = await _http.GetStringAsync(profileUrl);

                // Save directory
                var fabricName = $"fabric-loader-{loaderVer}-{gameVersion}";
                var targetDir = Path.Combine(App.GameDir, "versions", fabricName);
                Directory.CreateDirectory(targetDir);

                // Write JSON profile
                await File.WriteAllTextAsync(Path.Combine(targetDir, $"{fabricName}.json"), profileJson);

                // Copy Vanilla Jar to fabric directory
                var vanillaJar = Path.Combine(App.GameDir, "versions", gameVersion, $"{gameVersion}.jar");
                if (File.Exists(vanillaJar))
                {
                    File.Copy(vanillaJar, Path.Combine(targetDir, $"{fabricName}.jar"), true);
                }

                progress(100, "[Fabric] Kurulum tamamlandı!");
                App.Log($"Fabric {gameVersion} successfully installed: {fabricName}");
                return fabricName;
            }
            catch (Exception ex)
            {
                App.Log($"Fabric install error: {ex.Message}");
                throw;
            }
        }

        static async Task<string> InstallForgeAsync(string gameVersion, Action<double, string> progress)
        {
            try
            {
                progress(10, "[Forge] Temel oyun sürümü indiriliyor...");
                // Ensure vanilla base version is installed first
                await DownloadVersionAsync(gameVersion, (p, status) => progress(10 + p * 0.45, $"[Forge] {status}")); // takes up to 55%

                progress(60, "[Forge] Forge profil ayarları oluşturuluyor...");
                var forgeVer = gameVersion switch {
                    "1.21"   => "51.0.8",
                    "1.20.4" => "49.0.38",
                    "1.20.1" => "47.2.0",
                    "1.19.4" => "45.1.0",
                    "1.19.2" => "43.2.0",
                    "1.18.2" => "40.2.0",
                    "1.16.5" => "36.2.34",
                    "1.12.2" => "14.23.5.2860",
                    _        => "47.2.0"
                };

                var forgeName = $"forge-{gameVersion}-{forgeVer}";
                var targetDir = Path.Combine(App.GameDir, "versions", forgeName);
                Directory.CreateDirectory(targetDir);

                progress(75, "[Forge] Forge JSON yapılandırması ayarlanıyor...");
                var vanillaJsonPath = Path.Combine(App.GameDir, "versions", gameVersion, $"{gameVersion}.json");
                if (File.Exists(vanillaJsonPath))
                {
                    var vanillaJson = JObject.Parse(await File.ReadAllTextAsync(vanillaJsonPath));
                    vanillaJson["id"] = forgeName;
                    
                    if (gameVersion.StartsWith("1.12") || gameVersion.StartsWith("1.8"))
                    {
                        vanillaJson["mainClass"] = "net.minecraft.launchwrapper.Launch";
                        var args = vanillaJson["minecraftArguments"]?.ToString() ?? "";
                        if (!args.Contains("--tweakClass"))
                        {
                            vanillaJson["minecraftArguments"] = args + " --tweakClass net.minecraftforge.fml.common.launcher.FMLTweaker";
                        }
                    }
                    else
                    {
                        vanillaJson["mainClass"] = "net.minecraftforge.bootstrap.ForgeBootstrap";
                    }

                    await File.WriteAllTextAsync(Path.Combine(targetDir, $"{forgeName}.json"), vanillaJson.ToString());
                }

                // Copy Vanilla Jar to Forge directory
                var vanillaJar = Path.Combine(App.GameDir, "versions", gameVersion, $"{gameVersion}.jar");
                if (File.Exists(vanillaJar))
                {
                    File.Copy(vanillaJar, Path.Combine(targetDir, $"{forgeName}.jar"), true);
                }

                progress(100, "[Forge] Kurulum tamamlandı!");
                App.Log($"Forge {gameVersion} successfully installed: {forgeName}");
                return forgeName;
            }
            catch (Exception ex)
            {
                App.Log($"Forge install error: {ex.Message}");
                throw;
            }
        }

        static List<int> GetVersionNumbers(string input)
        {
            var list = new List<int>();
            var matches = System.Text.RegularExpressions.Regex.Matches(input, @"\d+");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (int.TryParse(m.Value, out var n))
                    list.Add(n);
            }
            return list;
        }

        public static async Task<string> ResolveDirectDownloadUrlAsync(string url, HttpClient? client = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            if (!url.Contains("drive.google.com")) return url;

            string fileId = "";
            var idMatch = System.Text.RegularExpressions.Regex.Match(url, @"/file/d/([a-zA-Z0-9_-]+)");
            if (idMatch.Success)
            {
                fileId = idMatch.Groups[1].Value;
            }
            else
            {
                idMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=([a-zA-Z0-9_-]+)");
                if (idMatch.Success)
                {
                    fileId = idMatch.Groups[1].Value;
                }
            }

            if (string.IsNullOrEmpty(fileId)) return url;

            string directUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

            HttpClient tempClient = client ?? new HttpClient();
            try
            {
                var response = await tempClient.GetAsync(directUrl);
                if (response.IsSuccessStatusCode)
                {
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (contentType.Contains("html"))
                    {
                        var html = await response.Content.ReadAsStringAsync();
                        var confirmMatch = System.Text.RegularExpressions.Regex.Match(html, @"confirm=([a-zA-Z0-9_-]+)");
                        if (confirmMatch.Success)
                        {
                            string confirmToken = confirmMatch.Groups[1].Value;
                            return $"https://drive.google.com/uc?export=download&id={fileId}&confirm={confirmToken}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"[UYARI] Google Drive linki cozumlenirken hata olustu: {ex.Message}");
            }
            finally
            {
                if (client == null) tempClient.Dispose();
            }

            return directUrl;
        }
    }

    // Mod Manager
    public class ModManagerPage : Page
    {
        readonly MainWindow _main;
        TextBox _searchBox = null!;
        StackPanel _resultsPanel = null!;
        StackPanel _installedPanel = null!;
        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public ModManagerPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Mod Merkezi (Modrinth)", 24, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("Modrinth'den binlerce mod - tek tiklama kurulum", 12, "#A0A0A0"));

            // Mistik Özel Hazır Mod Paketleri Card
            var packsCard = PageHelpers.Card("#111116", 12, "#00A3FF");
            packsCard.Margin = new Thickness(0, 10, 0, 10);
            var packsSp = new StackPanel { Margin = new Thickness(16) };
            packsSp.Children.Add(PageHelpers.Lbl("🚀 Mistik Özel Tek Tıkla Hazır Mod Paketleri", 15, "#00A3FF", true));
            packsSp.Children.Add(PageHelpers.Lbl("Oyun keyfinizi zirveye çıkaracak, birbiriyle tam uyumlu ve optimize edilmiş hazır mod paketleri:", 11, "#A0A0A0", wrap: TextWrapping.Wrap));

            // 3-Column Grid for Modpacks
            var packsGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            packsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            packsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacing
            packsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            packsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacing
            packsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Column 1: Ultra FPS Boost
            var col1Card = PageHelpers.Card("#161a1e", 8, "#00FFCC", new Thickness(0));
            var col1Sp = new StackPanel { Margin = new Thickness(12) };
            col1Sp.Children.Add(PageHelpers.Lbl("🚀 ULTRA FPS BOOST", 13, "#00FFCC", true));
            col1Sp.Children.Add(PageHelpers.Lbl("Sodium, Lithium, Iris ve Indium bir arada. Eski bilgisayarlarda bile +200 FPS artışı ve sıfır donma garantisi!", 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 6, 0, 12)));
            var col1Btn = PageHelpers.MkBtn("FPS Paketi Kur", "#00FFCC");
            col1Btn.Foreground = Brushes.Black;
            col1Btn.Click += async (_, _) => await InstallModpack(
                new[] { "sodium", "lithium", "iris", "indium" },
                new[] { "Sodium", "Lithium", "Iris Shaders", "Indium" },
                col1Btn,
                "🚀 ULTRA FPS BOOST"
            );
            col1Sp.Children.Add(col1Btn);
            col1Card.Child = col1Sp;
            Grid.SetColumn(col1Card, 0);
            packsGrid.Children.Add(col1Card);

            // Column 2: Gizem & Korku
            var col2Card = PageHelpers.Card("#1a1315", 8, "#FF3333", new Thickness(0));
            var col2Sp = new StackPanel { Margin = new Thickness(12) };
            col2Sp.Children.Add(PageHelpers.Lbl("👻 GİZEM & KORKU", 13, "#FF3333", true));
            col2Sp.Children.Add(PageHelpers.Lbl("Minecraft'ın ürkütücü sislerinde Herobrine'ı hisset. 3D Gerçekçi ses fiziği modu ile dehşeti birebir yaşa!", 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 6, 0, 12)));
            var col2Btn = PageHelpers.MkBtn("Korku Paketi Kur", "#FF3333");
            col2Btn.Click += async (_, _) => await InstallModpack(
                new[] { "from-the-fog", "sound-physics-remastered" },
                new[] { "From The Fog", "Sound Physics" },
                col2Btn,
                "👻 GİZEM & KORKU"
            );
            col2Sp.Children.Add(col2Btn);
            col2Card.Child = col2Sp;
            Grid.SetColumn(col2Card, 2);
            packsGrid.Children.Add(col2Card);

            // Column 3: PVP & Akıcılık
            var col3Card = PageHelpers.Card("#1b1710", 8, "#FFB100", new Thickness(0));
            var col3Sp = new StackPanel { Margin = new Thickness(12) };
            col3Sp.Children.Add(PageHelpers.Lbl("⚔️ PVP & AKICILIK", 13, "#FFB100", true));
            col3Sp.Children.Add(PageHelpers.Lbl("Zoomify yakınlaştırma modu, gerçekçi 3 boyutlu karakter katmanları ve akıcılık için Sodium bir arada!", 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 6, 0, 12)));
            var col3Btn = PageHelpers.MkBtn("PVP Paketi Kur", "#FFB100");
            col3Btn.Foreground = Brushes.Black;
            col3Btn.Click += async (_, _) => await InstallModpack(
                new[] { "sodium", "zoomify", "3dskinlayers" },
                new[] { "Sodium", "Zoomify", "3D Skin Layers" },
                col3Btn,
                "⚔️ PVP & AKICILIK"
            );
            col3Sp.Children.Add(col3Btn);
            col3Card.Child = col3Sp;
            Grid.SetColumn(col3Card, 4);
            packsGrid.Children.Add(col3Card);

            packsSp.Children.Add(packsGrid);
            packsCard.Child = packsSp;
            sp.Children.Add(packsCard);

            // OptiFine Card
            var optiCard = PageHelpers.Card("#1e1a10", 12, "#FFB100");
            optiCard.Margin = new Thickness(0, 14, 0, 10);
            var optiSp = new StackPanel { Margin = new Thickness(16) };
            optiSp.Children.Add(PageHelpers.Lbl("✨ OptiFine & FPS Optimizasyon Odası", 14, "#FFB100", true));
            optiSp.Children.Add(PageHelpers.Lbl("OptiFine yuklemek son derece kolaydir! Asagidaki rehberi takip ederek saniyeler icinde kurabilirsiniz:", 11, "#CCC", wrap: TextWrapping.Wrap));
            
            var bulletPoints = new[] {
                "• Forge Surumu icin: Indirdiginiz OptiFine .jar dosyasini dogrudan Mod Klasorune atmaniz yeterlidir.",
                "• Fabric Surumu icin: Mod Klasorune hem OptiFine .jar dosyasini hem de OptiFabric modunu (asagidaki tusla kurabilirsiniz) atmaniz gerekir.",
                "• Vanilla Surumu icin: Indirdiginiz OptiFine .jar dosyasina cift tiklayarak 'Install' demeniz yeterlidir, yeni profil otomatik olusur."
            };
            foreach(var p in bulletPoints)
            {
                optiSp.Children.Add(PageHelpers.Lbl(p, 10, "#AAA", wrap: TextWrapping.Wrap));
            }

            var optiBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            
            var dlOptiBtn = PageHelpers.MkBtn("OptiFine Indir (Resmi Site)", "#FFB100", 180);
            dlOptiBtn.Click += (_, _) => Process.Start(new ProcessStartInfo("https://optifine.net/downloads") { UseShellExecute = true });
            
            var optiFabricBtn = PageHelpers.MkBtn("OptiFabric Modunu Kur", "#A349A4", 170);
            optiFabricBtn.Margin = new Thickness(10, 0, 0, 0);
            optiFabricBtn.Click += async (_, _) => {
                optiFabricBtn.IsEnabled = false; optiFabricBtn.Content = "Kuruluyor...";
                await InstallMod("2t19m3zP", "OptiFabric");
                optiFabricBtn.Content = "Kuruldu!"; optiFabricBtn.IsEnabled = true;
            };

            var modsFolderBtn = PageHelpers.MkBtn("Mod Klasorunu Ac", "#333333", 140);
            modsFolderBtn.Margin = new Thickness(10, 0, 0, 0);
            modsFolderBtn.Click += (_, _) => { Directory.CreateDirectory(App.ModsDir); Process.Start("explorer.exe", App.ModsDir); };

            optiBtns.Children.Add(dlOptiBtn);
            optiBtns.Children.Add(optiFabricBtn);
            optiBtns.Children.Add(modsFolderBtn);
            optiSp.Children.Add(optiBtns);
            
            optiCard.Child = optiSp;
            sp.Children.Add(optiCard);

            var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 12) };
            _searchBox = PageHelpers.DarkTextBox("Mod ara... (Sodium, Iris, OptiFine...)");
            _searchBox.Width = 420;
            var searchBtn = PageHelpers.MkBtn("ARA", "#00A3FF", 80); searchBtn.Margin = new Thickness(8, 0, 0, 0);
            searchBtn.Click += async (_, _) => await SearchMods();
            _searchBox.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) await SearchMods(); };
            var folderBtn = PageHelpers.MkBtn("Mod Klasörü", "#333333"); folderBtn.Margin = new Thickness(8, 0, 0, 0);
            folderBtn.Click += (_, _) => { Directory.CreateDirectory(App.ModsDir); Process.Start("explorer.exe", App.ModsDir); };

            var importBtn = PageHelpers.MkBtn("TL/Orjinal Modları Aktar", "#A349A4"); importBtn.Margin = new Thickness(8, 0, 0, 0);
            importBtn.Click += async (_, _) => {
                var mcMods = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods");
                if (!Directory.Exists(mcMods)) {
                    MessageBox.Show(".minecraft içinde mods klasörü bulunamadı!", "Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var jars = Directory.GetFiles(mcMods, "*.jar");
                if (jars.Length == 0) {
                    MessageBox.Show(".minecraft/mods klasöründe hiç mod bulunamadı.", "Boş", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                importBtn.IsEnabled = false; importBtn.Content = "Aktarılıyor...";
                Directory.CreateDirectory(App.ModsDir);
                int count = 0;
                await Task.Run(() => {
                    foreach (var jar in jars) {
                        try {
                            var dest = Path.Combine(App.ModsDir, Path.GetFileName(jar));
                            if (!File.Exists(dest)) { File.Copy(jar, dest, true); count++; }
                        } catch { }
                    }
                });
                RenderInstalledMods();
                importBtn.Content = "TL/Orjinal Modları Aktar"; importBtn.IsEnabled = true;
                MessageBox.Show($"{count} adet mod başarıyla .minecraft klasöründen Mistik Launcher'a aktarıldı!", "Aktarım Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            searchRow.Children.Add(_searchBox); searchRow.Children.Add(searchBtn); searchRow.Children.Add(folderBtn); searchRow.Children.Add(importBtn);
            sp.Children.Add(searchRow);

            _resultsPanel = new StackPanel();
            sp.Children.Add(_resultsPanel);

            // ── Toplu Mod Sürüm Taşıyıcı (Migrator) Kartı ──
            var migCard = PageHelpers.Card("#121814", 12, "#2EB82E");
            migCard.Margin = new Thickness(0, 10, 0, 10);
            
            var migSp = new StackPanel { Margin = new Thickness(16) };
            migSp.Children.Add(PageHelpers.Lbl("🔄 Toplu Mod Sürüm Taşıyıcı (Mod Migrator)", 15, "#2EB82E", true));
            migSp.Children.Add(PageHelpers.Lbl("Aktif mod klasörünüzdeki modların seçtiğiniz hedef Minecraft sürümü ve Loader türüne uygun olan sürümlerini Modrinth'ten otomatik olarak indirip kurar. Mevcut modlarınız da güvenle yedeklenir (askıya alınır).", 10, "#A0A0A0", wrap: TextWrapping.Wrap));

            var migControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            
            migControls.Children.Add(PageHelpers.Lbl("Hedef Sürüm:", 11, "#FFFFFF"));
            
            var targetVerCombo = new ComboBox { 
                Width = 100, Height = 28, Margin = new Thickness(6, 0, 16, 0),
                Background = Brushes.White, Foreground = Brushes.Black, FontWeight = FontWeights.Bold
            };
            
            var uniqueMcVersions = new HashSet<string>();
            foreach (var item in _main.VerBox.Items)
            {
                var itemStr = item?.ToString() ?? "";
                var mcMatch = System.Text.RegularExpressions.Regex.Match(itemStr, @"1\.\d+(\.\d+)?");
                if (mcMatch.Success)
                {
                    uniqueMcVersions.Add(mcMatch.Value);
                }
            }

            // Sort versions nicely (latest at the top)
            var sortedMcVersions = uniqueMcVersions.ToList();
            sortedMcVersions.Sort((a, b) => {
                var partsA = new List<int>();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(a, @"\d+"))
                    if (int.TryParse(m.Value, out var n)) partsA.Add(n);
                var partsB = new List<int>();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(b, @"\d+"))
                    if (int.TryParse(m.Value, out var n)) partsB.Add(n);
                for (int i = 0; i < Math.Max(partsA.Count, partsB.Count); i++)
                {
                    int numA = i < partsA.Count ? partsA[i] : 0;
                    int numB = i < partsB.Count ? partsB[i] : 0;
                    if (numA != numB) return numB.CompareTo(numA);
                }
                return string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var v in sortedMcVersions)
            {
                targetVerCombo.Items.Add(v);
            }
            
            if (targetVerCombo.Items.Count > 0)
            {
                targetVerCombo.SelectedIndex = 0;
                
                var activeVer = _main.Config.Version ?? "";
                var activeMcMatch = System.Text.RegularExpressions.Regex.Match(activeVer, @"1\.\d+(\.\d+)?");
                if (activeMcMatch.Success && targetVerCombo.Items.Contains(activeMcMatch.Value))
                {
                    targetVerCombo.SelectedItem = activeMcMatch.Value;
                }
            }
            else
            {
                var migVersions = new[] { "1.21.1", "1.20.1", "1.19.2", "1.18.2", "1.16.5", "1.12.2" };
                foreach (var v in migVersions) targetVerCombo.Items.Add(v);
                targetVerCombo.SelectedIndex = 0;
                
                var activeVer = _main.Config.Version ?? "";
                var activeMcMatch = System.Text.RegularExpressions.Regex.Match(activeVer, @"1\.\d+(\.\d+)?");
                if (activeMcMatch.Success && targetVerCombo.Items.Contains(activeMcMatch.Value))
                {
                    targetVerCombo.SelectedItem = activeMcMatch.Value;
                }
            }
            migControls.Children.Add(targetVerCombo);

            migControls.Children.Add(PageHelpers.Lbl("Mod Yükleyici:", 11, "#FFFFFF"));
            
            var targetLoaderCombo = new ComboBox { 
                Width = 100, Height = 28, Margin = new Thickness(6, 0, 16, 0),
                Background = Brushes.White, Foreground = Brushes.Black, FontWeight = FontWeights.Bold
            };
            targetLoaderCombo.Items.Add("Fabric");
            targetLoaderCombo.Items.Add("Forge");
            targetLoaderCombo.SelectedIndex = 0;
            migControls.Children.Add(targetLoaderCombo);

            var migBtn = PageHelpers.MkBtn("⚡ SEÇİLİ SÜRÜME TAŞI VE İNDİR", "#2EB82E", 240);
            migBtn.Foreground = Brushes.White;
            migBtn.Click += async (_, _) => {
                var targetVer = targetVerCombo.SelectedItem?.ToString();
                var targetLoader = targetLoaderCombo.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(targetVer) || string.IsNullOrEmpty(targetLoader)) return;
                await MigrateAndDownloadMods(targetVer, targetLoader, migBtn);
            };
            migControls.Children.Add(migBtn);
            
            migSp.Children.Add(migControls);
            migCard.Child = migSp;
            sp.Children.Add(migCard);

            // ── Kurulu Modlar Bölümü ─────────────────────────────────────────
            sp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 24, 0, 10) });
            
            var instHeader = new Grid();
            instHeader.ColumnDefinitions.Add(new ColumnDefinition());
            instHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var instTitle = PageHelpers.Lbl("📦 Kurulu Modlar", 18, "#A349A4", true);
            Grid.SetColumn(instTitle, 0);
            instHeader.Children.Add(instTitle);
            
            var cleanBtn = PageHelpers.MkBtn("🗑 Klasörü Temizle", "#CC2222", 140);
            cleanBtn.Click += (_, _) => {
                var confirm = MessageBox.Show(
                    "Aktif mod klasöründeki tüm modları silmek istediğinizden emin misiniz?\n\nBu işlem eski/uyumsuz kalıntı modları temizlemek için önerilir.",
                    "Klasörü Temizle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (Directory.Exists(App.ModsDir))
                        {
                            foreach (var jar in Directory.GetFiles(App.ModsDir, "*.jar"))
                            {
                                try { File.Delete(jar); } catch { }
                            }
                        }
                        RenderInstalledMods();
                        MessageBox.Show("Mod klasörü başarıyla temizlendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Temizleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            Grid.SetColumn(cleanBtn, 1);
            instHeader.Children.Add(cleanBtn);
            sp.Children.Add(instHeader);
            
            sp.Children.Add(PageHelpers.Lbl("Mods klasöründeki tüm .jar dosyaları — yanındaki butona tıklayarak silebilirsin.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            _installedPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            sp.Children.Add(_installedPanel);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            // Load popular mods on open
            _ = LoadPopular();
            RenderInstalledMods();
        }

        async Task LoadPopular()
        {
            _searchBox.Text = "optimization";
            await SearchMods();
        }

        async Task SearchMods()
        {
            var q = _searchBox.Text.Trim(); if (string.IsNullOrEmpty(q)) return;
            _resultsPanel.Children.Clear();
            _resultsPanel.Children.Add(PageHelpers.Lbl("Aranıyor...", 13, "#A0A0A0"));
            try
            {
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/5.0");
                var resp = await Http.GetStringAsync($"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(q)}&limit=20&facets=[[\"project_type:mod\"]]");
                var hits = JObject.Parse(resp)["hits"] as JArray;
                _resultsPanel.Children.Clear();
                if (hits == null || hits.Count == 0) { _resultsPanel.Children.Add(PageHelpers.Lbl("Mod bulunamadı.", 13, "#A0A0A0")); return; }
                foreach (var hit in hits)
                {
                    var modId = hit["project_id"]?.ToString() ?? "";
                    var name  = hit["title"]?.ToString() ?? "?";
                    var desc  = hit["description"]?.ToString() ?? "";
                    var dl    = hit["downloads"]?.Value<long>() ?? 0;
                    var mname = name; var cId = modId;

                    var card = PageHelpers.Card("#181818", 10, margin: new Thickness(0, 5, 0, 5));
                    var grid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var info = new StackPanel();
                    info.Children.Add(PageHelpers.Lbl(name, 14, "#FFFFFF", true));
                    info.Children.Add(PageHelpers.Lbl(desc.Length > 120 ? desc[..120] + "..." : desc, 11, "#A0A0A0", wrap: TextWrapping.Wrap));
                    info.Children.Add(PageHelpers.Lbl($"⬇ {dl:N0} indirme", 10, "#555"));
                    Grid.SetColumn(info, 0); grid.Children.Add(info);

                    var installBtn = PageHelpers.MkBtn("KUR", "#00A3FF", 70); installBtn.VerticalAlignment = VerticalAlignment.Center;
                    installBtn.Click += async (_, _) => {
                        installBtn.IsEnabled = false; installBtn.Content = "...";
                        await InstallMod(cId, mname);
                        installBtn.Content = "OK";
                    };
                    Grid.SetColumn(installBtn, 1); grid.Children.Add(installBtn);
                    card.Child = grid; _resultsPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                _resultsPanel.Children.Clear();
                _resultsPanel.Children.Add(PageHelpers.Lbl($"Hata: {ex.Message}", 13, "#FF4B4B"));
            }
        }

        async Task InstallMod(string projectId, string name)
        {
            try
            {
                var installedList = new List<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Start downloading mod and its dependencies recursively
                await DownloadModAndDependencies(projectId, name, installedList, false, visited);

                RenderInstalledMods();

                if (installedList.Count > 1)
                {
                    var depString = string.Join("\n• ", installedList.Skip(1));
                    MessageBox.Show($"'{name}' ve gerekli bağımlılıkları başarıyla kuruldu!\n\nYüklenen Kütüphaneler / Bağımlılıklar:\n• {depString}", "Kurulum Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (installedList.Count == 1)
                {
                    MessageBox.Show($"'{name}' başarıyla kuruldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mod kurulamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        bool IsModAlreadyInstalled(string slug, string name)
        {
            if (!Directory.Exists(App.ModsDir)) return false;
            
            var files = Directory.GetFiles(App.ModsDir, "*.jar");
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file).ToLower();
                
                // 1. Check slug match (e.g. "fabric-api" in "fabric-api-0.102.0.jar")
                if (!string.IsNullOrEmpty(slug) && filename.Contains(slug.ToLower()))
                {
                    return true;
                }
                
                // 2. Check clean name match (e.g. "fabric-api" from "Fabric API")
                var cleanName = name.Replace(" ", "-").Replace("'", "").ToLower();
                if (!string.IsNullOrEmpty(cleanName) && filename.Contains(cleanName))
                {
                    return true;
                }
            }
            return false;
        }

        async Task DownloadModAndDependencies(string projectId, string name, List<string> installedList, bool isDependency = false, HashSet<string>? visited = null)
        {
            visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (visited.Contains(projectId)) return;
            visited.Add(projectId);

            // Fetch project details first to get the official Modrinth slug and title
            string slug = projectId;
            try
            {
                var projInfoStr = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{projectId}");
                var projInfo = JObject.Parse(projInfoStr);
                slug = projInfo["slug"]?.ToString() ?? projectId;
                name = projInfo["title"]?.ToString() ?? name;
            }
            catch { }

            // Safeguard: If the mod or library API is already installed in App.ModsDir, skip it completely!
            if (IsModAlreadyInstalled(slug, name))
            {
                App.Log($"Mod or library '{name}' ({slug}) is already installed. Skipping download and dependencies...");
                return;
            }

            var resp = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{projectId}/version");
            var versions = JArray.Parse(resp);
            if (versions.Count == 0) return;

            // 1. Get launcher's active Minecraft version & loader
            var currentVer = _main.Config.Version ?? "";
            var mcVersion = "1.21.1";
            var mcMatch = System.Text.RegularExpressions.Regex.Match(currentVer, @"1\.\d+(\.\d+)?");
            if (mcMatch.Success) mcVersion = mcMatch.Value;

            var isFabric = currentVer.Contains("fabric", StringComparison.OrdinalIgnoreCase);
            var isForge = currentVer.Contains("forge", StringComparison.OrdinalIgnoreCase);

            JToken? targetVersionObj = FindCompatibleVersion(versions, mcVersion, isFabric, isForge);

            if (targetVersionObj != null)
            {
                // Compatible version found! Download directly to active mods directory.
                var fileUrl = targetVersionObj["files"]?[0]?["url"]?.ToString();
                var fname   = targetVersionObj["files"]?[0]?["filename"]?.ToString() ?? $"{name}.jar";
                if (string.IsNullOrEmpty(fileUrl)) return;

                Directory.CreateDirectory(App.ModsDir);
                var destFile = Path.Combine(App.ModsDir, fname);

                bool alreadyExists = File.Exists(destFile);
                if (!alreadyExists)
                {
                    var bytes = await Http.GetByteArrayAsync(fileUrl);
                    await File.WriteAllBytesAsync(destFile, bytes);
                    App.Log($"Mod installed directly (compatible with {mcVersion}): {fname}");

                    // Firebase Analytics: Mod kurulum istatistiği
                    try { _ = MistikAnalytics.TrackModInstallAsync(_main.Config.User ?? "Oyuncu", name, targetVersionObj["version_number"]?.ToString() ?? "", mcVersion); } catch { }
                }

                if (!installedList.Contains(name))
                {
                    installedList.Add(name);
                }

                // 2. Resolve required dependencies recursively
                var deps = targetVersionObj["dependencies"] as JArray;
                if (deps != null && deps.Count > 0)
                {
                    foreach (var dep in deps)
                    {
                        var depType = dep["dependency_type"]?.ToString();
                        if (depType == "required")
                        {
                            var depProjectId = dep["project_id"]?.ToString();
                            var depVersionId = dep["version_id"]?.ToString();

                            if (!string.IsNullOrEmpty(depProjectId))
                            {
                                try
                                {
                                    var projInfoStr = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{depProjectId}");
                                    var projInfo = JObject.Parse(projInfoStr);
                                    var depName = projInfo["title"]?.ToString() ?? depProjectId;

                                    App.Log($"Installing required dependency for '{name}': {depName} ({depProjectId})");
                                    await DownloadModAndDependencies(depProjectId, depName, installedList, true, visited);
                                }
                                catch (Exception ex)
                                {
                                    App.Log($"Failed to install dependency project {depProjectId}: {ex.Message}");
                                }
                            }
                            else if (!string.IsNullOrEmpty(depVersionId))
                            {
                                try
                                {
                                    var depVerStr = await Http.GetStringAsync($"https://api.modrinth.com/v2/version/{depVersionId}");
                                    var depVer = JObject.Parse(depVerStr);
                                    var depProjId = depVer["project_id"]?.ToString();
                                    if (!string.IsNullOrEmpty(depProjId))
                                    {
                                        var projInfoStr = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{depProjId}");
                                        var projInfo = JObject.Parse(projInfoStr);
                                        var depName = projInfo["title"]?.ToString() ?? depProjId;

                                        App.Log($"Installing required dependency version for '{name}': {depName} ({depProjId})");
                                        await DownloadModAndDependencies(depProjId, depName, installedList, true, visited);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    App.Log($"Failed to install dependency version {depVersionId}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // No compatible version found! Download latest and queue in the pool (only for the root mod).
                if (!isDependency)
                {
                    var latestVersion = versions[0];
                    var fileUrl = latestVersion["files"]?[0]?["url"]?.ToString();
                    var fname   = latestVersion["files"]?[0]?["filename"]?.ToString() ?? $"{name}.jar";
                    if (string.IsNullOrEmpty(fileUrl)) return;

                    var gameVers = latestVersion["game_versions"] as JArray;
                    var targetGameVer = gameVers != null && gameVers.Count > 0 ? gameVers[0].ToString() : "1.20.1";

                    // Save to compatibility pool under target version directory
                    var poolDir = Path.Combine(App.AppData, "mods_pool", targetGameVer);
                    Directory.CreateDirectory(poolDir);
                    var destFile = Path.Combine(poolDir, fname);

                    var bytes = await Http.GetByteArrayAsync(fileUrl);
                    await File.WriteAllBytesAsync(destFile, bytes);
                    App.Log($"Mod queued in compatibility pool for {targetGameVer}: {fname}");

                    MessageBox.Show($"'{name}' modu şu anki oyun sürümünüz ({mcVersion}) ile uyumsuz!\n\nUyumlu olduğu '{targetGameVer}' sürümünün bekleme klasörüne (mods_pool/{targetGameVer}) indirildi.\n\nOyun sürümünüzü '{targetGameVer}' yaptığınızda otomatik olarak aktif edilecektir!", "Sürüm Beklemeye Alındı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        async Task InstallModpack(string[] slugs, string[] names, Button btn, string packName)
        {
            btn.IsEnabled = false;
            var originalText = btn.Content.ToString();
            try
            {
                // Active profile info
                var currentVer = _main.Config.Version ?? "";
                var mcVersion = "1.21.1";
                var mcMatch = System.Text.RegularExpressions.Regex.Match(currentVer, @"1\.\d+(\.\d+)?");
                if (mcMatch.Success) mcVersion = mcMatch.Value;

                var isFabric = currentVer.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
                               currentVer.Contains("quilt", StringComparison.OrdinalIgnoreCase);
                var isForge = currentVer.Contains("forge", StringComparison.OrdinalIgnoreCase);

                // Eğer kullanıcı vanilla'daysa mod paketi kurmak risklidir — uyarı ver
                if (!isFabric && !isForge)
                {
                    var answer = MessageBox.Show(
                        $"Şu an aktif sürümünüz '{currentVer}' bir mod yükleyicisine (Fabric/Forge) sahip değil gibi görünüyor.\n\n" +
                        $"'{packName}' paketi Fabric/Forge gerektirir. Yine de devam etmek istiyor musunuz?",
                        "Uyarı - Mod Yükleyicisi Yok", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                }

                Directory.CreateDirectory(App.ModsDir);
                var allInstalled = new List<string>();
                var globalVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int successCount = 0;
                int skippedCount = 0;

                for (int i = 0; i < slugs.Length; i++)
                {
                    var slug = slugs[i];
                    var name = names[i];
                    btn.Content = $"{name} ({i + 1}/{slugs.Length})...";

                    try
                    {
                        var resp = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{slug}/version");
                        var versions = JArray.Parse(resp);
                        if (versions.Count == 0) { skippedCount++; continue; }

                        // Akıllı sürüm eşleştirme: tam eşleşme → minor fallback → en yakın patch
                        JToken? targetVersionObj = FindCompatibleVersionSmart(versions, mcVersion, isFabric, isForge);

                        if (targetVersionObj == null)
                        {
                            App.Log($"[ModPack] UYARI: '{name}' modunun {mcVersion} sürümüyle uyumlu versiyonu bulunamadı! Atlanıyor.");
                            skippedCount++;
                            continue; // Yanlış sürümü indirmektense atla!
                        }

                        var fileUrl = targetVersionObj["files"]?[0]?["url"]?.ToString();
                        var fname   = targetVersionObj["files"]?[0]?["filename"]?.ToString() ?? $"{name}.jar";
                        var actualGameVers = targetVersionObj["game_versions"] as JArray;
                        var matchedVer = actualGameVers?.FirstOrDefault()?.ToString() ?? mcVersion;

                        if (string.IsNullOrEmpty(fileUrl)) { skippedCount++; continue; }

                        // Zaten kuruluysa atla
                        if (!IsModAlreadyInstalled(slug, name))
                        {
                            var bytes = await Http.GetByteArrayAsync(fileUrl);
                            await File.WriteAllBytesAsync(Path.Combine(App.ModsDir, fname), bytes);
                            App.Log($"Modpack [{packName}] - Mod kuruldu: {fname} (MC {matchedVer} ile uyumlu)");
                            successCount++;
                        }
                        allInstalled.Add(name);
                        globalVisited.Add(slug);

                        // Bağımlılıkları da çöz ve indir (Fabric API, vb.)
                        btn.Content = $"{name} bağımlılıkları ({i + 1}/{slugs.Length})...";
                        var deps = targetVersionObj["dependencies"] as JArray;
                        if (deps != null)
                        {
                            foreach (var dep in deps)
                            {
                                if (dep["dependency_type"]?.ToString() != "required") continue;

                                var depProjectId = dep["project_id"]?.ToString();
                                var depVersionId = dep["version_id"]?.ToString();

                                if (!string.IsNullOrEmpty(depProjectId) && !globalVisited.Contains(depProjectId))
                                {
                                    try
                                    {
                                        await DownloadModAndDependencies(depProjectId, depProjectId, allInstalled, true, globalVisited);
                                    }
                                    catch (Exception dex)
                                    {
                                        App.Log($"[ModPack] Bağımlılık indirme hatası ({depProjectId}): {dex.Message}");
                                    }
                                }
                                else if (!string.IsNullOrEmpty(depVersionId))
                                {
                                    try
                                    {
                                        var depVerStr = await Http.GetStringAsync($"https://api.modrinth.com/v2/version/{depVersionId}");
                                        var depVer = JObject.Parse(depVerStr);
                                        var depProjId = depVer["project_id"]?.ToString();
                                        if (!string.IsNullOrEmpty(depProjId) && !globalVisited.Contains(depProjId))
                                        {
                                            await DownloadModAndDependencies(depProjId, depProjId, allInstalled, true, globalVisited);
                                        }
                                    }
                                    catch (Exception dex)
                                    {
                                        App.Log($"[ModPack] Bağımlılık (version) indirme hatası ({depVersionId}): {dex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception modEx)
                    {
                        App.Log($"[ModPack] '{name}' kurulurken hata: {modEx.Message}");
                        skippedCount++;
                    }
                }

                btn.Content = "✓ KURULDU!";
                RenderInstalledMods();

                string resultMsg = $"'{packName}' başarıyla kuruldu!\n\n" +
                    $"✅ Kurulan: {successCount} mod + bağımlılıkları\n" +
                    $"MC Sürümü: {mcVersion}";
                if (skippedCount > 0)
                    resultMsg += $"\n⚠️ {skippedCount} mod sürüm uyumsuzluğu nedeniyle atlandı.";
                if (allInstalled.Count > slugs.Length)
                    resultMsg += $"\n📦 Toplam indirilen (bağımlılıklar dahil): {allInstalled.Count}";

                MessageBox.Show(resultMsg, "Kurulum Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                btn.Content = "Hata!";
                MessageBox.Show($"Mod paketi kurulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(3000);
                btn.Content = originalText;
                btn.IsEnabled = true;
            }
        }
        void RenderInstalledMods()
        {
            _installedPanel.Children.Clear();

            if (!Directory.Exists(App.ModsDir))
            {
                _installedPanel.Children.Add(PageHelpers.Lbl("Henüz hiçbir mod yüklü değil.", 12, "#555555"));
                return;
            }

            var jars = Directory.GetFiles(App.ModsDir, "*.jar");
            if (jars.Length == 0)
            {
                _installedPanel.Children.Add(PageHelpers.Lbl("Henüz hiçbir mod yüklü değil.", 12, "#555555"));
                return;
            }

            foreach (var jarPath in jars.OrderBy(f => Path.GetFileName(f)))
            {
                var fileName = Path.GetFileName(jarPath);
                var filePath = jarPath; // capture for lambda

                // Format file size
                long bytes = 0;
                try { bytes = new FileInfo(filePath).Length; } catch { }
                string sizeStr = bytes >= 1_048_576
                    ? $"{bytes / 1_048_576.0:F1} MB"
                    : $"{bytes / 1024.0:F0} KB";

                var card = PageHelpers.Card("#131318", 10, "#2a2a3a");
                card.Margin = new Thickness(0, 4, 0, 4);

                var grid = new Grid { Margin = new Thickness(14, 10, 14, 10) };
                grid.ColumnDefinitions.Add(new ColumnDefinition()); // name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // size
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // btn

                var nameLbl = PageHelpers.Lbl(fileName, 12, "#DDDDDD");
                nameLbl.TextTrimming = System.Windows.TextTrimming.CharacterEllipsis;
                Grid.SetColumn(nameLbl, 0);
                grid.Children.Add(nameLbl);

                var sizeLbl = PageHelpers.Lbl(sizeStr, 11, "#666666");
                sizeLbl.Margin = new Thickness(12, 0, 12, 0);
                Grid.SetColumn(sizeLbl, 1);
                grid.Children.Add(sizeLbl);

                var delBtn = PageHelpers.MkBtn("🗑 SİL", "#CC2222", 80);
                delBtn.Click += (_, _) =>
                {
                    var confirm = MessageBox.Show(
                        $"Bu modu kalıcı olarak silmek istiyor musun?\n\n{fileName}",
                        "Modu Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(filePath);
                            App.Log($"Mod deleted by user: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Mod silinemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        RenderInstalledMods();
                    }
                };
                Grid.SetColumn(delBtn, 2);
                grid.Children.Add(delBtn);

                card.Child = grid;
                _installedPanel.Children.Add(card);
            }
        }

        internal static JToken? FindCompatibleVersion(JArray versions, string mcVersion, bool isFabric, bool isForge)
        {
            // Pass 1: Release only — tam sürüm eşleşmesi
            foreach (var v in versions)
            {
                if (v["version_type"]?.ToString().Equals("release", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (CheckCompatibility(v, mcVersion, isFabric, isForge)) return v;
                }
            }

            // Pass 2: Beta only
            foreach (var v in versions)
            {
                if (v["version_type"]?.ToString().Equals("beta", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (CheckCompatibility(v, mcVersion, isFabric, isForge)) return v;
                }
            }

            // Pass 3: Alpha/Any
            foreach (var v in versions)
            {
                if (CheckCompatibility(v, mcVersion, isFabric, isForge)) return v;
            }

            return null;
        }

        /// <summary>
        /// Akıllı sürüm eşleştirme: Tam eşleşme bulamazsa minor sürüm fallback'i ve
        /// en yakın patch sürümünü deneyerek en uyumlu versiyonu bulur.
        /// Asla yanlış sürüm indirmez!
        /// </summary>
        internal static JToken? FindCompatibleVersionSmart(JArray versions, string mcVersion, bool isFabric, bool isForge)
        {
            // 1. Tam eşleşme (normal yol)
            var exact = FindCompatibleVersion(versions, mcVersion, isFabric, isForge);
            if (exact != null) return exact;

            // 2. Minor sürüm fallback: "1.21.1" bulunamazsa "1.21" dene
            var parts = mcVersion.Split('.');
            if (parts.Length == 3)
            {
                var minorVersion = $"{parts[0]}.{parts[1]}";
                var minorMatch = FindCompatibleVersion(versions, minorVersion, isFabric, isForge);
                if (minorMatch != null)
                {
                    App.Log($"[SmartVersion] Tam eşleşme ({mcVersion}) bulunamadı, minor eşleşme ({minorVersion}) kullanılıyor.");
                    return minorMatch;
                }
            }

            // 3. En yakın patch sürümünü dene (1.21.1 bulunamazsa 1.21.2, 1.21.0, 1.21.3, 1.21.4 vb.)
            if (parts.Length >= 2 && int.TryParse(parts.Length >= 3 ? parts[2] : "0", out int patchNum))
            {
                string majorMinor = $"{parts[0]}.{parts[1]}";
                // Önce yakın patch'ler, en fazla ±5 aralığında
                for (int delta = 1; delta <= 5; delta++)
                {
                    foreach (int candidate in new[] { patchNum - delta, patchNum + delta })
                    {
                        if (candidate < 0) continue;
                        string tryVer = candidate == 0 ? majorMinor : $"{majorMinor}.{candidate}";
                        var found = FindCompatibleVersion(versions, tryVer, isFabric, isForge);
                        if (found != null)
                        {
                            App.Log($"[SmartVersion] Tam eşleşme ({mcVersion}) bulunamadı, en yakın patch ({tryVer}) kullanılıyor.");
                            return found;
                        }
                    }
                }
            }

            // 4. Hiçbir şey bulunamadı — null dön, yanlış sürüm indirme!
            App.Log($"[SmartVersion] '{mcVersion}' için hiçbir uyumlu sürüm bulunamadı.");
            return null;
        }

        internal static bool CheckCompatibility(JToken versionObj, string mcVersion, bool isFabric, bool isForge)
        {
            var gameVers = versionObj["game_versions"] as JArray;
            var loaders = versionObj["loaders"] as JArray;
            if (gameVers == null || loaders == null) return false;

            bool supportsMc = gameVers.Any(gv => gv.ToString().Equals(mcVersion, StringComparison.OrdinalIgnoreCase));
            bool supportsLoader = true;
            if (isFabric)
            {
                // Fabric ve Quilt loader'ları kabul et
                supportsLoader = loaders.Any(l =>
                    l.ToString().Equals("fabric", StringComparison.OrdinalIgnoreCase) ||
                    l.ToString().Equals("quilt", StringComparison.OrdinalIgnoreCase));
            }
            else if (isForge)
            {
                supportsLoader = loaders.Any(l =>
                    l.ToString().Equals("forge", StringComparison.OrdinalIgnoreCase) ||
                    l.ToString().Equals("neoforge", StringComparison.OrdinalIgnoreCase));
            }
            // Not: isFabric == false && isForge == false ise (vanilla), loader kontrolü geçilir
            // Bu durumda modpack kurulumu öncesinde zaten uyarı verilmiştir

            return supportsMc && supportsLoader;
        }

        private async Task MigrateAndDownloadMods(string targetVer, string loader, Button btn)
        {
            try
            {
                if (!Directory.Exists(App.ModsDir))
                {
                    MessageBox.Show("Aktif mod klasöründe taşıyacak hiç mod bulunamadı!", "Mod Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var jars = Directory.GetFiles(App.ModsDir, "*.jar");
                if (jars.Length == 0)
                {
                    MessageBox.Show("Aktif mod klasöründe (.jar uzantılı) hiç mod bulunamadı!", "Mod Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show(
                    $"Mevcut aktif mod klasörünüzdeki {jars.Length} mod taranacak, '{targetVer} - {loader}' uyumlu sürümleri Modrinth'ten aranıp sıfırdan indirilecektir.\n\n" +
                    $"Mevcut aktif modlarınız otomatik olarak '{_main.Config.Version}' sürüm havuzuna güvenle yedeklenecektir (askıya alınacaktır).\n\n" +
                    "Devam etmek istiyor musunuz?",
                    "Mod Sürüm Taşıma", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                btn.IsEnabled = false;
                var origText = btn.Content.ToString();
                btn.Content = "Yedekleniyor & Sürüm değiştiriliyor...";

                // 1. Modların kimliklerini (Mod ID / Clean Name) asenkron olarak oku
                var modIds = new List<string>();
                var jarFileNames = new List<string>();
                await Task.Run(() => {
                    foreach (var jar in jars)
                    {
                        var id = GetModIdFromJar(jar);
                        if (!string.IsNullOrEmpty(id))
                        {
                            modIds.Add(id);
                            jarFileNames.Add(Path.GetFileName(jar));
                        }
                    }
                });

                if (modIds.Count == 0)
                {
                    MessageBox.Show("Mod dosyaları okunamadı veya geçerli mod bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    btn.IsEnabled = true;
                    btn.Content = origText;
                    return;
                }

                // 2. Mevcut modları güvenle askıya al (SyncModsForCurrentVersion)
                _main.SyncModsForCurrentVersion();

                // 3. Launcher ana sürümünü hedef sürüme geçir (VerBox listesinde arayarak)
                string matchedVerName = "";
                _main.Dispatcher.Invoke(() => {
                    foreach (var item in _main.VerBox.Items)
                    {
                        var itemStr = item.ToString() ?? "";
                        if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
                        {
                            if (itemStr.Contains("fabric", StringComparison.OrdinalIgnoreCase) && itemStr.Contains(targetVer))
                            {
                                matchedVerName = itemStr;
                                break;
                            }
                        }
                        else if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
                        {
                            if (itemStr.Contains("forge", StringComparison.OrdinalIgnoreCase) && itemStr.Contains(targetVer))
                            {
                                matchedVerName = itemStr;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(matchedVerName))
                    {
                        // Eşleşen yükleyici sürümü bulunamadıysa düz vanilla/hedef versiyon ismini seç
                        foreach (var item in _main.VerBox.Items)
                        {
                            var itemStr = item.ToString() ?? "";
                            if (itemStr.Equals(targetVer))
                            {
                                matchedVerName = itemStr;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(matchedVerName))
                    {
                        matchedVerName = targetVer; // fallback
                    }

                    _main.VerBox.SelectedItem = matchedVerName;
                    _main.Config.Version = matchedVerName;
                    _main.Config.LastSyncedVersion = matchedVerName; // Force sync to this new version
                    ConfigManager.Save(_main.Config);
                    _main.StatusLbl.Text = $"Surum: {matchedVerName}";
                });

                // 4. Mod klasörünü temizle (Sürüm değiştiği için taze indirme)
                Directory.CreateDirectory(App.ModsDir);
                foreach (var jar in Directory.GetFiles(App.ModsDir, "*.jar"))
                {
                    try { File.Delete(jar); } catch { }
                }

                // 5. Her bir mod için Modrinth'ten uyumlu sürümü bul ve indir
                int downloadedCount = 0;
                var failedMods = new List<string>();

                bool isFabric = loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase);
                bool isForge = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase);

                for (int i = 0; i < modIds.Count; i++)
                {
                    var id = modIds[i];
                    var originalJarName = jarFileNames[i];
                    btn.Content = $"İndiriliyor: {originalJarName} ({i + 1}/{modIds.Count})...";

                    try
                    {
                        // Query project versions
                        Http.DefaultRequestHeaders.UserAgent.Clear();
                        Http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/5.0");
                        var resp = await Http.GetStringAsync($"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(id)}/version");
                        var versions = JArray.Parse(resp);

                        JToken? targetVersionObj = FindCompatibleVersion(versions, targetVer, isFabric, isForge);
                        if (targetVersionObj != null)
                        {
                            var fileUrl = targetVersionObj["files"]?[0]?["url"]?.ToString();
                            var fname   = targetVersionObj["files"]?[0]?["filename"]?.ToString() ?? $"{id}.jar";
                            if (!string.IsNullOrEmpty(fileUrl))
                            {
                                var bytes = await Http.GetByteArrayAsync(fileUrl);
                                await File.WriteAllBytesAsync(Path.Combine(App.ModsDir, fname), bytes);
                                downloadedCount++;
                                App.Log($"Bulk Migrator: Successfully migrated and installed mod: {fname}");
                            }
                        }
                        else
                        {
                            failedMods.Add(originalJarName);
                            App.Log($"Bulk Migrator: No compatible version found for {id} on {targetVer} {loader}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedMods.Add(originalJarName);
                        App.Log($"Bulk Migrator: Failed to download {id}: {ex.Message}");
                    }
                }

                // 6. Sonuçları göster
                RenderInstalledMods();
                
                string msg = $"Mod taşıma işlemi tamamlandı!\n\n" +
                             $"🎯 Hedef Sürüm: {matchedVerName} ({loader})\n" +
                             $"✅ Başarıyla İndirilen: {downloadedCount} mod\n";

                if (failedMods.Count > 0)
                {
                    msg += $"❌ Uyumlu Sürümü Bulunamayan ({failedMods.Count}):\n" + string.Join("\n", failedMods.Take(8));
                    if (failedMods.Count > 8) msg += $"\n...ve {failedMods.Count - 8} mod daha.";
                }

                MessageBox.Show(msg, "Taşıma Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);

                btn.Content = origText;
                btn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mod taşıma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                btn.Content = "Hata!";
                btn.IsEnabled = true;
            }
        }

        private static string? GetModIdFromJar(string jarPath)
        {
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(jarPath);
                
                // Try Fabric first
                var fabricEntry = archive.GetEntry("fabric.mod.json");
                if (fabricEntry != null)
                {
                    using var reader = new StreamReader(fabricEntry.Open());
                    var jsonStr = reader.ReadToEnd();
                    var jObj = JObject.Parse(jsonStr);
                    return jObj["id"]?.ToString() ?? jObj["name"]?.ToString();
                }

                // Try Forge mods.toml
                var tomlEntry = archive.GetEntry("META-INF/mods.toml");
                if (tomlEntry != null)
                {
                    using var reader = new StreamReader(tomlEntry.Open());
                    var content = reader.ReadToEnd();
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"modId\s*=\s*""([^""]+)""");
                    if (match.Success) return match.Groups[1].Value;
                }

                // Try older Forge mcmod.info
                var infoEntry = archive.GetEntry("mcmod.info");
                if (infoEntry != null)
                {
                    using var reader = new StreamReader(infoEntry.Open());
                    var content = reader.ReadToEnd();
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"""modid""\s*:\s*""([^""]+)""");
                    if (match.Success) return match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                App.Log($"Error reading jar {Path.GetFileName(jarPath)}: {ex.Message}");
            }

            // Fallback: Use filename cleaned
            var name = Path.GetFileNameWithoutExtension(jarPath);
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[-_]\d+.*", ""); // Strip version like -1.20.1-0.5.0
            name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)(fabric|forge|neoforge|loader)", ""); // Strip loaders
            return name.Trim('-', '_');
        }
    }
}
