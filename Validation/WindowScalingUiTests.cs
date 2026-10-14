using System.Windows;
using System.Windows.Controls;
using MistikLauncher;

static class WindowScalingUiTests
{
    public static int Run(MainWindow window)
    {
        var root = (FrameworkElement)window.Content;
        var username = (TextBlock)window.FindName("UserNameLbl");
        var previousName = username.Text;
        username.Text = new string('W', 180);
        try
        {
            foreach (var size in new[] { new Size(960, 640), new Size(800, 500), new Size(768, 540), new Size(1200, 820) })
            {
                root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                var header = (Grid)window.FindName("ShellHeader");
                var language = (ComboBox)window.FindName("LanguageBox");
                var frame = (Frame)window.FindName("MainFrame");
                var launch = (Button)window.FindName("BtnLaunch");
                var version = (ComboBox)window.FindName("VerBox");
                var nav = (WrapPanel)window.FindName("QuickBarPanel");
                var caption = (Border)window.FindName("CaptionHost");
                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                var launchRow = (RowDefinition)window.FindName("ControlBarRow");
                double pixelTolerance = 1 / System.Windows.Media.VisualTreeHelper.GetDpi(caption).DpiScaleY;
                if (caption.ActualHeight > 38 + pixelTolerance || chrome.CaptionHeight != caption.Height || launchRow.ActualHeight > 54 + pixelTolerance)
                    throw new Exception($"Compact bars must preserve content space and match the native drag region: caption={caption.ActualHeight}/{caption.Height}, chrome={chrome.CaptionHeight}, footer={launchRow.ActualHeight}.");
                if (frame.ActualHeight < 250 || frame.TranslatePoint(new Point(0, frame.ActualHeight), root).Y > version.TranslatePoint(new Point(), root).Y)
                    throw new Exception("Compact shell must preserve a usable content viewport above the launch bar.");
                if (launch.TranslatePoint(new Point(launch.ActualWidth, launch.ActualHeight), root).X > size.Width ||
                    launch.TranslatePoint(new Point(launch.ActualWidth, launch.ActualHeight), root).Y > size.Height)
                    throw new Exception("Launch controls must remain within the client viewport.");
                var releases = (Button)window.FindName("BtnYoutube");
                if (releases.Visibility == Visibility.Visible && releases.TranslatePoint(new Point(releases.ActualWidth, 0), header).X > language.TranslatePoint(new Point(), header).X)
                    throw new Exception("Long player names must not push profile controls over the header links.");
                if (nav.Children.OfType<Button>().Any(button => button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), nav).X > nav.ActualWidth + 1))
                    throw new Exception("Navigation must wrap within the client viewport.");
            }
            Console.WriteLine("PASS shell content, navigation and launch controls fit compact and high-DPI sized viewports with a long username");
            return 1;
        }
        finally
        {
            username.Text = previousName;
            root.Measure(new Size(1200, 820)); root.Arrange(new Rect(0, 0, 1200, 820)); root.UpdateLayout();
        }
    }
}
