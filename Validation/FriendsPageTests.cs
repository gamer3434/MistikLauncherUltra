using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;

static class FriendsPageTests
{
    public static int Run(MainWindow window)
    {
        int checks = 0;
        void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }

        var originalRelay = window.Relay;
        var originalFriends = window.Config.Friends.ToList();
        var originalCodes = window.Config.FriendCodes.ToList();
        FriendsPage? page = null;
        try
        {
            window.Config.Friends.Clear();
            window.Config.FriendCodes.Clear();
            var relay = new MistikRelay("TestPlayer");
            window.Relay = relay;
            page = new FriendsPage(window);
            var fields = typeof(FriendsPage).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var timer = (DispatcherTimer)fields.Single(field => field.FieldType == typeof(DispatcherTimer)).GetValue(page)!;
            var subscription = fields.Single(field => field.FieldType == typeof(MistikRelay));
            var online = fields.Where(field => field.FieldType == typeof(StackPanel))
                .Select(field => (StackPanel?)field.GetValue(page))
                .Single(panel => panel != null && panel.Margin.Top == 6 && panel.Margin.Bottom == 16)!;

            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(timer.IsEnabled && ReferenceEquals(subscription.GetValue(page), relay), "friends page subscribes and starts refresh on load");
            Check(online.Children.Count == 1, "empty friends online view renders once");
            var empty = online.Children[0];

            var ticked = false;
            var frame = new DispatcherFrame();
            EventHandler tick = (_, _) => { ticked = true; frame.Continue = false; };
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
            timer.Tick += tick;
            timer.Stop();
            timer.Interval = TimeSpan.FromMilliseconds(1);
            timer.Start();
            timeout.Start();
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            timer.Tick -= tick;
            timer.Stop();
            timer.Interval = TimeSpan.FromSeconds(4);
            timer.Start();
            Check(ticked && ReferenceEquals(online.Children[0], empty), "unchanged online view survives refresh tick without reconstruction");

            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Check(!timer.IsEnabled && subscription.GetValue(page) == null, "friends page stops refresh and detaches on unload");
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(timer.IsEnabled && ReferenceEquals(subscription.GetValue(page), relay) && ReferenceEquals(online.Children[0], empty), "cached friends page resumes without rebuilding unchanged online view");
            return checks;
        }
        finally
        {
            page?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            window.Relay = originalRelay;
            window.Config.Friends.Clear(); window.Config.Friends.AddRange(originalFriends);
            window.Config.FriendCodes.Clear(); window.Config.FriendCodes.AddRange(originalCodes);
        }
    }
}
