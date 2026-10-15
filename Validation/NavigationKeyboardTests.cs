using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MistikLauncher;

static class NavigationKeyboardTests
{
    public static int Run(MainWindow window)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var frame = (Frame)window.FindName("MainFrame");
        var nav = typeof(MainWindow).GetField("_currentNav", flags)!;
        var previousNav = (string)nav.GetValue(window)!;
        var cache = (Dictionary<string, Page>)typeof(MainWindow).GetField("_pageCache", flags)!.GetValue(window)!;
        var previous = new Dictionary<string, Page?>();
        var editor = new TextBox { Text = "abc", Name = "NavigationEditingFixture" };
        var target = new Button { Content = "Keyboard navigation fixture" };
        var content = new StackPanel(); content.Children.Add(editor); content.Children.Add(target);
        foreach (var key in new[] { "Vers", "Mods", "Skin" })
        {
            previous[key] = cache.GetValueOrDefault(key);
            cache[key] = new Page { Content = key == "Mods" ? content : new TextBlock { Text = key } };
        }
        void Flush() { window.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
        void CheckPage(string operation)
        {
            Flush();
            if (!ReferenceEquals(frame.Content, cache["Mods"]) || !Equals(nav.GetValue(window), "Mods"))
                throw new Exception(operation + " must preserve the current page and highlighted menu.");
        }
        try
        {
            using var source = new HwndSource(new HwndSourceParameters("Isolated launcher keyboard fixture") { Width = 1, Height = 1, WindowStyle = 0 });
            window.Navigate("Vers"); Flush(); window.Navigate("Mods"); Flush();
            if (!frame.CanGoBack) throw new Exception("Keyboard regression fixture must have real navigation history.");
            target.Focus();
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Back) { RoutedEvent = Keyboard.KeyDownEvent });
            CheckPage("Backspace outside an editor");
            NavigationCommands.BrowseBack.Execute(null, target);
            CheckPage("Browser Back command");
            frame.GoBack(); CheckPage("Direct journal Back");
            Console.WriteLine("PASS Backspace and browser Back cannot bypass the selected launcher menu");
            editor.Focus(); editor.CaretIndex = editor.Text.Length;
            editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Back) { RoutedEvent = Keyboard.KeyDownEvent });
            CheckPage("Backspace editing");
            if (editor.Text != "ab") throw new Exception("Backspace must still delete text in editors.");
            Console.WriteLine("PASS Backspace still deletes text without navigating away");
            window.Navigate("Skin"); Flush();
            if (!ReferenceEquals(frame.Content, cache["Skin"]) || !Equals(nav.GetValue(window), "Skin"))
                throw new Exception("Explicit launcher navigation must still work after blocking journal traversal.");
            Console.WriteLine("PASS explicit menu navigation still works after blocked history commands");
            return 3;
        }
        finally
        {
            foreach (var pair in previous)
                if (pair.Value == null) cache.Remove(pair.Key); else cache[pair.Key] = pair.Value;
            window.Navigate(previousNav); Flush();
        }
    }
}
