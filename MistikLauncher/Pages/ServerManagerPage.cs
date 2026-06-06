using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncher.Pages
{
    public class ServerManagerPage : Page
    {
        readonly MainWindow _main;
        private readonly Button _launchBtn;
        private readonly TextBlock _statusLbl;

        public ServerManagerPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;

            var sp = new StackPanel { Margin = new Thickness(40, 50, 40, 50), VerticalAlignment = VerticalAlignment.Center };

            // Title
            sp.Children.Add(PageHelpers.Lbl("🤖  Mistik Sunucu Kurucu (Auto-MCS)", 26, "#FFFFFF", true, new Thickness(0, 0, 0, 8)));
            sp.Children.Add(PageHelpers.Lbl("Auto-MCS ile kendi bilgisayarınızda tek tıkla yüksek performanslı Minecraft sunucuları kurun, yönetin ve arkadaşlarınızla paylaşın.", 13, "#A0A0A0", wrap: TextWrapping.Wrap));

            // Features Card
            var featCard = PageHelpers.Card("#141414", 12, margin: new Thickness(0, 24, 0, 24));
            var featSp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            featSp.Children.Add(PageHelpers.Lbl("⚡ Auto-MCS Özellikleri", 15, "#00A3FF", bold: true, pad: new Thickness(0, 0, 0, 10)));
            featSp.Children.Add(PageHelpers.Lbl("• Tek Tıkla Kurulum: Paper, Fabric, Forge, Vanilla ve NeoForge sunucularını saniyeler içinde kurun.", 12, "#CCCCCC"));
            featSp.Children.Add(PageHelpers.Lbl("• Entegre Eklenti (Plugin) Marketi: Spigot/Paper sunucularınız için binlerce popüler eklentiyi tek tıkla yükleyin.", 12, "#CCCCCC", pad: new Thickness(0, 6, 0, 0)));
            featSp.Children.Add(PageHelpers.Lbl("• Gelişmiş Tünelleme: Port açma derdi olmadan arkadaşlarınızın sunucunuza katılabilmesi için tüneli otomatik başlatır.", 12, "#CCCCCC", pad: new Thickness(0, 6, 0, 0)));
            featSp.Children.Add(PageHelpers.Lbl("• Güvenli Sesli Sohbet (Simple Voice Chat): Sunucuya özel 3D konum tabanlı sesli sohbet desteği kurar.", 12, "#CCCCCC", pad: new Thickness(0, 6, 0, 0)));
            featCard.Child = featSp;
            sp.Children.Add(featCard);

            // Large launch button
            _launchBtn = new Button
            {
                Content = "🤖  AUTO-MCS BAŞLAT",
                Height = 56,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new LinearGradientBrush(
                    Color.FromRgb(0, 163, 255),
                    Color.FromRgb(0, 100, 200), 90),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 320
            };
            _launchBtn.Click += (_, _) => LaunchAutoMcs();
            sp.Children.Add(_launchBtn);

            // Status label
            _statusLbl = PageHelpers.Lbl("Auto-MCS arka planda otomatik olarak başlatılıyor...", 12, "#FFB100");
            _statusLbl.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(_statusLbl);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            // Auto-launch on page load
            _ = Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(() => LaunchAutoMcs()));
        }

        private async void LaunchAutoMcs()
        {
            try
            {
                _launchBtn.IsEnabled = false;
                _launchBtn.Content = "Çalıştırılıyor...";
                _statusLbl.Foreground = PageHelpers.HexBrush("#FFB100");

                string exePath = Path.Combine(App.GameDir, "auto-mcs.exe");
                if (!File.Exists(exePath) || new FileInfo(exePath).Length < 1000000) // 1MB'dan küçükse eksik inmiş olabilir
                {
                    _statusLbl.Text = "Auto-MCS bulunamadı. Buluttan indiriliyor, lütfen bekleyin...";
                    
                    // Klasörü oluştur
                    Directory.CreateDirectory(App.GameDir);
                    
                    // İndirme URL'si (GitHub Releases CDN veya benzeri)
                    string downloadUrl = "https://github.com/gamer3434/MistikLauncherUltra/releases/download/v5.3.0/auto-mcs.exe";
                    
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 MistikLauncher");
                    
                    // Progress bar veya durumu güncellemek için indirme işlemi
                    await Task.Run(async () =>
                    {
                        using var response = await client.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                        
                        var buffer = new byte[8192];
                        long totalRead = 0L;
                        int read;
                        
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            
                            if (totalBytes > 0)
                            {
                                double pct = ((double)totalRead / totalBytes) * 100.0;
                                string pctStr = $"{pct:F1}";
                                Dispatcher.Invoke(() => _statusLbl.Text = $"Buluttan İndiriliyor: %{pctStr} ({Math.Round((double)totalRead / (1024 * 1024), 2)} MB / {Math.Round((double)totalBytes / (1024 * 1024), 2)} MB)");
                            }
                        }
                    });
                }

                _statusLbl.Text = "Auto-MCS başlatılıyor...";
                
                await Task.Run(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                });

                _statusLbl.Text = "✅ Auto-MCS başarıyla başlatıldı!";
                _statusLbl.Foreground = PageHelpers.HexBrush("#2EB82E");

                // Firebase Analytics: Sunucu kurulum aracı başlatma istatistiği
                try { _ = MistikAnalytics.TrackServerStartAsync(_main.Config.User ?? "Oyuncu", "Auto-MCS", 25565); } catch { }
            }
            catch (Exception ex)
            {
                _statusLbl.Text = $"❌ Başlatma/İndirme hatası: {ex.Message}";
                _statusLbl.Foreground = PageHelpers.HexBrush("#FF4B4B");
                MessageBox.Show("Auto-MCS başlatılamadı veya indirilemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                try { _ = MistikAnalytics.TrackCrashAsync(_main.Config.User ?? "Oyuncu", $"Auto-MCS İndirme/Başlatma Hatası: {ex.Message}", ex.StackTrace ?? ""); } catch { }
            }
            finally
            {
                _launchBtn.IsEnabled = true;
                _launchBtn.Content = "🤖  AUTO-MCS BAŞLAT";
            }
        }
    }
}
