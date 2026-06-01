using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Security.Principal;

namespace MistikLauncherUninstaller
{
    public class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {


            // 2. TEMP Klasöründen Çalışma Kontrolü (Dizini silebilmek için kilit aşımı)
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string tempDir = Path.GetTempPath();
            string expectedTempExe = Path.Combine(tempDir, "MistikUninstaller_Temp.exe");

            if (args.Length == 0 || args[0] != "--uninstall-run")
            {
                try
                {
                    // Kendini TEMP dizinine kopyala
                    File.Copy(currentExe, expectedTempExe, true);

                    // TEMP'teki kopyayı çalıştır
                    var psi = new ProcessStartInfo(expectedTempExe, "--uninstall-run")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = tempDir
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Kaldırıcı başlatılamadı:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            var app = new App();
            var win = new UninstallerWindow();
            app.Run(win);
        }

    }

    public class UninstallerWindow : Window
    {
        private TextBlock statusText;
        private TextBlock titleText;
        private Border progressFill;
        private Button uninstallButton;
        private Button closeButton;
        private Button minButton;
        private Grid mainGrid;
        
        private string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");

        public UninstallerWindow()
        {
            // Pencere Özellikleri
            this.Title = "Mistik Launcher Kaldırıcısı";
            this.Width = 500;
            this.Height = 330;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;

            // Sürüklenebilir pencere
            this.MouseLeftButtonDown += (s, e) => {
                try { this.DragMove(); } catch { }
            };

            // Ana Arayüz Sınırı (Premium Karanlık Tema)
            var border = new Border
            {
                Background = HexBrush("#0B0B0B"),
                BorderBrush = HexBrush("#222222"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24)
            };

            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Başlık
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // İçerik
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Alt Butonlar

            // --- Başlık Kısmı ---
            var headerGrid = new Grid();
            var title = new TextBlock
            {
                Text = "🗑️ Mistik Launcher Kaldırıcı",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(title);

            // Pencere Kontrolleri
            var controlsSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            minButton = CreateControlBtn("—", "#A0A0A0");
            minButton.Click += (s, e) => this.WindowState = WindowState.Minimized;
            controlsSp.Children.Add(minButton);

            closeButton = CreateControlBtn("✕", "#FF4B4B");
            closeButton.Click += (s, e) => this.Close();
            controlsSp.Children.Add(closeButton);

            headerGrid.Children.Add(controlsSp);
            Grid.SetRow(headerGrid, 0);
            mainGrid.Children.Add(headerGrid);

            // --- İçerik Kısmı ---
            var contentSp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            titleText = new TextBlock
            {
                Text = "Mistik Launcher Sistemden Kaldırılsın mı?",
                Foreground = HexBrush("#FF6B00"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            contentSp.Children.Add(titleText);

            var descText = new TextBlock
            {
                Text = "Bu işlem tüm oyun ayarlarını, profilleri ve kısayolları kalıcı olarak silecektir.",
                Foreground = HexBrush("#888888"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            contentSp.Children.Add(descText);

            // İlerleme Çubuğu Konteyneri
            var progressContainer = new Border
            {
                Width = 400,
                Height = 10,
                Background = HexBrush("#161616"),
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Name = "ProgContainer"
            };

            progressFill = new Border
            {
                Width = 0,
                Height = 10,
                Background = new LinearGradientBrush(HexColor("#FF8C00"), HexColor("#FF4500"), 0),
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            progressContainer.Child = progressFill;
            contentSp.Children.Add(progressContainer);

            statusText = new TextBlock
            {
                Text = "Kaldırma işlemini başlatmak için aşağıdaki butona tıklayın.",
                Foreground = HexBrush("#A0A0A0"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            contentSp.Children.Add(statusText);

            Grid.SetRow(contentSp, 1);
            mainGrid.Children.Add(contentSp);

            // --- Alt Kısmı (Kaldırma Butonu) ---
            uninstallButton = new Button
            {
                Content = "SİSTEMDEN KALDIR",
                Width = 220,
                Height = 42,
                Background = HexBrush("#FF4B4B"), // Kırmızı tema kaldırıcı için
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Yuvarlak buton stili
            var btnStyle = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cpFactory);
            template.VisualTree = borderFactory;
            btnStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
            uninstallButton.Style = btnStyle;

            uninstallButton.Click += UninstallButton_Click;
            Grid.SetRow(uninstallButton, 2);
            mainGrid.Children.Add(uninstallButton);

            border.Child = mainGrid;
            this.Content = border;
        }

        private Button CreateControlBtn(string content, string hoverColor)
        {
            var btn = new Button
            {
                Content = content,
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = HexBrush("#888888"),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cpFactory);
            template.VisualTree = borderFactory;
            btn.Template = template;
            return btn;
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            uninstallButton.Visibility = Visibility.Collapsed;
            closeButton.IsEnabled = false;
            
            foreach (var child in ((StackPanel)mainGrid.Children[1]).Children)
            {
                if (child is Border && ((Border)child).Name == "ProgContainer")
                {
                    ((Border)child).Visibility = Visibility.Visible;
                    break;
                }
            }

            statusText.Text = "Kaldırma işlemi hazırlanıyor...";
            StartUninstall();
        }

        private async void StartUninstall()
        {
            try
            {
                // 1. Çalışan süreçleri kapat (15%)
                UpdateProgress(15, "Çalışan launcher süreçleri kapatılıyor...");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("MistikLauncher"))
                    {
                        try { proc.Kill(); } catch { }
                    }
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("MistikLauncherUltra"))
                    {
                        try { proc.Kill(); } catch { }
                    }
                    System.Threading.Thread.Sleep(800);
                });

                // 2. Kısayolları temizle (40%)
                UpdateProgress(40, "Masaüstü ve başlat menüsü kısayolları siliniyor...");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string start = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        
                        string lnk1 = Path.Combine(desk, "Mistik Launcher.lnk");
                        string lnk2 = Path.Combine(start, "Mistik Launcher.lnk");

                        if (File.Exists(lnk1)) File.Delete(lnk1);
                        if (File.Exists(lnk2)) File.Delete(lnk2);
                    }
                    catch { }
                });

                // 3. Kayıt Defterini Temizle (70%)
                UpdateProgress(70, "Sistem kayıt defteri girdileri temizleniyor...");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // Uninstall anahtarı
                        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient", false);
                        
                        // App Paths anahtarları
                        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MistikLauncher.exe", false);
                        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MistikLauncherUltra.exe", false);
                        
                        // GPU Tercihleri
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences", true))
                        {
                            if (key != null)
                            {
                                foreach (var valName in key.GetValueNames())
                                {
                                    if (valName.IndexOf(".mistik_ultra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        valName.IndexOf("MistikLauncher", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        try { key.DeleteValue(valName); } catch { }
                                    }
                                }
                            }
                        }

                        // NVIDIA Tweak
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true))
                        {
                            if (key != null)
                            {
                                foreach (var valName in key.GetValueNames())
                                {
                                    if (valName.IndexOf("MistikLauncher", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        try { key.DeleteValue(valName); } catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                });

                // 4. Kurulum Klasörünü Sil (90%)
                UpdateProgress(90, "Mistik Launcher dosyaları kalıcı olarak siliniyor...");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    if (Directory.Exists(appDataFolder))
                    {
                        // Altındaki .bak dosyaları dahil tüm kilitleri zorlayarak sil
                        for (int i = 0; i < 5; i++)
                        {
                            try
                            {
                                Directory.Delete(appDataFolder, true);
                                break;
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(500);
                            }
                        }
                    }
                });

                // 5. Tamamlandı (100%)
                UpdateProgress(100, "Kaldırma işlemi başarıyla tamamlandı!");
                await System.Threading.Tasks.Task.Delay(1000);

                MessageBox.Show("Mistik Launcher ve tüm verileri sisteminizden başarıyla kaldırıldı.", "Kaldırma Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // TEMP'teki kendini silmek için cmd scripti planla
                string tempExe = Process.GetCurrentProcess().MainModule.FileName;
                string batPath = Path.Combine(Path.GetTempPath(), "mistik_uninstaller_cleanup.bat");
                string batContent = "@echo off\n" +
                                    "timeout /t 2 /nobreak > nul\n" +
                                    "del \"" + tempExe + "\"\n" +
                                    "del \"%~f0\"\n";
                File.WriteAllText(batPath, batContent);
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + batPath + "\"") { CreateNoWindow = true, UseShellExecute = false });

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kaldırma sırasında bir hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void UpdateProgress(double pct, string text)
        {
            Dispatcher.Invoke(() =>
            {
                progressFill.Width = (pct / 100.0) * 400.0;
                statusText.Text = text;
            });
        }

        public static Color HexColor(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }

        public static SolidColorBrush HexBrush(string hex)
        {
            return new SolidColorBrush(HexColor(hex));
        }
    }
}
