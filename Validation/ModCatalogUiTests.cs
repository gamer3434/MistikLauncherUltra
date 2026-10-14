using System.Net;
using System.IO;
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
    public static int Run(MainWindow window, Action<string> capture, Action<string, int, int>? compactCapture = null)
    {
        var frame = (Frame)window.FindName("MainFrame"); var previous = frame.Content;
        var selectNav = typeof(MainWindow).GetMethod("SelectNav", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var previousNav = (string)typeof(MainWindow).GetField("_currentNav", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        selectNav.Invoke(window, new object[] { "Mods" });
        var fixture = new Fixture(window);
        using var client = new HttpClient(fixture);
        var page = new ModManagerPage(window, client);
        void FlushPage()
        {
            Flush(window);
            var pending = typeof(ModManagerPage).GetField("_installedCancellation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (pending.GetValue(page) != null && DateTime.UtcNow < deadline) { Flush(window); Thread.Sleep(1); }
            if (pending.GetValue(page) != null) throw new Exception("Installed catalog state did not settle.");
        }
        try
        {
            frame.Content = page; Flush(window); capture("mod-catalog-cards");
            var sortBox = Nodes(page).OfType<ComboBox>().Single(box => box.Name == "ModSort");
            sortBox.ApplyTemplate();
            if (sortBox.Template.FindName("DropDownToggle", sortBox) is null || sortBox.ItemContainerStyle is null ||
                sortBox.Background is not System.Windows.Media.SolidColorBrush sortBackground || sortBackground.Color == System.Windows.Media.Colors.White)
                throw new Exception("The catalog sort selector should use the launcher's dark combo box template.");
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
            var compatibility = Nodes(page).OfType<CheckBox>().Single(box => box.Name == "ModCompatibleOnly");
            compatibility.IsChecked = true; Flush(window);
            var filteredRequest = Uri.UnescapeDataString(fixture.SearchRequests.Last().Query);
            var filterGame = GameProfiles.MinecraftVersion(App.GameDir, window.Config.Version);
            var filterLoader = GameProfiles.Loader(App.GameDir, window.Config.Version);
            if (!filteredRequest.Contains("offset=0") || !filteredRequest.Contains("versions:" + filterGame) || !filteredRequest.Contains("categories:" + filterLoader))
                throw new Exception("Compatibility filter must reset pagination and query the selected Minecraft version and loader together.");
            var beforeFilterProfile = window.Config.Version;
            var previousRequests = fixture.SearchRequests.Count;
            window.SetVersion("1.21.1"); Flush(window);
            if (fixture.SearchRequests.Count != previousRequests || !Nodes(page).OfType<TextBlock>().Any(text => text.Text == Localization.T("modFilterNeedsLoader")) || nextPage.IsEnabled)
                throw new Exception("Vanilla compatibility filter must show guidance without querying invalid loader facets or retaining old results.");
            window.SetVersion(beforeFilterProfile); Flush(window);
            if (fixture.SearchRequests.Count <= previousRequests || !Uri.UnescapeDataString(fixture.SearchRequests.Last().Query).Contains("categories:" + filterLoader))
                throw new Exception("Changing back to a modded profile must automatically refresh filtered results.");
            compatibility.IsChecked = false; Flush(window);
            if (Uri.UnescapeDataString(fixture.SearchRequests.Last().Query).Contains("versions:"))
                throw new Exception("Turning the compatibility filter off must restore all-version browsing.");
            var oldLanguage = Localization.Language;
            Localization.SetLanguage(oldLanguage == "en" ? "tr" : "en"); page.RefreshLanguage();
            if (((ComboBoxItem)sortBox.Items[0]).Content?.ToString() != Localization.T("modSortRelevance") ||
                System.Windows.Automation.AutomationProperties.GetName(nextPage) != Localization.T("modNextPage") ||
                !Nodes(page).OfType<Button>().Any(button => Equals(button.Content, Localization.T("modInstall"))) ||
                !Nodes(page).OfType<Expander>().Any(expander => Equals(expander.Header, Localization.T("modTools"))))
                throw new Exception("Cached mod sorting and pagination labels must follow the selected language.");
            Localization.SetLanguage(oldLanguage); page.RefreshLanguage(); Flush(window);
            Console.WriteLine("PASS compatibility filter resets pages, follows selected profile, explains Vanilla, restores all mods and translates controls");
            FlushPage();
            var statusRoot = Path.Combine(App.AppData, "catalog-remove-fixture"); Directory.CreateDirectory(statusRoot);
            var statusJar = Path.Combine(statusRoot, "fixture.jar"); File.WriteAllText(statusJar, "verified catalog project fixture");
            var statusHash = Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(File.ReadAllBytes(statusJar))).ToLowerInvariant();
            fixture.InstalledVersionFiles[statusHash] = new JObject { ["id"] = "compatible", ["project_id"] = "fixture", ["files"] = new JArray(new JObject { ["hashes"] = new JObject { ["sha512"] = statusHash } }) };
            var statusSnapshot = InstalledModCatalog.ReadAsync(client, statusRoot, default).GetAwaiter().GetResult();
            var profileDirectory = (string)typeof(InstalledModCatalog).GetMethod("ProfileDirectory", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null,
                new object[] { App.GameDir, App.AppData, App.ModsDir, window.Config.Version, window.Config.LastSyncedVersion })!;
            Directory.CreateDirectory(profileDirectory);
            var profileJar = Path.Combine(profileDirectory, "catalog-verified-fixture.jar"); File.Copy(statusJar, profileJar);
            var refreshInstalled = typeof(ModManagerPage).GetMethod("RefreshInstalledStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            try {
                _ = refreshInstalled.Invoke(page, null); FlushPage();
                var actualRemove = Nodes(page).OfType<Button>().Single(button => button.Name == "CatalogRemoveButton" && button.Visibility == Visibility.Visible);
                if (!actualRemove.IsEnabled) throw new Exception("Settled asynchronous hash lookup must enable the verified remove action.");
                capture("mod-catalog-remove");
            }
            finally { File.Delete(profileJar); }
            _ = refreshInstalled.Invoke(page, null); FlushPage();
            if (Nodes(page).OfType<Button>().Any(button => button.Name == "CatalogRemoveButton" && button.Visibility == Visibility.Visible))
                throw new Exception("Deleting the verified JAR must hide the mini remove action after refreshed lookup.");
            Console.WriteLine("PASS asynchronous verified lookup enables mini remove and file deletion hides it again");
            var states = (Dictionary<string, CatalogInstallState>)typeof(InstalledModCatalog).GetField("projects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(statusSnapshot)!;
            var versionStates = (Dictionary<string, CatalogInstallState>)typeof(InstalledModCatalog).GetField("versions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(statusSnapshot)!;
            states["fixture"] = versionStates["compatible"] = CatalogInstallState.Installed;
            typeof(ModManagerPage).GetField("_catalogInstalled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(page, statusSnapshot);
            var applyStatus = typeof(ModManagerPage).GetMethod("ApplyCatalogStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            applyStatus.Invoke(page, null);
            var installedButton = Nodes(page).OfType<Button>().Single(button => Equals(button.Content, Localization.T("modCatalogInstalled")));
            if (installedButton.IsEnabled) throw new Exception("Verified installed card must replace install with a disabled installed button.");
            var removeButton = Nodes(page).OfType<Button>().Single(button => button.Name == "CatalogRemoveButton" && button.Visibility == Visibility.Visible);
            if (!removeButton.IsEnabled || !System.Windows.Automation.AutomationProperties.GetName(removeButton).Contains("Example project"))
                throw new Exception("Only verified installed cards must expose an accessible mini remove action.");
            var removeBusy = typeof(ModManagerPage).GetField("_catalogRemoving", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            removeBusy.SetValue(page, true); applyStatus.Invoke(page, null);
            if (removeButton.IsEnabled || Nodes(page).OfType<Button>().Any(button => Equals(button.Content, Localization.T("modInstall")) && button.IsEnabled))
                throw new Exception("A removal in progress must block duplicate removal and catalog installation.");
            removeBusy.SetValue(page, false); applyStatus.Invoke(page, null);
            var removeProject = typeof(ModManagerPage).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single(method => method.Name == "RemoveCatalogProject" && method.GetParameters().Length == 3);
            bool profileRejected = false;
            var beforeSynced = window.Config.LastSyncedVersion;
            try { removeProject.Invoke(page, new object[] { "fabric-999.999", statusSnapshot, "fixture" }); }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is IOException) { profileRejected = true; }
            if (!profileRejected || window.Config.LastSyncedVersion != beforeSynced || !File.Exists(statusJar))
                throw new Exception("A changed selected profile must reject removal before synchronizing or modifying files.");
            foreach (var flag in new[] { "launchPreparing", "gameRunning" })
            {
                var field = typeof(MainWindow).GetField(flag, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var old = field.GetValue(window); bool blocked = false;
                try {
                    field.SetValue(window, true);
                    try { removeProject.Invoke(page, new object[] { window.Config.Version, statusSnapshot, "fixture" }); }
                    catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is IOException error && error.Message == Localization.T("modCatalogRemoveBusy")) { blocked = true; }
                }
                finally { field.SetValue(window, old); }
                if (!blocked || !File.Exists(statusJar)) throw new Exception("Launch preparation and running games must block catalog removal before file changes.");
            }
            Console.WriteLine("PASS verified mini remove action blocks duplicate operations, changed profiles and active launch lifecycle");
            capture("mod-catalog-installed");
            states["fixture"] = CatalogInstallState.Disabled; applyStatus.Invoke(page, null);
            if (installedButton.IsEnabled || !Nodes(page).OfType<TextBlock>().Any(text => text.Text == Localization.T("modCatalogDisabled")))
                throw new Exception("Disabled mod must retain a readable disabled label rather than offering duplicate installation.");
            states["fixture"] = CatalogInstallState.Installed; applyStatus.Invoke(page, null);
            Console.WriteLine("PASS catalog installed and disabled states replace duplicate install action");
            var open = Nodes(page).OfType<Button>().Single(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Example project ·", StringComparison.Ordinal));
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (page.Content is not ModProjectView detail) throw new Exception("Card should open project inside its mod page.");
            detail.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Flush(window); capture("mod-catalog-description");
            var tabs = detail.Children.OfType<TabControl>().Single();
            var installedHeader = Nodes(detail).OfType<Button>().Single(button => button.Name == "ProjectInstallButton");
            if (!Equals(installedHeader.Content, Localization.T("modCatalogInstalled")) || installedHeader.IsEnabled)
                throw new Exception("Project details must share the catalog's verified installed state.");
            Console.WriteLine("PASS project detail shares installed state from its catalog without extra requests");
            if (tabs.Items.Count != 3) throw new Exception("Description, versions and gallery must all load. " + string.Join(" | ", Nodes(detail).OfType<TextBlock>().Select(text => text.Text)));
            if (tabs.Items.Cast<TabItem>().Any(tab => tab.MinWidth != 120)) throw new Exception("Project detail tabs should have consistent minimum widths without clipping translated labels.");
            if (tabs.ItemsPanel.LoadContent() is not StackPanel { Orientation: Orientation.Horizontal, Margin.Left: 0 } ) throw new Exception("Project detail tabs should share a flush horizontal strip without negative insets.");
            foreach (var width in new[] { 620d, 960d })
            {
                detail.Measure(new Size(width, 480)); detail.Arrange(new Rect(0, 0, width, 480)); detail.UpdateLayout();
                tabs.ApplyTemplate();
                var contentHost = (FrameworkElement)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                var items = tabs.Items.Cast<TabItem>().ToArray();
                var top = items[0].TranslatePoint(new Point(), detail).Y;
                foreach (var item in items)
                {
                    item.ApplyTemplate();
                    var border = (Border)System.Windows.Media.VisualTreeHelper.GetChild(item, 0);
                    if (Math.Abs(item.TranslatePoint(new Point(), detail).Y - top) > 0.5 ||
                        border.ActualWidth < item.MinWidth - 1 || border.ActualHeight < 44 ||
                        item.TranslatePoint(new Point(item.ActualWidth, item.ActualHeight), detail).Y >= contentHost.TranslatePoint(new Point(), detail).Y)
                        throw new Exception("Rounded detail tabs must remain whole, aligned and separate from their content at compact widths.");
                }
            }
            Flush(window);
            Console.WriteLine("PASS catalog card opens all three project detail tabs inside the launcher");
            tabs.SelectedIndex = 1; capture("mod-catalog-versions");
            var versions = (DependencyObject)((TabItem)tabs.Items[1]).Content;
            if (Nodes(versions).OfType<Button>().Count() != 1 || Nodes(versions).OfType<TextBlock>().Any(text => text.Text.Contains("Wrong build")))
                throw new Exception("Only compatible versions may offer installation.");
            Console.WriteLine("PASS project version list excludes incompatible builds");
            var selectedProfile = window.Config.Version;
            var preservedDescription = ((TabItem)tabs.Items[0]).Content;
            window.SetVersion("fabric-0.0.1"); Flush(window);
            if (Nodes(detail).OfType<Button>().Single(button => button.Name == "ProjectInstallButton").IsEnabled ||
                Nodes((DependencyObject)((TabItem)tabs.Items[1]).Content).OfType<Button>().Any() ||
                !ReferenceEquals(preservedDescription, ((TabItem)tabs.Items[0]).Content) || tabs.SelectedIndex != 1)
                throw new Exception("Selecting an incompatible profile must immediately update install eligibility and versions while preserving the description and selected tab.");
            window.SetVersion(selectedProfile); FlushPage();
            if (!Nodes(detail).OfType<Button>().Single(button => button.Name == "ProjectInstallButton").IsEnabled ||
                Nodes((DependencyObject)((TabItem)tabs.Items[1]).Content).OfType<Button>().Count() != 1)
                throw new Exception("Returning to a compatible profile must restore its installable version without reopening the project.");
            Console.WriteLine("PASS profile selection refreshes detail compatibility without reloading project content");
            var alternateProfile = GameProfiles.Loader(App.GameDir, selectedProfile) + "-" + GameProfiles.MinecraftVersion(App.GameDir, selectedProfile) + "-alternate";
            window.SetVersion(alternateProfile); Flush(window);
            var installForProfile = (Func<string?, Task<bool>>)typeof(ModProjectView).GetField("install", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(detail)!;
            try { installForProfile("compatible").GetAwaiter().GetResult(); }
            catch (HttpRequestException) { }
            if (fixture.InstallRequests != 1)
                throw new Exception("Project detail install must use the currently selected compatible profile rather than reject the profile captured when opening the page.");
            window.SetVersion(selectedProfile); Flush(window);
            Console.WriteLine("PASS detail installation uses click-time profile and reaches guarded download after profile selection");
            tabs.SelectedIndex = 2; capture("mod-catalog-gallery");
            var gallery = (DependencyObject)((TabItem)tabs.Items[2]).Content;
            if (!Nodes(gallery).OfType<TextBlock>().Any(text => text.Text.Contains("gallery", StringComparison.OrdinalIgnoreCase) || text.Text.Contains("galeri")))
                throw new Exception("Empty gallery should explain that no pictures were supplied.");
            var pending = new TaskCompletionSource<HttpResponseMessage>();
            fixture.PendingProject = pending;
            var obsoleteLoad = (Task)typeof(ModProjectView).GetMethod("Load", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(detail, null)!;
            fixture.PendingProject = null;
            detail.RefreshLanguage(); Flush(window);
            pending.SetException(new HttpRequestException("Obsolete request failed")); Flush(window);
            if (!obsoleteLoad.IsCompleted || Nodes(detail).OfType<TextBlock>().Any(text => text.Text.Contains("Obsolete request failed")))
                throw new Exception("An obsolete failed request must not overwrite the successfully refreshed project.");
            Console.WriteLine("PASS cancelled detail load cannot overwrite refreshed content with an obsolete error");
            fixture.ProjectTitle = string.Join(" ", Enumerable.Repeat("Uzun mod başlığı / Long project title", 10));
            fixture.ProjectDescription = string.Join(" ", Enumerable.Repeat("Uzun açıklama compact profile description", 24));
            fixture.ProjectCategories = new JArray(Enumerable.Repeat("long-category-name", 16));
            detail.RefreshLanguage(); Flush(window);
            detail.Measure(new Size(620, 480)); detail.Arrange(new Rect(0, 0, 620, 480)); detail.UpdateLayout();
            tabs.ApplyTemplate();
            var compactContent = (FrameworkElement)tabs.Template.FindName("PART_SelectedContentHost", tabs);
            var projectInstall = Nodes(detail).OfType<Button>().Single(button => button.Name == "ProjectInstallButton");
            var summary = Nodes(detail).OfType<FrameworkElement>().Single(element => element.Name == "ProjectSummary");
            var installStart = projectInstall.TranslatePoint(new Point(), detail);
            var installEnd = projectInstall.TranslatePoint(new Point(projectInstall.ActualWidth, projectInstall.ActualHeight), detail);
            if (compactContent.ActualHeight < 120 || installStart.X < 0 || installEnd.X > detail.ActualWidth + 0.5 ||
                summary.ActualHeight <= 0 || summary.TranslatePoint(new Point(0, summary.ActualHeight), detail).Y > tabs.TranslatePoint(new Point(), detail).Y)
                throw new Exception("Long project headings must keep installation in bounds and preserve a usable detail viewport at compact size.");
            capture("mod-catalog-long-heading");
            Console.WriteLine("PASS long project heading preserves compact install controls and content viewport");
            var shell = (FrameworkElement)window.Content;
            foreach (var size in new[] { new Size(800, 500), new Size(768, 540) })
            {
                shell.Measure(size); shell.Arrange(new Rect(size)); shell.UpdateLayout();
                tabs.SelectedIndex = 1; shell.UpdateLayout(); tabs.ApplyTemplate();
                compactContent = (FrameworkElement)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                if (compactContent.ActualHeight < 100 ||
                    projectInstall.TranslatePoint(new Point(projectInstall.ActualWidth, projectInstall.ActualHeight), shell).X > size.Width ||
                    compactContent.TranslatePoint(new Point(0, compactContent.ActualHeight), shell).Y > size.Height)
                    throw new Exception($"Full compact client must preserve100DIP content: shell{size}, page{detail.ActualWidth}x{detail.ActualHeight}, summary{summary.ActualHeight}, content{compactContent.ActualHeight}, installRight{projectInstall.TranslatePoint(new Point(projectInstall.ActualWidth, projectInstall.ActualHeight), shell).X}, contentBottom{compactContent.TranslatePoint(new Point(0, compactContent.ActualHeight), shell).Y}.");
                compactCapture?.Invoke("mod-detail-compact-" + (int)size.Width, (int)size.Width, (int)size.Height);
            }
            shell.Measure(new Size(1200, 820)); shell.Arrange(new Rect(0, 0, 1200, 820)); shell.UpdateLayout();
            Console.WriteLine("PASS full compact client preserves usable mod detail content with long metadata");
            var successfulPending = new TaskCompletionSource<HttpResponseMessage>();
            fixture.PendingProject = successfulPending;
            var obsoleteSuccess = (Task)typeof(ModProjectView).GetMethod("Load", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(detail, null)!;
            fixture.PendingProject = null;
            fixture.ProjectTitle = "Refreshed project"; fixture.ProjectDescription = "Refreshed description";
            fixture.ProjectCategories = new JArray("fabric", "optimization");
            detail.RefreshLanguage(); Flush(window);
            successfulPending.SetResult(Fixture.ProjectResponse("Obsolete successful project")); Flush(window);
            if (!obsoleteSuccess.IsCompleted || tabs.Items.Count != 3 ||
                !Nodes(detail).OfType<TextBlock>().Any(text => text.Text == "Refreshed project") ||
                Nodes(detail).OfType<TextBlock>().Any(text => text.Text == "Obsolete successful project"))
                throw new Exception("An obsolete successful request must not overwrite refreshed project metadata or tabs.");
            Console.WriteLine("PASS obsolete successful detail response cannot replace refreshed content");
            var unloadedPending = new TaskCompletionSource<HttpResponseMessage>();
            fixture.PendingProject = unloadedPending;
            var unloadedLoad = (Task)typeof(ModProjectView).GetMethod("Load", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(detail, null)!;
            detail.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var unloadedText = Nodes(detail).OfType<TextBlock>().Select(text => text.Text).ToArray();
            var unloadedTabCount = tabs.Items.Count;
            unloadedPending.SetResult(Fixture.ProjectResponse("Late unloaded project")); Flush(window);
            fixture.PendingProject = null;
            if (!unloadedLoad.IsCompleted || tabs.Items.Count != unloadedTabCount ||
                !Nodes(detail).OfType<TextBlock>().Select(text => text.Text).SequenceEqual(unloadedText))
                throw new Exception("Completing a pending request after unload must not mutate detail content or status.");
            Console.WriteLine("PASS unloaded detail ignores late metadata response");
            window.SetVersion(alternateProfile); Flush(window);
            var requestsBeforeBack = fixture.SearchRequests.Count;
            Nodes(detail).OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            if (fixture.SearchRequests.Count <= requestsBeforeBack)
                throw new Exception("Returning from details after changing profile must refresh catalog compatibility instead of restoring stale install controls.");
            window.SetVersion(selectedProfile); Flush(window);
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
            var pendingInstall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int installCalls = 0;
            var busyDetail = new ModProjectView(window, client, "fixture", () => { }, _ => { installCalls++; return pendingInstall.Task; });
            page.Content = busyDetail; busyDetail.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Flush(window);
            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            try
            {
                var busyHeader = Nodes(busyDetail).OfType<Button>().Single(button => button.Name == "ProjectInstallButton");
                busyHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.SetVersion("fabric-0.0.1"); Flush(window);
                if (busyHeader.IsEnabled) throw new Exception("Pending install must keep an incompatible profile's header disabled.");
                pendingInstall.SetException(new IOException("Controlled previous-profile install failure."));
                var deadline = DateTime.UtcNow.AddSeconds(3);
                var busyFlag = typeof(ModProjectView).GetField("installing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                while ((bool)busyFlag.GetValue(busyDetail)! && DateTime.UtcNow < deadline) { Flush(window); Thread.Sleep(1); }
                if ((bool)busyFlag.GetValue(busyDetail)! || busyHeader.IsEnabled)
                    throw new Exception("Old install completion must revalidate the current incompatible profile rather than enable installation.");
                window.SetVersion(selectedProfile); Flush(window);
                pendingInstall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                busyHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.SetVersion(alternateProfile); Flush(window);
                var busyVersions = (DependencyObject)((TabItem)busyDetail.Children.OfType<TabControl>().Single().Items[1]).Content;
                var busyRow = Nodes(busyVersions).OfType<Button>().Single();
                if (busyHeader.IsEnabled || busyRow.IsEnabled) throw new Exception("Compatible profile refresh must keep every install action disabled during an install.");
                busyRow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                busyHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (installCalls != 2) throw new Exception("Pending detail install must reject duplicate header or row invocation.");
                pendingInstall.SetResult(false); deadline = DateTime.UtcNow.AddSeconds(3);
                while ((bool)busyFlag.GetValue(busyDetail)! && DateTime.UtcNow < deadline) { Flush(window); Thread.Sleep(1); }
                if ((bool)busyFlag.GetValue(busyDetail)! || !busyHeader.IsEnabled)
                    throw new Exception("Completed install must restore availability for the current compatible profile.");
            }
            finally { window.SetVersion(selectedProfile); SynchronizationContext.SetSynchronizationContext(context); }
            Console.WriteLine("PASS pending detail install blocks duplicate actions and revalidates changed-profile availability after completion");
            return 17;
        }
        finally { frame.Content = previous; selectNav.Invoke(window, new object[] { previousNav }); }
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
        public int InstallRequests { get; private set; }
        public TaskCompletionSource<HttpResponseMessage>? PendingProject { get; set; }
        public string ProjectTitle { get; set; } = "Example project";
        public string ProjectDescription { get; set; } = "An illustrated catalog card opens description, versions and gallery.";
        public JArray ProjectCategories { get; set; } = new("optimization", "fabric");
        public JObject InstalledVersionFiles { get; } = new();
        public static HttpResponseMessage ProjectResponse(string title) => new(HttpStatusCode.OK) {
            Content = new StringContent(new JObject { ["id"]="fixture", ["project_type"]="mod", ["title"]=title,
                ["description"]="Late response", ["body"]="Late description", ["gallery"]=new JArray(), ["categories"]=new JArray("fabric") }.ToString())
        };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/version_files", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(InstalledVersionFiles.ToString()) });
            if (request.RequestUri!.AbsolutePath.EndsWith("/version/compatible", StringComparison.Ordinal))
            {
                InstallRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("Controlled install stop before file downloads.") });
            }
            if (PendingProject is not null && request.RequestUri!.AbsolutePath.EndsWith("/project/fixture")) return PendingProject.Task;
            var hit = new JObject { ["project_id"]="fixture", ["title"]="Example project", ["description"]="An illustrated catalog card opens description, versions and gallery.", ["downloads"]=12500, ["author"]="Example author", ["categories"]=new JArray("optimization", "fabric", "forge", "neoforge", "quilt"), ["versions"]=new JArray("1.20.1") };
            string game = GameProfiles.MinecraftVersion(App.GameDir, window.Config.Version);
            string loader = GameProfiles.Loader(App.GameDir, window.Config.Version);
            hit["versions"] = new JArray(game);
            var project = new JObject { ["id"]="fixture", ["project_type"]="mod", ["title"]=ProjectTitle, ["description"]=ProjectDescription, ["body"]="# About this mod\n\n**Rich description** and version selection.", ["downloads"]=12500, ["followers"]=42, ["gallery"]=new JArray(), ["categories"]=ProjectCategories.DeepClone() };
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
