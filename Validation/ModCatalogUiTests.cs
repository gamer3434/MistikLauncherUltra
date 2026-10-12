using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;
using Localization = MistikLauncher.Localization;

static class ModCatalogUiTests
{
    public static int Run(MainWindow window, Action<string> capture)
    {
        var frame = (Frame)window.FindName("MainFrame"); var previous = frame.Content;
        var fixture = new Fixture(window);
        using var client = new HttpClient(fixture);
        var page = new ModManagerPage(window, client);
        try
        {
            frame.Content = page; Flush(window); capture("mod-catalog-cards");
            if (fixture.SearchRequests.Count == 0 || fixture.SearchRequests[0].Query.Contains("query=") ||
                Uri.UnescapeDataString(fixture.SearchRequests[0].Query).Contains("versions:") ||
                Uri.UnescapeDataString(fixture.SearchRequests[0].Query).Contains("categories:"))
                throw new Exception("Opening the catalog should list mods across game versions and loaders without requiring a search phrase.");
            if (!Nodes(page).OfType<TextBlock>().Any(text => text.Text.Contains("41")))
                throw new Exception("Catalog should show the total number of available mods.");
            var unsupported = Nodes(page).OfType<Button>().FirstOrDefault(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Double $1 Every Minute ·", StringComparison.Ordinal));
            var unavailableInstall = Nodes(page).OfType<Button>().Single(button => Equals(button.Content, Localization.T("modIncompatible")));
            if (unsupported is null || unavailableInstall.IsEnabled) throw new Exception("Incompatible mods should stay visible but cannot be installed into this profile.");
            var catalogCard = Nodes(page).OfType<Button>().Single(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Example project ·", StringComparison.Ordinal));
            catalogCard.ApplyTemplate();
            if (catalogCard.Template.FindName("Surface", catalogCard) is not Border)
                throw new Exception("Mod cards should use the theme-safe hover surface instead of the Windows blue button highlight.");
            var nextPage = Nodes(page).OfType<Button>().Single(button => button.Content?.ToString() == "›");
            if (!nextPage.IsEnabled) throw new Exception("More compatible mods should be reachable through pagination.");
            nextPage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (!fixture.SearchRequests.Any(uri => uri.Query.Contains("offset=20")))
                throw new Exception("Next page should load the next group of matching mods.");
            Console.WriteLine("PASS empty search browses mods across versions and loaders, labels incompatible projects, and paginates");
            var open = Nodes(page).OfType<Button>().Single(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Example project ·", StringComparison.Ordinal));
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (page.Content is not ModProjectView detail) throw new Exception("Card should open project inside its mod page.");
            detail.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Flush(window); capture("mod-catalog-description");
            var tabs = detail.Children.OfType<TabControl>().Single();
            if (tabs.Items.Count != 3) throw new Exception("Description, versions and gallery must all load. " + string.Join(" | ", Nodes(detail).OfType<TextBlock>().Select(text => text.Text)));
            if (tabs.Items.Cast<TabItem>().Any(tab => tab.Width != 120)) throw new Exception("Project detail tabs should have consistent widths.");
            if (tabs.ItemsPanel.LoadContent() is not StackPanel { Orientation: Orientation.Horizontal, Margin.Left: -2 } ) throw new Exception("Project detail tabs should share a flush, fixed horizontal strip.");
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
            var search = Nodes(page).OfType<TextBox>().Single();
            search.Text = "Double $1 Every Minute";
            var searchTask = (Task)typeof(ModManagerPage).GetMethod("SearchMods", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(page, null)!;
            searchTask.GetAwaiter().GetResult(); Flush(window);
            if (!fixture.SearchRequests.Last().Query.Contains("Double%20%241%20Every%20Minute", StringComparison.OrdinalIgnoreCase) ||
                !Nodes(page).OfType<TextBlock>().Any(text => text.Text == "Double $1 Every Minute"))
                throw new Exception("A project with a dollar sign in its name should be searchable across all versions.");
            Console.WriteLine("PASS exact search finds Double $1 Every Minute despite selected profile mismatch");
            Console.WriteLine("PASS gallery empty state and back navigation preserve original catalog");
            return 4;
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
        public List<Uri> SearchRequests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var hit = new JObject { ["project_id"]="fixture", ["title"]="Example project", ["description"]="An illustrated catalog card opens description, versions and gallery.", ["downloads"]=12500, ["author"]="Example author", ["categories"]=new JArray("optimization", "fabric", "forge", "neoforge", "quilt"), ["versions"]=new JArray("1.20.1") };
            string game = GameProfiles.MinecraftVersion(App.GameDir, window.Config.Version);
            string loader = GameProfiles.Loader(App.GameDir, window.Config.Version);
            hit["versions"] = new JArray(game);
            var project = new JObject { ["id"]="fixture", ["project_type"]="mod", ["title"]="Example project", ["description"]=hit["description"], ["body"]="# About this mod\n\n**Rich description** and version selection.", ["downloads"]=12500, ["followers"]=42, ["gallery"]=new JArray(), ["categories"]=new JArray("optimization", "fabric") };
            JToken data = request.RequestUri!.AbsolutePath.EndsWith("/search") ? Search(request.RequestUri, hit)
                : request.RequestUri.AbsolutePath.EndsWith("/version") ? new JArray(
                    new JObject { ["id"]="compatible", ["name"]="Stable compatible build", ["version_number"]="1.0", ["game_versions"]=new JArray(game), ["loaders"]=new JArray(loader) },
                    new JObject { ["id"]="wrong", ["name"]="Wrong build", ["game_versions"]=new JArray("0.0"), ["loaders"]=new JArray(loader) }) : project;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(data.ToString()) });
        }
        JObject Search(Uri uri, JObject hit)
        {
            SearchRequests.Add(uri);
            bool exact = Uri.UnescapeDataString(uri.Query).Contains("Double $1 Every Minute", StringComparison.OrdinalIgnoreCase);
            var sample = new JObject { ["project_id"]="double-every-minute", ["title"]="Double $1 Every Minute", ["description"]="How long can you double a dollar every minute?", ["downloads"]=1400, ["author"]="Example author", ["categories"]=new JArray("fabric"), ["versions"]=new JArray(hit["versions"]![0]!.ToString()+"-future") };
            return new JObject { ["hits"] = exact ? new JArray(sample) : new JArray(hit, sample), ["total_hits"] = exact ? 1 : 41 };
        }
    }
}
