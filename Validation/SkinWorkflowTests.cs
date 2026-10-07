using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class SkinWorkflowTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var original=window.Config;
        try
        {
            var page=new SkinPage(window);
            var nameBox=Nodes(page).OfType<TextBox>().Single(box=>box.Name=="SkinLookupName");
            nameBox.Text="../../invalid";
            var apply=Nodes(page).OfType<Button>().Single(button=>button.Name=="SkinApplyUsername");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ReferenceEquals(original,window.Config) && ConfigManager.Load().User==original.User && apply.IsEnabled && Nodes(page).OfType<TextBlock>().Any(label=>label.Text==Localization.T("usernameHelp")),"invalid skin name shows guidance without changing the saved player profile");

            var pack=Path.Combine(App.GameDir,"resourcepacks","MistikSkinPack");
            Directory.CreateDirectory(pack);
            var previousSkin=Path.Combine(pack,"existing-skin.png");
            File.WriteAllText(previousSkin,"existing skin bytes");
            var options=Path.Combine(App.GameDir,"options.txt");
            const string savedOptions="resourcePacks:[\"vanilla\",\"file/MistikSkinPack\"]\n";
            File.WriteAllText(options,savedOptions);
            var invalidSkin=Path.Combine(App.AppData,"invalid-local-skin.png");
            File.WriteAllText(invalidSkin,"not a PNG");
            window.Config=new LauncherConfig { SkinType="local",SkinUser=invalidSkin };
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1")) && File.ReadAllText(previousSkin)=="existing skin bytes" && File.ReadAllText(options)==savedOptions,"corrupt local skin leaves the existing skin pack and game options intact");
            window.Config=new LauncherConfig { SkinType="username",SkinUser="../../invalid" };
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1")) && File.ReadAllText(previousSkin)=="existing skin bytes","skin preparation rejects invalid names before cache or network access");
        }
        finally { window.Config=original; }
        return checks;
    }
    static bool Wait(Task<bool> task)
    {
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame(); var dispatcher=Dispatcher.CurrentDispatcher;
            _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        return task.GetAwaiter().GetResult();
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
}
