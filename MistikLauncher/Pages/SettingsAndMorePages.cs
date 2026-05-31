using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Newtonsoft.Json.Linq;

namespace MistikLauncher.Pages
{
    // Shared helpers
    public static class PageHelpers
    {
        public static Color HexColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        public static SolidColorBrush HexBrush(string hex) => new(HexColor(hex));

        public static TextBlock Lbl(string text, double size = 13, string color = "#FFFFFF",
            bool bold = false, Thickness? pad = null, TextWrapping wrap = TextWrapping.NoWrap)
        {
            var tb = new TextBlock {
                Text = text, FontSize = size, Foreground = HexBrush(color),
                FontFamily = new FontFamily("Segoe UI"), TextWrapping = wrap,
                VerticalAlignment = VerticalAlignment.Center };
            if (bold) tb.FontWeight = FontWeights.Bold;
            if (pad.HasValue) tb.Padding = pad.Value;
            return tb;
        }

        public static Border Card(string bgColor = "#181818", double radius = 12,
            string? borderColor = null, Thickness? margin = null)
        {
            var b = new Border { Background = HexBrush(bgColor), CornerRadius = new CornerRadius(radius),
                Margin = margin ?? new Thickness(0, 6, 0, 6) };
            if (borderColor != null) { b.BorderBrush = HexBrush(borderColor); b.BorderThickness = new Thickness(1); }
            return b;
        }

        public static Button MkBtn(string text, string color = "#00A3FF", double width = 0)
        {
            var btn = new Button {
                Content = text, Background = HexBrush(color), Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12, FontWeight = FontWeights.Bold,
                Padding = new Thickness(14, 7, 14, 7), Cursor = System.Windows.Input.Cursors.Hand };
            if (width > 0) btn.Width = width;
            btn.Template = RoundedTemplate(color);
            return btn;
        }

        static ControlTemplate RoundedTemplate(string color)
        {
            var tpl = new ControlTemplate(typeof(Button));
            var f = new FrameworkElementFactory(typeof(Border));
            f.SetValue(Border.BackgroundProperty, HexBrush(color));
            f.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            f.AppendChild(cp); tpl.VisualTree = f;
            return tpl;
        }

        public static TextBox DarkTextBox(string placeholder = "", double height = 36)
            => new() {
                Background = HexBrush("#222222"), Foreground = Brushes.White, CaretBrush = Brushes.White,
                BorderBrush = HexBrush("#333333"), BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8), FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13, Height = height, Text = placeholder,
                VerticalContentAlignment = VerticalAlignment.Center };

        public static TextBlock SectionTitle(string text) => Lbl(text.ToUpper(), 11, "#666666", true);
    }

    // Settings Page
    public class SettingsPage : Page
    {
        readonly MainWindow _main;
        TextBox _tbUser = null!, _tbRam = null!, _tbGithubUser = null!;
        ComboBox _cbLang = null!, _cbAccent = null!, _cbAuthType = null!;
        CheckBox _chkAutoClose = null!;


        public SettingsPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Ayarlar", 24, "#FFFFFF", true));

            // General
            var genCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 16, 0, 0));
            var genSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            genSp.Children.Add(PageHelpers.Lbl("Genel & Hesap", 14, "#00A3FF", true));
            genSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 10, 0, 14) });
            genSp.Children.Add(PageHelpers.Lbl("Kullanici Adi", 12, "#A0A0A0"));
            _tbUser = PageHelpers.DarkTextBox(main.Config.User);
            genSp.Children.Add(_tbUser);
            genSp.Children.Add(PageHelpers.Lbl("Giriş & Karakter Sistemi", 12, "#A0A0A0", pad: new Thickness(0, 10, 0, 0)));
            var authTypes = new[] { "Normal (Çevrimdışı)", "Ely.by (Cilt & Giriş Desteği)" };
            var selectedAuthType = (main.Config.AuthType ?? "").ToLower() == "elyby" ? "Ely.by (Cilt & Giriş Desteği)" : "Normal (Çevrimdışı)";
            _cbAuthType = new ComboBox {
                ItemsSource = authTypes,
                SelectedItem = selectedAuthType,
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            if (_cbAuthType.SelectedItem == null) _cbAuthType.SelectedIndex = 0;
            genSp.Children.Add(_cbAuthType);

            var checkElyBtn = PageHelpers.MkBtn("Ely.by Bağlantısını Test Et", "#00A3FF", 240);
            checkElyBtn.Margin = new Thickness(0, 6, 0, 0);
            checkElyBtn.HorizontalAlignment = HorizontalAlignment.Left;
            checkElyBtn.Click += async (_, _) => {
                checkElyBtn.IsEnabled = false;
                checkElyBtn.Content = "Test ediliyor...";
                try
                {
                    var selAuth = (_cbAuthType?.SelectedItem as string) ?? "Normal (Çevrimdışı)";
                    bool isElySelected = selAuth.Contains("Ely.by");

                    if (!isElySelected)
                    {
                        MessageBox.Show("Ely.by entegrasyonu şu anda aktif değil.\n\nAktif etmek için yukarıdaki seçim kutusundan 'Ely.by (Cilt & Giriş Desteği)' seçeneğini belirleyip 'KAYDET' butonuna basın.", "Entegrasyon Aktif Değil", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Ely.by sunucusunu test et
                    using var cts = new System.Threading.CancellationTokenSource(4000);
                    using var http = new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    
                    var response = await http.GetAsync("https://authserver.ely.by/api/authlib-injector", cts.Token);
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        MessageBox.Show("✅ Ely.by entegrasyonu AKTİF ve sunucularına başarıyla bağlanıldı!\n\nCildiniz oyunda ve sunucularda sorunsuz görünecektir.", "Bağlantı Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"❌ Ely.by entegrasyonu aktif ancak sunucu hata döndürdü: {(int)response.StatusCode}\n\nSunucu bakımda olabilir.", "Bağlantı Sorunu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Ely.by sunucularına bağlanılamadı!\n\nOlası Nedenler:\n• İnternet bağlantınız yok.\n• Ely.by sunucusu şu an çökmüş/bakımda.\n• Türkiye'deki servis sağlayıcınız Ely.by adresini engellemiş.\n\nHata detayı: {ex.Message}", "Bağlantı Başarısız", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    checkElyBtn.Content = "Ely.by Bağlantısını Test Et";
                    checkElyBtn.IsEnabled = true;
                }
            };
            genSp.Children.Add(checkElyBtn);
            genSp.Children.Add(PageHelpers.Lbl("GitHub Kullanici Adi", 12, "#A0A0A0", pad: new Thickness(0, 10, 0, 0)));
            _tbGithubUser = PageHelpers.DarkTextBox(main.Config.GithubUser);
            genSp.Children.Add(_tbGithubUser);
            genSp.Children.Add(PageHelpers.Lbl("RAM (GB)", 12, "#A0A0A0", pad: new Thickness(0, 10, 0, 0)));
            _tbRam = PageHelpers.DarkTextBox(main.Config.Ram.ToString());
            _tbRam.Width = 120; _tbRam.HorizontalAlignment = HorizontalAlignment.Left;
            genSp.Children.Add(_tbRam);
            _chkAutoClose = new CheckBox { Content = "Oyun acilinca Launcher kapat",
                IsChecked = main.Config.AutoClose, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 14, 0, 0) };
            genSp.Children.Add(_chkAutoClose);
            genCard.Child = genSp; sp.Children.Add(genCard);

            // Appearance
            var appCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var appSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            appSp.Children.Add(PageHelpers.Lbl("Gorunum", 14, "#00A3FF", true));
            appSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 10, 0, 14) });
            appSp.Children.Add(PageHelpers.Lbl("Dil", 12, "#A0A0A0"));
            // Config.Lang'ı normalize et: "Türkçe"/"TÃ¼rkÃ§e" gibi bozuk değerleri "Turkce"'ye çevir
            var normalizedLang = (main.Config.Lang ?? "").Contains("ngl") ? "English" : "Turkce";
            if (main.Config.Lang == "English") normalizedLang = "English";
            _cbLang = new ComboBox { ItemsSource = new[] { "Turkce", "English" },
                SelectedItem = normalizedLang, Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 12) };
            if (_cbLang.SelectedItem == null) _cbLang.SelectedIndex = 0; // fallback: her zaman seçili olsun
            appSp.Children.Add(_cbLang);
            appSp.Children.Add(PageHelpers.Lbl("Tema Rengi", 12, "#A0A0A0"));
            // Config.Accent'i normalize et: geçerli bir değer olduğundan emin ol
            var validAccents = new[] { "Blue", "Red", "Green", "Purple", "Orange" };
            var normalizedAccent = System.Array.Exists(validAccents, a => a == main.Config.Accent) ? main.Config.Accent : "Blue";
            _cbAccent = new ComboBox { ItemsSource = validAccents,
                SelectedItem = normalizedAccent, Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            if (_cbAccent.SelectedItem == null) _cbAccent.SelectedIndex = 0; // fallback
            appSp.Children.Add(_cbAccent);
            appCard.Child = appSp; sp.Children.Add(appCard);




            var saveBtn = PageHelpers.MkBtn("KAYDET", "#00A3FF", 200);
            saveBtn.Margin = new Thickness(0, 16, 0, 0); saveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveBtn.Click += (_, _) => {
                try
                {
                    _main.Config.User       = (_tbUser?.Text ?? "").Trim();
                    var selAuth             = (_cbAuthType?.SelectedItem as string) ?? "Normal (Çevrimdışı)";
                    _main.Config.AuthType   = selAuth.Contains("Ely.by") ? "elyby" : "offline";
                    _main.Config.Ram        = int.TryParse((_tbRam?.Text ?? "").Trim(), out var r) ? Math.Max(1, r) : 4;
                    _main.Config.Lang       = (_cbLang?.SelectedItem as string) ?? "Turkce";
                    _main.Config.Accent     = (_cbAccent?.SelectedItem as string) ?? "Blue";
                    _main.Config.AutoClose  = _chkAutoClose?.IsChecked == true;
                    _main.Config.GithubUser = (_tbGithubUser?.Text ?? "").Trim();

                    ConfigManager.Save(_main.Config);
                    try { _main.ReloadConfig(); } catch { /* ReloadConfig hataları sessizce yut */ }
                    MessageBox.Show("Ayarlar kaydedildi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ayarlar kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            sp.Children.Add(saveBtn);

            // Admin
            var adminCard = PageHelpers.Card("#1a1015", 12, "#A349A4"); adminCard.Margin = new Thickness(0, 12, 0, 0);
            adminCard.Visibility = Visibility.Collapsed; // Varsayılan olarak gizli
            var adminSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            adminSp.Children.Add(PageHelpers.Lbl("Yonetici Modu", 13, "#A349A4", true));
            var adminBtn = PageHelpers.MkBtn("Yonetici Panelini Ac", "#A349A4");
            adminBtn.Margin = new Thickness(0, 8, 0, 0); adminBtn.HorizontalAlignment = HorizontalAlignment.Left;
            adminBtn.Click += (_, _) => main.Navigate("Admin");
            adminSp.Children.Add(adminBtn); adminCard.Child = adminSp; sp.Children.Add(adminCard);

            // Gizli Şifre Kartı
            var secretCard = PageHelpers.Card("#1a1015", 12, "#A349A4");
            secretCard.Margin = new Thickness(0, 12, 0, 0);
            secretCard.Visibility = Visibility.Collapsed;
            var secretSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            secretSp.Children.Add(PageHelpers.Lbl("Yönetici Şifresini Girin", 13, "#A349A4", true));
            var pwdBox = new PasswordBox { Background = PageHelpers.HexBrush("#121212"), Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 8), MaxWidth = 200, HorizontalAlignment = HorizontalAlignment.Left };
            var pwdBtn = PageHelpers.MkBtn("Kilidi Aç", "#A349A4", 100);
            pwdBtn.HorizontalAlignment = HorizontalAlignment.Left;
            pwdBtn.Click += (_, _) => {
                if (pwdBox.Password == "mistik34") // Gizli Şifre
                {
                    secretCard.Visibility = Visibility.Collapsed;
                    adminCard.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("Hatalı şifre!", "Erişim Reddedildi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            secretSp.Children.Add(pwdBox); secretSp.Children.Add(pwdBtn); secretCard.Child = secretSp; sp.Children.Add(secretCard);

            // Gizli Tuş Kombinasyonu (F12) - Global Yakalama
            KeyEventHandler shortcutHandler = (s, e) => {
                if (e.Key == Key.F12)
                {
                    if (adminCard.Visibility != Visibility.Visible)
                    {
                        secretCard.Visibility = Visibility.Visible;
                        pwdBox.Focus();
                        e.Handled = true;
                    }
                }
            };
            _main.PreviewKeyDown += shortcutHandler;
            this.Unloaded += (s, e) => _main.PreviewKeyDown -= shortcutHandler;

            // Shortcut card
            var scCard = PageHelpers.Card("#0d1f2d", 12, "#00A3FF"); scCard.Margin = new Thickness(0, 12, 0, 0);
            var scSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            scSp.Children.Add(PageHelpers.Lbl("Masaustu Kisayolu", 13, "#00A3FF", true));
            scSp.Children.Add(PageHelpers.Lbl("Launcher'i masaustune veya gorev cubuguna sabitle", 11, "#A0A0A0"));
            var scBtnRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var scBtn = PageHelpers.MkBtn("Masaustune Kisayol Olustur", "#00A3FF", 240);
            scBtn.Click += (_, _) => CreateShortcut();
            var scBtn2 = PageHelpers.MkBtn("Baslangica Ekle", "#333333", 160);
            scBtn2.Margin = new Thickness(10, 0, 0, 0);
            scBtn2.Click += (_, _) => AddToStartup();
            scBtnRow.Children.Add(scBtn); scBtnRow.Children.Add(scBtn2);
            scSp.Children.Add(scBtnRow);
            scCard.Child = scSp; sp.Children.Add(scCard);

            // Java 21 Kurulum Merkezi Card
            var javaCard = PageHelpers.Card("#0a1a2a", 12, "#00FFCC"); javaCard.Margin = new Thickness(0, 12, 0, 0);
            var javaSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            javaSp.Children.Add(PageHelpers.Lbl("☕ Java 21 Kurulum Merkezi", 14, "#00FFCC", true));
            javaSp.Children.Add(PageHelpers.Lbl("Minecraft 1.17+ ve tüm Fabric/Forge modları için Java 21 zorunludur. Aşağıdaki butona tıklayarak kalıcı olarak kurun.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            var javaStatusLbl = PageHelpers.Lbl("", 11, "#A0A0A0");
            javaStatusLbl.Margin = new Thickness(0, 8, 0, 0);
            var javaBtn = PageHelpers.MkBtn("☕ Java 21'i İndir ve Kalıcı Kur", "#00FFCC", 260);
            javaBtn.Foreground = Brushes.Black;
            javaBtn.Margin = new Thickness(0, 10, 0, 0);

            // Set initial state
            var localJava = Path.Combine(App.AppData, "java", "jre21", "bin", "java.exe");
            if (File.Exists(localJava))
            {
                javaStatusLbl.Text = "✅ Java 21 zaten kurulu ve aktif!";
                javaStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                javaBtn.IsEnabled = false;
                javaBtn.Content = "✅ Kuruldu";
            }

            javaBtn.Click += async (_, _) => {
                javaBtn.IsEnabled = false; 
                javaBtn.Content = "Kuruluyor...";
                javaStatusLbl.Text = "Java 21 indiriliyor ve kuruluyor, lütfen bekleyin...";
                javaStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                
                try
                {
                    var result = await _main.DownloadAndInstallJava21Async();
                    if (result != null && File.Exists(result))
                    {
                        javaBtn.Content = "✅ Kuruldu";
                        javaStatusLbl.Text = "Java 21 başarıyla kuruldu! Artık tüm modern sürümleri sorunsuz açabilirsiniz.";
                        javaStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                    }
                    else
                    {
                        javaBtn.Content = "☕ Java 21'i İndir ve Kalıcı Kur";
                        javaBtn.IsEnabled = true;
                        javaStatusLbl.Text = "Kurulum başarısız oldu. Lütfen tekrar deneyin.";
                        javaStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                    }
                }
                catch (Exception ex)
                {
                    javaBtn.Content = "☕ Java 21'i İndir ve Kalıcı Kur";
                    javaBtn.IsEnabled = true;
                    javaStatusLbl.Text = $"Hata oluştu: {ex.Message}";
                    javaStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                }
            };
            javaSp.Children.Add(javaBtn);
            javaSp.Children.Add(javaStatusLbl);
            javaCard.Child = javaSp; 
            sp.Children.Add(javaCard);

            // Bulut Güncelleme Sistemi Card
            var updateCard = PageHelpers.Card("#0a1f1a", 12, "#00A3FF"); updateCard.Margin = new Thickness(0, 12, 0, 0);
            var updateSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            updateSp.Children.Add(PageHelpers.Lbl("🔄 Launcher Güncelleme", 14, "#00A3FF", true));
            updateSp.Children.Add(PageHelpers.Lbl($"Mevcut Sürüm: {App.LocalVersion}", 11, "#CCCCCC", bold: true));
            updateSp.Children.Add(PageHelpers.Lbl("Launcher'ınızın en son sürümde olup olmadığını kontrol etmek için aşağıdaki butona tıklayın.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));

            var updateStatusLbl = PageHelpers.Lbl("", 11, "#A0A0A0");
            updateStatusLbl.Margin = new Thickness(0, 8, 0, 0);
            
            var updateBtn = PageHelpers.MkBtn("🔄 Güncellemeleri Denetle", "#00A3FF", 220);
            updateBtn.Margin = new Thickness(0, 14, 0, 0);
            updateBtn.Click += async (_, _) => {
                updateBtn.IsEnabled = false;
                updateBtn.Content = "Kontrol ediliyor...";
                updateStatusLbl.Text = "En son sürüm bilgileri kontrol ediliyor...";
                updateStatusLbl.Foreground = PageHelpers.HexBrush("#FFB100");
                try
                {
                    await _main.CheckCloudUpdateAsync(true);
                    updateStatusLbl.Text = "Kontrol tamamlandı.";
                    updateStatusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");
                }
                catch (Exception ex)
                {
                    updateStatusLbl.Text = $"Hata: {ex.Message}";
                    updateStatusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                }
                finally
                {
                    updateBtn.Content = "🔄 Güncellemeleri Denetle";
                    updateBtn.IsEnabled = true;
                }
            };
            updateSp.Children.Add(updateBtn);
            updateSp.Children.Add(updateStatusLbl);
            updateCard.Child = updateSp;
            sp.Children.Add(updateCard);

            // ℹ️ Yardım, Güncelleme Notları & Lisanslar Kartı
            var infoCard = PageHelpers.Card("#181818", 12); infoCard.Margin = new Thickness(0, 12, 0, 0);
            var infoSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            infoSp.Children.Add(PageHelpers.Lbl("ℹ️ Yardım & Ek Bilgiler", 14, "#00A3FF", true));
            infoSp.Children.Add(PageHelpers.Lbl("Launcher ile ilgili kurulum rehberleri, sürüm güncelleme notları ve lisans detaylarına buradan ulaşabilirsiniz.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));

            var infoBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            
            var changelogBtn = PageHelpers.MkBtn("📝 Güncelleme Notları", "#FFB100", 160);
            changelogBtn.Height = 35;
            changelogBtn.Click += (_, _) => _main.Navigate("Changelog");
            
            var guideBtn = PageHelpers.MkBtn("📖 Kurulum Rehberi", "#2EB82E", 160);
            guideBtn.Height = 35;
            guideBtn.Margin = new Thickness(12, 0, 0, 0);
            guideBtn.Click += (_, _) => _main.Navigate("Guide");
            
            var licensesBtn = PageHelpers.MkBtn("📜 Lisanslar", "#888888", 120);
            licensesBtn.Height = 35;
            licensesBtn.Margin = new Thickness(12, 0, 0, 0);
            licensesBtn.Click += (_, _) => _main.Navigate("Licenses");
            
            infoBtnRow.Children.Add(changelogBtn);
            infoBtnRow.Children.Add(guideBtn);
            infoBtnRow.Children.Add(licensesBtn);
            infoSp.Children.Add(infoBtnRow);
            infoCard.Child = infoSp;
            sp.Children.Add(infoCard);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        static void CreateShortcut()
        {
            try
            {
                var exe  = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var lnk  = Path.Combine(desk, "Mistik Launcher Ultra.lnk");
                // Use PowerShell to create shortcut (no COM dependency)
                var ps = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{lnk}');$s.TargetPath='{exe}';$s.Save()";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell", Arguments = $"-Command \"{ps}\"",
                    CreateNoWindow = true, UseShellExecute = false
                })?.WaitForExit(3000);
                MessageBox.Show($"Kisayol olusturuldu!\n{lnk}", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kisayol olusturulamadi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static void AddToStartup()
        {
            try
            {
                var exe  = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var startDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
                var lnk = Path.Combine(startDir, "Mistik Launcher Ultra.lnk");
                var ps = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{lnk}');$s.TargetPath='{exe}';$s.Save()";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell", Arguments = $"-Command \"{ps}\"",
                    CreateNoWindow = true, UseShellExecute = false
                })?.WaitForExit(3000);
                MessageBox.Show("Windows baslangicina eklendi!", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Optimization Page
    public class OptimizationPage : Page
    {
        public OptimizationPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Optimizasyon Merkezi", 24, "#FFFFFF", true));

            var items = new[] {
                ("Turbo Modu (Maksimum Performans)", "G1GC + agresif JVM optimizasyonu", main.Config.OptTurbo),
                ("FPS Artirici (Gorsel Akicilik)", "Render optimizasyonu ve frame sinirlaması kaldirma", main.Config.OptFps),
            };
            bool[] vals = { main.Config.OptTurbo, main.Config.OptFps };

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i]; var idx = i;
                var card = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
                var row = new Grid { Margin = new Thickness(20, 16, 20, 16) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var infoSp = new StackPanel();
                infoSp.Children.Add(PageHelpers.Lbl(item.Item1, 14, "#FFFFFF", true));
                infoSp.Children.Add(PageHelpers.Lbl(item.Item2, 11, "#A0A0A0"));
                var chk = new CheckBox { IsChecked = item.Item3, VerticalAlignment = VerticalAlignment.Center };
                chk.Checked   += (_, _) => vals[idx] = true;
                chk.Unchecked += (_, _) => vals[idx] = false;
                Grid.SetColumn(chk, 1); row.Children.Add(infoSp); row.Children.Add(chk);
                card.Child = row; sp.Children.Add(card);
            }

            var saveBtn = PageHelpers.MkBtn("KAYDET", "#00A3FF", 200);
            saveBtn.Margin = new Thickness(0, 16, 0, 0); saveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveBtn.Click += (_, _) => {
                try
                {
                    main.Config.OptTurbo = vals[0]; main.Config.OptFps = vals[1];
                    ConfigManager.Save(main.Config);
                    MessageBox.Show("Optimizasyon ayarlari kaydedildi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Optimizasyon kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            sp.Children.Add(saveBtn);

            // ── Kernel Optimizasyonları Kartı ──
            sp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 20, 0, 20) });
            sp.Children.Add(PageHelpers.Lbl("🔧 Kernel Düzeyinde Optimizasyonlar", 18, "#FF6B00", true));
            sp.Children.Add(PageHelpers.Lbl("Oyun başlatılınca otomatik uygulanır, kapanınca geri alınır. GPU'ya dokunmaz.", 11, "#A0A0A0"));

            var kernCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var kernSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            var chkKernPriority = new CheckBox { Content = "İşlem Önceliği → HIGH (CPU'da Minecraft'a öncelik verir)",
                IsChecked = main.Config.KernelPriority, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 10, 0, 0) };
            kernSp.Children.Add(chkKernPriority);

            var chkKernTimer = new CheckBox { Content = "Timer Çözünürlüğü → 1ms (Daha akıcı FPS, düşük input lag)",
                IsChecked = main.Config.KernelTimer, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernTimer);

            var chkKernAffinity = new CheckBox { Content = "CPU Affinity (Çekirdek 0'ı OS'a bırak, kalanını oyuna ver)",
                IsChecked = main.Config.KernelAffinity, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernAffinity);

            var chkKernPower = new CheckBox { Content = "Güç Planı → Yüksek Performans (Oyun süresince otomatik geçiş)",
                IsChecked = main.Config.KernelPower, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernPower);

            var chkKernNagle = new CheckBox { Content = "Nagle Kapatma (TCP gecikmesiz, düşük ping - Multiplayer)",
                IsChecked = main.Config.KernelNagle, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernNagle);

            var chkKernGpu = new CheckBox { Content = "🎮 GPU & Sistem Optimizasyonu (Ekran kartı tercihi, Game Bar kapatma, I/O Boost, Working Set)",
                IsChecked = main.Config.KernelGpu, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernGpu);

            var kernStatusBtn = PageHelpers.MkBtn("Optimizasyon Durumunu Göster", "#FF6B00", 260);
            kernStatusBtn.Margin = new Thickness(0, 12, 0, 0);
            kernStatusBtn.HorizontalAlignment = HorizontalAlignment.Left;
            kernStatusBtn.Click += (_, _) => {
                MessageBox.Show(KernelOptimizer.GetStatus(main.Config), "Kernel Optimizasyon Durumu", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            kernSp.Children.Add(kernStatusBtn);

            var kernSaveBtn = PageHelpers.MkBtn("KERNEL AYARLARINI KAYDET", "#FF6B00", 260);
            kernSaveBtn.Margin = new Thickness(0, 12, 0, 0);
            kernSaveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            kernSaveBtn.Click += (_, _) => {
                try {
                    main.Config.KernelPriority = chkKernPriority.IsChecked == true;
                    main.Config.KernelTimer    = chkKernTimer.IsChecked == true;
                    main.Config.KernelAffinity = chkKernAffinity.IsChecked == true;
                    main.Config.KernelPower    = chkKernPower.IsChecked == true;
                    main.Config.KernelNagle    = chkKernNagle.IsChecked == true;
                    main.Config.KernelGpu      = chkKernGpu.IsChecked == true;
                    ConfigManager.Save(main.Config);
                    MessageBox.Show("Kernel optimizasyon ayarları kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                } catch (Exception ex) {
                    MessageBox.Show($"Kernel ayarları kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            kernSp.Children.Add(kernSaveBtn);

            kernCard.Child = kernSp; sp.Children.Add(kernCard);

            // Separator
            sp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 20, 0, 20) });
            sp.Children.Add(PageHelpers.Lbl("⚡ Gelişmiş Mistik Performans Motoru", 18, "#FFB100", true));
            sp.Children.Add(PageHelpers.Lbl("Sisteminizdeki gereksiz yükleri kaldırın ve oyun ayarlarını en yüksek performansa uyarlayın.", 11, "#A0A0A0"));

            // Mistik Cleaner Card
            var cleanCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var cleanRow = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            cleanRow.ColumnDefinitions.Add(new ColumnDefinition());
            cleanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var cleanInfo = new StackPanel();
            cleanInfo.Children.Add(PageHelpers.Lbl("🧹 Mistik Sistem & Disk Temizleyici", 14, "#FFFFFF", true));
            cleanInfo.Children.Add(PageHelpers.Lbl("Eski hata raporlarını, Minecraft log dosyalarını temizler ve Windows DNS önbelleğini temizleyerek pingi düşürür.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            
            var cleanBtn = PageHelpers.MkBtn("Temizle & Hızlandır", "#2EB82E", 160);
            cleanBtn.Click += (_, _) => RunMistikCleaner(cleanBtn);
            
            Grid.SetColumn(cleanBtn, 1);
            cleanRow.Children.Add(cleanInfo);
            cleanRow.Children.Add(cleanBtn);
            cleanCard.Child = cleanRow;
            sp.Children.Add(cleanCard);

            // Mistik Graphics Optimizer Card
            var gfxCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var gfxRow = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            gfxRow.ColumnDefinitions.Add(new ColumnDefinition());
            gfxRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var gfxInfo = new StackPanel();
            gfxInfo.Children.Add(PageHelpers.Lbl("⚙️ Tek Tıkla Grafik FPS Ayarlayıcı", 14, "#FFFFFF", true));
            gfxInfo.Children.Add(PageHelpers.Lbl("Minecraft'ın kendi grafik ayarlarını (options.txt) ultra-düşük ayarlara çekerek ekran kartı yükünü tamamen sıfıra indirir.", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            
            var gfxBtn = PageHelpers.MkBtn("Grafikleri Optimize Et", "#FFB100", 160);
            gfxBtn.Foreground = Brushes.Black;
            gfxBtn.Click += (_, _) => OptimizeGameGraphics(gfxBtn);
            
            Grid.SetColumn(gfxBtn, 1);
            gfxRow.Children.Add(gfxInfo);
            gfxRow.Children.Add(gfxBtn);
            gfxCard.Child = gfxRow;
            sp.Children.Add(gfxCard);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        static void RunMistikCleaner(Button btn)
        {
            btn.IsEnabled = false;
            btn.Content = "Temizleniyor...";
            try
            {
                long freedBytes = 0;

                // 1. Clean logs directory
                var logsDir = Path.Combine(App.GameDir, "logs");
                if (Directory.Exists(logsDir))
                {
                    foreach (var file in Directory.GetFiles(logsDir))
                    {
                        try
                        {
                            var size = new FileInfo(file).Length;
                            File.Delete(file);
                            freedBytes += size;
                        }
                        catch { }
                    }
                }

                // 2. Clean crash-reports directory
                var crashDir = Path.Combine(App.GameDir, "crash-reports");
                if (Directory.Exists(crashDir))
                {
                    foreach (var file in Directory.GetFiles(crashDir))
                    {
                        try
                        {
                            var size = new FileInfo(file).Length;
                            File.Delete(file);
                            freedBytes += size;
                        }
                        catch { }
                    }
                }

                // 3. Clean Temp folder JVM logs/files
                try
                {
                    var tempDir = Path.GetTempPath();
                    foreach (var file in Directory.GetFiles(tempDir, "hs_err_pid*.log"))
                    {
                        try
                        {
                            var size = new FileInfo(file).Length;
                            File.Delete(file);
                            freedBytes += size;
                        }
                        catch { }
                    }
                }
                catch { }

                // 4. Flush DNS
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
                catch { }

                double freedMb = Math.Round((double)freedBytes / (1024 * 1024), 2);
                MessageBox.Show($"Mistik Temizlik Başarılı!\n\n• Toplam {freedMb} MB gereksiz log/hata dosyası silindi.\n• DNS önbelleği temizlenerek pinginiz optimize edildi.", "Mistik Performans Motoru", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Temizlik yapılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.Content = "Temizle & Hızlandır";
                btn.IsEnabled = true;
            }
        }

        static void OptimizeGameGraphics(Button btn)
        {
            btn.IsEnabled = false;
            try
            {
                var optionsPath = Path.Combine(App.GameDir, "options.txt");
                Directory.CreateDirectory(App.GameDir);

                var settings = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "enableVsync", "false" },
                    { "graphicsMode", "0" }, // 0: Fast
                    { "renderDistance", "6" },
                    { "simulationDistance", "6" },
                    { "particles", "2" }, // 2: Minimal
                    { "ao", "0" }, // Smooth Lighting Off
                    { "clouds", "false" },
                    { "bobView", "false" },
                    { "mipmapLevels", "0" }, // Turn off mipmaps for HUGE fps boost on Intel/AMD GPUs
                    { "maxFps", "260" }
                };

                var existingSettings = new System.Collections.Generic.Dictionary<string, string>();
                if (File.Exists(optionsPath))
                {
                    var lines = File.ReadAllLines(optionsPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            existingSettings[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                // Override settings
                foreach (var kvp in settings)
                {
                    existingSettings[kvp.Key] = kvp.Value;
                }

                // Write back
                var outputLines = new System.Collections.Generic.List<string>();
                foreach (var kvp in existingSettings)
                {
                    outputLines.Add($"{kvp.Key}:{kvp.Value}");
                }

                File.WriteAllLines(optionsPath, outputLines);

                MessageBox.Show("Minecraft ayarlarınız başarıyla optimize edildi!\n\n• Grafikler: Hızlı (Fast)\n• Görüş Mesafesi: 6 Chunk\n• Dikey Eşitleme (Vsync): KAPALI\n• Parçacıklar: En Az\n• Yumuşak Aydınlatma: KAPALI\n• Bulutlar: KAPALI\n• Mipmap Seviyesi: KAPALI (AMD/Intel için devasa FPS artışı)\n\nOyunu başlattığınızda ayarlar otomatik olarak uygulanmış olacaktır.", "Mistik Ultra FPS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar uygulanırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    // Guide Page
    public class GuidePage : Page
    {
        public GuidePage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Kurulum Rehberi", 24, "#FFFFFF", true));
            var steps = new[] {
                ("1. Surum Secimi", "Sol menuden 'Surum Yoneticisi'ne git ve bir Minecraft surumu indir.", "#00A3FF"),
                ("2. RAM Ayari",    "'Ayarlar' sayfasindan bilgisayarina uygun RAM miktarini sec (4-8 GB).", "#2EB82E"),
                ("3. Optimizasyon","'Optimizasyon' sayfasindan Turbo Modu'nu etkinlestir.", "#FFB100"),
                ("4. Mod Kurulumu","'Mod Merkezi'nden diledigin modu tek tikla kur.", "#A349A4"),
                ("5. Oyuna Gir",   "Alt cubuktan surumu sec ve 'OYUNA GIR' butonuna bas!", "#FF4B4B"),
                ("6. Arkadaslar",  "Arkadaslar sekmesinden oda kodunu arkadasina gonder, IP paylasimina gerek yok.", "#2EB82E"),
                ("7. 'Invalid Session' Cozumu", "Tünelden bağlanırken 'Invalid Session' (Geçersiz Oturum) hatası alırsanız endişelenmeyin! Mistik Launcher artık bunu tamamen otomatik olarak halleder. Tüneli başlattığınızda veya oyuna girdiğinizde, bilgisayarınızdaki tüm sunucu dosyaları (server.properties) ve LAN sunucusu ayarları otomatik olarak çevrimdışı moda (online-mode=false) çekilir, böylece arkadaşlarınız sorunsuz bağlanabilir.", "#A349A4"),
            };
            foreach (var (title, desc, color) in steps)
            {
                var card = PageHelpers.Card("#181818", 12, color, new Thickness(0, 10, 0, 0));
                var row = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
                row.Children.Add(PageHelpers.Lbl(title, 14, color, true));
                row.Children.Add(PageHelpers.Lbl(desc, 12, "#CCCCCC", wrap: TextWrapping.Wrap));
                card.Child = row; sp.Children.Add(card);
            }
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }

    // Licenses Page
    public class LicensesPage : Page
    {
        public LicensesPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Lisanslar", 24, "#FFFFFF", true));
            var libs = new[] {
                ("Mistik Launcher Ultra", "2026 Mistik Team. Tum haklari saklidir.", "#00A3FF"),
                ("MQTTnet",              "MIT License - dotnet/MQTTnet", "#2EB82E"),
                ("Newtonsoft.Json",       "MIT License - James Newton-King", "#FFB100"),
                (".NET 8 / WPF",          "MIT License - Microsoft Corporation", "#A349A4"),
                ("Modrinth API",          "Fair-use - modrinth.com", "#888888"),
            };
            foreach (var (name, lic, color) in libs)
            {
                var card = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 8, 0, 0));
                var row = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };
                row.Children.Add(PageHelpers.Lbl(name, 14, color, true));
                row.Children.Add(PageHelpers.Lbl(lic, 12, "#A0A0A0"));
                card.Child = row; sp.Children.Add(card);
            }
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }

    // Admin Panel
    public class AdminPanelPage : Page
    {
        private readonly MainWindow _main;
        private readonly StackPanel _mainContainer;
        private TabControl _tabControl = null!;
        private StackPanel _usersContainer = null!;
        private TextBlock _statsSummaryLbl = null!;
        private TextBox _searchBox = null!;
        private JObject? _cachedUsersData;

        public AdminPanelPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;

            _mainContainer = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };

            // Login / Password Verification Card
            var pwdCard = PageHelpers.Card("#1a1015", 12, "#A349A4", new Thickness(0, 16, 0, 0));
            var pwdSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            pwdSp.Children.Add(PageHelpers.Lbl("Yonetici Sifresi", 12, "#A0A0A0"));
            var pwdBox = new PasswordBox { Background = PageHelpers.HexBrush("#222"),
                Foreground = Brushes.White, BorderBrush = PageHelpers.HexBrush("#333"),
                BorderThickness = new Thickness(1), Padding = new Thickness(10, 8, 10, 8),
                FontFamily = new FontFamily("Segoe UI"), Height = 36 };
            pwdSp.Children.Add(pwdBox);

            var verifyBtn = PageHelpers.MkBtn("DOGRULA", "#A349A4", 120);
            verifyBtn.Margin = new Thickness(0, 12, 0, 0); verifyBtn.HorizontalAlignment = HorizontalAlignment.Left;
            
            verifyBtn.Click += (_, _) => {
                if (pwdBox.Password == App.AdminPassword)
                {
                    _mainContainer.Children.Remove(pwdCard);
                    BuildAdminConsole();
                }
                else
                {
                    MessageBox.Show("Yanlis sifre!", "Erisim Reddedildi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            pwdSp.Children.Add(verifyBtn);
            pwdCard.Child = pwdSp;
            _mainContainer.Children.Add(pwdCard);

            Content = new ScrollViewer { Content = _mainContainer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void BuildAdminConsole()
        {
            // Title
            _mainContainer.Children.Add(PageHelpers.Lbl("👑  Yönetici Konsolu (Firebase Dashboard)", 24, "#FFFFFF", true, new Thickness(0, 0, 0, 14)));

            // Styled TabControl for Admin sections
            _tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Styled TabItem Resource
            var style = new Style(typeof(TabItem));
            style.Setters.Add(new Setter(TabItem.BackgroundProperty, PageHelpers.HexBrush("#181818")));
            style.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(16, 8, 16, 8)));
            style.Setters.Add(new Setter(TabItem.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(TabItem.CursorProperty, System.Windows.Input.Cursors.Hand));
            _tabControl.Resources.Add(typeof(TabItem), style);

            // Tab 1: Live User Analytics
            var usersTab = new TabItem { Header = "👥  Oyuncu Analizleri & Loglar" };
            usersTab.Content = BuildUsersTab();
            _tabControl.Items.Add(usersTab);

            // Tab 2: Cloud Update
            var updateTab = new TabItem { Header = "🔄  Bulut Güncelleme Dağıtımı" };
            updateTab.Content = BuildUpdateTab();
            _tabControl.Items.Add(updateTab);

            // Tab 3: Update Rollback
            var rollbackTab = new TabItem { Header = "⏪  Bulut Güncelleme Geri Al" };
            rollbackTab.Content = BuildRollbackTab();
            _tabControl.Items.Add(rollbackTab);

            // Tab 4: Live Dashboard
            var dashTab = new TabItem { Header = "📊  Canlı İstatistikler" };
            dashTab.Content = BuildDashboardTab();
            _tabControl.Items.Add(dashTab);

            // Tab 5: Batch Operations
            var batchTab = new TabItem { Header = "⚡  Toplu İşlemler" };
            batchTab.Content = BuildBatchOperationsTab();
            _tabControl.Items.Add(batchTab);

            // Tab 6: Database Management
            var dbTab = new TabItem { Header = "💾  Veritabanı Yönetimi" };
            dbTab.Content = BuildDatabaseTab();
            _tabControl.Items.Add(dbTab);

            _mainContainer.Children.Add(_tabControl);

            // Load Firebase Users immediately
            RefreshUsersList();
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── TAB 4: Canlı İstatistikler Dashboard ─────────────────────────────
        // ══════════════════════════════════════════════════════════════════════
        private UIElement BuildDashboardTab()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            sp.Children.Add(PageHelpers.Lbl("📊 CANLI İSTATİSTİKLER DASHBOARD", 16, "#00FFCC", true));
            sp.Children.Add(PageHelpers.Lbl("Firebase veritabanından anlık çekilen premium istatistikler", 11, "#A0A0A0", pad: new Thickness(0, 2, 0, 14)));

            // Stat cards container
            var _dashStatsPanel = new StackPanel();
            sp.Children.Add(_dashStatsPanel);

            // Load button
            var loadBtn = PageHelpers.MkBtn("📊 İSTATİSTİKLERİ YÜKLE / YENİLE", "#00FFCC", 260);
            loadBtn.Foreground = Brushes.Black;
            loadBtn.FontWeight = FontWeights.Bold;
            loadBtn.Margin = new Thickness(0, 0, 0, 14);
            loadBtn.Click += async (_, _) =>
            {
                loadBtn.IsEnabled = false;
                loadBtn.Content = "Yükleniyor...";
                _dashStatsPanel.Children.Clear();
                _dashStatsPanel.Children.Add(PageHelpers.Lbl("🔄 Firebase'den veriler çekiliyor...", 12, "#FFB100"));

                try
                {
                    var jsonStr = await MistikAnalytics.GetAllUsersAsync();
                    if (string.IsNullOrEmpty(jsonStr) || jsonStr == "null")
                    {
                        _dashStatsPanel.Children.Clear();
                        _dashStatsPanel.Children.Add(PageHelpers.Lbl("Veritabanında henüz veri yok.", 12, "#888"));
                        return;
                    }

                    var allUsers = JObject.Parse(jsonStr);
                    int totalPlayers = 0, onlinePlayers = 0, totalCrashes = 0, totalMods = 0;
                    int totalGameLaunches = 0, bannedPlayers = 0;
                    string mostActivePlayer = "-", mostActiveCount = "0";
                    string mostPopularVersion = "-";
                    var versionCounts = new Dictionary<string, int>();
                    var recentCrashes = new List<(string user, string error, string time)>();
                    DateTime cutoff24h = DateTime.UtcNow.AddHours(-24);
                    int active24h = 0;

                    foreach (var prop in allUsers.Properties())
                    {
                        totalPlayers++;
                        var u = prop.Value as JObject;
                        if (u == null) continue;
                        var profile = u["profile"] as JObject;
                        var crashes = u["crashes"];
                        var mods = u["installed_mods"];
                        var gameHistory = u["game_history"];

                        string status = profile?["status"]?.ToString() ?? "offline";
                        if (status.Equals("online", StringComparison.OrdinalIgnoreCase)) onlinePlayers++;

                        bool isBanned = profile?["banned"]?.Value<bool>() ?? false;
                        if (isBanned) bannedPlayers++;

                        int cc = 0;
                        if (crashes is JObject co) cc = co.Count;
                        else if (crashes is JArray ca) cc = ca.Count;
                        totalCrashes += cc;

                        // Recent crashes
                        if (crashes is JObject crashObj)
                        {
                            foreach (var cp in crashObj.Properties())
                            {
                                if (cp.Value is JObject cVal)
                                {
                                    recentCrashes.Add((prop.Name, cVal["error"]?.ToString() ?? "?", cVal["timestamp"]?.ToString() ?? ""));
                                }
                            }
                        }

                        int mc = 0;
                        if (mods is JObject mo) mc = mo.Count;
                        else if (mods is JArray ma) mc = ma.Count;
                        totalMods += mc;

                        int gh = 0;
                        if (gameHistory is JObject gho) gh = gho.Count;
                        else if (gameHistory is JArray gha) gh = gha.Count;
                        totalGameLaunches += gh;

                        int openCount = int.TryParse(profile?["open_count"]?.ToString(), out int oc) ? oc : 0;
                        if (openCount > int.Parse(mostActiveCount))
                        {
                            mostActiveCount = openCount.ToString();
                            mostActivePlayer = prop.Name;
                        }

                        string gv = profile?["game_version"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(gv))
                        {
                            if (!versionCounts.ContainsKey(gv)) versionCounts[gv] = 0;
                            versionCounts[gv]++;
                        }

                        string lastActive = profile?["last_active"]?.ToString() ?? "";
                        if (DateTime.TryParse(lastActive, out DateTime laTime) && laTime > cutoff24h) active24h++;
                    }

                    mostPopularVersion = versionCounts.Count > 0
                        ? versionCounts.OrderByDescending(x => x.Value).First().Key
                        : "-";

                    _dashStatsPanel.Children.Clear();

                    // Row 1: Big number cards
                    var row1 = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    for (int i = 0; i < 4; i++) row1.ColumnDefinitions.Add(new ColumnDefinition());

                    AddStatCard(row1, 0, "👥", totalPlayers.ToString(), "Toplam Oyuncu", "#00A3FF");
                    AddStatCard(row1, 1, "🟢", onlinePlayers.ToString(), "Çevrimiçi", "#2EB82E");
                    AddStatCard(row1, 2, "⚠️", totalCrashes.ToString(), "Toplam Hata", "#FF4B4B");
                    AddStatCard(row1, 3, "📦", totalMods.ToString(), "Mod Kurulumu", "#A349A4");
                    _dashStatsPanel.Children.Add(row1);

                    // Row 2
                    var row2 = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    for (int i = 0; i < 4; i++) row2.ColumnDefinitions.Add(new ColumnDefinition());

                    AddStatCard(row2, 0, "🚀", totalGameLaunches.ToString(), "Oyun Başlatma", "#FFB100");
                    AddStatCard(row2, 1, "🕐", active24h.ToString(), "Son 24 Saat", "#00FFCC");
                    AddStatCard(row2, 2, "🚫", bannedPlayers.ToString(), "Banlı Oyuncu", "#FF6666");
                    AddStatCard(row2, 3, "🎮", mostPopularVersion, "En Popüler Sürüm", "#8888FF");
                    _dashStatsPanel.Children.Add(row2);

                    // Most active player card
                    var activeCard = PageHelpers.Card("#161a1e", 10, "#FFB100", new Thickness(0, 0, 0, 14));
                    var acSp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
                    acSp.Children.Add(PageHelpers.Lbl($"👑 En Aktif Oyuncu: {mostActivePlayer} ({mostActiveCount} açılış)", 13, "#FFB100", true));
                    activeCard.Child = acSp;
                    _dashStatsPanel.Children.Add(activeCard);

                    // Recent crashes list
                    if (recentCrashes.Count > 0)
                    {
                        _dashStatsPanel.Children.Add(PageHelpers.Lbl("⚠️ Son Çökmeler", 13, "#FF4B4B", true, new Thickness(0, 8, 0, 6)));
                        var sortedCrashes = recentCrashes.OrderByDescending(x => x.time).Take(8).ToList();
                        foreach (var (user, error, time) in sortedCrashes)
                        {
                            DateTime.TryParse(time, out DateTime dt);
                            string ts = dt != DateTime.MinValue ? dt.ToLocalTime().ToString("dd.MM HH:mm") : "?";
                            var row = PageHelpers.Card("#2a1215", 6, margin: new Thickness(0, 0, 0, 4));
                            var rSp = new StackPanel { Margin = new Thickness(12, 6, 12, 6) };
                            rSp.Children.Add(PageHelpers.Lbl($"⚠ {user}: {(error.Length > 80 ? error[..77] + "..." : error)}", 10, "#FF8888", wrap: TextWrapping.Wrap));
                            rSp.Children.Add(PageHelpers.Lbl($"⏰ {ts}", 9, "#666"));
                            row.Child = rSp;
                            _dashStatsPanel.Children.Add(row);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _dashStatsPanel.Children.Clear();
                    _dashStatsPanel.Children.Add(PageHelpers.Lbl($"❌ Hata: {ex.Message}", 12, "#FF4B4B"));
                }
                finally
                {
                    loadBtn.Content = "📊 İSTATİSTİKLERİ YÜKLE / YENİLE";
                    loadBtn.IsEnabled = true;
                }
            };
            sp.Children.Add(loadBtn);
            return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void AddStatCard(Grid grid, int col, string icon, string value, string label, string color)
        {
            var card = PageHelpers.Card("#111116", 10, color, new Thickness(col > 0 ? 6 : 0, 0, 0, 0));
            var cSp = new StackPanel { Margin = new Thickness(14, 12, 14, 12), HorizontalAlignment = HorizontalAlignment.Center };
            cSp.Children.Add(PageHelpers.Lbl(icon, 20, color, pad: new Thickness(0, 0, 0, 2)));
            cSp.Children.Add(PageHelpers.Lbl(value, 28, "#FFFFFF", true));
            cSp.Children.Add(PageHelpers.Lbl(label, 10, "#A0A0A0"));
            card.Child = cSp;
            Grid.SetColumn(card, col);
            grid.Children.Add(card);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── TAB 5: Toplu İşlemler ────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════
        private UIElement BuildBatchOperationsTab()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            sp.Children.Add(PageHelpers.Lbl("⚡ TOPLU İŞLEMLER MERKEZİ", 16, "#FFB100", true));
            sp.Children.Add(PageHelpers.Lbl("Birden fazla oyuncuyu aynı anda yönetin — premium yönetici araçları", 11, "#A0A0A0", pad: new Thickness(0, 2, 0, 14)));

            // ── 1. Toplu Mesaj Gönderme ──
            var msgCard = PageHelpers.Card("#111116", 12, "#00A3FF", new Thickness(0, 0, 0, 14));
            var msgSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            msgSp.Children.Add(PageHelpers.Lbl("📢 TÜM OYUNCULARA TOPLU MESAJ GÖNDER", 14, "#00A3FF", true));
            msgSp.Children.Add(PageHelpers.Lbl("Bu mesaj tüm kayıtlı oyuncuların Launcher'ında pop-up olarak görünecektir.", 10, "#A0A0A0", pad: new Thickness(0, 4, 0, 8)));

            var broadcastBox = PageHelpers.DarkTextBox("Tüm oyunculara gönderilecek mesaj...");
            broadcastBox.Height = 70;
            broadcastBox.AcceptsReturn = true;
            broadcastBox.TextWrapping = TextWrapping.Wrap;
            msgSp.Children.Add(broadcastBox);

            var broadcastBtn = PageHelpers.MkBtn("📢 HERKESE GÖNDER", "#00A3FF", 180);
            broadcastBtn.Margin = new Thickness(0, 10, 0, 0);
            broadcastBtn.Click += async (_, _) =>
            {
                string msg = broadcastBox.Text.Trim();
                if (string.IsNullOrEmpty(msg) || msg == "Tüm oyunculara gönderilecek mesaj...") return;

                var confirm = MessageBox.Show($"Aşağıdaki mesajı TÜM kayıtlı oyunculara göndermek istiyor musunuz?\n\n\"{msg}\"",
                    "Toplu Mesaj Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                broadcastBtn.IsEnabled = false;
                broadcastBtn.Content = "Gönderiliyor...";
                try
                {
                    await MistikAnalytics.SendBroadcastMessageAsync(msg);
                    MessageBox.Show("Mesaj tüm oyunculara başarıyla gönderildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Toplu Mesaj Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    broadcastBtn.Content = "📢 HERKESE GÖNDER";
                    broadcastBtn.IsEnabled = true;
                }
            };
            msgSp.Children.Add(broadcastBtn);
            msgCard.Child = msgSp;
            sp.Children.Add(msgCard);

            // ── 2. Toplu Ban ──
            var banCard = PageHelpers.Card("#1a1015", 12, "#FF4B4B", new Thickness(0, 0, 0, 14));
            var banSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            banSp.Children.Add(PageHelpers.Lbl("🚫 TOPLU BANLAMA", 14, "#FF4B4B", true));
            banSp.Children.Add(PageHelpers.Lbl("Birden fazla oyuncuyu virgülle ayırarak girin. Tümü aynı anda banlanır.", 10, "#A0A0A0", pad: new Thickness(0, 4, 0, 8)));

            var banNamesBox = PageHelpers.DarkTextBox("Oyuncu1, Oyuncu2, Oyuncu3...");
            banSp.Children.Add(banNamesBox);

            var banRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var batchBanBtn = PageHelpers.MkBtn("❌ HEPSİNİ BANLA", "#FF4B4B", 160);
            batchBanBtn.Click += async (_, _) =>
            {
                var names = banNamesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (names.Count == 0) return;

                var confirm = MessageBox.Show($"{names.Count} oyuncuyu banlamak istiyor musunuz?\n\n{string.Join(", ", names)}",
                    "Toplu Ban Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                batchBanBtn.IsEnabled = false;
                batchBanBtn.Content = "Banlanıyor...";
                try
                {
                    await MistikAnalytics.BanMultipleUsersAsync(names, true);
                    MessageBox.Show($"{names.Count} oyuncu başarıyla banlandı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshUsersList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Toplu Ban Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    batchBanBtn.Content = "❌ HEPSİNİ BANLA";
                    batchBanBtn.IsEnabled = true;
                }
            };
            banRow.Children.Add(batchBanBtn);

            var batchUnbanBtn = PageHelpers.MkBtn("🟢 HEPSİNİN BANINI KALDIR", "#2EB82E", 200);
            batchUnbanBtn.Margin = new Thickness(10, 0, 0, 0);
            batchUnbanBtn.Click += async (_, _) =>
            {
                var names = banNamesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (names.Count == 0) return;

                batchUnbanBtn.IsEnabled = false;
                try
                {
                    await MistikAnalytics.BanMultipleUsersAsync(names, false);
                    MessageBox.Show($"{names.Count} oyuncunun banı kaldırıldı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshUsersList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    batchUnbanBtn.IsEnabled = true;
                }
            };
            banRow.Children.Add(batchUnbanBtn);
            banSp.Children.Add(banRow);
            banCard.Child = banSp;
            sp.Children.Add(banCard);

            // ── 3. Tüm Logları Temizle ──
            var logCard = PageHelpers.Card("#1a1a10", 12, "#FFB100", new Thickness(0, 0, 0, 14));
            var logSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            logSp.Children.Add(PageHelpers.Lbl("🧹 TÜM ÇÖKME LOGLARINI TEMİZLE", 14, "#FFB100", true));
            logSp.Children.Add(PageHelpers.Lbl("Firebase veritabanındaki TÜM oyuncuların çökme/hata kayıtlarını sıfırlar. Bu işlem geri alınamaz!", 10, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 8)));

            var clearAllLogsBtn = PageHelpers.MkBtn("🧹 TÜM LOGLARI SİL", "#FFB100", 180);
            clearAllLogsBtn.Foreground = Brushes.Black;
            clearAllLogsBtn.Click += async (_, _) =>
            {
                var confirm = MessageBox.Show("TÜM oyuncuların çökme loglarını silmek istiyor musunuz?\n\nBu işlem GERİ ALINAMAZ!",
                    "Toplu Log Temizleme", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                clearAllLogsBtn.IsEnabled = false;
                clearAllLogsBtn.Content = "Temizleniyor...";
                try
                {
                    await MistikAnalytics.DeleteAllCrashLogsAsync();
                    MessageBox.Show("Tüm çökme logları başarıyla silindi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshUsersList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    clearAllLogsBtn.Content = "🧹 TÜM LOGLARI SİL";
                    clearAllLogsBtn.IsEnabled = true;
                }
            };
            logSp.Children.Add(clearAllLogsBtn);
            logCard.Child = logSp;
            sp.Children.Add(logCard);

            return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── TAB 6: Veritabanı Yönetimi ───────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════
        private UIElement BuildDatabaseTab()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            sp.Children.Add(PageHelpers.Lbl("💾 VERİTABANI YÖNETİMİ", 16, "#A349A4", true));
            sp.Children.Add(PageHelpers.Lbl("Firebase Realtime Database üzerinde gelişmiş yönetim araçları", 11, "#A0A0A0", pad: new Thickness(0, 2, 0, 14)));

            // ── 1. JSON Dışa Aktarma ──
            var exportCard = PageHelpers.Card("#111116", 12, "#00A3FF", new Thickness(0, 0, 0, 14));
            var expSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            expSp.Children.Add(PageHelpers.Lbl("📥 VERİTABANI DIŞA AKTARMA (JSON EXPORT)", 14, "#00A3FF", true));
            expSp.Children.Add(PageHelpers.Lbl("Tüm Firebase verilerini JSON dosyası olarak bilgisayarınıza indirin. Yedekleme ve analiz için idealdir.", 10, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 8)));

            var exportBtn = PageHelpers.MkBtn("📥 JSON OLARAK İNDİR", "#00A3FF", 200);
            exportBtn.Click += async (_, _) =>
            {
                exportBtn.IsEnabled = false;
                exportBtn.Content = "İndiriliyor...";
                try
                {
                    var json = await MistikAnalytics.ExportDatabaseAsync();
                    if (string.IsNullOrEmpty(json) || json == "null")
                    {
                        MessageBox.Show("Veritabanı boş!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var sfd = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "JSON Dosyası (*.json)|*.json",
                        FileName = $"mistik_db_backup_{DateTime.Now:yyyyMMdd_HHmm}.json",
                        Title = "Veritabanı Yedeğini Kaydet"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        // Pretty print
                        try
                        {
                            var parsed = JObject.Parse(json);
                            await System.IO.File.WriteAllTextAsync(sfd.FileName, parsed.ToString(Newtonsoft.Json.Formatting.Indented));
                        }
                        catch
                        {
                            await System.IO.File.WriteAllTextAsync(sfd.FileName, json);
                        }
                        MessageBox.Show($"Veritabanı başarıyla dışa aktarıldı!\n\nDosya: {sfd.FileName}", "Export Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    exportBtn.Content = "📥 JSON OLARAK İNDİR";
                    exportBtn.IsEnabled = true;
                }
            };
            expSp.Children.Add(exportBtn);
            exportCard.Child = expSp;
            sp.Children.Add(exportCard);

            // ── 2. İnaktif Kullanıcı Temizleme ──
            var cleanCard = PageHelpers.Card("#1a1510", 12, "#FFB100", new Thickness(0, 0, 0, 14));
            var clSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            clSp.Children.Add(PageHelpers.Lbl("🗑 İNAKTİF KULLANICILARI TEMİZLE", 14, "#FFB100", true));
            clSp.Children.Add(PageHelpers.Lbl("Belirli süre boyunca hiç aktif olmayan kullanıcıları veritabanından otomatik temizler. Veritabanı boyutunu küçültür ve performansı artırır.", 10, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 8)));

            var daysRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            daysRow.Children.Add(PageHelpers.Lbl("İnaktif Eşiği (Gün):", 11, "#FFFFFF"));
            var daysBox = PageHelpers.DarkTextBox("30");
            daysBox.Width = 60;
            daysBox.Margin = new Thickness(8, 0, 0, 0);
            daysRow.Children.Add(daysBox);
            clSp.Children.Add(daysRow);

            var cleanUsersBtn = PageHelpers.MkBtn("🗑 İNAKTİF KULLANICILARI SİL", "#FFB100", 240);
            cleanUsersBtn.Foreground = Brushes.Black;
            cleanUsersBtn.Click += async (_, _) =>
            {
                if (!int.TryParse(daysBox.Text.Trim(), out int days) || days < 1)
                {
                    MessageBox.Show("Geçerli bir gün sayısı girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show($"{days} günden uzun süredir aktif olmayan kullanıcıları silmek istiyor musunuz?\n\nBu işlem GERİ ALINAMAZ!",
                    "İnaktif Temizleme", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                cleanUsersBtn.IsEnabled = false;
                cleanUsersBtn.Content = "Temizleniyor...";
                try
                {
                    int deleted = await MistikAnalytics.CleanInactiveUsersAsync(days);
                    MessageBox.Show($"{deleted} inaktif kullanıcı başarıyla temizlendi!", "Temizlik Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshUsersList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    cleanUsersBtn.Content = "🗑 İNAKTİF KULLANICILARI SİL";
                    cleanUsersBtn.IsEnabled = true;
                }
            };
            clSp.Children.Add(cleanUsersBtn);
            cleanCard.Child = clSp;
            sp.Children.Add(cleanCard);

            // ── 3. Veritabanı Sağlık Kontrolü ──
            var healthCard = PageHelpers.Card("#101a16", 12, "#2EB82E", new Thickness(0, 0, 0, 14));
            var hSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            hSp.Children.Add(PageHelpers.Lbl("🏥 VERİTABANI SAĞLIK KONTROLÜ", 14, "#2EB82E", true));

            var _healthPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            hSp.Children.Add(_healthPanel);

            var healthBtn = PageHelpers.MkBtn("🏥 SAĞLIK KONTROLÜ BAŞLAT", "#2EB82E", 220);
            healthBtn.Foreground = Brushes.White;
            healthBtn.Click += async (_, _) =>
            {
                healthBtn.IsEnabled = false;
                healthBtn.Content = "Kontrol ediliyor...";
                _healthPanel.Children.Clear();

                try
                {
                    var json = await MistikAnalytics.ExportDatabaseAsync();
                    if (string.IsNullOrEmpty(json) || json == "null")
                    {
                        _healthPanel.Children.Add(PageHelpers.Lbl("⚠️ Veritabanı boş.", 11, "#FFB100"));
                        return;
                    }

                    int sizeBytes = System.Text.Encoding.UTF8.GetByteCount(json);
                    double sizeMB = sizeBytes / (1024.0 * 1024.0);
                    var parsed = JObject.Parse(json);
                    int userCount = parsed.Count;

                    string sizeStatus = sizeMB < 5 ? "🟢 Sağlıklı" : sizeMB < 20 ? "🟡 Orta" : "🔴 Büyük";
                    string sizeColor = sizeMB < 5 ? "#2EB82E" : sizeMB < 20 ? "#FFB100" : "#FF4B4B";

                    _healthPanel.Children.Add(PageHelpers.Lbl($"📊 Toplam Boyut: {sizeMB:F2} MB  —  {sizeStatus}", 12, sizeColor, true));
                    _healthPanel.Children.Add(PageHelpers.Lbl($"👥 Kayıtlı Kullanıcı: {userCount}", 11, "#CCCCCC", pad: new Thickness(0, 4, 0, 0)));
                    _healthPanel.Children.Add(PageHelpers.Lbl($"📝 JSON Karakter: {json.Length:N0}", 11, "#CCCCCC", pad: new Thickness(0, 2, 0, 0)));
                    _healthPanel.Children.Add(PageHelpers.Lbl($"🔗 Firebase Sınırı: 1 GB (Kullanım: %{(sizeMB / 1024 * 100):F4})", 11, "#888", pad: new Thickness(0, 2, 0, 0)));

                    if (sizeMB > 50)
                        _healthPanel.Children.Add(PageHelpers.Lbl("⚠️ ÖNERİ: Veritabanı büyük. İnaktif kullanıcıları temizleyin veya eski logları silin.", 11, "#FF4B4B", wrap: TextWrapping.Wrap, pad: new Thickness(0, 8, 0, 0)));
                    else
                        _healthPanel.Children.Add(PageHelpers.Lbl("✅ Veritabanı sağlığı iyi durumda.", 11, "#2EB82E", pad: new Thickness(0, 8, 0, 0)));
                }
                catch (Exception ex)
                {
                    _healthPanel.Children.Add(PageHelpers.Lbl($"❌ Sağlık kontrolü hatası: {ex.Message}", 11, "#FF4B4B"));
                }
                finally
                {
                    healthBtn.Content = "🏥 SAĞLIK KONTROLÜ BAŞLAT";
                    healthBtn.IsEnabled = true;
                }
            };
            hSp.Children.Add(healthBtn);
            healthCard.Child = hSp;
            sp.Children.Add(healthCard);

            return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildUsersTab()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            // Stats Card
            var statsCard = PageHelpers.Card("#111", 10, "#00A3FF", new Thickness(0, 0, 0, 14));
            var statsSp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            _statsSummaryLbl = PageHelpers.Lbl("Veriler Firebase'den yükleniyor...", 12, "#FFFFFF", bold: true);
            statsSp.Children.Add(_statsSummaryLbl);
            statsCard.Child = statsSp;
            sp.Children.Add(statsCard);

            // Actions row (Search + Refresh)
            var actionGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition());
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _searchBox = PageHelpers.DarkTextBox("Oyuncu adı ara...");
            _searchBox.Width = 300;
            _searchBox.HorizontalAlignment = HorizontalAlignment.Left;
            _searchBox.TextChanged += (s, e) => FilterUsersList();
            _searchBox.GotFocus += (s, e) => { if (_searchBox.Text == "Oyuncu adı ara...") _searchBox.Text = ""; };
            _searchBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_searchBox.Text)) _searchBox.Text = "Oyuncu adı ara..."; };
            Grid.SetColumn(_searchBox, 0);
            actionGrid.Children.Add(_searchBox);

            var refreshBtn = PageHelpers.MkBtn("🔄 VERİLERİ YENİLE", "#2EB82E", 160);
            refreshBtn.Height = 35;
            refreshBtn.Click += (s, e) => RefreshUsersList();
            Grid.SetColumn(refreshBtn, 1);
            actionGrid.Children.Add(refreshBtn);

            sp.Children.Add(actionGrid);

            // Users list container
            _usersContainer = new StackPanel();
            sp.Children.Add(_usersContainer);

            return sp;
        }

        private async void RefreshUsersList()
        {
            if (_usersContainer == null) return;

            _usersContainer.Children.Clear();
            _usersContainer.Children.Add(PageHelpers.Lbl("🔄 Firebase Realtime Database verileri çekiliyor, lütfen bekleyin...", 12, "#FFB100"));
            
            try
            {
                var jsonStr = await MistikAnalytics.GetAllUsersAsync();
                if (string.IsNullOrEmpty(jsonStr) || jsonStr == "null")
                {
                    _usersContainer.Children.Clear();
                    _usersContainer.Children.Add(PageHelpers.Lbl("📭 Kayıtlı aktif oyuncu bulunamadı. Launcher'ı ilk kez kullanan biri olduğunda veriler burada listelenecektir.", 12, "#A0A0A0"));
                    _statsSummaryLbl.Text = "Toplam Oyuncu: 0  |  Çevrimiçi: 0  |  Toplam Hata: 0";
                    return;
                }

                _cachedUsersData = JObject.Parse(jsonStr);
                FilterUsersList();
            }
            catch (Exception ex)
            {
                _usersContainer.Children.Clear();
                _usersContainer.Children.Add(PageHelpers.Lbl($"❌ Veritabanı hatası: {ex.Message}", 12, "#FF4B4B"));
            }
        }

        private void FilterUsersList()
        {
            if (_cachedUsersData == null || _usersContainer == null) return;

            _usersContainer.Children.Clear();
            string query = _searchBox.Text.Trim().ToLower();
            if (query == "oyuncu adı ara...") query = "";

            int totalUsers = 0;
            int onlineUsers = 0;
            int totalCrashes = 0;

            foreach (var prop in _cachedUsersData.Properties())
            {
                string rawUser = prop.Name;
                var userVal = prop.Value as JObject;
                if (userVal == null) continue;

                totalUsers++;
                var profile = userVal["profile"] as JObject;
                var crashesObj = userVal["crashes"] as JObject;
                
                int crashCount = crashesObj?.Count ?? 0;
                totalCrashes += crashCount;

                string status = profile?["status"]?.ToString() ?? "offline";
                if (status.ToLower() == "online") onlineUsers++;

                // Search Filter
                if (!string.IsNullOrEmpty(query) && !rawUser.ToLower().Contains(query)) continue;

                // Build User card
                var userCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 0, 0, 10));
                var userSp = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };

                var mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition());
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftSp = new StackPanel();
                string statusLed = status.ToLower() == "online" ? "🟢  " : "🔴  ";
                
                var nameLbl = PageHelpers.Lbl($"{statusLed}{rawUser}", 16, "#FFFFFF", bold: true);
                leftSp.Children.Add(nameLbl);

                string launcherVer = profile?["launcher_version"]?.ToString() ?? "?";
                string gameVer = profile?["game_version"]?.ToString() ?? "?";
                string os = profile?["os"]?.ToString() ?? "Bilinmiyor";
                string ram = profile?["ram_gb"]?.ToString() ?? "?";
                string openCount = profile?["open_count"]?.ToString() ?? "1";
                string lastActive = profile?["last_active"]?.ToString() ?? "";
                
                DateTime.TryParse(lastActive, out DateTime lastActiveTime);
                string relativeTime = lastActiveTime != DateTime.MinValue ? lastActiveTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "Bilinmiyor";

                leftSp.Children.Add(PageHelpers.Lbl($"🎮 Sürüm: {gameVer} (L: {launcherVer})  |  🖥️ OS: {os}  |  🧠 RAM: {ram} GB", 11, "#A0A0A0", pad: new Thickness(0, 4, 0, 0)));
                leftSp.Children.Add(PageHelpers.Lbl($"🚀 Toplam Açılış: {openCount}  |  📅 Son Aktif: {relativeTime}  |  ⚠️ Kayıtlı Hatalar: {crashCount}", 11, crashCount > 0 ? "#FF4B4B" : "#888888", pad: new Thickness(0, 2, 0, 0)));

                Grid.SetColumn(leftSp, 0);
                mainGrid.Children.Add(leftSp);

                var rightSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var detailsBtn = PageHelpers.MkBtn("🔍 DETAYLARI GÖR", "#00A3FF", 140);
                detailsBtn.Height = 32;
                
                var capturedUser = rawUser;
                var capturedUserVal = userVal;
                detailsBtn.Click += (s, e) => ShowUserDetails(capturedUser, capturedUserVal);
                rightSp.Children.Add(detailsBtn);

                Grid.SetColumn(rightSp, 1);
                mainGrid.Children.Add(rightSp);

                userSp.Children.Add(mainGrid);
                userCard.Child = userSp;
                _usersContainer.Children.Add(userCard);
            }

            _statsSummaryLbl.Text = $"📊 TOPLAM KAYITLI OYUNCU: {totalUsers}   |   🟢 AKTİF / ÇEVRİMİÇİ: {onlineUsers}   |   ⚠️ TOPLAM RAPORLANAN HATA: {totalCrashes}";
            if (_usersContainer.Children.Count == 0)
            {
                _usersContainer.Children.Add(PageHelpers.Lbl("🔍 Arama kriterlerinize uyan oyuncu bulunamadı.", 12, "#A0A0A0"));
            }
        }

        private void ShowUserDetails(string username, JObject userData)
        {
            var profile = userData["profile"] as JObject;
            var crashes = userData["crashes"];
            var gameHistory = userData["game_history"];
            var installedMods = userData["installed_mods"];

            var detailsWindow = new Window
            {
                Title = $"Oyuncu Detayları: {username}",
                Width = 720,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = PageHelpers.HexBrush("#0D0D0D"),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI")
            };

            var mainSp = new StackPanel { Margin = new Thickness(24) };
            
            // Header card
            var headerCard = PageHelpers.Card("#141414", 12, "#00A3FF", new Thickness(0, 0, 0, 16));
            var headerSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            headerSp.Children.Add(PageHelpers.Lbl($"👤 {username} - Sistem & Profil Bilgileri", 16, "#FFFFFF", bold: true));
            headerSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 8, 0, 12) });
            
            string os = profile?["os"]?.ToString() ?? "Bilinmiyor";
            string ram = profile?["ram_gb"]?.ToString() ?? "?";
            string openCount = profile?["open_count"]?.ToString() ?? "1";
            string lVer = profile?["launcher_version"]?.ToString() ?? "?";
            string gVer = profile?["game_version"]?.ToString() ?? "?";
            
            headerSp.Children.Add(PageHelpers.Lbl($"• İşletim Sistemi: {os}", 11, "#CCCCCC"));
            headerSp.Children.Add(PageHelpers.Lbl($"• RAM Kapasitesi: {ram} GB", 11, "#CCCCCC", pad: new Thickness(0, 3, 0, 0)));
            headerSp.Children.Add(PageHelpers.Lbl($"• Başlatıcı Sürümü: {lVer}", 11, "#CCCCCC", pad: new Thickness(0, 3, 0, 0)));
            headerSp.Children.Add(PageHelpers.Lbl($"• Seçili Sürüm: {gVer}", 11, "#CCCCCC", pad: new Thickness(0, 3, 0, 0)));
            headerSp.Children.Add(PageHelpers.Lbl($"• Toplam Launcher Açılışı: {openCount} kez", 11, "#CCCCCC", pad: new Thickness(0, 3, 0, 0)));

            // GPU bilgisi
            string gpuName = profile?["gpu"]?.ToString() ?? "Bilinmiyor";
            headerSp.Children.Add(PageHelpers.Lbl($"• GPU (Ekran Kartı): {gpuName}", 11, "#00FFCC", pad: new Thickness(0, 3, 0, 0)));

            // Son aktif zaman
            string lastActiveStr = profile?["last_active"]?.ToString() ?? "";
            if (DateTime.TryParse(lastActiveStr, out DateTime lastActiveTime2))
            {
                var diff = DateTime.UtcNow - lastActiveTime2;
                string agoStr = diff.TotalMinutes < 1 ? "Az önce" :
                                diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes} dk önce" :
                                diff.TotalDays < 1 ? $"{(int)diff.TotalHours} saat önce" :
                                $"{(int)diff.TotalDays} gün önce";
                headerSp.Children.Add(PageHelpers.Lbl($"• Son Aktif: {lastActiveTime2.ToLocalTime():dd.MM.yyyy HH:mm} ({agoStr})", 11, "#888888", pad: new Thickness(0, 3, 0, 0)));
            }

            bool isBanned = profile?["banned"]?.Value<bool>() ?? false;
            string banStatusText = isBanned ? "❌ HESAP ENGELLİ (BANLI)" : "🟢 AKTİF (BANSIZ)";
            string banStatusColor = isBanned ? "#FF4B4B" : "#2EB82E";
            headerSp.Children.Add(PageHelpers.Lbl($"• Hesap Durumu: {banStatusText}", 11, banStatusColor, bold: true, pad: new Thickness(0, 3, 0, 0)));

            headerCard.Child = headerSp;
            mainSp.Children.Add(headerCard);

            // Sub TabControl for User Data
            var subTab = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            
            var tabStyle = new Style(typeof(TabItem));
            tabStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            tabStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(14, 8, 14, 8)));
            tabStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, 12.0));
            tabStyle.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.Bold));
            tabStyle.Setters.Add(new Setter(TabItem.CursorProperty, System.Windows.Input.Cursors.Hand));
            
            // WPF'in varsayılan seçili beyaz arkaplanını ezen modern koyu ControlTemplate
            string templateHtml = @"
                <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                 TargetType='{x:Type TabItem}'>
                    <Border x:Name='Border' BorderThickness='0,0,0,2' BorderBrush='Transparent' Background='#141414' Padding='14,8,14,8' CornerRadius='4,4,0,0' Margin='0,0,4,0'>
                        <ContentPresenter x:Name='ContentSite' VerticalAlignment='Center' HorizontalAlignment='Center'
                                          ContentSource='Header' RecognizesAccessKey='True'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsSelected' Value='True'>
                            <Setter TargetName='Border' Property='Background' Value='#1E1E1E'/>
                            <Setter TargetName='Border' Property='BorderBrush' Value='#00A3FF'/>
                            <Setter Property='Foreground' Value='#00A3FF'/>
                        </Trigger>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Border' Property='Background' Value='#222222'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>";

            try
            {
                var parserContext = new System.Windows.Markup.ParserContext();
                parserContext.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                parserContext.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(templateHtml, parserContext);
                tabStyle.Setters.Add(new Setter(TabItem.TemplateProperty, template));
            }
            catch { }
            
            subTab.Resources.Add(typeof(TabItem), tabStyle);

            // 1. Game History Tab
            var histTab = new TabItem { Header = "🎮 Oyun Geçmişi" };
            var histSp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            if (gameHistory == null || (!gameHistory.HasValues))
            {
                histSp.Children.Add(PageHelpers.Lbl("Oyun başlatma geçmişi bulunamadı.", 11, "#888"));
            }
            else
            {
                var histList = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JObject>();
                if (gameHistory is Newtonsoft.Json.Linq.JObject jo)
                {
                    foreach (var prop in jo.Properties())
                    {
                        if (prop.Value is Newtonsoft.Json.Linq.JObject ho) histList.Add(ho);
                    }
                }
                else if (gameHistory is Newtonsoft.Json.Linq.JArray ja)
                {
                    foreach (var token in ja)
                    {
                        if (token is Newtonsoft.Json.Linq.JObject ho) histList.Add(ho);
                    }
                }

                foreach (var h in histList)
                {
                    string ver = h["version"]?.ToString() ?? "?";
                    string time = h["launched_at"]?.ToString() ?? "";
                    string allocRam = h["ram_allocated"]?.ToString() ?? "?";
                    DateTime.TryParse(time, out DateTime dt);
                    string timeStr = dt != DateTime.MinValue ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : "Bilinmiyor";

                    var row = PageHelpers.Card("#181818", 8, margin: new Thickness(0, 0, 0, 6));
                    var rowSp = new StackPanel { Margin = new Thickness(14, 8, 14, 8) };
                    rowSp.Children.Add(PageHelpers.Lbl($"🎮 Sürüm: {ver}  |  Ayrılmış RAM: {allocRam} GB", 12, "#FFFFFF", bold: true));
                    rowSp.Children.Add(PageHelpers.Lbl($"⏰ Tarih: {timeStr}", 10, "#888888"));
                    row.Child = rowSp;
                    histSp.Children.Add(row);
                }
            }
            histTab.Content = new ScrollViewer { Content = histSp, MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            subTab.Items.Add(histTab);

            // 2. Installed Mods Tab
            var modsTab = new TabItem { Header = "📦 Kurulu Modlar" };
            var modsSp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            if (installedMods == null || (!installedMods.HasValues))
            {
                modsSp.Children.Add(PageHelpers.Lbl("Kurulu mod bilgisi bulunamadı.", 11, "#888"));
            }
            else
            {
                var modList = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JObject>();
                if (installedMods is Newtonsoft.Json.Linq.JObject jo)
                {
                    foreach (var prop in jo.Properties())
                    {
                        if (prop.Value is Newtonsoft.Json.Linq.JObject mo) modList.Add(mo);
                    }
                }
                else if (installedMods is Newtonsoft.Json.Linq.JArray ja)
                {
                    foreach (var token in ja)
                    {
                        if (token is Newtonsoft.Json.Linq.JObject mo) modList.Add(mo);
                    }
                }

                if (modList.Count == 0)
                {
                    modsSp.Children.Add(PageHelpers.Lbl("Kurulu mod bilgisi bulunamadı.", 11, "#888"));
                }
                else
                {
                    foreach (var m in modList)
                    {
                        string name = m["mod_name"]?.ToString() ?? "?";
                        string ver = m["mod_version"]?.ToString() ?? "?";
                        string modGameVer = m["game_version"]?.ToString() ?? "?";
                        string time = m["installed_at"]?.ToString() ?? "";
                        DateTime.TryParse(time, out DateTime dt);
                        string timeStr = dt != DateTime.MinValue ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : "Bilinmiyor";

                        var row = PageHelpers.Card("#181818", 8, margin: new Thickness(0, 0, 0, 6));
                        var rowSp = new StackPanel { Margin = new Thickness(14, 8, 14, 8) };
                        rowSp.Children.Add(PageHelpers.Lbl($"📦 {name} (Sürüm: {ver})", 12, "#00A3FF", bold: true));
                        rowSp.Children.Add(PageHelpers.Lbl($"⏰ Sürüm Uyumluluğu: {modGameVer}  |  Yükleme: {timeStr}", 10, "#A0A0A0"));
                        row.Child = rowSp;
                        modsSp.Children.Add(row);
                    }
                }
            }
            modsTab.Content = new ScrollViewer { Content = modsSp, MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            subTab.Items.Add(modsTab);

            // 3. Crashes & Logs Tab
            var crashTab = new TabItem { Header = "⚠️ Hata & Çökme Raporları" };
            var crashSp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            if (crashes == null || (!crashes.HasValues))
            {
                crashSp.Children.Add(PageHelpers.Lbl("Kayıtlı çökme veya sistem hatası bulunamadı. Mistik temiz!", 12, "#2EB82E", bold: true));
            }
            else
            {
                var crashList = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JObject>();
                if (crashes is Newtonsoft.Json.Linq.JObject jo)
                {
                    foreach (var prop in jo.Properties())
                    {
                        if (prop.Value is Newtonsoft.Json.Linq.JObject co) crashList.Add(co);
                    }
                }
                else if (crashes is Newtonsoft.Json.Linq.JArray ja)
                {
                    foreach (var token in ja)
                    {
                        if (token is Newtonsoft.Json.Linq.JObject co) crashList.Add(co);
                    }
                }

                foreach (var c in crashList)
                {
                    string err = c["error"]?.ToString() ?? "?";
                    string stack = c["stack_trace"]?.ToString() ?? "";
                    string time = c["timestamp"]?.ToString() ?? "";
                    string lVer_err = c["launcher_version"]?.ToString() ?? "?";
                    DateTime.TryParse(time, out DateTime dt);
                    string timeStr = dt != DateTime.MinValue ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : "Bilinmiyor";

                    var row = PageHelpers.Card("#2a1215", 8, "#FF4B4B", new Thickness(0, 0, 0, 8));
                    var rowSp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
                    rowSp.Children.Add(PageHelpers.Lbl($"⚠️ Hata: {err}", 12, "#FF4B4B", bold: true, wrap: TextWrapping.Wrap));
                    rowSp.Children.Add(PageHelpers.Lbl($"⏰ Tarih: {timeStr}  |  Launcher Sürümü: {lVer_err}", 10, "#CCCCCC"));
                    
                    if (!string.IsNullOrEmpty(stack))
                    {
                        var stackCard = new Border { Background = PageHelpers.HexBrush("#111"), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(10) };
                        var stackBox = new TextBox
                        {
                            Text = stack,
                            Background = Brushes.Transparent,
                            Foreground = PageHelpers.HexBrush("#FFB100"),
                            BorderThickness = new Thickness(0),
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            AcceptsReturn = true,
                            FontSize = 9.5,
                            FontFamily = new FontFamily("Consolas"),
                            MaxHeight = 120,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                        };
                        stackCard.Child = stackBox;
                        rowSp.Children.Add(stackCard);
                    }

                    row.Child = rowSp;
                    crashSp.Children.Add(row);
                }
            }
            crashTab.Content = new ScrollViewer { Content = crashSp, MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            subTab.Items.Add(crashTab);

            mainSp.Children.Add(subTab);

            // ── Admin Hızlı Kontrol Butonları ──
            var actionTitle = PageHelpers.Lbl("👑 Yönetici Hızlı Aksiyonları", 13, "#FFB100", bold: true, pad: new Thickness(0, 16, 0, 8));
            mainSp.Children.Add(actionTitle);

            var buttonGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // 1. Ban/Unban Button
            var banBtn = PageHelpers.MkBtn(isBanned ? "🟢 BAN KALDIR" : "❌ HESABI BANLA", isBanned ? "#2EB82E" : "#FF4B4B", 180);
            banBtn.Height = 36;
            banBtn.Click += async (s, e) =>
            {
                var confirm = MessageBox.Show(
                    isBanned ? $"'{username}' adlı oyuncunun banını açmak istiyor musunuz?" : $"'{username}' adlı oyuncuyu banlamak istiyor musunuz?\n\nBu işlem oyuncunun Launcher'a girmesini engelleyecektir.",
                    "Ban Onayı", MessageBoxButton.YesNo, isBanned ? MessageBoxImage.Question : MessageBoxImage.Warning);
                
                if (confirm == MessageBoxResult.Yes)
                {
                    banBtn.IsEnabled = false;
                    try
                    {
                        await MistikAnalytics.TrackBanUserAsync(username, !isBanned);
                        MessageBox.Show("İşlem başarıyla Firebase'e uygulandı! Oyuncu listesini yenileyebilirsiniz.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        detailsWindow.Close();
                        RefreshUsersList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ban işlemi sırasında hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        banBtn.IsEnabled = true;
                    }
                }
            };
            Grid.SetColumn(banBtn, 0);
            buttonGrid.Children.Add(banBtn);

            // 2. Alert message Button
            var msgBtn = PageHelpers.MkBtn("✉️ MESAJ GÖNDER", "#00A3FF", 180);
            msgBtn.Height = 36;
            msgBtn.Margin = new Thickness(8, 0, 8, 0);
            msgBtn.Click += (s, e) =>
            {
                // Custom Input Box Simulation
                var msgWindow = new Window
                {
                    Title = "Kullanıcıya Mesaj Gönder",
                    Width = 400,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = PageHelpers.HexBrush("#111"),
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI"),
                    ResizeMode = ResizeMode.NoResize
                };

                var mSp = new StackPanel { Margin = new Thickness(16) };
                mSp.Children.Add(PageHelpers.Lbl($"👤 {username} kullanıcısına gönderilecek mesajı yazın:", 11, "#CCC"));
                
                var tbMsg = PageHelpers.DarkTextBox("Mistik Launcher Yönetimi: ");
                tbMsg.Height = 80;
                tbMsg.AcceptsReturn = true;
                tbMsg.TextWrapping = TextWrapping.Wrap;
                mSp.Children.Add(tbMsg);

                var sendBtn = PageHelpers.MkBtn("GÖNDER", "#00A3FF", 100);
                sendBtn.Margin = new Thickness(0, 10, 0, 0);
                sendBtn.HorizontalAlignment = HorizontalAlignment.Right;
                sendBtn.Click += async (s2, e2) =>
                {
                    var msgText = tbMsg.Text.Trim();
                    if (string.IsNullOrEmpty(msgText)) return;
                    
                    sendBtn.IsEnabled = false;
                    try
                    {
                        await MistikAnalytics.SendAlertMessageAsync(username, msgText);
                        MessageBox.Show("Mesaj Firebase veritabanına iletildi! Oyuncu çevrimiçi olduğunda veya Launcher'ı ilk açtığında ekranda belirecektir.", "Mesaj Gönderildi", MessageBoxButton.OK, MessageBoxImage.Information);
                        msgWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        sendBtn.IsEnabled = true;
                    }
                };
                mSp.Children.Add(sendBtn);
                msgWindow.Content = mSp;
                msgWindow.ShowDialog();
            };
            Grid.SetColumn(msgBtn, 1);
            buttonGrid.Children.Add(msgBtn);

            // 3. Clear logs Button
            var cleanBtn = PageHelpers.MkBtn("🧹 LOGLARI TEMİZLE", "#FFB100", 180);
            cleanBtn.Height = 36;
            cleanBtn.Click += async (s, e) =>
            {
                var confirm = MessageBox.Show($"'{username}' adlı oyuncunun tüm çökme/hata loglarını Firebase'den silmek istiyor musunuz?", "Logları Temizle", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    cleanBtn.IsEnabled = false;
                    try
                    {
                        await MistikAnalytics.DeleteUserLogsAsync(username);
                        MessageBox.Show("Kullanıcının hata geçmişi başarıyla sıfırlandı!", "Loglar Temizlendi", MessageBoxButton.OK, MessageBoxImage.Information);
                        detailsWindow.Close();
                        RefreshUsersList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Temizleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        cleanBtn.IsEnabled = true;
                    }
                }
            };
            Grid.SetColumn(cleanBtn, 2);
            buttonGrid.Children.Add(cleanBtn);

            // 4. Zorla Güncelle Button
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var forceUpdateBtn = PageHelpers.MkBtn("🔄 ZORLA GÜNCELLE", "#A349A4", 180);
            forceUpdateBtn.Height = 36;
            forceUpdateBtn.Margin = new Thickness(8, 0, 0, 0);
            forceUpdateBtn.Click += async (s, e) =>
            {
                var confirm = MessageBox.Show(
                    $"'{username}' kullanıcısına zorla güncelleme komutu göndermek istiyor musunuz?\n\nOyuncu Launcher'ı bir sonraki açışında otomatik olarak en son sürümü indirecektir.",
                    "Zorla Güncelle", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                forceUpdateBtn.IsEnabled = false;
                forceUpdateBtn.Content = "Gönderiliyor...";
                try
                {
                    await MistikAnalytics.SendAlertMessageAsync(username, "[FORCE_UPDATE] Yönetici tarafından zorunlu güncelleme tetiklendi. Launcher'ınız en son sürüme güncellenecektir.");
                    MessageBox.Show("Zorla güncelleme komutu başarıyla gönderildi!\n\nOyuncu Launcher'ı açtığında güncelleme başlayacaktır.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    forceUpdateBtn.Content = "🔄 ZORLA GÜNCELLE";
                    forceUpdateBtn.IsEnabled = true;
                }
            };
            Grid.SetColumn(forceUpdateBtn, 3);
            buttonGrid.Children.Add(forceUpdateBtn);

            // 5. Uzaktan Mod Kur Button
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var remoteModBtn = PageHelpers.MkBtn("📦 MOD KUR", "#FF6B00", 180);
            remoteModBtn.Height = 36;
            remoteModBtn.Margin = new Thickness(8, 0, 0, 0);
            remoteModBtn.Click += (s, e) =>
            {
                // Custom Mod Prompt Window - Expanded size to support local file uploading
                var modWindow = new Window
                {
                    Title = "Kullanıcıya Uzaktan Mod Kur",
                    Width = 460,
                    Height = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = PageHelpers.HexBrush("#111"),
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI"),
                    ResizeMode = ResizeMode.NoResize
                };

                var modSp = new StackPanel { Margin = new Thickness(16) };
                modSp.Children.Add(PageHelpers.Lbl($"👤 {username} kullanıcısına kurulacak modu yapılandırın:", 11, "#CCC"));

                modSp.Children.Add(PageHelpers.Lbl("Mod Dosya Adı (Örn: InvMove.jar)", 10, "#888", pad: new Thickness(0, 6, 0, 2)));
                var tbModName = PageHelpers.DarkTextBox("InvMove-0.9.0+1.21.1-Fabric.jar");
                modSp.Children.Add(tbModName);

                modSp.Children.Add(PageHelpers.Lbl("Mod İndirme Linki (Doğrudan .jar İndirme Bağlantısı)", 10, "#888", pad: new Thickness(0, 8, 0, 2)));
                var tbModUrl = PageHelpers.DarkTextBox("https://cdn.modrinth.com/data/REfW2AEX/versions/4q5KJDfw/InvMove-0.9.0%2B1.21.1-Fabric.jar");
                modSp.Children.Add(tbModUrl);

                // --- NEW: Local File Picker & Cloud Uploader ---
                modSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 12, 0, 12) });
                modSp.Children.Add(PageHelpers.Lbl("📁 VEYA bilgisayarınızdan yerel bir mod (.jar) dosyası seçip yükleyin:", 10, "#AAA", bold: true));

                var fileSelectPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                var selectFileBtn = PageHelpers.MkBtn("📂 YEREL MOD SEÇ & BULUTA YÜKLE", "#00A3FF", 320);
                selectFileBtn.Height = 28;
                selectFileBtn.FontSize = 10;
                
                var uploadStatus = PageHelpers.Lbl("", 10, "#2EB82E", pad: new Thickness(8, 6, 0, 0));
                
                fileSelectPanel.Children.Add(selectFileBtn);
                fileSelectPanel.Children.Add(uploadStatus);
                modSp.Children.Add(fileSelectPanel);

                var installBtn = PageHelpers.MkBtn("MODU GÖNDER & KUR", "#FF6B00", 160);
                installBtn.Margin = new Thickness(0, 16, 0, 0);
                installBtn.HorizontalAlignment = HorizontalAlignment.Right;

                selectFileBtn.Click += async (s3, e3) =>
                {
                    var ofd = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Minecraft Mod Dosyası (*.jar)|*.jar",
                        Title = "Gönderilecek Mod Dosyasını Seçin"
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        string filePath = ofd.FileName;
                        string fileName = System.IO.Path.GetFileName(filePath);
                        
                        tbModName.Text = fileName;
                        tbModUrl.Text = "Yükleniyor... Lütfen bekleyin.";
                        uploadStatus.Text = "⏳ Yükleniyor...";
                        uploadStatus.Foreground = PageHelpers.HexBrush("#FFB100");
                        selectFileBtn.IsEnabled = false;
                        installBtn.IsEnabled = false;

                        try
                        {
                            string uploadedUrl = await MistikAnalytics.UploadFileToCatboxAsync(filePath);
                            tbModUrl.Text = uploadedUrl;
                            uploadStatus.Text = "✅ Yüklendi!";
                            uploadStatus.Foreground = PageHelpers.HexBrush("#2EB82E");
                            MessageBox.Show("Mod dosyası başarıyla bulut sunucusuna yüklendi ve indirme bağlantısı otomatik oluşturuldu!", "Yükleme Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            tbModUrl.Text = "";
                            uploadStatus.Text = "❌ Hata!";
                            uploadStatus.Foreground = PageHelpers.HexBrush("#FF4B4B");
                            MessageBox.Show($"Dosya yüklenirken bir bulut hatası oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            selectFileBtn.IsEnabled = true;
                            installBtn.IsEnabled = true;
                        }
                    }
                };
                // --- END: Local File Picker & Cloud Uploader ---

                installBtn.Click += async (s2, e2) =>
                {
                    var modNameText = tbModName.Text.Trim();
                    var modUrlText = tbModUrl.Text.Trim();
                    if (string.IsNullOrEmpty(modNameText) || string.IsNullOrEmpty(modUrlText))
                    {
                        MessageBox.Show("Mod adı ve indirme linki boş bırakılamaz!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    installBtn.IsEnabled = false;
                    try
                    {
                        await MistikAnalytics.SendRemoteModAsync(username, modNameText, modUrlText);
                        MessageBox.Show("Mod kurulum komutu Firebase'e başarıyla gönderildi!\n\nOyuncu Launcher'ı açtığında mod arka planda otomatik kurulacaktır.", "Kurulum Komutu Gönderildi", MessageBoxButton.OK, MessageBoxImage.Information);
                        modWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        installBtn.IsEnabled = true;
                    }
                };

                modSp.Children.Add(installBtn);
                modWindow.Content = modSp;
                modWindow.ShowDialog();
            };
            Grid.SetColumn(remoteModBtn, 4);
            buttonGrid.Children.Add(remoteModBtn);

            mainSp.Children.Add(buttonGrid);

            detailsWindow.Content = new ScrollViewer { Content = mainSp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            detailsWindow.ShowDialog();
        }

        private UIElement BuildUpdateTab()
        {
            var roleSp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            roleSp.Children.Add(PageHelpers.Lbl("☁️ BULUTTAN GÜNCELLEME DAĞITIMI", 14, "#00A3FF", true));
            roleSp.Children.Add(PageHelpers.Lbl("Aktif olan tüm oyuncuların Launcher'larına anında güncelleme uyarısı gönderin ve dosyayı otomatik indirtin.", 11, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 12)));

            roleSp.Children.Add(PageHelpers.Lbl("Yeni Sürüm Kodu (Örn: v5.3.0)", 11, "#A0A0A0"));
            var tbUpdateVer = PageHelpers.DarkTextBox("v5.3.0");
            roleSp.Children.Add(tbUpdateVer);

            roleSp.Children.Add(PageHelpers.Lbl("Güncelleme İndirme URL'si (Doğrudan .exe Bağlantısı)", 11, "#A0A0A0", pad: new Thickness(0, 8, 0, 0)));
            var tbUpdateUrl = PageHelpers.DarkTextBox("https://github.com/gamer3434/MistikLauncherUltra/releases/download/v5.3.0/MistikLauncher.exe");
            roleSp.Children.Add(tbUpdateUrl);

            roleSp.Children.Add(PageHelpers.Lbl("Yenilikler / Güncelleme Notları", 11, "#A0A0A0", pad: new Thickness(0, 8, 0, 0)));
            var tbChangelog = PageHelpers.DarkTextBox("• Hata düzeltmeleri yapıldı.\n• Harita indirme sistemi optimize edildi.\n• FPS performansı artırıldı.", 80);
            tbChangelog.AcceptsReturn = true;
            tbChangelog.TextWrapping = TextWrapping.Wrap;
            tbChangelog.VerticalContentAlignment = VerticalAlignment.Top;
            roleSp.Children.Add(tbChangelog);

            var publishBtn = PageHelpers.MkBtn("GÜNCELLEMEYİ BULUTA YAYINLA", "#00A3FF");
            publishBtn.Margin = new Thickness(0, 14, 0, 0);
            publishBtn.HorizontalAlignment = HorizontalAlignment.Left;
            
            publishBtn.Click += async (_, _) => {
                string ver = tbUpdateVer.Text.Trim();
                string url = tbUpdateUrl.Text.Trim();
                string changelog = tbChangelog.Text.Trim();

                if (string.IsNullOrEmpty(ver) || string.IsNullOrEmpty(url) || string.IsNullOrEmpty(changelog))
                {
                    MessageBox.Show("Lütfen tüm alanları doldurun!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_main.Relay == null || !_main.Relay.Connected)
                {
                    MessageBox.Show("Bulut sunucusu (MQTT) bağlantısı aktif değil!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                publishBtn.IsEnabled = false;
                publishBtn.Content = "YAYINLANIYOR...";
                try
                {
                    await _main.Relay.PublishUpdateAsync(ver, url, changelog);
                    MessageBox.Show($"'{ver}' sürüm güncellemesi tüm istemcilere başarıyla dağıtıldı!", "Güncelleme Yayınlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Güncelleme gönderilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    publishBtn.Content = "GÜNCELLEMEYİ BULUTA YAYINLA";
                    publishBtn.IsEnabled = true;
                }

                try
                {
                    string historyPath = System.IO.Path.Combine(App.AppData, "update_history.json");
                    var historyList = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JObject>();
                    if (System.IO.File.Exists(historyPath))
                    {
                        try
                        {
                            var arr = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(historyPath));
                            foreach (var item in arr)
                                if (item is Newtonsoft.Json.Linq.JObject jo) historyList.Add(jo);
                        }
                        catch { }
                    }
                    historyList.Insert(0, new Newtonsoft.Json.Linq.JObject
                    {
                        ["version"]   = tbUpdateVer.Text.Trim(),
                        ["url"]       = tbUpdateUrl.Text.Trim(),
                        ["changelog"] = tbChangelog.Text.Trim(),
                        ["date"]      = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    });
                    if (historyList.Count > 20) historyList = historyList.GetRange(0, 20);
                    System.IO.File.WriteAllText(historyPath, new Newtonsoft.Json.Linq.JArray(historyList).ToString());
                }
                catch { }
            };

            return roleSp;
        }

        private UIElement BuildRollbackTab()
        {
            var roleSp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            roleSp.Children.Add(PageHelpers.Lbl("⏪ HATALI GÜNCELLEMEYİ GERİ AL (ROLLBACK)", 14, "#FF4B4B", true));
            roleSp.Children.Add(PageHelpers.Lbl(
                "Yanlışlıkla yayınladığınız bir güncellemeyi geri almak için aşağıdaki geçmişten seçin ve " +
                "\"GERİ AL\" butonuna basın. Seçilen eski sürüm tüm istemcilere anında yeniden dağıtılacaktır.",
                11, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 12)));

            var rollbackPanel = new StackPanel();
            BuildRollbackList(rollbackPanel, _main);

            var rollbackScroll = new ScrollViewer
            {
                Content = rollbackPanel,
                MaxHeight = 280,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var refreshBtn = PageHelpers.MkBtn("🔄 LİSTEYİ YENİLE", "#283040", 160);
            refreshBtn.Height = 30;
            refreshBtn.HorizontalAlignment = HorizontalAlignment.Left;
            refreshBtn.Margin = new Thickness(0, 0, 0, 10);
            refreshBtn.Click += (_, _) => { BuildRollbackList(rollbackPanel, _main); };

            roleSp.Children.Add(refreshBtn);
            roleSp.Children.Add(rollbackScroll);

            return roleSp;
        }

        private static void BuildRollbackList(StackPanel panel, MainWindow main)
        {
            panel.Children.Clear();
            string historyPath = System.IO.Path.Combine(App.AppData, "update_history.json");
            if (!System.IO.File.Exists(historyPath))
            {
                panel.Children.Add(PageHelpers.Lbl("Henüz yayınlanmış güncelleme geçmişi bulunamadı.", 11, "#555555"));
                return;
            }

            Newtonsoft.Json.Linq.JArray history;
            try
            {
                history = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(historyPath));
            }
            catch
            {
                panel.Children.Add(PageHelpers.Lbl("Geçmiş okunamadı.", 11, "#FF4B4B"));
                return;
            }

            foreach (var token in history)
            {
                if (token is not Newtonsoft.Json.Linq.JObject entry) continue;

                var ver      = entry["version"]?.ToString()   ?? "?";
                var url      = entry["url"]?.ToString()        ?? "";
                var chlog    = entry["changelog"]?.ToString()  ?? "";
                var date     = entry["date"]?.ToString()       ?? "";

                var card = PageHelpers.Card("#1a1020", 10, "#4a1a4a", new Thickness(0, 0, 0, 8));
                var cardSp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

                var headerRow = new Grid();
                headerRow.ColumnDefinitions.Add(new ColumnDefinition());
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var verLbl = PageHelpers.Lbl($"🏷️  {ver}", 13, "#E080FF", true);
                Grid.SetColumn(verLbl, 0);
                headerRow.Children.Add(verLbl);

                var dateLbl = PageHelpers.Lbl(date, 10, "#666666");
                Grid.SetColumn(dateLbl, 1);
                headerRow.Children.Add(dateLbl);

                cardSp.Children.Add(headerRow);

                var urlPreview = url.Length > 60 ? url[..57] + "..." : url;
                cardSp.Children.Add(PageHelpers.Lbl($"🔗 {urlPreview}", 10, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 4)));

                var firstLine = chlog.Split('\n')[0].Trim();
                if (firstLine.Length > 80) firstLine = firstLine[..77] + "...";
                cardSp.Children.Add(PageHelpers.Lbl(firstLine, 10, "#888888"));

                var capturedVer = ver; var capturedUrl = url; var capturedChlog = chlog;
                var rollbackBtn = PageHelpers.MkBtn($"⏪ {ver} SÜRÜMÜNE GERİ AL", "#FF4B4B", 220);
                rollbackBtn.Height = 30;
                rollbackBtn.HorizontalAlignment = HorizontalAlignment.Left;
                rollbackBtn.Margin = new Thickness(0, 10, 0, 0);
                rollbackBtn.Click += async (_, _) =>
                {
                    var confirm = MessageBox.Show(
                        $"'{capturedVer}' sürümü tüm istemcilere yeniden dağıtılacak. Emin misiniz?",
                        "Geri Al Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;

                    if (main.Relay == null || !main.Relay.Connected)
                    {
                        MessageBox.Show("Bulut sunucusu bağlantısı aktif değil!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    rollbackBtn.IsEnabled = false;
                    rollbackBtn.Content = "GERİ ALINIYOR...";
                    try
                    {
                        await main.Relay.PublishUpdateAsync(capturedVer, capturedUrl, capturedChlog);
                        rollbackBtn.Content = $"✅ {capturedVer} Geri Alındı!";
                        rollbackBtn.Background = PageHelpers.HexBrush("#2EB82E");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Geri alma başarısız:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        rollbackBtn.IsEnabled = true;
                        rollbackBtn.Content = $"⏪ {capturedVer} SÜRÜMÜNE GERİ AL";
                    }
                };
                cardSp.Children.Add(rollbackBtn);

                card.Child = cardSp;
                panel.Children.Add(card);
            }
        }
    }
}
