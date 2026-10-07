using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace MistikLauncher.Pages
{
    public class ElybyPage : Page
    {
        private readonly WebView2? _wb;
        private readonly MainWindow _main;
        private double _currentZoom = 1.0;

        public ElybyPage(MainWindow main)
        {
            _main = main;
            Background = Brushes.Transparent;

            // WebView2 kullanılabilirliğini kontrol et
            bool isWebView2Available = false;
            try
            {
                string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                isWebView2Available = !string.IsNullOrEmpty(version);
            }
            catch
            {
                isWebView2Available = false;
            }

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
            btnBack.Click += (_, _) => { if (_wb != null && _wb.CanGoBack) _wb.GoBack(); };

            var btnRefresh = PageHelpers.MkBtn("🔄 Yenile", "#333333", 80);
            btnRefresh.Height = 28;
            btnRefresh.Margin = new Thickness(0, 0, 8, 0);
            btnRefresh.Click += (_, _) => { try { _wb?.Reload(); } catch { } };

            var btnZoomOut = PageHelpers.MkBtn("🔍 Uzaklaştır (-)", "#333333", 110);
            btnZoomOut.Height = 28;
            btnZoomOut.Margin = new Thickness(0, 0, 8, 0);
            btnZoomOut.Click += (_, _) => {
                if (_currentZoom > 0.4) {
                    _currentZoom -= 0.1;
                    SetZoom(_currentZoom);
                }
            };

            var btnZoomIn = PageHelpers.MkBtn("🔍 Yakınlaştır (+)", "#333333", 110);
            btnZoomIn.Height = 28;
            btnZoomIn.Margin = new Thickness(0, 0, 8, 0);
            btnZoomIn.Click += (_, _) => {
                if (_currentZoom < 2.0) {
                    _currentZoom += 0.1;
                    SetZoom(_currentZoom);
                }
            };

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
            controlsSp.Children.Add(btnZoomOut);
            controlsSp.Children.Add(btnZoomIn);
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
            var warningLbl = PageHelpers.Lbl("💡 Bilgi: Ely.by Cilt Paneli artık Chromium tabanlı WebView2 tarayıcı motoru kullanıyor. Tıpkı Chrome ve Opera GX gibi tüm modern web standartlarını destekler!", 10.5, "#CCCCCC", wrap: TextWrapping.Wrap);
            warningBar.Child = warningLbl;
            Grid.SetRow(warningBar, 1);
            grid.Children.Add(warningBar);

            // Web Browser Container
            var browserBorder = new Border
            {
                Margin = new Thickness(15),
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Background = Brushes.Black,
                BorderBrush = PageHelpers.HexBrush("#1f1f1f"),
                BorderThickness = new Thickness(1)
            };

            UIElement browserContent;

            if (isWebView2Available)
            {
                _wb = new WebView2();
                _wb.NavigationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        SetZoom(_currentZoom);
                    }
                };
                _wb.Source = new Uri("https://ely.by");
                browserContent = _wb;
            }
            else
            {
                _wb = null;
                // Bilgilendirme ve indirme ekranı
                var errorSp = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(20)
                };
                errorSp.Children.Add(PageHelpers.Lbl("⚠️ WebView2 Tarayıcı Motoru Eksik", 18, "#FF3B30", bold: true));
                
                var desc = PageHelpers.Lbl(
                    "Ely.by Cilt Paneli'ni görüntülemek için bilgisayarınızda Microsoft Edge WebView2 Runtime kurulu olmalıdır.\n" +
                    "Bu bileşen Windows 10/11'de genellikle yüklüdür fakat sisteminizde bulunamadı. Lütfen aşağıdaki butona tıklayarak Microsoft'un sitesinden indirin ve kurun, ardından Launcher'ı yeniden başlatın.",
                    12, "#CCCCCC", wrap: TextWrapping.Wrap);
                desc.Margin = new Thickness(0, 10, 0, 20);
                desc.TextAlignment = TextAlignment.Center;
                errorSp.Children.Add(desc);

                var btnDownload = PageHelpers.MkBtn("📥 WebView2 Runtime İndir (Microsoft)", "#00A3FF", 280);
                btnDownload.Height = 36;
                btnDownload.Click += (_, _) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/#download-section") { UseShellExecute = true });
                    }
                    catch { }
                };
                errorSp.Children.Add(btnDownload);

                browserContent = new Border
                {
                    Background = PageHelpers.HexBrush("#121212"),
                    Child = errorSp,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
            }

            browserBorder.Child = browserContent;
            Grid.SetRow(browserBorder, 2);
            grid.Children.Add(browserBorder);

            Content = grid;
        }

        private void SetZoom(double zoomFactor)
        {
            try
            {
                if (_wb != null && _wb.CoreWebView2 != null)
                {
                    _wb.ZoomFactor = zoomFactor;
                    _currentZoom = zoomFactor;
                }
            }
            catch { }
        }

    }
}
