using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncher.Pages
{
    public class ElybyPage : Page
    {
        private readonly WebBrowser _wb;
        private readonly MainWindow _main;

        public ElybyPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;
            _wb = new WebBrowser();

            // Main Grid
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Info Bar
            grid.RowDefinitions.Add(new RowDefinition()); // WebBrowser

            // Header Toolbar
            var toolbar = new Border
            {
                Background = PageHelpers.HexBrush("#141414"),
                BorderBrush = PageHelpers.HexBrush("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 10, 20, 10)
            };

            var tbGrid = new Grid();
            tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tbGrid.ColumnDefinitions.Add(new ColumnDefinition());
            tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title
            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(PageHelpers.Lbl("🌐  Ely.by Cilt Paneli", 16, "#00A3FF", bold: true));
            titleSp.Children.Add(PageHelpers.Lbl(" | Hesabınızı yönetin ve skininizi değiştirin", 11, "#888888"));
            Grid.SetColumn(titleSp, 0);
            tbGrid.Children.Add(titleSp);

            // Controls
            var controlsSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnBack = PageHelpers.MkBtn("⬅️ Geri", "#333333", 70);
            btnBack.Height = 28;
            btnBack.Margin = new Thickness(0, 0, 8, 0);
            btnBack.Click += (_, _) => { if (_wb.CanGoBack) _wb.GoBack(); };

            var btnRefresh = PageHelpers.MkBtn("🔄 Yenile", "#333333", 80);
            btnRefresh.Height = 28;
            btnRefresh.Margin = new Thickness(0, 0, 8, 0);
            btnRefresh.Click += (_, _) => { try { _wb.Refresh(); } catch { } };

            var btnExt = PageHelpers.MkBtn("🌍 Dış Tarayıcıda Aç", "#2EB82E", 140);
            btnExt.Height = 28;
            btnExt.Margin = new Thickness(0, 0, 8, 0);
            btnExt.Click += (_, _) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ely.by") { UseShellExecute = true });
                }
                catch { }
            };

            var btnSkinRoom = PageHelpers.MkBtn("🎨 Karakter Odası", "#00A3FF", 130);
            btnSkinRoom.Height = 28;
            btnSkinRoom.Click += (_, _) => _main.Navigate("Skin");

            controlsSp.Children.Add(btnBack);
            controlsSp.Children.Add(btnRefresh);
            controlsSp.Children.Add(btnExt);
            controlsSp.Children.Add(btnSkinRoom);

            Grid.SetColumn(controlsSp, 2);
            tbGrid.Children.Add(controlsSp);
            toolbar.Child = tbGrid;

            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // Gömülü Tarayıcı Uyarısı / Bilgilendirme Çubuğu
            var warningBar = new Border
            {
                Background = PageHelpers.HexBrush("#1c2936"),
                BorderBrush = PageHelpers.HexBrush("#00A3FF"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 8, 20, 8)
            };
            var warningLbl = PageHelpers.Lbl("💡 Önemli Bilgi: Ely.by web sitesi modern kod yapıları kullandığından, gömülü Internet Explorer motorunda bazı sayfalar tam yüklenmeyebilir veya takılabilir. En pürüzsüz ve hızlı deneyim için lütfen sağ üstteki yeşil \"🌍 Dış Tarayıcıda Aç\" butonuna basarak siteyi Chrome/Edge üzerinde açın!", 10.5, "#CCCCCC", wrap: TextWrapping.Wrap);
            warningBar.Child = warningLbl;
            Grid.SetRow(warningBar, 1);
            grid.Children.Add(warningBar);

            // Web Browser Container (with some margin & rounded border if possible)
            var browserBorder = new Border
            {
                Margin = new Thickness(15),
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Background = Brushes.Black,
                BorderBrush = PageHelpers.HexBrush("#1f1f1f"),
                BorderThickness = new Thickness(1)
            };

            _wb.Navigated += (s, e) => SetSilent(_wb, true); // Suppress script errors on load
            _wb.Source = new Uri("https://ely.by/skins");

            browserBorder.Child = _wb;
            Grid.SetRow(browserBorder, 2);
            grid.Children.Add(browserBorder);

            Content = grid;
        }

        private static void SetSilent(WebBrowser wb, bool silent)
        {
            try
            {
                var fi = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    var axIWebBrowser2 = fi.GetValue(wb);
                    if (axIWebBrowser2 != null)
                    {
                        axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { silent });
                    }
                }
            }
            catch { }
        }
    }
}
