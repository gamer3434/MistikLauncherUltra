using System.Reflection;
using System.IO;
using System.Windows.Controls;
using MistikLauncher;

static class LaunchLifecycleTests
{
    public static int Run(MainWindow window)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var selected=(ComboBox)typeof(MainWindow).GetField("VerBox",flags)!.GetValue(window)!;
        var handler=typeof(MainWindow).GetMethod("HandleLaunch",flags)!;
        var original=window.Config.Version;
        var previous=selected.SelectedItem;
        int checks=0;
        try
        {
            foreach(var state in new[]{"launchPreparing","gameRunning"})
            {
                var field=typeof(MainWindow).GetField(state,flags)!;
                field.SetValue(window,true);
                try
                {
                    handler.Invoke(window,null);
                    if(window.Config.Version!=original || !(bool)field.GetValue(window)!) throw new Exception("Repeated launch altered an active launch.");
                    Console.WriteLine("PASS repeated launch is ignored while "+state); checks++;
                }
                finally { field.SetValue(window,false); }
            }
            bool started=false;
            try { window.WithSyncedMods(original+"-changed",_=>started=true); }
            catch(IOException) { }
            if(started) throw new Exception("A changed profile reached process start.");
            Console.WriteLine("PASS changed profile cannot reach game process start"); checks++;
            var serverPage=new MistikLauncher.Pages.ServerManagerPage(window);
            var serverType=serverPage.GetType();
            serverType.GetField("launching",flags)!.SetValue(serverPage,true);
            ((Task)serverType.GetMethod("Launch",flags)!.Invoke(serverPage,null)!).GetAwaiter().GetResult();
            serverType.GetMethod("Refresh",flags)!.Invoke(serverPage,null);
            if(((Button)serverType.GetField("launch",flags)!.GetValue(serverPage)!).IsEnabled || ((Button)serverType.GetField("update",flags)!.GetValue(serverPage)!).IsEnabled) throw new Exception("Refresh enabled a second server manager launch.");
            Console.WriteLine("PASS server manager refresh preserves in-flight launch and update guards"); checks++;
        }
        finally { selected.SelectedItem=previous; }
        return checks;
    }
}
