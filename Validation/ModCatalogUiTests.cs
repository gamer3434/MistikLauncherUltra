using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class ModCatalogUiTests
{
    public static int Run(MainWindow window, Action<string> capture)
    {
        var frame = (Frame)window.FindName("MainFrame"); var previous = frame.Content;
        using var client = new HttpClient(new Fixture(window));
        var page = new ModManagerPage(window, client);
        try
        {
            frame.Content = page; Flush(window); capture("mod-catalog-cards");
            var open = Nodes(page).OfType<Button>().Single(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Example project ·", StringComparison.Ordinal));
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (page.Content is not ModProjectView detail) throw new Exception("Card should open project inside its mod page.");
            detail.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Flush(window); capture("mod-catalog-description");
            var tabs = detail.Children.OfType<TabControl>().Single();
            if (tabs.Items.Count != 3) throw new Exception("Description, versions and gallery must all load. " + string.Join(" | ", Nodes(detail).OfType<TextBlock>().Select(text => text.Text)));
            Console.WriteLine("PASS catalog card opens all three project detail tabs inside the launcher");
            tabs.SelectedIndex = 1; capture("mod-catalog-versions");
            var versions = (DependencyObject)((TabItem)tabs.Items[1]).Content;
            if (Nodes(versions).OfType<Button>().Count() != 1 || Nodes(versions).OfType<TextBlock>().Any(text => text.Text.Contains("Wrong build")))
                throw new Exception("Only compatible versions may offer installation.");
            Console.WriteLine("PASS project version list excludes incompatible builds");
            tabs.SelectedIndex = 2; capture("mod-catalog-gallery");
            var gallery = (DependencyObject)((TabItem)tabs.Items[2]).Content;
            if (!Nodes(gallery).OfType<TextBlock>().Any(text => text.Text.Contains("gallery", StringComparison.OrdinalIgnoreCase) || text.Text.Contains("galeri")))
                throw new Exception("Empty gallery should explain that no pictures were supplied.");
            Nodes(detail).OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (page.Content is ModProjectView) throw new Exception("Back should restore the original search and results.");
            Console.WriteLine("PASS gallery empty state and back navigation preserve original catalog");
            return 3;
        }
        finally { frame.Content = previous; }
    }
    static void Flush(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach (var descendant in Nodes(child)) yield return descendant;
    }
    sealed class Fixture(MainWindow window) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var hit = new JObject { ["project_id"]="fixture", ["title"]="Example project", ["description"]="An illustrated catalog card opens description, versions and gallery.", ["downloads"]=12500, ["author"]="Example author", ["categories"]=new JArray("optimization", "fabric") };
            string game = GameProfiles.MinecraftVersion(App.GameDir, window.Config.Version);
            string loader = GameProfiles.Loader(App.GameDir, window.Config.Version);
            var project = new JObject { ["id"]="fixture", ["project_type"]="mod", ["title"]="Example project", ["description"]=hit["description"], ["body"]="# About this mod\n\n**Rich description** and version selection.", ["downloads"]=12500, ["followers"]=42, ["gallery"]=new JArray(), ["categories"]=new JArray("optimization", "fabric") };
            JToken data = request.RequestUri!.AbsolutePath.EndsWith("/search") ? new JObject { ["hits"]=new JArray(hit), ["total_hits"]=1 }
                : request.RequestUri.AbsolutePath.EndsWith("/version") ? new JArray(
                    new JObject { ["id"]="compatible", ["name"]="Stable compatible build", ["version_number"]="1.0", ["game_versions"]=new JArray(game), ["loaders"]=new JArray(loader) },
                    new JObject { ["id"]="wrong", ["name"]="Wrong build", ["game_versions"]=new JArray("0.0"), ["loaders"]=new JArray(loader) }) : project;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(data.ToString()) });
        }
    }
}
