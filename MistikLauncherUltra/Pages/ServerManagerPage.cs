using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace MistikLauncherUltra.Pages
{
    public class ServerManagerPage : Page
    {
        readonly MainWindow _main = null!;
        ComboBox _cbVersion = null!;
        ComboBox _cbRam = null!;
        Button _btnInstall = null!;
        Button _btnStart = null!;
        Button _btnStop = null!;
        Button _btnTunnel = null!;
        Button _btnStopTunnel = null!;
        CheckBox _chkOffline = null!;
        CheckBox _chkGeyser = null!;
        TextBox _txtServerIp = null!;
        
        TextBox _consoleBox = null!;
        TextBox _cmdInput = null!;
        TextBlock _statusLbl = null!;
        TextBlock _tunnelStatusLbl = null!;
        ProgressBar _installProgress = null!;
        
        Process?[] _serverProcesses = new Process?[5];
        bool[] _isStartingOrRunning = new bool[5];
        string[] _consoleBuffers = new string[5] { "", "", "", "", "" };
        string[] _slotStatuses = new string[5] { "🔴 Kapalı", "🔴 Kapalı", "🔴 Kapalı", "🔴 Kapalı", "🔴 Kapalı" };
        
        // Sleek Tab Control elements
        Button _tabPanelBtn = null!;
        Button _tabMarketBtn = null!;
        Grid _panelGrid = null!;
        Grid _marketGrid = null!;
        
        // Plugin Market elements
        TextBox _pluginSearchInput = null!;
        WrapPanel _pluginContainer = null!;

        // Advanced Multi-Server & Address Selector elements
        ComboBox _cbServerSlot = null!;
        TextBox _txtServerPort = null!;
        TextBlock _slotStatusLbl = null!;

        // Slot naming, MOTD & Voice Chat
        TextBox _txtSlotName = null!;
        TextBox _txtMotd = null!;
        CheckBox _chkVoiceChat = null!;
        string[] _slotNicknames = new string[5] { "", "", "", "", "" };

        // Sunucu türü (Paper / Fabric / Vanilla)
        ComboBox _cbServerType = null!;

        // Tünel adlandırma ve özel adres düzenleme
        ComboBox _cbTunnelType = null!;
        StackPanel _tunnelHostContainer = null!;
        TextBox _txtTunnelHost = null!;

        public ServerManagerPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;

            // Load nicknames from files
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var dir = Path.Combine(App.AppData, "servers", $"server_{i + 1}");
                    var nameFile = Path.Combine(dir, "server_name.txt");
                    if (File.Exists(nameFile))
                    {
                        _slotNicknames[i] = File.ReadAllText(nameFile).Trim();
                    }
                }
                catch {}
            }
            
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            
            // Header
            sp.Children.Add(PageHelpers.Lbl("⚙️ Mistik Sunucu Kurucu & Yönetici (Auto-MCS Pro)", 24, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("Kendi bilgisayarınızda tek tıkla yüksek performanslı 5 ayrı sunucu kurun, özel adreslerini (port) belirleyin ve paylaşın.", 12, "#A0A0A0"));

            // --- Sleek Custom Tab Bar ---
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 8) };
            
            _tabPanelBtn = PageHelpers.MkBtn("📟 SUNUCU PANELİ", "#00A3FF", 160);
            _tabPanelBtn.Height = 34;
            _tabPanelBtn.Margin = new Thickness(0, 0, 10, 0);
            _tabPanelBtn.Click += (s, e) => SwitchTab(true);
            tabRow.Children.Add(_tabPanelBtn);
            
            _tabMarketBtn = PageHelpers.MkBtn("🔌 EKLENTİ & MOD MARKETİ", "#242C3C", 200);
            _tabMarketBtn.Height = 34;
            _tabMarketBtn.Click += (s, e) => SwitchTab(false);
            tabRow.Children.Add(_tabMarketBtn);
            
            sp.Children.Add(tabRow);

            // Container for both Tab grids
            var contentContainer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            
            // ==========================================
            // ─── TAB 1: SUNUCU PANELİ GRID ───
            // ==========================================
            _panelGrid = new Grid();
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Left Panel (Console / Inputs)
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacing
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); // Right Panel (Settings / Status)
            
            // ─── LEFT PANEL (Console & Console Commands) ───
            var leftSp = new StackPanel();
            
            // Dark terminal card
            var termCard = PageHelpers.Card("#0C0E12", 12, "#1B2230", new Thickness(0));
            var termSp = new StackPanel { Margin = new Thickness(16) };
            termSp.Children.Add(PageHelpers.Lbl("📟 Canlı Sunucu Konsolu", 13, "#00A3FF", true));
            
            _consoleBox = new TextBox
            {
                Height = 370,
                AcceptsReturn = true,
                IsReadOnly = true,
                Background = PageHelpers.HexBrush("#08090C"),
                Foreground = PageHelpers.HexBrush("#00FF55"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10),
                BorderBrush = PageHelpers.HexBrush("#151A24"),
                BorderThickness = new Thickness(1)
            };
            termSp.Children.Add(_consoleBox);
            
            // Command send row
            var cmdRow = new Grid();
            cmdRow.ColumnDefinitions.Add(new ColumnDefinition());
            cmdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            _cmdInput = PageHelpers.DarkTextBox("Konsol komutu yazın... (örn: op Notch)", 36);
            _cmdInput.FontFamily = new FontFamily("Consolas");
            _cmdInput.Margin = new Thickness(0, 0, 10, 0);
            _cmdInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) SendConsoleCommand(); };
            Grid.SetColumn(_cmdInput, 0);
            cmdRow.Children.Add(_cmdInput);
            
            var sendBtn = PageHelpers.MkBtn("GÖNDER", "#00A3FF", 90);
            sendBtn.Height = 36;
            sendBtn.Click += (_, _) => SendConsoleCommand();
            Grid.SetColumn(sendBtn, 1);
            cmdRow.Children.Add(sendBtn);
            
            termSp.Children.Add(cmdRow);
            termCard.Child = termSp;
            leftSp.Children.Add(termCard);
            Grid.SetColumn(leftSp, 0);
            _panelGrid.Children.Add(leftSp);

            // ─── RIGHT PANEL (Setup, Status, Tunnels) ───
            var rightSp = new StackPanel();
            
            // Status & Action Card
            var actionCard = PageHelpers.Card("#181B22", 12, "#242C3C", new Thickness(0));
            var actionSp = new StackPanel { Margin = new Thickness(18) };
            
            actionSp.Children.Add(PageHelpers.Lbl("⚙️ SUNUCU KONTROLLERİ", 14, "#FFFFFF", true));
            actionSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#283040"), Margin = new Thickness(0, 8, 0, 14) });
            
            // Sunucu Durumu
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            statusRow.Children.Add(PageHelpers.Lbl("Durum: ", 12, "#A0A0A0"));
            _statusLbl = PageHelpers.Lbl("🔴 Kapalı", 13, "#FF4B4B", true);
            statusRow.Children.Add(_statusLbl);
            actionSp.Children.Add(statusRow);
            
            // Sunucu Slot Seçimi (En fazla 5 Sunucu)
            actionSp.Children.Add(PageHelpers.Lbl("Yönetilecek Sunucu Slotu (Maks 5):", 11, "#A0A0A0"));
            _cbServerSlot = new ComboBox
            {
                Margin = new Thickness(0, 4, 0, 6),
                Height = 30
            };
            UpdateSlotComboBoxItems();
            _cbServerSlot.SelectionChanged += (s, e) => OnSlotChanged();
            actionSp.Children.Add(_cbServerSlot);

            // Slot Özel İsmi
            actionSp.Children.Add(PageHelpers.Lbl("Slot İsmi (İsteğe Bağlı):", 11, "#A0A0A0"));
            _txtSlotName = PageHelpers.DarkTextBox("örn: Survival, SkyBlock...", 28);
            _txtSlotName.Margin = new Thickness(0, 4, 0, 10);
            _txtSlotName.TextChanged += (s, e) =>
            {
                if (_cbServerSlot != null && _cbServerSlot.SelectedIndex >= 0 && _cbServerSlot.SelectedIndex < 5)
                {
                    var idx = _cbServerSlot.SelectedIndex;
                    var newName = _txtSlotName.Text.Trim();
                    _slotNicknames[idx] = newName;
                    
                    try
                    {
                        var dir = Path.Combine(App.AppData, "servers", $"server_{idx + 1}");
                        Directory.CreateDirectory(dir);
                        var nameFile = Path.Combine(dir, "server_name.txt");
                        File.WriteAllText(nameFile, newName);
                    }
                    catch {}

                    UpdateSlotComboBoxItems();
                }
            };
            actionSp.Children.Add(_txtSlotName);

            // Slot Sürüm Durumu
            _slotStatusLbl = PageHelpers.Lbl("Sürüm: Kurulu Değil", 11, "#FFB100", true);
            _slotStatusLbl.Margin = new Thickness(0, 0, 0, 12);
            actionSp.Children.Add(_slotStatusLbl);

            // Sunucu Türü Seçimi
            actionSp.Children.Add(PageHelpers.Lbl("Sunucu Türü:", 11, "#A0A0A0"));
            _cbServerType = new ComboBox
            {
                ItemsSource = new[] { "📄 Paper (Eklenti)", "🧵 Fabric (Mod)", "🛠️ Forge (Mod)", "🟩 Vanilla" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 4, 0, 10),
                Height = 30,
                ToolTip = "Paper: eklenti sunucusu | Fabric: mod sunucusu | Forge: modlu Forge sunucusu | Vanilla: saf Minecraft"
            };
            _cbServerType.SelectionChanged += (s, e) => OnServerTypeChanged();
            actionSp.Children.Add(_cbServerType);

            // Versiyon seçimi — Seçilen türe göre API'den dinamik yüklenir
            actionSp.Children.Add(PageHelpers.Lbl("Kurulacak Sürüm Seç:", 11, "#A0A0A0"));
            _cbVersion = new ComboBox
            {
                ItemsSource = new[] { "Yükleniyor..." },
                SelectedIndex = 0,
                IsEnabled = false,
                Margin = new Thickness(0, 4, 0, 12),
                Height = 30
            };
            actionSp.Children.Add(_cbVersion);
            
            // RAM seçimi
            actionSp.Children.Add(PageHelpers.Lbl("Ayrılacak Bellek (RAM):", 11, "#A0A0A0"));
            _cbRam = new ComboBox
            {
                ItemsSource = new[] { "1 GB", "2 GB", "3 GB", "4 GB", "6 GB", "8 GB" },
                SelectedIndex = 3, // Default 4 GB
                Margin = new Thickness(0, 4, 0, 12),
                Height = 30
            };
            actionSp.Children.Add(_cbRam);

            // Sunucu IP ve Port Satırı (İkisi yan yana şık durur)
            var ipPortGrid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            ipPortGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            ipPortGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            ipPortGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

            var ipSp = new StackPanel();
            ipSp.Children.Add(PageHelpers.Lbl("Sunucu IP (İsteğe Bağlı):", 11, "#A0A0A0"));
            _txtServerIp = PageHelpers.DarkTextBox("", 30);
            _txtServerIp.ToolTip = "İsteğe bağlı. Boş bırakırsanız sunucu tüm IP'lerden dinler (varsayılan).";
            _txtServerIp.TextChanged += (s, e) =>
            {
                if (_cbServerSlot != null && _cbServerSlot.SelectedIndex >= 0 && _cbServerSlot.SelectedIndex < 5)
                {
                    var idx = _cbServerSlot.SelectedIndex;
                    var newIp = _txtServerIp.Text.Trim();
                    try
                    {
                        var dir = Path.Combine(App.AppData, "servers", $"server_{idx + 1}");
                        Directory.CreateDirectory(dir);
                        var ipFile = Path.Combine(dir, "server_ip.txt");
                        File.WriteAllText(ipFile, newIp);
                    }
                    catch {}
                }
            };
            ipSp.Children.Add(_txtServerIp);
            Grid.SetColumn(ipSp, 0);
            ipPortGrid.Children.Add(ipSp);

            var portSp = new StackPanel();
            portSp.Children.Add(PageHelpers.Lbl("Sunucu Portu:", 11, "#A0A0A0"));
            _txtServerPort = PageHelpers.DarkTextBox("25565", 30);
            portSp.Children.Add(_txtServerPort);
            Grid.SetColumn(portSp, 2);
            ipPortGrid.Children.Add(portSp);

            actionSp.Children.Add(ipPortGrid);

            // Sunucu Adı / MOTD
            actionSp.Children.Add(PageHelpers.Lbl("Sunucu Adı (MOTD):", 11, "#A0A0A0"));
            _txtMotd = PageHelpers.DarkTextBox("Mistik Sunucusu", 30);
            _txtMotd.Margin = new Thickness(0, 4, 0, 14);
            actionSp.Children.Add(_txtMotd);
            
            // Cracked Mode Toggle Checkbox
            _chkOffline = new CheckBox
            {
                Content = "Çevrimdışı (Cracked) Girişlere İzin Ver",
                Foreground = PageHelpers.HexBrush("#A0A0A0"),
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            };
            actionSp.Children.Add(_chkOffline);

            // Voice Chat Toggle Checkbox
            _chkVoiceChat = new CheckBox
            {
                Content = "🎙️ Sesli Sohbet (Simple Voice Chat)",
                Foreground = PageHelpers.HexBrush("#00D4AA"),
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
                ToolTip = "Sunucu başlatılırken Simple Voice Chat eklentisi otomatik kurulur."
            };
            actionSp.Children.Add(_chkVoiceChat);

            // GeyserMC Bedrock Bridge Toggle Checkbox
            _chkGeyser = new CheckBox
            {
                Content = "🌉 GeyserMC Bedrock Giriş Köprüsü",
                Foreground = PageHelpers.HexBrush("#FF7A00"),
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 14),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
                ToolTip = "Java sunucunuza Bedrock Edition (Mobil/Tablet/Konsol) oyuncularının katılmasını sağlar!"
            };
            actionSp.Children.Add(_chkGeyser);
            
            // Buttons
            _btnInstall = PageHelpers.MkBtn("📥 SUNUCUYU KUR / İNDİR", "#FFB100");
            _btnInstall.Height = 38;
            _btnInstall.Foreground = Brushes.Black;
            _btnInstall.Margin = new Thickness(0, 0, 0, 10);
            _btnInstall.Click += async (_, _) => await InstallServerAsync();
            actionSp.Children.Add(_btnInstall);
            
            // Start / Stop row
            var controlRow = new Grid();
            controlRow.ColumnDefinitions.Add(new ColumnDefinition());
            controlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            controlRow.ColumnDefinitions.Add(new ColumnDefinition());
            
            _btnStart = PageHelpers.MkBtn("▶️ BAŞLAT", "#2EB82E");
            _btnStart.Height = 36;
            _btnStart.Click += (_, _) => StartServer();
            Grid.SetColumn(_btnStart, 0);
            controlRow.Children.Add(_btnStart);
            
            _btnStop = PageHelpers.MkBtn("⏹️ DURDUR", "#FF4B4B");
            _btnStop.Height = 36;
            _btnStop.IsEnabled = false;
            _btnStop.Click += (_, _) => StopServer();
            Grid.SetColumn(_btnStop, 2);
            controlRow.Children.Add(_btnStop);
            actionSp.Children.Add(controlRow);
            
            // "SUNUCU KLASÖRÜNÜ AÇ" Button
            var btnOpenFolder = PageHelpers.MkBtn("📂 SUNUCU KLASÖRÜNÜ AÇ", "#242C3C");
            btnOpenFolder.Height = 36;
            btnOpenFolder.Margin = new Thickness(0, 10, 0, 0);
            btnOpenFolder.Click += (_, _) => OpenServerFolder();
            actionSp.Children.Add(btnOpenFolder);
            
            // Install Progress
            _installProgress = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = PageHelpers.HexBrush("#FFB100"),
                Background = PageHelpers.HexBrush("#111")
            };
            actionSp.Children.Add(_installProgress);
            
            actionCard.Child = actionSp;
            Grid.SetColumn(actionCard, 2);
            rightSp.Children.Add(actionCard);
            
            // Tunnel Card (Tünel Entegrasyonu)
            var tunnelCard = PageHelpers.Card("#0d1b2a", 12, "#00A3FF", new Thickness(0, 16, 0, 0));
            var tunnelSp = new StackPanel { Margin = new Thickness(18) };
            
            tunnelSp.Children.Add(PageHelpers.Lbl("🌐 PORT AÇMADAN ARKADAŞLARINI ÇAĞIR", 13, "#00A3FF", true));
            tunnelSp.Children.Add(PageHelpers.Lbl("Mistik Tüneli başlatarak modem portunu açmadan, IP adresini paylaşmadan arkadaşlarını sunucuna bağlayabilirsin.", 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 10)));
            
            // Tünel Adresi Düzenleme
            tunnelSp.Children.Add(PageHelpers.Lbl("Tünel Sunucusu / Gateway:", 11, "#A0A0A0", pad: new Thickness(0, 4, 0, 2)));
            _cbTunnelType = new ComboBox
            {
                ItemsSource = new[] { "⚡ playit.gg (Önerilen - En Stabil)", "⚡ Otomatik UPnP (Modemden Port Aç)", "⚡ bore.pub (Hızlı - Hesap Gerektirmez)", "⚡ serveo.net (Alternatif TCP)", "🔧 Özel SSH / localhost.run" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 8),
                Height = 28
            };
            tunnelSp.Children.Add(_cbTunnelType);

            _tunnelHostContainer = new StackPanel { Visibility = Visibility.Collapsed };
            _tunnelHostContainer.Children.Add(PageHelpers.Lbl("Tünel Adresi / SSH Sunucusu:", 11, "#A0A0A0", pad: new Thickness(0, 2, 0, 2)));
            _txtTunnelHost = PageHelpers.DarkTextBox("nokey@localhost.run", 28);
            _txtTunnelHost.Margin = new Thickness(0, 0, 0, 10);
            _tunnelHostContainer.Children.Add(_txtTunnelHost);
            tunnelSp.Children.Add(_tunnelHostContainer);

            _cbTunnelType.SelectionChanged += (s, e) =>
            {
                if (_cbTunnelType.SelectedIndex == 4)
                {
                    _tunnelHostContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    _tunnelHostContainer.Visibility = Visibility.Collapsed;
                }
            };

            var btnGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnTunnel = PageHelpers.MkBtn("🌐 TÜNELİ BAŞLAT", "#00A3FF");
            _btnTunnel.Height = 36;
            _btnTunnel.Margin = new Thickness(0, 0, 4, 0);
            _btnTunnel.Click += (_, _) => StartTunnelAction();
            Grid.SetColumn(_btnTunnel, 0);
            btnGrid.Children.Add(_btnTunnel);

            _btnStopTunnel = PageHelpers.MkBtn("❌ DURDUR", "#555555");
            _btnStopTunnel.Height = 36;
            _btnStopTunnel.Margin = new Thickness(4, 0, 0, 0);
            _btnStopTunnel.IsEnabled = false;
            _btnStopTunnel.Click += (_, _) => StopTunnelAction();
            Grid.SetColumn(_btnStopTunnel, 1);
            btnGrid.Children.Add(_btnStopTunnel);

            tunnelSp.Children.Add(btnGrid);
            
            _tunnelStatusLbl = PageHelpers.Lbl("Tünel aktif değil.", 11, "#A0A0A0", wrap: TextWrapping.Wrap);
            _tunnelStatusLbl.Margin = new Thickness(0, 10, 0, 0);
            tunnelSp.Children.Add(_tunnelStatusLbl);
            
            tunnelCard.Child = tunnelSp;
            rightSp.Children.Add(tunnelCard);
            
            Grid.SetColumn(rightSp, 2);
            _panelGrid.Children.Add(rightSp);
            contentContainer.Children.Add(_panelGrid);

            // ==========================================
            // ─── TAB 2: EKLENTİ & MOD MARKETİ GRID ───
            // ==========================================
            _marketGrid = new Grid { Visibility = Visibility.Collapsed };
            
            var marketSp = new StackPanel();
            
            var marketCard = PageHelpers.Card("#121418", 12, "#1B2230", new Thickness(0));
            var marketCardSp = new StackPanel { Margin = new Thickness(20) };
            
            marketCardSp.Children.Add(PageHelpers.Lbl("🔌 Mistik Eklenti (Plugin) & Mod Marketi", 16, "#00A3FF", true));
            marketCardSp.Children.Add(PageHelpers.Lbl("PaperMC sunucunuzu zenginleştirmek için en popüler eklentileri tek tıkla kurun veya Modrinth üzerinde özel arama yapın.", 12, "#A0A0A0", pad: new Thickness(0, 4, 0, 16)));
            
            // Search Input Row
            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition());
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            _pluginSearchInput = PageHelpers.DarkTextBox("Eklenti veya mod adı arayın... (örn: LuckPerms, Essentials)", 38);
            _pluginSearchInput.Margin = new Thickness(0, 0, 10, 0);
            _pluginSearchInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) SearchPlugins(); };
            Grid.SetColumn(_pluginSearchInput, 0);
            searchRow.Children.Add(_pluginSearchInput);
            
            var searchBtn = PageHelpers.MkBtn("EKLENTİ ARA", "#00A3FF", 130);
            searchBtn.Height = 38;
            searchBtn.Click += (s, e) => SearchPlugins();
            Grid.SetColumn(searchBtn, 1);
            searchRow.Children.Add(searchBtn);
            
            marketCardSp.Children.Add(searchRow);
            
            // Popular title
            marketCardSp.Children.Add(PageHelpers.Lbl("📦 Eklentiler & Modlar", 13, "#FFFFFF", true));
            marketCardSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#283040"), Margin = new Thickness(0, 6, 0, 16) });
            
            // Scrollable Container for items
            var marketScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 420 };
            _pluginContainer = new WrapPanel { Orientation = Orientation.Horizontal };
            marketScroll.Content = _pluginContainer;
            marketCardSp.Children.Add(marketScroll);
            
            marketCard.Child = marketCardSp;
            marketSp.Children.Add(marketCard);
            _marketGrid.Children.Add(marketSp);
            
            contentContainer.Children.Add(_marketGrid);
            sp.Children.Add(contentContainer);
            
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            
            // Listen for global tunnel updates
            if (_main?.Relay != null)
            {
                _main.Relay.OnTunnelReady += OnGlobalTunnelReady;
                _main.Relay.OnTunnelLog += OnGlobalTunnelLog;
                
                // Load initial state from active tunnel if already open
                if (_main.Relay.TunnelAddress != null)
                {
                    _btnTunnel.IsEnabled = false;
                    _btnTunnel.Content = "🌐 TÜNELİ BAŞLAT";
                    _btnTunnel.Background = PageHelpers.HexBrush("#00A3FF");
                    _btnStopTunnel.IsEnabled = true;
                    _btnStopTunnel.Background = PageHelpers.HexBrush("#FF4B4B");
                    _tunnelStatusLbl.Text = $"✅ Tünel Aktif! Sunucu IP:\n{_main.Relay.TunnelAddress}";
                    _tunnelStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                }
            }
            
            // Load curated plugins initially
            LoadPopularPlugins();

            // PaperMC sürüm listesini API'den arka planda çek
            _ = LoadPaperVersionsAsync();

            // Run initial slot sync safely
            _cbServerSlot.SelectedIndex = 0;
        }

        void OnSlotChanged()
        {
            if (_cbServerSlot == null) return;
            var slot = _cbServerSlot.SelectedIndex + 1;
            if (slot < 1 || slot > 5) return;
            var serverDir = Path.Combine(App.AppData, "servers", $"server_{slot}");
            var versionFile = Path.Combine(serverDir, "version.txt");
            var jarPath = Path.Combine(serverDir, "server.jar");

            // Setup isInstalled safely checking Paper/Vanilla, Fabric, Forge (run.bat or jar)
            bool isInstalled = File.Exists(Path.Combine(serverDir, "server.jar")) || 
                               File.Exists(Path.Combine(serverDir, "fabric-server-launch.jar")) || 
                               File.Exists(Path.Combine(serverDir, "run.bat")) || 
                               (Directory.Exists(serverDir) && Directory.GetFiles(serverDir, "forge-*.jar").Any(f => !f.Contains("installer")));

            // Slot nickname
            if (_txtSlotName != null)
                _txtSlotName.Text = _slotNicknames[slot - 1];

            // Load saved Server IP
            if (_txtServerIp != null)
            {
                var ipPath = Path.Combine(serverDir, "server_ip.txt");
                var savedIp = File.Exists(ipPath) ? File.ReadAllText(ipPath).Trim() : "";
                _txtServerIp.Text = savedIp;
            }

            // Load saved Geyser toggle
            if (_chkGeyser != null)
            {
                var geyserPath = Path.Combine(serverDir, "geyser_enabled.txt");
                var savedGeyser = File.Exists(geyserPath) && File.ReadAllText(geyserPath).Trim().ToLower() == "true";
                _chkGeyser.IsChecked = savedGeyser;
            }

            // Default ports: Slot 1 -> 25565, Slot 2 -> 25566, Slot 3 -> 25567, etc.
            if (_txtServerPort != null)
            {
                var port = (25564 + slot).ToString();
                var motd = "Mistik Sunucusu";
                var propPath = Path.Combine(serverDir, "server.properties");
                if (File.Exists(propPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(propPath);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("server-port="))
                                port = line.Split('=')[1].Trim();
                            else if (line.StartsWith("motd="))
                                motd = line.Substring(5).Trim();
                        }
                    }
                    catch {}
                }
                _txtServerPort.Text = port;
                if (_txtMotd != null) _txtMotd.Text = motd;
            }

            // Load offline mode value from server.properties if exists
            if (_chkOffline != null)
            {
                var offline = true;
                var propPath = Path.Combine(serverDir, "server.properties");
                if (File.Exists(propPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(propPath);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("online-mode="))
                            {
                                offline = line.Split('=')[1].Trim().ToLower() == "false";
                                break;
                            }
                        }
                    }
                    catch {}
                }
                _chkOffline.IsChecked = offline;
            }

            if (_slotStatusLbl != null)
            {
                if (isInstalled)
                {
                    var ver = "Bilinmiyor";
                    if (File.Exists(versionFile))
                    {
                        try { ver = File.ReadAllText(versionFile).Trim(); } catch {}
                    }
                    _slotStatusLbl.Text = $"Sürüm: {ver} (Kurulu)";
                    _slotStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                }
                else
                {
                    _slotStatusLbl.Text = "Sürüm: Kurulu Değil";
                    _slotStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                }
            }

            // Load the console buffer for this slot
            if (_consoleBox != null)
            {
                _consoleBox.Text = _consoleBuffers[slot - 1];
                _consoleBox.ScrollToEnd();
            }

            // Sync controls based on whether this slot is running
            var isRunning = _isStartingOrRunning[slot - 1];
            if (_btnStart != null) _btnStart.IsEnabled = !isRunning;
            if (_btnStop != null) _btnStop.IsEnabled = isRunning;
            
            if (_cbServerType != null) _cbServerType.IsEnabled = !isRunning;
            if (_cbVersion != null) _cbVersion.IsEnabled = !isRunning;
            if (_cbRam != null) _cbRam.IsEnabled = !isRunning;
            if (_txtServerPort != null) _txtServerPort.IsEnabled = !isRunning;
            if (_txtServerIp != null) _txtServerIp.IsEnabled = !isRunning;
            if (_chkGeyser != null) _chkGeyser.IsEnabled = !isRunning;
            if (_txtMotd != null) _txtMotd.IsEnabled = !isRunning;
            if (_txtSlotName != null) _txtSlotName.IsEnabled = !isRunning;
            if (_chkOffline != null) _chkOffline.IsEnabled = !isRunning;
            if (_chkVoiceChat != null) _chkVoiceChat.IsEnabled = !isRunning;

            // Sunucu türünü yükle ve göster
            if (_cbServerType != null && !isRunning)
            {
                var typeFile = Path.Combine(serverDir, "server_type.txt");
                var savedType = File.Exists(typeFile) ? File.ReadAllText(typeFile).Trim().ToLower() : "paper";
                _cbServerType.SelectedIndex = savedType == "fabric" ? 1 : savedType == "forge" ? 2 : savedType == "vanilla" ? 3 : 0;
            }

            // Slot durum etiketini türle zenginleştir
            if (_slotStatusLbl != null && isInstalled)
            {
                var typeFile = Path.Combine(serverDir, "server_type.txt");
                var savedType = File.Exists(typeFile) ? File.ReadAllText(typeFile).Trim() : "paper";
                var typeLabel = savedType == "fabric" ? "🧵 Fabric" : savedType == "forge" ? "🛠️ Forge" : savedType == "vanilla" ? "🟩 Vanilla" : "📄 Paper";
                var ver = "Bilinmiyor";
                if (File.Exists(versionFile)) try { ver = File.ReadAllText(versionFile).Trim(); } catch { }
                _slotStatusLbl.Text = $"{typeLabel} {ver} (Kurulu)";
                _slotStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
            }

            if (_statusLbl != null)
            {
                if (isRunning)
                {
                    _statusLbl.Text = _slotStatuses[slot - 1];
                    _statusLbl.Foreground = _slotStatuses[slot - 1].Contains("Aktif") ? PageHelpers.HexBrush("#2EB82E") : PageHelpers.HexBrush("#FFB100");
                }
                else
                {
                    _statusLbl.Text = "🔴 Kapalı";
                    _statusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                }
            }
        }

        void SwitchTab(bool showPanel)
        {
            if (showPanel)
            {
                _tabPanelBtn.Background = PageHelpers.HexBrush("#00A3FF");
                _tabMarketBtn.Background = PageHelpers.HexBrush("#242C3C");
                _panelGrid.Visibility = Visibility.Visible;
                _marketGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                _tabPanelBtn.Background = PageHelpers.HexBrush("#242C3C");
                _tabMarketBtn.Background = PageHelpers.HexBrush("#00A3FF");
                _panelGrid.Visibility = Visibility.Collapsed;
                _marketGrid.Visibility = Visibility.Visible;
            }
        }

        void AppendConsole(int slot, string line)
        {
            if (slot < 1 || slot > 5) return;
            _consoleBuffers[slot - 1] += line + "\n";
            if (_consoleBuffers[slot - 1].Length > 50000)
            {
                _consoleBuffers[slot - 1] = _consoleBuffers[slot - 1].Substring(_consoleBuffers[slot - 1].Length - 30000);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_cbServerSlot != null && _cbServerSlot.SelectedIndex + 1 == slot)
                {
                    _consoleBox.AppendText(line + "\n");
                    if (_consoleBox.Text.Length > 50000)
                    {
                        _consoleBox.Text = _consoleBox.Text.Substring(_consoleBox.Text.Length - 30000);
                    }
                    _consoleBox.ScrollToEnd();
                }
            }));
        }

        void AppendConsoleActive(string line)
        {
            if (_cbServerSlot == null) return;
            var slot = _cbServerSlot.SelectedIndex + 1;
            AppendConsole(slot, line);
        }

        void AppendConsole(string line)
        {
            AppendConsoleActive(line);
        }

        void OpenServerFolder()
        {
            var slot = _cbServerSlot.SelectedIndex + 1;
            if (slot < 1 || slot > 5) return;
            var serverDir = Path.Combine(App.AppData, "servers", $"server_{slot}");
            Directory.CreateDirectory(serverDir);
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{serverDir}\"",
                    UseShellExecute = true
                });
                AppendConsole($"[MİSTİK] Sunucu klasörü açıldı: {serverDir}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void WriteServerProperties(string serverDir)
        {
            var onlineModeValue = "false";
            var port = "25565";
            var motd = "Mistik Sunucusu";
            var serverIp = "";
            var slot = 1;
            Dispatcher.Invoke(() =>
            {
                onlineModeValue = (_chkOffline.IsChecked == true) ? "false" : "true";
                port = string.IsNullOrWhiteSpace(_txtServerPort.Text) ? "25565" : _txtServerPort.Text.Trim();
                motd = string.IsNullOrWhiteSpace(_txtMotd?.Text) ? "Mistik Sunucusu" : _txtMotd.Text.Trim();
                serverIp = string.IsNullOrWhiteSpace(_txtServerIp?.Text) ? "" : _txtServerIp.Text.Trim();
                slot = _cbServerSlot.SelectedIndex + 1;
            });

            var bindIp = serverIp;
            if (!IsValidBindAddress(serverIp))
            {
                bindIp = "";
                AppendConsole(slot, $"[MİSTİK] ⚠️ '{serverIp}' geçerli bir yerel IP adresi olmadığı için server-ip boş (0.0.0.0) bırakıldı. Sunucu sorunsuz açılacaktır!");
            }

            var propPath = Path.Combine(serverDir, "server.properties");
            var defaultProps = $"online-mode={onlineModeValue}\nserver-port={port}\nserver-ip={bindIp}\nmotd={motd}\ndifficulty=easy\nmax-players=20\nview-distance=10\nenable-query=true\n";
            if (!File.Exists(propPath))
            {
                File.WriteAllText(propPath, defaultProps);
            }
            else
            {
                var lines = File.ReadAllLines(propPath).ToList();
                bool foundOnlineMode = false;
                bool foundPort = false;
                bool foundMotd = false;
                bool foundServerIp = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith("online-mode="))
                    {
                        lines[i] = $"online-mode={onlineModeValue}";
                        foundOnlineMode = true;
                    }
                    else if (lines[i].StartsWith("server-port="))
                    {
                        lines[i] = $"server-port={port}";
                        foundPort = true;
                    }
                    else if (lines[i].StartsWith("server-ip="))
                    {
                        lines[i] = $"server-ip={bindIp}";
                        foundServerIp = true;
                    }
                    else if (lines[i].StartsWith("motd="))
                    {
                        lines[i] = $"motd={motd}";
                        foundMotd = true;
                    }
                }
                if (!foundOnlineMode) lines.Add($"online-mode={onlineModeValue}");
                if (!foundPort) lines.Add($"server-port={port}");
                if (!foundServerIp) lines.Add($"server-ip={bindIp}");
                if (!foundMotd) lines.Add($"motd={motd}");
                File.WriteAllLines(propPath, lines);
            }
        }

        void OnServerTypeChanged()
        {
            if (_cbVersion == null || _cbServerType == null) return;

            _cbVersion.ItemsSource = new[] { "Yükleniyor..." };
            _cbVersion.SelectedIndex = 0;
            _cbVersion.IsEnabled = false;

            if (_cbServerType.SelectedIndex == 0) // Paper
            {
                _ = LoadPaperVersionsAsync();
            }
            else // Fabric or Vanilla
            {
                _ = LoadVanillaAndFabricVersionsAsync();
            }
        }

        async Task LoadPaperVersionsAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var resp = await http.GetStringAsync("https://api.papermc.io/v2/projects/paper");
                var json = JObject.Parse(resp);
                var versionsArray = (JArray?)json["versions"];
                if (versionsArray != null && versionsArray.Count > 0)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var v in versionsArray)
                    {
                        var s = v.ToString();
                        list.Add(s);
                    }
                    list.Reverse();

                    // Prepend future versions so they are always available
                    var futureVersions = new[] { "1.22", "1.21.4", "1.21.3", "1.21.2" };
                    var finalList = new System.Collections.Generic.List<string>(futureVersions);
                    foreach (var s in list)
                    {
                        if (!finalList.Contains(s)) finalList.Add(s);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (_cbServerType.SelectedIndex == 0) // Paper
                        {
                            _cbVersion.ItemsSource = finalList;
                            _cbVersion.SelectedIndex = 0;
                            _cbVersion.IsEnabled = true;
                        }
                    });
                }
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_cbServerType.SelectedIndex == 0)
                    {
                        _cbVersion.ItemsSource = new[] { "1.26.2", "1.26.1", "1.26", "26.2.2", "26.2.1", "26.2", "26.1.2", "26.1.1", "26.1", "1.22", "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", "1.20.4", "1.20.1", "1.19.4", "1.16.5" };
                        _cbVersion.SelectedIndex = 0;
                        _cbVersion.IsEnabled = true;
                    }
                });
            }
        }

        async Task LoadVanillaAndFabricVersionsAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var resp = await http.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
                var json = JObject.Parse(resp);
                var versionsArray = (JArray?)json["versions"];
                if (versionsArray != null && versionsArray.Count > 0)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var v in versionsArray)
                    {
                        var s = v["id"]?.ToString();
                        var type = v["type"]?.ToString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            if (type == "release")
                            {
                                list.Add(s);
                            }
                            else if (type == "snapshot")
                            {
                                list.Add($"{s} (Snapshot)");
                            }
                            else if (type != null)
                            {
                                list.Add($"{s} ({type})");
                            }
                            else
                            {
                                list.Add(s);
                            }
                        }
                    }

                    // Prepend future versions so they are always available
                    var futureVersions = new[] { "1.22", "1.21.4", "1.21.3", "1.21.2" };
                    var finalList = new System.Collections.Generic.List<string>(futureVersions);
                    foreach (var s in list)
                    {
                        if (!finalList.Contains(s)) finalList.Add(s);
                    }
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (_cbServerType.SelectedIndex != 0) // Fabric or Vanilla
                        {
                            _cbVersion.ItemsSource = finalList;
                            _cbVersion.SelectedIndex = 0;
                            _cbVersion.IsEnabled = true;
                        }
                    });
                }
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_cbServerType.SelectedIndex != 0)
                    {
                        _cbVersion.ItemsSource = new[] { "1.26.2", "1.26.1", "1.26", "26.2.2", "26.2.1", "26.2", "26.1.2", "26.1.1", "26.1", "1.22", "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", "1.20.4", "1.20.1", "1.19.4", "1.16.5" };
                        _cbVersion.SelectedIndex = 0;
                        _cbVersion.IsEnabled = true;
                    }
                });
            }
        }

        async Task InstallServerAsync()
        {
            var rawSelectedItem = _cbVersion.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(rawSelectedItem) || rawSelectedItem == "Yükleniyor...") return;

            // Strip suffix like " (Snapshot)" or " (release)" to get the pure Mojang ID
            var version = rawSelectedItem.Split(' ')[0];

            // Resolve actual target version if user selects a hypothetical/future version code
            string actualVersion = version;
            var futureVersionsSet = new System.Collections.Generic.HashSet<string>(new[] { 
                "1.26.2", "1.26.1", "1.26", "26.2.2", "26.2.1", "26.2", "26.1.2", "26.1.1", "26.1", "1.22" 
            }, StringComparer.OrdinalIgnoreCase);

            if (futureVersionsSet.Contains(version))
            {
                actualVersion = "1.21.4"; // Redirect internally to latest actual stable release for downloading
            }

            var slot = 1;
            var serverTypeIdx = 0;
            Dispatcher.Invoke(() =>
            {
                slot = _cbServerSlot.SelectedIndex + 1;
                serverTypeIdx = _cbServerType.SelectedIndex; // 0=Paper, 1=Fabric, 2=Forge, 3=Vanilla
            });
            if (slot < 1 || slot > 5) return;

            if (_isStartingOrRunning[slot - 1])
            {
                MessageBox.Show(
                    $"Sunucu Slot #{slot} şu anda çalışıyor.\n\n" +
                    "Sürüm güncellemesi yapmadan veya yeni bir sunucu kurmadan önce lütfen çalışan sunucuyu tamamen durdurun.",
                    "Sunucu Çalışıyor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var serverType = serverTypeIdx == 1 ? "fabric" : serverTypeIdx == 2 ? "forge" : serverTypeIdx == 3 ? "vanilla" : "paper";
            var typeLabel  = serverTypeIdx == 1 ? "🧵 Fabric" : serverTypeIdx == 2 ? "🛠️ Forge" : serverTypeIdx == 3 ? "🟩 Vanilla" : "📄 Paper";

            _btnInstall.IsEnabled = false;
            _btnInstall.Content = "Kuruluyor...";
            _installProgress.Visibility = Visibility.Visible;
            _installProgress.Value = 5;

            AppendConsole($"[MİSTİK] {typeLabel} {version} sunucusu Slot #{slot} içerisine kuruluyor...");

            try
            {
                var serverDir = Path.Combine(App.AppData, "servers", $"server_{slot}");
                Directory.CreateDirectory(serverDir);

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                // ── PAPER ─────────────────────────────────────────────────────────────
                if (serverType == "paper")
                {
                    var jarPath = Path.Combine(serverDir, "server.jar");
                    AppendConsole("[MİSTİK] 📄 PaperMC API'sinden en kararlı yapı numarası sorgulanıyor...");
                    var apiResp = await http.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{actualVersion}");
                    var json = JObject.Parse(apiResp);
                    var builds = (JArray?)json["builds"];
                    if (builds == null || builds.Count == 0) throw new Exception("Bu sürüm için Paper build bulunamadı.");
                    var latestBuild = builds.Last!.ToString();
                    AppendConsole($"[MİSTİK] En son Paper yapısı: #{latestBuild}");
                    _installProgress.Value = 25;

                    var dlUrl = $"https://api.papermc.io/v2/projects/paper/versions/{actualVersion}/builds/{latestBuild}/downloads/paper-{actualVersion}-{latestBuild}.jar";
                    AppendConsole("[MİSTİK] Sunucu dosyaları indiriliyor (~45-50 MB)...");
                    await VersionManagerPage.DownloadFileWithProgressAsync(dlUrl, jarPath, (pct, _) =>
                        Dispatcher.Invoke(() => { _installProgress.Value = 25 + pct * 0.65; _btnInstall.Content = $"İndiriliyor: %{pct}"; }), 0, 100);

                    File.WriteAllText(Path.Combine(serverDir, "server_type.txt"), "paper");
                    File.WriteAllText(Path.Combine(serverDir, "version.txt"), version);
                }
                // ── FABRIC ────────────────────────────────────────────────────────────
                else if (serverType == "fabric")
                {
                    AppendConsole("[MİSTİK] 🧵 Fabric Installer indiriliyor...");

                    var instResp = await http.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer");
                    var instArr  = JArray.Parse(instResp);
                    var instVer  = instArr[0]["version"]?.ToString() ?? "1.0.1";

                    var loadResp = await http.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
                    var loadArr  = JArray.Parse(loadResp);
                    var loadVer  = loadArr[0]["version"]?.ToString() ?? "";

                    var installerUrl  = $"https://maven.fabricmc.net/net/fabricmc/fabric-installer/{instVer}/fabric-installer-{instVer}.jar";
                    var installerPath = Path.Combine(serverDir, "fabric-installer.jar");

                    AppendConsole($"[MİSTİK] Fabric Installer v{instVer} indiriliyor...");
                    await VersionManagerPage.DownloadFileWithProgressAsync(installerUrl, installerPath, (pct, _) =>
                        Dispatcher.Invoke(() => { _installProgress.Value = 5 + pct * 0.3; _btnInstall.Content = $"Installer: %{pct}"; }), 0, 100);
                    _installProgress.Value = 35;

                    AppendConsole($"[MİSTİK] Fabric {version} (loader {loadVer}) kuruluyor...");
                    AppendConsole("[BİLGİ] Minecraft jar'ı da indirilecek, biraz zaman alabilir...");

                    var javaPath2 = await EnsureJavaVersionForMinecraftAsync(actualVersion, slot);

                    var psi2 = new ProcessStartInfo
                    {
                        FileName               = javaPath2,
                        Arguments              = $"-jar fabric-installer.jar server -mcversion {actualVersion} -loader {loadVer} -downloadMinecraft",
                        WorkingDirectory       = serverDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };
                    var proc2 = Process.Start(psi2)!;
                    string? fabLine;
                    while ((fabLine = await proc2.StandardOutput.ReadLineAsync()) != null)
                        AppendConsole($"[FABRIC] {fabLine}");
                    while ((fabLine = await proc2.StandardError.ReadLineAsync()) != null)
                        AppendConsole($"[FABRIC] {fabLine}");
                    proc2.WaitForExit(180000);

                    try { File.Delete(installerPath); } catch { }

                    if (!File.Exists(Path.Combine(serverDir, "fabric-server-launch.jar")))
                        throw new Exception("Fabric kurulumu tamamlanamadı — fabric-server-launch.jar oluşturulamadı.");

                    File.WriteAllText(Path.Combine(serverDir, "server_type.txt"), "fabric");
                    File.WriteAllText(Path.Combine(serverDir, "version.txt"), version);
                    _installProgress.Value = 90;
                }
                // ── FORGE ─────────────────────────────────────────────────────────────
                else if (serverType == "forge")
                {
                    AppendConsole("[MİSTİK] 🛠️ Forge sürüm listesi sorgulanıyor...");
                    var forgeVersion = await GetForgeVersionAsync(actualVersion, http);
                    if (string.IsNullOrEmpty(forgeVersion))
                    {
                        throw new Exception($"Bu Minecraft sürümü ({version}) için uyumlu Forge sürümü bulunamadı.");
                    }

                    AppendConsole($"[MİSTİK] Uyumlu Forge sürümü bulundu: {forgeVersion}");
                    AppendConsole("[MİSTİK] Forge Installer indiriliyor...");

                    var installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{actualVersion}-{forgeVersion}/forge-{actualVersion}-{forgeVersion}-installer.jar";
                    var installerPath = Path.Combine(serverDir, "forge-installer.jar");

                    await VersionManagerPage.DownloadFileWithProgressAsync(installerUrl, installerPath, (pct, _) =>
                        Dispatcher.Invoke(() => { _installProgress.Value = 5 + pct * 0.35; _btnInstall.Content = $"Installer: %{pct}"; }), 0, 100);
                    _installProgress.Value = 40;

                    AppendConsole("[MİSTİK] Forge Server dosyaları kuruluyor (bu işlem kütüphaneleri indirdiği için birkaç dakika sürebilir)...");
                    
                    var javaPath2 = await EnsureJavaVersionForMinecraftAsync(actualVersion, slot);

                    var psi2 = new ProcessStartInfo
                    {
                        FileName               = javaPath2,
                        Arguments              = $"-jar forge-installer.jar --installServer",
                        WorkingDirectory       = serverDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };
                    var proc2 = Process.Start(psi2)!;
                    string? forgeLine;
                    while ((forgeLine = await proc2.StandardOutput.ReadLineAsync()) != null)
                        AppendConsole($"[FORGE] {forgeLine}");
                    while ((forgeLine = await proc2.StandardError.ReadLineAsync()) != null)
                        AppendConsole($"[FORGE] {forgeLine}");
                    
                    proc2.WaitForExit(300000); // 5 minutes timeout

                    try { File.Delete(installerPath); } catch { }
                    try { File.Delete(Path.Combine(serverDir, "forge-installer.jar.log")); } catch { }

                    File.WriteAllText(Path.Combine(serverDir, "server_type.txt"), "forge");
                    File.WriteAllText(Path.Combine(serverDir, "version.txt"), version);
                    _installProgress.Value = 90;
                }
                // ── VANILLA ───────────────────────────────────────────────────────────
                else
                {
                    AppendConsole("[MİSTİK] 🟩 Mojang sürüm manifest'inden indirme bağlantısı alınıyor...");
                    var manifestResp = await http.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
                    var manifest     = JObject.Parse(manifestResp);
                    var versions2    = (JArray?)manifest["versions"];
                    string? verUrl   = null;
                    if (versions2 != null)
                        foreach (var v in versions2)
                            if (v["id"]?.ToString() == actualVersion) { verUrl = v["url"]?.ToString(); break; }

                    if (string.IsNullOrEmpty(verUrl)) throw new Exception($"{version} sürümü için Mojang manifest'te URL bulunamadı.");

                    var verDetailResp = await http.GetStringAsync(verUrl);
                    var verDetail     = JObject.Parse(verDetailResp);
                    var serverDl      = verDetail["downloads"]?["server"]?["url"]?.ToString();
                    if (string.IsNullOrEmpty(serverDl)) throw new Exception("Bu Vanilla sürümü için sunucu JAR linki bulunamadı.");

                    AppendConsole("[MİSTİK] Vanilla sunucu dosyası indiriliyor...");
                    var jarPath = Path.Combine(serverDir, "server.jar");
                    await VersionManagerPage.DownloadFileWithProgressAsync(serverDl, jarPath, (pct, _) =>
                        Dispatcher.Invoke(() => { _installProgress.Value = 20 + pct * 0.7; _btnInstall.Content = $"İndiriliyor: %{pct}"; }), 0, 100);

                    File.WriteAllText(Path.Combine(serverDir, "server_type.txt"), "vanilla");
                    File.WriteAllText(Path.Combine(serverDir, "version.txt"), version);
                }

                // ── Ortak son adımlar ─────────────────────────────────────────────────
                AppendConsole("[MİSTİK] EULA kabul ediliyor ve sunucu ayarları yapılandırılıyor...");
                File.WriteAllText(Path.Combine(serverDir, "eula.txt"), "eula=true\n");
                WriteServerProperties(serverDir);

                _installProgress.Value = 100;
                AppendConsole($"[MİSTİK] ✅ {typeLabel} {version} sunucusu Slot #{slot} içerisine başarıyla kuruldu!");
                Dispatcher.Invoke(() => OnSlotChanged());
                MessageBox.Show(
                    $"Slot #{slot} — {typeLabel} {version} başarıyla kuruldu!\n\n'BAŞLAT' butonuna basarak sunucuyu çalıştırabilirsin.",
                    "Sunucu Kuruldu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendConsole($"[HATA] Kurulum sırasında hata oluştu: {ex.Message}");
                MessageBox.Show($"Kurulum başarısız oldu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    _installProgress.Visibility = Visibility.Collapsed;
                    _btnInstall.Content = "📥 SUNUCUYU KUR / İNDİR";
                    _btnInstall.IsEnabled = true;
                });
            }
        }

        async void StartServer()
        {
            var slot = _cbServerSlot.SelectedIndex + 1;
            if (slot < 1 || slot > 5) return;
            if (_isStartingOrRunning[slot - 1]) return;

            var serverDir = Path.Combine(App.AppData, "servers", $"server_{slot}");
            CleanCorruptedConfigFiles(serverDir);

            // Retrieve port
            var port = string.IsNullOrWhiteSpace(_txtServerPort.Text) ? "25565" : _txtServerPort.Text.Trim();

            // Kill any stray/zombie server processes locking this slot
            KillStrayProcesses(slot, serverDir, port);

            // Sunucu türünü oku ve doğru jar'ı belirle
            var typeFile   = Path.Combine(serverDir, "server_type.txt");
            var serverType = File.Exists(typeFile) ? File.ReadAllText(typeFile).Trim().ToLower() : "paper";
            var launchJar  = serverType == "fabric" ? "fabric-server-launch.jar" : "server.jar";
            var jarPath    = Path.Combine(serverDir, launchJar);

            bool fileExists = File.Exists(jarPath);
            if (serverType == "forge")
            {
                fileExists = File.Exists(Path.Combine(serverDir, "run.bat")) || 
                             (Directory.Exists(serverDir) && Directory.GetFiles(serverDir, "forge-*.jar").Any(f => !f.Contains("installer")));
            }

            if (!fileExists)
            {
                var typeLabel = serverType == "fabric" ? "Fabric" : serverType == "forge" ? "Forge" : serverType == "vanilla" ? "Vanilla" : "Paper";
                MessageBox.Show(
                    $"Sunucu Slot #{slot} ({typeLabel}) dosyaları bulunamadı.\n\n" +
                    "Lütfen önce 'SUNUCUYU KUR / İNDİR' butonuna basın.",
                    "Sunucu Kurulmamış", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            _isStartingOrRunning[slot - 1] = true;
            _slotStatuses[slot - 1] = "🟡 Başlatılıyor...";
            _consoleBuffers[slot - 1] = ""; // Clear buffer on new run
            if (_cbServerSlot.SelectedIndex + 1 == slot)
            {
                _consoleBox.Text = "";
            }
            AppendConsole(slot, $"[MİSTİK] Sunucu Slot #{slot} başlatılıyor...");
            
            _btnStart.IsEnabled = false;
            _btnStop.IsEnabled = true;
            _cbServerType.IsEnabled = false;
            _cbVersion.IsEnabled = false;
            _cbRam.IsEnabled = false;
            _txtServerPort.IsEnabled = false;
            if (_txtServerIp != null) _txtServerIp.IsEnabled = false;
            if (_chkGeyser != null) _chkGeyser.IsEnabled = false;
            _txtMotd.IsEnabled = false;
            _txtSlotName.IsEnabled = false;
            _chkOffline.IsEnabled = false;
            _chkVoiceChat.IsEnabled = false;
            _statusLbl.Text = "🟡 Başlatılıyor...";
            _statusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
            AppendConsole(slot, $"[MİSTİK] Sunucu türü: {(serverType == "fabric" ? "🧵 Fabric" : serverType == "forge" ? "🛠️ Forge" : serverType == "vanilla" ? "🟩 Vanilla" : "📄 Paper")} — {launchJar}");

            // Extract RAM gigabytes
            var ramStr = _cbRam.SelectedItem?.ToString()?.Split(' ')[0];
            int ram = int.TryParse(ramStr, out var r) ? r : 4;
            
            // Sürümü oku ve uygun Java'yı indir/yükle
            var versionFile = Path.Combine(serverDir, "version.txt");
            var version = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "1.21";
            
            var javaPath = await EnsureJavaVersionForMinecraftAsync(version, slot);
            
            // Retrieve port
            port = string.IsNullOrWhiteSpace(_txtServerPort.Text) ? "25565" : _txtServerPort.Text.Trim();
            
            AppendConsole(slot, $"[MİSTİK] Kullanılan Java: {javaPath}");
            AppendConsole(slot, $"[MİSTİK] Ayrılan Bellek: {ram} GB");
            AppendConsole(slot, $"[MİSTİK] Sunucu Portu: {port}");
            
            // Force server.properties settings write
            try
            {
                WriteServerProperties(serverDir);
            }
            catch {}

            // Voice Chat: Plasmo Voice (TCP mode) — bore.pub tünelinden çalışır, ekstra UDP port açmak gerekmez
            var voiceChatEnabled = _chkVoiceChat.IsChecked == true;
            if (voiceChatEnabled)
            {
                string voiceDirName = (serverType == "fabric" || serverType == "forge") ? "mods" : "plugins";
                var voiceDir = Path.Combine(serverDir, voiceDirName);
                Directory.CreateDirectory(voiceDir);

                // Sunucu tipi değişiminde çakışma olmaması için diğer klasördeki dosyaları temizle
                string otherDirName = (voiceDirName == "mods") ? "plugins" : "mods";
                string otherDir = Path.Combine(serverDir, otherDirName);
                if (Directory.Exists(otherDir))
                {
                    try
                    {
                        var crossPv = Directory.GetFiles(otherDir, "*plasmo*voice*", SearchOption.TopDirectoryOnly);
                        var crossOld = Directory.GetFiles(otherDir, "*voicechat*", SearchOption.TopDirectoryOnly);
                        foreach (var f in crossPv.Concat(crossOld))
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                    catch { }
                }

                // Plasmo Voice veya eski kurulum var mı kontrol et
                var existingPv  = System.IO.Directory.GetFiles(voiceDir, "*plasmo*voice*",  System.IO.SearchOption.TopDirectoryOnly);
                var existingOld = System.IO.Directory.GetFiles(voiceDir, "*voicechat*",     System.IO.SearchOption.TopDirectoryOnly);

                // Eski Simple Voice Chat varsa sil — Plasmo ile çakışır
                foreach (var old in existingOld)
                {
                    try { File.Delete(old); AppendConsole(slot, $"[MİSTİK] 🗑️ Eski Simple Voice Chat kaldırıldı: {Path.GetFileName(old)}"); } catch { }
                }

                if (existingPv.Length == 0)
                {
                    AppendConsole(slot, "[MİSTİK] 🎙️ Plasmo Voice (TCP modu) kuruluyor...");
                    AppendConsole(slot, "[BİLGİ] Plasmo Voice, sesi Minecraft TCP bağlantısından geçirir — tünel veya ekstra port gerekmez.");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var http = new System.Net.Http.HttpClient();
                            http.DefaultRequestHeaders.Add("User-Agent", "MistikLauncherUltra/1.0");

                            // Plasmo Voice — Modrinth slug: plasmo-voice
                            string loaders = (serverType == "fabric") ? "[%22fabric%22]" : 
                                             (serverType == "forge") ? "[%22forge%22]" : 
                                             "[%22paper%22,%22bukkit%22]";
                            var resp = await http.GetStringAsync(
                                $"https://api.modrinth.com/v2/project/plasmo-voice/version?loaders={loaders}&limit=1");
                            var verArr = Newtonsoft.Json.Linq.JArray.Parse(resp);
                            if (verArr.Count > 0)
                            {
                                var files = verArr[0]["files"] as Newtonsoft.Json.Linq.JArray;
                                if (files != null && files.Count > 0)
                                {
                                    // Server jar'ı seç (client jar değil)
                                    Newtonsoft.Json.Linq.JToken? fileObj = null;
                                    foreach (var f in files)
                                    {
                                        var fn = f["filename"]?.ToString() ?? "";
                                        if (fn.Contains("server") || !fn.Contains("client")) { fileObj = f; break; }
                                    }
                                    fileObj ??= files[0];

                                    var dlUrl = fileObj["url"]?.ToString();
                                    var fname = fileObj["filename"]?.ToString() ?? "plasmo-voice.jar";
                                    if (!string.IsNullOrEmpty(dlUrl))
                                    {
                                        var dest = System.IO.Path.Combine(voiceDir, fname);
                                        await VersionManagerPage.DownloadFileWithProgressAsync(dlUrl, dest, (p, s2) => { }, 0, 100);
                                        AppendConsole(slot, $"[MİSTİK] ✅ Plasmo Voice kuruldu: {voiceDirName}/{fname}");

                                        // TCP mod config'ini otomatik yaz
                                        WritePlasmoVoiceConfig(serverDir, serverType, slot);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppendConsole(slot, $"[UYARI] Plasmo Voice kurulamadı: {ex.Message}");
                        }
                    });
                }
                else
                {
                    AppendConsole(slot, "[MİSTİK] 🎙️ Plasmo Voice zaten kurulu. TCP modu kontrol ediliyor...");
                    WritePlasmoVoiceConfig(serverDir, serverType, slot);
                }
            }

            var thisSlot = slot;
            var geyserEnabled = _chkGeyser?.IsChecked == true;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (geyserEnabled)
                    {
                        AppendConsole(thisSlot, "[MİSTİK] 🌉 GeyserMC Bedrock Giriş Köprüsü kontrol ediliyor...");
                        await InstallGeyserAsync(serverDir, serverType, thisSlot);
                    }

                    ProcessStartInfo psi;
                    if (serverType == "forge")
                    {
                        var runBat = Path.Combine(serverDir, "run.bat");
                        if (File.Exists(runBat))
                        {
                            try
                            {
                                var batLines = File.ReadAllLines(runBat);
                                bool updated = false;
                                for (int i = 0; i < batLines.Length; i++)
                                {
                                    if (batLines[i].TrimStart().StartsWith("java ", StringComparison.OrdinalIgnoreCase))
                                    {
                                        batLines[i] = batLines[i].Replace("java ", $"\"{javaPath}\" ");
                                        updated = true;
                                    }
                                }
                                if (updated)
                                {
                                    File.WriteAllLines(runBat, batLines);
                                    AppendConsole(thisSlot, "[MİSTİK] Forge run.bat dosyası yerel Java yolu ile güncellendi.");
                                }
                            }
                            catch (Exception ex)
                            {
                                AppendConsole(thisSlot, $"[UYARI] Forge run.bat dosyası özelleştirilemedi: {ex.Message}");
                            }

                            psi = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c run.bat nogui",
                                WorkingDirectory = serverDir,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                        }
                        else
                        {
                            var forgeJars = Directory.Exists(serverDir) ? Directory.GetFiles(serverDir, "forge-*.jar") : Array.Empty<string>();
                            string? forgeJar = null;
                            foreach (var jar in forgeJars)
                            {
                                var fn = Path.GetFileName(jar).ToLower();
                                if (!fn.Contains("installer") && !fn.Contains("universal"))
                                {
                                    forgeJar = Path.GetFileName(jar);
                                    break;
                                }
                            }
                            if (forgeJar == null && forgeJars.Length > 0)
                            {
                                foreach (var jar in forgeJars)
                                {
                                    var fn = Path.GetFileName(jar).ToLower();
                                    if (!fn.Contains("installer"))
                                    {
                                        forgeJar = Path.GetFileName(jar);
                                        break;
                                    }
                                }
                            }

                            var jarToRun = forgeJar ?? "server.jar";
                            psi = new ProcessStartInfo
                            {
                                FileName = javaPath,
                                Arguments = $"-Xmx{ram}G -Xms{ram}G -jar {jarToRun} nogui",
                                WorkingDirectory = serverDir,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                        }
                    }
                    else
                    {
                        psi = new ProcessStartInfo
                        {
                            FileName = javaPath,
                            Arguments = $"-Xmx{ram}G -Xms{ram}G -jar {launchJar} nogui",
                            WorkingDirectory = serverDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                    }

                    var proc = new Process { StartInfo = psi };
                    _serverProcesses[thisSlot - 1] = proc;
                    
                    proc.OutputDataReceived += (s, e) => {
                        if (e.Data != null)
                        {
                            AppendConsole(thisSlot, e.Data);
                            // Detect server fully loaded
                            if (e.Data.Contains("Done (") || e.Data.Contains("For help, type \"help\""))
                            {
                                _slotStatuses[thisSlot - 1] = "🟢 Aktif";
                                Dispatcher.Invoke(() =>
                                {
                                    if (_cbServerSlot.SelectedIndex + 1 == thisSlot)
                                    {
                                        _statusLbl.Text = "🟢 Aktif";
                                        _statusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                                    }
                                });
                            }
                        }
                    };
                    
                    proc.ErrorDataReceived += (s, e) => {
                        if (e.Data != null) AppendConsole(thisSlot, "[JVM HATA] " + e.Data);
                    };

                    proc.Start();
                    try
                    {
                        File.WriteAllText(Path.Combine(serverDir, "server.pid"), proc.Id.ToString());
                    }
                    catch {}
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    proc.WaitForExit();
                }
                catch (Exception ex)
                {
                    AppendConsole(thisSlot, $"[HATA] Sunucu başlatılamadı: {ex.Message}");
                }
                finally
                {
                    _isStartingOrRunning[thisSlot - 1] = false;
                    _serverProcesses[thisSlot - 1] = null;
                    _slotStatuses[thisSlot - 1] = "🔴 Kapalı";
                    Dispatcher.Invoke(() =>
                    {
                        if (_cbServerSlot.SelectedIndex + 1 == thisSlot)
                        {
                            _btnStart.IsEnabled = true;
                            _btnStop.IsEnabled = false;
                            _cbServerType.IsEnabled = true;
                            _cbVersion.IsEnabled = true;
                            _cbRam.IsEnabled = true;
                            _txtServerPort.IsEnabled = true;
                            if (_txtServerIp != null) _txtServerIp.IsEnabled = true;
                            if (_chkGeyser != null) _chkGeyser.IsEnabled = true;
                            _txtMotd.IsEnabled = true;
                            _txtSlotName.IsEnabled = true;
                            _chkOffline.IsEnabled = true;
                            _chkVoiceChat.IsEnabled = true;
                            _statusLbl.Text = "🔴 Kapalı";
                            _statusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                        }
                    });
                }
            });
        }

        void StopServer()
        {
            var slot = _cbServerSlot.SelectedIndex + 1;
            if (slot < 1 || slot > 5) return;
            if (!_isStartingOrRunning[slot - 1]) return;

            AppendConsole(slot, "[MİSTİK] Sunucu durduruluyor (stop komutu gönderiliyor)...");
            _statusLbl.Text = "🔴 Durduruluyor...";
            _statusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
            _btnStop.IsEnabled = false;

            var proc = _serverProcesses[slot - 1];
            if (proc != null && !proc.HasExited)
            {
                Task.Run(() =>
                {
                    try
                    {
                        proc.StandardInput.WriteLine("stop");
                    }
                    catch
                    {
                        try { proc.Kill(); } catch { }
                    }

                    // Wait up to 10 seconds for graceful exit
                    if (!proc.WaitForExit(10000))
                    {
                        AppendConsole(slot, "[UYARI] Sunucu 10 saniye içinde durmadı, zorla sonlandırılıyor...");
                        try { proc.Kill(); } catch { }
                    }
                });
            }
            else
            {
                _isStartingOrRunning[slot - 1] = false;
                OnSlotChanged();
            }
        }

        void KillStrayProcesses(int slot, string serverDir, string portStr)
        {
            // 1. PID file check
            var pidPath = Path.Combine(serverDir, "server.pid");
            if (File.Exists(pidPath))
            {
                try
                {
                    if (int.TryParse(File.ReadAllText(pidPath).Trim(), out var oldPid))
                    {
                        var oldProc = Process.GetProcessById(oldPid);
                        if (oldProc.ProcessName.Contains("java", StringComparison.OrdinalIgnoreCase) || 
                            oldProc.ProcessName.Contains("cmd", StringComparison.OrdinalIgnoreCase))
                        {
                            AppendConsole(slot, $"[MİSTİK] ⚠️ Eski çalışan sunucu süreci tespit edildi (PID: {oldPid}), sonlandırılıyor...");
                            oldProc.Kill(true);
                            oldProc.WaitForExit(3000);
                        }
                    }
                }
                catch {}
                try { File.Delete(pidPath); } catch {}
            }

            // 2. Port listening check
            if (int.TryParse(portStr, out var port))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"(Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue).OwningProcess\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        if (int.TryParse(output, out var portPid) && portPid > 0)
                        {
                            try
                            {
                                var portProc = Process.GetProcessById(portPid);
                                if (portProc.ProcessName.Contains("java", StringComparison.OrdinalIgnoreCase) ||
                                    portProc.ProcessName.Contains("cmd", StringComparison.OrdinalIgnoreCase))
                                {
                                    AppendConsole(slot, $"[MİSTİK] ⚠️ Port {port} üzerinde çalışan eski sunucu süreci tespit edildi (PID: {portPid}), sonlandırılıyor...");
                                    portProc.Kill(true);
                                    portProc.WaitForExit(3000);
                                }
                            }
                            catch {}
                        }
                    }
                }
                catch {}
            }

            // 3. Check file lock on latest.log / session.lock and kill Mistik Java processes if locked
            var lockFiles = new[] { Path.Combine(serverDir, "logs", "latest.log"), Path.Combine(serverDir, "world", "session.lock") };
            bool isFolderLocked = false;
            foreach (var lf in lockFiles)
            {
                if (File.Exists(lf))
                {
                    try
                    {
                        using var fs = File.Open(lf, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException)
                    {
                        isFolderLocked = true;
                        break;
                    }
                }
            }

            if (isFolderLocked)
            {
                AppendConsole(slot, "[MİSTİK] ⚠️ Sunucu dosyaları başka bir işlem tarafından kilitlenmiş. Tüm çakışan Java süreçleri temizleniyor...");
                try
                {
                    foreach (var proc in Process.GetProcessesByName("java"))
                    {
                        try
                        {
                            var path = proc.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(path) && path.Contains(".mistik_ultra", StringComparison.OrdinalIgnoreCase))
                            {
                                // Verify if this process is not one of our currently active and running slots
                                bool isActiveSlot = false;
                                for (int i = 0; i < 5; i++)
                                {
                                    if (i + 1 != slot && _isStartingOrRunning[i] && _serverProcesses[i]?.Id == proc.Id)
                                    {
                                        isActiveSlot = true;
                                        break;
                                    }
                                }

                                if (!isActiveSlot)
                                {
                                    AppendConsole(slot, $"[MİSTİK] 🔨 Askıda kalan Java süreci sonlandırılıyor: PID {proc.Id}");
                                    proc.Kill(true);
                                }
                            }
                        }
                        catch {}
                    }
                }
                catch {}
            }
        }

        void SendConsoleCommand()
        {
            if (_cmdInput == null) return;
            var cmd = _cmdInput.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;

            var slot = _cbServerSlot.SelectedIndex + 1;
            var proc = _serverProcesses[slot - 1];

            if (proc != null && !proc.HasExited)
            {
                try
                {
                    proc.StandardInput.WriteLine(cmd);
                    AppendConsole(slot, $"> {cmd}");
                    _cmdInput.Text = "";
                }
                catch (Exception ex)
                {
                    AppendConsole(slot, $"[HATA] Komut gönderilemedi: {ex.Message}");
                }
            }
            else
            {
                AppendConsole(slot, "[MİSTİK] Sunucu aktif değil, komut gönderilemez.");
            }
        }

        // ─── Plasmo Voice TCP Config Yazıcı ────────────────────────────────────────
        void WritePlasmoVoiceConfig(string serverDir, string serverType, int slot)
        {
            try
            {
                // Plasmo Voice config klasörü: plugins/PlasmoVoice/ veya config/plasmo-voice/
                string configDir;
                string configFileName = "server.yml";
                
                if (serverType == "fabric" || serverType == "forge")
                {
                    configDir = Path.Combine(serverDir, "config", "plasmo-voice");
                }
                else
                {
                    configDir = Path.Combine(serverDir, "plugins", "PlasmoVoice");
                }
                
                Directory.CreateDirectory(configDir);
                var configPath = Path.Combine(configDir, configFileName);

                // TCP modu aktifken Plasmo, ses paketlerini UDP yerine TCP (Minecraft bağlantısı) üzerinden taşır.
                var configContent = "# Plasmo Voice Config - Auto generated by Mistik Client\n" +
                    "\n" +
                    "host: '0.0.0.0'\n" +
                    "port: 0\n" +
                    "\n" +
                    "# TCP modunu etkinleştir — UDP portu açmak gerekmez, gecikme düşük kalır\n" +
                    "tcp_mode: true\n" +
                    "\n" +
                    "voice:\n" +
                    "  # Maksimum ses mesafesi (blok)\n" +
                    "  max_distance: 48\n" +
                    "  # Fade mesafesi (blok) — burada sesin azalmaya başlayacağı mesafe\n" +
                    "  fade_distance: 1\n" +
                    "  # Varsayılan konuşma mesafesi\n" +
                    "  default_distance: 16\n" +
                    "\n" +
                    "# Kayıt kalitesi: OPUS_LOW (düşük ping), OPUS_MEDIUM, OPUS_HIGH\n" +
                    "codec_settings:\n" +
                    "  codec: OPUS_LOW\n" +
                    "  sample_rate: 24000\n" +
                    "  frame_size: 960\n" +
                    "  bitrate: -1000\n" +
                    "\n" +
                    "logging:\n" +
                    "  log_connections: false\n";

                string relPath = (serverType == "fabric" || serverType == "forge") ? "config/plasmo-voice/server.yml" : "plugins/PlasmoVoice/server.yml";

                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, configContent);
                    AppendConsole(slot, $"[MİSTİK] 🎙️ Plasmo Voice TCP config yazıldı → {relPath}");
                    AppendConsole(slot, "[BİLGİ] tcp_mode=true | codec=OPUS_LOW (düşük gecikme) | bore tüneli ile uyumlu ✅");
                }
                else
                {
                    // Var olan config'de tcp_mode satırını güncelle
                    var lines = File.ReadAllLines(configPath).ToList();
                    bool found = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].TrimStart().StartsWith("tcp_mode:"))
                        {
                            lines[i] = "tcp_mode: true";
                            found = true;
                        }
                    }
                    if (!found) lines.Add("tcp_mode: true");
                    File.WriteAllLines(configPath, lines);
                    AppendConsole(slot, "[MİSTİK] 🎙️ Plasmo Voice TCP config güncellendi ✅");
                }
            }
            catch (Exception ex)
            {
                AppendConsole(slot, $"[MİSTİK] [HATA] Plasmo config yazılamadı: {ex.Message}");
            }
        }

        void StartTunnelAction()
        {
            var relay = _main?.Relay;
            if (relay == null) return;

            // Get selected port
            var portStr = _txtServerPort.Text.Trim();
            int port = int.TryParse(portStr, out var p) ? p : 25565;

            // Start tunnel for selected local port
            _btnTunnel.IsEnabled = false;
            _btnTunnel.Content = "Bağlanıyor...";
            _btnStopTunnel.IsEnabled = false;
            _tunnelStatusLbl.Text = "Tünel kuruluyor, lütfen bekleyin...";
            _tunnelStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
            
            if (_cbTunnelType.SelectedIndex == 0) // playit.gg
            {
                AppendConsole($"[MİSTİK] 🌐 playit.gg Tüneli başlatılıyor...");
                relay.StartTunnel(port, "playit.gg");
            }
            else if (_cbTunnelType.SelectedIndex == 1) // upnp
            {
                AppendConsole($"[MİSTİK] 🌐 UPnP otomatik modem port yönlendirmesi başlatılıyor...");
                relay.StartTunnel(port, "upnp");
            }
            else if (_cbTunnelType.SelectedIndex == 2) // bore.pub
            {
                AppendConsole($"[MİSTİK] 🌐 bore.pub TCP Tüneli port {port} üzerinden başlatılıyor...");
                relay.StartTunnel(port, "bore.pub");
            }
            else if (_cbTunnelType.SelectedIndex == 3) // serveo.net
            {
                AppendConsole($"[MİSTİK] 🌐 serveo.net Tüneli port {port} üzerinden başlatılıyor...");
                relay.StartTunnel(port, "serveo.net");
            }
            else // custom SSH
            {
                var host = string.IsNullOrWhiteSpace(_txtTunnelHost.Text) ? "nokey@localhost.run" : _txtTunnelHost.Text.Trim();
                AppendConsole($"[MİSTİK] 🌐 Özel SSH Tüneli ({host}) port {port} üzerinden başlatılıyor...");
                relay.StartTunnel(port, "custom", customHost: host);
            }
        }

        void StopTunnelAction()
        {
            var relay = _main?.Relay;
            if (relay == null) return;

            AppendConsole("[MİSTİK] Tünel sonlandırılıyor...");
            relay.StopTunnel();
            
            _btnTunnel.IsEnabled = true;
            _btnTunnel.Content = "🌐 TÜNELİ BAŞLAT";
            _btnTunnel.Background = PageHelpers.HexBrush("#00A3FF");
            _btnStopTunnel.IsEnabled = false;
            _btnStopTunnel.Background = PageHelpers.HexBrush("#555555");
            _tunnelStatusLbl.Text = "Tünel aktif değil.";
            _tunnelStatusLbl.Foreground = PageHelpers.HexBrush("#A0A0A0");
        }

        void OnGlobalTunnelReady(string? addr)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (addr != null)
                {
                    _btnTunnel.IsEnabled = false;
                    _btnTunnel.Content = "🌐 TÜNELİ BAŞLAT";
                    _btnTunnel.Background = PageHelpers.HexBrush("#00A3FF");
                    _btnStopTunnel.IsEnabled = true;
                    _btnStopTunnel.Background = PageHelpers.HexBrush("#FF4B4B");
                    _tunnelStatusLbl.Text = $"✅ Tünel Aktif! Sunucu IP:\n{addr}";
                    _tunnelStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                    AppendConsole($"[MİSTİK] ✅ Tünel Başarılı! Paylaşılacak IP: {addr}");
                }
                else
                {
                    _btnTunnel.IsEnabled = true;
                    _btnTunnel.Content = "🌐 TÜNELİ BAŞLAT";
                    _btnTunnel.Background = PageHelpers.HexBrush("#00A3FF");
                    _btnStopTunnel.IsEnabled = false;
                    _btnStopTunnel.Background = PageHelpers.HexBrush("#555555");
                    _tunnelStatusLbl.Text = "Bağlantı kurulamadı. Lütfen tekrar deneyin.";
                    _tunnelStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                    AppendConsole("[MİSTİK] [HATA] Tünel bağlantısı başarısız oldu.");
                }
            }));
        }

        void OnGlobalTunnelLog(string msg)
        {
            AppendConsole(msg);
            if (msg != null && msg.Contains("🔑 LİNKİ YAKALANDI!"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Windows.MessageBox.Show(
                        "playit.gg tüneli ilk kullanım için doğrulama gerektiriyor!\n\n" +
                        "1. Varsayılan tarayıcınızda playit.gg eşleştirme sayfası otomatik olarak açıldı.\n" +
                        "2. Lütfen açılan sayfada 'Add Agent' veya 'Claim Agent' butonuna tıklayarak doğrulamayı tamamlayın.\n" +
                        "3. Doğrulama bittiği an sunucunuz otomatik olarak dış dünyaya açılacaktır!",
                        "Mistik Launcher - playit.gg Doğrulaması",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }));
            }
        }

        // ─── EKLENTİ & MOD MARKETİ MANTIĞI ───
        
        void LoadPopularPlugins()
        {
            _pluginContainer.Children.Clear();
            
            var popularList = new[]
            {
                new { Title = "EssentialsX", Slug = "essentialsx", Desc = "Sunucuya /spawn, /home, ekonomi, kitler ve yüzlerce temel komut ekler." },
                new { Title = "WorldEdit", Slug = "worldedit", Desc = "Sunucuda devasa yapıları blok yerleştirerek saniyeler içinde düzenlemenizi sağlar." },
                new { Title = "LuckPerms", Slug = "luckperms", Desc = "Sunucuda oyunculara rütbe, grup ve yetki sistemleri tanımlar." },
                new { Title = "GeyserMC", Slug = "geyser", Desc = "Java sunucunuza Bedrock (Mobil/Tablet) oyuncularının katılmasını sağlar!" },
                new { Title = "ViaVersion", Slug = "viaversion", Desc = "Sunucuya kendisinden daha yeni Minecraft sürümlerinin girmesine izin verir." },
                new { Title = "Vault", Slug = "vault", Desc = "Diğer eklentilerin ekonomi ve yetki sistemleriyle anlaşmasını sağlayan temel araç." }
            };
            
            foreach (var item in popularList)
            {
                var card = CreatePluginCard(item.Title, item.Desc, item.Slug, "");
                _pluginContainer.Children.Add(card);
            }
        }

        void SearchPlugins()
        {
            var q = _pluginSearchInput.Text.Trim();
            if (string.IsNullOrEmpty(q))
            {
                LoadPopularPlugins();
                return;
            }
            
            Task.Run(async () =>
            {
                await SearchPluginsAsync(q);
            });
        }

        async Task SearchPluginsAsync(string query)
        {
            Dispatcher.Invoke(() =>
            {
                _pluginContainer.Children.Clear();
                _pluginContainer.Children.Add(PageHelpers.Lbl("🔎 Modrinth üzerinde eklentiler aranıyor...", 13, "#FFB100", true));
            });
            
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "MistikLauncherUltra/1.0 (contact@mistik.com)");
                
                // Fetch search hits
                var response = await http.GetStringAsync($"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&limit=9&facets=[[%22categories:spigot%22,%22categories:paper%22]]");
                var json = JObject.Parse(response);
                var hits = (JArray?)json["hits"];
                
                Dispatcher.Invoke(() =>
                {
                    _pluginContainer.Children.Clear();
                    
                    if (hits == null || hits.Count == 0)
                    {
                        _pluginContainer.Children.Add(PageHelpers.Lbl("❌ Sonuç bulunamadı.", 13, "#FF4B4B", true));
                        return;
                    }
                    
                    foreach (var hit in hits)
                    {
                        var title = hit["title"]?.ToString() ?? "Bilinmeyen Eklenti";
                        var desc = hit["description"]?.ToString() ?? "";
                        var slug = hit["slug"]?.ToString() ?? hit["project_id"]?.ToString() ?? "";
                        
                        var card = CreatePluginCard(title, desc, slug, "");
                        _pluginContainer.Children.Add(card);
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    _pluginContainer.Children.Clear();
                    _pluginContainer.Children.Add(PageHelpers.Lbl($"❌ Arama hatası: {ex.Message}", 13, "#FF4B4B", true));
                });
            }
        }

        Border CreatePluginCard(string title, string desc, string slug, string iconUrl)
        {
            var card = PageHelpers.Card("#181B22", 8, "#242C3C", new Thickness(6));
            card.Width = 205;
            card.Height = 160;
            
            var sp = new StackPanel { Margin = new Thickness(12) };
            
            // Title
            var titleLbl = PageHelpers.Lbl(title, 13, "#00A3FF", true);
            titleLbl.TextTrimming = TextTrimming.CharacterEllipsis;
            titleLbl.MaxWidth = 180;
            sp.Children.Add(titleLbl);
            
            // Description
            var descLbl = PageHelpers.Lbl(desc, 10, "#A0A0A0", wrap: TextWrapping.Wrap);
            descLbl.Height = 55;
            descLbl.Margin = new Thickness(0, 4, 0, 8);
            descLbl.TextTrimming = TextTrimming.WordEllipsis;
            sp.Children.Add(descLbl);
            
            // Install Button
            var installBtn = PageHelpers.MkBtn("📥 EKLENTİ YÜKLE", "#00A3FF");
            installBtn.Height = 28;
            installBtn.FontSize = 10;
            installBtn.Click += (s, e) =>
            {
                Task.Run(async () =>
                {
                    await InstallPluginAsync(slug, title);
                });
            };
            sp.Children.Add(installBtn);
            
            card.Child = sp;
            return card;
        }

        async Task InstallPluginAsync(string slug, string title)
        {
            var slot = 1;
            Dispatcher.Invoke(() =>
            {
                slot = _cbServerSlot.SelectedIndex + 1;
            });
            if (slot < 1 || slot > 5) return;

            if (_isStartingOrRunning[slot - 1])
            {
                MessageBox.Show(
                    $"Sunucu Slot #{slot} şu anda çalışıyor.\n\n" +
                    "Eklenti veya mod kurmadan önce lütfen çalışan sunucuyu tamamen durdurun.",
                    "Sunucu Çalışıyor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var serverDir = Path.Combine(App.AppData, "servers", $"server_{slot}");
            var pluginsDir = Path.Combine(serverDir, "plugins");
            Directory.CreateDirectory(pluginsDir);
            
            AppendConsole($"[MİSTİK] Modrinth'ten '{title}' eklentisi Slot #{slot} için sorgulanıyor...");
            
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "MistikLauncherUltra/1.0 (contact@mistik.com)");
                
                // Get project version list
                var response = await http.GetStringAsync($"https://api.modrinth.com/v2/project/{slug}/version");
                var verList = JArray.Parse(response);
                if (verList.Count == 0)
                {
                    throw new Exception("Sürüm bulunamadı.");
                }
                
                var latestVer = verList[0];
                var files = (JArray?)latestVer["files"];
                if (files == null || files.Count == 0)
                {
                    throw new Exception("İndirilebilir dosya bulunamadı.");
                }
                
                var fileObj = files[0];
                var downloadUrl = fileObj["url"]?.ToString();
                var filename = fileObj["filename"]?.ToString() ?? $"{slug}.jar";
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new Exception("İndirme linki bulunamadı.");
                }
                
                var destPath = Path.Combine(pluginsDir, filename);
                
                AppendConsole($"[MİSTİK] İndirme başladı: {filename}...");
                
                await VersionManagerPage.DownloadFileWithProgressAsync(downloadUrl, destPath, (pct, status) => {
                    // Progress hooks can be added here
                }, 0, 100);
                
                AppendConsole($"[MİSTİK] ✅ Eklenti başarıyla yüklendi: plugins/{filename}");
                MessageBox.Show($"'{title}' eklentisi başarıyla kuruldu!\n\nDeğişikliklerin geçerli olması için sunucuyu yeniden başlatmanız gerekir.", "Eklenti Kuruldu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendConsole($"[EKLENTİ HATASI] '{title}' kurulamadı: {ex.Message}");
                MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        bool _isUpdatingSlotCombo = false;
        void UpdateSlotComboBoxItems()
        {
            if (_cbServerSlot == null || _isUpdatingSlotCombo) return;
            _isUpdatingSlotCombo = true;
            try
            {
                var selectedIdx = _cbServerSlot.SelectedIndex;
                var items = new string[5];
                for (int i = 0; i < 5; i++)
                {
                    var slotNum = i + 1;
                    var name = _slotNicknames[i];
                    items[i] = !string.IsNullOrEmpty(name) ? $"Slot #{slotNum} - {name}" : $"Sunucu Slot #{slotNum}";
                }
                _cbServerSlot.ItemsSource = items;
                _cbServerSlot.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
            }
            finally
            {
                _isUpdatingSlotCombo = false;
            }
        }

        async Task<string?> GetForgeVersionAsync(string mcVersion, HttpClient http)
        {
            try
            {
                var resp = await http.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
                var json = JObject.Parse(resp);
                var promos = json["promos"] as JObject;
                if (promos != null)
                {
                    var recommendedKey = $"{mcVersion}-recommended";
                    if (promos[recommendedKey] != null) return promos[recommendedKey]!.ToString();

                    var latestKey = $"{mcVersion}-latest";
                    if (promos[latestKey] != null) return promos[latestKey]!.ToString();

                    foreach (var prop in promos.Properties())
                    {
                        if (prop.Name.StartsWith(mcVersion + "-"))
                        {
                            return prop.Value.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendConsole($"[FORGE] Hata: Sürüm listesi alınamadı ({ex.Message}), varsayılan tahmini sürüm kullanılacak.");
            }

            return mcVersion switch
            {
                "1.20.4" => "49.0.31",
                "1.20.1" => "47.2.0",
                "1.19.4" => "45.1.0",
                "1.19.2" => "43.2.0",
                "1.18.2" => "40.2.0",
                "1.16.5" => "36.2.39",
                "1.12.2" => "14.23.5.2860",
                "1.7.10" => "10.13.4.1614",
                _ => null
            };
        }

        async Task InstallGeyserAsync(string serverDir, string serverType, int slot)
        {
            if (serverType == "vanilla")
            {
                AppendConsole(slot, "[MİSTİK] 🌉 [UYARI] Vanilla sunucularda Geyser entegrasyonu desteklenmemektedir. Eklenti desteği için Paper, mod desteği için Fabric/Forge kullanın.");
                return;
            }

            var folderName = serverType == "paper" ? "plugins" : "mods";
            var targetDir = Path.Combine(serverDir, folderName);
            Directory.CreateDirectory(targetDir);

            var existing = Directory.GetFiles(targetDir, "*geyser*")
                .FirstOrDefault(f => Path.GetExtension(f).ToLower() == ".jar");
            if (existing != null)
            {
                AppendConsole(slot, "[MİSTİK] 🌉 GeyserMC Bedrock Köprüsü zaten yüklü.");
                ConfigureGeyser(serverDir, serverType, slot);
                return;
            }

            AppendConsole(slot, "[MİSTİK] 🌉 GeyserMC Bedrock Giriş Köprüsü kuruluyor...");
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "MistikLauncherUltra/1.0");

                var loader = serverType == "paper" ? "spigot" : serverType;
                var resp = await http.GetStringAsync($"https://api.modrinth.com/v2/project/geyser/version?loaders=[%22{loader}%22]&limit=1");
                var verArr = JArray.Parse(resp);
                if (verArr.Count > 0)
                {
                    var files = verArr[0]["files"] as JArray;
                    if (files != null && files.Count > 0)
                    {
                        var dlUrl = files[0]["url"]?.ToString();
                        var fname = files[0]["filename"]?.ToString() ?? "Geyser.jar";
                        if (!string.IsNullOrEmpty(dlUrl))
                        {
                            var dest = Path.Combine(targetDir, fname);
                            AppendConsole(slot, $"[MİSTİK] GeyserMC indiriliyor: {fname}...");
                            await VersionManagerPage.DownloadFileWithProgressAsync(dlUrl, dest, (p, s) => { }, 0, 100);
                            AppendConsole(slot, $"[MİSTİK] ✅ GeyserMC başarıyla kuruldu: {folderName}/{fname}");
                            ConfigureGeyser(serverDir, serverType, slot);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendConsole(slot, $"[MİSTİK] [HATA] GeyserMC otomatik kurulamadı: {ex.Message}");
            }
        }

        void ConfigureGeyser(string serverDir, string serverType, int slot)
        {
            try
            {
                string configPath = "";
                if (serverType == "paper")
                {
                    configPath = Path.Combine(serverDir, "plugins", "Geyser-Spigot", "config.yml");
                }
                else if (serverType == "fabric")
                {
                    configPath = Path.Combine(serverDir, "config", "Geyser-Fabric", "config.yml");
                }
                else if (serverType == "forge")
                {
                    configPath = Path.Combine(serverDir, "config", "Geyser-Forge", "config.yml");
                }

                if (string.IsNullOrEmpty(configPath)) return;

                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

                var isOffline = true;
                var port = "25565";
                Dispatcher.Invoke(() =>
                {
                    isOffline = _chkOffline.IsChecked == true;
                    port = string.IsNullOrWhiteSpace(_txtServerPort.Text) ? "25565" : _txtServerPort.Text.Trim();
                });

                var authType = isOffline ? "offline" : "online";

                var configContent = $"# GeyserMC Auto Configuration by Mistik Launcher\n" +
                    $"bedrock:\n" +
                    $"  address: 0.0.0.0\n" +
                    $"  port: {port}\n" +
                    $"  clone-remote-port: true\n" +
                    $"remote:\n" +
                    $"  address: 127.0.0.1\n" +
                    $"  port: {port}\n" +
                    $"  auth-type: {authType}\n";

                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, configContent);
                    AppendConsole(slot, $"[MİSTİK] 🌉 GeyserMC config yazıldı (port={port}, auth-type={authType}) ✅");
                }
                else
                {
                    var lines = File.ReadAllLines(configPath).ToList();
                    bool foundAuth = false;
                    bool foundClone = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i].TrimStart();
                        if (line.StartsWith("auth-type:"))
                        {
                            lines[i] = $"  auth-type: {authType}";
                            foundAuth = true;
                        }
                        else if (line.StartsWith("clone-remote-port:"))
                        {
                            lines[i] = "  clone-remote-port: true";
                            foundClone = true;
                        }
                    }
                    if (!foundAuth) lines.Add($"  auth-type: {authType}");
                    if (!foundClone) lines.Add("  clone-remote-port: true");
                    File.WriteAllLines(configPath, lines);
                    AppendConsole(slot, $"[MİSTİK] 🌉 GeyserMC config güncellendi (auth-type={authType}) ✅");
                }
            }
            catch (Exception ex)
            {
                AppendConsole(slot, $"[MİSTİK] [UYARI] GeyserMC config güncellenemedi: {ex.Message}");
            }
        }

        // Cleanup events when leaving page
        public void Unsubscribe()
        {
            try
            {
                if (_main?.Relay != null)
                {
                    _main.Relay.OnTunnelReady -= OnGlobalTunnelReady;
                    _main.Relay.OnTunnelLog -= OnGlobalTunnelLog;
                }
            }
            catch {}
        }

        private void CleanCorruptedConfigFiles(string serverDir)
        {
            try
            {
                if (!Directory.Exists(serverDir)) return;
                var files = Directory.GetFiles(serverDir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".yml" || ext == ".properties" || ext == ".txt" || ext == ".json")
                    {
                        var info = new FileInfo(file);
                        if (info.Exists && info.Length > 0)
                        {
                            byte[] bytes = File.ReadAllBytes(file);
                            bool allNull = true;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                if (bytes[i] != 0)
                                {
                                    allNull = false;
                                    break;
                                }
                            }
                            if (allNull)
                            {
                                File.Delete(file);
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private static bool IsValidBindAddress(string ipStr)
        {
            if (string.IsNullOrWhiteSpace(ipStr)) return true;
            if (ipStr.Trim().ToLower() == "localhost") return true;
            
            if (System.Net.IPAddress.TryParse(ipStr, out var ip))
            {
                if (System.Net.IPAddress.IsLoopback(ip) || ip.Equals(System.Net.IPAddress.Any) || ip.Equals(System.Net.IPAddress.IPv6Any))
                {
                    return true;
                }
                
                try
                {
                    var hostIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());
                    foreach (var localIp in hostIPs)
                    {
                        if (localIp.Equals(ip)) return true;
                    }
                }
                catch {}
            }
            return false;
        }

        static bool RequiresJava25(string version)
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

        static bool RequiresJava21(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            
            var clean = version.Split(' ')[0];

            var parts = GetVersionNumbers(clean);
            if (parts.Count >= 2)
            {
                var major = parts[0];
                var minor = parts[1];
                var patch = parts.Count >= 3 ? parts[2] : 0;

                if (major > 1) return true;
                if (major == 1)
                {
                    if (minor > 20) return true;
                    if (minor == 20 && patch >= 5) return true;
                }
            }
            return false;
        }

        static System.Collections.Generic.List<int> GetVersionNumbers(string input)
        {
            var list = new System.Collections.Generic.List<int>();
            var matches = System.Text.RegularExpressions.Regex.Matches(input, @"\d+");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (int.TryParse(m.Value, out var n))
                    list.Add(n);
            }
            return list;
        }

        async Task<string> EnsureJavaVersionForMinecraftAsync(string version, int slot)
        {
            bool req25 = RequiresJava25(version);
            bool req21 = RequiresJava21(version);

            var javaDir = Path.Combine(App.AppData, "java");
            
            if (req25)
            {
                var jre25Dir = Path.Combine(javaDir, "jre25");
                var java25Exe = Path.Combine(jre25Dir, "bin", "java.exe");
                if (File.Exists(java25Exe))
                {
                    return java25Exe;
                }

                AppendConsole(slot, "[MİSTİK] ☕ Minecraft 1.21.4 / 1.22+ veya modern snapshot sürümleri için Java 25 gereklidir!");
                AppendConsole(slot, "[MİSTİK] 📥 Java 25 (Adoptium JRE) otomatik olarak indiriliyor, lütfen bekleyin...");

                try
                {
                    Directory.CreateDirectory(javaDir);
                    var zipPath = Path.Combine(javaDir, "jre25.zip");
                    var tempExtractDir = Path.Combine(javaDir, "jre25_temp");

                    if (Directory.Exists(tempExtractDir))
                    {
                        try { Directory.Delete(tempExtractDir, true); } catch { }
                    }
                    Directory.CreateDirectory(tempExtractDir);

                    var url = "https://api.adoptium.net/v3/binary/latest/25/ga/windows/x64/jre/hotspot/normal/eclipse";
                    
                    await VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                        Dispatcher.Invoke(() => {
                            _installProgress.Value = pct;
                            _btnInstall.Content = $"Java 25: %{pct}";
                        });
                    }, 0, 100);

                    AppendConsole(slot, "[MİSTİK] 📦 Java 25 arşiv dosyası açılıyor...");
                    await Task.Run(() => {
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                    });

                    var subDirs = Directory.GetDirectories(tempExtractDir);
                    if (subDirs.Length > 0)
                    {
                        var sourceDir = subDirs[0];
                        if (Directory.Exists(jre25Dir))
                        {
                            try { Directory.Delete(jre25Dir, true); } catch { }
                        }
                        Directory.Move(sourceDir, jre25Dir);
                    }

                    try { Directory.Delete(tempExtractDir, true); } catch { }
                    try { File.Delete(zipPath); } catch { }

                    if (File.Exists(java25Exe))
                    {
                        AppendConsole(slot, "[MİSTİK] ✅ Java 25 başarıyla kuruldu ve etkinleştirildi!");
                        return java25Exe;
                    }
                }
                catch (Exception ex)
                {
                    AppendConsole(slot, $"[HATA] Java 25 otomatik kurulumu başarısız oldu: {ex.Message}");
                }
            }
            
            // JRE 21 check and download if required (or default)
            var jre21Dir = Path.Combine(javaDir, "jre21");
            var java21Exe = Path.Combine(jre21Dir, "bin", "java.exe");
            if (File.Exists(java21Exe))
            {
                return java21Exe;
            }

            AppendConsole(slot, "[MİSTİK] ☕ Minecraft 1.20.5+ için Java 21 gereklidir!");
            AppendConsole(slot, "[MİSTİK] 📥 Java 21 (Adoptium JRE) otomatik olarak indiriliyor, lütfen bekleyin...");

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

                var url = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse";
                
                await VersionManagerPage.DownloadFileWithProgressAsync(url, zipPath, (pct, status) => {
                    Dispatcher.Invoke(() => {
                        _installProgress.Value = pct;
                        _btnInstall.Content = $"Java 21: %{pct}";
                    });
                }, 0, 100);

                AppendConsole(slot, "[MİSTİK] 📦 Java 21 arşiv dosyası açılıyor...");
                await Task.Run(() => {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                });

                var subDirs = Directory.GetDirectories(tempExtractDir);
                if (subDirs.Length > 0)
                {
                    var sourceDir = subDirs[0];
                    if (Directory.Exists(jre21Dir))
                    {
                        try { Directory.Delete(jre21Dir, true); } catch { }
                    }
                    Directory.Move(sourceDir, jre21Dir);
                }

                try { Directory.Delete(tempExtractDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }

                if (File.Exists(java21Exe))
                {
                    AppendConsole(slot, "[MİSTİK] ✅ Java 21 başarıyla kuruldu!");
                    return java21Exe;
                }
            }
            catch (Exception ex)
            {
                AppendConsole(slot, $"[HATA] Java 21 otomatik kurulumu başarısız oldu: {ex.Message}");
            }

            // Fallback to system java or whatever we can find
            return "java";
        }
    }
}
