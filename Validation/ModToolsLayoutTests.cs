using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class ModToolsLayoutTests
{
    public static int Run(MainWindow window)
    {
        var previousLanguage = Localization.Language;
        using var client = new HttpClient(new EmptyCatalog());
        try
        {
            foreach (var language in new[] { "tr", "en" })
            {
                Localization.SetLanguage(language);
                var page = new ModManagerPage(window, client);
                foreach (var width in new[] { 640d, 768d, 1200d })
                {
                    page.Measure(new Size(width, 720)); page.Arrange(new Rect(0, 0, width, 720)); page.UpdateLayout();
                    var searchControls = Nodes(page).OfType<Grid>().Single(grid => grid.Name == "ModSearchControls");
                    if (searchControls.Children.OfType<FrameworkElement>().Any(control => control.TranslatePoint(new Point(control.ActualWidth, control.ActualHeight), searchControls).X > searchControls.ActualWidth + 1))
                        throw new Exception("Search, import and folder controls must fit the catalog at compact widths.");
                    if (searchControls.ActualWidth < 650 && searchControls.Children.OfType<Button>().Count(button => Grid.GetRow(button) == 1) != 2)
                        throw new Exception("Compact catalog tools should flow below search instead of squeezing its input.");
                    var header = Nodes(page).OfType<Grid>().Single(grid => grid.Name == "InstalledModsHeader");
                    var actions = Nodes(page).OfType<WrapPanel>().Single(panel => panel.Name == "InstalledModActions");
                    var title = header.Children.OfType<TextBlock>().Single();
                    if (title.ActualWidth < 140 || actions.Children.OfType<Button>().Any(button =>
                        button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), header).X > header.ActualWidth + 1))
                        throw new Exception("Compact installed-mod heading and backup actions must fit their viewport without narrowing the heading or cutting off buttons.");
                    if (header.ActualWidth < 700 && Grid.GetRow(actions) != 1)
                        throw new Exception("Compact mod backup actions must move below their heading.");
                }
            }
            Console.WriteLine("PASS installed-mod backup controls and heading fit 640/768/1200 DIP in both languages");
            Console.WriteLine("PASS catalog search controls reflow at compact widths in both languages");
            return 2;
        }
        finally { Localization.SetLanguage(previousLanguage); }
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach (var descendant in Nodes(child)) yield return descendant;
    }
    sealed class EmptyCatalog : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"hits\":[],\"total_hits\":0}") });
    }
}
