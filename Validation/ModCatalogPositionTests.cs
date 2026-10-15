using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class ModCatalogPositionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int Run(MainWindow window, Action<string> capture)
    {
        var frame = (Frame)window.FindName("MainFrame"); var previous = frame.Content;
        var nav = typeof(MainWindow).GetField("_currentNav", Private)!; var previousNav = nav.GetValue(window);
        var selectNav = typeof(MainWindow).GetMethod("SelectNav", Private)!;
        var root = Path.Combine(App.AppData, "catalog-position-fixture"); Directory.CreateDirectory(root);
        var paths = Enumerable.Range(0, 32).Select(index => Path.Combine(root, $"position-{index:D2}.jar")).ToArray();
        foreach (var path in paths) File.WriteAllText(path, "local position fixture " + Path.GetFileName(path));
        var scanRoot = (string)typeof(InstalledModCatalog).GetMethod("ProfileDirectory", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            new object[] { App.GameDir, App.AppData, App.ModsDir, window.Config.Version, window.Config.LastSyncedVersion })!;
        Directory.CreateDirectory(scanRoot);
        var probe = Path.Combine(scanRoot, "catalog-position-probe-" + Guid.NewGuid().ToString("N") + ".jar"); File.WriteAllText(probe, "position lookup probe");
        using var fixture = new Fixture(window); using var client = new HttpClient(fixture);
        var page = new ModManagerPage(window, client);
        page.Resources[typeof(ComboBox)] = window.FindResource(typeof(ComboBox));
        page.Resources[typeof(ComboBoxItem)] = window.FindResource(typeof(ComboBoxItem));
        var render = typeof(ModManagerPage).GetMethod("RenderInstalledMods", Private)!;
        var apply = typeof(ModManagerPage).GetMethod("ApplyCatalogStatus", Private)!;
        var snapshotField = typeof(ModManagerPage).GetField("_catalogInstalled", Private)!;
        var pendingField = typeof(ModManagerPage).GetField("_installedCancellation", Private)!;
        void Flush()
        {
            window.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
        void Settle()
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            do { Flush(); if (pendingField.GetValue(page) == null) return; Thread.Sleep(1); } while (DateTime.UtcNow < deadline);
            throw new Exception("Position fixture installed lookup did not settle.");
        }
        try
        {
            nav.SetValue(window, "Mods"); selectNav.Invoke(window, new object[] { "Mods" }); frame.Content = page; Flush(); Settle();
            render.Invoke(page, new object?[] { root }); Settle();
            var scroll = (ScrollViewer)page.Content;
            var content = scroll.Content;
            var cards = (StackPanel)typeof(ModManagerPage).GetField("_resultsPanel", Private)!.GetValue(page)!;
            var installed = (StackPanel)typeof(ModManagerPage).GetField("_installedPanel", Private)!.GetValue(page)!;
            if (cards.Children.Count != 20 || installed.Children.Count != 32) throw new Exception("Position fixture must have enough real catalog and installed rows to scroll.");
            var snapshot = InstalledModCatalog.ReadAsync(client, "", default).GetAwaiter().GetResult();
            var states = (Dictionary<string, CatalogInstallState>)typeof(InstalledModCatalog).GetField("projects", Private)!.GetValue(snapshot)!;
            snapshotField.SetValue(page, snapshot); apply.Invoke(page, null); Flush();
            var cardAnchor = (FrameworkElement)cards.Children[8];
            scroll.ScrollToVerticalOffset(cardAnchor.TranslatePoint(new Point(), scroll).Y + scroll.VerticalOffset - 90); Flush();
            var cardY = cardAnchor.TranslatePoint(new Point(), scroll).Y;
            if (cardY < 0 || cardY > scroll.ViewportHeight - 50) throw new Exception("Position fixture catalog anchor must begin inside the viewport.");
            void CheckPosition(FrameworkElement anchor, double expectedY, string operation)
            {
                Flush();
                var actualY = anchor.TranslatePoint(new Point(), scroll).Y;
                if (!ReferenceEquals(frame.Content, page) || !ReferenceEquals(page.Content, scroll) || !ReferenceEquals(scroll.Content, content) || !Equals(nav.GetValue(window), "Mods"))
                    throw new Exception(operation + " changed the mod page, active menu or scroll content.");
                if (Math.Abs(actualY - expectedY) > 2)
                    throw new Exception($"{operation} moved the visible mod section: expected Y={expectedY:0.##}, actual Y={actualY:0.##}, offset={scroll.VerticalOffset:0.##}.");
            }
            states["position-project-0"] = CatalogInstallState.Installed; apply.Invoke(page, null);
            CheckPosition(cardAnchor, cardY, "Installed catalog label");
            var actions = ((Grid)((Border)cards.Children[0]).Child).Children.OfType<StackPanel>().Single();
            var status = actions.Children.OfType<TextBlock>().Single();
            var installButton = actions.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First();
            var previousStatus = status.Text; var previousCaption = installButton.Content;
            snapshotField.SetValue(page, null); apply.Invoke(page, null);
            if (status.Text != previousStatus || !Equals(installButton.Content, previousCaption) || installButton.IsEnabled)
                throw new Exception("Pending refresh must preserve the displayed installed label and caption while blocking actions.");
            CheckPosition(cardAnchor, cardY, "Pending catalog status");
            snapshotField.SetValue(page, InstalledModCatalog.ReadAsync(client, "", default).GetAwaiter().GetResult()); apply.Invoke(page, null);
            CheckPosition(cardAnchor, cardY, "Completed catalog status");
            Console.WriteLine("PASS asynchronous catalog status changes preserve the visible card and active mod menu");

            var row = (FrameworkElement)installed.Children[10];
            scroll.ScrollToVerticalOffset(row.TranslatePoint(new Point(), scroll).Y + scroll.VerticalOffset - 90); Flush();
            var installedY = installed.TranslatePoint(new Point(), scroll).Y;
            capture("mod-position-before-removal");
            fixture.PendingInstalled = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = fixture.PendingInstalled;
            var previousRequests = Volatile.Read(ref fixture.InstalledRequests);
            var backup = ModFiles.Remove(root, paths[10]);
            render.Invoke(page, new object?[] { root });
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Volatile.Read(ref fixture.InstalledRequests) == previousRequests && DateTime.UtcNow < deadline) { Flush(); Thread.Sleep(1); }
            if (Volatile.Read(ref fixture.InstalledRequests) == previousRequests || pendingField.GetValue(page) == null) throw new Exception("Removal fixture must hold a real installed-file lookup pending.");
            CheckPosition(installed, installedY, "Installed row removal while status is pending");
            if (File.Exists(paths[10]) || !File.Exists(backup) || installed.Children.Count != 31) throw new Exception("Position test must perform a real reversible fixture removal and redraw the installed list.");
            response.SetResult(Fixture.Json(new JObject())); fixture.PendingInstalled = null; Settle();
            CheckPosition(installed, installedY, "Installed row removal after final catalog status");
            capture("mod-position-after-removal");
            Console.WriteLine("PASS removing a visible installed row retains the same section through redraw and delayed status lookup without changing menus");
            fixture.PendingInstalled = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            response = fixture.PendingInstalled;
            render.Invoke(page, new object?[] { root });
            var otherPage = new Page { Content = new TextBlock { Text = "User selected another menu" } };
            frame.Content = otherPage; nav.SetValue(window, "Settings"); selectNav.Invoke(window, new object[] { "Settings" });
            response.SetResult(Fixture.Json(new JObject())); fixture.PendingInstalled = null; Settle();
            if (!ReferenceEquals(frame.Content, otherPage) || !Equals(nav.GetValue(window), "Settings"))
                throw new Exception("Completing an old mod refresh must not reopen the mod menu after the user navigates elsewhere.");
            Console.WriteLine("PASS delayed mod refresh respects a newer user-selected menu instead of reopening the old page");
            return 4;
        }
        finally
        {
            fixture.PendingInstalled?.TrySetResult(Fixture.Json(new JObject())); fixture.PendingInstalled = null;
            frame.Content = previous; nav.SetValue(window, previousNav); selectNav.Invoke(window, new[] { previousNav! });
            if (File.Exists(probe)) File.Delete(probe);
        }
    }

    sealed class Fixture(MainWindow window) : HttpMessageHandler
    {
        public TaskCompletionSource<HttpResponseMessage>? PendingInstalled;
        public int InstalledRequests;
        public static HttpResponseMessage Json(JToken value) => new(HttpStatusCode.OK) { Content = new StringContent(value.ToString()) };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/version_files", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref InstalledRequests);
                return PendingInstalled?.Task ?? Task.FromResult(Json(new JObject()));
            }
            var hits = new JArray(Enumerable.Range(0, 20).Select(index => new JObject {
                ["project_id"] = "position-project-" + index, ["title"] = "Position test project " + index,
                ["description"] = "A real catalog card used to verify stable browsing after removing installed mods.",
                ["categories"] = new JArray("fabric", "forge", "neoforge", "quilt"),
                ["versions"] = new JArray(GameProfiles.MinecraftVersion(App.GameDir, window.Config.Version))
            }));
            return Task.FromResult(Json(new JObject { ["hits"] = hits, ["total_hits"] = 20 }));
        }
    }
}
