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
        public AutoMcsUpdater AutoMcs { get; } = new();
        public LauncherUpdater LauncherUpdates { get; }
        public MistikRelay?   Relay;
        readonly Dictionary<string, Page> _pageCache = new();
        readonly Dictionary<string, string> _pageLanguages = new();
        readonly HashSet<Page> _pendingTranslations = new();
        Page? _pendingPage;
        readonly HttpClient _http = new();
        readonly HttpClient _skinHttp = new() { Timeout=TimeSpan.FromSeconds(6),MaxResponseContentBufferSize=65536 };
        int _avatarGeneration, _skinPackGeneration;
        string _accent = "#00A3FF";
        string _currentNav = "Dash";
        bool _isPopulatingVersionBox = false;
        int _backgroundModSync;
        // ponytail: one per-window gate; share it if multi-window support is added.
        readonly object _modSyncGate = new();
        internal object ConfigurationGate => _modSyncGate;

        public MainWindow()
        {

            InitializeComponent();
            Config = ConfigManager.Load();
            LauncherUpdates = new LauncherUpdater(() => BtnLaunch.IsEnabled && !AutoMcs.Busy && !(GlobalProgress.Value > 0 && GlobalProgress.Value < 100) && System.Windows.Input.Keyboard.FocusedElement is not TextBox);
            Title = "Mistik Launcher " + LauncherUpdates.CurrentVersion;
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
            ProfileButton.Click+=(_,_)=>Navigate("Settings");
            StateChanged+=(_,_)=>ApplyWindowAppearance();
            SourceInitialized+=(_,_)=>System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(ConstrainMaximizedWindow);

            VerBox.SelectionChanged += (s, e) => {
                if (_isPopulatingVersionBox) return;
                var selected = VerBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selected))
                {
                    SetVersion(selected);
                    StatusLbl.Text = $"{Localization.T("version")}: {selected}";
                    QueueBackgroundModSync();
                }
            };

            BtnLaunch.Click  += (_, _) => HandleLaunch();
            BtnDiscord.Content = "GitHub";
            BtnDiscord.Click += (_, _) => Open("https://github.com/gamer3434/MistikLauncherUltra");
            BtnYoutube.Click += (_, _) => Open("https://github.com/gamer3434/MistikLauncherUltra/releases");

            Localization.SetLanguage(Config.Lang);
            LanguageBox.SelectedIndex = Localization.Language == "en" ? 1 : 0;
            LanguageBox.SelectionChanged += (_,_) => SwitchLanguage(LanguageBox.SelectedIndex == 1 ? "English" : "Turkce");
            Localization.Changed += RefreshLanguage;
            MainFrame.LoadCompleted += (_, e) => {
                if (ReferenceEquals(_pendingPage, e.Content)) _pendingPage = null;
                if (e.Content is Page loaded && _pendingTranslations.Remove(loaded))
                    Localization.TranslateTree(loaded);
            };
            Closed += (_,_) => { Localization.Changed -= RefreshLanguage; _http.Dispose(); _skinHttp.Dispose(); };
            RefreshLanguage();
            var updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            updateTimer.Tick += async (_,_) => await CheckLauncherUpdatesAsync(Config.LauncherAutoUpdate);
            Loaded += async (_,_) => {
                QueueBackgroundModSync();
                updateTimer.Start();
                await Task.WhenAll(CheckLauncherUpdatesAsync(Config.LauncherAutoUpdate), AutoMcs.CheckAsync(Config.AutoMcsAutoUpdate && File.Exists(AutoMcs.ExecutablePath)));
            };
            Closed += (_,_) => updateTimer.Stop();
            Navigate("Dash");
            // Relay is opt-in. Do not start a polling task before the user connects.
            // This prevents a permanent background loop on every launcher start.


            // Dispose the opt-in relay when the window closes.
            Closing += async (s, e) =>
            {
                try { if (Relay != null) await Relay.DisposeAsync(); } catch (Exception ex) { App.Log("Relay cleanup: " + ex.Message); }
            };
        }

        public async Task CheckLauncherUpdatesAsync(bool apply)
        {
            try
            {
                if(await LauncherUpdates.CheckAsync(apply) && apply && await LauncherUpdates.StartInstallerAsync())
                    System.Windows.Application.Current.Shutdown();
            }
            catch(Exception ex) { App.Log("Update handoff: " + ex.Message); MessageBox.Show(Localization.T("luError")+"\n"+ex.Message,"Mistik Launcher"); }
        }

        public async Task<GameRuntimeHealthResult> VerifyAndRepairGameAsync(string version, Action<string>? status = null)
        {
            var progress = new Progress<GameRuntimeHealthProgress>(update =>
            {
                var text = update.Status + (string.IsNullOrWhiteSpace(update.Artifact) ? "" : ": " + update.Artifact);
                status?.Invoke(text);
                SetStatus(text);
            });
            return await GameRuntimeHealth.VerifyAndRepairAsync(App.GameDir, version, progress: progress);
        }

        public void SwitchLanguage(string code)
        {
            var language = code == "English" || code == "en" ? "English" : "Turkce";
            if (Config.Lang == language && Localization.Language == (language == "English" ? "en" : "tr")) return;
            Config.Lang = language;
            ConfigManager.Save(Config);
            Localization.SetLanguage(Config.Lang);
        }
        void RefreshLanguage()
        {
            ApplyWindowAppearance();
            BuildNav();
            BtnLaunch.Content = Localization.T("play");
            ProfileButton.ToolTip=Localization.T("profile");
            BtnYoutube.Content = Localization.T("releases");
            SelectNav(_currentNav);
            StatusLbl.Text = Localization.T("version") + ": " + Config.Version;
            if(MainFrame.Content is ILanguagePage currentPage) currentPage.RefreshLanguage();
            Localization.TranslateTree(MainFrame);
            if (MainFrame.Content is Page active && _pageCache.TryGetValue(_currentNav, out var cached) && ReferenceEquals(active, cached))
            {
                _pageLanguages[_currentNav] = Localization.Language;
                _pendingTranslations.Remove(active);
            }
            var index = Localization.Language == "en" ? 1 : 0;
            if (LanguageBox.SelectedIndex != index) LanguageBox.SelectedIndex = index;
        }
        static void Open(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private async void InitializeStartupOptimizationsAsync()
        {
            try
            {
                // Run GPU detection and optimizations on a background thread so WMI queries or registry calls never hang the UI thread!
                await Task.Run(() =>
                {
                    try
                    {
                        // GPU Algılama
                        string gpuName = KernelOptimizer.DetectGpuName();
                        App.Log($"[Startup] Algılanan Ekran Kartı: {gpuName}");

                        // NVIDIA Profil Kaydı ve GPU tercihi
                        try
                        {
                            KernelOptimizer.ApplyGpuPreference(Process.GetCurrentProcess());
                        }
                        catch (Exception ex)
                        {
                            App.Log($"[Startup GPU Opt Hata] {ex.Message}");
                        }

                        // Optimizasyon Durum Tespiti
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
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[Startup Background Opt Hata] {ex.Message}");
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
            ColorThemes.Apply(Config.Accent);
            _accent=ColorThemes.Accent;
            foreach(var resource in ColorThemes.Resources) Resources[resource.Key]=resource.Value;
            Resources["ThemeActionText"]=ColorThemes.ActionText;
            var c = ColorThemes.Brush("#00A3FF").Color;
            LogoText.Foreground  = new SolidColorBrush(c);
            BtnLaunch.Background = ColorThemes.Brush("#00A3FF");
            BtnLaunch.Foreground = ColorThemes.ActionText;
            GlobalProgress.Foreground = new SolidColorBrush(c);
            ApplyWindowAppearance();
        }

        public void SetColorTheme(string name)
        {
            Config.Accent=name; ConfigManager.Save(Config); ApplyAccent(name);
            SelectNav(_currentNav);
        }

        public static Color HexColor(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        public static SolidColorBrush HexBrush(string hex) => ColorThemes.IsThemed(hex)?ColorThemes.Brush(hex):new(HexColor(hex));

        // ── Nav ───────────────────────────────────────────────────────────────
        void BuildNav() => BuildQuickBar();

        readonly Dictionary<string,Button> quickButtons=new();
        public void BuildQuickBar()
        {
            QuickBarPanel.Children.Clear(); quickButtons.Clear();
            foreach(var item in new[]{("Dash","home","\uE80F"),("Vers","versions","\uE7FC"),("Mods","mods","\uE74C"),("Skin","skin","\uE77B"),("Server","server","\uE968"),("Updates","updates","\uE777"),("Settings","settings","\uE713"),("Opt","optimization","\uE9D9")})
            {
                if(item.Item1!="Opt" && item.Item1!="Updates" && !Config.QuickLinks.Contains(item.Item1)) continue;
                var label=item.Item1 switch { "Updates" => Localization.Language=="en"?"Updates":"Güncellemeler", _ => Localization.T(item.Item2) };
                var content=new StackPanel { Orientation=Orientation.Horizontal };
                content.Children.Add(new TextBlock { Text=item.Item3,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=16,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0) });
                content.Children.Add(new TextBlock { Text=label,VerticalAlignment=VerticalAlignment.Center });
                var button=new Button { Content=content,Style=(Style)FindResource("NavBtn"),Height=44,Padding=new Thickness(12,0,12,0),MinWidth=100 };
                System.Windows.Automation.AutomationProperties.SetName(button,label);
                button.Click+=(_,_)=>Navigate(item.Item1); quickButtons[item.Item1]=button; QuickBarPanel.Children.Add(button);
            }
            QuickBarHost.Visibility=quickButtons.Count==0?Visibility.Collapsed:Visibility.Visible;
            SelectNav(_currentNav);
        }

        void SelectNav(string key)
        {
            foreach(var item in quickButtons) {
                bool selected=item.Key==key;
                item.Value.Background=selected?ColorThemes.Brush("#274565"):Brushes.Transparent;
                item.Value.Foreground=selected?Brushes.White:HexBrush("#ADBED6");
                item.Value.BorderBrush=selected?ColorThemes.Brush("#00A3FF"):Brushes.Transparent;
                item.Value.BorderThickness=new Thickness(selected?1:0);
            }
        }

        public void Navigate(string key)
        {
            if (key == "Admin") key = "Settings";
            _currentNav = key;
            SelectNav(key);

            // Server sayfası her zaman cache'den gelsin — sunucu kapanmasın!
            // Diğer sayfalar da cache'e alınır (hızlı geçiş için).
            bool created=false;
            if (!_pageCache.TryGetValue(key, out Page? page) || page == null)
            {
                created=true;
                page = key switch {
                    "Dash"      => new Pages.ModernHomePage(this),
                    "Vers"      => new Pages.VersionManagerPage(this),
                    "Mods"      => new Pages.ModManagerPage(this),
                    "Skin"      => new Pages.SkinPage(this),
                    "Elyby"     => new Pages.ElybyPage(this),
                    "Friends"   => new Pages.FriendsPage(this),
                    "Server"    => new Pages.ServerManagerPage(this),
                    "Changelog" => new Pages.ChangelogPage(this),
                    "Opt"       => new Pages.OptimizationPage(this),
                    "Guide"     => new Pages.GuidePage(this),
                    "Updates"   => new Pages.ModernSettingsPage(this, updatesOnly:true),
                    "Settings"  => new Pages.ModernSettingsPage(this),
                    "Licenses"  => new Pages.LicensesPage(this),
                    _           => new Pages.ModernHomePage(this)
                };
                _pageCache[key] = page;
            }

            if (ReferenceEquals(_pendingPage, page) || (_pendingPage == null && ReferenceEquals(MainFrame.Content, page))) return;

            page.Resources[typeof(ComboBox)]=FindResource(typeof(ComboBox));
            page.Resources[typeof(ComboBoxItem)]=FindResource(typeof(ComboBoxItem));
            foreach(var resource in ColorThemes.Resources) page.Resources[resource.Key]=resource.Value;
            page.Resources["ThemeActionText"]=ColorThemes.ActionText;
            // Cached pages only need rebuilding when the language has changed.
            bool languageChanged = created || !_pageLanguages.TryGetValue(key, out var pageLanguage) || pageLanguage != Localization.Language;
            if (!created && languageChanged && page is ILanguagePage localized) localized.RefreshLanguage();
            _pageLanguages[key] = Localization.Language;
            if (languageChanged) _pendingTranslations.Add(page);
            _pendingPage = page;
            MainFrame.Navigate(page);
        }

        // Belirli bir sayfanın cache'ini temizler (yeniden oluşturmak için)
        public void InvalidatePageCache(string key)
        {
            if (_pageCache.Remove(key, out var page)) _pendingTranslations.Remove(page);
            _pageLanguages.Remove(key);
        }


        // ── Version box ───────────────────────────────────────────────────────
        public void PopulateVersionBox()
        {
            _isPopulatingVersionBox = true;
            try
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
                                if (GameProfiles.IsInstalled(App.GameDir, name))
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
            QueueBackgroundModSync();
        }

        void QueueBackgroundModSync()
        {
            if (!IsLoaded || string.IsNullOrWhiteSpace(Config.Version) || Interlocked.Exchange(ref _backgroundModSync,1)!=0) return;
            var requestedVersion=Config.Version;
            _ = Task.Run(() => {
                try { SyncModsForCurrentVersion(requestedVersion); }
                finally
                {
                    Interlocked.Exchange(ref _backgroundModSync,0);
                    // A selection change while the scan was running must not be lost.
                    string currentVersion;
                    lock (_modSyncGate) currentVersion = Config.Version ?? "";
                    if (!string.Equals(currentVersion,requestedVersion,StringComparison.Ordinal))
                        QueueBackgroundModSync();
                }
            });
        }

        public void SetVersion(string version)
        {
            lock (_modSyncGate)
            {
                Config.Version = version;
                ConfigManager.Save(Config);
            }
            if(_pageCache.TryGetValue("Vers",out var page) && page is Pages.VersionManagerPage versions && versions.IsLoaded)
                versions.RefreshSelection();
            if(_pageCache.TryGetValue("Dash", out var homePage) && homePage is Pages.ModernHomePage home) { home.RefreshLanguage(); home.InvalidateReadiness(); }
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

        (string Type, string Skin, string User) SkinSelection() => (Config.SkinType, Config.SkinUser, Config.User);

        public void LoadAvatar() => _ = LoadAvatarAsync();
        public async Task LoadAvatarAsync(HttpClient? client = null)
        {
            int generation = ++_avatarGeneration;
            var selection = SkinSelection();
            try
            {
                var image = selection.Type == "local" && File.Exists(selection.Skin)
                    ? GetSkinFace(selection.Skin)
                    : await FetchAvatarAsync(selection.Type == "username" ? selection.Skin : selection.User, 40, client);
                if (generation != _avatarGeneration || selection != SkinSelection() || image == null) return;
                AvatarImg.Source = image;
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(AvatarImg, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            }
            catch (Exception ex) { App.Log("Avatar refresh: " + ex.Message); }
        }

        public static string? SkinTextureUrl(string? raw)
        {
            if(!Uri.TryCreate(raw,UriKind.Absolute,out var url) || url.Scheme is not ("http" or "https") || !url.IsDefaultPort || url.UserInfo.Length!=0 ||
                !(url.Host.Equals("ely.by",StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".ely.by",StringComparison.OrdinalIgnoreCase) || url.Host.Equals("textures.minecraft.net",StringComparison.OrdinalIgnoreCase))) return null;
            return new UriBuilder(url) { Scheme="https",Port=-1 }.Uri.AbsoluteUri;
        }
        public async Task<System.Windows.Media.ImageSource?> FetchAvatarAsync(string username, int size = 64, HttpClient? client = null)
        {
            client ??= _skinHttp;
            username??="";
            if(!Regex.IsMatch(username,@"^[A-Za-z0-9_]{3,16}$")) return null;
            size=Math.Clamp(size,16,128);
            var elybyCache = Path.Combine(App.AppData, $"elyby_{username}.png");
            var cache = Path.Combine(App.AppData, $"avatar_{username}_{size}.png");
            static BitmapImage DecodeAvatar(byte[] bytes)
            {
                var image = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream; image.EndInit(); image.Freeze();
                return image;
            }
            static async Task SaveCache(string path, byte[] bytes)
            {
                Directory.CreateDirectory(App.AppData);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { await File.WriteAllBytesAsync(temporary, bytes); File.Move(temporary, path, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            try {
                // Önce Ely.by'den skin denemesi yapalım
                var cachedFace = File.Exists(elybyCache) ? GetSkinFace(elybyCache) : null;
                try {
                    if (cachedFace == null || (DateTime.Now - File.GetLastWriteTime(elybyCache)).TotalDays > 1) {
                        var jsonStr = await client.GetStringAsync($"https://skinsystem.ely.by/textures/{Uri.EscapeDataString(username)}");
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                        var texUrl = SkinTextureUrl(jObj["SKIN"]?["url"]?.ToString());
                        if (!string.IsNullOrEmpty(texUrl)) {
                            var elyBytes = await client.GetByteArrayAsync(texUrl); SkinValidator.Validate(elyBytes);
                            await SaveCache(elybyCache, elyBytes);
                            return GetSkinFace(elybyCache);
                        }
                    }
                } catch { }
                if (cachedFace != null) return cachedFace;

                if (File.Exists(cache)) {
                    var cachedBytes = await File.ReadAllBytesAsync(cache);
                    try { return DecodeAvatar(cachedBytes); }
                    catch { File.Delete(cache); }
                }
                var bytes = await client.GetByteArrayAsync($"https://mc-heads.net/avatar/{Uri.EscapeDataString(username)}/{size}");
                var bmp = DecodeAvatar(bytes);
                await SaveCache(cache, bytes);
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
            SetVersion(ver);
            
            // Son güvenlik önlemi olarak modları senkronize et
            if(!SyncModsForCurrentVersion()) { MessageBox.Show(Localization.T("modSyncFailed"),"Mistik Launcher",MessageBoxButton.OK,MessageBoxImage.Warning); return; }

            _ = LaunchMinecraftAsync(ver);
        }

        bool gameRunning;
        async Task LaunchMinecraftAsync(string version)
        {
            if(gameRunning) return;
            var started=DateTime.UtcNow;
            var captured=new System.Text.StringBuilder();
            var outputGate=new object();
            BtnLaunch.IsEnabled = false;
            BtnLaunch.Content   = Localization.Language=="en"?"Launching…":"Başlatılıyor…";
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
                if (javaPath != null)
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
                    else if (javaVer < GameProfiles.RequiredJava(App.GameDir,version))
                    {
                        var res = MessageBox.Show(
                            $"Seçtiğiniz sürüm ({version}) için en az Java {GameProfiles.RequiredJava(App.GameDir,version)} gereklidir. Ancak bilgisayarınızda sadece Java {javaVer} bulundu.\n\nJava 21 otomatik olarak indirilip kurulsun mu?",
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

                if(javaPath==null) return;
                SetProgress(40);
                SetStatus("Oyun dosyalari dogrulaniyor...");
                var health = await VerifyAndRepairGameAsync(version, message => SetStatus(message));
                if (!health.CanLaunch)
                {
                    var detail = $"MLU-INTEGRITY: {health.FailedArtifact ?? "runtime"}\n{health.FailedPath}\n{health.FailureReason}";
                    CrashDiagnostics.Show(this, CrashDiagnostics.Report(null, started, detail));
                    return;
                }

                var versDir = Path.Combine(App.GameDir, "versions", version);

                // 3. Natives klasoru olustur
                var natives = Path.Combine(versDir, "natives");
                Directory.CreateDirectory(natives);

                SetStatus("Karakter (Skin) yaması uygulanıyor...");
                await PrepareSkinPackAsync(version);

                SetStatus("Oyun dili senkronize ediliyor...");
                EnsureGameLanguageMatchesLauncher();

                SetStatus("Görüş mesafesi optimize ediliyor...");
                EnsureChunkDistanceOptimized();

                SetStatus("Oyun dosyalari hazir...");

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
                var requestedRam = Math.Clamp(Config.Ram,1,32) * 1024;
                var ram = requestedRam;
                try
                {
                    ram = LaunchReadiness.ClampRamMb(Config.Ram,KernelOptimizer.GetTotalPhysicalMemory());
                    if(ram<requestedRam) App.Log($"[RAMOpt] RAM allocation reduced: {requestedRam}MB -> {ram}MB");
                }
                catch { }

                var args = BuildLaunchArgs(version, ram, natives, injectorPath, resolvedUuid);

                SetProgress(70);
                SetStatus("Minecraft baslatılıyor...");
                App.Log($"Launch: {javaPath} {args[..Math.Min(args.Length,120)]}...");

                var psi = new ProcessStartInfo(javaPath, args) {
                    WorkingDirectory=App.GameDir, UseShellExecute=false, CreateNoWindow=true,
                    RedirectStandardOutput=true, RedirectStandardError=true
                };
                var process=Process.Start(psi) ?? throw new InvalidOperationException("Java process could not start.");
                void Capture(object sender,DataReceivedEventArgs line) {
                    if(line.Data==null) return;
                    lock(outputGate) {
                        captured.AppendLine(line.Data.Length>4096?line.Data[..4096]:line.Data);
                        if(captured.Length>65536) captured.Remove(0,captured.Length-65536);
                    }
                }
                process.OutputDataReceived+=Capture; process.ErrorDataReceived+=Capture;
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                await Task.Delay(1500);
                if(process.HasExited) {
                    process.WaitForExit();
                    int earlyExit=process.ExitCode; process.Dispose();
                    string output; lock(outputGate) output=captured.ToString();
                    CrashDiagnostics.Show(this,CrashDiagnostics.Report(earlyExit,started,output));
                    return;
                }
                gameRunning=true;
                if(Config.KernelPriority || Config.KernelTimer || Config.KernelAffinity || Config.KernelPower || Config.KernelNagle || Config.KernelGpu)
                    KernelOptimizer.ApplyAll(process,Config);
                if(Config.AutoClose) Hide();
                _ = Task.Run(async ()=> {
                    try {
                        await process.WaitForExitAsync(); process.WaitForExit();
                        int exit=process.ExitCode;
                        string output; lock(outputGate) output=captured.ToString();
                        string? report=exit==0?null:CrashDiagnostics.Report(exit,started,output);
                        await Dispatcher.InvokeAsync(()=> {
                            if(report!=null) CrashDiagnostics.Show(this,report);
                            else if(Config.AutoClose) { Show(); WindowState=WindowState.Normal; Activate(); }
                        });
                    } catch(Exception monitorError) { App.Log("Game monitor: "+monitorError.Message); }
                    finally {
                        KernelOptimizer.RevertAll(); process.Dispose();
                        if(!Dispatcher.HasShutdownStarted) await Dispatcher.InvokeAsync(()=> {
                            gameRunning=false;
                            BtnLaunch.IsEnabled=true;
                            if(Config.AutoClose && !IsVisible) { Show(); WindowState=WindowState.Normal; Activate(); }
                        });
                    }
                });
                SetProgress(100);
                Relay?.UpdateStatus("Oyunda", version, "Minecraft");



            }
            catch (Exception ex)
            {
                App.Log($"Launch error: {ex.Message}");
                CrashDiagnostics.Show(this,CrashDiagnostics.Report(null,started,ex.Message));
            }
            finally
            {
                BtnLaunch.IsEnabled = !gameRunning;
                BtnLaunch.Content   = Localization.T("play");
                SetProgress(0);
                SetStatus($"{Localization.T("version")}: {version}");
            }
        }

        public bool EnsureMistikSkinPackEnabled(bool enable)
        {
            string? temporary = null;
            try
            {
                string optionsPath = Path.Combine(App.GameDir, "options.txt");
                if (!File.Exists(optionsPath) && !enable) return true;

                var lines = File.Exists(optionsPath) ? File.ReadAllLines(optionsPath) : Array.Empty<string>();
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

                Directory.CreateDirectory(App.GameDir);
                temporary = optionsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllLines(temporary, lines);
                if (File.Exists(optionsPath)) File.Replace(temporary, optionsPath, null);
                else File.Move(temporary, optionsPath);
                return true;
            }
            catch (Exception ex)
            {
                App.Log($"Error updating options.txt resourcePacks: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); } catch { }
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
                if (GameGraphicsOptions.EnsureStartupSettings(App.GameDir, Config.OptFps))
                    App.Log("[ChunkOpt] Startup graphics settings updated; Minecraft FPS limit preserved.");
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

        public T WithSyncedMods<T>(string expectedVersion, Func<string,T> write)
        {
            lock (_modSyncGate)
            {
                if (!string.Equals(Config.Version,expectedVersion,StringComparison.Ordinal) || !SyncModsForCurrentVersion(expectedVersion))
                    throw new IOException(Localization.T("modSyncFailed"));
                return write(App.ModsDir);
            }
        }

        public bool SyncModsForCurrentVersion(string? requestedVersion=null)
        {
            lock (_modSyncGate)
            {
                try
                {
                    var currentVer = requestedVersion ?? Config.Version ?? "";
                    if (string.IsNullOrEmpty(currentVer) || !string.Equals(Config.Version,currentVer,StringComparison.Ordinal)) return false;

                    string currentLoader = GameProfiles.Loader(App.GameDir,currentVer);
                    string mcVersion = GameProfiles.MinecraftVersion(App.GameDir,currentVer);
                    string currentPoolKey = GameProfiles.VersionPoolKey(mcVersion,currentLoader);

                    var previousSynced = Config.LastSyncedVersion ?? "";
                    var lastSynced = previousSynced;

                    // If nothing has changed, do not do anything
                    if (lastSynced == currentVer) return true;

                    var modsPoolDir = Path.Combine(App.AppData, "mods_pool");
                    Directory.CreateDirectory(modsPoolDir);

                    string lastLoader = GameProfiles.Loader(App.GameDir,lastSynced);
                    string lastPoolKey=GameProfiles.VersionPoolKey(GameProfiles.MinecraftVersion(App.GameDir,lastSynced),lastLoader);
                    // Never clear leftovers: a locked file or collision must preserve both pools and the active set.
                    ModFiles.SyncPools(App.ModsDir,Path.Combine(modsPoolDir,lastPoolKey),currentLoader=="vanilla"?null:Path.Combine(modsPoolDir,currentPoolKey),finalize:() => {
                        try
                        {
                            if (!string.Equals(Config.Version,currentVer,StringComparison.Ordinal)) throw new IOException(Localization.T("modSyncFailed"));
                            Config.LastSyncedVersion = currentVer;
                            ConfigManager.Save(Config);
                        }
                        catch
                        {
                            Config.LastSyncedVersion = previousSynced;
                            throw;
                        }
                    });

                    // Uyumsuz modları otomatik askıya al
                    SuspendIncompatibleMods(mcVersion, currentLoader);

                    App.Log($"Mods synchronized successfully for version: {currentVer} ({currentLoader})");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Log($"SyncModsForCurrentVersion error: {ex.Message}");
                    // This method can run on a worker thread. Marshal the small UI update
                    // back to WPF's dispatcher instead of freezing/crashing on cross-thread access.
                    Dispatcher.BeginInvoke(new Action(() => StatusLbl.Text=Localization.T("modSyncFailed")));
                    return false;
                }
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
                        bool loaderMismatch = !string.IsNullOrEmpty(modLoader) && modLoader!=currentLoader;

                        // Sürüm uyumsuzluğu kontrolü
                        bool versionMismatch = false;
                        if (modMcVersions != null && modMcVersions.Count > 0)
                        {
                            versionMismatch = !IsVersionCompatible(mcVersion, modMcVersions);
                        }
                        // ponytail: extracted version numbers lose operators and exclusive bounds; warn until a full constraint parser is needed.
                        if(versionMismatch && !loaderMismatch) { App.Log($"[ModGuard] Check version compatibility manually: {Path.GetFileName(jar)}"); continue; }

                        if (loaderMismatch || versionMismatch)
                        {
                            // Uyumsuz modu ilgili havuza taşı
                            string reason = loaderMismatch ? $"loader ({modLoader} != {currentLoader})" : $"sürüm ({string.Join(",", modMcVersions ?? new List<string>())} !~ {mcVersion})";
                            var safeVersion=Regex.Match(modMcVersions?.FirstOrDefault()??"",@"^\d+\.\d+(\.\d+)?$");
                            string poolKey = new[]{"fabric","quilt","forge","neoforge"}.Contains(modLoader) && safeVersion.Success
                                ? GameProfiles.VersionPoolKey(safeVersion.Value,modLoader!)
                                : "incompatible";
                            var poolDir = Path.Combine(modsPoolDir, poolKey);
                            Directory.CreateDirectory(poolDir);

                            var dest = Path.Combine(poolDir, Path.GetFileName(jar));
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

                // A jar declaring multiple loaders cannot safely be classified as one.
                if(zip.GetEntry("quilt.mod.json")!=null)
                    return (zip.GetEntry("fabric.mod.json")!=null || zip.GetEntry("META-INF/mods.toml")!=null || zip.GetEntry("META-INF/neoforge.mods.toml")!=null?null:"quilt",null);

                // 1. Fabric: fabric.mod.json
                var fabricEntry = zip.GetEntry("fabric.mod.json");
                if(fabricEntry!=null && (zip.GetEntry("META-INF/mods.toml")!=null || zip.GetEntry("META-INF/neoforge.mods.toml")!=null)) return (null,null);
                if (fabricEntry != null)
                {
                    if(fabricEntry.Length>262144) return (null,null);
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
                    if(neoforgeEntry.Length>262144) return (null,null);
                    loader = "neoforge";
                    using var reader = new StreamReader(neoforgeEntry.Open());
                    var toml = reader.ReadToEnd();
                    mcVersions = ExtractVersionsFromToml(toml);
                    return (loader, mcVersions);
                }

                if (forgeEntry != null)
                {
                    if(forgeEntry.Length>262144) return (null,null);
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

        public async Task<bool> PrepareSkinPackAsync(string version, HttpClient? client = null)
        {
            int generation = ++_skinPackGeneration;
            var selection = SkinSelection();
            var selectedVersion = Config.Version;
            bool Current() => generation == _skinPackGeneration && selection == SkinSelection() && selectedVersion == Config.Version;
            client ??= _skinHttp;
            try
            {
                var packDir = Path.Combine(App.GameDir, "resourcepacks", "MistikSkinPack");
                int format = GetPackFormatForVersion(version);

                if (selection.Type == "username")
                {
                    var user = !string.IsNullOrEmpty(selection.Skin) ? selection.Skin : selection.User;
                    if (!Regex.IsMatch(user ?? "", @"^[A-Za-z0-9_]{3,16}$")) return false;
                    if (string.IsNullOrEmpty(user) || user == "Oyuncu")
                    {
                        return EnsureMistikSkinPackEnabled(false);
                    }

                    byte[]? skinBytes = null;
                    try {
                        var jsonStr = await client.GetStringAsync($"https://skinsystem.ely.by/textures/{Uri.EscapeDataString(user)}");
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                        var texUrl = SkinTextureUrl(jObj["SKIN"]?["url"]?.ToString());
                        if (!string.IsNullOrEmpty(texUrl)) {
                            var downloaded = await client.GetByteArrayAsync(texUrl);
                            SkinValidator.Validate(downloaded);
                            skinBytes = downloaded;
                        }
                    } catch { }

                    if (!Current()) return false;
                    if (skinBytes == null) {
                        try {
                            using var response = await client.GetAsync($"https://mc-heads.net/skin/{Uri.EscapeDataString(user)}");
                            if (response.IsSuccessStatusCode) {
                                skinBytes = await response.Content.ReadAsByteArrayAsync();
                            }
                        } catch { }
                    }

                    if (skinBytes != null)
                    {
                        SkinValidator.Validate(skinBytes);
                        if (!Current()) return false;
                        CommitSkinPack(packDir, skinBytes, format);
                        // Clear old previews only after a replacement skin has been committed.
                        try
                        {
                            foreach (var cache in new[] { $"elyby_{user}.png", $"avatar_{user}_40.png", $"avatar_{user}_64.png" })
                            {
                                var path = Path.Combine(App.AppData, cache);
                                if (File.Exists(path)) File.Delete(path);
                            }
                        }
                        catch { }
                        if (!EnsureMistikSkinPackEnabled(true)) return false;
                        App.Log($"Skin for '{user}' successfully downloaded and applied with pack_format {format}.");
                        return true;
                    }
                    else
                    {
                        App.Log($"Failed to download skin for '{user}'. Keeping the existing custom skin pack.");
                        return false;
                    }
                }
                else if (selection.Type == "local")
                {
                    var filePath = selection.Skin;
                    if (File.Exists(filePath))
                    {
                        var skinBytes = await File.ReadAllBytesAsync(filePath);
                        SkinValidator.Validate(skinBytes);
                        if (!Current()) return false;
                        CommitSkinPack(packDir, skinBytes, format);

                        if (!EnsureMistikSkinPackEnabled(true)) return false;
                        App.Log($"Local skin applied successfully from: {filePath} with pack_format {format}.");
                        return true;
                    }
                    else
                    {
                        App.Log($"Local skin file does not exist: {filePath}");
                        return false;
                    }
                }
                else
                {
                    if (!EnsureMistikSkinPackEnabled(false)) return false;
                    if (Directory.Exists(packDir))
                    {
                        try { Directory.Delete(packDir, true); } catch { }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                App.Log($"PrepareSkinPackAsync error: {ex.Message}");
                return false;
            }
        }

        static void CommitSkinPack(string packDir, byte[] skinBytes, int format)
        {
            string stage = packDir + ".stage-" + Guid.NewGuid().ToString("N");
            string backup = packDir + ".backup-" + Guid.NewGuid().ToString("N");
            bool committed = false;
            try
            {
                foreach (var relative in new[] { "steve.png", "alex.png", "player/wide/steve.png", "player/slim/alex.png" })
                {
                    var path = Path.Combine(stage, "assets", "minecraft", "textures", "entity", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, skinBytes);
                }
                File.WriteAllText(Path.Combine(stage, "pack.mcmeta"), "{\"pack\":{\"pack_format\":" + format + ",\"description\":\"Mistik Launcher Ozel Skin Kaynak Paketi\"}}");
                if (Directory.Exists(packDir)) Directory.Move(packDir, backup);
                try { Directory.Move(stage, packDir); committed = true; }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, packDir); throw; }
            }
            finally
            {
                try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
                // Keep the backup if a failed swap could not be rolled back.
                try { if (committed && Directory.Exists(backup)) Directory.Delete(backup, true); } catch { }
            }
        }

        string BuildLaunchArgs(string version, int ramMb, string natives, string? injectorPath = null, string? resolvedUuid = null)
        {
            var libs    = BuildClasspath(version);
            // ★ PERF FIX: Xms = Xmx → G1GC heap resize yok, daha az GC pause
            var jvm     = $"-Xmx{ramMb}m -Xms{ramMb}m";
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
            var profile=GameProfiles.Read(App.GameDir,version);
            if(profile!=null && GameProfiles.Kind(profile) is "Forge" or "NeoForge")
            {
                // Forge provides its own JVM requirements; speculative tuning can break bootstrap.
                optArgs="";
                var values=new Dictionary<string,string> {
                    ["library_directory"]=Path.Combine(App.GameDir,"libraries"), ["classpath_separator"]=";",
                    ["classpath"]=libs,["natives_directory"]=natives,["version_name"]=version,
                    ["launcher_name"]="MistikLauncher",["launcher_version"]=LauncherUpdates.CurrentVersion,
                    ["auth_player_name"]=Config.User,["auth_uuid"]=launchUuid,["auth_access_token"]="0",
                    ["game_directory"]=App.GameDir,["assets_root"]=Path.Combine(App.GameDir,"assets"),["assets_index_name"]=assetIndex,
                    ["user_type"]="legacy",["version_type"]="release",["user_properties"]="{}"
                };
                string Expand(string arg) {
                    foreach(var pair in values) arg=arg.Replace("${"+pair.Key+"}",pair.Value);
                    if(arg.Contains("${")) throw new InvalidDataException(Localization.T("forgeInvalid"));
                    return "\""+arg.Replace("\"","\\\"")+"\"";
                }
                var forgeJvm=string.Join(" ",GameProfiles.Arguments(profile["arguments"]?["jvm"]).Select(Expand));
                var forgeGame=string.Join(" ",GameProfiles.Arguments(profile["arguments"]?["game"]).Select(Expand));
                // Legacy Forge embeds its complete game arguments, including FMLTweaker.
                if(profile["minecraftArguments"]!=null)
                {
                    var legacy=profile["minecraftArguments"]!.ToString();
                    foreach(var pair in values) legacy=legacy.Replace("${"+pair.Key+"}",pair.Value);
                    return $"{jvm} {optArgs} -Djava.library.path=\"{natives}\" -cp \"{libs}\" {forgeJvm} {mainClass} {legacy}";
                }
                return $"{jvm} {optArgs} -Djava.library.path=\"{natives}\" -cp \"{libs}\" {forgeJvm} {mainClass} " +
                    $"--username \"{Config.User}\" --version \"{version}\" --gameDir \"{App.GameDir}\" --assetsDir \"{Path.Combine(App.GameDir,"assets")}\" --assetIndex {assetIndex} --accessToken 0 --uuid {launchUuid} {forgeGame}";
            }

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
            AddLibrariesFromVersionJson(version, libs, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return string.Join(";", libs);
        }

        void AddLibrariesFromVersionJson(string version, List<string> libs, HashSet<string> seen)
        {
            if (!GameProfiles.SafeId(version) || seen.Count >= 16 || !seen.Add(version))
                throw new InvalidDataException("Invalid or cyclic game profile.");
            var json = GameProfiles.Read(App.GameDir, version) ?? throw new InvalidDataException("Invalid game profile: " + version);
            var jar = Path.Combine(App.GameDir, "versions", version, version + ".jar");
            if (File.Exists(jar) && !libs.Contains(jar)) libs.Add(jar);
            var parent = json["inheritsFrom"]?.ToString();
            if (!string.IsNullOrEmpty(parent)) AddLibrariesFromVersionJson(parent, libs, seen);
            if (json["libraries"] is not JArray entries) return;
            foreach (var library in entries.OfType<JObject>())
            {
                if (!GameRuntimeHealth.AppliesToWindows(library)) continue;
                string name = library["name"]?.ToString() ?? "";
                var artifact = library["downloads"]?["artifact"];
                if (artifact == null && library["downloads"]?["classifiers"] is JObject) continue;
                if (Regex.IsMatch(name, @":natives-windows(?:-[A-Za-z0-9_]+)?$")) continue;
                string? relative = artifact?["path"]?.ToString();
                if (string.IsNullOrWhiteSpace(relative) && !GameRuntimeHealth.TryMavenArtifact(name, out relative))
                    throw new InvalidDataException("Invalid library coordinate: " + name);
                if (!GameRuntimeHealth.TryChildPath(Path.Combine(App.GameDir, "libraries"), relative!, out var local))
                    throw new InvalidDataException("Invalid library path: " + relative);
                if (File.Exists(local) && !libs.Contains(local)) libs.Add(local);
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

        internal static async Task<string?> FindJavaAsync()
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

        internal static int GetJavaMajorVersion(string path)
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
            return !string.IsNullOrEmpty(version) && GameProfiles.RequiredJava(App.GameDir,version)>=25;
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

        void SetStatus(string s) => Dispatcher.Invoke(() => StatusLbl.Text = Localization.T(s));

        // ── Relay ─────────────────────────────────────────────────────────────
        async Task StartRelayAsync()
        {
            try {
                Relay = new MistikRelay(Config.User);
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
            lock (_modSyncGate)
            {
                try { Config = ConfigManager.Load(); } catch { Config ??= new LauncherConfig(); }
                Config.Version ??= "1.21";
            }

            // Null-safe Config fields
            Config.User       ??= "Oyuncu";
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

            if (_pageCache.TryGetValue("Dash", out var homePage) && homePage is Pages.ModernHomePage home) { home.RefreshLanguage(); home.InvalidateReadiness(); }

            // Settings sayfasının cache'ini temizle ki yeni config ile yeniden oluşturulsun
            try { InvalidatePageCache("Settings"); }
            catch { }
        }
    }
}
