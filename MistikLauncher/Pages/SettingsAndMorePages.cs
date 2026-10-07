using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        static ControlTemplate? roundedTemplate;
        public static Color HexColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        public static SolidColorBrush HexBrush(string hex)
        {
            hex=hex.ToUpperInvariant() switch {
                "#181818" or "#141414" or "#20364B" or "#13253C"=>"#192C46",
                "#121212" or "#0A0A0A" or "#142333"=>"#0D1727",
                "#222222" or "#222" or "#28445E"=>"#203853", "#333333" or "#333" or "#35546E"=>"#365574",
                "#65C6E8"=>"#00A3FF",
                "#666666" or "#A0A0A0" or "#BDCAD8"=>"#ADBED6", "#FFFFFF"=>"#EFF5FF", _=>hex.ToUpperInvariant()
            };
            return ColorThemes.IsThemed(hex)?ColorThemes.Brush(hex):new(HexColor(hex));
        }

        public static TextBlock Lbl(string text, double size = 13, string color = "#FFFFFF",
            bool bold = false, Thickness? pad = null, TextWrapping wrap = TextWrapping.Wrap)
        {
            var tb = new TextBlock {
                Text = text, FontSize = Math.Max(12,size), Foreground = HexBrush(color),
                FontFamily = new FontFamily("Segoe UI"), TextWrapping = wrap,
                VerticalAlignment = VerticalAlignment.Center };
            if (bold) tb.FontWeight = FontWeights.Bold;
            if (pad.HasValue) tb.Padding = pad.Value;
            Localization.RegisterText(tb,text);
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
                Content = text, MinHeight = 40, Background = HexBrush(color), Foreground = color.ToUpperInvariant() is "#00A3FF" or "#226DA0" or "#65C6E8"?ColorThemes.ActionText:Brushes.White,
                BorderThickness = new Thickness(0), FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,
                Padding = new Thickness(14, 7, 14, 7), Cursor = System.Windows.Input.Cursors.Hand };
            if (width > 0) btn.MinWidth = width;
            btn.Template = RoundedTemplate();
            Localization.RegisterText(btn,text);
            return btn;
        }

        static ControlTemplate RoundedTemplate()
        {
            const string xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Name='border' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}' BorderThickness='2' BorderBrush='Transparent'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='border' Property='BorderBrush' Value='White'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='border' Property='Opacity' Value='0.85'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='border' Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='border' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";
            return roundedTemplate ??= (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        public static TextBox DarkTextBox(string placeholder = "", double height = 36)
            => new() {
                Background = HexBrush("#222222"), Foreground = Brushes.White, CaretBrush = Brushes.White,
                BorderBrush = HexBrush("#333333"), BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8), FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13, MinHeight = height, Text = placeholder,
                VerticalContentAlignment = VerticalAlignment.Center };

        public static TextBlock SectionTitle(string text) => Lbl(text, 14, "#BDCAD8", true);
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

            // Release updater card
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
                    await _main.CheckLauncherUpdatesAsync(true);
                    updateStatusLbl.Text = Localization.T(_main.LauncherUpdates.StatusKey) +
                        (_main.LauncherUpdates.Error is null ? "" : "\n" + _main.LauncherUpdates.Error);
                    updateStatusLbl.Foreground = PageHelpers.HexBrush(_main.LauncherUpdates.Error is null ? "#2EB82E" : "#FF4B4B");
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
            sp.Children.Add(PageHelpers.Lbl("optTitle", 24, "#FFFFFF", true));

            var items = new[] {
                ("optTurbo", "optTurboHelp", main.Config.OptTurbo),
                ("optFps", "optFpsHelp", main.Config.OptFps),
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
                System.Windows.Automation.AutomationProperties.SetName(chk, Localization.T(item.Item1));
                chk.Checked   += (_, _) => vals[idx] = true;
                chk.Unchecked += (_, _) => vals[idx] = false;
                Grid.SetColumn(chk, 1); row.Children.Add(infoSp); row.Children.Add(chk);
                card.Child = row; sp.Children.Add(card);
            }

            var saveBtn = PageHelpers.MkBtn("optSave", "#00A3FF", 200);
            saveBtn.Margin = new Thickness(0, 16, 0, 0); saveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveBtn.Click += (_, _) => {
                try
                {
                    main.Config.OptTurbo = vals[0]; main.Config.OptFps = vals[1];
                    ConfigManager.Save(main.Config);
                    MessageBox.Show(Localization.T("optSaved"), Localization.T("optSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Localization.T("optSaveFailed")}\n{ex.Message}", Localization.T("optErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            sp.Children.Add(saveBtn);

            // ── Kernel Optimizasyonları Kartı ──
            sp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 20, 0, 20) });
            sp.Children.Add(PageHelpers.Lbl("optKernelTitle", 18, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("optKernelHelp", 11, "#A0A0A0"));

            var kernCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var kernSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            var chkKernPriority = new CheckBox { Content = Localization.T("optKernelPriority"),
                IsChecked = main.Config.KernelPriority, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 10, 0, 0) };
            kernSp.Children.Add(chkKernPriority);
            Localization.RegisterText(chkKernPriority, "optKernelPriority");

            var chkKernTimer = new CheckBox { Content = Localization.T("optKernelTimer"),
                IsChecked = main.Config.KernelTimer, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernTimer);
            Localization.RegisterText(chkKernTimer, "optKernelTimer");

            var chkKernAffinity = new CheckBox { Content = Localization.T("optKernelAffinity"),
                IsChecked = false, IsEnabled = false, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernAffinity);
            Localization.RegisterText(chkKernAffinity, "optKernelAffinity");

            var chkKernPower = new CheckBox { Content = Localization.T("optKernelPower"),
                IsChecked = main.Config.KernelPower, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernPower);
            Localization.RegisterText(chkKernPower, "optKernelPower");

            var chkKernNagle = new CheckBox { Content = Localization.T("optKernelNagle"),
                IsChecked = main.Config.KernelNagle, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernNagle);
            Localization.RegisterText(chkKernNagle, "optKernelNagle");

            var chkKernGpu = new CheckBox { Content = Localization.T("optKernelGpu"),
                IsChecked = main.Config.KernelGpu, Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 6, 0, 0) };
            kernSp.Children.Add(chkKernGpu);
            Localization.RegisterText(chkKernGpu, "optKernelGpu");

            var kernStatusBtn = PageHelpers.MkBtn("optStatus", "#FF6B00", 260);
            kernStatusBtn.Margin = new Thickness(0, 12, 0, 0);
            kernStatusBtn.HorizontalAlignment = HorizontalAlignment.Left;
            kernStatusBtn.Click += (_, _) => {
                var selected = new[] {
                    (main.Config.KernelPriority, "optKernelPriority"), (main.Config.KernelTimer, "optKernelTimer"),
                    (main.Config.KernelPower, "optKernelPower"),
                    (main.Config.KernelNagle, "optKernelNagle"), (main.Config.KernelGpu, "optKernelGpu")
                }.Where(item => item.Item1).Select(item => "• " + Localization.T(item.Item2));
                var summary = string.Join("\n", selected);
                MessageBox.Show(Localization.T("optStatusHelp") + "\n\n" +
                    (summary.Length > 0 ? summary : Localization.T("optStatusNone")),
                    Localization.T("optStatusTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            };
            kernSp.Children.Add(kernStatusBtn);

            var kernSaveBtn = PageHelpers.MkBtn("optKernelSave", "#FF6B00", 260);
            kernSaveBtn.Margin = new Thickness(0, 12, 0, 0);
            kernSaveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            kernSaveBtn.Click += (_, _) => {
                try {
                    main.Config.KernelPriority = chkKernPriority.IsChecked == true;
                    main.Config.KernelTimer    = chkKernTimer.IsChecked == true;
                    main.Config.KernelAffinity = false;
                    main.Config.KernelPower    = chkKernPower.IsChecked == true;
                    main.Config.KernelNagle    = chkKernNagle.IsChecked == true;
                    main.Config.KernelGpu      = chkKernGpu.IsChecked == true;
                    ConfigManager.Save(main.Config);
                    MessageBox.Show(Localization.T("optKernelSaved"), Localization.T("optSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                } catch (Exception ex) {
                    MessageBox.Show($"{Localization.T("optKernelFailed")}\n{ex.Message}", Localization.T("optErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            kernSp.Children.Add(kernSaveBtn);

            kernCard.Child = kernSp; sp.Children.Add(kernCard);

            // Separator
            sp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 20, 0, 20) });
            sp.Children.Add(PageHelpers.Lbl("optAdvancedTitle", 18, "#FFB100", true));
            sp.Children.Add(PageHelpers.Lbl("optAdvancedHelp", 11, "#A0A0A0"));

            // Mistik Cleaner Card
            var cleanCard = PageHelpers.Card("#181818", 12, margin: new Thickness(0, 12, 0, 0));
            var cleanRow = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            cleanRow.ColumnDefinitions.Add(new ColumnDefinition());
            cleanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var cleanInfo = new StackPanel();
            cleanInfo.Children.Add(PageHelpers.Lbl("optCleanTitle", 14, "#FFFFFF", true));
            cleanInfo.Children.Add(PageHelpers.Lbl("optCleanHelp", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            
            var cleanBtn = PageHelpers.MkBtn("optCleanAction", "#2EB82E", 160);
            cleanBtn.Click += async (_, _) => await RunMistikCleaner(cleanBtn);
            
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
            gfxInfo.Children.Add(PageHelpers.Lbl("optGraphicsTitle", 14, "#FFFFFF", true));
            gfxInfo.Children.Add(PageHelpers.Lbl("optGraphicsHelp", 11, "#A0A0A0", wrap: TextWrapping.Wrap));
            
            var gfxBtn = PageHelpers.MkBtn("optGraphicsAction", "#FFB100", 160);
            gfxBtn.Foreground = Brushes.Black;
            gfxBtn.Click += (_, _) => OptimizeGameGraphics(gfxBtn);
            
            Grid.SetColumn(gfxBtn, 1);
            gfxRow.Children.Add(gfxInfo);
            gfxRow.Children.Add(gfxBtn);
            gfxCard.Child = gfxRow;
            sp.Children.Add(gfxCard);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        static async Task RunMistikCleaner(Button btn)
        {
            btn.IsEnabled = false;
            Localization.RegisterText(btn, "optCleanBusy");
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-7);
                long freedBytes = await Task.Run(() => GameLogCleaner.CleanOldLogs(App.GameDir, cutoff));
                double freedMb = Math.Round((double)freedBytes / (1024 * 1024), 2);
                MessageBox.Show($"{Localization.T("optCleanDone")} {freedMb} MB",
                    Localization.T("optCleanTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Localization.T("optCleanFailed")} {ex.Message}",
                    Localization.T("optErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Localization.RegisterText(btn, "optCleanAction");
                btn.IsEnabled = true;
            }
        }

        static void OptimizeGameGraphics(Button btn)
        {
            btn.IsEnabled = false;
            try
            {
                var changed = GameGraphicsOptions.ApplyFastPreset(App.GameDir);
                MessageBox.Show(Localization.T(changed ? "optGraphicsSaved" : "optGraphicsUnchanged"),
                    Localization.T("optGraphicsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Localization.T("optGraphicsFailed")} {ex.Message}", Localization.T("optErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
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


}
