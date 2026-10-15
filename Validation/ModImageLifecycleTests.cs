using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Reflection;
using MistikLauncher.Pages;

static class ModImageLifecycleTests
{
    public static int Run()
    {
        var requests = new List<(CancellationToken Token, TaskCompletionSource<BitmapSource?> Result)>();
        Func<Uri, int, CancellationToken, Task<BitmapSource?>> load = (_, _, token) => {
            var result = new TaskCompletionSource<BitmapSource?>(); requests.Add((token, result)); return result.Task;
        };
        var icon = (Border)typeof(ModProjectView).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "Icon" && method.GetParameters().Length == 3)
            .Invoke(null, new object[] { "https://cdn.modrinth.com/test.png", 68, load })!;
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4); bitmap.Freeze();
        icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        if (requests.Count != 1 || !requests[0].Token.IsCancellationRequested)
            throw new Exception("Leaving a mod card must cancel its queued or downloading icon request.");
        icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        requests[0].Result.SetResult(bitmap); Flush();
        if (icon.Child is Image || requests.Count != 2)
            throw new Exception("A late previous icon request must not replace a reloaded card.");
        requests[1].Result.SetResult(bitmap); Flush();
        if (icon.Child is not Image { Source: not null }) throw new Exception("The current icon request must render after a reload.");
        icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        if (requests.Count != 2) throw new Exception("Reattaching a rendered icon must reuse its image instead of starting another request.");
        Console.WriteLine("PASS mod icon unload cancels work, late results are ignored, reload resumes and rendered images are reused");
        var gallery = (Border)typeof(ModProjectView).GetMethod("GalleryImage", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object?[] { new Uri("https://cdn.modrinth.com/original.png"), new Uri("https://cdn.modrinth.com/preview.png"), CancellationToken.None, load })!;
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        if (requests.Count != 3 || !requests[2].Token.IsCancellationRequested) throw new Exception("Leaving the gallery must cancel its outstanding image request.");
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        requests[2].Result.SetResult(bitmap); Flush();
        if (gallery.Child is Image || requests.Count != 4) throw new Exception("An obsolete gallery request must not overwrite a newly opened gallery.");
        requests[3].Result.SetResult(null); Flush();
        if (requests.Count != 5) throw new Exception("A failed original must try the safe preview source.");
        requests[4].Result.SetResult(null); Flush();
        var retry = ((StackPanel)gallery.Child).Children.OfType<Button>().Single();
        retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (requests.Count != 6) throw new Exception("Gallery retry must start one request and reject duplicate retries while pending.");
        requests[5].Result.SetResult(bitmap); Flush();
        if (gallery.Child is not Image { Source: not null }) throw new Exception("Retry must replace the failure panel with the recovered gallery image.");
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        if (requests.Count != 6) throw new Exception("A decoded gallery image must be reused across tab switches.");
        Console.WriteLine("PASS gallery cancellation ignores late responses, preview fallback and explicit retry recover images without duplicate requests");
        return 10;
    }
    static void Flush()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
