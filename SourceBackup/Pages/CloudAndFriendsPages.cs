using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MistikLauncherUltra.Pages
{
    // ─── Skin Page ────────────────────────────────────────────────────────────
    public class SkinPage : Page
    {
        static readonly string[] Skins = { "Notch","Herobrine","jeb_","Steve","Alex","Faker","Dream","TechnoBlade","MistikGamer","MrBeast","Elraenn","Lego","SpiderMan","Deadpool","Panda","Slime","Creeper" };
        public SkinPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40,30,40,30) };
            sp.Children.Add(PageHelpers.Lbl("🎨  Karakter (Skin) Odasi", 24, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("Kullanici adi yazarak veya bilgisayarinizdan .png skin yukleyerek karakterinizi degistirin", 12, "#A0A0A0"));

            // Dual card grid
            var skinTypeGrid = new Grid { Margin = new Thickness(0, 16, 0, 20) };
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Col 0: Username skin Card
            var userCard = PageHelpers.Card("#181818", 12);
            var userSp = new StackPanel { Margin = new Thickness(16) };
            userSp.Children.Add(PageHelpers.Lbl("Kullanici Adi ile Skin Cek", 14, "#00A3FF", true));
            userSp.Children.Add(PageHelpers.Lbl("Premium bir oyuncunun adini yazarak skinini cekin:", 10, "#888", wrap: TextWrapping.Wrap));
            var userRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var tb = PageHelpers.DarkTextBox(main.Config.User, 38); tb.Width = 150;
            var applyBtn = PageHelpers.MkBtn("Uygula", "#00A3FF", 75);
            applyBtn.Margin = new Thickness(6, 0, 0, 0);
            applyBtn.Click += (_, _) => {
                var n = tb.Text.Trim();
                if (string.IsNullOrEmpty(n)) return;
                main.Config.User = n; main.Config.SkinType = "username"; main.Config.SkinUser = n;
                ConfigManager.Save(main.Config); main.ReloadConfig();
                MessageBox.Show($"Karakteriniz '{n}' skini olarak ayarlandi!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            userRow.Children.Add(tb); userRow.Children.Add(applyBtn);
            userSp.Children.Add(userRow);
            userCard.Child = userSp;
            Grid.SetColumn(userCard, 0); skinTypeGrid.Children.Add(userCard);

            // Col 2: Custom Local Skin Card
            var localCard = PageHelpers.Card("#181818", 12);
            var localSp = new StackPanel { Margin = new Thickness(16) };
            localSp.Children.Add(PageHelpers.Lbl("Bilgisayardan Ozel Skin (.png) Yukle", 14, "#2EB82E", true));
            localSp.Children.Add(PageHelpers.Lbl("Kendi indirdiginiz .png skin dosyasini oyuna kaynak paketi olarak yukleyin:", 10, "#888", wrap: TextWrapping.Wrap));
            
            var chooseBtn = PageHelpers.MkBtn("Skin Dosyasi Sec (.png)", "#2EB82E", 190);
            chooseBtn.Margin = new Thickness(0, 12, 0, 0);
            chooseBtn.Click += (_, _) => {
                var dlg = new Microsoft.Win32.OpenFileDialog {
                    Filter = "Minecraft Skin (*.png)|*.png",
                    Title = "PNG formatindaki skin dosyanizi secin"
                };
                if (dlg.ShowDialog() == true) {
                    ApplyLocalSkin(main, dlg.FileName);
                }
            };
            localSp.Children.Add(chooseBtn);
            localCard.Child = localSp;
            Grid.SetColumn(localCard, 2); skinTypeGrid.Children.Add(localCard);

            sp.Children.Add(skinTypeGrid);

            sp.Children.Add(PageHelpers.Lbl("Hazır Karakter Galerisi", 14, "#00A3FF", true));
            var wrap = new WrapPanel { Margin = new Thickness(0,10,0,0) };
            foreach(var s in Skins)
            {
                var name = s;
                var card = new Border { Background=PageHelpers.HexBrush("#181818"), CornerRadius=new CornerRadius(12),
                    Margin=new Thickness(6), Padding=new Thickness(16), Width=150,
                    Cursor=System.Windows.Input.Cursors.Hand };
                var cSp = new StackPanel { HorizontalAlignment=HorizontalAlignment.Center };
                var img = new Image { Width=64, Height=64, HorizontalAlignment=HorizontalAlignment.Center };
                _ = LoadImgAsync(img, name, 64);
                cSp.Children.Add(img);
                cSp.Children.Add(PageHelpers.Lbl(name, 13, "#FFFFFF", true));
                var btn = PageHelpers.MkBtn("Seç", "#00A3FF"); btn.Margin=new Thickness(0,8,0,0);
                btn.Click += (_,_) => {
                    main.Config.User=name; main.Config.SkinType="username"; main.Config.SkinUser=name;
                    ConfigManager.Save(main.Config); main.ReloadConfig();
                    MessageBox.Show($"'{name}' seçildi!","✓",MessageBoxButton.OK,MessageBoxImage.Information);
                };
                cSp.Children.Add(btn);
                card.Child=cSp; wrap.Children.Add(card);
            }
            sp.Children.Add(wrap);
            Content = new ScrollViewer { Content=sp, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        }

        internal static async Task LoadImgAsync(Image img, string user, int size)
        {
            try {
                using var http = new HttpClient { Timeout=TimeSpan.FromSeconds(5) };
                var bytes = await http.GetByteArrayAsync($"https://mc-heads.net/avatar/{user}/{size}");
                var bmp = new BitmapImage();
                using var ms = new System.IO.MemoryStream(bytes);
                bmp.BeginInit(); bmp.CacheOption=BitmapCacheOption.OnLoad; bmp.StreamSource=ms; bmp.EndInit(); bmp.Freeze();
                img.Dispatcher.Invoke(() => img.Source = bmp);
            } catch {}
        }

        void ApplyLocalSkin(MainWindow main, string filePath)
        {
            try {
                var packDir = Path.Combine(App.GameDir, "resourcepacks", "MistikSkinPack");
                var textureDir = Path.Combine(packDir, "assets", "minecraft", "textures", "entity");
                Directory.CreateDirectory(textureDir);

                // Copy to steve and alex
                File.Copy(filePath, Path.Combine(textureDir, "steve.png"), true);
                File.Copy(filePath, Path.Combine(textureDir, "alex.png"), true);

                // Create pack.mcmeta
                var mcmetaPath = Path.Combine(packDir, "pack.mcmeta");
                var mcmetaContent = "{\n  \"pack\": {\n    \"pack_format\": 15,\n    \"description\": \"Mistik Launcher Ozel Skin Kaynak Paketi\"\n  }\n}";
                File.WriteAllText(mcmetaPath, mcmetaContent);

                main.Config.SkinType = "local";
                main.Config.SkinUser = filePath;
                ConfigManager.Save(main.Config); main.ReloadConfig();

                MessageBox.Show("Ozel skininiz basariyla 'Mistik Ozel Skin' kaynak paketi olarak yuklendi!\n\nOyuna girdikten sonra Secenekler > Kaynak Paketleri kismindan 'Mistik Ozel Skin' paketini aktif etmeniz yeterlidir!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show($"Skin yuklenirken hata olustu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ─── Cloud Page ───────────────────────────────────────────────────────────
    public class CloudPage : Page
    {
        public CloudPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40,30,40,30) };
            var hdr = new Grid();
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            hdr.Children.Add(PageHelpers.Lbl("☁️  Bulut Sunucular", 24, "#FFFFFF", true));
            var badge = new Border { Background=PageHelpers.HexBrush("#122c1b"), BorderBrush=PageHelpers.HexBrush("#2EB82E"),
                BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(12),
                Padding=new Thickness(12,6,12,6), VerticalAlignment=VerticalAlignment.Center };
            var badgeLbl = PageHelpers.Lbl("● Ping kontrol ediliyor…", 11, "#2EB82E", true);
            badge.Child = badgeLbl; Grid.SetColumn(badge,1); hdr.Children.Add(badge);
            sp.Children.Add(hdr);
            var panel = new StackPanel { Margin=new Thickness(0,16,0,0) };
            sp.Children.Add(panel);
            Content = new ScrollViewer { Content=sp, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            _ = LoadAsync(panel, badgeLbl);
        }

        static async Task LoadAsync(StackPanel panel, TextBlock badge)
        {
            var tasks = App.Servers.Select(async srv => {
                var (on,pl,mx,ping) = await McPing.PingAsync(srv.Ip, srv.Port);
                return (srv,on,pl,mx,ping);
            });
            var res = await Task.WhenAll(tasks);
            panel.Dispatcher.Invoke(() => {
                badge.Text = $"● {res.Count(r=>r.on)} Sunucu Aktif";
                panel.Children.Clear();
                foreach(var (srv,on,pl,mx,ping) in res)
                {
                    var card = new Border { Background=PageHelpers.HexBrush("#181818"), CornerRadius=new CornerRadius(10),
                        Margin=new Thickness(0,5,0,5), BorderBrush=PageHelpers.HexBrush(srv.Color), BorderThickness=new Thickness(1,0,0,0) };
                    var g = new Grid { Margin=new Thickness(16,12,16,12) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                    var ic = PageHelpers.Lbl(srv.Icon, 22, "#FFF"); ic.Margin=new Thickness(0,0,14,0);
                    Grid.SetColumn(ic,0); g.Children.Add(ic);
                    var info = new StackPanel();
                    info.Children.Add(PageHelpers.Lbl(srv.Name,14,"#FFF",true));
                    info.Children.Add(PageHelpers.Lbl($"{srv.Mode}  |  {srv.Ver}",11,"#A0A0A0"));
                    Grid.SetColumn(info,1); g.Children.Add(info);
                    var stat = new StackPanel { HorizontalAlignment=HorizontalAlignment.Right };
                    stat.Children.Add(PageHelpers.Lbl(on?$"● {pl}/{mx}":"● Kapalı",12,on?"#2EB82E":"#FF4B4B",true));
                    if(on) stat.Children.Add(PageHelpers.Lbl($"{ping}ms",11,"#A0A0A0"));
                    Grid.SetColumn(stat,2); g.Children.Add(stat);
                    card.Child=g; panel.Children.Add(card);
                }
            });
        }
    }

    // Friends Page - MQTT Relay
    public class FriendsPage : Page
    {
        readonly MainWindow _main;
        StackPanel _savedList = null!;
        StackPanel _onlineList = null!;
        TextBlock  _badge = null!;
        Border     _bdgBorder = null!;
        TextBlock  _tunnelLbl = null!;
        TextBox    _codeEntry = null!;
        TextBox    _portEntry = null!;
        TextBox    _myCodeBox = null!;
        Button     _tunnelBtn = null!;
        System.Windows.Threading.DispatcherTimer _timer = null!;
        StackPanel _visualMapContainer = null!;
        Border     _statusBanner = null!;
        Button     _pingCheckBtn = null!;
        TextBlock  _pingStatusLbl = null!;
        ComboBox   _gatewayCombo = null!;
        TextBox    _consoleBox = null!;
        TextBox    _subdomainEntry = null!;

        public FriendsPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };

            // Header
            var hdr = new Grid();
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.Children.Add(PageHelpers.Lbl("Arkadaslar", 24, "#FFFFFF", true));
            _bdgBorder = new Border { Background = PageHelpers.HexBrush("#1a1a2e"),
                BorderBrush = PageHelpers.HexBrush("#A349A4"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6, 12, 6),
                VerticalAlignment = VerticalAlignment.Center };
            _badge = PageHelpers.Lbl("Baglaniyor...", 11, "#A349A4", true);
            _bdgBorder.Child = _badge;
            Grid.SetColumn(_bdgBorder, 1); hdr.Children.Add(_bdgBorder);
            sp.Children.Add(hdr);

            // Room code card
            var codeCard = PageHelpers.Card("#181818", 12, "#333", new Thickness(0, 16, 0, 0));
            var codeSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            codeSp.Children.Add(PageHelpers.Lbl("Benim Oda Kodum (IP paylasılmaz):", 11, "#A0A0A0", true));
            var codeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            _myCodeBox = new TextBox {
                IsReadOnly = true, Width = 180, Height = 42,
                Background = PageHelpers.HexBrush("#111"), Foreground = Brushes.White,
                FontSize = 22, FontWeight = FontWeights.Bold,
                BorderBrush = PageHelpers.HexBrush("#00A3FF"), BorderThickness = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 0, 10, 0),
                Text = _main.Relay?.RoomCode ?? "......" };
            var copyBtn = PageHelpers.MkBtn("Kopyala", "#00A3FF", 100);
            copyBtn.Margin = new Thickness(10, 0, 0, 0);
            copyBtn.Click += (_, _) => {
                if (_myCodeBox.Text.Length == 6) {
                    Clipboard.SetText(_myCodeBox.Text);
                    _badge.Text = "Kod panoya kopyalandi!";
                }
            };
            codeRow.Children.Add(_myCodeBox); codeRow.Children.Add(copyBtn);
            codeSp.Children.Add(codeRow);
            codeSp.Children.Add(PageHelpers.Lbl("Bu kodu arkadasina Discord/mesaj ile gonder", 10, "#555"));
            codeSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 14, 0, 14) });

            // Add friend by code
            codeSp.Children.Add(PageHelpers.Lbl("Arkadas Kodu Ekle:", 11, "#A0A0A0", true));
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            _codeEntry = PageHelpers.DarkTextBox("6 haneli kod (ornek: A3F8C2)", 38);
            _codeEntry.Width = 260;
            var addBtn = PageHelpers.MkBtn("Ekle", "#2EB82E", 80);
            addBtn.Margin = new Thickness(10, 0, 0, 0);
            addBtn.Click += (_, _) => AddFriendByCode();
            _codeEntry.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddFriendByCode(); };
            addRow.Children.Add(_codeEntry); addRow.Children.Add(addBtn);
            codeSp.Children.Add(addRow);
            codeSp.Children.Add(PageHelpers.Lbl("Arkadasinin launcher'inda gorunen 6 haneli kodu gir", 10, "#555"));
            codeCard.Child = codeSp; sp.Children.Add(codeCard);

            // Saved friends
            sp.Children.Add(PageHelpers.Lbl("KAYITLI ARKADASLAR", 11, "#666666", true));
            _savedList = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            sp.Children.Add(_savedList);

            // Online players
            sp.Children.Add(PageHelpers.Lbl("CANLI / CEVRIMICI", 11, "#666666", true));
            _onlineList = new StackPanel { Margin = new Thickness(0, 6, 0, 16) };
            sp.Children.Add(_onlineList);

            // Tunnel card - Connection guide
            var tunCard = PageHelpers.Card("#111625", 12, "#00A3FF", new Thickness(0, 8, 0, 20));
            var tunSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            
            // Header with vibrant icon and styling
            var tunHdr = new Grid();
            tunHdr.ColumnDefinitions.Add(new ColumnDefinition());
            tunHdr.Children.Add(PageHelpers.Lbl("🌐 MİSTİK ULTRA P2P SUNUCU PAYLAŞMA MOTORU", 14, "#00A3FF", bold: true));
            tunSp.Children.Add(tunHdr);
            
            tunSp.Children.Add(PageHelpers.Lbl("Minecraft LAN dünyanızı hiçbir port açma veya IP paylaşma derdi olmadan anında internetteki arkadaşlarınıza açın.", 11, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 12)));

            // Visual Connection Flow Map Container
            _visualMapContainer = new StackPanel { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 14) 
            };
            tunSp.Children.Add(_visualMapContainer);

            // Step-by-step 2-Column Guide
            var guideGrid = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition());
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition());
            
            // Col 0: Tünel Kurucusu (Sunucu Açan)
            var ownerCard = PageHelpers.Card("#11131a", 8, "#28a745");
            var ownerSp = new StackPanel { Margin = new Thickness(14) };
            ownerSp.Children.Add(PageHelpers.Lbl("🎮 Sunucu Kurucusu (Ev Sahibi)", 12, "#2EB82E", bold: true));
            ownerSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 6, 0, 8) });
            var ownerSteps = new[] {
                "1. Minecraft'ta dünyanızı açın.",
                "2. ESC menüsünden 'Yerel Ağda Paylaş'a basın.",
                "3. Sohbette görünen 5 haneli portu yukaraya yazın.",
                "4. 'Tüneli Başlat'a basarak tüneli aktif edin."
            };
            foreach (var step in ownerSteps)
                ownerSp.Children.Add(PageHelpers.Lbl(step, 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 2, 0, 2)));
            ownerCard.Child = ownerSp;
            Grid.SetColumn(ownerCard, 0); guideGrid.Children.Add(ownerCard);
            
            // Col 2: Katılımcı (Arkadaş)
            var joinerCard = PageHelpers.Card("#11131a", 8, "#00A3FF");
            var joinerSp = new StackPanel { Margin = new Thickness(14) };
            joinerSp.Children.Add(PageHelpers.Lbl("👥 Katılacak Arkadaşlar", 12, "#00A3FF", bold: true));
            joinerSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 6, 0, 8) });
            var joinerSteps = new[] {
                "1. Arkadaş listesinde yeşil 'Bağlan' butonu otomatik belirir.",
                "2. 'Bağlan' butonuna basarak adresi kopyalayın.",
                "3. Minecraft > Çok Oyunculu > Doğrudan Bağlan yoluna gidin.",
                "4. Adresi yapıştırıp dünyayla bağlantıyı kurun!"
            };
            foreach (var step in joinerSteps)
                joinerSp.Children.Add(PageHelpers.Lbl(step, 10, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 2, 0, 2)));
            joinerCard.Child = joinerSp;
            Grid.SetColumn(joinerCard, 2); guideGrid.Children.Add(joinerCard);
            
            tunSp.Children.Add(guideGrid);

            // Controls section
            var portInputSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            portInputSp.Children.Add(PageHelpers.Lbl("Port:", 11, "#A0A0A0", bold: true, pad: new Thickness(0,0,6,0)));
            
            _portEntry = PageHelpers.DarkTextBox("25565", 38); _portEntry.Width = 65;
            _portEntry.HorizontalContentAlignment = HorizontalAlignment.Center;
            _portEntry.VerticalContentAlignment = VerticalAlignment.Center;
            _portEntry.FontSize = 13;
            _portEntry.FontWeight = FontWeights.Bold;
            portInputSp.Children.Add(_portEntry);

            portInputSp.Children.Add(PageHelpers.Lbl("  Tünel:", 11, "#A0A0A0", bold: true, pad: new Thickness(0,0,6,0)));
            
            _gatewayCombo = new ComboBox {
                Width = 140,
                Height = 38,
                Background = PageHelpers.HexBrush("#181818"),
                Foreground = Brushes.White,
                BorderBrush = PageHelpers.HexBrush("#282828"),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            _gatewayCombo.Items.Add("Serveo.net (Özel Adres)");
            _gatewayCombo.Items.Add("Localhost.run (Yedek)");
            _gatewayCombo.SelectedIndex = 0;
            portInputSp.Children.Add(_gatewayCombo);

            portInputSp.Children.Add(PageHelpers.Lbl("  İsim (Özel):", 11, "#A0A0A0", bold: true, pad: new Thickness(0,0,6,0)));
            
            _subdomainEntry = PageHelpers.DarkTextBox("mistik" + (_main.Relay?.RoomCode.ToLower() ?? "oda"), 38);
            _subdomainEntry.Width = 110;
            _subdomainEntry.FontSize = 12;
            _subdomainEntry.ToolTip = "Serveo için özel alt alan adı (subdomain) girin. Örn: mustacraft";
            portInputSp.Children.Add(_subdomainEntry);
            
            _tunnelBtn = PageHelpers.MkBtn("🚀 Tüneli Başlat ve Buluta Aç", "#28a745", 200);
            _tunnelBtn.Margin = new Thickness(10, 0, 0, 0);
            _tunnelBtn.Click += (_, _) => ToggleTunnel();
            portInputSp.Children.Add(_tunnelBtn);
            
            tunSp.Children.Add(portInputSp);
            
            tunSp.Children.Add(PageHelpers.Lbl("Tünel adresi tünel açıldığında arkadaşlarınızın ekranında yeşil 'Bağlan' butonu olarak otomatik gözükür.", 10, "#555555", pad: new Thickness(0, 6, 0, 0)));

            // Canlı Log Konsolu
            _consoleBox = new TextBox {
                IsReadOnly = true,
                Height = 110,
                Background = PageHelpers.HexBrush("#08080c"),
                Foreground = PageHelpers.HexBrush("#39FF14"), // Neon yeşili
                BorderBrush = PageHelpers.HexBrush("#1d2d44"),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(8),
                Text = "🖥️ Mistik Tünel Log Konsolu hazır.\nTüneli başlattığınızda SSH bağlantı adımları burada anlık listelenecektir...\n"
            };
            tunSp.Children.Add(_consoleBox);

            // Real-time server status banner
            _statusBanner = new Border {
                Background = PageHelpers.HexBrush("#0d1b2a"),
                BorderBrush = PageHelpers.HexBrush("#00A3FF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 14, 0, 0),
                Visibility = Visibility.Collapsed
            };
            
            var bannerSp = new StackPanel();
            bannerSp.Children.Add(PageHelpers.Lbl("🔗 BULUT SUNUCU ADRESİ (Arkadaşlarınızla Paylaşın):", 10, "#00A3FF", bold: true));
            
            var addrRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 8) };
            _tunnelLbl = PageHelpers.Lbl("Bağlanıyor...", 14, "#2EB82E", bold: true);
            
            var copyAddrBtn = PageHelpers.MkBtn("📋 Adresi Kopyala", "#00A3FF", 120);
            copyAddrBtn.Margin = new Thickness(14, 0, 0, 0);
            copyAddrBtn.Click += (_, _) => {
                if (!string.IsNullOrEmpty(_tunnelLbl.Text) && _tunnelLbl.Text != "Baglanamadi." && _tunnelLbl.Text != "Bağlanıyor...") {
                    Clipboard.SetText(_tunnelLbl.Text);
                    MessageBox.Show("Bulut adresi panoya kopyalandı! Arkadaşlarınıza gönderebilirsiniz.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            addrRow.Children.Add(_tunnelLbl);
            addrRow.Children.Add(copyAddrBtn);
            bannerSp.Children.Add(addrRow);
            
            bannerSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#1d2d44"), Margin = new Thickness(0, 8, 0, 8) });
            bannerSp.Children.Add(PageHelpers.Lbl("🔍 GERÇEK ZAMANLI SUNUCU KONTROLÜ:", 10, "#A0A0A0", bold: true));
            
            var pingRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _pingCheckBtn = PageHelpers.MkBtn("🔍 Sunucu Durumunu Test Et", "#2EB82E", 180);
            _pingStatusLbl = PageHelpers.Lbl("Test etmek için butona basın.", 11, "#A0A0A0");
            _pingStatusLbl.Margin = new Thickness(12, 0, 0, 0);
            
            _pingCheckBtn.Click += async (_, _) => {
                _pingCheckBtn.IsEnabled = false;
                _pingStatusLbl.Text = "Sunucuya ping atılıyor...";
                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                try {
                    var addr = _tunnelLbl.Text.Trim();
                    if (!string.IsNullOrEmpty(addr)) {
                        string host = addr;
                        int port = 25565;
                        if (addr.Contains(":")) {
                            var parts = addr.Split(':');
                            host = parts[0];
                            if (parts.Length > 1) {
                                int.TryParse(parts[1], out port);
                            }
                        }
                        var (online, players, max, ping) = await McPing.PingAsync(host, port);
                        if (online) {
                            if (ping < 80) {
                                _pingStatusLbl.Text = $"⚡ MÜKEMMEL | Oyuncular: {players}/{max} | Gecikme: {ping}ms";
                                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#39FF14"); // Neon Yeşil
                            } else if (ping < 180) {
                                _pingStatusLbl.Text = $"📶 ORTA DERECE | Oyuncular: {players}/{max} | Gecikme: {ping}ms";
                                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100"); // Neon Sarı/Turuncu
                            } else {
                                _pingStatusLbl.Text = $"🐢 YÜKSEK GECİKME | Oyuncular: {players}/{max} | Gecikme: {ping}ms";
                                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B"); // Kırmızı
                            }
                        } else {
                            _pingStatusLbl.Text = "❌ KAPALI! Oyun içi LAN paylaşımını açtığınızdan emin olun.";
                            _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                        }
                    } else {
                        _pingStatusLbl.Text = "Geçersiz tünel adresi.";
                        _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                    }
                } catch (Exception ex) {
                    _pingStatusLbl.Text = $"Hata: {ex.Message}";
                    _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                } finally {
                    _pingCheckBtn.IsEnabled = true;
                }
            };
            
            pingRow.Children.Add(_pingCheckBtn);
            pingRow.Children.Add(_pingStatusLbl);
            bannerSp.Children.Add(pingRow);
            _statusBanner.Child = bannerSp;
            tunSp.Children.Add(_statusBanner);

            tunCard.Child = tunSp; sp.Children.Add(tunCard);

            // Initialize connection flow map state based on active tunnel
            if (_main.Relay?.TunnelAddress != null)
            {
                _tunnelBtn.Content = "🛑 Tüneli Durdur";
                _tunnelBtn.Background = PageHelpers.HexBrush("#FF4B4B");
                _tunnelLbl.Text = _main.Relay.TunnelAddress;
                _statusBanner.Visibility = Visibility.Visible;
                UpdateVisualMap(2);
            }
            else
            {
                UpdateVisualMap(0);
            }

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            RenderSaved();
            RenderOnline();

            // Relay event - once
            if (_main.Relay != null)
            {
                _myCodeBox.Text = _main.Relay.RoomCode;
                _main.Relay.OnUpdate -= OnRelayUpdate;
                _main.Relay.OnUpdate += OnRelayUpdate;
                _main.Relay.OnFriendRequestReceived -= OnFriendRequestReceived;
                _main.Relay.OnFriendRequestReceived += OnFriendRequestReceived;
                _main.Relay.OnFriendRequestAccepted -= OnFriendRequestAccepted;
                _main.Relay.OnFriendRequestAccepted += OnFriendRequestAccepted;
                _main.Relay.OnTunnelLog -= OnTunnelLog;
                _main.Relay.OnTunnelLog += OnTunnelLog;
            }

            // Timer: her 4 saniye badge + refresh
            _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += (_, _) => {
                UpdateBadge();
                if (_myCodeBox.Text == "......" && _main.Relay?.RoomCode != null)
                    _myCodeBox.Text = _main.Relay.RoomCode;
                RenderOnline();
            };
            _timer.Start();

            Unloaded += (_, _) => {
                _timer.Stop();
                if (_main.Relay != null)
                {
                    _main.Relay.OnUpdate -= OnRelayUpdate;
                    _main.Relay.OnFriendRequestReceived -= OnFriendRequestReceived;
                    _main.Relay.OnFriendRequestAccepted -= OnFriendRequestAccepted;
                    _main.Relay.OnTunnelLog -= OnTunnelLog;
                }
            };

            UpdateBadge();
        }

        void OnRelayUpdate(List<PeerInfo> players) => Dispatcher.BeginInvoke(() => RenderOnline(players));

        void UpdateBadge()
        {
            var relay = _main.Relay;
            if (relay == null) {
                _badge.Text = "Relay yok"; _badge.Foreground = PageHelpers.HexBrush("#FF4B4B");
                _bdgBorder.BorderBrush = PageHelpers.HexBrush("#FF4B4B");
            } else if (relay.Connected) {
                _badge.Text = "MQTT Aktif - IP Yok";
                _badge.Foreground = PageHelpers.HexBrush("#2EB82E");
                _bdgBorder.Background = PageHelpers.HexBrush("#122c1b");
                _bdgBorder.BorderBrush = PageHelpers.HexBrush("#2EB82E");
            } else {
                _badge.Text = "Baglaniyor...";
                _badge.Foreground = PageHelpers.HexBrush("#FFB100");
                _bdgBorder.BorderBrush = PageHelpers.HexBrush("#FFB100");
            }
        }

        void RenderSaved()
        {
            _savedList.Children.Clear();
            var codes = _main.Config.FriendCodes;
            var names = _main.Config.Friends;

            var entries = new System.Collections.Generic.List<(string code, string name)>();
            for (int i = 0; i < codes.Count; i++)
                entries.Add((codes[i], i < names.Count ? names[i] : "?"));
            foreach (var n in names)
                if (!entries.Any(x => x.name == n))
                    entries.Add(("", n));

            if (!entries.Any()) {
                _savedList.Children.Add(PageHelpers.Lbl("Henuz arkadasin yok. Yukardaki kutudan kod ekle.", 12, "#555"));
                return;
            }

            foreach (var (code, name) in entries)
            {
                var card = new Border { Background = PageHelpers.HexBrush("#1a2a1a"),
                    CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 3, 0, 3) };
                var g = new Grid { Margin = new Thickness(14, 10, 14, 10) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var img = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
                _ = SkinPage.LoadImgAsync(img, name == "?" ? "Steve" : name, 32);
                Grid.SetColumn(img, 0); g.Children.Add(img);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(PageHelpers.Lbl(name == "?" ? $"[{code}]" : name, 13, "#FFF", true));
                if (!string.IsNullOrEmpty(code))
                    info.Children.Add(PageHelpers.Lbl($"Kod: {code}", 10, "#A0A0A0"));
                Grid.SetColumn(info, 1); g.Children.Add(info);

                var cn = name; var cc = code;
                var delBtn = PageHelpers.MkBtn("Sil", "#FF4B4B", 55);
                delBtn.Click += (_, _) => { RemoveFriend(cc, cn); RenderSaved(); RenderOnline(); };
                Grid.SetColumn(delBtn, 2); g.Children.Add(delBtn);
                card.Child = g; _savedList.Children.Add(card);
            }
        }

        void RenderOnline(List<PeerInfo>? players = null)
        {
            players ??= _main.Relay?.GetOnlinePlayers() ?? new();
            _onlineList.Children.Clear();
            var codes = _main.Config.FriendCodes;
            var names = _main.Config.Friends;
            var friends = players.Where(p => codes.Contains(p.RoomCode) || names.Contains(p.User)).ToList();
            var others  = players.Where(p => !friends.Contains(p)).ToList();

            if (!players.Any()) {
                _onlineList.Children.Add(PageHelpers.Lbl("Simdilik cevrimici kimse yok.", 12, "#555"));
                return;
            }
            if (friends.Any()) {
                _onlineList.Children.Add(PageHelpers.Lbl($"Arkadaslar ({friends.Count})", 11, "#2EB82E", true));
                foreach (var p in friends) AddOnlineCard(p, true);
            }
            if (others.Any()) {
                _onlineList.Children.Add(PageHelpers.Lbl($"Diger Cevrimici ({others.Count})", 11, "#666666", true));
                foreach (var p in others) AddOnlineCard(p, false);
            }
        }

        void AddOnlineCard(PeerInfo p, bool isFriend)
        {
            var card = new Border { Background = PageHelpers.HexBrush(isFriend ? "#1e3a2f" : "#1a1a1a"),
                CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 3, 0, 3) };
            var g = new Grid { Margin = new Thickness(14, 10, 14, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var img = new Image { Width = 36, Height = 36, Margin = new Thickness(0, 0, 12, 0) };
            _ = SkinPage.LoadImgAsync(img, p.User, 36);
            Grid.SetColumn(img, 0); g.Children.Add(img);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(PageHelpers.Lbl(p.User, 13, "#FFF", true));
            var det = $"Kod: {p.RoomCode}";
            if (!string.IsNullOrEmpty(p.Ver))    det += $"  |  MC {p.Ver}";
            if (!string.IsNullOrEmpty(p.Status)) det += $"  |  {p.Status}";
            info.Children.Add(PageHelpers.Lbl(det, 10, "#A0A0A0"));
            if (!string.IsNullOrEmpty(p.Tunnel))
                info.Children.Add(PageHelpers.Lbl($"Tunel: {p.Tunnel}", 10, "#2EB82E", true));
            Grid.SetColumn(info, 1); g.Children.Add(info);

            var acts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!isFriend) {
                var btn = PageHelpers.MkBtn("Ekle", "#00A3FF", 70);
                btn.Click += (_, _) => { QuickAdd(p.RoomCode, p.User); };
                acts.Children.Add(btn);
            } else {
                var delBtn = PageHelpers.MkBtn("Sil", "#FF4B4B", 55);
                delBtn.Click += (_, _) => { RemoveFriend(p.RoomCode, p.User); };
                acts.Children.Add(delBtn);
                if (!string.IsNullOrEmpty(p.Tunnel)) {
                    var joinBtn = PageHelpers.MkBtn("Baglan", "#2EB82E", 70);
                    joinBtn.Margin = new Thickness(0, 4, 0, 0);
                    joinBtn.Click += (_, _) => {
                        Clipboard.SetText(p.Tunnel!);
                        MessageBox.Show($"Sunucu:\n{p.Tunnel}\n(Panoya kopyalandi)\n\nMinecraft Cok Oyunculu > Sunucu Ekle",
                            "Baglan", MessageBoxButton.OK, MessageBoxImage.Information);
                    };
                    acts.Children.Add(joinBtn);
                }
            }
            Grid.SetColumn(acts, 2); g.Children.Add(acts);
            card.Child = g; _onlineList.Children.Add(card);
        }

        void AddFriendByCode()
        {
            var code = _codeEntry.Text.Trim().ToUpper().Replace(" ", "");
            if (code.Length != 6) {
                MessageBox.Show("Lutfen 6 haneli oda kodunu girin. Ornek: A3F8C2", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_main.Relay != null && code == _main.Relay.RoomCode) {
                MessageBox.Show("Kendi kodunuzu ekleyemezsiniz!", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _codeEntry.Clear();
            _ = Task.Run(async () => {
                if (_main.Relay != null) {
                    await _main.Relay.SendFriendRequestAsync(code);
                    Dispatcher.Invoke(() => {
                        MessageBox.Show($"Oda koduna ({code}) gercek zamanli arkadaslik istegi gonderildi!\nArkadasiniz onayladiginda listenize eklenecektir.",
                            "Istek Gonderildi", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            });
        }

        void OnFriendRequestReceived(string fromUser, string fromCode)
        {
            Dispatcher.BeginInvoke(() => {
                var res = MessageBox.Show(
                    $"{fromUser} (Oda Kodu: {fromCode}) size arkadaslik istegi gonderdi!\n\nKabul ediyor musunuz?",
                    "Arkadaslik Istegi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _ = Task.Run(async () => {
                        if (_main.Relay != null) {
                            await _main.Relay.AcceptFriendRequestAsync(fromCode);
                        }
                    });
                    if (!_main.Config.FriendCodes.Contains(fromCode)) _main.Config.FriendCodes.Add(fromCode);
                    if (!_main.Config.Friends.Contains(fromUser)) _main.Config.Friends.Add(fromUser);
                    ConfigManager.Save(_main.Config);
                    RenderSaved(); RenderOnline();
                    MessageBox.Show($"{fromUser} ile artik arkadassiniz!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        void OnFriendRequestAccepted(string fromUser, string fromCode)
        {
            Dispatcher.BeginInvoke(() => {
                if (!_main.Config.FriendCodes.Contains(fromCode)) _main.Config.FriendCodes.Add(fromCode);
                if (!_main.Config.Friends.Contains(fromUser)) _main.Config.Friends.Add(fromUser);
                ConfigManager.Save(_main.Config);
                RenderSaved(); RenderOnline();
                MessageBox.Show($"{fromUser} arkadaslik isteginizi kabul etti!", "Istek Kabul Edildi", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        void QuickAdd(string code, string user)
        {
            if (!string.IsNullOrEmpty(code) && !_main.Config.FriendCodes.Contains(code)) _main.Config.FriendCodes.Add(code);
            if (!string.IsNullOrEmpty(user) && !_main.Config.Friends.Contains(user)) _main.Config.Friends.Add(user);
            ConfigManager.Save(_main.Config);
            _ = Task.Run(async () => {
                if (_main.Relay != null && !string.IsNullOrEmpty(code)) {
                    await _main.Relay.SendFriendRequestAsync(code);
                }
            });
            RenderSaved();
            RenderOnline();
        }

        void RemoveFriend(string code, string name)
        {
            if (!string.IsNullOrEmpty(code))
                _main.Config.FriendCodes.Remove(code);
            if (!string.IsNullOrEmpty(name))
                _main.Config.Friends.Remove(name);
            ConfigManager.Save(_main.Config);
            RenderSaved();
            RenderOnline();
        }

        void OnTunnelLog(string log)
        {
            Dispatcher.BeginInvoke(() => {
                if (_consoleBox != null)
                {
                    _consoleBox.AppendText(log + "\n");
                    _consoleBox.ScrollToEnd();
                }
            });
        }

        void ToggleTunnel()
        {
            var relay = _main.Relay;
            if (relay == null) { MessageBox.Show("Relay baglanti yok.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (relay.TunnelAddress != null) {
                relay.StopTunnel();
                _tunnelBtn.Content = "🚀 Tüneli Başlat ve Buluta Aç"; 
                _tunnelBtn.Background = PageHelpers.HexBrush("#28a745");
                _statusBanner.Visibility = Visibility.Collapsed;
                _tunnelLbl.Text = ""; 
                if (_consoleBox != null) _consoleBox.Text = "🖥️ Mistik Tünel Log Konsolu sıfırlandı.\nTüneli başlattığınızda loglar burada görünecektir...\n";
                UpdateVisualMap(0);
                return;
            }
            int.TryParse(_portEntry.Text.Trim(), out var port); if (port == 0) port = 25565;
            
            // Get selected gateway name
            string gateway = "serveo.net";
            if (_gatewayCombo.SelectedIndex == 1) {
                gateway = "localhost.run";
            }

            if (_consoleBox != null) _consoleBox.Text = $"🖥️ Tünel başlatılıyor (Seçilen Sunucu: {gateway})...\n";
            _tunnelBtn.IsEnabled = false; 
            _tunnelBtn.Content = "⌛ Bağlanıyor...";
            _tunnelBtn.Background = PageHelpers.HexBrush("#FFB100");
            _statusBanner.Visibility = Visibility.Collapsed;
            
            UpdateVisualMap(1);
            
            relay.OnTunnelReady -= OnTunnelReady;
            relay.OnTunnelReady += OnTunnelReady;
            
            string customSub = _subdomainEntry != null ? _subdomainEntry.Text.Trim() : "";
            relay.StartTunnel(port, gateway, customSub);
            
            _ = Task.Delay(15000).ContinueWith(_ => Dispatcher.BeginInvoke(() => {
                if (relay.TunnelAddress == null) {
                    _tunnelBtn.IsEnabled = true; 
                    _tunnelBtn.Content = "🚀 Tüneli Başlat ve Buluta Aç";
                    _tunnelBtn.Background = PageHelpers.HexBrush("#28a745");
                    UpdateVisualMap(0);
                }
            }));
        }

        void OnTunnelReady(string? addr)
        {
            Dispatcher.BeginInvoke(() => {
                _tunnelBtn.IsEnabled = true;
                if (string.IsNullOrEmpty(addr)) {
                    _tunnelBtn.Content = "🚀 Tüneli Başlat ve Buluta Aç"; 
                    _tunnelBtn.Background = PageHelpers.HexBrush("#28a745");
                    _statusBanner.Visibility = Visibility.Collapsed;
                    UpdateVisualMap(0);
                } else {
                    _tunnelBtn.Content = "🛑 Tüneli Durdur"; 
                    _tunnelBtn.Background = PageHelpers.HexBrush("#FF4B4B");
                    _tunnelLbl.Text = addr;
                    _statusBanner.Visibility = Visibility.Visible;
                    
                    _pingStatusLbl.Text = "Test etmek için butona basın.";
                    _pingStatusLbl.Foreground = PageHelpers.HexBrush("#A0A0A0");
                    
                    UpdateVisualMap(2);
                    Clipboard.SetText(addr);
                    MessageBox.Show($"TÜNEL AKTİF!\n\n{addr}\n\n(Panoya kopyalandı)\n\nMinecraft > Çok Oyunculu > Doğrudan Bağlan veya Sunucu Ekle kısmına yapıştırın.",
                        "Tünel Hazır", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        // Visual flow node helpers
        Border Node(string name, string sub, string color, bool active)
        {
            var card = PageHelpers.Card(active ? "#11221b" : "#141414", 8, active ? color : "#282828", new Thickness(0));
            card.Width = 110;
            var sp = new StackPanel { Margin = new Thickness(8, 6, 8, 6), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(PageHelpers.Lbl(name, 10, active ? "#FFFFFF" : "#888888", bold: true));
            sp.Children.Add(PageHelpers.Lbl(sub, 9, active ? color : "#555555"));
            card.Child = sp;
            return card;
        }

        UIElement Arrow(bool active)
        {
            var tb = PageHelpers.Lbl("➔", 12, active ? "#2EB82E" : "#444444", bold: true, pad: new Thickness(4, 0, 4, 0));
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }

        void UpdateVisualMap(int state)
        {
            if (_visualMapContainer == null) return;
            _visualMapContainer.Children.Clear();
            
            // Node 1: Bilgisayarım
            var n1 = Node("Bilgisayarım", $"Port: {(_portEntry != null ? _portEntry.Text : "25565")}", "#00A3FF", true);
            _visualMapContainer.Children.Add(n1);
            
            // Arrow 1
            _visualMapContainer.Children.Add(Arrow(state >= 1));
            
            // Node 2: SSH Tüneli
            var n2 = Node("Mistik Tünel", state == 1 ? "Kuruluyor..." : (state == 2 ? "Aktif" : "Beklemede"), state == 1 ? "#FFB100" : (state == 2 ? "#2EB82E" : "#888888"), state >= 1);
            _visualMapContainer.Children.Add(n2);
            
            // Arrow 2
            _visualMapContainer.Children.Add(Arrow(state == 2));
            
            // Node 3: Bulut Sunucu
            var n3 = Node("Serveo Ağ Geçidi", state == 2 ? "Bağlantı OK" : "Beklemede", state == 2 ? "#2EB82E" : "#888888", state == 2);
            _visualMapContainer.Children.Add(n3);
            
            // Arrow 3
            _visualMapContainer.Children.Add(Arrow(state == 2));
            
            // Node 4: Oyuncular
            var n4 = Node("Arkadaşlar", state == 2 ? "Katılabilir" : "Beklemede", state == 2 ? "#2EB82E" : "#888888", state == 2);
            _visualMapContainer.Children.Add(n4);
        }
    }

    // ─── Changelog Page ───────────────────────────────────────────────────────
    public class ChangelogPage : Page
    {
        public ChangelogPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin=new Thickness(40,30,40,30) };
            sp.Children.Add(PageHelpers.Lbl("📜  Son Güncellemeler",24,"#FFF",true));
            sp.Children.Add(PageHelpers.Lbl($"Mevcut Sürüm: {App.LocalVersion}",12,"#A0A0A0"));
            foreach(var e in App.Changelog)
            {
                var card = new Border { Background=PageHelpers.HexBrush("#181818"), CornerRadius=new CornerRadius(12),
                    Margin=new Thickness(0,12,0,0), BorderBrush=PageHelpers.HexBrush(e.Color), BorderThickness=new Thickness(1) };
                var cSp = new StackPanel();
                var tBar = new Border { Background=new SolidColorBrush(Color.FromArgb(25,PageHelpers.HexColor(e.Color).R,PageHelpers.HexColor(e.Color).G,PageHelpers.HexColor(e.Color).B)) };
                var tG = new Grid { Margin=new Thickness(16,10,16,10) };
                tG.ColumnDefinitions.Add(new ColumnDefinition()); tG.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                tG.Children.Add(PageHelpers.Lbl(e.Ver,16,e.Color,true));
                var dl = PageHelpers.Lbl(e.Date,12,"#A0A0A0"); Grid.SetColumn(dl,1); tG.Children.Add(dl);
                tBar.Child=tG; cSp.Children.Add(tBar);
                foreach(var item in e.Items)
                {
                    var iRow = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(16,3,16,3) };
                    iRow.Children.Add(PageHelpers.Lbl("●",11,e.Color));
                    iRow.Children.Add(PageHelpers.Lbl("  "+item,12,"#CCC",wrap:TextWrapping.Wrap));
                    cSp.Children.Add(iRow);
                }
                cSp.Children.Add(new StackPanel { Height=8 });
                card.Child=cSp; sp.Children.Add(card);
            }
            Content = new ScrollViewer { Content=sp, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        }
    }
}
