using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using System.Reflection;
using Localization = MistikLauncher.Localization;

static class WindowScalingUiTests
{
    public static int Run(MainWindow window, Action<string, int, int>? capture = null)
    {
        var root = (FrameworkElement)window.Content;
        var username = (TextBlock)window.FindName("UserNameLbl");
        var previousName = username.Text;
        var previousLanguage = Localization.Language;
        var previousBusy = window.LauncherUpdates.Busy;
        var busyProperty = typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Busy))!;
        var mainFrame = (Frame)window.FindName("MainFrame"); var previousPage = mainFrame.Content;
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
            busyProperty.SetValue(window.LauncherUpdates, false);
            foreach (var language in new[] { "tr", "en" })
            {
                Localization.SetLanguage(language);
                var settings = new ModernSettingsPage(window);
                var layout = (DockPanel)settings.Content;
                var body = layout.Children.OfType<ScrollViewer>().Single();
                var footer = layout.Children.OfType<Border>().Single();
                var message = Nodes(settings).OfType<TextBlock>().Single(text => text.Name == "SettingsResult");
                message.Text = string.Join(" ", Enumerable.Repeat("Detailed failure path C:/Users/Example/launcher-data; retry after unlocking the file.", 100));
                var save = Nodes(settings).OfType<Button>().Single(button => button.Name == "SaveSettingsButton");
                foreach (var width in new[] { 640d, 768d, 1200d })
                {
                    settings.Measure(new Size(width, 480)); settings.Arrange(new Rect(0, 0, width, 480)); settings.UpdateLayout();
                    if (footer.ActualHeight > 100 || body.ActualHeight < 300 ||
                        save.TranslatePoint(new Point(save.ActualWidth, save.ActualHeight), settings).X > width + 1 ||
                        save.TranslatePoint(new Point(save.ActualWidth, save.ActualHeight), settings).Y > 481 ||
                        message.ToolTip?.ToString() != message.Text || System.Windows.Automation.AutomationProperties.GetHelpText(message) != message.Text)
                        throw new Exception("Long settings errors must preserve the compact settings viewport and save action while exposing the complete accessible message.");
                }
                mainFrame.Content = settings;
                capture?.Invoke("settings-long-error-" + language, 960, 640);
                var versions = Nodes(settings).OfType<ComboBox>().Single(box => box.Name == "LauncherDowngradeVersion");
                var downgrade = Nodes(settings).OfType<Button>().Single(button => button.Name == "DowngradeLauncherButton");
                var load = Nodes(settings).OfType<Button>().Single(button => button.Name == "LoadOlderReleasesButton");
                var update = typeof(ModernSettingsPage).GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic)!;
                if (downgrade.IsEnabled || versions.IsEnabled || !load.IsEnabled) throw new Exception("Downgrade must require a selected verified release.");
                versions.Items.Add("v0.0.1"); versions.SelectedIndex = 0;
                if (!downgrade.IsEnabled || !versions.IsEnabled) throw new Exception("Selecting an older release must enable the idle downgrade action.");
                busyProperty.SetValue(window.LauncherUpdates, true); update.Invoke(settings, null);
                if (downgrade.IsEnabled || versions.IsEnabled || load.IsEnabled) throw new Exception("An active updater must disable all downgrade actions.");
                busyProperty.SetValue(window.LauncherUpdates, false);
                var loading = typeof(ModernSettingsPage).GetField("loadingOlderVersions", BindingFlags.Instance | BindingFlags.NonPublic)!;
                loading.SetValue(settings, true); update.Invoke(settings, null);
                if (downgrade.IsEnabled || versions.IsEnabled || load.IsEnabled) throw new Exception("Loading older releases must block stale selection and duplicate requests.");
                loading.SetValue(settings, false); update.Invoke(settings, null);
                if (!downgrade.IsEnabled || !versions.IsEnabled || !load.IsEnabled) throw new Exception("Completed release listing must restore idle selection.");
                settings.RefreshLanguage(); settings.UpdateLayout();
                if (Nodes(settings).OfType<Button>().Single(button => button.Name == "DowngradeLauncherButton").IsEnabled)
                    throw new Exception("Rebuilding settings must not enable downgrade before a new release selection.");
            }
            Console.WriteLine("PASS long settings errors retain the compact viewport, save control and full accessible details in both languages");
            Console.WriteLine("PASS downgrade actions require selection and pause while the updater or release listing is busy");
            return 3;
        }
        finally
        {
            username.Text = previousName;
            busyProperty.SetValue(window.LauncherUpdates, previousBusy);
            Localization.SetLanguage(previousLanguage); mainFrame.Content = previousPage;
            root.Measure(new Size(1200, 820)); root.Arrange(new Rect(0, 0, 1200, 820)); root.UpdateLayout();
        }
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach (var nested in Nodes(child)) yield return nested;
    }
}
