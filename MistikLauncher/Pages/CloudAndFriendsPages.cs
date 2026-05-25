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

namespace MistikLauncher.Pages
{
    // ─── Skin Page ────────────────────────────────────────────────────────────
    public class SkinPage : Page
    {
        public SkinPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40,30,40,30) };
            sp.Children.Add(PageHelpers.Lbl("🎨  Karakter (Skin) Odasi", 24, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("Kullanici adi arayip canlı önizleyerek veya bilgisayarinizdan .png skin yukleyerek karakterinizi degistirin", 12, "#A0A0A0"));

            // Premium Skin Changer Info Card
            var skinInfoCard = PageHelpers.Card("#0f1b29", 10, "#FFB100", new Thickness(0, 12, 0, 12));
            var skinInfoSp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            skinInfoSp.Children.Add(PageHelpers.Lbl("💡 Karakter Değişikliği Hakkında Önemli Bilgiler", 13, "#FFB100", bold: true));
            skinInfoSp.Children.Add(PageHelpers.Lbl("• Oyununuz Açıkken Değiştirme: Oyun açıkken skin değiştirdiyseniz, oyun içinde F3 + T tuşlarına basarak kaynak paketlerini yenileyin veya oyunu yeniden başlatın.", 10, "#CCCCCC", wrap: TextWrapping.Wrap));
            skinInfoSp.Children.Add(PageHelpers.Lbl("• Çok Oyunculu Sunucular: Özel skin eklentisi (SkinsRestorer vb.) olan sunucularda sunucu taraflı skin sistemi geçerlidir. Mistik Skin Sistemi, Tek Oyunculu dünyalarda ve normal yerel ağ sunucularında çalışır.", 10, "#CCCCCC", wrap: TextWrapping.Wrap));
            skinInfoSp.Children.Add(PageHelpers.Lbl("• Paket Kontrolü: Oyun içinde Ayarlar > Kaynak Paketleri menüsünden 'Mistik Launcher Ozel Skin' paketinin aktif ve listede en üstte olduğundan emin olun.", 10, "#CCCCCC", wrap: TextWrapping.Wrap));
            skinInfoCard.Child = skinInfoSp;
            sp.Children.Add(skinInfoCard);

            // Dual card grid
            var skinTypeGrid = new Grid { Margin = new Thickness(0, 16, 0, 20) };
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            skinTypeGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Col 0: Search & Live Preview Card
            var searchCard = PageHelpers.Card("#181818", 12);
            var searchSp = new StackPanel { Margin = new Thickness(20) };
            searchSp.Children.Add(PageHelpers.Lbl("🔍 Karakter Arama & Canlı Önizleme", 14, "#00A3FF", true));
            searchSp.Children.Add(PageHelpers.Lbl("Premium oyuncu adı yazarak karakteri aratın ve önizleyin:", 10, "#888", wrap: TextWrapping.Wrap));
            
            // Search Input Row
            var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var tb = PageHelpers.DarkTextBox(main.Config.User, 38); 
            tb.Width = 150;
            searchRow.Children.Add(tb);
            
            var searchBtn = PageHelpers.MkBtn("Ara & Önizle", "#00A3FF", 100);
            searchBtn.Margin = new Thickness(8, 0, 0, 0);
            searchRow.Children.Add(searchBtn);
            searchSp.Children.Add(searchRow);

            // Preview Row (Image + Details)
            var previewRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition());
            
            var previewImg = new Image { Width = 80, Height = 80, Margin = new Thickness(0, 0, 16, 0), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(previewImg, 0);
            previewRow.Children.Add(previewImg);

            var previewDetails = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock previewNameLbl;
            TextBlock previewStatusLbl;

            if (main.Config.SkinType == "local" && !string.IsNullOrEmpty(main.Config.SkinUser) && File.Exists(main.Config.SkinUser)) {
                try {
                    var bmp = new BitmapImage();
                    bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(main.Config.SkinUser);
                    bmp.EndInit(); bmp.Freeze();
                    var cropped = new CroppedBitmap(bmp, new Int32Rect(8, 8, 8, 8));
                    previewImg.Source = cropped;
                    System.Windows.Media.RenderOptions.SetBitmapScalingMode(previewImg, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                } catch { }
                previewNameLbl = PageHelpers.Lbl("Ozel Skin", 14, "#FFFFFF", true);
                previewStatusLbl = PageHelpers.Lbl("Karakter hazır. Oyuna kurmak için aşağıdaki butona basın.", 10, "#A0A0A0", wrap: TextWrapping.Wrap);
                previewDetails.Children.Add(previewNameLbl);
                previewDetails.Children.Add(previewStatusLbl);
            } else {
                string initialUser = !string.IsNullOrEmpty(main.Config.SkinUser) ? main.Config.SkinUser : (string.IsNullOrEmpty(main.Config.User) ? "Steve" : main.Config.User);
                if (initialUser.Contains("/") || initialUser.Contains("\\")) initialUser = "Steve"; // Safety check for local skin path
                _ = LoadImgAsync(previewImg, initialUser, 80);
                previewNameLbl = PageHelpers.Lbl(initialUser, 14, "#FFFFFF", true);
                previewStatusLbl = PageHelpers.Lbl("Karakter hazır. Oyuna kurmak için aşağıdaki butona basın.", 10, "#A0A0A0", wrap: TextWrapping.Wrap);
                previewDetails.Children.Add(previewNameLbl);
                previewDetails.Children.Add(previewStatusLbl);
            }
            Grid.SetColumn(previewDetails, 1);
            previewRow.Children.Add(previewDetails);
            searchSp.Children.Add(previewRow);

            // Apply Button
            var applyBtn = PageHelpers.MkBtn("✨ Karakteri Oyuna Kur", "#2EB82E");
            applyBtn.Height = 40;
            applyBtn.Margin = new Thickness(0, 16, 0, 0);
            applyBtn.Click += async (_, _) => {
                var n = tb.Text.Trim();
                if (string.IsNullOrEmpty(n)) return;
                applyBtn.IsEnabled = false;
                applyBtn.Content = "Kuruluyor...";
                try
                {
                    main.Config.User = n; main.Config.SkinType = "username"; main.Config.SkinUser = n;
                    ConfigManager.Save(main.Config); main.ReloadConfig();
                    await main.PrepareSkinPackAsync();
                    MessageBox.Show($"Karakteriniz '{n}' skini başarıyla indirildi ve kuruldu!\n\nEğer oyununuz açıksa F3 + T tuşlarına basarak kaynak paketini yenileyin.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Karakter kurulurken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    applyBtn.IsEnabled = true;
                    applyBtn.Content = "✨ Karakteri Oyuna Kur";
                }
            };
            searchSp.Children.Add(applyBtn);

            // Search action
            Action doSearch = () => {
                var n = tb.Text.Trim();
                if (string.IsNullOrEmpty(n)) return;
                previewNameLbl.Text = n;
                previewStatusLbl.Text = "Önizleme indiriliyor...";
                previewStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                _ = LoadImgAsync(previewImg, n, 80).ContinueWith(t => {
                    previewImg.Dispatcher.Invoke(() => {
                        var statusLbl = (TextBlock)previewDetails.Children[1];
                        statusLbl.Text = "Karakter önizlemesi yüklendi. Oyuna kurmaya hazır!";
                        statusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                    });
                });
            };

            searchBtn.Click += (_, _) => doSearch();
            tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) doSearch(); };

            searchCard.Child = searchSp;
            Grid.SetColumn(searchCard, 0); skinTypeGrid.Children.Add(searchCard);

            // Col 2: Custom Local Skin Card
            var localCard = PageHelpers.Card("#181818", 12);
            var localSp = new StackPanel { Margin = new Thickness(20) };
            localSp.Children.Add(PageHelpers.Lbl("📁 Bilgisayardan Ozel Skin (.png) Yukle", 14, "#2EB82E", true));
            localSp.Children.Add(PageHelpers.Lbl("Kendi indirdiginiz .png skin dosyasini oyuna kaynak paketi olarak yukleyin:", 10, "#888", wrap: TextWrapping.Wrap));
            
            var selectedPathRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var pathBox = PageHelpers.DarkTextBox(main.Config.SkinType == "local" ? main.Config.SkinUser : "Dosya secilmedi...", 38);
            pathBox.Width = 150;
            pathBox.IsReadOnly = true;
            selectedPathRow.Children.Add(pathBox);

            var chooseBtn = PageHelpers.MkBtn("Gozat", "#2EB82E", 60);
            chooseBtn.Margin = new Thickness(8, 0, 0, 0);

            var applyLocalBtn = PageHelpers.MkBtn("Uygula", "#00A3FF", 80);
            applyLocalBtn.Margin = new Thickness(8, 0, 0, 0);

            selectedPathRow.Children.Add(chooseBtn);
            selectedPathRow.Children.Add(applyLocalBtn);

            localSp.Children.Add(selectedPathRow);

            string currentLocalPath = "";

            chooseBtn.Click += (_, _) => {
                var dlg = new Microsoft.Win32.OpenFileDialog {
                    Filter = "Minecraft Skin (*.png)|*.png",
                    Title = "PNG formatindaki skin dosyanizi secin"
                };
                if (dlg.ShowDialog() == true) {
                    currentLocalPath = dlg.FileName;
                    pathBox.Text = currentLocalPath;
                }
            };

            applyLocalBtn.Click += (_, _) => {
                string targetPath = currentLocalPath;
                if (string.IsNullOrEmpty(targetPath)) {
                    if (main.Config.SkinType == "local" && File.Exists(main.Config.SkinUser))
                        targetPath = main.Config.SkinUser;
                }

                if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
                    ApplyLocalSkin(main, targetPath);
                    try {
                        var bmp = new BitmapImage();
                        bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(main.Config.SkinUser);
                        bmp.EndInit(); bmp.Freeze();
                        var cropped = new CroppedBitmap(bmp, new Int32Rect(8, 8, 8, 8));
                        previewImg.Source = cropped;
                        System.Windows.Media.RenderOptions.SetBitmapScalingMode(previewImg, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                        previewNameLbl.Text = "Ozel Skin";
                        previewStatusLbl.Text = "Karakter hazır.";
                        previewStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                    } catch { }
                    MessageBox.Show("Ozel skin basariyla oyuna kuruldu!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show("Lutfen once bir skin dosyasi secin.", "Uyari", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            localCard.Child = localSp;
            Grid.SetColumn(localCard, 2); skinTypeGrid.Children.Add(localCard);

            sp.Children.Add(skinTypeGrid);
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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
                // Kalici olarak AppData icine kopyala
                string localDest = Path.Combine(App.AppData, "custom_skin.png");
                File.Copy(filePath, localDest, true);

                var packDir = Path.Combine(App.GameDir, "resourcepacks", "MistikSkinPack");
                var textureDir = Path.Combine(packDir, "assets", "minecraft", "textures", "entity");
                Directory.CreateDirectory(textureDir);

                // Copy to steve and alex
                File.Copy(localDest, Path.Combine(textureDir, "steve.png"), true);
                File.Copy(localDest, Path.Combine(textureDir, "alex.png"), true);

                // Create pack.mcmeta
                var mcmetaPath = Path.Combine(packDir, "pack.mcmeta");
                var mcmetaContent = "{\n  \"pack\": {\n    \"pack_format\": 1,\n    \"description\": \"Mistik Launcher Ozel Skin Kaynak Paketi\"\n  }\n}";
                File.WriteAllText(mcmetaPath, mcmetaContent);

                main.Config.SkinType = "local";
                main.Config.SkinUser = localDest;
                ConfigManager.Save(main.Config); main.ReloadConfig();

                main.EnsureMistikSkinPackEnabled(true);
                main.LoadAvatar();

                MessageBox.Show("Ozel skininiz basariyla 'Mistik Ozel Skin' kaynak paketi olarak yuklendi ve aktif edildi!\n\nOyuna girdiginizde karakteriniz otomatik olarak hazir olacaktir!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
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
        TextBox    _customHostEntry = null!;
        StackPanel _subdomainRow = null!;
        StackPanel _customHostRow = null!;

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

            // ════════════════════════════════════════════════════════════════════════
            // TÜNEL KARTI — playit.gg tarzı, özgür domain sistemi
            // ════════════════════════════════════════════════════════════════════════
            var tunCard = PageHelpers.Card("#0d1117", 16, "#00A3FF", new Thickness(0, 8, 0, 20));
            var tunSp = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

            // ── Başlık ───────────────────────────────────────────────────────────────
            var hdrRow = new StackPanel { Orientation = Orientation.Horizontal };
            hdrRow.Children.Add(PageHelpers.Lbl("🌐", 22, "#00A3FF", pad: new Thickness(0, 0, 10, 0)));
            var hdrTxt = new StackPanel();
            hdrTxt.Children.Add(PageHelpers.Lbl("MİSTİK TÜNEL MOTORU", 15, "#FFFFFF", bold: true));
            hdrTxt.Children.Add(PageHelpers.Lbl("Port açmadan, IP paylaşmadan — arkadaşlarınla anında oyna", 11, "#606888"));
            hdrRow.Children.Add(hdrTxt);
            tunSp.Children.Add(hdrRow);

            // 💡 Premium Invalid Session Troubleshooter & Otomatik Koruma Paneli
            var sessionTip = PageHelpers.Card("#0e1520", 12, "#00FFCC", new Thickness(0, 10, 0, 12));
            var sessionSp = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
            
            sessionSp.Children.Add(PageHelpers.Lbl("🛡️ BAĞLANTI & 'INVALID SESSION' (GEÇERSİZ OTURUM) ÇÖZÜM REHBERİ", 13, "#00FFCC", bold: true, pad: new Thickness(0, 0, 0, 8)));
            
            // LAN Section
            var lanTitle = PageHelpers.Lbl("🎮 1. Yerel Ağ (LAN) Dünyaları İçin:", 11, "#FFFFFF", bold: true);
            var lanDesc = PageHelpers.Lbl("• Yerel Ağda Paylaş ile kurulan dünyalarda, oyunu kuran (Host) dahil TÜM oyuncular Mistik Launcher (offline/cracked) kullanmalıdır. Orijinal resmi launcher'dan giren bir host varsa, offline arkadaşlar bağlanırken 'Invalid Session' hatası alır.", 10, "#CCCCCC", wrap: TextWrapping.Wrap);
            lanDesc.Margin = new Thickness(12, 2, 0, 8);
            sessionSp.Children.Add(lanTitle); sessionSp.Children.Add(lanDesc);

            // Dedicated Servers Section
            var dedTitle = PageHelpers.Lbl("💻 2. Kendi Bireysel Sunucularınız İçin:", 11, "#FFFFFF", bold: true);
            var dedDesc = PageHelpers.Lbl("• Sunucu klasörünüzdeki 'server.properties' dosyasını açın, 'online-mode=true' satırını 'online-mode=false' olarak değiştirip kaydedin ve sunucunuzu YENİDEN BAŞLATIN.", 10, "#CCCCCC", wrap: TextWrapping.Wrap);
            dedDesc.Margin = new Thickness(12, 2, 0, 8);
            sessionSp.Children.Add(dedTitle); sessionSp.Children.Add(dedDesc);

            // Aternos Section
            var aterTitle = PageHelpers.Lbl("☁️ 3. Aternos Sunucuları İçin:", 11, "#FFFFFF", bold: true);
            var aterDesc = PageHelpers.Lbl("• Aternos panelinde Ayarlar (Options) kısmına gidin, 'Korsan (Cracked)' seçeneğini aktif (Yeşil/Açık) konuma getirin ve sunucuyu YENİDEN BAŞLATIN.", 10, "#CCCCCC", wrap: TextWrapping.Wrap);
            aterDesc.Margin = new Thickness(12, 2, 0, 8);
            sessionSp.Children.Add(aterTitle); sessionSp.Children.Add(aterDesc);

            // Automatic Protection notice
            var autoNotice = PageHelpers.Lbl("⚡ Otomatik Koruma: Mistik Tünel Motoru, tüneli başlattığınızda yerel sunucu dosyalarınızı otomatik olarak tarayıp offline moda hazırlar!", 10, "#00FFCC", bold: true);
            autoNotice.Margin = new Thickness(0, 4, 0, 0);
            sessionSp.Children.Add(autoNotice);

            sessionTip.Child = sessionSp;
            tunSp.Children.Add(sessionTip);

            tunSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#1a2040"), Margin = new Thickness(0, 8, 0, 14) });

            // ── Servis Seçici (Tab-bar tarzı) ────────────────────────────────────────
            tunSp.Children.Add(PageHelpers.Lbl("TÜNEL SERVİSİ SEÇ:", 10, "#606888", bold: true));

            var providerWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 10) };

            // Provider definitions: (label, tooltip, index)
            // Index: 0=bore.pub, 1=custom
            var providers = new[] {
                ("🎯 bore.pub",       "En güvenilir ücretsiz TCP tüneli.\nHesap gerektirmez, Minecraft için özel yapılmış gibi çalışır.\nİlk kullanımda ~3MB indirme yapar.", 0),
                ("⚙️ Özel SSH",     "Kendi VPS veya herhangi SSH tünel sunucunuz (Örn: nokey@localhost.run).\nuser@host:port formatında girin.",                                           1),
            };

            // We need a local reference array to toggle borders
            var provBorders = new Border[providers.Length];

            for (int pi = 0; pi < providers.Length; pi++)
            {
                var (lbl, tip, idx) = providers[pi];
                int capturedIdx = idx;
                bool isSelected = Math.Clamp(_main.Config.TunnelGateway, 0, 1) == idx;

                var pb = new Border {
                    Background      = PageHelpers.HexBrush(isSelected ? "#0f2040" : "#12141c"),
                    BorderBrush     = PageHelpers.HexBrush(isSelected ? "#00A3FF" : "#242838"),
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    CornerRadius    = new CornerRadius(8),
                    Margin          = new Thickness(0, 0, 8, 8),
                    Padding         = new Thickness(14, 8, 14, 8),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    ToolTip         = tip,
                };
                pb.Child = PageHelpers.Lbl(lbl, 11, isSelected ? "#00A3FF" : "#A0A0B0", bold: isSelected);
                provBorders[pi] = pb;

                pb.MouseLeftButtonUp += (_, _) => {
                    _gatewayCombo.SelectedIndex = capturedIdx;
                };
                providerWrap.Children.Add(pb);
            }

            tunSp.Children.Add(providerWrap);

            // Hidden combo to carry state (not visible, used by ToggleTunnel)
            _gatewayCombo = new ComboBox { Visibility = Visibility.Collapsed };
            _gatewayCombo.Items.Add("bore.pub");    // 0
            _gatewayCombo.Items.Add("custom");      // 1
            _gatewayCombo.SelectedIndex = Math.Clamp(_main.Config.TunnelGateway, 0, 1);
            tunSp.Children.Add(_gatewayCombo);

            // Sync provider border highlights when combo changes
            _gatewayCombo.SelectionChanged += (_, _) => {
                for (int i = 0; i < provBorders.Length; i++) {
                    bool sel = i == _gatewayCombo.SelectedIndex;
                    provBorders[i].Background      = PageHelpers.HexBrush(sel ? "#0f2040" : "#12141c");
                    provBorders[i].BorderBrush     = PageHelpers.HexBrush(sel ? "#00A3FF" : "#242838");
                    provBorders[i].BorderThickness = new Thickness(sel ? 2 : 1);
                    ((TextBlock)provBorders[i].Child).Foreground = PageHelpers.HexBrush(sel ? "#00A3FF" : "#A0A0B0");
                    ((TextBlock)provBorders[i].Child).FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
                }
                UpdateProviderRows();
            };

            // ── Port satırı ──────────────────────────────────────────────────────────
            var portRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            portRow.Children.Add(PageHelpers.Lbl("LAN PORT:", 10, "#606888", bold: true, pad: new Thickness(0, 0, 8, 0)));
            _portEntry = PageHelpers.DarkTextBox(_main.Config.TunnelPort > 0 ? _main.Config.TunnelPort.ToString() : "25565", 36);
            _portEntry.Width = 80; _portEntry.FontSize = 13; _portEntry.FontWeight = FontWeights.Bold;
            _portEntry.HorizontalContentAlignment = HorizontalAlignment.Center;
            _portEntry.ToolTip = "Minecraft'ta ESC → Yerel Ağda Paylaş sonrası sohbette görünen port numarası";
            portRow.Children.Add(_portEntry);
            portRow.Children.Add(PageHelpers.Lbl("   ← Minecraft sohbetinde görünen port", 10, "#404060"));
            tunSp.Children.Add(portRow);

            // ── Servis bazlı ekstra ayar satırları ──────────────────────────────────
            // Custom SSH — host:port input
            _customHostRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _customHostRow.Children.Add(PageHelpers.Lbl("SSH Adresi:", 10, "#606888", bold: true, pad: new Thickness(0, 0, 8, 0)));
            string savedHost = !string.IsNullOrEmpty(_main.Config.TunnelCustomHost) ? _main.Config.TunnelCustomHost : "nokey@localhost.run";
            _customHostEntry = PageHelpers.DarkTextBox(savedHost, 36);
            _customHostEntry.Width = 300; _customHostEntry.FontSize = 11;
            _customHostEntry.ToolTip = "Örnekler:\n  nokey@localhost.run\n  user@myvps.com:2222\n  mycustomvps.com";
            _customHostRow.Children.Add(_customHostEntry);
            _customHostRow.Children.Add(PageHelpers.Lbl("  (user@host:port)", 10, "#404060"));

            // _subdomainEntry and _subdomainRow (Serveo is removed, but we keep them collapsed/dummy for field references)
            _subdomainEntry = new TextBox { Visibility = Visibility.Collapsed };
            _subdomainRow = new StackPanel { Visibility = Visibility.Collapsed };

            tunSp.Children.Add(_customHostRow);

            void UpdateProviderRows() {
                int idx = _gatewayCombo.SelectedIndex;
                _customHostRow.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed; // Custom SSH
            }
            UpdateProviderRows();

            // ── Büyük Başlat Butonu ──────────────────────────────────────────────────
            _tunnelBtn = new Button {
                Content    = "🚀  TÜNELI BAŞLAT",
                Height     = 48,
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(0, 180, 100),
                    System.Windows.Media.Color.FromRgb(0, 130, 60), 90),
                BorderThickness = new Thickness(0),
                Cursor          = System.Windows.Input.Cursors.Hand,
                Margin          = new Thickness(0, 4, 0, 0),
            };
            _tunnelBtn.Click += (_, _) => ToggleTunnel();
            tunSp.Children.Add(_tunnelBtn);

            // ── Büyük Adres Göstergesi (tünel açıkken) ──────────────────────────────
            _statusBanner = new Border {
                Background      = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(0, 20, 40),
                    System.Windows.Media.Color.FromRgb(0, 10, 25), 90),
                BorderBrush     = PageHelpers.HexBrush("#00A3FF"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12),
                Margin          = new Thickness(0, 14, 0, 0),
                Padding         = new Thickness(20, 16, 20, 16),
                Visibility      = Visibility.Collapsed,
            };

            var bannerSp = new StackPanel();

            // Live status LED + label
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            statusRow.Children.Add(PageHelpers.Lbl("● TÜNEL AKTİF", 11, "#39FF14", bold: true));
            statusRow.Children.Add(PageHelpers.Lbl(" — Arkadaşlarınla bu adresi paylaş:", 11, "#606888", pad: new Thickness(4, 0, 0, 0)));
            bannerSp.Children.Add(statusRow);

            // Big address display
            var addrBox = new Border {
                Background      = PageHelpers.HexBrush("#050d18"),
                BorderBrush     = PageHelpers.HexBrush("#00A3FF"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(16, 10, 16, 10),
                Margin          = new Thickness(0, 0, 0, 10),
            };
            _tunnelLbl = new TextBlock {
                FontSize   = 20,
                FontWeight = FontWeights.Bold,
                Foreground = PageHelpers.HexBrush("#39FF14"),
                FontFamily = new FontFamily("Consolas"),
                Text       = "",
                TextWrapping = TextWrapping.Wrap,
            };
            addrBox.Child = _tunnelLbl;
            bannerSp.Children.Add(addrBox);

            // Copy + Test buttons row
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var copyAddrBtn = PageHelpers.MkBtn("📋 Kopyala", "#00A3FF", 110);
            copyAddrBtn.Height = 36;
            copyAddrBtn.Click += (_, _) => {
                if (!string.IsNullOrEmpty(_tunnelLbl.Text)) {
                    Clipboard.SetText(_tunnelLbl.Text);
                    copyAddrBtn.Content = "✅ Kopyalandı!";
                    System.Windows.Threading.DispatcherTimer t = new() { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) => { copyAddrBtn.Content = "📋 Kopyala"; t.Stop(); };
                    t.Start();
                }
            };
            btnRow.Children.Add(copyAddrBtn);

            _pingCheckBtn = PageHelpers.MkBtn("🔍 Test Et", "#2EB82E", 100);
            _pingCheckBtn.Height  = 36;
            _pingCheckBtn.Margin  = new Thickness(8, 0, 0, 0);
            bannerSp.Children.Add(btnRow);
            btnRow.Children.Add(_pingCheckBtn);

            _pingStatusLbl = PageHelpers.Lbl("", 11, "#606888");
            _pingStatusLbl.Margin = new Thickness(0, 6, 0, 0);
            _pingStatusLbl.TextWrapping = TextWrapping.Wrap;
            bannerSp.Children.Add(_pingStatusLbl);

            _pingCheckBtn.Click += async (_, _) => {
                _pingCheckBtn.IsEnabled = false;
                _pingStatusLbl.Text = "⏳ Test ediliyor...";
                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                try {
                    var addr = _tunnelLbl.Text.Trim();
                    if (!string.IsNullOrEmpty(addr)) {
                        string host = addr; int port = 25565;
                        if (addr.Contains(":")) {
                            var parts = addr.Split(':');
                            host = parts[0];
                            if (parts.Length > 1 && int.TryParse(parts[1], out var pp)) port = pp;
                        }
                        bool tcpOk = false; long tcpMs = 0;
                        try {
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            using var tcp = new System.Net.Sockets.TcpClient();
                            var cts = new System.Threading.CancellationTokenSource(4000);
                            await tcp.ConnectAsync(host, port, cts.Token);
                            sw.Stop(); tcpOk = true; tcpMs = sw.ElapsedMilliseconds;
                        } catch { }

                        if (!tcpOk) {
                            _pingStatusLbl.Text = "❌ TÜNEL KAPALI — SSH tünelinizi yeniden başlatın.";
                            _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                        } else {
                            var (online, players, max, ping) = await McPing.PingAsync(host, port);
                            if (online) {
                                string lbl2 = ping < 80 ? "⚡ MÜKEMMEL" : ping < 180 ? "📶 ORTA" : "🐢 YÜKSEK GECİKME";
                                string col2 = ping < 80 ? "#39FF14" : ping < 180 ? "#FFB100" : "#FF4B4B";
                                _pingStatusLbl.Text = $"{lbl2}  |  {players}/{max} oyuncu  |  {ping}ms";
                                _pingStatusLbl.Foreground = PageHelpers.HexBrush(col2);
                            } else {
                                _pingStatusLbl.Text = $"✅ Tünel açık ({tcpMs}ms) — Minecraft sunucusu başlamayı bekliyor. ESC → Yerel Ağda Paylaş'ı yapın.";
                                _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                            }
                        }
                    }
                } catch (Exception ex) {
                    _pingStatusLbl.Text = $"Hata: {ex.Message}";
                    _pingStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                } finally { _pingCheckBtn.IsEnabled = true; }
            };

            _statusBanner.Child = bannerSp;
            tunSp.Children.Add(_statusBanner);

            // ── Canlı Log Konsolu ────────────────────────────────────────────────────
            _consoleBox = new TextBox {
                IsReadOnly  = true,
                Height      = 100,
                Background  = PageHelpers.HexBrush("#060a0f"),
                Foreground  = PageHelpers.HexBrush("#39FF14"),
                BorderBrush = PageHelpers.HexBrush("#1a2040"),
                BorderThickness = new Thickness(1),
                FontFamily  = new FontFamily("Consolas"),
                FontSize    = 10,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin  = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(8),
                Text    = "🖥️ Tünel log konsolu hazır. Başlat butonuna basınca SSH adımları burada görünür...\n",
            };
            tunSp.Children.Add(_consoleBox);

            // Visual map container (kept for compatibility)
            _visualMapContainer = new StackPanel { Visibility = Visibility.Collapsed };
            tunSp.Children.Add(_visualMapContainer);

            tunCard.Child = tunSp; sp.Children.Add(tunCard);

            // Initialize connection flow map state based on active tunnel
            if (_main.Relay?.TunnelAddress != null)
            {
                _tunnelBtn.Content    = "🛑  TÜNELI DURDUR";
                _tunnelBtn.Background = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(200, 30, 30),
                    System.Windows.Media.Color.FromRgb(140, 0, 0), 90);
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
            if (relay == null) { MessageBox.Show("Relay bağlantı yok.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            // ── Stop tunnel if running ────────────────────────────────────────────
            if (relay.TunnelAddress != null) {
                relay.StopTunnel();
                _tunnelBtn.Content    = "🚀 TÜNELI BAŞLAT";
                _tunnelBtn.Background = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(0, 180, 100),
                    System.Windows.Media.Color.FromRgb(0, 130, 60), 90);
                _statusBanner.Visibility = Visibility.Collapsed;
                _tunnelLbl.Text = "";
                if (_consoleBox != null)
                    _consoleBox.Text = "🖥️ Mistik Tünel Log Konsolu sıfırlandı.\nTüneli başlattığınızda loglar burada görünecektir...\n";
                UpdateVisualMap(0);
                return;
            }

            // ── Read and validate port ────────────────────────────────────────────
            int.TryParse(_portEntry.Text.Trim(), out var port);
            if (port == 0) port = 25565;

            // ── Map gateway combo index to gateway key (0=bore.pub, 1=custom) ──
            string gateway = _gatewayCombo.SelectedIndex switch {
                1 => "custom",
                _ => "bore.pub"  // index 0 = bore (recommended, actually works!)
            };

            string customSub  = _subdomainEntry?.Text.Trim() ?? "";
            string customHost = _customHostEntry?.Text.Trim() ?? "";

            // ── Persist settings ──────────────────────────────────────────────────
            _main.Config.TunnelGateway          = _gatewayCombo.SelectedIndex;
            _main.Config.TunnelPort             = port;
            _main.Config.TunnelCustomSubdomain  = customSub;
            _main.Config.TunnelCustomHost       = customHost;
            ConfigManager.Save(_main.Config);

            // ── UI feedback ───────────────────────────────────────────────────────
            string displayGateway = _gatewayCombo.SelectedIndex == 1 ? (customHost.Length > 0 ? customHost : "Özel Sunucu") : gateway;
            if (_consoleBox != null)
                _consoleBox.Text = $"🖥️ Tünel başlatılıyor (Seçilen Sunucu: {displayGateway})...\n";

            _tunnelBtn.IsEnabled  = false;
            _tunnelBtn.Content    = "⌛  Bağlanıyor...";
            _tunnelBtn.Background = PageHelpers.HexBrush("#996600");
            _statusBanner.Visibility = Visibility.Collapsed;

            UpdateVisualMap(1);

            relay.OnTunnelReady -= OnTunnelReady;
            relay.OnTunnelReady += OnTunnelReady;

            relay.StartTunnel(port, gateway, customSub, customHost);

            // bore indirme sürebilir — 60 saniye bekle
            int waitMs = gateway == "bore.pub" ? 60000 : 25000;
            _ = Task.Delay(waitMs).ContinueWith(_ => Dispatcher.BeginInvoke(() => {
                if (relay.TunnelAddress == null) {
                    _tunnelBtn.IsEnabled  = true;
                    _tunnelBtn.Content    = "🚀  TÜNELI BAŞLAT";
                    _tunnelBtn.Background = new System.Windows.Media.LinearGradientBrush(
                        System.Windows.Media.Color.FromRgb(0, 180, 100),
                        System.Windows.Media.Color.FromRgb(0, 130, 60), 90);
                    if (_consoleBox != null)
                        _consoleBox.AppendText("[UYARI] Bağlantı zaman aşımına uğradı. Logları kontrol edin.\n");
                    UpdateVisualMap(0);
                }
            }));
        }


        void OnTunnelReady(string? addr)
        {
            Dispatcher.BeginInvoke(() => {
                _tunnelBtn.IsEnabled = true;
                if (string.IsNullOrEmpty(addr)) {
                    // Failed to connect
                    _tunnelBtn.Content    = "🚀  TÜNELI BAŞLAT";
                    _tunnelBtn.Background = new System.Windows.Media.LinearGradientBrush(
                        System.Windows.Media.Color.FromRgb(0, 180, 100),
                        System.Windows.Media.Color.FromRgb(0, 130, 60), 90);
                    _statusBanner.Visibility = Visibility.Collapsed;
                    UpdateVisualMap(0);
                } else {
                    // Connected!
                    _tunnelBtn.Content    = "🛑  TÜNELI DURDUR";
                    _tunnelBtn.Background = new System.Windows.Media.LinearGradientBrush(
                        System.Windows.Media.Color.FromRgb(200, 30, 30),
                        System.Windows.Media.Color.FromRgb(140, 0, 0), 90);
                    _tunnelLbl.Text = addr;
                    _statusBanner.Visibility = Visibility.Visible;
                    // Silent copy — no popup
                    try { Clipboard.SetText(addr); } catch { }
                    _pingStatusLbl.Text = "📌 Adres panoya kopyalandı! Minecraft → Çok Oyunculu → Doğrudan Bağlan'a yapıştır.";
                    _pingStatusLbl.Foreground = PageHelpers.HexBrush("#39FF14");
                    UpdateVisualMap(2);
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
            string gatewayName = _gatewayCombo?.SelectedIndex == 1 ? "Özel Ağ Geçidi" : "Bore.pub Ağ Geçidi";
            var n3 = Node(gatewayName, state == 2 ? "Bağlantı OK" : "Beklemede", state == 2 ? "#2EB82E" : "#888888", state == 2);
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
