using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncher.Pages
{
    public class DashboardPage : Page
    {
        public DashboardPage(MainWindow main)
        {
            Background = Brushes.Transparent;
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var sp = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };

            // Welcome banner
            var banner = PageHelpers.Card("#181818", 16);
            var bSp = new StackPanel { Margin = new Thickness(30, 25, 30, 25) };
            bSp.Children.Add(PageHelpers.Lbl($"Mistik Ultra'ya Hos Geldin, {main.Config.User}!", 26, "#FFFFFF", true));
            bSp.Children.Add(PageHelpers.Lbl("Surum yonetimi, mod merkezi, arkadaslar ve daha fazlasi.", 13, "#A0A0A0"));
            banner.Child = bSp; sp.Children.Add(banner);

            // Announcement
            var ann = PageHelpers.Card("#122c1b", 12, "#2EB82E"); ann.Margin = new Thickness(0, 14, 0, 0);
            ann.Child = PageHelpers.Lbl($"Mistik Launcher Ultra {App.LocalVersion} - C# WPF - Antivirus sorunu yok!", 13, "#2EB82E", true, new Thickness(20, 14, 20, 14));
            sp.Children.Add(ann);

            // Stats row
            var statsRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            statsRow.ColumnDefinitions.Add(new ColumnDefinition());
            statsRow.ColumnDefinitions.Add(new ColumnDefinition());
            statsRow.ColumnDefinitions.Add(new ColumnDefinition());
            var statData = new[] {
                ("\U0001F680", "Acilis Sayisi", main.Config.OpenCount.ToString()),
                ("\U0001F3AE", "Secili Surum",  main.Config.Version),
                ("\U0001F464", "Kullanici",     main.Config.User),
            };
            for (int i = 0; i < statData.Length; i++)
            {
                var c = PageHelpers.Card("#181818", 12);
                c.Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0);
                var cSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16), HorizontalAlignment = HorizontalAlignment.Center };
                cSp.Children.Add(PageHelpers.Lbl(statData[i].Item1, 28, "#FFF"));
                cSp.Children.Add(PageHelpers.Lbl(statData[i].Item2, 11, "#A0A0A0"));
                cSp.Children.Add(PageHelpers.Lbl(statData[i].Item3, 16, "#00A3FF", true));
                c.Child = cSp; Grid.SetColumn(c, i); statsRow.Children.Add(c);
            }
            sp.Children.Add(statsRow);

            // Quick actions
            var actCard = PageHelpers.Card("#181818", 12); actCard.Margin = new Thickness(0, 16, 0, 0);
            var actSp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            actSp.Children.Add(PageHelpers.Lbl("Hizli Erisim", 14, "#00A3FF", true));
            actSp.Children.Add(new Separator { Background = PageHelpers.HexBrush("#282828"), Margin = new Thickness(0, 8, 0, 12) });
            var actRow = new WrapPanel();
            void AddBtn(string txt, string tag) {
                var b = PageHelpers.MkBtn(txt, "#222222"); b.Margin = new Thickness(0, 0, 8, 8);
                b.Click += (_, _) => main.Navigate(tag); actRow.Children.Add(b);
            }
            AddBtn("Surum Indir", "Vers");
            AddBtn("Mod Kur", "Mods");
            AddBtn("Skin Degistir", "Skin");
            AddBtn("Arkadaslar", "Friends");
            
            var autoMcsBtn = PageHelpers.MkBtn("Sunucu Kur (Auto-MCS)", "#222222");
            autoMcsBtn.Margin = new Thickness(0, 0, 8, 8);
            autoMcsBtn.Click += async (_, _) => {
                string exePath = System.IO.Path.Combine(App.GameDir, "auto-mcs.exe");
                if (!System.IO.File.Exists(exePath)) {
                    MessageBox.Show("Auto-MCS henüz kurulmamış. İndirme ve kurulum sayfasına yönlendiriliyorsunuz...", "Kurulum Gerekli", MessageBoxButton.OK, MessageBoxImage.Information);
                    main.Navigate("Server");
                    return;
                }

                try {
                    autoMcsBtn.IsEnabled = false;
                    autoMcsBtn.Content = "Açılıyor...";
                    await Task.Run(() => {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                            FileName = exePath,
                            UseShellExecute = true
                        });
                    });
                } catch (Exception ex) {
                    MessageBox.Show("Auto-MCS başlatılırken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                } finally {
                    autoMcsBtn.IsEnabled = true;
                    autoMcsBtn.Content = "Sunucu Kur (Auto-MCS)";
                }
            };
            actRow.Children.Add(autoMcsBtn);

            AddBtn("Ayarlar", "Settings");
            AddBtn("Optimizasyon", "Opt");
            actSp.Children.Add(actRow); actCard.Child = actSp; sp.Children.Add(actCard);

            // Relay status
            var relayCard = PageHelpers.Card("#0d1f2d", 12, "#00A3FF"); relayCard.Margin = new Thickness(0, 14, 0, 0);
            var rSp = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };
            rSp.Children.Add(PageHelpers.Lbl("P2P Relay Durumu", 13, "#00A3FF", true));
            var relayText = PageHelpers.Lbl(
                main.Relay?.Connected == true
                    ? $"MQTT Relay aktif - Oda kodun: {main.Relay.RoomCode}"
                    : "Relay baglaniyor...",
                12, main.Relay?.Connected == true ? "#2EB82E" : "#FFB100");
            rSp.Children.Add(relayText);
            
            _ = Task.Run(async () => {
                int waitCount = 0;
                while (main.Relay?.Connected != true && waitCount < 10) {
                    await Task.Delay(1000);
                    waitCount++;
                }
                main.Dispatcher.Invoke(() => {
                    if (main.Relay?.Connected == true) {
                        relayText.Text = $"MQTT Relay aktif - Oda kodun: {main.Relay.RoomCode}";
                        relayText.Foreground = PageHelpers.HexBrush("#2EB82E");
                    } else {
                        relayText.Text = "Relay baglanti hatasi veya zaman asimi.";
                        relayText.Foreground = PageHelpers.HexBrush("#FF4B4B");
                    }
                });
            });

            relayCard.Child = rSp; sp.Children.Add(relayCard);

            scroll.Content = sp; Content = scroll;
        }
    }
}
