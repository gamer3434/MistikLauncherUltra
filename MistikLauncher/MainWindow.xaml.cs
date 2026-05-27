using System;
using System.IO;
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

        public MainWindow()
        {
            SetBrowserEmulation();
            InitializeComponent();
            Config = ConfigManager.Load();
            Config.OpenCount++;
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

            BtnLaunch.Click  += (_, _) => HandleLaunch();
            BtnDiscord.Click += (_, _) => Open("https://discord.gg/");
            BtnYoutube.Click += (_, _) => Open("https://www.youtube.com/@kardoeditx99");

            Navigate("Dash");
            _ = StartRelayAsync();
            _ = RelayLoopAsync();
            // Arka planda otomatik güncelleme kontrolü devre dışı bırakıldı.
            // _ = Task.Delay(2000).ContinueWith(async _ => await CheckCloudUpdateAsync(false));
        }

        static void Open(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

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
            AddNav("Dash",      "Ana Panel");
            AddNav("Vers",      "Surum Yoneticisi");
            AddNav("Mods",      "Mod Merkezi");
            AddNav("Maps",      "Harita Merkezi");
            AddNav("Skin",      "Karakter Skin");
            AddNav("Elyby",     "Ely.by Paneli");
            AddNav("Changelog", "Son Guncellemeler");
            if (Config.Role == "Yonetici")
                AddNav("Admin", "Yonetici Paneli");
            AddNav("Opt",      "Optimizasyon");
            AddNav("Guide",    "Kurulum Rehberi");
            AddNav("Settings", "Ayarlar");
            AddNav("Licenses", "Lisanslar");
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
                    "Maps"      => new Pages.MapManagerPage(this),
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
                "1.26.2", "1.26.1", "1.26",
                "26.2.2", "26.2.1", "26.2", "26.1.2", "26.1.1", "26.1",
                "1.22", "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", 
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
                }
                else if (Config.AuthType == "elyby")
                {
                    var uname = Config.SkinType == "username" ? Config.SkinUser : Config.User;
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

                    if (!success) {
                        var img = await FetchAvatarAsync(uname, 40);
                        Dispatcher.Invoke(() => { if (img != null) AvatarImg.Source = img; });
                    }
                }
                else
                {
                    var uname = Config.SkinType == "username" ? Config.SkinUser : Config.User;
                    var img   = await FetchAvatarAsync(uname, 40);
                    Dispatcher.Invoke(() => { if (img != null) AvatarImg.Source = img; });
                }
            });
        }

        public async Task<BitmapImage?> FetchAvatarAsync(string username, int size = 64)
        {
            var elybyCache = Path.Combine(App.AppData, $"elyby_{username}.png");
            var cache = Path.Combine(App.AppData, $"avatar_{username}_{size}.png");
            try {
                // Önce Ely.by'den skin denemesi yapalım
                try {
                    if (File.Exists(elybyCache) && (DateTime.Now - File.GetLastWriteTime(elybyCache)).TotalDays < 1) {
                        // cache valid
                    } else {
                        var jsonStr = await _http.GetStringAsync($"http://skinsystem.ely.by/textures/{username}");
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                        var texUrl = jObj["SKIN"]?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(texUrl)) {
                            var elyBytes = await _http.GetByteArrayAsync(texUrl);
                            Directory.CreateDirectory(App.AppData);
                            await File.WriteAllBytesAsync(elybyCache, elyBytes);
                        }
                    }
                    var faceSrc = GetSkinFace(elybyCache);
                    if (faceSrc is System.Windows.Media.Imaging.BitmapSource bmpSrc) {
                        // Crop edilen yüzü BitmapImage formunda kullanamayız ama RenderTargetBitmap vb yapılabilir
                        // Veya Image nesnesine doğrudan ImageSource olarak verilebilir.
                        // FetchAvatarAsync sadece BitmapImage dönüyor. 
                    }
                } catch { }

                byte[] bytes;
                if (File.Exists(cache))
                    bytes = await File.ReadAllBytesAsync(cache);
                else {
                    bytes = await _http.GetByteArrayAsync(
                        $"https://mc-heads.net/avatar/{username}/{size}",
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
                var ram  = Math.Max(Config.Ram, 1) * 1024;
                var args = BuildLaunchArgs(version, ram, natives, injectorPath, resolvedUuid);

                SetProgress(70);
                SetStatus("Minecraft baslatılıyor...");
                App.Log($"Launch: {javaPath} {args[..Math.Min(args.Length,120)]}...");

                var psi = new ProcessStartInfo(javaPath, args) {
                    WorkingDirectory = App.GameDir,
                    UseShellExecute  = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                if (process != null)
                {
                    var errTask = process.StandardError.ReadToEndAsync();
                    var outTask = process.StandardOutput.ReadToEndAsync();
                    
                    // Wait 1.5 seconds to detect immediate exit or crash
                    await Task.Delay(1500);
                    if (process.HasExited)
                    {
                        var errText = await errTask;
                        var outText = await outTask;
                        var errMsg = !string.IsNullOrEmpty(errText) ? errText : outText;
                        if (string.IsNullOrEmpty(errMsg)) errMsg = "Oyun baslatilamadi veya beklenmedik sekilde kapandi. (Exit Code: " + process.ExitCode + ")";
                        throw new Exception(errMsg);
                    }

                    // ── Kernel Optimizasyonlarını Uygula ──
                    bool anyKernelOpt = Config.KernelPriority || Config.KernelTimer || Config.KernelAffinity || Config.KernelPower || Config.KernelNagle;
                    if (anyKernelOpt)
                    {
                        SetStatus("Kernel optimizasyonları uygulanıyor...");
                        KernelOptimizer.ApplyAll(process, Config);
                    }

                    // Oyun kapandığında optimizasyonları geri al
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await process.WaitForExitAsync();
                            KernelOptimizer.RevertAll();
                            App.Log("[KernelOpt] Oyun kapandı, tüm optimizasyonlar geri alındı.");
                        }
                        catch (Exception ex)
                        {
                            App.Log($"[KernelOpt] Oyun izleme hatası: {ex.Message}");
                            KernelOptimizer.RevertAll();
                        }
                    });
                }
                SetProgress(100);
                Relay?.UpdateStatus("Oyunda", version, "Minecraft");

                if (Config.AutoClose)
                    Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                App.Log($"Launch error: {ex.Message}");
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

                // Extract exact MC version (e.g. 1.21.1) from folder name
                var mcVersion = "1.21.1";
                var mcMatch = System.Text.RegularExpressions.Regex.Match(currentVer, @"1\.\d+(\.\d+)?");
                if (mcMatch.Success) mcVersion = mcMatch.Value;

                var lastSynced = Config.LastSyncedVersion ?? "";

                // If nothing has changed, do not do anything
                if (lastSynced == currentVer) return;

                var modsPoolDir = Path.Combine(App.AppData, "mods_pool");
                Directory.CreateDirectory(modsPoolDir);

                // 1. If we had a previously synced version, move current mods from App.ModsDir back to mods_pool/{lastSynced}
                if (!string.IsNullOrEmpty(lastSynced) && Directory.Exists(App.ModsDir))
                {
                    var lastMcVersion = "1.21.1";
                    var lastMcMatch = System.Text.RegularExpressions.Regex.Match(lastSynced, @"1\.\d+(\.\d+)?");
                    if (lastMcMatch.Success) lastMcVersion = lastMcMatch.Value;

                    var lastPoolDir = Path.Combine(modsPoolDir, lastMcVersion);
                    Directory.CreateDirectory(lastPoolDir);

                    var currentJars = Directory.GetFiles(App.ModsDir, "*.jar");
                    foreach (var jar in currentJars)
                    {
                        var dest = Path.Combine(lastPoolDir, Path.GetFileName(jar));
                        try
                        {
                            if (File.Exists(dest)) File.Delete(dest);
                            File.Move(jar, dest);
                            App.Log($"Moved active mod to pool: {Path.GetFileName(jar)} -> mods_pool/{lastMcVersion}");
                        }
                        catch (Exception ex)
                        {
                            App.Log($"Failed to move mod {Path.GetFileName(jar)} to pool: {ex.Message}");
                        }
                    }
                }

                // 2. Clear any leftover active jars in App.ModsDir to be perfectly clean
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

                // 3. Move/Copy mods from mods_pool/{mcVersion} into App.ModsDir
                var newPoolDir = Path.Combine(modsPoolDir, mcVersion);
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
                            App.Log($"Moved pool mod to active: {Path.GetFileName(jar)} from mods_pool/{mcVersion}");
                        }
                        catch (Exception ex)
                        {
                            App.Log($"Failed to activate mod {Path.GetFileName(jar)}: {ex.Message}");
                        }
                    }
                }

                // Update config
                Config.LastSyncedVersion = currentVer;
                ConfigManager.Save(Config);
                App.Log($"Mods synchronized successfully for version: {currentVer}");
            }
            catch (Exception ex)
            {
                App.Log($"SyncModsForCurrentVersion error: {ex.Message}");
            }
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
            var jvm     = $"-Xmx{ramMb}m -Xms{ramMb / 2}m -Dminecraft.server.onlineMode=false -Dminecraft.server.online-mode=false";
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
                    // Advanced low-latency, stutter-free G1GC options
                    optList.Add("-XX:+UseG1GC");
                    optList.Add("-XX:MaxGCPauseMillis=50");
                    optList.Add("-XX:+UnlockExperimentalVMOptions");
                    optList.Add("-XX:G1NewSizePercent=30");
                    optList.Add("-XX:G1MaxNewSizePercent=40");
                    optList.Add("-XX:G1ReservePercent=15");
                    optList.Add("-XX:G1HeapRegionSize=32m");
                }

                // Memory saving and CPU efficiency optimizations
                optList.Add("-XX:+UseStringDeduplication");
                optList.Add("-XX:+UseCompressedOops");
                optList.Add("-XX:+UseCompressedClassPointers");
                optList.Add("-XX:CICompilerCount=2");
                optList.Add("-XX:+OptimizeStringConcat");
            }
            else
            {
                optList.Add("-XX:+UseG1GC");
            }

            if (Config.OptFps)
            {
                // Force Direct3D/OpenGL Hardware Rendering Accelerators on Windows!
                optList.Add("-Dsun.java2d.d3d=true");
                optList.Add("-Dsun.java2d.opengl=true");
                optList.Add("-Dsun.java2d.noddraw=true");
                
                // Disable console logging overhead (boosts CPU performance!)
                optList.Add("-Dforge.forceNoStdout=true");
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
                    if (manual)
                    {
                        Dispatcher.Invoke(() =>
                            MessageBox.Show(
                                "Mistik Launcher güncel! En son sürümü kullanıyorsunuz.",
                                "Güncelleme Kontrolü",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information));
                    }
                    return;
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