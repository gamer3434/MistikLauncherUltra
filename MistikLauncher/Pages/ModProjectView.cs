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
    JArray? projectVersions;
    Button? projectInstall;
    readonly List<Button> installButtons = new();
    bool installing;
    Border? projectSummary;
    FrameworkElement? projectIcon;
    TextBlock? projectTitle, projectDescription, projectCategories;
    string T(string tr, string en) => Localization.Language == "en" ? en : tr;

    public ModProjectView(MainWindow main, HttpClient client, string id, Action back, Func<string?, Task<bool>> install)
    {
        this.main = main; this.client = client; this.id = id; this.install = install;
        tabs.BorderBrush = PageHelpers.HexBrush("#3A3F46"); tabs.Padding = new Thickness(12);
        tabs.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabControl">
              <Grid>
                <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" Margin="0,0,0,12">
                  <ItemsPresenter/>
                </ScrollViewer>
                <Border Grid.Row="1" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="12"
                        Background="#171B22" Padding="{TemplateBinding Padding}">
                  <ContentPresenter x:Name="PART_SelectedContentHost" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    ContentSource="SelectedContent"/>
                </Border>
              </Grid>
            </ControlTemplate>
            """);
        tabs.ItemsPanel = (ItemsPanelTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
            """);
        tabs.ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem">
              <Setter Property="Foreground" Value="#C6CBD2"/><Setter Property="Background" Value="#30343A"/>
              <Setter Property="FontSize" Value="15"/><Setter Property="FontWeight" Value="SemiBold"/>
              <Setter Property="MinWidth" Value="120"/><Setter Property="Margin" Value="0,0,8,0"/><Setter Property="MinHeight" Value="44"/>
              <Setter Property="BorderBrush" Value="Transparent"/><Setter Property="BorderThickness" Value="2"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
                <Border Background="{TemplateBinding Background}" CornerRadius="18" Padding="18,8"
                        BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                  <ContentPresenter ContentSource="Header" HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                </Border>
              </ControlTemplate></Setter.Value></Setter>
              <Style.Triggers><Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="#195D40"/><Setter Property="Foreground" Value="#38E58E"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter Property="BorderBrush" Value="#557361"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="BorderBrush" Value="#38E58E"/></Trigger></Style.Triggers>
            </Style>
            """);
        Margin = new Thickness(22, 16, 22, 16);
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var backButton = PageHelpers.MkBtn(T("← Modlara dön", "← Back to mods"), "#28445E");
        backButton.HorizontalAlignment = HorizontalAlignment.Left;
        backButton.Click += (_, _) => back(); heading.Children.Add(backButton);
        heading.Children.Add(status); Children.Add(heading);
        SetRow(tabs, 1); Children.Add(tabs);
        Loaded += async (_, _) => await Load();
        SizeChanged += (_, _) => UpdateCompactLayout();
        Unloaded += (_, _) => { cancellation?.Cancel(); browser?.Dispose(); browser = null; };
    }

    internal static string DescriptionHtml(string markdown)
    {
        var html = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().Build());
        return "<!doctype html><html><head><meta charset='utf-8'><meta name='referrer' content='no-referrer'>" +
            "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; style-src 'unsafe-inline'; img-src https://cdn.modrinth.com https://cdn-raw.modrinth.com https://raw.githubusercontent.com https://i.imgur.com;\">" +
            "<style>body{margin:0;background:#171b22;color:#c6cbd2;font:15px/1.7 Segoe UI,sans-serif;padding:16px;overflow-wrap:anywhere}h1,h2,h3{color:white;line-height:1.3}h1:first-child,h2:first-child,p:first-child{margin-top:0}a{color:#38e58e}a:focus-visible{outline:2px solid #38e58e}img{max-width:100%;height:auto;border-radius:8px}pre,code{background:#0c1118;border-radius:6px}pre{padding:12px;overflow:auto}table{display:block;max-width:100%;overflow:auto;border-collapse:collapse}td,th{border:1px solid #454a51;padding:8px}blockquote{margin-left:0;border-left:3px solid #195d40;padding-left:16px}</style></head><body>" + html + "</body></html>";
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

    public void RefreshProfile()
    {
        if (projectVersions == null || projectInstall == null || tabs.Items.Count < 2) return;
        var versionPanel = new StackPanel { Margin = new Thickness(16) };
        var profile = main.Config.Version ?? "";
        var game = GameProfiles.MinecraftVersion(App.GameDir, profile);
        var loader = GameProfiles.Loader(App.GameDir, profile);
        var compatible = projectVersions.Where(version => ModManagerPage.CheckCompatibility(version, game, loader)).Take(100).ToArray();
        installButtons.Clear(); installButtons.Add(projectInstall);
        projectInstall.IsEnabled = !installing && compatible.Length > 0;
        projectInstall.ToolTip = compatible.Length == 0 ? T("Bu Minecraft sürümü ve yükleyici için uyumlu dosya yok.", "No compatible file for this Minecraft version and loader.") : T("Seçili profile en güncel uyumlu sürümü kur.", "Install the latest compatible version into the selected profile.");
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(projectInstall, true);
        versionPanel.Children.Add(PageHelpers.Lbl(T("Seçili profil: ", "Selected profile: ") + game + " / " + loader, 14, "#ADBED6"));
        if (compatible.Length == 0) versionPanel.Children.Add(PageHelpers.Lbl(T("Bu profil için uyumlu sürüm yok.", "No compatible version for this profile."), 15, "#F0CF84"));
        foreach (var version in compatible)
        {
            var row = new DockPanel { Margin = new Thickness(14) };
            var button = InstallButton(version["id"]?.ToString()); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
            var details = new StackPanel();
            details.Children.Add(PageHelpers.Lbl(version["name"]?.ToString() ?? "", 16, "#FFFFFF", true));
            details.Children.Add(PageHelpers.Lbl(string.Join("  ·  ", new[] { version["version_number"]?.ToString(), version["version_type"]?.ToString(), game, loader }.Where(value => !string.IsNullOrWhiteSpace(value))), 13, "#ADBED6"));
            row.Children.Add(details);
            var card = PageHelpers.Card("#202731", 10, "#303A45"); card.Child = row; versionPanel.Children.Add(card);
        }
        ((TabItem)tabs.Items[1]).Content = new ScrollViewer { Content = versionPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    void UpdateCompactLayout()
    {
        bool compact = ActualHeight > 0 && ActualHeight + Margin.Top + Margin.Bottom < 432;
        Margin = compact ? new Thickness(16, 10, 16, 10) : new Thickness(22, 16, 22, 16);
        tabs.Padding = new Thickness(compact ? 8 : 12);
        if (projectSummary == null || projectIcon == null || projectTitle == null || projectDescription == null || projectCategories == null) return;
        projectSummary.Padding = new Thickness(compact ? 8 : 16);
        projectSummary.Margin = compact ? new Thickness(0, 6, 0, 6) : new Thickness(0, 12, 0, 14);
        projectIcon.Width = projectIcon.Height = compact ? 48 : 92;
        projectIcon.Margin = new Thickness(0, 0, compact ? 12 : 18, 0);
        projectTitle.MaxHeight = compact ? 28 : 56;
        projectDescription.Visibility = projectCategories.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    async Task Load()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        browser?.Dispose(); browser = null;
        projectVersions = null; projectInstall = null;
        tabs.Items.Clear();
        while (heading.Children.Count > 2) heading.Children.RemoveAt(2);
        ((Button)heading.Children[0]).Content = T("← Modlara dön", "← Back to mods");
        status.Visibility = Visibility.Visible;
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
            var header = new Grid();
            header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            projectIcon = Icon(project["icon_url"]?.ToString(), 92); header.Children.Add(projectIcon);
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Top }; SetColumn(copy, 1); header.Children.Add(copy);
            var title = PageHelpers.Lbl(project["title"]?.ToString() ?? id, 22, "#FFFFFF", true);
            title.MaxHeight = 56; title.LineHeight = 28; title.TextTrimming = TextTrimming.CharacterEllipsis; title.ToolTip = title.Text;
            projectTitle = title;
            copy.Children.Add(title);
            var summary = PageHelpers.Lbl(project["description"]?.ToString() ?? "", 14, "#ADBED6");
            summary.MaxHeight = 42; summary.LineHeight = 21; summary.TextTrimming = TextTrimming.CharacterEllipsis; summary.ToolTip = summary.Text;
            projectDescription = summary;
            title.ToolTip = title.Text + "\n\n" + summary.Text;
            copy.Children.Add(summary);
            copy.Children.Add(PageHelpers.Lbl($"↓ {project["downloads"]?.Value<long>() ?? 0:N0}   ♡ {project["followers"]?.Value<long>() ?? 0:N0}", 13, "#ADBED6"));
            var categories = PageHelpers.Lbl(string.Join("  ·  ", (project["categories"] as JArray ?? new()).Select(value => value.ToString())), 12, "#ADBED6", wrap: TextWrapping.NoWrap);
            categories.TextTrimming = TextTrimming.CharacterEllipsis; categories.ToolTip = categories.Text; copy.Children.Add(categories);
            projectCategories = categories;
            var latest = InstallButton(null); latest.Name = "ProjectInstallButton"; SetColumn(latest, 2); header.Children.Add(latest);
            projectSummary = new Border { Name = "ProjectSummary", Child = header, Padding = new Thickness(16),
                CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = PageHelpers.HexBrush("#303A45"),
                Background = PageHelpers.HexBrush("#171B22"), Margin = new Thickness(0, 12, 0, 14) };
            heading.Children.Add(projectSummary); UpdateCompactLayout();
            status.Text = ""; status.Visibility = Visibility.Collapsed; tabs.Items.Clear();
            var description = new Grid();
            browser = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 23, 27, 34) };
            description.Children.Add(browser);
            tabs.Items.Add(new TabItem { Header = T("Açıklama", "Description"), Content = description });
            projectVersions = versions; projectInstall = latest;
            tabs.Items.Add(new TabItem { Header = T("Sürümler", "Versions") });
            RefreshProfile();
            var gallery = new StackPanel { Margin = new Thickness(16) };
            foreach (var item in (project["gallery"] as JArray ?? new()).Take(30))
            {
                var previewUrl = ImageUrl(item["url"]?.ToString());
                if (GalleryImageUrl(item["raw_url"]?.ToString(), previewUrl?.AbsoluteUri) is not Uri url) continue;
                var entry = new StackPanel();
                if (!string.IsNullOrWhiteSpace(item["title"]?.ToString())) entry.Children.Add(PageHelpers.Lbl(item["title"]!.ToString(), 16, "#FFFFFF", true));
                var image = new Image { MaxHeight = 520, MaxWidth = 1200, HorizontalAlignment = HorizontalAlignment.Center,
                    Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 12) };
                var imageState = new Border { MinHeight = 160, Background = PageHelpers.HexBrush("#0C1118"), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 10, 0, 10),
                    Child = PageHelpers.Lbl(T("Resim yükleniyor…", "Loading image…"), 14, "#ADBED6", pad: new Thickness(16)) };
                entry.Children.Add(imageState);
                async Task LoadGalleryImage()
                {
                    var picture = await ModCatalogImages.LoadAsync(url, 1200, token);
                    if (picture is null && previewUrl is not null && previewUrl != url)
                        picture = await ModCatalogImages.LoadAsync(previewUrl, 1200, token);
                    if (token.IsCancellationRequested) return;
                    if (picture is not null) { image.Source = picture; imageState.Child = image; }
                    else imageState.Child = PageHelpers.Lbl(T("Resim yüklenemedi. İnternet bağlantını kontrol edip sayfayı tekrar aç.", "Image could not load. Check your connection and reopen this page."), 14, "#F0CF84", pad: new Thickness(16));
                }
                bool requested = false;
                imageState.Loaded += async (_, _) => {
                    if (requested || token.IsCancellationRequested) return;
                    requested = true;
                    await LoadGalleryImage();
                };
                if (!string.IsNullOrWhiteSpace(item["description"]?.ToString())) entry.Children.Add(PageHelpers.Lbl(item["description"]!.ToString(), 14, "#ADBED6"));
                var card = PageHelpers.Card("#202731", 12, "#303A45"); card.Padding = new Thickness(14); card.Child = entry; gallery.Children.Add(card);
            }
            if (gallery.Children.Count == 0) gallery.Children.Add(PageHelpers.Lbl(T("Bu modun galeri resmi yok.", "This project has no gallery images."), 15, "#ADBED6"));
            tabs.Items.Add(new TabItem { Header = T("Galeri", "Gallery"), Content = new ScrollViewer { Content = gallery, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            var activeBrowser = browser;
            try
            {
                var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(App.AppData, "mod-description-browser"));
                if (token.IsCancellationRequested) return;
                await activeBrowser.EnsureCoreWebView2Async(environment);
                if (token.IsCancellationRequested) return;
                activeBrowser.CoreWebView2.Settings.IsScriptEnabled = false;
                activeBrowser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                activeBrowser.CoreWebView2.Settings.IsWebMessageEnabled = false;
                activeBrowser.CoreWebView2.NavigationStarting += (_, args) => {
                    if (args.Uri == "about:blank") return;
                    args.Cancel = true;
                    if (args.IsUserInitiated && Uri.TryCreate(args.Uri, UriKind.Absolute, out var url) && url.Scheme == "https" && url.UserInfo.Length == 0)
                        try { Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }); } catch (Exception error) { status.Visibility = Visibility.Visible; status.Text = error.Message; }
                };
                activeBrowser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
                activeBrowser.CoreWebView2.DownloadStarting += (_, args) => args.Cancel = true;
                activeBrowser.NavigateToString(DescriptionHtml(project["body"]?.ToString() ?? ""));
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                activeBrowser.Dispose();
                if (ReferenceEquals(browser, activeBrowser)) browser = null;
                description.Children.Clear();
                var fallback = PageHelpers.Lbl(project["body"]?.ToString() ?? "", 15, "#ADBED6", pad: new Thickness(16));
                fallback.VerticalAlignment = VerticalAlignment.Top;
                description.Children.Add(new ScrollViewer { Content = fallback, VerticalContentAlignment = VerticalAlignment.Top, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!token.IsCancellationRequested) { status.Visibility = Visibility.Visible; status.Text = T("Yüklenemedi: ", "Could not load: ") + error.Message; } }
    }

    Button InstallButton(string? version)
    {
        var button = PageHelpers.MkBtn(T("↓ Kur", "↓ Install"), "#159D59", 118);
        button.VerticalAlignment = VerticalAlignment.Top; button.Margin = new Thickness(12, 0, 0, 0);
        button.IsEnabled = !installing; installButtons.Add(button);
        button.Click += async (_, _) => {
            if (installing) return;
            installing = true;
            foreach (var pendingButton in installButtons) pendingButton.IsEnabled = false;
            try
            {
                await ModManagerPage.RunInstallButton(button, () => install(version), error => { status.Visibility = Visibility.Visible; status.Text = error.Message; }, T("Kuruldu", "Installed"));
            }
            finally { installing = false; RefreshProfile(); }
        };
        return button;
    }
}
