using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;

namespace MistikLauncher.Pages;

public sealed class ModProjectView : Grid
{
    readonly HttpClient client;
    readonly MainWindow main;
    readonly string id;
    readonly Func<string?, Task<bool>> install;
    readonly StackPanel heading = new();
    readonly TabControl tabs = new() { Background = Brushes.Transparent, Foreground = Brushes.White };
    readonly TextBlock status = PageHelpers.Lbl("", 13, "#ADBED6", wrap: TextWrapping.Wrap);
    CancellationTokenSource? cancellation;
    WebView2? browser;
    string T(string tr, string en) => Localization.Language == "en" ? en : tr;

    public ModProjectView(MainWindow main, HttpClient client, string id, Action back, Func<string?, Task<bool>> install)
    {
        this.main = main; this.client = client; this.id = id; this.install = install;
        tabs.BorderBrush = PageHelpers.HexBrush("#3A3F46"); tabs.Padding = new Thickness(12);
        tabs.ItemsPanel = (ItemsPanelTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Orientation="Horizontal" Margin="-2,0,0,0"/>
            </ItemsPanelTemplate>
            """);
        tabs.ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem">
              <Setter Property="Foreground" Value="#C6CBD2"/><Setter Property="Background" Value="#30343A"/>
              <Setter Property="FontSize" Value="15"/><Setter Property="FontWeight" Value="SemiBold"/>
              <Setter Property="Width" Value="120"/><Setter Property="Margin" Value="0,0,8,10"/><Setter Property="MinHeight" Value="44"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
                <Border Background="{TemplateBinding Background}" CornerRadius="18" Padding="20,10">
                  <ContentPresenter ContentSource="Header" HorizontalAlignment="Center"/>
                </Border>
              </ControlTemplate></Setter.Value></Setter>
              <Style.Triggers><Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="#195D40"/><Setter Property="Foreground" Value="#38E58E"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Background" Value="#195D40"/></Trigger></Style.Triggers>
            </Style>
            """);
        Margin = new Thickness(22, 16, 22, 16);
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var backButton = PageHelpers.MkBtn(T("← Modlara dön", "← Back to mods"), "#28445E");
        backButton.HorizontalAlignment = HorizontalAlignment.Left;
        backButton.Click += (_, _) => back(); heading.Children.Add(backButton);
        heading.Children.Add(status); Children.Add(heading);
        SetRow(tabs, 2); Children.Add(tabs);
        Loaded += async (_, _) => await Load();
        Unloaded += (_, _) => { cancellation?.Cancel(); browser?.Dispose(); browser = null; };
    }

    internal static string DescriptionHtml(string markdown)
    {
        var html = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().Build());
        return "<!doctype html><html><head><meta charset='utf-8'><meta name='referrer' content='no-referrer'>" +
            "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; style-src 'unsafe-inline'; img-src https://cdn.modrinth.com https://cdn-raw.modrinth.com https://raw.githubusercontent.com https://i.imgur.com;\">" +
            "<style>body{background:#24272c;color:#c6cbd2;font:16px/1.6 Segoe UI,sans-serif;padding:20px;overflow-wrap:anywhere}h1,h2,h3{color:white}a{color:#19cb6c}img{max-width:100%;height:auto}pre,code{background:#17191c}pre{padding:12px;overflow:auto}table{border-collapse:collapse}td,th{border:1px solid #454a51;padding:8px}</style></head><body>" + html + "</body></html>";
    }

    internal static Uri? ImageUrl(string? raw) => Uri.TryCreate(raw, UriKind.Absolute, out var url) &&
        url.Scheme == "https" && url.IsDefaultPort && url.UserInfo.Length == 0 &&
        url.Host is "cdn.modrinth.com" or "cdn-raw.modrinth.com" ? url : null;

    internal static Uri? GalleryImageUrl(string? raw, string? preview) => ImageUrl(raw) ?? ImageUrl(preview);

    internal static FrameworkElement Icon(string? raw, int size)
    {
        var border = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(12),
            Background = PageHelpers.HexBrush("#34383E"), Margin = new Thickness(0, 0, 18, 0), ClipToBounds = true,
            Child = new TextBlock { Text = "◇", FontSize = 32, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var url = ImageUrl(raw);
        if (url != null)
        {
            async Task LoadIcon()
            {
                var picture = await ModCatalogImages.LoadAsync(url, size);
                if (picture != null) border.Child = new Image { Source = picture, Stretch = Stretch.Uniform };
            }
            _ = LoadIcon();
        }
        return border;
    }

    public void RefreshLanguage() => _ = Load();

    async Task Load()
    {
        cancellation?.Cancel(); cancellation = new(); var token = cancellation.Token;
        browser?.Dispose(); browser = null;
        ((Button)heading.Children[0]).Content = T("← Modlara dön", "← Back to mods");
        status.Text = T("Mod bilgileri yükleniyor…", "Loading project…");
        try
        {
            var projectTask = client.GetStringAsync("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(id), token);
            var versionsTask = client.GetStringAsync("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(id) + "/version", token);
            await Task.WhenAll(projectTask, versionsTask);
            if (token.IsCancellationRequested) return;
            var project = JObject.Parse(await projectTask);
            if (project["project_type"]?.ToString() != "mod" || project["id"]?.ToString() != id)
                throw new InvalidOperationException(T("Geçersiz mod bilgisi.", "Invalid project metadata."));
            var versions = JArray.Parse(await versionsTask);
            while (heading.Children.Count > 2) heading.Children.RemoveAt(2);
            var header = new Grid { Margin = new Thickness(0, 16, 0, 20) };
            header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            header.Children.Add(Icon(project["icon_url"]?.ToString(), 92));
            var copy = new StackPanel(); SetColumn(copy, 1); header.Children.Add(copy);
            copy.Children.Add(PageHelpers.Lbl(project["title"]?.ToString() ?? id, 26, "#FFFFFF", true));
            copy.Children.Add(PageHelpers.Lbl(project["description"]?.ToString() ?? "", 15, "#ADBED6", wrap: TextWrapping.Wrap));
            copy.Children.Add(PageHelpers.Lbl($"↓ {project["downloads"]?.Value<long>() ?? 0:N0}   ♡ {project["followers"]?.Value<long>() ?? 0:N0}", 13, "#ADBED6"));
            copy.Children.Add(PageHelpers.Lbl(string.Join("  ·  ", (project["categories"] as JArray ?? new()).Select(value => value.ToString())), 12, "#ADBED6", wrap: TextWrapping.Wrap));
            var latest = InstallButton(null); SetColumn(latest, 2); header.Children.Add(latest);
            heading.Children.Add(header); status.Text = ""; tabs.Items.Clear();
            var description = new Grid();
            browser = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 36, 39, 44) };
            description.Children.Add(browser);
            tabs.Items.Add(new TabItem { Header = T("Açıklama", "Description"), Content = description });
            var versionPanel = new StackPanel { Margin = new Thickness(16) };
            var profile = main.Config.Version ?? "";
            var game = GameProfiles.MinecraftVersion(App.GameDir, profile);
            var loader = GameProfiles.Loader(App.GameDir, profile);
            var compatible = versions.Where(version => ModManagerPage.CheckCompatibility(version, game, loader)).Take(100).ToArray();
            latest.IsEnabled = compatible.Length > 0;
            versionPanel.Children.Add(PageHelpers.Lbl(T("Seçili profil: ", "Selected profile: ") + game + " / " + loader, 14, "#ADBED6"));
            if (compatible.Length == 0) versionPanel.Children.Add(PageHelpers.Lbl(T("Bu profil için uyumlu sürüm yok.", "No compatible version for this profile."), 15, "#F0CF84"));
            foreach (var version in compatible)
            {
                var row = new DockPanel { Margin = new Thickness(0, 8, 0, 8) };
                var button = InstallButton(version["id"]?.ToString()); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
                row.Children.Add(PageHelpers.Lbl((version["name"]?.ToString() ?? "") + "\n" + version["version_number"] + "  ·  " + version["version_type"], 14, "#FFFFFF", wrap: TextWrapping.Wrap));
                versionPanel.Children.Add(row);
            }
            tabs.Items.Add(new TabItem { Header = T("Sürümler", "Versions"), Content = new ScrollViewer { Content = versionPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            var gallery = new StackPanel { Margin = new Thickness(16) };
            foreach (var item in (project["gallery"] as JArray ?? new()).Take(30))
            {
                var previewUrl = ImageUrl(item["url"]?.ToString());
                if (GalleryImageUrl(item["raw_url"]?.ToString(), previewUrl?.AbsoluteUri) is not Uri url) continue;
                gallery.Children.Add(PageHelpers.Lbl(item["title"]?.ToString() ?? "", 18, "#FFFFFF", true));
                var image = new Image { MaxHeight = 520, MaxWidth = 1200, HorizontalAlignment = HorizontalAlignment.Center,
                    Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 12) };
                gallery.Children.Add(image);
                async Task LoadGalleryImage()
                {
                    var picture = await ModCatalogImages.LoadAsync(url, 1200, token);
                    if (picture is null && previewUrl is not null && previewUrl != url)
                        picture = await ModCatalogImages.LoadAsync(previewUrl, 1200, token);
                    if (!token.IsCancellationRequested) image.Source = picture;
                }
                _ = LoadGalleryImage();
                gallery.Children.Add(PageHelpers.Lbl(item["description"]?.ToString() ?? "", 14, "#ADBED6", wrap: TextWrapping.Wrap));
            }
            if (gallery.Children.Count == 0) gallery.Children.Add(PageHelpers.Lbl(T("Bu modun galeri resmi yok.", "This project has no gallery images."), 15, "#ADBED6"));
            tabs.Items.Add(new TabItem { Header = T("Galeri", "Gallery"), Content = new ScrollViewer { Content = gallery, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            try
            {
                var activeBrowser = browser;
                var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(App.AppData, "mod-description-browser"));
                await activeBrowser.EnsureCoreWebView2Async(environment);
                if (token.IsCancellationRequested) return;
                activeBrowser.CoreWebView2.Settings.IsScriptEnabled = false;
                activeBrowser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                activeBrowser.CoreWebView2.Settings.IsWebMessageEnabled = false;
                activeBrowser.CoreWebView2.NavigationStarting += (_, args) => {
                    if (args.Uri == "about:blank") return;
                    args.Cancel = true;
                    if (args.IsUserInitiated && Uri.TryCreate(args.Uri, UriKind.Absolute, out var url) && url.Scheme == "https" && url.UserInfo.Length == 0)
                        try { Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }); } catch (Exception error) { status.Text = error.Message; }
                };
                activeBrowser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
                activeBrowser.CoreWebView2.DownloadStarting += (_, args) => args.Cancel = true;
                activeBrowser.NavigateToString(DescriptionHtml(project["body"]?.ToString() ?? ""));
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                browser?.Dispose(); browser = null; description.Children.Clear();
                description.Children.Add(new ScrollViewer { Content = PageHelpers.Lbl(project["body"]?.ToString() ?? "", 15, "#ADBED6", wrap: TextWrapping.Wrap) });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { status.Text = T("Yüklenemedi: ", "Could not load: ") + error.Message; }
    }

    Button InstallButton(string? version)
    {
        var button = PageHelpers.MkBtn(T("↓ Kur", "↓ Install"), "#159D59", 118);
        button.VerticalAlignment = VerticalAlignment.Top; button.Margin = new Thickness(12, 0, 0, 0);
        button.Click += async (_, _) => await ModManagerPage.RunInstallButton(button, () => install(version), error => status.Text = error.Message, T("Kuruldu", "Installed"));
        return button;
    }
}
