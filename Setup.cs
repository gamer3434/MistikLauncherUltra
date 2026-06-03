using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Security.Principal;

namespace MistikLauncherSetup
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            // Enable TLS 1.1, 1.2, and 1.3 for secure downloads from GitHub
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var app = new App();
            var win = new SetupWindow();
            app.Run(win);
        }
    }

    public class SetupWindow : Window
    {
        private TextBlock statusText;
        private TextBlock titleText;
        private Border progressFill;
        private Button installButton;
        private Button closeButton;
        private Button minButton;
        private Grid mainGrid;
        
        private string downloadUrl = "https://github.com/gamer3434/MistikLauncherUltra/releases/download/v5.5.0/MistikLauncherUltra.exe";
        private string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
        private string officialExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra", "MistikLauncher.exe");

        public SetupWindow()
        {
            // Pencere Özellikleri
            this.Title = "Mistik Launcher Kurulumu";
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

            // Arayüz Sınırı (Premium Karanlık Tema)
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
                Text = "⚡ Mistik Launcher",
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
                Text = "Mistik Launcher v5.5.0 Kurulumu",
                Foreground = HexBrush("#FF6B00"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            contentSp.Children.Add(titleText);

            var descText = new TextBlock
            {
                Text = "En son optimizasyonlar ve NVIDIA App uyumluluğu ile kurulmaya hazır.",
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
                Text = "Kuruluma başlamak için aşağıdaki butona tıklayın.",
                Foreground = HexBrush("#A0A0A0"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            contentSp.Children.Add(statusText);

            Grid.SetRow(contentSp, 1);
            mainGrid.Children.Add(contentSp);

            // --- Alt Kısmı (Butonlar) ---
            installButton = new Button
            {
                Content = "KURULUMU BAŞLAT",
                Width = 220,
                Height = 42,
                Background = HexBrush("#FF6B00"),
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
            installButton.Style = btnStyle;

            installButton.Click += InstallButton_Click;
            Grid.SetRow(installButton, 2);
            mainGrid.Children.Add(installButton);

            border.Child = mainGrid;
            this.Content = border;

            // Arka planda en son sürüm bilgisini ve URL'yi sorgula
            FetchLatestVersionUrl();
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

        private async void FetchLatestVersionUrl()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0");
                    string json = await client.DownloadStringTaskAsync("https://raw.githubusercontent.com/gamer3434/MistikLauncherUltra/main/update.json");
                    var matchUrl = Regex.Match(json, "\"url\"\\s*:\\s*\"([^\"]+)\"");
                    var matchVer = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (matchUrl.Success)
                    {
                        downloadUrl = matchUrl.Groups[1].Value;
                    }
                    if (matchVer.Success)
                    {
                        string ver = matchVer.Groups[1].Value;
                        Dispatcher.Invoke(() => {
                            titleText.Text = "Mistik Launcher " + ver + " Kurulumu";
                        });
                    }
                }
            }
            catch { }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            installButton.Visibility = Visibility.Collapsed;
            closeButton.IsEnabled = false;
            
            foreach (var child in ((StackPanel)mainGrid.Children[1]).Children)
            {
                if (child is Border && ((Border)child).Name == "ProgContainer")
                {
                    ((Border)child).Visibility = Visibility.Visible;
                    break;
                }
            }

            statusText.Text = "Mistik Launcher indiriliyor...";
            
            try { Directory.CreateDirectory(appDataFolder); } catch { }
            string tempFile = Path.Combine(appDataFolder, "MistikLauncherUltraSetup.tmp");
            StartDownload(tempFile);
        }

        private async void StartDownload(string tempFile)
        {
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    using (var response = await http.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;
                        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = 0;
                            int read;
                            
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                totalRead += read;
                                
                                if (totalBytes.HasValue)
                                {
                                    double percentage = Math.Round((double)totalRead / totalBytes.Value * 100);
                                    Dispatcher.Invoke(() =>
                                    {
                                        progressFill.Width = (percentage / 100.0) * 400.0;
                                        statusText.Text = "Mistik Launcher indiriliyor (" + percentage + "%)...";
                                    });
                                }
                            }
                        }
                    }
                }
                
                PerformInstallation(tempFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("İndirme sırasında bir hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private async void PerformInstallation(string tempFile)
        {
            try
            {


                // Kaldırıcıyı (MistikUninstaller.exe) indir
                Dispatcher.Invoke(() => statusText.Text = "Kaldırıcı bileşenleri indiriliyor...");
                string uninsTemp = Path.Combine(appDataFolder, "MistikUninstaller_Setup.tmp");
                string uninsUrl = downloadUrl.Replace("MistikLauncherUltra.exe", "MistikUninstaller.exe");
                
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    using (var http = new System.Net.Http.HttpClient())
                    {
                        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                        var response = await http.GetAsync(uninsUrl);
                        using (var fs = new FileStream(uninsTemp, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }
                });

                Dispatcher.Invoke(() => statusText.Text = "Eski sürümler kapatılıyor...");

                // Eski süreçleri öldür
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

                Dispatcher.Invoke(() => statusText.Text = "Dosyalar kopyalanıyor...");

                // Kopyalama döngüsü (Güvenli .bak hot-swap yöntemi ile dosya kilidi hatası tamamen aşılır)
                Directory.CreateDirectory(appDataFolder);
                await System.Threading.Tasks.Task.Run(() =>
                {
                    string officialUninstallerPath = Path.Combine(appDataFolder, "MistikUninstaller.exe");

                    // Launcher Kopyalama
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            if (File.Exists(officialExePath))
                            {
                                string bakPath = officialExePath + ".bak";
                                try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { }
                                try { File.Move(officialExePath, bakPath); } catch { }
                            }
                            File.Copy(tempFile, officialExePath, true);
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    // Uninstaller Kopyalama
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            if (File.Exists(officialUninstallerPath))
                            {
                                string bakPath = officialUninstallerPath + ".bak";
                                try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { }
                                try { File.Move(officialUninstallerPath, bakPath); } catch { }
                            }
                            File.Copy(uninsTemp, officialUninstallerPath, true);
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    // Geçici dosyaları sil
                    try { File.Delete(tempFile); } catch { }
                    try { File.Delete(uninsTemp); } catch { }
                });

                Dispatcher.Invoke(() => statusText.Text = "Kısayollar ve sistem kayıtları oluşturuluyor...");

                // Kısayolları oluştur
                CreateShortcut(Environment.SpecialFolder.Desktop, "Mistik Launcher.lnk", officialExePath, appDataFolder);
                CreateShortcut(Environment.SpecialFolder.Programs, "Mistik Launcher.lnk", officialExePath, appDataFolder);

                // Kayıt defteri girdileri
                RegisterUninstall(officialExePath, appDataFolder);

                Dispatcher.Invoke(() => statusText.Text = "Kurulum tamamlandı! Başlatılıyor...");
                await System.Threading.Tasks.Task.Delay(1000);

                // Temizlik: Single-file extraction hatalarını önlemek için temp .net klasörünü temizle
                try {
                    string dotNetTemp = Path.Combine(Path.GetTempPath(), ".net", "MistikLauncherUltra");
                    if (Directory.Exists(dotNetTemp)) Directory.Delete(dotNetTemp, true);
                } catch { }

                // Uygulamayı başlat
                Process.Start(new ProcessStartInfo(officialExePath)
                {
                    WorkingDirectory = appDataFolder,
                    UseShellExecute = true
                });

                Dispatcher.Invoke(() => this.Close());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kurulum tamamlanamadı:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Dispatcher.Invoke(() => this.Close());
            }
        }

        private void CreateShortcut(Environment.SpecialFolder folder, string linkName, string targetPath, string workDir)
        {
            try
            {
                string folderPath = Environment.GetFolderPath(folder);
                string linkPath = Path.Combine(folderPath, linkName);

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(linkPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workDir;
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Save();
                }
            }
            catch { }
        }

        private void RegisterUninstall(string exePath, string appDir)
        {
            try
            {
                string uninstallerPath = Path.Combine(appDir, "MistikUninstaller.exe");
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "Mistik Launcher");
                        key.SetValue("DisplayIcon", exePath + ",0");
                        key.SetValue("DisplayVersion", "5.5.0");
                        key.SetValue("Publisher", "Mistik");
                        key.SetValue("InstallLocation", appDir);
                        key.SetValue("UninstallString", "\"" + uninstallerPath + "\"");
                        key.SetValue("NoModify", 1);
                        key.SetValue("NoRepair", 1);
                    }
                }
            }
            catch { }
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
