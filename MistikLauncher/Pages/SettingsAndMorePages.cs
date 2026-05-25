using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        ComboBox _cbLang = null!, _cbAccent = null!;
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
            _cbLang = new ComboBox { ItemsSource = new[] { "Turkce", "English" },
                SelectedItem = main.Config.Lang, Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 12) };
            appSp.Children.Add(_cbLang);
            appSp.Children.Add(PageHelpers.Lbl("Tema Rengi", 12, "#A0A0A0"));
            _cbAccent = new ComboBox { ItemsSource = new[] { "Blue", "Red", "Green", "Purple", "Orange" },
                SelectedItem = main.Config.Accent, Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            appSp.Children.Add(_cbAccent);
            appCard.Child = appSp; sp.Children.Add(appCard);

            var saveBtn = PageHelpers.MkBtn("KAYDET", "#00A3FF", 200);
            saveBtn.Margin = new Thickness(0, 16, 0, 0); saveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveBtn.Click += (_, _) => {
                _main.Config.User       = _tbUser.Text.Trim();
                _main.Config.Ram        = int.TryParse(_tbRam.Text.Trim(), out var r) ? r : 4;
                _main.Config.Lang       = (_cbLang.SelectedItem as string) ?? "Turkce";
                _main.Config.Accent     = (_cbAccent.SelectedItem as string) ?? "Blue";
                _main.Config.AutoClose  = _chkAutoClose.IsChecked == true;
                _main.Config.GithubUser = _tbGithubUser.Text.Trim();
                ConfigManager.Save(_main.Config);
                _main.ReloadConfig();
                MessageBox.Show("Ayarlar kaydedildi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            sp.Children.Add(saveBtn);

            // Admin
            var adminCard = PageHelpers.Card("#1a1015", 12, "#A349A4"); adminCard.Margin = new Thickness(0, 12, 0, 0);
            var adminSp = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            adminSp.Children.Add(PageHelpers.Lbl("Yonetici Modu", 13, "#A349A4", true));
            var adminBtn = PageHelpers.MkBtn("Yonetici Panelini Ac", "#A349A4");
            adminBtn.Margin = new Thickness(0, 8, 0, 0); adminBtn.HorizontalAlignment = HorizontalAlignment.Left;
            adminBtn.Click += (_, _) => main.Navigate("Admin");
            adminSp.Children.Add(adminBtn); adminCard.Child = adminSp; sp.Children.Add(adminCard);

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
                main.Config.OptTurbo = vals[0]; main.Config.OptFps = vals[1];
                ConfigManager.Save(main.Config);
                MessageBox.Show("Optimizasyon ayarlari kaydedildi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            sp.Children.Add(saveBtn);

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
        public AdminPanelPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("Yonetici Paneli", 24, "#FFFFFF", true));

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
            var roleSp = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 14, 0, 0) };
            roleSp.Children.Add(PageHelpers.Lbl("Rol", 12, "#A0A0A0"));
            var cbRole = new ComboBox { ItemsSource = new[] { "Kullanici", "Yonetici" },
                SelectedItem = main.Config.Role, Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            roleSp.Children.Add(cbRole);
            var saveRoleBtn = PageHelpers.MkBtn("KAYDET", "#A349A4", 100);
            saveRoleBtn.Margin = new Thickness(0, 10, 0, 0); saveRoleBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveRoleBtn.Click += (_, _) => {
                main.Config.Role = (cbRole.SelectedItem as string) ?? "Kullanici";
                ConfigManager.Save(main.Config); main.ReloadConfig();
                MessageBox.Show("Rol guncellendi.", "Basarili", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            roleSp.Children.Add(saveRoleBtn);

            // Cloud Update Section
            roleSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 18, 0, 14) });
            roleSp.Children.Add(PageHelpers.Lbl("☁️ BULUTTAN GÜNCELLEME DAĞITIMI", 14, "#00A3FF", true));
            roleSp.Children.Add(PageHelpers.Lbl("Aktif olan tüm oyuncuların Launcher'larına anında güncelleme uyarısı gönderin ve dosyayı otomatik indirtin.", 11, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 12)));

            roleSp.Children.Add(PageHelpers.Lbl("Yeni Sürüm Kodu (Örn: v5.1.0)", 11, "#A0A0A0"));
            var tbUpdateVer = PageHelpers.DarkTextBox("v5.1.0");
            roleSp.Children.Add(tbUpdateVer);

            roleSp.Children.Add(PageHelpers.Lbl("Güncelleme İndirme URL'si (Doğrudan .exe Bağlantısı)", 11, "#A0A0A0", pad: new Thickness(0, 8, 0, 0)));
            var tbUpdateUrl = PageHelpers.DarkTextBox("https://example.com/launcher.exe");
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
            // refreshAction atanacak ama rollbackPanel henüz tanımlanmadı — pointer olarak tutuyoruz
            Action? refreshRollbackAction = null;
            publishBtn.Click += async (_, _) => {
                string ver = tbUpdateVer.Text.Trim();
                string url = tbUpdateUrl.Text.Trim();
                string changelog = tbChangelog.Text.Trim();

                if (string.IsNullOrEmpty(ver) || string.IsNullOrEmpty(url) || string.IsNullOrEmpty(changelog))
                {
                    MessageBox.Show("Lütfen tüm alanları doldurun!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (main.Relay == null || !main.Relay.Connected)
                {
                    MessageBox.Show("Bulut sunucusu (MQTT) bağlantısı aktif değil!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                publishBtn.IsEnabled = false;
                publishBtn.Content = "YAYINLANIYOR...";
                try
                {
                    await main.Relay.PublishUpdateAsync(ver, url, changelog);
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

                // Başarılıysa güncelleme geçmişine kaydet
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
                    // Keep last 20
                    if (historyList.Count > 20) historyList = historyList.GetRange(0, 20);
                    System.IO.File.WriteAllText(historyPath, new Newtonsoft.Json.Linq.JArray(historyList).ToString());
                    // Refresh rollback list
                    refreshRollbackAction?.Invoke();
                }
                catch { }
            };
            roleSp.Children.Add(publishBtn);

            // ── HATA GÜNCELLEME GERİ AL ───────────────────────────────────────
            roleSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 22, 0, 14) });
            roleSp.Children.Add(PageHelpers.Lbl("⏪ HATALI GÜNCELLEMEYİ GERİ AL (ROLLBACK)", 14, "#FF4B4B", true));
            roleSp.Children.Add(PageHelpers.Lbl(
                "Yanlışlıkla yayınladığınız bir güncellemeyi geri almak için aşağıdaki geçmişten seçin ve " +
                "\"GERİ AL\" butonuna basın. Seçilen eski sürüm tüm istemcilere anında yeniden dağıtılacaktır.",
                11, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 12)));

            // Rollback status label
            var rollbackStatusLbl = PageHelpers.Lbl("", 11, "#A0A0A0");
            rollbackStatusLbl.Margin = new Thickness(0, 0, 0, 8);

            // Rollback history panel (dynamic)
            var rollbackPanel = new StackPanel();
            refreshRollbackAction = () => BuildRollbackList(rollbackPanel, main);
            BuildRollbackList(rollbackPanel, main);

            var rollbackScroll = new ScrollViewer
            {
                Content = rollbackPanel,
                MaxHeight = 280,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // Refresh button
            var refreshBtn = PageHelpers.MkBtn("🔄 LİSTEYİ YENILE", "#283040", 160);
            refreshBtn.Height = 30;
            refreshBtn.HorizontalAlignment = HorizontalAlignment.Left;
            refreshBtn.Margin = new Thickness(0, 0, 0, 10);
            refreshBtn.Click += (_, _) => { BuildRollbackList(rollbackPanel, main); };

            roleSp.Children.Add(refreshBtn);
            roleSp.Children.Add(rollbackStatusLbl);
            roleSp.Children.Add(rollbackScroll);

            verifyBtn.Click += (_, _) => {
                if (pwdBox.Password == App.AdminPassword)
                    roleSp.Visibility = Visibility.Visible;
                else
                    MessageBox.Show("Yanlis sifre!", "Erisim Reddedildi", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            pwdSp.Children.Add(verifyBtn); pwdSp.Children.Add(roleSp);
            pwdCard.Child = pwdSp; sp.Children.Add(pwdCard);
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        static void BuildRollbackList(StackPanel panel, MainWindow main)
        {
            panel.Children.Clear();
            string historyPath = System.IO.Path.Combine(App.AppData, "update_history.json");
            if (!System.IO.File.Exists(historyPath))
            {
                panel.Children.Add(PageHelpers.Lbl("Henüz yayınlanmış güncelleme geçmişi bulunamadı.\nGüncelleme yayınladıkça burada görünecek.", 11, "#555555", wrap: TextWrapping.Wrap));
                return;
            }

            Newtonsoft.Json.Linq.JArray history;
            try
            {
                history = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(historyPath));
            }
            catch
            {
                panel.Children.Add(PageHelpers.Lbl("Geçmiş okunamadı (bozuk dosya).", 11, "#FF4B4B"));
                return;
            }

            if (history.Count == 0)
            {
                panel.Children.Add(PageHelpers.Lbl("Geçmişte yayınlanmış güncelleme yok.", 11, "#555555"));
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

                // Header row: version + date
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

                // URL preview (trimmed)
                var urlPreview = url.Length > 60 ? url[..57] + "..." : url;
                cardSp.Children.Add(PageHelpers.Lbl($"🔗 {urlPreview}", 10, "#A0A0A0", wrap: TextWrapping.Wrap, pad: new Thickness(0, 4, 0, 4)));

                // Changelog preview (first line)
                var firstLine = chlog.Split('\n')[0].Trim();
                if (firstLine.Length > 80) firstLine = firstLine[..77] + "...";
                cardSp.Children.Add(PageHelpers.Lbl(firstLine, 10, "#888888"));

                // Rollback button
                var capturedVer = ver; var capturedUrl = url; var capturedChlog = chlog;
                var rollbackBtn = PageHelpers.MkBtn($"⏪ {ver} SÜRÜMÜNE GERİ AL", "#FF4B4B", 220);
                rollbackBtn.Height = 30;
                rollbackBtn.HorizontalAlignment = HorizontalAlignment.Left;
                rollbackBtn.Margin = new Thickness(0, 10, 0, 0);
                rollbackBtn.Click += async (_, _) =>
                {
                    var confirm = MessageBox.Show(
                        $"'{capturedVer}' sürümü tüm istemcilere yeniden dağıtılacak.\n\nEmin misiniz?",
                        "Geri Al Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;

                    if (main.Relay == null || !main.Relay.Connected)
                    {
                        MessageBox.Show("Bulut sunucusu (MQTT) bağlantısı aktif değil!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
