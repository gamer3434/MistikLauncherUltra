using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace MistikLauncher
{
    public partial class MainWindow : Window
    {
        public LauncherConfig Config;
        public MistikRelay?   Relay;
        public string? LatestOnlineVersion;
        public string? LatestOnlineUrl;
        public string? LatestOnlineChangelog;
        readonly Dictionary<string, Button> _navBtns = new();
        readonly Dictionary<string, Page> _pageCache = new();
        readonly HttpClient _http = new();
        string _accent = "#00A3FF";
        bool _isPopulatingVersionBox = false;

        public MainWindow()
        {
            SetBrowserEmulation();
            InitializeComponent();
            Config = ConfigManager.Load();
            Config.OpenCount++;
            // Clean up and validate config version to prevent launching fake versions like 26.1.1
            if (!string.IsNullOrEmpty(Config.Version) && (Config.Version.Contains("26.") || Config.Version.Contains("1.26")))
            {
                Config.Version = "fabric-loader-0.19.2-1.21.1";
                Config.LastSyncedVersion = "fabric-loader-0.19.2-1.21.1";
            }
            ConfigManager.Save(Config);

            _accent = Config.Accent switch {
                "Red"    => "#FF4B4B",
                "Green"  => "#2EB82E",
                "Purple" => "#A349A4",
                "Orange" => "#FFB100",
                _        => "#00A3FF"
            };
            ApplyAccent(_accent);
            BuildNav();
            PopulateVersionBox();
            LoadAvatar();

            VerBox.SelectionChanged += (s, e) => {
                if (_isPopulatingVersionBox) return;
                var selected = VerBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selected))
                {
                    Config.Version = selected;
                    ConfigManager.Save(Config);
                    StatusLbl.Text = $"Surum: {selected}";
                    SyncModsForCurrentVersion();
                }
            };

            BtnLaunch.Click  += (_, _) => HandleLaunch();
            BtnDiscord.Click += (_, _) => Open("https://discord.gg/");
            BtnYoutube.Click += (_, _) => Open("https://www.youtube.com/@kardoeditx99");

            Navigate("Dash");
            _ = StartRelayAsync();
            _ = RelayLoopAsync();
            // Arka planda otomatik güncelleme kontrolü aktif edildi.
            _ = Task.Delay(2000).ContinueWith(async _ => await CheckCloudUpdateAsync(false));

            // Firebase Analytics: Oturum başlangıcı
            _ = MistikAnalytics.TrackSessionStartAsync(Config.User ?? "Oyuncu", App.LocalVersion, Config.Version ?? "1.21");
            _ = CheckRemoteSettingsAsync();
            InitializeStartupOptimizationsAsync();

            // Kapanışta oturum kaydı
            Closing += async (s, e) =>
            {
                try { await MistikAnalytics.TrackSessionEndAsync(Config.User ?? "Oyuncu"); } catch { }
            };
        }

        static void Open(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private async void InitializeStartupOptimizationsAsync()
        {
            try
            {
                // GPU Algılama
                string gpuName = KernelOptimizer.DetectGpuName();
                App.Log($"[Startup] Algılanan Ekran Kartı: {gpuName}");

                // Firebase'e GPU bilgisi gönderme
                _ = MistikAnalytics.TrackGpuInfoAsync(Config.User ?? "Oyuncu", gpuName);

                // NVIDIA Profil Kaydı ve GPU tercihi
                await Task.Run(() =>
                {
                    try
                    {
                        KernelOptimizer.ApplyGpuPreference(Process.GetCurrentProcess());
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[Startup GPU Opt Hata] {ex.Message}");
                    }
                });

                // Optimizasyon Durum Tespiti
                await Task.Run(() =>
                {
                    try
                    {
                        var opts = KernelOptimizer.DetectCurrentOptimizations();
                        App.Log("[Startup] Mevcut Optimizasyon Durumları:");
                        foreach (var kv in opts)
                        {
                            App.Log($"  - {kv.Key}: {(kv.Value ? "AKTİF" : "PASİF")}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[Startup Opt Hata] {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                App.Log($"[InitializeStartupOptimizationsAsync Hata] {ex.Message}");
            }
        }

        // ── Accent ────────────────────────────────────────────────────────────
        void ApplyAccent(string hex)
        {
            var c = HexColor(hex);
            LogoText.Foreground  = new SolidColorBrush(c);
            BtnLaunch.Background = new SolidColorBrush(c);
            GlobalProgress.Foreground = new SolidColorBrush(c);
        }

        public static Color HexColor(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        public static SolidColorBrush HexBrush(string hex) => new(HexColor(hex));

        // ── Nav ───────────────────────────────────────────────────────────────
        void BuildNav()
        {
            NavPanel.Children.Clear(); _navBtns.Clear();
            AddNav("Dash",      "🏠  Ana Panel");
            AddNav("Vers",      "🎮  Sürüm Yöneticisi");
            AddNav("Mods",      "📦  Mod Merkezi");
            AddNav("Skin",      "👕  Karakter Cildi");
            AddNav("Server",    "🌐  Sunucu Kur");
            AddNav("Opt",       "🚀  Optimizasyon");
            if (Config.Role == "Yonetici")
                AddNav("Admin", "👑  Yönetici Paneli");
            AddNav("Settings",  "⚙️  Ayarlar");
        }

        void AddNav(string key, string label)
        {
            var btn = new Button { Content = label, Style = (Style)FindResource("NavBtn") };
            btn.Click += (_, _) => Navigate(key);
            NavPanel.Children.Add(btn);
            _navBtns[key] = btn;
        }

        void SelectNav(string key)
        {
            foreach (var b in _navBtns.Values) b.Background = Brushes.Transparent;
            if (_navBtns.TryGetValue(key, out var btn)) btn.Background = HexBrush(_accent);
        }

        public void Navigate(string key)
        {
            SelectNav(key);

            // Server sayfası her zaman cache'den gelsin — sunucu kapanmasın!
            // Diğer sayfalar da cache'e alınır (hızlı geçiş için).
            if (!_pageCache.TryGetValue(key, out Page? page) || page == null)
            {
                page = key switch {
                    "Dash"      => new Pages.DashboardPage(this),
                    "Vers"      => new Pages.VersionManagerPage(this),
                    "Mods"      => new Pages.ModManagerPage(this),
                    "Skin"      => new Pages.SkinPage(this),
                    "Elyby"     => new Pages.ElybyPage(this),
                    "Friends"   => new Pages.FriendsPage(this),
                    "Server"    => new Pages.ServerManagerPage(this),
                    "Changelog" => new Pages.ChangelogPage(this),
                    "Admin"     => new Pages.AdminPanelPage(this),
                    "Opt"       => new Pages.OptimizationPage(this),
                    "Guide"     => new Pages.GuidePage(this),
                    "Settings"  => new Pages.SettingsPage(this),
                    "Licenses"  => new Pages.LicensesPage(this),
                    _           => new Pages.DashboardPage(this)
                };
                _pageCache[key] = page;
            }

            MainFrame.Navigate(page);
        }

        // Belirli bir sayfanın cache'ini temizler (yeniden oluşturmak için)
        public void InvalidatePageCache(string key) => _pageCache.Remove(key);


        // ── Version box ───────────────────────────────────────────────────────
        public void PopulateVersionBox()
        {
            _isPopulatingVersionBox = true;
            try
            {
                SyncModsForCurrentVersion();
                VerBox.Items.Clear();
                var uniqueVersions = new HashSet<string>();

                // 1. Scan downloaded versions (Ensure both jar and json exist for validity)
                try {
                    var versDir = Path.Combine(App.GameDir, "versions");
                    if (Directory.Exists(versDir))
                    {
                        foreach (var d in Directory.GetDirectories(versDir))
                        {
                            var name = Path.GetFileName(d);
                            if (!string.IsNullOrEmpty(name))
                            {
                                var jarFile = Path.Combine(d, $"{name}.jar");
                                var jsonFile = Path.Combine(d, $"{name}.json");
                                if (File.Exists(jarFile) && File.Exists(jsonFile))
                                {
                                    uniqueVersions.Add(name);
                                }
                            }
                        }
                    }
                } catch { }

                // 2. Add complete Minecraft version history from 1.8 up to latest 26.2.2 (Mojang's new 2026 format)
                var defaults = new[] { 
                    "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", 
                    "1.20.6", "1.20.5", "1.20.4", "1.20.3", "1.20.2", "1.20.1", "1.20", 
                    "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19", 
                    "1.18.2", "1.18.1", "1.18", 
                    "1.17.1", "1.17", 
                    "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1", "1.16", 
                    "1.15.2", "1.15.1", "1.15", 
                    "1.14.4", "1.14.3", "1.14.2", "1.14.1", "1.14", 
                    "1.13.2", "1.13.1", "1.13", 
                    "1.12.2", "1.12.1", "1.12", 
                    "1.11.2", "1.11.1", "1.11", 
                    "1.10.2", "1.10.1", "1.10", 
                    "1.9.4", "1.9.2", "1.9", 
                    "1.8.9", "1.8.8", "1.8"
                };
                foreach (var v in defaults)
                    uniqueVersions.Add(v);

                // 3. Add config version
                if (!string.IsNullOrEmpty(Config.Version))
                    uniqueVersions.Add(Config.Version);

                // 4. Sort version list nicely (latest at the top)
                var sorted = uniqueVersions.ToList();
                sorted.Sort((a, b) => {
                    var partsA = GetVersionNumbers(a);
                    var partsB = GetVersionNumbers(b);
                    for (int i = 0; i < Math.Max(partsA.Count, partsB.Count); i++)
                    {
                        int numA = i < partsA.Count ? partsA[i] : 0;
                        int numB = i < partsB.Count ? partsB[i] : 0;
                        if (numA != numB) return numB.CompareTo(numA);
                    }
                    return string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
                });

                // 5. Populate VerBox
                foreach (var v in sorted)
                {
                    VerBox.Items.Add(v);
                }

                VerBox.SelectedItem = Config.Version;
                if (VerBox.SelectedItem == null && VerBox.Items.Count > 0)
                    VerBox.SelectedIndex = 0;

                UserNameLbl.Text = Config.User;
                StatusLbl.Text   = $"Surum: {VerBox.SelectedItem}";
            }
            finally
            {
                _isPopulatingVersionBox = false;
            }
        }

        static List<int> GetVersionNumbers(string input)
        {
            var list = new List<int>();
            var matches = Regex.Matches(input, @"\d+");
            foreach (Match m in matches)
            {
                if (int.TryParse(m.Value, out var n))
                    list.Add(n);
            }
            return list;
        }

        // ── Avatar ────────────────────────────────────────────────────────────
        public static System.Windows.Media.ImageSource? GetSkinFace(string filePath)
        {
            try {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                    
                    if ((bmp.PixelWidth == 64 && bmp.PixelHeight == 64) || (bmp.PixelWidth == 64 && bmp.PixelHeight == 32)) {
                        var baseFace = new System.Windows.Media.Imaging.CroppedBitmap(bmp, new Int32Rect(8, 8, 8, 8));
                        baseFace.Freeze();

                        var overlayFace = new System.Windows.Media.Imaging.CroppedBitmap(bmp, new Int32Rect(40, 8, 8, 8));
                        overlayFace.Freeze();

                        var drawingVisual = new DrawingVisual();
                        using (var drawingContext = drawingVisual.RenderOpen()) {
                            drawingContext.DrawImage(baseFace, new Rect(0, 0, 8, 8));
                            drawingContext.DrawImage(overlayFace, new Rect(0, 0, 8, 8));
                        }

                        var renderTargetBitmap = new RenderTargetBitmap(8, 8, 96, 96, PixelFormats.Pbgra32);
                        renderTargetBitmap.Render(drawingVisual);
                        renderTargetBitmap.Freeze();

                        return renderTargetBitmap;
                    }
                    return null;
                }
            } catch {
                return null;
            }
        }

        public void LoadAvatar()
        {
            _ = Task.Run(async () => {
                // 1. Yerel skin dosyası varsa onu kullan
                if (Config.SkinType == "local" && !string.IsNullOrEmpty(Config.SkinUser) && File.Exists(Config.SkinUser))
                {
                    Dispatcher.Invoke(() => {
                        try {
                            var face = GetSkinFace(Config.SkinUser);
                            if (face != null) {
                                AvatarImg.Source = face;
                                System.Windows.Media.RenderOptions.SetBitmapScalingMode(AvatarImg, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                            }
                        } catch { }
                    });
                    return;
                }

                // 2. Her zaman Ely.by'den skin çekmeyi dene (AuthType ne olursa olsun)
                var uname = Config.SkinType == "username" ? Config.SkinUser : Config.User;
                if (!string.IsNullOrEmpty(uname))
                {
                    var cache = Path.Combine(App.AppData, $"elyby_{uname}.png");
                    bool success = false;
                    try {
                        if (!File.Exists(cache) || (DateTime.Now - File.GetLastWriteTime(cache)).TotalDays > 1) {
                            var jsonStr = await _http.GetStringAsync($"http://skinsystem.ely.by/textures/{uname}");
                            var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                            var texUrl = jObj["SKIN"]?["url"]?.ToString();
                            if (!string.IsNullOrEmpty(texUrl)) {
                                var bytes = await _http.GetByteArrayAsync(texUrl);
                                Directory.CreateDirectory(App.AppData);
                                await File.WriteAllBytesAsync(cache, bytes);
                            }
                        }
                        if (File.Exists(cache)) {
                            Dispatcher.Invoke(() => {
                                var face = GetSkinFace(cache);
                                if (face != null) {
                                    AvatarImg.Source = face;
                                    System.Windows.Media.RenderOptions.SetBitmapScalingMode(AvatarImg, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                                    success = true;
                                }
                            });
                        }
                    } catch { }

                    if (success) return;

                    // 3. Ely.by başarısız olduysa mc-heads fallback
                    try {
                        var img = await FetchAvatarAsync(uname, 40);
                        Dispatcher.Invoke(() => { if (img != null) AvatarImg.Source = img; });
                    } catch { }
                }
            });
        }

        public async Task<System.Windows.Media.ImageSource?> FetchAvatarAsync(string username, int size = 64)
        {
            var elybyCache = Path.Combine(App.AppData, $"elyby_{username}.png");
            var cache = Path.Combine(App.AppData, $"avatar_{username}_{size}.png");
            try {
                // Önce Ely.by'den skin denemesi yapalım
                try {
                    if (!File.Exists(elybyCache) || (DateTime.Now - File.GetLastWriteTime(elybyCache)).TotalDays > 1) {
                        var jsonStr = await _http.GetStringAsync($"http://skinsystem.ely.by/textures/{Uri.EscapeDataString(username)}");
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                        var texUrl = jObj["SKIN"]?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(texUrl)) {
                            var elyBytes = await _http.GetByteArrayAsync(texUrl);
                            Directory.CreateDirectory(App.AppData);
                            await File.WriteAllBytesAsync(elybyCache, elyBytes);
                        }
                    }
                    if (File.Exists(elybyCache)) {
                        var faceSrc = GetSkinFace(elybyCache);
                        if (faceSrc != null) {
                            return faceSrc;
                        }
                    }
                } catch { }

                byte[] bytes;
                if (File.Exists(cache))
                    bytes = await File.ReadAllBytesAsync(cache);
                else {
                    bytes = await _http.GetByteArrayAsync(
                        $"https://mc-heads.net/avatar/{Uri.EscapeDataString(username)}/{size}",
                        new CancellationTokenSource(5000).Token);
                    Directory.CreateDirectory(App.AppData);
                    await File.WriteAllBytesAsync(cache, bytes);
                }
                var bmp = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms; bmp.EndInit(); bmp.Freeze();
                return bmp;
            } catch { return null; }
        }

        // ── Launch ────────────────────────────────────────────────────────────
        void HandleLaunch()
        {
            var ver = VerBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(ver)) {
                MessageBox.Show("Lutfen bir Minecraft surumu secin veya indirin.\n\nSurum Yoneticisi'nden bir surum indirin.",
                                "Surum Bulunamadi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Config.Version = ver; ConfigManager.Save(Config);
            
            // Son güvenlik önlemi olarak modları senkronize et
            SyncModsForCurrentVersion();

            _ = LaunchMinecraftAsync(ver);
        }

        async Task LaunchMinecraftAsync(string version)
        {
            BtnLaunch.IsEnabled = false;
            BtnLaunch.Content   = "BASLATILIYOR...";
            SetProgress(5);

            try
            {
                // 1. Java bul
                SetStatus("Java aranıyor...");
                var javaPath = await FindJavaAsync();
                if (javaPath == null)
                {
                    var res = MessageBox.Show(
                        "Java bulunamadı!\n\nModern Minecraft ve modları açabilmek için Java 21 gereklidir.\n\nJava 21 otomatik olarak indirilip kurulsun mu?",
                        "Java Yok", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        javaPath = await DownloadAndInstallJava21Async();
                        if (javaPath == null) return;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (IsModernVersion(version))
                {
                    int javaVer = GetJavaMajorVersion(javaPath);
                    bool req25 = RequiresJava25(version);
                    if (req25 && javaVer < 25)
                    {
                        var res = MessageBox.Show(
                            $"Seçtiğiniz sürüm ({version}) için en az Java 25 gereklidir. Ancak bilgisayarınızda sadece Java {javaVer} bulundu.\n\nJava 25 otomatik olarak indirilip kurulsun mu?",
                            "Uyumsuz Java Sürümü", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (res == MessageBoxResult.Yes)
                        {
                            var autoJava = await DownloadAndInstallJava25Async();
                            if (autoJava != null)
                            {
                                javaPath = autoJava;
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    else if (javaVer < 17)
                    {
                        var res = MessageBox.Show(
                            $"Seçtiğiniz sürüm ({version}) için en az Java 17/21 gereklidir. Ancak bilgisayarınızda sadece Java {javaVer} bulundu.\n\nJava 21 otomatik olarak indirilip kurulsun mu?",
                            "Uyumsuz Java Sürümü", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (res == MessageBoxResult.Yes)
                        {
                            var autoJava = await DownloadAndInstallJava21Async();
                            if (autoJava != null)
                            {
                                javaPath = autoJava;
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // 2. JAR kontrol
                var versDir = Path.Combine(App.GameDir, "versions", version);
                var jar     = Path.Combine(versDir, $"{version}.jar");
                if (!File.Exists(jar))
                {
                    var res = MessageBox.Show(
                        $"Surum dosyasi bulunamadi: {version}\n\nSurum Yoneticisi'nden indirmek ister misiniz?",
                        "Dosya Yok", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes) Navigate("Vers");
                    return;
                }

                SetProgress(40);
                SetStatus("Lutfen bekleyin...");

                // 3. Natives klasoru olustur
                var natives = Path.Combine(versDir, "natives");
                Directory.CreateDirectory(natives);

                // Oyun klasöründeki veya sunucu klasörlerindeki server.properties dosyalarını çevrimdışı moda zorla
                try
                {
                    if (Relay != null)
                    {
                        _ = Task.Run(() => Relay.EnforceOfflineModeInProperties());
                    }
                    else
                    {
                        // Relay henüz başlatılmadıysa veya null ise manuel olarak sessizce oyun dizinini düzelt
                        _ = Task.Run(() => {
                            try {
                                if (Directory.Exists(App.GameDir)) {
                                    var files = Directory.GetFiles(App.GameDir, "server.properties", SearchOption.AllDirectories);
                                    foreach (var file in files) {
                                        var lines = File.ReadAllLines(file);
                                        bool updated = false;
                                        for (int i = 0; i < lines.Length; i++) {
                                            if (lines[i].Trim().StartsWith("online-mode", StringComparison.OrdinalIgnoreCase)) {
                                                if (!lines[i].Contains("false")) { lines[i] = "online-mode=false"; updated = true; }
                                            }
                                        }
                                        if (!updated && !lines.Any(l => l.Trim().StartsWith("online-mode", StringComparison.OrdinalIgnoreCase))) {
                                            var newLines = new List<string>(lines) { "online-mode=false" };
                                            lines = newLines.ToArray();
                                            updated = true;
                                        }
                                        if (updated) { File.WriteAllLines(file, lines); }
                                    }
                                }
                            } catch { }
                        });
                    }
                }
                catch { }

                SetStatus("Karakter (Skin) yaması uygulanıyor...");
                await PrepareSkinPackAsync(version);

                SetStatus("Oyun dili senkronize ediliyor...");
                EnsureGameLanguageMatchesLauncher();

                SetStatus("Görüş mesafesi optimize ediliyor...");
                EnsureChunkDistanceOptimized();

                SetStatus("Eksik kütüphaneler indiriliyor...");
                await EnsureLibrariesInstalledAsync(version, (pct, status) => Dispatcher.Invoke(() => SetProgress((int)(40 + pct * 0.25), status)));

                string? injectorPath = null;
                string? resolvedUuid = null;
                if ((Config.AuthType ?? "").ToLower() == "elyby")
                {
                    SetStatus("Ely.by skin doğrulayıcı kontrol ediliyor...");
                    injectorPath = await EnsureAuthlibInjectorInstalledAsync();
                    try
                    {
                        var elyJson = await _http.GetStringAsync($"https://authserver.ely.by/api/users/profiles/minecraft/{Uri.EscapeDataString(Config.User)}");
                        if (!string.IsNullOrEmpty(elyJson))
                        {
                            var elyProfile = JObject.Parse(elyJson);
                            var rawId = elyProfile["id"]?.ToString();
                            if (!string.IsNullOrEmpty(rawId) && rawId.Length == 32)
                            {
                                resolvedUuid = $"{rawId[..8]}-{rawId.Substring(8, 4)}-{rawId.Substring(12, 4)}-{rawId.Substring(16, 4)}-{rawId.Substring(20)}";
                                App.Log($"Resolved Ely.by UUID for '{Config.User}': {resolvedUuid}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"Failed to resolve Ely.by UUID, falling back to offline UUID: {ex.Message}");
                    }
                }

                // 4. Argumanlari olustur
                // ★ RAM Güvenlik Sınırı (Out of Memory ve Shader Çökmelerini Önler)
                var rawRamGb = Math.Max(Config.Ram, 1);
                var ram = rawRamGb * 1024;
                try
                {
                    ulong totalPhysBytes = KernelOptimizer.GetTotalPhysicalMemory();
                    if (totalPhysBytes > 0)
                    {
                        int totalPhysGb = (int)(totalPhysBytes / (1024.0 * 1024.0 * 1024.0));
                        if (rawRamGb >= totalPhysGb)
                        {
                            // Fiziksel RAM'in hepsini vermeye çalışırsa otomatik olarak güvenli bir sınır koy (Toplam RAM - 4GB, en az 4GB, en fazla 8GB shaderlar için)
                            int safeRamGb = Math.Max(4, totalPhysGb - 4);
                            if (safeRamGb > 8) safeRamGb = 8; // Shaderlar ve sistem için en ideal dengeli dağılım
                            ram = safeRamGb * 1024;
                            App.Log($"[RAMOpt] Sistem fiziksel belleği ({totalPhysGb}GB) yetersiz! RAM kilidi uygulandı: {rawRamGb}GB -> {safeRamGb}GB");
                        }
                    }
                }
                catch { }

                var args = BuildLaunchArgs(version, ram, natives, injectorPath, resolvedUuid);

                SetProgress(70);
                SetStatus("Minecraft baslatılıyor...");
                App.Log($"Launch: {javaPath} {args[..Math.Min(args.Length,120)]}...");

                // ★ PERF FIX: stdout/stderr redirect kaldırıldı – pipe buffer overhead yok
                var psi = new ProcessStartInfo(javaPath, args) {
                    WorkingDirectory = App.GameDir,
                    UseShellExecute  = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                if (process != null)
                {
                    // Wait 1.5 seconds to detect immediate exit or crash
                    await Task.Delay(1500);
                    if (process.HasExited)
                    {
                        throw new Exception($"Oyun baslatılamadı veya beklenmedik sekilde kapandı. (Exit Code: {process.ExitCode})");
                    }

                    // ── Kernel Optimizasyonlarını Uygula ──
                    bool anyKernelOpt = Config.KernelPriority || Config.KernelTimer || Config.KernelAffinity || Config.KernelPower || Config.KernelNagle || Config.KernelGpu;
                    if (anyKernelOpt)
                    {
                        SetStatus("Kernel optimizasyonları uygulanıyor...");
                        KernelOptimizer.ApplyAll(process, Config);
                    }

                    // Oyun kapandığında optimizasyonları geri al ve başlatıcıyı geri aç
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await process.WaitForExitAsync();
                            int exitCode = process.ExitCode;
                            KernelOptimizer.RevertAll();
                            App.Log($"[Oyun İzleme] Oyun kapandı. Exit Code: {exitCode}");

                            if (exitCode != 0)
                            {
                                App.Log($"[Oyun İzleme] Oyun anormal şekilde kapandı (çöktü)! Hata analizi yapılıyor...");
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        string crashLog = "Oyun Hata Kodu ile Çöktü. Exit Code: " + exitCode;
                                        string crashStack = "Detaylı çökme raporu bulunamadı.";

                                        // 1. En son crash-reports dosyasını bulmaya çalış
                                        string crashDir = Path.Combine(App.GameDir, "crash-reports");
                                        if (Directory.Exists(crashDir))
                                        {
                                            var latestCrash = new DirectoryInfo(crashDir)
                                                .GetFiles("crash-*.txt")
                                                .OrderByDescending(f => f.LastWriteTime)
                                                .FirstOrDefault();

                                            if (latestCrash != null && (DateTime.Now - latestCrash.LastWriteTime).TotalMinutes < 3)
                                            {
                                                // Son 3 dakika içinde oluşturulmuş bir crash report var
                                                var lines = await File.ReadAllLinesAsync(latestCrash.FullName);
                                                crashLog = lines.FirstOrDefault(l => l.StartsWith("Description:")) ?? "Minecraft Çökme Raporu (" + latestCrash.Name + ")";
                                                crashStack = string.Join("\n", lines.Take(50)); // İlk 50 satırı stack trace olarak al
                                                App.Log($"[Oyun İzleme] En son crash report dosyası okundu: {latestCrash.Name}");
                                            }
                                        }

                                        // 2. Eğer crash report yoksa, logs/latest.log dosyasının son 30 satırını oku (FATAL/ERROR satırları arat)
                                        if (crashStack == "Detaylı çökme raporu bulunamadı.")
                                        {
                                            string latestLogPath = Path.Combine(App.GameDir, "logs", "latest.log");
                                            if (File.Exists(latestLogPath))
                                            {
                                                var lines = await File.ReadAllLinesAsync(latestLogPath);
                                                var last30 = lines.Skip(Math.Max(0, lines.Length - 30)).ToArray();
                                                
                                                var errorLine = last30.LastOrDefault(l => l.Contains("[ERROR]") || l.Contains("[FATAL]") || l.Contains("Exception in thread")) ?? "Bilinmeyen çökme hatası.";
                                                crashLog = "Oyun Günlüğü Hatası: " + errorLine;
                                                crashStack = "En Son Log Çıktısı (Son 30 Satır):\n" + string.Join("\n", last30);
                                                App.Log("[Oyun İzleme] logs/latest.log dosyasının son satırları okundu.");
                                            }
                                        }

                                        // Firebase'e hatayı gönder!
                                        await MistikAnalytics.TrackCrashAsync(Config.User ?? "Oyuncu", crashLog, crashStack);
                                    }
                                    catch (Exception exVal)
                                    {
                                        App.Log($"[Oyun İzleme] Çökme analizi başarısız: {exVal.Message}");
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Log($"[KernelOpt] Oyun izleme hatası: {ex.Message}");
                            KernelOptimizer.RevertAll();
                        }
                        finally
                        {
                            if (Config.AutoClose)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    this.Show();
                                    this.WindowState = WindowState.Normal;
                                    this.Activate();
                                });
                            }
                        }
                    });
                }
                SetProgress(100);
                Relay?.UpdateStatus("Oyunda", version, "Minecraft");

                // Firebase Analytics: Oyun başlatma istatistiği
                try { _ = MistikAnalytics.TrackGameLaunchAsync(Config.User ?? "Oyuncu", version, ram / 1024); } catch { }

                // Oyun başarıyla açıldığı için mod listesini de Firebase'e senkronize et
                try
                {
                    if (Directory.Exists(App.ModsDir))
                    {
                        var jarFiles = Directory.GetFiles(App.ModsDir, "*.jar")
                                                .Select(x => Path.GetFileNameWithoutExtension(x) ?? "")
                                                .Where(x => !string.IsNullOrEmpty(x))
                                                .ToList();
                        _ = MistikAnalytics.SyncInstalledModsAsync(Config.User ?? "Oyuncu", jarFiles);
                    }
                }
                catch { }

                if (Config.AutoClose)
                {
                    Dispatcher.Invoke(() => this.Hide());
                }
            }
            catch (Exception ex)
            {
                App.Log($"Launch error: {ex.Message}");
                try { _ = MistikAnalytics.TrackCrashAsync(Config.User ?? "Oyuncu", $"Oyun Başlatma Hatası: {ex.Message}", ex.StackTrace ?? ""); } catch { }
                MessageBox.Show($"Baslatma hatasi:\n{ex.Message}", "Hata",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnLaunch.IsEnabled = true;
                BtnLaunch.Content   = "OYUNA GIR";
                SetProgress(0);
                SetStatus($"Surum: {version}");
            }
        }

        public void EnsureMistikSkinPackEnabled(bool enable)
        {
            try
            {
                string optionsPath = Path.Combine(App.GameDir, "options.txt");
                if (!File.Exists(optionsPath))
                {
                    if (enable)
                    {
                        File.WriteAllText(optionsPath, "resourcePacks:[\"MistikSkinPack\",\"file/MistikSkinPack\"]\r\n");
                    }
                    return;
                }

                var lines = File.ReadAllLines(optionsPath);
                bool foundRes = false;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("resourcePacks:", StringComparison.OrdinalIgnoreCase))
                    {
                        foundRes = true;
                        string content = trimmed.Substring("resourcePacks:".Length).Trim();
                        var items = new List<string>();
                        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""([^""]+)""");
                        foreach (System.Text.RegularExpressions.Match m in matches) items.Add(m.Groups[1].Value);

                        // Once eski kayitlari temizle
                        items.Remove("file/MistikSkinPack");
                        items.Remove("MistikSkinPack");
                        
                        if (enable)
                        {
                            // En yüksek öncelik (listenin sonu / en sağı) için listenin sonuna ekle
                            items.Add("MistikSkinPack");
                            items.Add("file/MistikSkinPack");
                        }

                        lines[i] = "resourcePacks:[" + string.Join(",", items.Select(x => $"\"{x}\"")) + "]";
                    }
                    else if (trimmed.StartsWith("incompatibleResourcePacks:", StringComparison.OrdinalIgnoreCase))
                    {
                        string content = trimmed.Substring("incompatibleResourcePacks:".Length).Trim();
                        var items = new List<string>();
                        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""([^""]+)""");
                        foreach (System.Text.RegularExpressions.Match m in matches) items.Add(m.Groups[1].Value);

                        items.Remove("file/MistikSkinPack");
                        items.Remove("MistikSkinPack");

                        lines[i] = "incompatibleResourcePacks:[" + string.Join(",", items.Select(x => $"\"{x}\"")) + "]";
                    }
                }

                if (!foundRes && enable)
                {
                    var newLines = lines.ToList();
                    newLines.Add("resourcePacks:[\"MistikSkinPack\",\"file/MistikSkinPack\"]");
                    lines = newLines.ToArray();
                }

                File.WriteAllLines(optionsPath, lines);
            }
            catch (Exception ex)
            {
                App.Log($"Error updating options.txt resourcePacks: {ex.Message}");
            }
        }

        public void EnsureGameLanguageMatchesLauncher()
        {
            try
            {
                string optionsPath = Path.Combine(App.GameDir, "options.txt");
                string targetLangCode = (Config.Lang ?? "").Contains("English") ? "en_us" : "tr_tr";

                if (!File.Exists(optionsPath))
                {
                    File.WriteAllText(optionsPath, $"lang:{targetLangCode}\r\n");
                    App.Log($"options.txt created with default language: {targetLangCode}");
                    return;
                }

                var lines = File.ReadAllLines(optionsPath).ToList();
                bool langFound = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("lang:", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"lang:{targetLangCode}";
                        langFound = true;
                        break;
                    }
                }

                if (!langFound)
                {
                    lines.Add($"lang:{targetLangCode}");
                }

                File.WriteAllLines(optionsPath, lines);
                App.Log($"Game language synchronized to option: {targetLangCode}");
            }
            catch (Exception ex)
            {
                App.Log($"EnsureGameLanguageMatchesLauncher error: {ex.Message}");
            }
        }

        public void EnsureChunkDistanceOptimized()
        {
            try
            {
                string optionsPath = Path.Combine(App.GameDir, "options.txt");
                if (!File.Exists(optionsPath)) return;

                var lines = File.ReadAllLines(optionsPath).ToList();
                bool modified = false;

                bool hasSyncWrites = false;
                bool hasMaxFps = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("renderDistance:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int dist))
                        {
                            if (dist > 12)
                            {
                                lines[i] = "renderDistance:12";
                                modified = true;
                                App.Log($"[ChunkOpt] Render distance capped from {dist} to 12 for smoother startup.");
                            }
                        }
                    }
                    else if (trimmed.StartsWith("simulationDistance:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int dist))
                        {
                            if (dist > 8)
                            {
                                lines[i] = "simulationDistance:8";
                                modified = true;
                                App.Log($"[ChunkOpt] Simulation distance capped from {dist} to 8 for smoother startup.");
                            }
                        }
                    }
                    else if (trimmed.StartsWith("syncChunkWrites:", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSyncWrites = true;
                        if (!trimmed.EndsWith("false"))
                        {
                            lines[i] = "syncChunkWrites:false";
                            modified = true;
                            App.Log("[ChunkOpt] syncChunkWrites disabled to prevent disk write stutters.");
                        }
                    }
                    else if (trimmed.StartsWith("maxFps:", StringComparison.OrdinalIgnoreCase))
                    {
                        hasMaxFps = true;
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int fps))
                        {
                            if (fps > 144)
                            {
                                lines[i] = "maxFps:144";
                                modified = true;
                                App.Log($"[ChunkOpt] FPS capped to 144 (was {fps}) to prevent GPU overheating.");
                            }
                        }
                    }
                }

                if (!hasSyncWrites)
                {
                    lines.Add("syncChunkWrites:false");
                    modified = true;
                    App.Log("[ChunkOpt] syncChunkWrites:false added to options.txt.");
                }
                if (!hasMaxFps)
                {
                    lines.Add("maxFps:144");
                    modified = true;
                    App.Log("[ChunkOpt] maxFps:144 added to options.txt.");
                }

                if (modified)
                {
                    File.WriteAllLines(optionsPath, lines);
                }
            }
            catch (Exception ex)
            {
                App.Log($"EnsureChunkDistanceOptimized error: {ex.Message}");
            }
        }

        public async Task<string?> EnsureAuthlibInjectorInstalledAsync()
        {
            try
            {
                var injectorPath = Path.Combine(App.AppData, "authlib-injector.jar");
                if (File.Exists(injectorPath) && new FileInfo(injectorPath).Length > 10000)
                {
                    return injectorPath;
                }

                App.Log("authlib-injector.jar not found, downloading from official release...");
                var url = "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.5/authlib-injector-1.2.5.jar";
                var bytes = await _http.GetByteArrayAsync(url);
                Directory.CreateDirectory(App.AppData);
                await File.WriteAllBytesAsync(injectorPath, bytes);
                App.Log("authlib-injector.jar downloaded successfully!");
                return injectorPath;
            }
            catch (Exception ex)
            {
                App.Log($"Failed to download authlib-injector: {ex.Message}");
                try
                {
                    var fallbackUrl = "https://authlib-injector.yushijinhun.ms/artifact/latest/authlib-injector.jar";
                    var bytes = await _http.GetByteArrayAsync(fallbackUrl);
                    var injectorPath = Path.Combine(App.AppData, "authlib-injector.jar");
                    Directory.CreateDirectory(App.AppData);
                    await File.WriteAllBytesAsync(injectorPath, bytes);
                    App.Log("authlib-injector.jar fallback download success!");
                    return injectorPath;
                }
                catch (Exception ex2)
                {
                    App.Log($"Fallback download failed: {ex2.Message}");
                    return null;
                }
            }
        }

        public void SyncModsForCurrentVersion()
        {
            try
            {
                var currentVer = Config.Version ?? "";
                if (string.IsNullOrEmpty(currentVer)) return;

                // 1. Determine loader type for current version
                string currentLoader = "vanilla";
                if (currentVer.Contains("fabric", StringComparison.OrdinalIgnoreCase)) currentLoader = "fabric";
                else if (currentVer.Contains("forge", StringComparison.OrdinalIgnoreCase)) currentLoader = "forge";
                else if (currentVer.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) currentLoader = "neoforge";

                // Extract exact MC version (e.g. 1.21.1) from folder name
                var mcVersion = "1.21.1";
                var mcMatch = System.Text.RegularExpressions.Regex.Match(currentVer, @"1\.\d+(\.\d+)?");
                if (mcMatch.Success) mcVersion = mcMatch.Value;

                // Dynamic pool key: e.g. "1.21.1_fabric" or "1.20.1_forge"
                string currentPoolKey = currentLoader == "vanilla" ? "vanilla" : $"{mcVersion}_{currentLoader}";

                var lastSynced = Config.LastSyncedVersion ?? "";

                // If nothing has changed, do not do anything
                if (lastSynced == currentVer) return;

                var modsPoolDir = Path.Combine(App.AppData, "mods_pool");
                Directory.CreateDirectory(modsPoolDir);

                // 2. If we had a previously synced version, move current mods from App.ModsDir back to its pool
                if (!string.IsNullOrEmpty(lastSynced) && Directory.Exists(App.ModsDir))
                {
                    string lastLoader = "vanilla";
                    if (lastSynced.Contains("fabric", StringComparison.OrdinalIgnoreCase)) lastLoader = "fabric";
                    else if (lastSynced.Contains("forge", StringComparison.OrdinalIgnoreCase)) lastLoader = "forge";
                    else if (lastSynced.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) lastLoader = "neoforge";

                    var lastMcVersion = "1.21.1";
                    var lastMcMatch = System.Text.RegularExpressions.Regex.Match(lastSynced, @"1\.\d+(\.\d+)?");
                    if (lastMcMatch.Success) lastMcVersion = lastMcMatch.Value;

                    if (lastLoader != "vanilla")
                    {
                        var lastPoolKey = $"{lastMcVersion}_{lastLoader}";
                        var lastPoolDir = Path.Combine(modsPoolDir, lastPoolKey);
                        Directory.CreateDirectory(lastPoolDir);

                        var currentJars = Directory.GetFiles(App.ModsDir, "*.jar");
                        foreach (var jar in currentJars)
                        {
                            var dest = Path.Combine(lastPoolDir, Path.GetFileName(jar));
                            try
                            {
                                if (File.Exists(dest)) File.Delete(dest);
                                File.Move(jar, dest);
                                App.Log($"Moved active mod to pool: {Path.GetFileName(jar)} -> mods_pool/{lastPoolKey}");
                            }
                            catch (Exception ex)
                            {
                                App.Log($"Failed to move mod {Path.GetFileName(jar)} to pool: {ex.Message}");
                            }
                        }
                    }
                }

                // 3. Clear any leftover active jars in App.ModsDir to be perfectly clean
                if (Directory.Exists(App.ModsDir))
                {
                    foreach (var jar in Directory.GetFiles(App.ModsDir, "*.jar"))
                    {
                        try { File.Delete(jar); } catch { }
                    }
                }
                else
                {
                    Directory.CreateDirectory(App.ModsDir);
                }

                // 4. Move/Copy mods from mods_pool/{currentPoolKey} into App.ModsDir (only if NOT vanilla)
                if (currentLoader != "vanilla")
                {
                    var newPoolDir = Path.Combine(modsPoolDir, currentPoolKey);
                    if (Directory.Exists(newPoolDir))
                    {
                        var poolJars = Directory.GetFiles(newPoolDir, "*.jar");
                        foreach (var jar in poolJars)
                        {
                            var dest = Path.Combine(App.ModsDir, Path.GetFileName(jar));
                            try
                            {
                                if (File.Exists(dest)) File.Delete(dest);
                                File.Move(jar, dest);
                                App.Log($"Moved pool mod to active: {Path.GetFileName(jar)} from mods_pool/{currentPoolKey}");
                            }
                            catch (Exception ex)
                            {
                                App.Log($"Failed to activate mod {Path.GetFileName(jar)}: {ex.Message}");
                            }
                        }
                    }
                }

                // Update config
                Config.LastSyncedVersion = currentVer;
                ConfigManager.Save(Config);

                // Uyumsuz modları otomatik askıya al
                SuspendIncompatibleMods(mcVersion, currentLoader);

                // Modları Firebase veritabanına senkronize et
                try
                {
                    if (Directory.Exists(App.ModsDir))
                    {
                        var jarFiles = Directory.GetFiles(App.ModsDir, "*.jar")
                                                .Select(x => Path.GetFileNameWithoutExtension(x) ?? "")
                                                .Where(x => !string.IsNullOrEmpty(x))
                                                .ToList();
                        _ = MistikAnalytics.SyncInstalledModsAsync(Config.User ?? "Oyuncu", jarFiles);
                    }
                }
                catch { }

                App.Log($"Mods synchronized successfully for version: {currentVer} ({currentLoader})");
            }
            catch (Exception ex)
            {
                App.Log($"SyncModsForCurrentVersion error: {ex.Message}");
            }
        }

        // ── Otomatik Uyuşmayan Mod Askılama Sistemi ──────────────────────────
        /// <summary>
        /// Mods klasöründeki tüm .jar dosyalarını tarayarak mevcut sürüm ve loader ile
        /// uyumsuz olanları otomatik olarak askıya alır (mods_pool'a taşır).
        /// </summary>
        void SuspendIncompatibleMods(string mcVersion, string currentLoader)
        {
            try
            {
                if (!Directory.Exists(App.ModsDir)) return;
                if (currentLoader == "vanilla") return; // Vanilla'da mod kontrolü yapma

                var jars = Directory.GetFiles(App.ModsDir, "*.jar");
                if (jars.Length == 0) return;

                var modsPoolDir = Path.Combine(App.AppData, "mods_pool");
                int suspended = 0;

                foreach (var jar in jars)
                {
                    try
                    {
                        var (modLoader, modMcVersions) = InspectModJar(jar);

                        // Loader uyumsuzluğu kontrolü
                        bool loaderMismatch = false;
                        if (!string.IsNullOrEmpty(modLoader))
                        {
                            if (currentLoader == "fabric" && modLoader == "forge") loaderMismatch = true;
                            if (currentLoader == "forge" && modLoader == "fabric") loaderMismatch = true;
                            if (currentLoader == "fabric" && modLoader == "neoforge") loaderMismatch = true;
                            if (currentLoader == "neoforge" && modLoader == "fabric") loaderMismatch = true;
                        }

                        // Sürüm uyumsuzluğu kontrolü
                        bool versionMismatch = false;
                        if (modMcVersions != null && modMcVersions.Count > 0)
                        {
                            versionMismatch = !IsVersionCompatible(mcVersion, modMcVersions);
                        }

                        if (loaderMismatch || versionMismatch)
                        {
                            // Uyumsuz modu ilgili havuza taşı
                            string reason = loaderMismatch ? $"loader ({modLoader} != {currentLoader})" : $"sürüm ({string.Join(",", modMcVersions ?? new List<string>())} !~ {mcVersion})";
                            string poolKey = !string.IsNullOrEmpty(modLoader) && modMcVersions?.Count > 0
                                ? $"{modMcVersions[0]}_{modLoader}"
                                : "incompatible";
                            var poolDir = Path.Combine(modsPoolDir, poolKey);
                            Directory.CreateDirectory(poolDir);

                            var dest = Path.Combine(poolDir, Path.GetFileName(jar));
                            if (File.Exists(dest)) File.Delete(dest);
                            File.Move(jar, dest);
                            suspended++;
                            App.Log($"[ModGuard] Uyumsuz mod askıya alındı: {Path.GetFileName(jar)} ({reason}) -> mods_pool/{poolKey}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[ModGuard] Mod dosyası analiz edilemedi: {Path.GetFileName(jar)}: {ex.Message}");
                    }
                }

                if (suspended > 0)
                {
                    App.Log($"[ModGuard] Toplam {suspended} uyumsuz mod askıya alındı.");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[ModGuard] Uyumsuz mod askılama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Bir .jar dosyasının içindeki fabric.mod.json veya mods.toml dosyasını okuyarak
        /// modun loader tipini ve desteklediği Minecraft sürümlerini döndürür.
        /// </summary>
        (string? loader, List<string>? mcVersions) InspectModJar(string jarPath)
        {
            string? loader = null;
            List<string>? mcVersions = null;

            try
            {
                using var zip = ZipFile.OpenRead(jarPath);

                // 1. Fabric: fabric.mod.json
                var fabricEntry = zip.GetEntry("fabric.mod.json");
                if (fabricEntry != null)
                {
                    loader = "fabric";
                    using var reader = new StreamReader(fabricEntry.Open());
                    var json = reader.ReadToEnd();
                    try
                    {
                        var obj = JObject.Parse(json);
                        var depends = obj["depends"] as JObject;
                        if (depends != null)
                        {
                            var mcDep = depends["minecraft"]?.ToString();
                            if (!string.IsNullOrEmpty(mcDep))
                            {
                                mcVersions = ExtractVersionsFromConstraint(mcDep);
                            }
                        }
                    }
                    catch { }
                    return (loader, mcVersions);
                }

                // 2. Forge / NeoForge: META-INF/mods.toml veya META-INF/neoforge.mods.toml
                var neoforgeEntry = zip.GetEntry("META-INF/neoforge.mods.toml");
                var forgeEntry = zip.GetEntry("META-INF/mods.toml");

                if (neoforgeEntry != null)
                {
                    loader = "neoforge";
                    using var reader = new StreamReader(neoforgeEntry.Open());
                    var toml = reader.ReadToEnd();
                    mcVersions = ExtractVersionsFromToml(toml);
                    return (loader, mcVersions);
                }

                if (forgeEntry != null)
                {
                    loader = "forge";
                    using var reader = new StreamReader(forgeEntry.Open());
                    var toml = reader.ReadToEnd();
                    mcVersions = ExtractVersionsFromToml(toml);
                    return (loader, mcVersions);
                }
            }
            catch { }

            return (loader, mcVersions);
        }

        /// <summary>
        /// Fabric'in sürüm kısıtlama stringinden (örn: ">=1.20 <=1.21.1" veya "1.21.x") 
        /// desteklenen sürümleri çıkarır.
        /// </summary>
        List<string> ExtractVersionsFromConstraint(string constraint)
        {
            var versions = new List<string>();
            var matches = Regex.Matches(constraint, @"1\.\d+(\.\d+)?");
            foreach (Match m in matches)
            {
                if (!versions.Contains(m.Value)) versions.Add(m.Value);
            }
            return versions;
        }

        /// <summary>
        /// Forge/NeoForge mods.toml dosyasından Minecraft sürüm bilgisini çıkarır.
        /// </summary>
        List<string> ExtractVersionsFromToml(string toml)
        {
            var versions = new List<string>();
            // modId = "minecraft" satırından sonraki versionRange'i bul
            var mcSectionMatch = Regex.Match(toml, @"modId\s*=\s*""minecraft"".*?versionRange\s*=\s*""([^""]+)""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (mcSectionMatch.Success)
            {
                var range = mcSectionMatch.Groups[1].Value;
                var matches = Regex.Matches(range, @"1\.\d+(\.\d+)?");
                foreach (Match m in matches)
                {
                    if (!versions.Contains(m.Value)) versions.Add(m.Value);
                }
            }
            return versions;
        }

        /// <summary>
        /// Aktif MC sürümünün, modun desteklediği sürüm listesiyle uyumlu olup olmadığını kontrol eder.
        /// Örn: aktif "1.21.1", mod ["1.20", "1.21.1"] -> true
        /// Örn: aktif "1.21.1", mod ["1.20", "1.20.4"] -> false
        /// Sürüm aralığı varsa (min-max), aralık kontrolü yapılır.
        /// </summary>
        bool IsVersionCompatible(string activeVersion, List<string> modVersions)
        {
            if (modVersions == null || modVersions.Count == 0) return true;

            // Tam eşleşme kontrolü
            foreach (var v in modVersions)
            {
                if (v == activeVersion) return true;
                // Minor sürüm eşleşmesi: mod "1.21" ise, "1.21.1" de uyumludur
                if (activeVersion.StartsWith(v + ".") || activeVersion == v) return true;
            }

            // Aralık kontrolü: en az 2 sürüm varsa [min, max] aralığı olarak değerlendir
            if (modVersions.Count >= 2)
            {
                var activeNums = GetVersionNumbers(activeVersion);
                var minNums = GetVersionNumbers(modVersions[0]);
                var maxNums = GetVersionNumbers(modVersions[modVersions.Count - 1]);

                if (CompareVersionNums(activeNums, minNums) >= 0 && CompareVersionNums(activeNums, maxNums) <= 0)
                    return true;
            }

            return false;
        }

        static int CompareVersionNums(List<int> a, List<int> b)
        {
            for (int i = 0; i < Math.Max(a.Count, b.Count); i++)
            {
                int numA = i < a.Count ? a[i] : 0;
                int numB = i < b.Count ? b[i] : 0;
                if (numA != numB) return numA.CompareTo(numB);
            }
            return 0;
        }

        public static int GetPackFormatForVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return 1;
            
            var clean = version;
            var mcMatch = System.Text.RegularExpressions.Regex.Match(version, @"1\.\d+(\.\d+)?");
            if (mcMatch.Success)
            {
                clean = mcMatch.Value;
            }

            var parts = new List<int>();
            var matches = System.Text.RegularExpressions.Regex.Matches(clean, @"\d+");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (int.TryParse(m.Value, out var n)) parts.Add(n);
            }

            if (parts.Count < 2) return 1;
            
            int major = parts[0];
            int minor = parts[1];
            int patch = parts.Count >= 3 ? parts[2] : 0;

            if (major > 1) return 46;

            if (major == 1)
            {
                if (minor >= 22) return 48;
                if (minor == 21)
                {
                    if (patch >= 2) return 42;
                    return 34;
                }
                if (minor == 20)
                {
                    if (patch >= 5) return 32;
                    if (patch >= 2) return 18;
                    return 15;
                }
                if (minor == 19)
                {
                    if (patch >= 4) return 13;
                    if (patch >= 3) return 12;
                    return 10;
                }
                if (minor == 18)
                {
                    if (patch >= 2) return 9;
                    return 8;
                }
                if (minor == 17) return 7;
                if (minor == 16)
                {
                    if (patch >= 2) return 6;
                    return 5;
                }
                if (minor == 15) return 5;
                if (minor == 14 || minor == 13) return 4;
                if (minor == 12 || minor == 11) return 3;
                if (minor == 10 || minor == 9) return 2;
            }

            return 1;
        }

        public async Task PrepareSkinPackAsync(string version)
        {
            try
            {
                var packDir = Path.Combine(App.GameDir, "resourcepacks", "MistikSkinPack");
                var textureDirOld = Path.Combine(packDir, "assets", "minecraft", "textures", "entity");
                var textureDirNewWide = Path.Combine(packDir, "assets", "minecraft", "textures", "entity", "player", "wide");
                var textureDirNewSlim = Path.Combine(packDir, "assets", "minecraft", "textures", "entity", "player", "slim");
                int format = GetPackFormatForVersion(version);

                if (Config.SkinType == "username")
                {
                    var user = !string.IsNullOrEmpty(Config.SkinUser) ? Config.SkinUser : Config.User;
                    if (string.IsNullOrEmpty(user) || user == "Oyuncu")
                    {
                        EnsureMistikSkinPackEnabled(false);
                        return;
                    }

                    // Clear cached avatar files so the bottom circular preview updates immediately!
                    try
                    {
                        var cache1 = Path.Combine(App.AppData, $"elyby_{user}.png");
                        var cache2 = Path.Combine(App.AppData, $"avatar_{user}_40.png");
                        var cache3 = Path.Combine(App.AppData, $"avatar_{user}_64.png");
                        if (File.Exists(cache1)) File.Delete(cache1);
                        if (File.Exists(cache2)) File.Delete(cache2);
                        if (File.Exists(cache3)) File.Delete(cache3);
                    }
                    catch { }

                    byte[]? skinBytes = null;
                    try {
                        var jsonStr = await _http.GetStringAsync($"http://skinsystem.ely.by/textures/{Uri.EscapeDataString(user)}");
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                        var texUrl = jObj["SKIN"]?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(texUrl)) {
                            skinBytes = await _http.GetByteArrayAsync(texUrl);
                        }
                    } catch { }

                    if (skinBytes == null) {
                        try {
                            using var response = await _http.GetAsync($"https://mc-heads.net/skin/{Uri.EscapeDataString(user)}");
                            if (response.IsSuccessStatusCode) {
                                skinBytes = await response.Content.ReadAsByteArrayAsync();
                            }
                        } catch { }
                    }

                    if (skinBytes != null)
                    {
                        if (Directory.Exists(packDir))
                        {
                            try { Directory.Delete(packDir, true); } catch { }
                        }
                        Directory.CreateDirectory(textureDirOld);
                        Directory.CreateDirectory(textureDirNewWide);
                        Directory.CreateDirectory(textureDirNewSlim);
                        
                        await File.WriteAllBytesAsync(Path.Combine(textureDirOld, "steve.png"), skinBytes);
                        await File.WriteAllBytesAsync(Path.Combine(textureDirOld, "alex.png"), skinBytes);
                        await File.WriteAllBytesAsync(Path.Combine(textureDirNewWide, "steve.png"), skinBytes);
                        await File.WriteAllBytesAsync(Path.Combine(textureDirNewSlim, "alex.png"), skinBytes);

                        var mcmetaPath = Path.Combine(packDir, "pack.mcmeta");
                        var mcmetaContent = "{\n  \"pack\": {\n    \"pack_format\": " + format + ",\n    \"description\": \"Mistik Launcher Ozel Skin Kaynak Paketi\"\n  }\n}";
                        await File.WriteAllTextAsync(mcmetaPath, mcmetaContent);

                        EnsureMistikSkinPackEnabled(true);
                        App.Log($"Skin for '{user}' successfully downloaded and applied with pack_format {format}.");
                    }
                    else
                    {
                        App.Log($"Failed to download skin for '{user}'. Disabling custom skin pack.");
                        EnsureMistikSkinPackEnabled(false);
                        if (Directory.Exists(packDir))
                        {
                            try { Directory.Delete(packDir, true); } catch { }
                        }
                    }
                }
                else if (Config.SkinType == "local")
                {
                    var filePath = Config.SkinUser;
                    if (File.Exists(filePath))
                    {
                        if (Directory.Exists(packDir))
                        {
                            try { Directory.Delete(packDir, true); } catch { }
                        }
                        Directory.CreateDirectory(textureDirOld);
                        Directory.CreateDirectory(textureDirNewWide);
                        Directory.CreateDirectory(textureDirNewSlim);
                        
                        File.Copy(filePath, Path.Combine(textureDirOld, "steve.png"), true);
                        File.Copy(filePath, Path.Combine(textureDirOld, "alex.png"), true);
                        File.Copy(filePath, Path.Combine(textureDirNewWide, "steve.png"), true);
                        File.Copy(filePath, Path.Combine(textureDirNewSlim, "alex.png"), true);

                        var mcmetaPath = Path.Combine(packDir, "pack.mcmeta");
                        var mcmetaContent = "{\n  \"pack\": {\n    \"pack_format\": " + format + ",\n    \"description\": \"Mistik Launcher Ozel Skin Kaynak Paketi\"\n  }\n}";
                        File.WriteAllText(mcmetaPath, mcmetaContent);

                        EnsureMistikSkinPackEnabled(true);
                        App.Log($"Local skin applied successfully from: {filePath} with pack_format {format}.");
                    }
                    else
                    {
                        App.Log($"Local skin file does not exist: {filePath}");
                    }
                }
                else
                {
                    EnsureMistikSkinPackEnabled(false);
                    if (Directory.Exists(packDir))
                    {
                        try { Directory.Delete(packDir, true); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"PrepareSkinPackAsync error: {ex.Message}");
            }
        }

        string BuildLaunchArgs(string version, int ramMb, string natives, string? injectorPath = null, string? resolvedUuid = null)
        {
            var libs    = BuildClasspath(version);
            // ★ PERF FIX: Xms = Xmx → G1GC heap resize yok, daha az GC pause
            var jvm     = $"-Xmx{ramMb}m -Xms{ramMb}m -Dminecraft.server.onlineMode=false -Dminecraft.server.online-mode=false";
            if (!string.IsNullOrEmpty(injectorPath) && File.Exists(injectorPath))
            {
                jvm += $" -javaagent:\"{injectorPath}\"=https://authserver.ely.by/api/authlib-injector";
            }
            
            // Ultimate JVM & GC Optimizations for very old and modern computers!
            var optList = new List<string>();

            if (Config.OptTurbo)
            {
                // If RAM is low (2GB or less), SerialGC is much more efficient than G1GC
                if (ramMb <= 2048)
                {
                    optList.Add("-XX:+UseSerialGC");
                }
                else
                {
                    // Advanced low-latency, stutter-free Aikar's G1GC options
                    optList.Add("-XX:+UseG1GC");
                    optList.Add("-XX:+ParallelRefProcEnabled");
                    optList.Add("-XX:MaxGCPauseMillis=200");
                    optList.Add("-XX:+UnlockExperimentalVMOptions");
                    optList.Add("-XX:+DisableExplicitGC");
                    // AlwaysPreTouch kaldırıldı: 10GB+ heap'te başlangıçta CPU'yu %100'e çıkarıyordu
                    optList.Add("-XX:G1NewSizePercent=30");
                    optList.Add("-XX:G1MaxNewSizePercent=40");
                    optList.Add("-XX:G1ReservePercent=20");
                    optList.Add("-XX:G1HeapWastePercent=5");
                    optList.Add("-XX:G1MixedGCCountTarget=4");
                    optList.Add("-XX:InitiatingHeapOccupancyPercent=15");
                    optList.Add("-XX:G1MixedGCLiveThresholdPercent=90");
                    optList.Add("-XX:G1RSetUpdatingPauseTimePercent=5");
                    optList.Add("-XX:SurvivorRatio=32");
                    optList.Add("-XX:+PerfDisableSharedMem");
                    optList.Add("-XX:MaxTenuringThreshold=1");

                    // Dynamic G1HeapRegionSize based on allocated RAM
                    if (ramMb >= 12288) // 12GB+
                        optList.Add("-XX:G1HeapRegionSize=32m");
                    else if (ramMb >= 8192) // 8GB-12GB
                        optList.Add("-XX:G1HeapRegionSize=16m");
                    else if (ramMb >= 4096) // 4GB-8GB
                        optList.Add("-XX:G1HeapRegionSize=8m");
                    else // 2GB-4GB
                        optList.Add("-XX:G1HeapRegionSize=4m");
                }

                // Memory saving and CPU efficiency optimizations
                optList.Add("-XX:+UseStringDeduplication");
                optList.Add("-XX:+UseCompressedOops");
                optList.Add("-XX:+UseCompressedClassPointers");
                optList.Add("-XX:+OptimizeStringConcat");
            }
            else
            {
                optList.Add("-XX:+UseG1GC");
            }

            if (Config.OptFps)
            {
                // ── Agresif JIT Derleyici Optimizasyonları ──
                optList.Add("-XX:+UnlockDiagnosticVMOptions");
                optList.Add("-XX:-DontCompileHugeMethods");        // Büyük metodları da derle (Minecraft çok büyük metodlar içerir)
                optList.Add("-XX:+TieredCompilation");              // Kademeli derleme aktif (hızlı başlangıç + max performans)

                // ── C2 Compiler (Max Performans Katmanı) Ayarları ──
                optList.Add("-XX:MaxInlineLevel=15");               // Derin inline zinciri (varsayılan 9) → daha az method call overhead
                optList.Add("-XX:MaxInlineSize=100");               // Daha büyük metodları da inline yap (varsayılan 35 byte)
                optList.Add("-XX:FreqInlineSize=325");              // Sık çağrılan büyük metodları inline yap (varsayılan 325)


                // ── GC Logging ve Overhead Kapatma ──
                optList.Add("-XX:-OmitStackTraceInFastThrow");      // Exception bilgisini koru (hata ayıklama için)
                optList.Add("-XX:+AlwaysActAsServerClassMachine");  // JVM'i sunucu modunda çalıştır (daha agresif C2 JIT)
                optList.Add("-XX:+UseNUMA");                        // NUMA farkındalığı (multi-socket/hybrid CPU'larda faydalı)

                // ── Büyük Sayfa (Large Pages) Desteği ──
                if (KernelOptimizer.IsLargePageAvailable())
                {
                    optList.Add("-XX:+UseLargePages");
                    App.Log("[KernelOpt] JVM Large Pages aktif edildi.");
                }

                // ── LWJGL / OpenGL Performans Flagleri ──
                optList.Add("-Dorg.lwjgl.opengl.Display.allowSoftwareOpenGL=false"); // Yazılım OpenGL'i engelle, her zaman GPU kullan
            }

            var optArgs = string.Join(" ", optList);

            string mainClass = "net.minecraft.client.main.Main";
            string assetIndex = "legacy";
            try
            {
                var jsonPath = Path.Combine(App.GameDir, "versions", version, $"{version}.json");
                if (File.Exists(jsonPath))
                {
                    var json = JObject.Parse(File.ReadAllText(jsonPath));
                    
                    var mcObj = json["mainClass"]?.ToString();
                    if (!string.IsNullOrEmpty(mcObj)) mainClass = mcObj;

                    var idObj = json["assetIndex"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(idObj))
                    {
                        assetIndex = idObj;
                    }
                    else
                    {
                        var parent = json["inheritsFrom"]?.ToString();
                        if (!string.IsNullOrEmpty(parent))
                        {
                            var parentJsonPath = Path.Combine(App.GameDir, "versions", parent, $"{parent}.json");
                            if (File.Exists(parentJsonPath))
                            {
                                var parentJson = JObject.Parse(File.ReadAllText(parentJsonPath));
                                var pIdObj = parentJson["assetIndex"]?["id"]?.ToString();
                                if (!string.IsNullOrEmpty(pIdObj)) assetIndex = pIdObj;
                            }
                        }
                    }
                }
            }
            catch { }

            string launchUuid = !string.IsNullOrEmpty(resolvedUuid) ? resolvedUuid : GetOfflineUUID(Config.User);
            return $"{jvm} {optArgs} " +
                   $"-Djava.library.path=\"{natives}\" " +
                   $"-cp \"{libs}\" {mainClass} " +
                   $"--username \"{Config.User}\" " +
                   $"--version \"{version}\" " +
                   $"--gameDir \"{App.GameDir}\" " +
                   $"--assetsDir \"{Path.Combine(App.GameDir, "assets")}\" " +
                   $"--assetIndex {assetIndex} " +
                   $"--accessToken 0 --uuid {launchUuid}";
        }

        private string GetOfflineUUID(string username)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                    hash[6] = (byte)((hash[6] & 0x0f) | 0x30); // version 3
                    hash[8] = (byte)((hash[8] & 0x3f) | 0x80); // variant IETF
                    
                    // Format exactly as Java's UUID.toString() (big-endian 8-4-4-4-12 hex string)
                    return string.Format("{0:x2}{1:x2}{2:x2}{3:x2}-{4:x2}{5:x2}-{6:x2}{7:x2}-{8:x2}{9:x2}-{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
                        hash[0], hash[1], hash[2], hash[3],
                        hash[4], hash[5],
                        hash[6], hash[7],
                        hash[8], hash[9],
                        hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
                }
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        string BuildClasspath(string version)
        {
            var libs = new List<string>();
            AddLibrariesFromVersionJson(version, libs);
            return string.Join(";", libs);
        }

        void AddLibrariesFromVersionJson(string version, List<string> libs)
        {
            try
            {
                var vDir = Path.Combine(App.GameDir, "versions", version);
                var jar = Path.Combine(vDir, $"{version}.jar");
                if (File.Exists(jar) && !libs.Contains(jar)) libs.Add(jar);

                var jsonPath = Path.Combine(vDir, $"{version}.json");
                if (!File.Exists(jsonPath)) return;

                var json = JObject.Parse(File.ReadAllText(jsonPath));
                
                // Inherit libraries from parent if inheritsFrom is specified
                var parent = json["inheritsFrom"]?.ToString();
                if (!string.IsNullOrEmpty(parent))
                {
                    AddLibrariesFromVersionJson(parent, libs);
                }

                var libsArray = json["libraries"] as JArray;
                if (libsArray != null)
                {
                    foreach (var lib in libsArray)
                    {
                        string? name = lib["name"]?.ToString();
                        if (string.IsNullOrEmpty(name)) continue;

                        string? relPath = null;
                        var artifact = lib["downloads"]?["artifact"];
                        if (artifact != null)
                        {
                            relPath = artifact["path"]?.ToString();
                        }

                        if (string.IsNullOrEmpty(relPath))
                        {
                            var parts = name.Split(':');
                            if (parts.Length >= 3)
                            {
                                var group = parts[0].Replace('.', '/');
                                var art = parts[1];
                                var ver = parts[2];
                                var classifier = parts.Length >= 4 ? $"-{parts[3]}" : "";
                                relPath = $"{group}/{art}/{ver}/{art}-{ver}{classifier}.jar";
                            }
                        }

                        if (string.IsNullOrEmpty(relPath)) continue;

                        // 1. Check local libraries folder
                        var localLib = Path.Combine(App.GameDir, "libraries", relPath);
                        if (File.Exists(localLib))
                        {
                            if (!libs.Contains(localLib)) libs.Add(localLib);
                            continue;
                        }

                        // 2. Check official .minecraft libraries folder
                        var officialLib = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            ".minecraft", "libraries", relPath);
                        if (File.Exists(officialLib))
                        {
                            if (!libs.Contains(officialLib)) libs.Add(officialLib);
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"AddLibrariesFromVersionJson error for {version}: {ex.Message}");
            }
        }

        public async Task EnsureLibrariesInstalledAsync(string version, Action<double, string>? progress = null)
        {
            try
            {
                var jsonPath = Path.Combine(App.GameDir, "versions", version, $"{version}.json");
                if (!File.Exists(jsonPath)) return;

                var json = JObject.Parse(await File.ReadAllTextAsync(jsonPath));
                
                var parentVer = json["inheritsFrom"]?.ToString();
                if (!string.IsNullOrEmpty(parentVer))
                {
                    await EnsureLibrariesInstalledAsync(parentVer, progress);
                }

                var libsArray = json["libraries"] as JArray;
                if (libsArray == null) return;

                var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/1.0 (contact@mistik.com)");

                int count = libsArray.Count;
                int current = 0;

                foreach (var lib in libsArray)
                {
                    current++;
                    string? name = lib["name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    string? downloadUrl = null;
                    string? relPath = null;

                    var artifact = lib["downloads"]?["artifact"];
                    if (artifact != null)
                    {
                        downloadUrl = artifact["url"]?.ToString();
                        relPath = artifact["path"]?.ToString();
                    }

                    if (string.IsNullOrEmpty(relPath) || string.IsNullOrEmpty(downloadUrl))
                    {
                        var parts = name.Split(':');
                        if (parts.Length >= 3)
                        {
                            var group = parts[0].Replace('.', '/');
                            var art = parts[1];
                            var ver = parts[2];
                            var classifier = parts.Length >= 4 ? $"-{parts[3]}" : "";
                            
                            relPath = $"{group}/{art}/{ver}/{art}-{ver}{classifier}.jar";
                            
                            var baseUrl = lib["url"]?.ToString() ?? "https://libraries.minecraft.net/";
                            if (!baseUrl.EndsWith("/")) baseUrl += "/";
                            
                            downloadUrl = baseUrl + relPath;
                        }
                    }

                    if (string.IsNullOrEmpty(relPath) || string.IsNullOrEmpty(downloadUrl)) continue;

                    var localFile = Path.Combine(App.GameDir, "libraries", relPath);
                    var officialFile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        ".minecraft", "libraries", relPath);

                    if (File.Exists(localFile) || File.Exists(officialFile))
                    {
                        continue;
                    }

                    if (progress != null)
                    {
                        double pct = ((double)current / count) * 100.0;
                        progress(pct, $"Kütüphane indiriliyor ({current}/{count}): {Path.GetFileName(relPath)}");
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
                        var data = await client.GetByteArrayAsync(downloadUrl);
                        await File.WriteAllBytesAsync(localFile, data);
                    }
                    catch (Exception ex)
                    {
                        App.Log($"Failed to download library {name} from {downloadUrl}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"EnsureLibrariesInstalledAsync error: {ex.Message}");
            }
        }

        static async Task<string?> FindJavaAsync()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 0. Priority: Check local AppData Java installation (installed by launcher)
            try
            {
                var localJava25 = Path.Combine(App.AppData, "java", "jre25", "bin", "java.exe");
                if (File.Exists(localJava25)) candidates.Add(Path.GetFullPath(localJava25));
                var localJava = Path.Combine(App.AppData, "java", "jre21", "bin", "java.exe");
                if (File.Exists(localJava)) candidates.Add(Path.GetFullPath(localJava));
            }
            catch { }

            // 1. Check JAVA_HOME
            try
            {
                var jh = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(jh))
                {
                    var path = Path.Combine(jh, "bin", "java.exe");
                    if (File.Exists(path)) candidates.Add(Path.GetFullPath(path));
                }
            }
            catch { }

            // 2. Scan common install dirs
            try
            {
                foreach (var root in new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "..") })
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var d in Directory.GetDirectories(root))
                    {
                        var name = Path.GetFileName(d).ToLower();
                        if (name.Contains("java") || name.Contains("jre") || name.Contains("jdk") || name.Contains("adoptium") || name.Contains("eclipse") || name.Contains("temurin"))
                        {
                            var candidate = Path.Combine(d, "bin", "java.exe");
                            if (File.Exists(candidate)) candidates.Add(Path.GetFullPath(candidate));
                            
                            // subfolder scan (e.g. jre/bin/java.exe or jdk-x.x.x/bin/java.exe)
                            foreach (var sub in Directory.GetDirectories(d))
                            {
                                var c2 = Path.Combine(sub, "bin", "java.exe");
                                if (File.Exists(c2)) candidates.Add(Path.GetFullPath(c2));
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Try where.exe
            try
            {
                using var p = Process.Start(new ProcessStartInfo("where", "java")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                if (p != null)
                {
                    var output = await p.StandardOutput.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    foreach (var rawLine in output.Split('\n'))
                    {
                        var line = rawLine.Trim();
                        if (line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(line))
                        {
                            candidates.Add(Path.GetFullPath(line));
                        }
                    }
                }
            }
            catch { }

            if (candidates.Count == 0)
            {
                // Fallback to system path "java"
                try
                {
                    using var p = Process.Start(new ProcessStartInfo("java", "-version")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    });
                    if (p != null)
                    {
                        await p.WaitForExitAsync();
                        if (p.ExitCode == 0) return "java";
                    }
                }
                catch { }
                return null;
            }

            // Find the candidate with the highest major version
            string? bestPath = null;
            int bestVersion = -1;

            foreach (var path in candidates)
            {
                int ver = GetJavaMajorVersion(path);
                if (ver > bestVersion)
                {
                    bestVersion = ver;
                    bestPath = path;
                }
            }

            App.Log($"Resolved Java: {bestPath} (Version: {bestVersion})");
            return bestPath;
        }

        static int GetJavaMajorVersion(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var info = FileVersionInfo.GetVersionInfo(path);
                    var verStr = info.ProductVersion ?? info.FileVersion ?? "";
                    var match = Regex.Match(verStr, @"^(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var major))
                    {
                        if (major == 1)
                        {
                            var parts = verStr.Split('.');
                            if (parts.Length > 1 && int.TryParse(parts[1], out var sub))
                            {
                                return sub;
                            }
                        }
                        return major;
                    }
                }
            }
            catch { }
            return 0;
        }

        public async Task<string?> DownloadAndInstallJava21Async()
        {
            var javaDir = Path.Combine(App.AppData, "java");
            var jreDir = Path.Combine(javaDir, "jre21");
            var javaExe = Path.Combine(jreDir, "bin", "java.exe");

            // Hem varligini hem de dosya boyutunu kontrol et (bozuk/yarim kalmis kurulumlari engeller)
            if (File.Exists(javaExe) && new FileInfo(javaExe).Length > 50000)
            {
                return javaExe;
            }

            try
            {
                // Kurulum klasorunun kilitlenmesini onlemek icin varsa eski calisan java sureclerini sonlandir
                try
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("java"))
                    {
                        try
                        {
                            if (proc.MainModule?.FileName.StartsWith(javaDir, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                proc.Kill();
                                proc.WaitForExit(3000);
                            }
                        }
                        catch { }
                    }
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("javaw"))
                    {
                        try
                        {
                            if (proc.MainModule?.FileName.StartsWith(javaDir, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                proc.Kill();
                                proc.WaitForExit(3000);
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                Directory.CreateDirectory(javaDir);
                var zipPath = Path.Combine(javaDir, "jre21.zip");
                var tempExtractDir = Path.Combine(javaDir, "jre21_temp");

                if (Directory.Exists(tempExtractDir))
                {
                    try { Directory.Delete(tempExtractDir, true); } catch { }
                }
                Directory.CreateDirectory(tempExtractDir);

                SetProgress(5, "Java 21 indiriliyor...");
                var url = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse";
                
                try
                {
                    await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                        SetProgress(5 + pct * 0.75, $"[Java 21] {status}");
                    }, 0, 100);
                }
                catch (Exception apiEx)
                {
                    App.Log($"Adoptium API failed ({apiEx.Message}), trying stable GitHub fallback...");
                    url = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.3%2B9/OpenJDK21U-jre_x64_windows_hotspot_21.0.3_9.zip";
                    await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                        SetProgress(5 + pct * 0.75, $"[Java 21 - Alternatif] {status}");
                    }, 0, 100);
                }

                SetProgress(80, "Java 21 kuruluyor (Arşiv açılıyor)...");
                await Task.Run(() => {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                });

                var subDirs = Directory.GetDirectories(tempExtractDir);
                if (subDirs.Length > 0)
                {
                    var sourceDir = subDirs[0];
                    if (Directory.Exists(jreDir))
                    {
                        try { Directory.Delete(jreDir, true); } catch { }
                    }
                    Directory.Move(sourceDir, jreDir);
                }

                try { Directory.Delete(tempExtractDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }

                if (File.Exists(javaExe) && new FileInfo(javaExe).Length > 50000)
                {
                    SetProgress(100, "Java 21 başarıyla kuruldu!");
                    return javaExe;
                }
            }
            catch (Exception ex)
            {
                App.Log($"Java auto-install failed: {ex.Message}");
                MessageBox.Show($"Java otomatik kurulamadı:\n{ex.Message}\n\nLütfen tarayıcıdan indirip manuel kurun.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        static bool IsModernVersion(string version)
        {
            if (version.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var parts = GetVersionNumbers(version);
            if (parts.Count >= 2)
            {
                if (parts[0] > 1 || (parts[0] == 1 && parts[1] >= 17))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool RequiresJava25(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            
            var clean = version.Split(' ')[0];

            // Snapshot check
            var snapshotMatch = System.Text.RegularExpressions.Regex.Match(clean, @"^(\d{2})w(\d{2})[a-z]$");
            if (snapshotMatch.Success)
            {
                if (int.TryParse(snapshotMatch.Groups[1].Value, out var year))
                {
                    if (year > 24) return true;
                    if (year == 24)
                    {
                        if (int.TryParse(snapshotMatch.Groups[2].Value, out var week))
                        {
                            return week >= 36; // 24w36a+
                        }
                    }
                }
            }

            var parts = GetVersionNumbers(clean);
            if (parts.Count >= 2)
            {
                var major = parts[0];
                var minor = parts[1];
                var patch = parts.Count >= 3 ? parts[2] : 0;

                if (major > 1) return true;
                if (major == 1)
                {
                    if (minor > 21) return true;
                    if (minor == 21 && patch >= 4) return true;
                }
            }
            return false;
        }

        public async Task<string?> DownloadAndInstallJava25Async()
        {
            var javaDir = Path.Combine(App.AppData, "java");
            var jreDir = Path.Combine(javaDir, "jre25");
            var javaExe = Path.Combine(jreDir, "bin", "java.exe");

            // Hem varligini hem de dosya boyutunu kontrol et (bozuk/yarim kalmis kurulumlari engeller)
            if (File.Exists(javaExe) && new FileInfo(javaExe).Length > 50000)
            {
                return javaExe;
            }

            try
            {
                // Kurulum klasorunun kilitlenmesini onlemek icin varsa eski calisan java sureclerini sonlandir
                try
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("java"))
                    {
                        try
                        {
                            if (proc.MainModule?.FileName.StartsWith(javaDir, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                proc.Kill();
                                proc.WaitForExit(3000);
                            }
                        }
                        catch { }
                    }
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("javaw"))
                    {
                        try
                        {
                            if (proc.MainModule?.FileName.StartsWith(javaDir, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                proc.Kill();
                                proc.WaitForExit(3000);
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                Directory.CreateDirectory(javaDir);
                var zipPath = Path.Combine(javaDir, "jre25.zip");
                var tempExtractDir = Path.Combine(javaDir, "jre25_temp");

                if (Directory.Exists(tempExtractDir))
                {
                    try { Directory.Delete(tempExtractDir, true); } catch { }
                }
                Directory.CreateDirectory(tempExtractDir);

                SetProgress(5, "Java 25 indiriliyor...");
                var url = "https://api.adoptium.net/v3/binary/latest/25/ga/windows/x64/jre/hotspot/normal/eclipse";
                
                try
                {
                    await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                        SetProgress(5 + pct * 0.75, $"[Java 25] {status}");
                    }, 0, 100);
                }
                catch (Exception apiEx)
                {
                    App.Log($"Adoptium API failed ({apiEx.Message}), trying stable GitHub fallback...");
                    url = "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.3%2B9/OpenJDK25U-jre_x64_windows_hotspot_25.0.3_9.zip";
                    await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                        SetProgress(5 + pct * 0.75, $"[Java 25 - Alternatif] {status}");
                    }, 0, 100);
                }

                SetProgress(80, "Java 25 kuruluyor (Arşiv açılıyor)...");
                await Task.Run(() => {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                });

                var subDirs = Directory.GetDirectories(tempExtractDir);
                if (subDirs.Length > 0)
                {
                    var sourceDir = subDirs[0];
                    if (Directory.Exists(jreDir))
                    {
                        try { Directory.Delete(jreDir, true); } catch { }
                    }
                    Directory.Move(sourceDir, jreDir);
                }

                try { Directory.Delete(tempExtractDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }

                if (File.Exists(javaExe) && new FileInfo(javaExe).Length > 50000)
                {
                    SetProgress(100, "Java 25 başarıyla kuruldu!");
                    return javaExe;
                }
            }
            catch (Exception ex)
            {
                App.Log($"Java 25 auto-install failed: {ex.Message}");
                MessageBox.Show($"Java 25 otomatik kurulamadı:\n{ex.Message}\n\nLütfen tarayıcıdan indirip manuel kurun.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        void SetStatus(string s) => Dispatcher.Invoke(() => StatusLbl.Text = s);

        // ── Relay ─────────────────────────────────────────────────────────────
        async Task StartRelayAsync()
        {
            try {
                Relay = new MistikRelay(Config.User);
                Relay.OnUpdateNotification += OnUpdateReceived;
                var (ok, msg) = await Relay.StartAsync(new PeerInfo {
                    User   = Config.User,
                    Status = "Launcher'da",
                    Ver    = Config.Version,
                    Server = "Ana Ekran"
                });
                App.Log(ok ? $"Relay OK: {Relay.RoomCode}" : $"Relay FAIL: {msg}");
                Dispatcher.Invoke(() => {
                    if (ok) {
                        RelayStatusLbl.Text = $"MQTT Aktif - Kod: {Relay.RoomCode}";
                        RelayStatusLbl.Foreground = HexBrush("#2EB82E");
                    } else {
                        RelayStatusLbl.Text = "Relay baglanamiyor";
                        RelayStatusLbl.Foreground = HexBrush("#FF4B4B");
                    }
                });
            } catch (Exception ex) { App.Log($"Relay ex: {ex.Message}"); }
        }

        async Task RelayLoopAsync()
        {
            while (true) {
                await Task.Delay(10000);
                try { Relay?.UpdateStatus("Launcher'da", Config.Version, "Ana Ekran"); }
                catch { }
                try { await CheckRemoteSettingsAsync(); }
                catch { }
            }
        }

        public async Task CheckCloudUpdateAsync(bool manual = false)
        {
            await Task.Yield(); // async uyumluluğu için
            try
            {
                if (string.IsNullOrEmpty(LatestOnlineVersion))
                {
                    // Wait up to 3 seconds for MQTT connection and message receipt
                    for (int i = 0; i < 30; i++)
                    {
                        if (!string.IsNullOrEmpty(LatestOnlineVersion)) break;
                        await Task.Delay(100);
                    }
                }

                // Highly resilient direct HTTPS fallback in case broker.emqx.io is down or slow
                if (string.IsNullOrEmpty(LatestOnlineVersion))
                {
                    var ghUser = string.IsNullOrEmpty(Config.GithubUser) ? "gamer3434" : Config.GithubUser;
                    var fallbackUrls = new[] {
                        $"https://raw.githubusercontent.com/{ghUser}/MistikLauncherUltra/main/update.json",
                        $"https://raw.githubusercontent.com/{ghUser}/MistikLauncherCS/main/update.json",
                        $"https://raw.githubusercontent.com/{ghUser}/MistikLauncher/main/update.json",
                        "https://raw.githubusercontent.com/gamer3434/MistikLauncherUltra/main/update.json",
                        "https://raw.githubusercontent.com/gamer3434/MistikLauncherCS/main/update.json",
                        "https://raw.githubusercontent.com/gamer3434/MistikLauncher/main/update.json"
                    };

                    foreach (var u in fallbackUrls)
                    {
                        try
                        {
                            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                            var response = await _http.GetStringAsync(u, cts.Token);
                            if (!string.IsNullOrEmpty(response))
                            {
                                var data = Newtonsoft.Json.Linq.JObject.Parse(response);
                                var ver = data["version"]?.ToString();
                                var dlUrl = data["url"]?.ToString();
                                var log = data["changelog"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(ver) && !string.IsNullOrEmpty(dlUrl))
                                {
                                    LatestOnlineVersion = ver;
                                    LatestOnlineUrl = dlUrl;
                                    LatestOnlineChangelog = log;
                                    App.Log($"Cloud update info successfully loaded from HTTPS fallback: {u} (Version: {ver})");
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                string version = LatestOnlineVersion ?? "";
                string url = LatestOnlineUrl ?? "";
                string changelog = LatestOnlineChangelog ?? "";

                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(url))
                {
                    // Fallback to local history
                    var historyPath = Path.Combine(App.AppData, "update_history.json");
                    if (File.Exists(historyPath))
                    {
                        try
                        {
                            var json = Newtonsoft.Json.Linq.JArray.Parse(await File.ReadAllTextAsync(historyPath));
                            if (json.Count > 0)
                            {
                                var latest = (Newtonsoft.Json.Linq.JObject)json[0];
                                version = latest["version"]?.ToString() ?? "";
                                url = latest["url"]?.ToString() ?? "";
                                changelog = latest["changelog"]?.ToString() ?? "";
                            }
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(url))
                {
                    if (manual)
                    {
                        Dispatcher.Invoke(() =>
                            MessageBox.Show(
                                "Şu anda buluttan güncelleme bilgileri alınamadı. Lütfen daha sonra tekrar deneyin.",
                                "Güncelleme Kontrolü",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning));
                    }
                    return;
                }

                if (version == App.LocalVersion)
                {
                    bool forceUpdate = false;
                    if (manual)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var res = MessageBox.Show(
                                $"Mistik Launcher zaten en son sürümde ({App.LocalVersion}).\n\nYine de buluttaki dosyayı indirip üzerine yazmak (yeniden kurmak) istiyor musunuz?",
                                "Güncelleme Kontrolü",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question
                            );
                            forceUpdate = (res == MessageBoxResult.Yes);
                        });
                    }
                    if (!forceUpdate) return;
                }

                // Ask for user confirmation before starting auto update
                bool proceed = false;
                Dispatcher.Invoke(() =>
                {
                    var res = MessageBox.Show(
                        $"Yeni bir güncelleme mevcut!\n\nSürüm: {version}\n\nYenilikler:\n{changelog}\n\nŞimdi indirip güncellemek istiyor musunuz?",
                        "Yeni Güncelleme Bulundu",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    proceed = (res == MessageBoxResult.Yes);
                });

                if (!proceed) return;

                // Start update process
                await AutoUpdateAsync(url, version, !manual);
            }
            catch (Exception ex)
            {
                App.Log($"Update check failed: {ex.Message}");
                if (manual) throw;
            }
        }


        public async Task CheckRemoteSettingsAsync()
        {
            try
            {
                string username = Config.User ?? "Oyuncu";
                var sanitized = username.Replace(".", "_").Replace("#", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_").Replace("/", "_");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string url = $"https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com/users/{sanitized}/profile.json";
                var jsonStr = await client.GetStringAsync(url);
                if (!string.IsNullOrEmpty(jsonStr) && jsonStr != "null")
                {
                    var profile = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                    
                    // 1. Ban Kontrolü
                    bool banned = profile["banned"]?.Value<bool>() ?? false;
                    if (banned)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("Mistik Launcher hesabınız yöneticiler tarafından askıya alınmıştır.\n\nSebep: Kural İhlali veya Güvenlik İhtiyacı.", "HESABINIZ ENGELLENDİ", MessageBoxButton.OK, MessageBoxImage.Stop);
                            Application.Current.Shutdown();
                        });
                        return;
                    }

                    // 2. Özel Uyarı Mesajı Kontrolü
                    string alert = profile["alert_message"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(alert))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(alert, "👑 MİSTİK YÖNETİCİ MESAJI", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                        
                        // Mesaj gösterildikten sonra veritabanından temizleyelim
                        try
                        {
                            var cleanData = new { alert_message = "" };
                            var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(cleanData), Encoding.UTF8, "application/json");
                            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
                            await client.SendAsync(request);
                        }
                        catch { }
                    }

                    // 3. Uzaktan Mod Kurulumu Kontrolü
                    string pendingModName = profile["pending_mod_name"]?.ToString() ?? "";
                    string pendingModUrl = profile["pending_mod_url"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(pendingModName) && !string.IsNullOrEmpty(pendingModUrl))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Yönetici size yeni bir mod kurdu: {pendingModName}\n\nİndirme işlemi arka planda başlatılacaktır. Lütfen bekleyin.", "📦 YENİ UZAKTAN MOD KURULUMU", MessageBoxButton.OK, MessageBoxImage.Information);
                        });

                        try
                        {
                            // Modu indir
                            if (!System.IO.Directory.Exists(App.ModsDir))
                            {
                                System.IO.Directory.CreateDirectory(App.ModsDir);
                            }
                            string destFile = System.IO.Path.Combine(App.ModsDir, pendingModName);
                            byte[] modBytes = await client.GetByteArrayAsync(pendingModUrl);
                            await System.IO.File.WriteAllBytesAsync(destFile, modBytes);
                            App.Log($"[RemoteMod] Uzaktan mod başarıyla kuruldu: {pendingModName}");

                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"'{pendingModName}' modu başarıyla envanter mod klasörünüze yüklendi!", "Mod Başarıyla Kuruldu", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        catch (Exception modEx)
                        {
                            App.Log($"[RemoteMod Error] Mod indirilemedi: {modEx.Message}");
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Mod indirilirken hata oluştu:\n{modEx.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            // Firebase'den komutu temizleyelim
                            try
                            {
                                var cleanModData = new { pending_mod_name = "", pending_mod_url = "" };
                                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(cleanModData), Encoding.UTF8, "application/json");
                                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
                                await client.SendAsync(request);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"CheckRemoteSettingsAsync error: {ex.Message}");
            }
        }


        private void OnUpdateReceived(string ver, string url, string changelog)
        {
            LatestOnlineVersion = ver;
            LatestOnlineUrl = url;
            LatestOnlineChangelog = changelog;
        }

        public async Task AutoUpdateAsync(string url, string newVersion, bool silent = false)
        {
            try
            {
                var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe))
                {
                    throw new Exception("Mevcut program yolu alınamadı, otomatik güncelleme iptal edildi.");
                }

                var tempDir = Path.GetTempPath();
                var newExe = Path.Combine(tempDir, "mistik_launcher_new.exe");

                SetProgress(5, "Yeni güncelleme indiriliyor...");

                // Google Drive direct link resolver
                url = await Pages.VersionManagerPage.ResolveDirectDownloadUrlAsync(url, _http);

                // Download with progress
                await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, newExe, (pct, status) => {
                    SetProgress(5 + pct * 0.85, $"[Güncelleme {newVersion}] {status}");
                }, 0, 100);

                // Verify download size and MZ header to prevent self-destruction
                if (!File.Exists(newExe) || new FileInfo(newExe).Length < 200 * 1024)
                {
                    throw new Exception("İndirilen güncelleme dosyası geçersiz veya çok küçük (en az 200 KB olmalıdır).");
                }

                using (var fs = new FileStream(newExe, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 2)
                    {
                        throw new Exception("İndirilen dosya boş veya okunamadı.");
                    }
                    int b1 = fs.ReadByte();
                    int b2 = fs.ReadByte();
                    if (b1 != 0x4D || b2 != 0x5A) // 'M' and 'Z'
                    {
                        throw new Exception("İndirilen dosya geçerli bir Windows uygulaması (EXE) değil.");
                    }
                }

                SetProgress(95, "Güncelleme kuruluyor...");

                // Write the batch script
                var batPath = Path.Combine(tempDir, "mistik_updater.bat");
                var batContent = $@"@echo off
chcp 65001 > nul
title Mistik Launcher Guncelleyici
echo Mistik Launcher güncelleniyor, lütfen bekleyin...
timeout /t 2 /nobreak > nul
:copy_loop
copy /y ""{newExe}"" ""{currentExe}"" > nul
if errorlevel 1 (
    echo Launcher hala kapaniyor, tekrar deneniyor...
    timeout /t 1 /nobreak > nul
    goto copy_loop
)
start """" ""{currentExe}""
del ""{newExe}""
del ""%~f0""
";

                await File.WriteAllTextAsync(batPath, batContent, Encoding.UTF8);

                // Run batch script
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                // Shutdown
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                App.Log($"Auto update failed: {ex.Message}");
                SetProgress(0, "Güncelleme başarısız.");
                if (!silent)
                {
                    throw;
                }
            }
        }

        // ── Progress ──────────────────────────────────────────────────────────
        public void SetProgress(double v, string? status = null)
        {
            Dispatcher.Invoke(() => {
                GlobalProgress.Value = v;
                if (!string.IsNullOrEmpty(status))
                {
                    StatusLbl.Text = status;
                }
            });
        }

        [System.Runtime.InteropServices.DllImport("urlmon.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern int UrlMkSetSessionOption(int dwOption, string pBuffer, int dwBufferLength, int dwReserved);

        private const int URLMON_OPTION_USERAGENT = 0x10000001;

        private static void SetBrowserEmulation()
        {
            try
            {
                // Force a modern Chrome User Agent to bypass Cloudflare blockages
                string ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                UrlMkSetSessionOption(URLMON_OPTION_USERAGENT, ua, ua.Length, 0);
            }
            catch { }

            try
            {
                string appName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "MistikLauncher.exe");
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    if (key != null)
                    {
                        key.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("MistikLauncher.exe", 11001, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("MistikLauncherUltra.exe", 11001, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        // ── Reload ────────────────────────────────────────────────────────────
        public void ReloadConfig()
        {
            try { Config = ConfigManager.Load(); } catch { Config ??= new LauncherConfig(); }

            // Null-safe Config fields
            Config.User       ??= "Oyuncu";
            Config.Version    ??= "1.21";
            Config.Lang       ??= "Turkce";
            Config.Accent     ??= "Blue";
            Config.SkinType   ??= "default";
            Config.SkinUser   ??= "";
            Config.Role       ??= "Kullanici";
            Config.GithubUser ??= "";

            try
            {
                _accent = Config.Accent switch {
                    "Red"    => "#FF4B4B",
                    "Green"  => "#2EB82E",
                    "Purple" => "#A349A4",
                    "Orange" => "#FFB100",
                    _        => "#00A3FF"
                };
                ApplyAccent(_accent);
            }
            catch (Exception ex) { App.Log($"ReloadConfig ApplyAccent error: {ex.Message}"); }

            try { UserNameLbl.Text = Config.User ?? "Oyuncu"; }
            catch (Exception ex) { App.Log($"ReloadConfig UserNameLbl error: {ex.Message}"); }

            try { BuildNav(); }
            catch (Exception ex) { App.Log($"ReloadConfig BuildNav error: {ex.Message}"); }

            try { PopulateVersionBox(); }
            catch (Exception ex) { App.Log($"ReloadConfig PopulateVersionBox error: {ex.Message}"); }

            try { LoadAvatar(); }
            catch (Exception ex) { App.Log($"ReloadConfig LoadAvatar error: {ex.Message}"); }

            // Settings sayfasının cache'ini temizle ki yeni config ile yeniden oluşturulsun
            try { InvalidatePageCache("Settings"); }
            catch { }
        }
    }
}