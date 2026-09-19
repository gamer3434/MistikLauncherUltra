using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MistikLauncher.Pages;
public sealed class ServerManagerPage : Page, ILanguagePage
{
    readonly MainWindow main;
    readonly AutoMcsUpdater updater;
    TextBlock status=null!, installed=null!, latest=null!;
    Button launch=null!, update=null!;
    ProgressBar progress=null!;
    public ServerManagerPage(MainWindow window)
    {
        main=window; updater=window.AutoMcs;
        Loaded += (_,_) => { Localization.Changed+=Render; updater.Changed+=RefreshAsync; Refresh(); };
        Unloaded += (_,_) => { Localization.Changed-=Render; updater.Changed-=RefreshAsync; };
        Render();
    }
    public void RefreshLanguage() => Render();
    void Render()
    {
        Background=PageHelpers.HexBrush("#142333");
        var stack=new StackPanel { Margin=new Thickness(36), MaxWidth=920 };
        stack.Children.Add(PageHelpers.Lbl(Localization.T("mcsTitle"),30,"#FFFFFF",true));
        stack.Children.Add(PageHelpers.Lbl(Localization.T("mcsIntro"),15,"#BDCAD8",pad:new Thickness(0,10,0,24),wrap:TextWrapping.Wrap));
        var panel=new Border { Background=PageHelpers.HexBrush("#20364B"), CornerRadius=new CornerRadius(16), Padding=new Thickness(24) };
        var inner=new StackPanel();
        inner.Children.Add(PageHelpers.Lbl("Auto-MCS",24,"#FFFFFF",true));
        inner.Children.Add(PageHelpers.Lbl(Localization.T("mcsOfficial"),13,"#BDCAD8",pad:new Thickness(0,4,0,20)));
        var versions=new System.Windows.Controls.Primitives.UniformGrid { Columns=2 };
        StackPanel Version(string key,out TextBlock value) {
            var section=new StackPanel { Margin=new Thickness(0,0,12,20) };
            section.Children.Add(PageHelpers.Lbl(Localization.T(key),13,"#BDCAD8"));
            value=PageHelpers.Lbl("—",22,"#FFFFFF",true,pad:new Thickness(0,6,0,0)); section.Children.Add(value); return section;
        }
        versions.Children.Add(Version("mcsInstalled",out installed)); versions.Children.Add(Version("mcsLatest",out latest)); inner.Children.Add(versions);
        var actions=new WrapPanel();
        launch=PageHelpers.MkBtn(Localization.T("mcsLaunch"),"#226DA0"); launch.Margin=new Thickness(0,0,12,12);
        launch.Click += async (_,_) => await Launch(); actions.Children.Add(launch);
        update=PageHelpers.MkBtn(Localization.T("mcsUpdate"),"#35546E"); update.Margin=new Thickness(0,0,12,12);
        update.Click += async (_,_) => await updater.CheckAsync(true,true); actions.Children.Add(update);
        var release=PageHelpers.MkBtn(Localization.T("mcsReleaseNotes"),"#35546E"); release.Margin=new Thickness(0,0,0,12);
        release.Click+=(_,_)=>Open(AutoMcsUpdater.Repository+"/releases"); actions.Children.Add(release); inner.Children.Add(actions);
        progress=new ProgressBar { Height=6, Maximum=100, Foreground=PageHelpers.HexBrush("#65C6E8"), Background=PageHelpers.HexBrush("#142333"), BorderThickness=new Thickness(0), Margin=new Thickness(0,12,0,10) }; inner.Children.Add(progress);
        status=PageHelpers.Lbl("",14,"#CCE1EF",wrap:TextWrapping.Wrap); inner.Children.Add(status); panel.Child=inner; stack.Children.Add(panel);
        var automatic=new CheckBox { Content=Localization.T("mcsAutomatic"), IsChecked=main.Config.AutoMcsAutoUpdate, Foreground=Brushes.White, Margin=new Thickness(0,24,0,8) };
        automatic.Click+=(_,_)=> {
            main.Config.AutoMcsAutoUpdate=automatic.IsChecked==true;
            try { ConfigManager.Save(main.Config); } catch(Exception ex) { status.Text=Localization.T("mcsError")+": "+ex.Message; }
        }; stack.Children.Add(automatic);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("mcsSafeUpdate"),13,"#BDCAD8",wrap:TextWrapping.Wrap));
        stack.Children.Add(PageHelpers.Lbl(Localization.T("mcsWorkspace"),20,"#FFFFFF",true,pad:new Thickness(0,28,0,12)));
        foreach(var entry in new[] { ("mcsCreate","mcsCreateHelp"),("mcsManage","mcsManageHelp"),("mcsBackup","mcsBackupHelp") })
        {
            var row=new Grid { Margin=new Thickness(0,0,0,16) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(170) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var heading=PageHelpers.Lbl(Localization.T(entry.Item1),15,"#FFFFFF",true); var description=PageHelpers.Lbl(Localization.T(entry.Item2),14,"#BDCAD8",wrap:TextWrapping.Wrap);
            Grid.SetColumn(description,1); row.Children.Add(heading); row.Children.Add(description); stack.Children.Add(row);
        }
        Content=new ScrollViewer { Content=stack, VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Refresh();
    }
    void RefreshAsync() { if(!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(Refresh)); }
    void Refresh()
    {
        installed.Text=updater.InstalledVersion=="?"?Localization.T("mcsUnknown"):updater.InstalledVersion;
        latest.Text=updater.LatestVersion;
        var transfer="";
        if(updater.StatusKey=="mcsDownloading" && updater.TotalBytes>0)
        {
            var speed=updater.DownloadSpeedBytesPerSecond>0?FormatBytes((long)updater.DownloadSpeedBytesPerSecond)+"/s":"—";
            var eta=FormatEta(updater.RemainingTime);
            transfer=$"\n{Localization.T("mcsDownloaded")}: {FormatBytes(updater.DownloadedBytes)} / {FormatBytes(updater.TotalBytes)} · {Localization.T("mcsSpeed")}: {speed} · {Localization.T("mcsRemaining")}: {eta}";
        }
        status.Text=Localization.T(updater.StatusKey)+transfer+(updater.Error==null?"":"\n"+updater.Error);
        status.Foreground=PageHelpers.HexBrush(updater.Error==null?"#CCE1EF":"#F3BDA3");
        progress.Value=updater.Progress; progress.Visibility=updater.Busy?Visibility.Visible:Visibility.Collapsed;
        launch.IsEnabled=!updater.Busy; update.IsEnabled=!updater.Busy;
        launch.Content=Localization.T(File.Exists(updater.ExecutablePath)?"mcsLaunch":"mcsInstallLaunch");
    }
    static string FormatBytes(long bytes)
    {
        if(bytes<1024) return $"{bytes} B";
        if(bytes<1024*1024) return $"{bytes/1024d:0.0} KB";
        if(bytes<1024*1024*1024) return $"{bytes/1024d/1024d:0.0} MB";
        return $"{bytes/1024d/1024d/1024d:0.00} GB";
    }
    static string FormatEta(TimeSpan? eta)
    {
        if(eta==null) return "—";
        var value=eta.Value; return value.TotalHours>=1?$"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}":$"{value.Minutes:00}:{value.Seconds:00}";
    }
    async Task Launch()
    {
        if(updater.IsRunning) { status.Text=Localization.T("mcsAlreadyRunning"); return; }
        launch.IsEnabled=false;
        try
        {
            if(!File.Exists(updater.ExecutablePath) || main.Config.AutoMcsAutoUpdate) await updater.CheckAsync(true);
            if(!File.Exists(updater.ExecutablePath)) { Refresh(); return; }
            Process.Start(new ProcessStartInfo(updater.ExecutablePath) { UseShellExecute=true, WorkingDirectory=Path.GetDirectoryName(updater.ExecutablePath)! });
            status.Text=Localization.T("mcsStarted");
        }
        catch(Exception ex) { status.Text=Localization.T("mcsError")+": "+ex.Message; }
        finally { launch.IsEnabled=!updater.Busy; }
    }
    void Open(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute=true }); } catch(Exception ex) { status.Text=Localization.T("mcsError")+": "+ex.Message; } }
}
