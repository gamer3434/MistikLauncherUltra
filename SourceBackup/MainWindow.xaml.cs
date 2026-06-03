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

namespace MistikLauncherUltra
{
    public partial class MainWindow : Window
    {
        public LauncherConfig Config;
        public MistikRelay?   Relay;
        readonly Dictionary<string, Button> _navBtns = new();
        readonly HttpClient _http = new();
        string _accent = "#00A3FF";

        public MainWindow()
        {
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
            _ = Task.Delay(2000).ContinueWith(async _ => await CheckCloudUpdateAsync(false));
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
            AddNav("Cloud",     "Bulut Sunucular");
            AddNav("Friends",   "Arkadaslar");
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
            Page page = key switch {
                "Dash"      => new Pages.DashboardPage(this),
                "Vers"      => new Pages.VersionManagerPage(this),
                "Mods"      => new Pages.ModManagerPage(this),
                "Maps"      => new Pages.MapManagerPage(this),
                "Skin"      => new Pages.SkinPage(this),
                "Cloud"     => new Pages.CloudPage(this),
                "Friends"   => new Pages.FriendsPage(this),
                "Changelog" => new Pages.ChangelogPage(this),
                "Admin"     => new Pages.AdminPanelPage(this),
                "Opt"       => new Pages.OptimizationPage(this),
                "Guide"     => new Pages.GuidePage(this),
                "Settings"  => new Pages.SettingsPage(this),
                "Licenses"  => new Pages.LicensesPage(this),
                _           => new Pages.DashboardPage(this)
            };
            MainFrame.Navigate(page);
        }

        // ── Version box ───────────────────────────────────────────────────────
        public void PopulateVersionBox()
        {
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

            // 2. Add complete Minecraft version history from 1.8 up to latest 26.1.2 (Mojang's new 2026 format)
            var defaults = new[] { 
                "26.1.2", "26.1.1", "26.1",
                "1.21.1", "1.21", 
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
        public void LoadAvatar()
        {
            _ = Task.Run(async () => {
                var uname = Config.SkinType == "username" ? Config.SkinUser : Config.User;
                var img   = await FetchAvatarAsync(uname, 40);
                Dispatcher.Invoke(() => { if (img != null) AvatarImg.Source = img; });
            });
        }

        public async Task<BitmapImage?> FetchAvatarAsync(string username, int size = 64)
        {
            var cache = Path.Combine(App.AppData, $"avatar_{username}_{size}.png");
            try {
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
                    if (javaVer < 17)
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

                // 4. Argumanlari olustur
                var ram  = Math.Max(Config.Ram, 1) * 1024;
                var args = BuildLaunchArgs(version, ram, natives);

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

        string BuildLaunchArgs(string version, int ramMb, string natives)
        {
            var libs    = BuildClasspath(version);
            var jvm     = $"-Xmx{ramMb}m -Xms{ramMb / 2}m";
            
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
            try
            {
                var jsonPath = Path.Combine(App.GameDir, "versions", version, $"{version}.json");
                if (File.Exists(jsonPath))
                {
                    var jsonStr = File.ReadAllText(jsonPath);
                    var match = Regex.Match(jsonStr, @"""mainClass""\s*:\s*""([^""]+)""");
                    if (match.Success)
                    {
                        mainClass = match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return $"{jvm} {optArgs} " +
                   $"-Djava.library.path=\"{natives}\" " +
                   $"-cp \"{libs}\" {mainClass} " +
                   $"--username \"{Config.User}\" " +
                   $"--version \"{version}\" " +
                   $"--gameDir \"{App.GameDir}\" " +
                   $"--assetsDir \"{Path.Combine(App.GameDir, "assets")}\" " +
                   $"--accessToken 0 --uuid {Guid.NewGuid():N}";
        }

        string BuildClasspath(string version)
        {
            var libs  = new List<string>();
            var vDir  = Path.Combine(App.GameDir, "versions", version);
            var jar   = Path.Combine(vDir, $"{version}.jar");
            if (File.Exists(jar)) libs.Add(jar);
            var libDir = Path.Combine(App.GameDir, "libraries");
            if (Directory.Exists(libDir))
                libs.AddRange(Directory.GetFiles(libDir, "*.jar", SearchOption.AllDirectories));
            return string.Join(";", libs);
        }

        static async Task<string?> FindJavaAsync()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 0. Priority: Check local AppData Java installation (installed by launcher)
            try
            {
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

            if (File.Exists(javaExe))
            {
                return javaExe;
            }

            try
            {
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
                
                await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                    SetProgress(5 + pct * 0.75, $"[Java 21] {status}");
                }, 0, 100);

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

                if (File.Exists(javaExe))
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
            try
            {
                var manifestUrl = "https://raw.githubusercontent.com/Musta/mistik-client-metadata/main/cloud_update_manifest.json";
                var response = await _http.GetStringAsync(manifestUrl);
                var json = Newtonsoft.Json.Linq.JObject.Parse(response);
                var version = (string?)json["version"] ?? "";
                var url = (string?)json["download_url"] ?? (string?)json["url"] ?? "";
                var changelog = (string?)json["changelog"] ?? "";

                if (!string.IsNullOrEmpty(version))
                {
                    if (version != App.LocalVersion)
                    {
                        bool userWantsUpdate = false;
                        Dispatcher.Invoke(() =>
                        {
                            var res = MessageBox.Show(
                                $"Yeni Bir Güncelleme Bulundu!\n\nYeni Sürüm: {version}\nDeğişiklikler:\n{changelog}\n\nŞimdi indirmek ve otomatik güncellemek istiyor musunuz?",
                                "Bulut Güncelleme Sistemi",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);
                            if (res == MessageBoxResult.Yes)
                            {
                                userWantsUpdate = true;
                            }
                        });

                        if (userWantsUpdate)
                        {
                            await AutoUpdateAsync(url, version);
                        }
                    }
                    else
                    {
                        if (manual)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    "Mistik Launcher güncel! En son sürümü kullanıyorsunuz.",
                                    "Güncelleme Kontrolü",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                    }
                }
                else
                {
                    if (manual)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Güncelleme bilgisi alınamadı. Manifest geçersiz veya boş.",
                                "Güncelleme Kontrolü",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Cloud update check failed: {ex.Message}");
                if (manual)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Güncelleme kontrolü başarısız oldu:\n{ex.Message}",
                            "Güncelleme Kontrolü",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            }
        }

        private void OnUpdateReceived(string ver, string url, string changelog)
        {
            Dispatcher.Invoke(() => {
                var res = MessageBox.Show(
                    $"Yeni Bir Güncelleme Bulundu!\n\nYeni Sürüm: {ver}\nDeğişiklikler:\n{changelog}\n\nŞimdi indirmek ve otomatik güncellemek istiyor musunuz?",
                    "Mistik Launcher Güncellemesi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    _ = AutoUpdateAsync(url, ver);
                }
            });
        }

        public async Task AutoUpdateAsync(string url, string newVersion)
        {
            try
            {
                var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe))
                {
                    MessageBox.Show("Mevcut program yolu alınamadı, otomatik güncelleme iptal edildi.", "Güncelleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var tempDir = Path.GetTempPath();
                var newExe = Path.Combine(tempDir, "mistik_launcher_new.exe");

                SetProgress(5, "Yeni güncelleme indiriliyor...");

                // Download with progress
                await Pages.VersionManagerPage.DownloadFileWithProgressAsync(url, newExe, (pct, status) => {
                    SetProgress(5 + pct * 0.85, $"[Güncelleme {newVersion}] {status}");
                }, 0, 100);

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
                MessageBox.Show($"Güncelleme başarısız:\n{ex.Message}", "Güncelleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                SetProgress(0, "Güncelleme başarısız.");
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

        // ── Reload ────────────────────────────────────────────────────────────
        public void ReloadConfig()
        {
            Config = ConfigManager.Load();
            _accent = Config.Accent switch {
                "Red"    => "#FF4B4B",
                "Green"  => "#2EB82E",
                "Purple" => "#A349A4",
                "Orange" => "#FFB100",
                _        => "#00A3FF"
            };
            ApplyAccent(_accent);
            UserNameLbl.Text = Config.User;
            BuildNav();
            PopulateVersionBox();
            LoadAvatar();
        }
    }
}