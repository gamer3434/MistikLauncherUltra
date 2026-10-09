using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class SettingsLifecycleTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var updater=window.LauncherUpdates;
        var originalConfig=window.Config;
        var originalAutoUpdate=originalConfig.LauncherAutoUpdate;
        var originalCurseForgeKey=originalConfig.CurseForgeApiKey;
        var originalLanguage=Localization.Language;
        var syncGate=typeof(MainWindow).GetField("_modSyncGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        var configurationGate=typeof(MainWindow).GetProperty("ConfigurationGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        var busy=updater.Busy; var status=updater.StatusKey;
        var progress=updater.Progress; var error=updater.Error;
        void State(bool running,string key)
        {
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Busy))!.SetValue(updater,running);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.StatusKey))!.SetValue(updater,key);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Error))!.SetValue(updater,null);
        }
        T Control<T>(ModernSettingsPage page,string name)=>(T)typeof(ModernSettingsPage).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        try
        {
            const string savedFixtureKey="$2a$10$settings_saved_fixture_key_0123456789012345678901234567";
            const string pendingFixtureKey="$2a$10$settings_pending_fixture_key_01234567890123456789012345";
            window.Config.CurseForgeApiKey=savedFixtureKey;
            ConfigManager.Save(window.Config);
            Check(ReferenceEquals(configurationGate,syncGate),"settings persistence shares the existing mod synchronization gate");
            foreach(var updatesOnly in new[]{false,true})
            {
                State(true,"luChecking");
                var page=new ModernSettingsPage(window,updatesOnly);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                State(false,"luCurrent");
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    if(!Control<Button>(page,"updateButton").IsEnabled || Control<ProgressBar>(page,"updateProgress").Visibility!=Visibility.Collapsed || !Control<TextBlock>(page,"updateStatus").Text.StartsWith(Localization.T("luCurrent")))
                        throw new Exception("Cached update page did not refresh after a hidden check completed.");
                    Console.WriteLine("PASS cached "+(updatesOnly?"updates":"settings")+" page refreshes completed check on load"); checks++;
                }
                finally { page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }

            var settings=new ModernSettingsPage(window);
            var updates=new ModernSettingsPage(window,updatesOnly:true);
            var user=Control<TextBox>(settings,"user"); var memory=Control<TextBox>(settings,"ram");
            user.Text="PendingName"; memory.Text="7";
            Control<PasswordBox>(settings,"curseForgeKey").Password=pendingFixtureKey;
            Check(!Control<Expander>(settings,"integrations").IsExpanded && Control<PasswordBox>(updates,"curseForgeKey")==null,"CurseForge key remains masked inside optional settings integration and is absent from updates-only page");
            var cachedContent=settings.Content;
            var updatesAutomatic=Control<CheckBox>(updates,"autoUpdate");
            updatesAutomatic.IsChecked=!originalAutoUpdate;
            bool automaticSavedUnderGate=false;
            void ObserveAutomaticSave(LauncherConfig _) => automaticSavedUnderGate=System.Threading.Monitor.IsEntered(syncGate);
            ConfigManager.Saved+=ObserveAutomaticSave;
            try { updatesAutomatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { ConfigManager.Saved-=ObserveAutomaticSave; }
            Check(automaticSavedUnderGate,"automatic update preference is persisted under the mod synchronization gate");
            settings.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                Check(Control<CheckBox>(settings,"autoUpdate").IsChecked==window.Config.LauncherAutoUpdate && ConfigManager.Load().LauncherAutoUpdate==!originalAutoUpdate,"cached settings reflects automatic updates changed from the updates page");
                Check(ReferenceEquals(settings.Content,cachedContent) && user.Text=="PendingName" && memory.Text=="7","refreshing shared automatic update preference preserves unsaved settings drafts");
            }
            finally { settings.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            Control<CheckBox>(settings,"autoUpdate").IsChecked=originalAutoUpdate;
            Control<CheckBox>(settings,"autoUpdate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            updates.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try { Check(Control<CheckBox>(updates,"autoUpdate").IsChecked==originalAutoUpdate,"cached updates reflects automatic updates changed from the settings page"); }
            finally { updates.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }

            var previous=(window.Config.User,window.Config.Ram,window.Config.AuthType,window.Config.AutoClose,window.Config.Lang);
            var configPath=Path.Combine(App.AppData,"config.json");
            var savedBytes=File.ReadAllBytes(configPath);
            Control<ComboBox>(settings,"provider").SelectedIndex=previous.AuthType=="elyby"?0:1;
            Control<CheckBox>(settings,"close").IsChecked=!previous.AutoClose;
            Control<ComboBox>(settings,"language").SelectedIndex=previous.Lang=="English"?0:1;
            using(var locked=new FileStream(configPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                foreach(var page in new[]{settings,updates})
                {
                    var automatic=Control<CheckBox>(page,"autoUpdate");
                    automatic.IsChecked=!originalAutoUpdate;
                    automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(window.Config.LauncherAutoUpdate==originalAutoUpdate && automatic.IsChecked==originalAutoUpdate && Control<TextBlock>(page,"updateStatus").Text.StartsWith(Localization.T("error")),"locked automatic update preference rolls back config and checkbox with visible error: "+(ReferenceEquals(page,settings)?"settings":"updates"));
                }
                Check(user.Text=="PendingName" && memory.Text=="7" && File.ReadAllBytes(configPath).SequenceEqual(savedBytes),"failed automatic update clicks preserve the draft and persisted preferences without escaping the event handler");
                typeof(ModernSettingsPage).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settings,null);
            }
            Check(ReferenceEquals(window.Config,originalConfig) && (window.Config.User,window.Config.Ram,window.Config.AuthType,window.Config.AutoClose,window.Config.Lang)==previous && File.ReadAllBytes(configPath).SequenceEqual(savedBytes),"failed settings persistence preserves the active player profile and saved preferences");
            Check(user.Text=="PendingName" && memory.Text=="7" && Control<TextBlock>(settings,"result").Text.StartsWith(Localization.T("error")),"failed settings save retains the draft and displays an error for retry");
            Check(window.Config.CurseForgeApiKey==savedFixtureKey && ConfigManager.Load().CurseForgeApiKey==savedFixtureKey && Control<PasswordBox>(settings,"curseForgeKey").Password==pendingFixtureKey,"locked settings save preserves the current CurseForge key and the pending masked draft");
            Control<Expander>(settings,"integrations").IsExpanded=true;
            settings.RefreshLanguage();
            user=Control<TextBox>(settings,"user"); memory=Control<TextBox>(settings,"ram");
            Check(Control<PasswordBox>(settings,"curseForgeKey").Password==pendingFixtureKey && Control<Expander>(settings,"integrations").IsExpanded,"settings language refresh preserves the pending CurseForge key and integration expansion");
            user.Text=previous.User; memory.Text=previous.Ram.ToString();
            foreach(var invalidKey in new[]{"fixture\r\nheader", "fixture\u0131key"})
            {
                Control<PasswordBox>(settings,"curseForgeKey").Password=invalidKey;
                typeof(ModernSettingsPage).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settings,null);
                Check(window.Config.CurseForgeApiKey==savedFixtureKey && File.ReadAllBytes(configPath).SequenceEqual(savedBytes) && Control<TextBlock>(settings,"result").Text==Localization.T("curseForgeKeyInvalid"),"invalid CurseForge API key is rejected visibly without altering saved settings");
            }
            Control<PasswordBox>(settings,"curseForgeKey").Password=" "+pendingFixtureKey+" ";

            var skin=Path.Combine(App.AppData,"settings-avatar-fixture.png");
            var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,new byte[64*64*4],64*4);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(skin)) encoder.Save(stream);
            var localConfig=Newtonsoft.Json.JsonConvert.DeserializeObject<LauncherConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(originalConfig))!;
            localConfig.SkinType="local"; localConfig.SkinUser=skin; window.Config=localConfig;
            const string synchronizedProfile="settings-synchronized-profile";
            Task.Run(()=> { lock(syncGate) window.Config.LastSyncedVersion=synchronizedProfile; }).GetAwaiter().GetResult();
            user.Text=previous.User; memory.Text=previous.Ram.ToString();
            var targetLanguage=originalLanguage=="en"?"Turkce":"English";
            Control<ComboBox>(settings,"language").SelectedIndex=targetLanguage=="English"?1:0;
            FileStream? savedLock=null; int saveNotifications=0;
            bool settingsSavedUnderGate=false; string? notifiedSyncedProfile=null;
            void LockAfterSave(LauncherConfig saved) {
                saveNotifications++;
                settingsSavedUnderGate=System.Threading.Monitor.IsEntered(syncGate);
                notifiedSyncedProfile=saved.LastSyncedVersion;
                savedLock=new FileStream(configPath,FileMode.Open,FileAccess.Read,FileShare.Read);
            }
            ConfigManager.Saved+=LockAfterSave;
            try
            {
                typeof(ModernSettingsPage).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settings,null);
                var savedLanguage=ConfigManager.Load().Lang;
                var feedback=Control<TextBlock>(settings,"result").Text;
                Check(window.Config.CurseForgeApiKey==pendingFixtureKey && ConfigManager.Load().CurseForgeApiKey==pendingFixtureKey,"masked CurseForge API key trims and round-trips through settings persistence");
                bool ContainsFixtureSecret(string path)=>new[]{savedFixtureKey,pendingFixtureKey}.Any(key=>File.ReadAllBytes(path).AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(key))>=0);
                Check(!ContainsFixtureSecret(configPath) && !ContainsFixtureSecret(configPath+".bak"),"primary and backup settings contain no plaintext CurseForge API key");
                Check(settingsSavedUnderGate && notifiedSyncedProfile==synchronizedProfile && window.Config.LastSyncedVersion==synchronizedProfile && ConfigManager.Load().LastSyncedVersion==synchronizedProfile,"settings snapshot and persistence retain completed mod synchronization under the shared gate");
                Check(saveNotifications==1 && window.Config.Lang==targetLanguage && Localization.Language==(targetLanguage=="English"?"en":"tr") && savedLanguage==targetLanguage && feedback==Localization.T("saved"),$"successful settings language save updates the interface without a second write after reload (notifications={saveNotifications}, target={targetLanguage}, runtime={window.Config.Lang}, disk={savedLanguage}, UI={Localization.Language}, result={feedback})");
            }
            finally { ConfigManager.Saved-=LockAfterSave; savedLock?.Dispose(); }
        }
        finally
        {
            window.Config=originalConfig;
            window.Config.LauncherAutoUpdate=originalAutoUpdate;
            window.Config.CurseForgeApiKey=originalCurseForgeKey;
            ConfigManager.Save(window.Config);
            Localization.SetLanguage(originalLanguage);
            var temporaryConfig=Path.Combine(App.AppData,"config.json.tmp");
            if(File.Exists(temporaryConfig)) File.Delete(temporaryConfig);
            State(busy,status);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Progress))!.SetValue(updater,progress);
            typeof(LauncherUpdater).GetProperty(nameof(LauncherUpdater.Error))!.SetValue(updater,error);
        }
        return checks;
    }
}
