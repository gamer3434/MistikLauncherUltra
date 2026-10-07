using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class SettingsLifecycleTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        var updater=window.LauncherUpdates;
        var busy=updater.Busy; var status=updater.StatusKey;
        var progress=updater.Progress; var error=updater.Error;
        void State(bool running,string key)
        {
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Busy))!.SetValue(updater,running);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.StatusKey))!.SetValue(updater,key);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Error))!.SetValue(updater,null);
        }
        T Control<T>(ModernSettingsPage page,string name)=>(T)typeof(ModernSettingsPage).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        try
        {
            foreach(var updatesOnly in new[]{false,true})
            {
                State(true,"luChecking");
                var page=new ModernSettingsPage(window,updatesOnly);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                State(false,"luCurrent");
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    if(!Control<Button>(page,"updateButton").IsEnabled || Control<ProgressBar>(page,"updateProgress").Visibility!=Visibility.Collapsed || !Control<TextBlock>(page,"updateStatus").Text.StartsWith(Localization.T("luCurrent")))
                        throw new Exception("Cached update page did not refresh after a hidden check completed.");
                    Console.WriteLine("PASS cached "+(updatesOnly?"updates":"settings")+" page refreshes completed check on load"); checks++;
                }
                finally { page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
        }
        finally
        {
            State(busy,status);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Progress))!.SetValue(updater,progress);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Error))!.SetValue(updater,error);
        }
        return checks;
    }
}
