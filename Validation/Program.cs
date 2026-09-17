using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MistikLauncher;
using Localization = MistikLauncher.Localization;

class Program
{
    static int checks;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "MistikValidation", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("MISTIK_DATA_DIR", testRoot);
            Directory.CreateDirectory(testRoot);
            checks += ModToggleTests.Run(Path.Combine(testRoot,"mod-toggle"));
            checks += ForgeTests.Run(Path.Combine(testRoot,"forge-tests"));
            checks += AutoMcsTests.Run(testRoot).GetAwaiter().GetResult();
            checks += LauncherUpdateTests.Run(testRoot).GetAwaiter().GetResult();
            int packageIndex=Array.IndexOf(args,"--verify-package");
            if(packageIndex>=0) {
                var packaged=MistikLauncher.Updates.UpdateEngine.Verify(Path.GetFullPath(args[packageIndex+1]));
                var current=typeof(LauncherUpdater).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
                Check(packaged.Version==current,"actual portable manifest matches compiled version and file hashes");
            }
            int helperIndex=Array.IndexOf(args,"--helper-smoke");
            if(helperIndex>=0) { HelperSmoke.Run(testRoot,Path.GetFullPath(args[helperIndex+1]),Path.GetFullPath(args[helperIndex+2])).GetAwaiter().GetResult(); checks++; }
            if(args.Contains("--live-mcs"))
            {
                var official=new AutoMcsUpdater(dataDirectory:Path.Combine(testRoot,"official-auto-mcs"),running:()=>false);
                Check(official.CheckAsync(true,true).GetAwaiter().GetResult(),"live official package download, digest and extraction");
                Console.WriteLine("Official installed version: "+official.InstalledVersion);
            }
            var config = new LauncherConfig { User="../bad", Ram=200, Lang="English", TunnelPort=-1, Role="Yonetici" };
            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();
            Check(loaded.User=="Player" && loaded.Ram==32 && loaded.TunnelPort==1 && loaded.Role=="User", "config validation");
            config.User="TestPlayer"; config.Ram=4; ConfigManager.Save(config);
            Check(File.Exists(Path.Combine(testRoot,"config.json.bak")), "atomic settings backup");
            File.WriteAllText(Path.Combine(testRoot,"config.json"), "{broken");
            Check(ConfigManager.Load().Ram==32 && ConfigManager.Load().User=="Player", "corrupt settings restores previous backup");
            ConfigManager.Save(config);
            Check(!ReleaseSecurity.AutomaticUpdatesEnabled && !MistikLauncher.App.AdminAccessEnabled, "unsafe remote controls disabled");
            bool rejected=false;
            try { ReleaseSecurity.ValidateUninstallTarget(Path.GetTempPath()); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected, "uninstall rejects arbitrary directory");
            Check(ReleaseSecurity.ValidateUninstallTarget(testRoot)==testRoot, "valid application directory recognized");
            var serverRoot=Path.Combine(testRoot,"servers","fixture"); Directory.CreateDirectory(serverRoot);
            var properties=Path.Combine(serverRoot,"server.properties");
            const string original="online-mode=true\nserver-port=25565\n";
            File.WriteAllText(properties,original);
            var relay=new MistikRelay("TestPlayer"); relay.EnforceOfflineModeInProperties();
            Check(File.ReadAllText(properties)==original, "tunnel preserves server authentication");
            var assembly=typeof(Localization).Assembly;
            using var tr=assembly.GetManifestResourceStream("MistikLauncher.Locales.tr.json")!;
            using var en=assembly.GetManifestResourceStream("MistikLauncher.Locales.en.json")!;
            var turkish=JsonSerializer.Deserialize<Dictionary<string,string>>(tr)!;
            var english=JsonSerializer.Deserialize<Dictionary<string,string>>(en)!;
            Check(turkish.Keys.Order().SequenceEqual(english.Keys.Order()), "locale keys match");
            Check(turkish.Values.All(v=>!string.IsNullOrWhiteSpace(v)) && english.Values.All(v=>!string.IsNullOrWhiteSpace(v)), "no empty translations");
            var application=new MistikLauncher.Application(); application.InitializeComponent();
            checks+=CloudTests.Run(Path.Combine(testRoot,"cloud-unit"));
            if(args.Contains("--live-cloud")) checks+=CloudTests.Live(Path.Combine(testRoot,"cloud-live")).GetAwaiter().GetResult();
            var window=new MainWindow { Width=1200, Height=820 };
            Directory.CreateDirectory(App.ModsDir);
            string disabledMod=Path.Combine(App.ModsDir,"kept.jar.disabled"); File.WriteAllText(disabledMod,"kept bytes");
            window.Config.LastSyncedVersion="1.20.1-forge-47.4.10"; window.Config.Version="1.19.2-forge-43.5.0"; window.SyncModsForCurrentVersion();
            Check(File.Exists(Path.Combine(testRoot,"mods_pool","1.20.1_forge","kept.jar.disabled")),"disabled mod state follows original version pool");
            window.Config.Version="1.20.1-forge-47.4.10"; window.SyncModsForCurrentVersion();
            Check(File.ReadAllText(disabledMod)=="kept bytes" && !File.Exists(disabledMod[..^9]),"returning to a version preserves disabled mod state");
            var modPage=new MistikLauncher.Pages.ModManagerPage(window);
            var installedMods=((StackPanel)((ScrollViewer)modPage.Content).Content).Children.OfType<StackPanel>().Last();
            Button ModToggleButton() => (Button)((Grid)((Border)installedMods.Children[0]).Child).Children.OfType<StackPanel>().Last().Children[0];
            ModToggleButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(File.Exists(disabledMod[..^9]) && !File.Exists(disabledMod),"installed mod Enable button applies immediately");
            ModToggleButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(File.Exists(disabledMod) && File.ReadAllText(disabledMod)=="kept bytes","installed mod Disable button preserves file and refreshes row");
            foreach(var style in MainWindow.WindowButtonStyles) {
                window.SetWindowButtons(style);
                Check(ConfigManager.Load().WindowButtons==style && ((StackPanel)window.FindName("CaptionButtons")).Children.Count==3,"window button style renders and persists: "+style);
            }
            window.SetWindowButtons("MacOS");
            var caption=(StackPanel)window.FindName("CaptionButtons");
            ((Button)caption.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.WindowState==WindowState.Minimized,"custom window button minimizes");
            window.WindowState=WindowState.Normal;
            ((Button)caption.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.WindowState==WindowState.Maximized,"custom window button maximizes");
            Check(((DockPanel)window.FindName("WindowSurface")).Margin.Left==6 && window.WindowStyle==WindowStyle.None,"maximized window reserves resize frame and uses one title bar");
            ((Button)caption.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.WindowState==WindowState.Normal,"custom window button restores");
            Check(((DockPanel)window.FindName("WindowSurface")).Margin.Left==0,"restored window removes maximized frame inset");
            ((Button)window.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Check(((Frame)window.FindName("MainFrame")).Content is MistikLauncher.Pages.ModernSettingsPage,"top-right player profile opens settings");
            var bar=(System.Windows.Controls.WrapPanel)window.FindName("QuickBarPanel");
            Check(bar.Children.Count==7,"top bar exposes seven bilingual shortcuts by default");
            window.Config.QuickLinks=new(){"Dash","Settings"}; ConfigManager.Save(window.Config); window.BuildQuickBar();
            Check(bar.Children.Count==3 && ConfigManager.Load().QuickLinks.SequenceEqual(new[]{"Dash","Settings"}),"shortcut customization persists");
            ((Button)bar.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Flush(window);
            Check(((Frame)window.FindName("MainFrame")).Content is MistikLauncher.Pages.ModernSettingsPage,"top shortcut navigates to requested page");
            window.Config.QuickLinks.Clear(); window.BuildQuickBar();
            Check(((Border)window.FindName("QuickBarHost")).Visibility==Visibility.Collapsed,"empty top bar hides without reserving space");
            window.Config.QuickLinks=new(){"Dash","Vers","Mods","Skin","Server","Settings"}; ConfigManager.Save(window.Config); window.BuildQuickBar();
            foreach(var name in ColorThemes.Names)
            {
                var surface=ColorThemes.Brush("#192C46"); var previous=surface.Color;
                window.SetColorTheme(name);
                Check(ConfigManager.Load().Accent==name && !surface.IsFrozen,"runtime theme persists and remains mutable: "+name);
                double Luminance(Color color) {
                    double Linear(byte c) { double v=c/255.0; return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4); }
                    return Linear(color.R)*0.2126+Linear(color.G)*0.7152+Linear(color.B)*0.0722;
                }
                double background=Luminance(ColorThemes.Brush("#00A3FF").Color), foreground=Luminance(((SolidColorBrush)ColorThemes.ActionText).Color);
                Check((Math.Max(background,foreground)+0.05)/(Math.Min(background,foreground)+0.05)>=4.5,"primary action text contrast meets 4.5:1: "+name);
                window.Navigate("Dash");
            }
            window.SetColorTheme("Amber");
            Check(window.FindName("NavSearch")==null && window.FindName("SearchLabel")==null,"unnecessary navigation search removed");
            ForgeTests.Run(App.GameDir);
            window.PopulateVersionBox();
            var versionBox=(ComboBox)window.FindName("VerBox");
            Check(versionBox.Items.Cast<object>().Any(x=>x.ToString()=="1.20.1-forge-47.4.10"),"official Forge appears in launcher version selector");
            versionBox.SelectedItem="1.20.1-forge-47.4.10";
            Check(ConfigManager.Load().Version=="1.20.1-forge-47.4.10","Forge selection is saved across restarts");
            var forgePath=Path.Combine(App.GameDir,"versions","1.20.1-forge-47.4.10","1.20.1-forge-47.4.10.json");
            var forgeJson=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(forgePath));
            forgeJson["mainClass"]="cpw.mods.bootstraplauncher.BootstrapLauncher";
            forgeJson["arguments"]=Newtonsoft.Json.Linq.JObject.Parse("{\"jvm\":[\"--add-opens\",\"java.base/java.lang=ALL-UNNAMED\",\"-DlibraryDirectory=${library_directory}\"],\"game\":[\"--launchTarget\",\"forgeclient\",\"--fml.forgeVersion\",\"47.4.10\"]}");
            File.WriteAllText(forgePath,forgeJson.ToString());
            var launch=(string)typeof(MainWindow).GetMethod("BuildLaunchArgs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,new object?[]{"1.20.1-forge-47.4.10",4096,Path.Combine(App.GameDir,"natives"),null,null})!;
            Check(launch.Contains("--launchTarget") && launch.Contains("forgeclient") && launch.Contains("--add-opens") && !launch.Contains("${"),"complete Forge launch command retains bootstrap parameters and expands paths");
            string output = args.Length>0 ? Path.GetFullPath(args[0]) : Path.Combine(Environment.CurrentDirectory,"docs","screenshots");
            Directory.CreateDirectory(output);
            foreach (var code in new[] { "tr", "en" })
            {
                window.SwitchLanguage(code);
                Check(Localization.T("play")== (code=="tr"?"Oyunu başlat":"Launch game"), "runtime language " + code);
                Check(ConfigManager.Load().Lang==(code=="tr"?"Turkce":"English"), "language persistence " + code);
                window.Navigate("Dash"); Capture(window,Path.Combine(output,"home-"+code+".png"));
                Check(Texts(window.Content as DependencyObject).Contains(Localization.T("welcome")),"cached home language " + code);
                window.Navigate("Settings"); Capture(window,Path.Combine(output,"settings-"+code+".png"));
                Check(Texts(window.Content as DependencyObject).Contains(Localization.T("luTitle")+" · "+window.LauncherUpdates.CurrentVersion),"cached settings language " + code);
                var settingsPage=(MistikLauncher.Pages.ModernSettingsPage)((Frame)window.FindName("MainFrame")).Content;
                var settingsScroll=((DockPanel)settingsPage.Content).Children.OfType<ScrollViewer>().Single();
                settingsScroll.ScrollToVerticalOffset(300); Capture(window,Path.Combine(output,"window-styles-"+code+".png"));
                settingsScroll.ScrollToTop();
                window.Navigate("Server"); Capture(window,Path.Combine(output,"server-"+code+".png"));
            }
            window.Close();
            Console.WriteLine($"{checks} checks passed. Test data: {testRoot}");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static IEnumerable<string> Texts(DependencyObject? node)
    {
        if(node==null) yield break;
        if(node is TextBlock text) yield return text.Text;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            foreach(var value in Texts(VisualTreeHelper.GetChild(node,i))) yield return value;
    }
    static void Capture(Window window,string path)
    {
        Flush(window);
        var root=(FrameworkElement)window.Content;
        root.Measure(new Size(1200,820)); root.Arrange(new Rect(0,0,1200,820)); root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1200,820,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path); encoder.Save(file);
    }
    static void Flush(Window window)
    {
        var frame=new DispatcherFrame();
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }
}


