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
            if(args.Length==3 && args[0]=="--pc-forge")
            {
                var isolated=Path.GetFullPath(args[1]);
                if(!Path.GetFileName(isolated).StartsWith("pc-test-",StringComparison.Ordinal)) throw new InvalidOperationException("Use a separate pc-test-* data directory.");
                Environment.SetEnvironmentVariable("MISTIK_DATA_DIR",isolated);
                Check(GameProfiles.IsInstalled(App.GameDir,args[2]),"isolated official Vanilla base is installed");
                var forge=ForgeInstaller.InstallAsync(args[2],(_,message)=>Console.WriteLine(message)).GetAwaiter().GetResult();
                Check(GameProfiles.IsInstalled(App.GameDir,forge),"real official Forge installer produces selectable inherited profile");
                Console.WriteLine("Installed Forge: "+forge); return 0;
            }
            string testRoot = Path.Combine(Path.GetTempPath(), "MistikValidation", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("MISTIK_DATA_DIR", testRoot);
            Directory.CreateDirectory(testRoot);
            InstallerTests.Run(testRoot,Check);
            Check(CrashDiagnostics.Category("java.lang.OutOfMemoryError")=="MLU-MEMORY","memory failure classification");
            Check(CrashDiagnostics.Category("UnsupportedClassVersionError")=="MLU-JAVA","Java failure classification");
            Check(CrashDiagnostics.Category("Incompatible mods found")=="MLU-MOD-DEPENDENCY","mod compatibility failure classification");
            Check(CrashDiagnostics.Category("exit code 1")=="MLU-EXIT","exit code alone does not blame a mod");
            Check(CrashDiagnostics.Category("ClassNotFoundException")=="MLU-CLASSPATH","missing class has actionable profile/library guidance");
            var manualReport=CrashDiagnostics.ManualReport();
            var savedReport=CrashDiagnostics.SaveReport(manualReport);
            Check(File.Exists(savedReport) && new FileInfo(savedReport).Length>0,"manual error report can be saved locally");
            Directory.CreateDirectory(App.ModsDir);
            File.WriteAllText(Path.Combine(App.ModsDir,"suspect.jar"),"fixture");
            File.WriteAllText(Path.Combine(App.ModsDir,"innocent.jar"),"fixture");
            var report=CrashDiagnostics.Report(1,DateTime.UtcNow,"ERROR failed mod file: suspect.jar\nLoaded mods: innocent.jar");
            Check(report.Contains(Path.Combine(App.ModsDir,"suspect.jar")) && !report.Contains(Path.Combine(App.ModsDir,"innocent.jar")),"only explicit failure evidence identifies a suspect mod path");
            Directory.CreateDirectory(Path.Combine(App.GameDir,"logs"));
            var stale=Path.Combine(App.GameDir,"logs","latest.log"); File.WriteAllText(stale,"STALE_CRASH_MARKER"); File.SetLastWriteTimeUtc(stale,DateTime.UtcNow.AddHours(-1));
            Check(!CrashDiagnostics.Report(1,DateTime.UtcNow,"new failure").Contains("STALE_CRASH_MARKER"),"previous run logs are excluded from new crash analysis");
            Check(!CrashDiagnostics.Redact("accessToken=secret refresh_token:private").Contains("secret") && !CrashDiagnostics.Redact("accessToken=secret refresh_token:private").Contains("private"),"diagnostic token redaction");
            var invalidLighting=ConfigManager.Normalize(new LauncherConfig { CloseLighting="bad",CloseRgb="invalid" });
            Check(invalidLighting.CloseLighting=="Theme" && invalidLighting.CloseRgb=="#FFB000","close lighting settings validation");
            checks += ModToggleTests.Run(Path.Combine(testRoot,"mod-toggle"));
            if(args.Contains("--live-mod")) checks+=ModToggleTests.Live(Path.Combine(testRoot,"official-mod")).GetAwaiter().GetResult();
            checks += ForgeTests.Run(Path.Combine(testRoot,"forge-tests"));
            checks += AutoMcsTests.Run(testRoot).GetAwaiter().GetResult();
            checks += LauncherUpdateTests.Run(testRoot).GetAwaiter().GetResult();
            checks += ReleaseCacheTests.Run(testRoot).GetAwaiter().GetResult();
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
            Check(loaded.User=="Player" && loaded.Ram==32 && loaded.TunnelPort==1 && loaded.Role=="User" && loaded.VersionCode==App.LocalVersion, "config validation and version migration");
            Check(!File.ReadAllText(Path.Combine(testRoot,"config.json")).Contains("Player"), "saved settings do not expose the player name");
            config.User="TestPlayer"; config.Ram=4; ConfigManager.Save(config);
            Check(File.Exists(Path.Combine(testRoot,"config.json.bak")), "atomic settings backup");
            Check(!File.ReadAllText(Path.Combine(testRoot,"config.json.bak")).Contains("Player"), "settings backup is encrypted");
            File.WriteAllText(Path.Combine(testRoot,"config.json"), "{broken");
            Check(ConfigManager.Load().Ram==32 && ConfigManager.Load().User=="Player", "corrupt settings restores previous backup");
            ConfigManager.Save(ConfigManager.Load());
            File.WriteAllText(Path.Combine(testRoot,"config.json"), "{broken again");
            Check(ConfigManager.Load().User=="Player", "saving recovered settings keeps a healthy backup");
            File.WriteAllText(Path.Combine(testRoot,"config.json"), "{\"user\":\"LegacyUser\",\"ram\":8}");
            Check(ConfigManager.Load().User=="LegacyUser", "legacy plaintext settings remain readable");
            ConfigManager.Save(ConfigManager.Load());
            Check(ConfigManager.Load().User=="LegacyUser" && !File.ReadAllText(Path.Combine(testRoot,"config.json")).Contains("LegacyUser") && !File.ReadAllText(Path.Combine(testRoot,"config.json.bak")).Contains("LegacyUser"), "legacy settings and backup migrate to encrypted files");
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
            checks+=SecurityTests.Run();
            checks+=OptimizationTests.Run(Path.Combine(testRoot,"graphics-tests"));
            var window=new MainWindow { Width=1200, Height=820 };
            Check(MainWindow.SkinTextureUrl("http://textures.minecraft.net/texture/test")=="https://textures.minecraft.net/texture/test" && MainWindow.SkinTextureUrl("https://ely.by.attacker.invalid/test")==null && MainWindow.SkinTextureUrl("file:///C:/Windows/test.png")==null,"skin texture URLs enforce trusted HTTPS hosts");
            Check(window.FetchAvatarAsync("../../outside").GetAwaiter().GetResult()==null,"avatar username traversal is rejected before network or cache access");
            Check(!MistikLauncher.Pages.SkinPage.LoadImgAsync(new Image(),"../../outside",80).GetAwaiter().GetResult(),"skin preview rejects invalid usernames before network access");
            Directory.CreateDirectory(App.ModsDir);
            string disabledMod=Path.Combine(App.ModsDir,"kept.jar.disabled"); File.WriteAllText(disabledMod,"kept bytes");
            window.Config.LastSyncedVersion="1.20.1-forge-47.4.10"; window.Config.Version="1.19.2-forge-43.5.0"; window.SyncModsForCurrentVersion();
            Check(File.Exists(Path.Combine(testRoot,"mods_pool","1.20.1_forge","kept.jar.disabled")),"disabled mod state follows original version pool");
            window.Config.Version="1.20.1-forge-47.4.10"; window.SyncModsForCurrentVersion();
            Check(File.ReadAllText(disabledMod)=="kept bytes" && !File.Exists(disabledMod[..^9]),"returning to a version preserves disabled mod state");
            var collision=Path.Combine(testRoot,"mods_pool","1.20.1_forge","kept.jar.disabled"); File.WriteAllText(collision,"existing pool copy");
            window.Config.Version="1.19.2-forge-43.5.0";
            Check(!window.SyncModsForCurrentVersion() && window.Config.LastSyncedVersion=="1.20.1-forge-47.4.10" && File.ReadAllText(disabledMod)=="kept bytes" && File.ReadAllText(collision)=="existing pool copy","failed launcher mod sync preserves files and retains previous version state");
            File.Delete(collision); window.Config.Version="1.20.1-forge-47.4.10";
            var modPage=new MistikLauncher.Pages.ModManagerPage(window);
            var installedMods=((StackPanel)((ScrollViewer)modPage.Content).Content).Children.OfType<StackPanel>().Last();
            Button ModToggleButton() => (Button)((Grid)((Border)installedMods.Children[0]).Child).Children.OfType<StackPanel>().Last().Children[0];
            ModToggleButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(File.Exists(disabledMod[..^9]) && !File.Exists(disabledMod),"installed mod Enable button applies immediately");
            ModToggleButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(File.Exists(disabledMod) && File.ReadAllText(disabledMod)=="kept bytes","installed mod Disable button preserves file and refreshes row");
            void ModFixture(string path,params (string name,string content)[] entries) {
                using var archive=System.IO.Compression.ZipFile.Open(path,System.IO.Compression.ZipArchiveMode.Create);
                foreach(var entry in entries) { using var writer=new StreamWriter(archive.CreateEntry(entry.name).Open()); writer.Write(entry.content); }
            }
            void Guard(string loader) => typeof(MainWindow).GetMethod("SuspendIncompatibleMods",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,new object[]{"1.20.1",loader});
            var openRange=Path.Combine(App.ModsDir,"open-range.jar");
            ModFixture(openRange,("fabric.mod.json","{\"depends\":{\"minecraft\":\">=1.19\"}}")); Guard("fabric");
            Check(File.Exists(openRange),"open-ended version constraints do not falsely suspend compatible mods"); File.Delete(openRange);
            var universal=Path.Combine(App.ModsDir,"universal.jar");
            ModFixture(universal,("fabric.mod.json","{}"),("META-INF/mods.toml","modLoader=\"javafml\"")); Guard("forge");
            Check(File.Exists(universal),"multi-loader mod metadata is preserved instead of guessed"); File.Delete(universal);
            var neo=Path.Combine(App.ModsDir,"neo-only.jar");
            ModFixture(neo,("META-INF/neoforge.mods.toml","modLoader=\"javafml\"")); Guard("forge");
            Check(!File.Exists(neo) && File.Exists(Path.Combine(testRoot,"mods_pool","incompatible","neo-only.jar")),"NeoForge-only mod is safely suspended from Forge without deletion");
            foreach(var style in MainWindow.WindowButtonStyles) {
                window.SetWindowButtons(style);
                Check(ConfigManager.Load().WindowButtons==style && ((StackPanel)window.FindName("CaptionButtons")).Children.Count==3,"window button style renders and persists: "+style);
                Check(((StackPanel)window.FindName("CaptionButtons")).Children.OfType<Button>().Count(CaptionHasGlow)==1,"only close has lighting: "+style);
            }
            var sourceText=new TextBlock { Text="initial" }; var boundText=new TextBlock();
            System.Windows.Data.BindingOperations.SetBinding(boundText,TextBlock.TextProperty,new System.Windows.Data.Binding("Text") { Source=sourceText });
            Localization.TranslateTree(boundText); sourceText.Text="updated"; Flush(window);
            Check(System.Windows.Data.BindingOperations.IsDataBound(boundText,TextBlock.TextProperty) && boundText.Text=="updated","legacy translation preserves live WPF text bindings");
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
            Check(App.LocalVersion=="v"+window.LauncherUpdates.CurrentVersion,"all local version labels match actual compiled version");
            Check(new LauncherConfig().VersionCode==App.LocalVersion && App.Changelog[0].Ver==App.LocalVersion,"config and changelog use the compiled release version");
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
            Check(((Border)window.FindName("QuickBarHost")).Visibility==Visibility.Visible && bar.Children.Count==1,"optimization remains reachable with no optional shortcuts");
            ((Button)bar.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Check(((Frame)window.FindName("MainFrame")).Content is MistikLauncher.Pages.OptimizationPage,"optimization shortcut opens its page");
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
                var secondary=MistikLauncher.Pages.PageHelpers.MkBtn("Secondary","#203853");
                double dark=Luminance(((SolidColorBrush)secondary.Background).Color), light=Luminance(((SolidColorBrush)secondary.Foreground).Color);
                Check((Math.Max(dark,light)+0.05)/(Math.Min(dark,light)+0.05)>=4.5,"secondary button stays readable after theme change: "+name);
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
            Check(launch.Contains("--launchTarget") && launch.Contains("forgeclient") && launch.Contains("--add-opens") && !launch.Contains("${") && !launch.Contains("onlineMode=false") && !launch.Contains("online-mode=false"),"complete Forge launch command retains bootstrap parameters without forcing server authentication off");
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
                var cachedSettingsContent=settingsPage.Content;
                window.Navigate("Dash"); window.Navigate("Settings"); Flush(window);
                Check(ReferenceEquals(settingsPage.Content,cachedSettingsContent),"cached settings navigation reuses visual tree: "+code);
                var settingsScroll=((DockPanel)settingsPage.Content).Children.OfType<ScrollViewer>().Single();
                var player=Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="PlayerNameBox");
                var memory=Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="MemoryBox");
                player.Text="PendingName"; memory.Text="invalid";
                window.SwitchLanguage(code=="tr"?"en":"tr"); Flush(window);
                Check(!ReferenceEquals(settingsPage.Content,cachedSettingsContent),"language change rebuilds cached settings: "+code);
                Check(Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="PlayerNameBox").Text=="PendingName" && Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="MemoryBox").Text=="invalid","language switching preserves unsaved form values: "+code);
                window.SwitchLanguage(code); Flush(window);
                settingsScroll=((DockPanel)settingsPage.Content).Children.OfType<ScrollViewer>().Single();
                Nodes(settingsPage).OfType<Button>().Single(button=>button.Name=="SaveSettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
                var feedback=Nodes(settingsPage).OfType<TextBlock>().Single(text=>text.Name=="SettingsResult");
                Check(feedback.Text==Localization.T("invalid") && !Nodes(settingsScroll).Contains(feedback),"validation feedback remains in fixed footer: "+code);
                Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="PlayerNameBox").Text=window.Config.User;
                Nodes(settingsPage).OfType<TextBox>().Single(box=>box.Name=="MemoryBox").Text=window.Config.Ram.ToString();
                feedback.Text="";
                var lighting=Nodes(settingsPage).OfType<ComboBox>().Single(box=>box.Name=="CloseLightingBox");
                Check(!Texts(settingsPage).Contains(Localization.T("cloudTitle")) && !Nodes(settingsPage).OfType<PasswordBox>().Any(),"cloud account UI removed: "+code);
                Check(lighting.Items.Count==3 && !lighting.Items.Cast<string>().Any(item=>item.Contains("Rainbow") || item.Contains("Gökkuşağı")),"single RGB mode replaces Rainbow: "+code);
                lighting.SelectedIndex=2; Flush(window); Localization.TranslateTree(settingsPage);
                lighting.SelectedIndex=1; Flush(window); Localization.TranslateTree(settingsPage); Flush(window);
                Check(Texts(lighting).Any(text=>text.StartsWith("RGB")) && window.Config.CloseLighting=="RGB","lighting selector displays new value after translation: "+code);
                var closeFrame=((StackPanel)window.FindName("CaptionButtons")).Children.OfType<Button>().Where(CaptionHasGlow).Select(button=>(Border)button.Content).Single();
                var circle=((Grid)closeFrame.Child).Children.OfType<Border>().Single();
                var cross=((Grid)closeFrame.Child).Children.OfType<System.Windows.Shapes.Path>().Single();
                Check(circle.Width==38 && circle.Height==24 && circle.CornerRadius.TopLeft==5 && cross.HorizontalAlignment==HorizontalAlignment.Center && cross.VerticalAlignment==VerticalAlignment.Center && ((SolidColorBrush)circle.BorderBrush).HasAnimatedProperties && circle.Effect.HasAnimatedProperties && ConfigManager.Load().CloseLighting=="RGB","centered RGB outline animates independently of Windows animation preference and persists: "+code);
                var before=((SolidColorBrush)circle.BorderBrush).Color;
                var animationFrame=new DispatcherFrame();
                var animationTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(350) };
                animationTimer.Tick+=(_,_)=> { animationTimer.Stop(); animationFrame.Continue=false; };
                animationTimer.Start(); Dispatcher.PushFrame(animationFrame);
                Check(((SolidColorBrush)circle.BorderBrush).Color!=before && ((System.Windows.Media.Effects.DropShadowEffect)circle.Effect).Color==((SolidColorBrush)circle.BorderBrush).Color,"RGB motion stays active and glow stays synchronized: "+code);
                lighting.SelectedIndex=2; Flush(window);
                Check(((StackPanel)window.FindName("CaptionButtons")).Children.OfType<Button>().All(button=>!CaptionHasGlow(button)),"Off disables caption lighting: "+code);
                lighting.SelectedIndex=1; Flush(window);
                var styleHeading=Nodes(settingsPage).OfType<TextBlock>().Single(text=>text.Text==Localization.T("windowStyleTitle"));
                settingsScroll.ScrollToVerticalOffset(styleHeading.TranslatePoint(new Point(0,0),(UIElement)settingsScroll.Content).Y-20); Capture(window,Path.Combine(output,"window-styles-"+code+".png"));
                settingsScroll.ScrollToTop();
                window.Navigate("Server"); Capture(window,Path.Combine(output,"server-"+code+".png"));
                var status=(TextBlock)window.FindName("StatusLbl"); var previousStatus=status.Text; status.Text=new string('W',250);
                window.Navigate("Dash"); Capture(window,Path.Combine(output,"home-compact-"+code+".png"),960,640);
                var root=(FrameworkElement)window.Content;
                var selector=(ComboBox)window.FindName("VerBox");
                Check(status.TranslatePoint(new Point(status.ActualWidth,0),root).X<=selector.TranslatePoint(new Point(0,0),root).X,"long status cannot overlap launch controls at compact width: "+code);
                var captionTitle=(TextBlock)window.FindName("CaptionTitle");
                Check(Math.Abs(captionTitle.TranslatePoint(new Point(captionTitle.ActualWidth/2,0),root).X-root.ActualWidth/2)<1,"caption title remains centered regardless of button position: "+code);
                status.Text=previousStatus; window.Navigate("Settings"); Capture(window,Path.Combine(output,"settings-compact-"+code+".png"),960,640);
                window.Navigate("Vers"); Capture(window,Path.Combine(output,"versions-"+code+".png"));
                window.Navigate("Mods"); Capture(window,Path.Combine(output,"mods-"+code+".png"),960,640);
                var currentModPage=(DependencyObject)((Frame)window.FindName("MainFrame")).Content;
                Check(Texts(currentModPage).Contains(Localization.T("modCenterTitle")),"mod center opens in selected language: "+code);
                window.SwitchLanguage(code=="tr"?"en":"tr"); Flush(window);
                Check(Texts(currentModPage).Contains(Localization.T("modCenterTitle")),"cached mod center headings follow language switch: "+code);
                window.SwitchLanguage(code); Flush(window);
                window.Navigate("Skin"); Capture(window,Path.Combine(output,"skin-"+code+".png"),960,640);
                Check(Texts((DependencyObject)((Frame)window.FindName("MainFrame")).Content).Contains(Localization.T("skinStudioTitle")),"cached character studio opens in selected language: "+code);
                window.Navigate("Opt"); Capture(window,Path.Combine(output,"performance-"+code+".png"),960,640);
                Check(Texts((DependencyObject)((Frame)window.FindName("MainFrame")).Content).Contains(Localization.T("optTitle")),"optimization page opens in selected language: "+code);
            }
            var horizontal=new System.Windows.Controls.Primitives.ScrollBar { Orientation=Orientation.Horizontal,Style=(Style)application.FindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) };
            horizontal.ApplyTemplate();
            var track=(System.Windows.Controls.Primitives.Track)horizontal.Template.FindName("PART_Track",horizontal);
            Check(double.IsNaN(horizontal.Width) && horizontal.Height==12 && track.DecreaseRepeatButton.Command==System.Windows.Controls.Primitives.ScrollBar.PageLeftCommand && track.IncreaseRepeatButton.Command==System.Windows.Controls.Primitives.ScrollBar.PageRightCommand,"horizontal scrollbar preserves width and page commands");
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
    static bool CaptionHasGlow(Button button) => ((Border)button.Content).Effect!=null || ((Border)button.Content).Child is Grid grid && grid.Children.OfType<Border>().Any(outline=>outline.Effect!=null);
    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            foreach(var child in Nodes(VisualTreeHelper.GetChild(node,i))) yield return child;
    }
    static void Capture(Window window,string path,int width=1200,int height=820)
    {
        Flush(window);
        var root=(FrameworkElement)window.Content;
        root.Measure(new Size(width,height)); root.Arrange(new Rect(0,0,width,height)); root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
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


