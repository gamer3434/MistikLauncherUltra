using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher.Pages;
public class ShaderManagerPage : Page, ILanguagePage
{
    readonly MainWindow main;
    readonly HttpClient client;
    readonly TextBox query;
    readonly StackPanel results = new(), installed = new();
    readonly TextBlock status;
    readonly Button search;
    CancellationTokenSource? pending;
    string? searchProfile;
    bool installing;
    static readonly HttpClient Http = CreateHttp();
    static HttpClient CreateHttp()
    {
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60), MaxResponseContentBufferSize = ShaderFiles.MaximumBytes };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/6.2 (gamer3434/MistikLauncherUltra)"); return http;
    }
    static string Root => Path.Combine(App.GameDir, "shaderpacks");
    public ShaderManagerPage(MainWindow main, HttpClient? client = null)
    {
        this.main = main; this.client = client ?? Http; Background = Brushes.Transparent;
        var body = new StackPanel { Margin = new Thickness(40, 30, 40, 30) };
        body.Children.Add(PageHelpers.Lbl("shaderTitle", 28, bold: true));
        body.Children.Add(PageHelpers.Lbl("shaderIntro", 14, "#A0A0A0"));
        body.Children.Add(PageHelpers.Lbl("shaderRequirement", 13, "#ADBED6", pad: new Thickness(0, 12, 0, 12)));
        var actions = new WrapPanel();
        var iris = PageHelpers.MkBtn("shaderSetup"); iris.Margin = new Thickness(0, 0, 10, 10);
        iris.Click += async (_, _) =>
        {
            await ModManagerPage.RunInstallButton(iris, async () =>
            {
                if (GameProfiles.Loader(App.GameDir, main.Config.Version ?? "") != "fabric")
                {
                    main.Navigate("Vers"); throw new IOException(Localization.T("clientFabricRequired"));
                }
                main.Navigate("Mods");
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                if (main.FindName("MainFrame") is not Frame { Content: ModManagerPage mods }) throw new IOException(Localization.T("shaderProfileChanged"));
                return await mods.InstallShaderSupportAsync();
            }, e => MessageBox.Show(e.Message, Localization.T("error"), MessageBoxButton.OK, MessageBoxImage.Warning));
        }; actions.Children.Add(iris);
        var folder = PageHelpers.MkBtn("shaderFolder", "#226DA0"); folder.Margin = new Thickness(0, 0, 10, 10);
        actions.Children.Add(folder); body.Children.Add(actions);
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        query = PageHelpers.DarkTextBox(); query.Margin = new Thickness(0, 0, 10, 0);
        query.KeyDown += async (_, e) => { if (e.Key == Key.Enter && !installing) { e.Handled = true; await Search(); } };
        search = PageHelpers.MkBtn("shaderSearch"); search.Click += async (_, _) => await Search();
        Grid.SetColumn(search, 1); row.Children.Add(query); row.Children.Add(search); body.Children.Add(row);
        status = PageHelpers.Lbl("", 13, "#ADBED6", pad: new Thickness(0, 10, 0, 10)); body.Children.Add(status);
        folder.Click += (_, _) => { try { ShaderFiles.EnsureRoot(Root); Process.Start(new ProcessStartInfo(Root) { UseShellExecute = true }); } catch (Exception e) { status.Text = e.Message; } };
        body.Children.Add(results); body.Children.Add(PageHelpers.Lbl("shaderInstalled", 18, bold: true, pad: new Thickness(0, 16, 0, 8))); body.Children.Add(installed);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += async (_, _) => { RenderInstalled(); if (!installing && (results.Children.Count == 0 || searchProfile != main.Config.Version)) await Search(); };
        Unloaded += (_, _) => pending?.Cancel(); RefreshLanguage();
    }
    public void RefreshLanguage()
    {
        System.Windows.Automation.AutomationProperties.SetName(query, Localization.T("shaderSearch"));
        Localization.TranslateTree(this); RenderInstalled();
    }
    async Task Search()
    {
        if (installing) return;
        pending?.Cancel(); pending?.Dispose(); pending = new CancellationTokenSource(); var cancel = pending;
        string profile = main.Config.Version ?? "", game = GameProfiles.MinecraftVersion(App.GameDir, profile);
        searchProfile = profile;
        results.Children.Clear(); status.Text = Localization.T("shaderSearching");
        try
        {
            string facets = JsonConvert.SerializeObject(new[] { new[] { "project_type:shader" }, new[] { "versions:" + game } });
            string url = "https://api.modrinth.com/v2/search?limit=20&index=downloads&query=" + Uri.EscapeDataString(query.Text.Trim()) + "&facets=" + Uri.EscapeDataString(facets);
            var json = JObject.Parse(await client.GetStringAsync(url, cancel.Token));
            if (cancel.IsCancellationRequested || main.Config.Version != profile) return;
            foreach (var hit in json["hits"] ?? new JArray())
            {
                if (hit["project_type"]?.ToString() != "shader") continue;
                string id = hit["project_id"]?.ToString() ?? "", title = hit["title"]?.ToString() ?? id;
                var card = PageHelpers.Card(); var content = new StackPanel { Margin = new Thickness(14) };
                content.Children.Add(PageHelpers.Lbl(title, 16, bold: true)); content.Children.Add(PageHelpers.Lbl(hit["description"]?.ToString() ?? "", 13, "#ADBED6"));
                var button = PageHelpers.MkBtn("shaderInstall", width: 100); button.HorizontalAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 8, 0, 0);
                button.Click += async (_, _) => await Install(id, profile, game, button); content.Children.Add(button); card.Child = content; results.Children.Add(card);
            }
            status.Text = game + " · " + (results.Children.Count == 0 ? Localization.T("shaderNoResults") : Localization.T("shaderResults"));
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!cancel.IsCancellationRequested) status.Text = Localization.T("error") + ": " + e.Message; }
    }
    async Task Install(string project, string profile, string game, Button button)
    {
        if (installing) return; installing = true; search.IsEnabled = query.IsEnabled = false; button.IsEnabled = false;
        try
        {
            if (main.Config.Version != profile) throw new IOException(Localization.T("shaderProfileChanged"));
            status.Text = Localization.T("shaderInstalling");
            var versions = JArray.Parse(await client.GetStringAsync("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(project) + "/version?game_versions=" + Uri.EscapeDataString(JsonConvert.SerializeObject(new[] { game }))));
            var version = versions.Where(v => v["project_id"]?.ToString() == project && ShaderFiles.Compatible(v, game) && v["version_type"]?.ToString() == "release")
                .OrderByDescending(v => v["date_published"]?.Value<DateTimeOffset>()).FirstOrDefault() ?? throw new IOException(Localization.T("shaderNoCompatible"));
            var files = version["files"] as JArray ?? throw new IOException(Localization.T("shaderInvalid"));
            var file = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true && f["filename"]?.ToString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                ?? files.FirstOrDefault(f => f["filename"]?.ToString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) ?? throw new IOException(Localization.T("shaderInvalid"));
            long size = file["size"]?.Value<long>() ?? 0;
            if (size <= 0 || size > ShaderFiles.MaximumBytes) throw new IOException(Localization.T("shaderInvalid"));
            var bytes = await client.GetByteArrayAsync(ModFiles.DownloadUrl(file["url"]?.ToString() ?? ""));
            if (bytes.LongLength != size) throw new IOException(Localization.T("shaderInvalid"));
            if (main.Config.Version != profile) throw new IOException(Localization.T("shaderProfileChanged"));
            await Task.Run(() =>
            {
                lock (main.ConfigurationGate)
                {
                    if (main.Config.Version != profile) throw new IOException(Localization.T("shaderProfileChanged"));
                    ShaderFiles.Install(Root, file["filename"]!.ToString(), bytes, file["hashes"]?["sha512"]?.ToString());
                }
            });
            RenderInstalled(); status.Text = Localization.T("shaderInstalledHelp");
        }
        catch (Exception e) { status.Text = Localization.T("error") + ": " + e.Message; }
        finally { installing = false; search.IsEnabled = query.IsEnabled = button.IsEnabled = true; }
    }
    void RenderInstalled()
    {
        installed.Children.Clear();
        try
        {
            foreach (string path in ShaderFiles.List(Root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                bool disabled = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var button = PageHelpers.MkBtn(disabled ? "shaderEnable" : "shaderDisable", "#226DA0"); DockPanel.SetDock(button, Dock.Right);
                button.Click += (_, _) => { try { ShaderFiles.Toggle(Root, path); RenderInstalled(); } catch (Exception e) { status.Text = e.Message; } };
                row.Children.Add(button); row.Children.Add(PageHelpers.Lbl(Path.GetFileName(disabled ? path[..^9] : path), pad: new Thickness(0, 0, 10, 0))); installed.Children.Add(row);
            }
            if (installed.Children.Count == 0) installed.Children.Add(PageHelpers.Lbl("shaderEmpty", color: "#ADBED6"));
        }
        catch (Exception e) { status.Text = e.Message; }
    }
}
