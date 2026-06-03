using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncherUltra.Pages
{
    public class MapManagerPage : Page
    {
        readonly MainWindow _main;
        static readonly HttpClient Http = CreateHttpClient();

        static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }

        public MapManagerPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;

            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
            sp.Children.Add(PageHelpers.Lbl("🌍 Harita Merkezi (Özel Haritalar)", 24, "#FFFFFF", true));
            sp.Children.Add(PageHelpers.Lbl("Minecraft'ın en popüler macera, parkur ve hayatta kalma haritalarını tek tıkla indirin.", 12, "#A0A0A0"));

            // 3-Column Grid for maps
            var mapsGrid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            mapsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            mapsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Spacing
            mapsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            mapsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Spacing
            mapsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Card 1: SkyBlock
            var map1Card = PageHelpers.Card("#11151c", 12, "#00A3FF", new Thickness(0));
            var map1Sp = new StackPanel { Margin = new Thickness(18) };
            map1Sp.Children.Add(PageHelpers.Lbl("🚀 MODERN GÖKYÜZÜ ADASI", 15, "#00A3FF", true));
            map1Sp.Children.Add(PageHelpers.Lbl("Survival / SkyBlock  |  Boyut: 1.2 MB", 10, "#888"));
            map1Sp.Children.Add(PageHelpers.Lbl("Minecraft'ın en popüler hayatta kalma modu! Gökyüzündeki adalarda kaynakları akıllıca kullanın, portalları açın ve ejderhayı yenerek zafer kazanın!", 11, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 10, 0, 18)));
            var map1Btn = PageHelpers.MkBtn("HARİTAYI YÜKLE", "#00A3FF");
            map1Btn.Click += async (_, _) => await InstallMap(
                "VoidBlock SkyBlock",
                "https://github.com/Loweredgames/Voidblock/releases/download/26.1_JE-0/Voidblock.zip",
                map1Btn
            );
            map1Sp.Children.Add(map1Btn);
            map1Card.Child = map1Sp;
            Grid.SetColumn(map1Card, 0);
            mapsGrid.Children.Add(map1Card);

            // Card 2: Hardcore Skyblock
            var map2Card = PageHelpers.Card("#111812", 12, "#2EB82E", new Thickness(0));
            var map2Sp = new StackPanel { Margin = new Thickness(18) };
            map2Sp.Children.Add(PageHelpers.Lbl("💀 ZORLU GÖKYÜZÜ ADASI", 15, "#2EB82E", true));
            map2Sp.Children.Add(PageHelpers.Lbl("Hardcore SkyBlock  |  Boyut: 1.2 MB", 10, "#888"));
            map2Sp.Children.Add(PageHelpers.Lbl("Efsanevi Voidblock haritasının en zorlu sürümü! Sadece tek bir canınız var. Gökyüzünde hayatta kalmak için kaynaklarınızı son derece titizlikle yönetin!", 11, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 10, 0, 18)));
            var map2Btn = PageHelpers.MkBtn("HARİTAYI YÜKLE", "#2EB82E");
            map2Btn.Click += async (_, _) => await InstallMap(
                "Hardcore SkyBlock",
                "https://github.com/Loweredgames/Voidblock/releases/download/26.1_JE-0/Voidblock.Hardcore.zip",
                map2Btn
            );
            map2Sp.Children.Add(map2Btn);
            map2Card.Child = map2Sp;
            Grid.SetColumn(map2Card, 2);
            mapsGrid.Children.Add(map2Card);

            // Card 3: Parkour Warrior 28
            var map3Card = PageHelpers.Card("#1a1112", 12, "#FF4B4B", new Thickness(0));
            var map3Sp = new StackPanel { Margin = new Thickness(18) };
            map3Sp.Children.Add(PageHelpers.Lbl("🏁 PARKOUR SAVAŞÇILARI 28", 15, "#FF4B4B", true));
            map3Sp.Children.Add(PageHelpers.Lbl("Parkour / Challenge  |  Boyut: 4.8 MB", 10, "#888"));
            map3Sp.Children.Add(PageHelpers.Lbl("Parkour Warrior 28 antrenman ve yarışma haritası! Yeteneklerinizi test edin, engelleri aşın ve en kısa sürede bitişe ulaşarak rekorları kırın!", 11, "#CCCCCC", wrap: TextWrapping.Wrap, pad: new Thickness(0, 10, 0, 18)));
            var map3Btn = PageHelpers.MkBtn("HARİTAYI YÜKLE", "#FF4B4B");
            map3Btn.Click += async (_, _) => await InstallMap(
                "Parkour Warrior 28",
                "https://github.com/LightedTechnology/Parkour-Warrior-28/releases/download/v2.0.0/Parkour.Warrior.28.v2.0.0.v2.0p.AIO.zip",
                map3Btn
            );
            map3Sp.Children.Add(map3Btn);
            map3Card.Child = map3Sp;
            Grid.SetColumn(map3Card, 4);
            mapsGrid.Children.Add(map3Card);

            sp.Children.Add(mapsGrid);

            // Info Card at bottom
            var infoCard = PageHelpers.Card("#181818", 12, "#FFB100", new Thickness(0, 24, 0, 0));
            var infoSp = new StackPanel { Margin = new Thickness(16) };
            infoSp.Children.Add(PageHelpers.Lbl("💡 Önemli Bilgilendirme", 14, "#FFB100", true));
            infoSp.Children.Add(PageHelpers.Lbl("• Haritalar indirildikten sonra arka planda otomatik olarak ayıklanır ve '.minecraft/saves' klasörünüze yerleştirilir.", 11, "#CCC"));
            infoSp.Children.Add(PageHelpers.Lbl("• İndirme bittiğinde Minecraft'ı açıp 'Tek Oyunculu' (Singleplayer) ekranına girmeniz yeterlidir. Haritayı dünya listenizde göreceksiniz.", 11, "#CCC"));
            infoSp.Children.Add(PageHelpers.Lbl("• Harita yükleme hızı internet bağlantınıza bağlı olarak birkaç saniye sürebilir, yükleme esnasında launcher'ı kapatmayın.", 11, "#CCC"));
            infoCard.Child = infoSp;
            sp.Children.Add(infoCard);

            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        async Task InstallMap(string mapName, string zipUrl, Button btn)
        {
            btn.IsEnabled = false;
            var originalText = btn.Content.ToString();
            try
            {
                btn.Content = "İndiriliyor...";
                var savesDir = Path.Combine(App.GameDir, "saves");
                Directory.CreateDirectory(savesDir);

                var zipPath = Path.Combine(App.AppData, $"{mapName}.zip");

                // Download
                var bytes = await Http.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);

                // Extract
                btn.Content = "Kuruluyor...";
                await Task.Run(() =>
                {
                    var tempExtract = Path.Combine(savesDir, "temp_" + Guid.NewGuid().ToString("N"));
                    if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                    Directory.CreateDirectory(tempExtract);

                    // Extract zip to temporary directory
                    ZipFile.ExtractToDirectory(zipPath, tempExtract);

                    // Find level.dat recursively (makes it 100% path-independent!)
                    var levelDats = Directory.GetFiles(tempExtract, "level.dat", SearchOption.AllDirectories);
                    if (levelDats.Length == 0)
                    {
                        throw new FileNotFoundException("Harita veri dosyası (level.dat) zip içeriğinde bulunamadı.");
                    }

                    var worldFolder = Path.GetDirectoryName(levelDats[0])!;
                    var targetDir = Path.Combine(savesDir, mapName);
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, true);
                    }

                    // Move the actual world directory to target saves folder
                    Directory.Move(worldFolder, targetDir);

                    // Cleanup temp extract and zip
                    if (Directory.Exists(tempExtract))
                    {
                        Directory.Delete(tempExtract, true);
                    }
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                });

                btn.Content = "✓ KURULDU!";
                MessageBox.Show($"'{mapName}' haritası başarıyla kuruldu!\n\nOyunu başlattıktan sonra Tek Oyunculu dünyalarınız arasında görünecektir.", "Harita Kuruldu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                btn.Content = "Hata!";
                MessageBox.Show($"Harita kurulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(3000);
                btn.Content = originalText;
                btn.IsEnabled = true;
            }
        }
    }
}
