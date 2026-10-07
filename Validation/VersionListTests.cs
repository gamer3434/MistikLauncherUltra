using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Net;
using System.Net.Http;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;
using Localization = MistikLauncher.Localization;

static class VersionListTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var blue=PageHelpers.MkBtn("Blue","#226DA0"); var red=PageHelpers.MkBtn("Red","#FF4B4B");
        Check(ReferenceEquals(blue.Template,red.Template) && ((System.Windows.Media.SolidColorBrush)blue.Background).Color!=((System.Windows.Media.SolidColorBrush)red.Background).Color,"buttons reuse rounded template while retaining individual backgrounds");
        var catalog=typeof(VersionManagerPage).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(field=>field.FieldType==typeof(JArray));
        var previous=catalog.GetValue(null);
        var previousVersion=window.Config.Version;
        var previousLanguage=Localization.Language;
        try
        {
            catalog.SetValue(null,new JArray(Enumerable.Range(1,95).Select(index=>new JObject { ["id"]="batch-snapshot-"+index,["type"]="snapshot" })));
            var page=new VersionManagerPage(window);
            var cards=Nodes(page).OfType<StackPanel>().Single(panel=>panel.Name=="VersionListPanel");
            Button? More()=>Nodes(page).OfType<Button>().SingleOrDefault(button=>button.Name=="VersionLoadMore");
            string[] Versions()=>Nodes(page).OfType<TextBlock>().Select(text=>text.Text).Where(text=>text.StartsWith("batch-snapshot-",StringComparison.Ordinal)).ToArray();
            void Filter(string title)=>Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,title)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(More()!=null && cards.Children.OfType<Border>().Count()==40,"large version catalog initially creates forty cards across all loaders");
            Filter("Snapshot");
            Check(Versions().Length==40,"snapshot filter resets version batch to forty");
            var snapshotFilter=Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,"Snapshot"));
            var allFilter=Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("Hepsi")));
            Check(((System.Windows.Media.SolidColorBrush)snapshotFilter.Background).Color==PageHelpers.HexBrush("#00A3FF").Color && ((System.Windows.Media.SolidColorBrush)allFilter.Background).Color==PageHelpers.HexBrush("#333333").Color && ReferenceEquals(allFilter.Foreground,System.Windows.Media.Brushes.White),"version filter highlight follows selected results with readable inactive text");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==80,"show more adds one forty-version batch");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==95 && Versions().Distinct().Count()==95 && More()==null,"all catalog versions remain reachable without duplicates");
            Filter("Vanilla"); Filter("Snapshot");
            Check(Versions().Length==40 && More()!=null,"changing filters resets expanded version list");
            Localization.TranslateTree(page);
            Check(Equals(More()!.Content,Localization.T("versionLoadMore")),"version batch action uses selected locale");
            foreach(var id in new[]{"batch-selection-first","batch-selection-second"})
            {
                var directory=System.IO.Path.Combine(App.GameDir,"versions",id); System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".json"),new JObject { ["id"]=id }.ToString());
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".jar"),"fixture");
            }
            window.Config.Version="batch-selection-first"; Filter("Vanilla");
            Button Selection(string id)=>Nodes(cards.Children.OfType<Border>().Single(card=>Nodes(card).OfType<TextBlock>().Any(text=>text.Text==id))).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("SEC")) || Equals(button.Content,Localization.T("SECILDI")));
            Check(!Selection("batch-selection-first").IsEnabled && Selection("batch-selection-second").IsEnabled,"version cards identify current installed profile");
            window.Config.Version="batch-selection-second"; page.RefreshSelection();
            Check(Selection("batch-selection-first").IsEnabled && !Selection("batch-selection-second").IsEnabled,"visible version selection refreshes after global profile change");
            window.Config.Version="batch-selection-first"; page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(!Selection("batch-selection-first").IsEnabled && Selection("batch-selection-second").IsEnabled,"cached version page refreshes stale selection on load");
            var unchanged=cards.Children[0]; page.RefreshSelection();
            Check(ReferenceEquals(unchanged,cards.Children[0]),"unchanged version selection retains cached cards");
            const string fabricId="fabric-loader-0.16.10-1.20.1";
            foreach(var id in new[]{"1.20.1",fabricId})
            {
                var directory=System.IO.Path.Combine(App.GameDir,"versions",id); System.IO.Directory.CreateDirectory(directory);
                var profile=new JObject { ["id"]=id };
                if(id==fabricId) { profile["inheritsFrom"]="1.20.1"; profile["libraries"]=new JArray(new JObject { ["name"]="net.fabricmc:fabric-loader:0.16.10" }); }
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".json"),profile.ToString());
                if(id!=fabricId) System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".jar"),"fixture");
            }
            Filter("Fabric");
            Check(Nodes(cards).OfType<TextBlock>().Count(label=>label.Text==fabricId)==1 && !Nodes(cards).OfType<TextBlock>().Any(label=>label.Text=="fabric-1.20.1"),"installed Fabric profile appears once without its duplicate catalog alias");
            Localization.SetLanguage("en"); Filter("Snapshot");
            Check(Nodes(cards).OfType<Button>().Where(button=>button.Name!="VersionLoadMore").All(button=>Equals(button.Content,Localization.T("INDIR"))),"rebuilt version cards retain English action labels");

            var load=typeof(VersionManagerPage).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(method=>method.ReturnType==typeof(Task) && method.GetParameters().Select(parameter=>parameter.ParameterType).SequenceEqual(new[]{typeof(bool),typeof(HttpClient)}));
            Task Load(VersionManagerPage target,bool force,HttpClient client)=>(Task)load.Invoke(target,new object[]{force,client})!;
            var handler=new CatalogHandler(); using var client=new HttpClient(handler);
            var otherPage=new VersionManagerPage(window);
            Nodes(otherPage).OfType<Button>().Single(button=>Equals(button.Content,"Snapshot")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var refresh=Load(page,true,client);
            var waiting=Load(otherPage,false,client);
            Check(!waiting.IsCompleted && handler.Requests==1,"a second version page waits for in-flight catalog refresh");
            handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"versions\":[{\"id\":\"catalog-refreshed\",\"type\":\"snapshot\"}]}") });
            Wait(Task.WhenAll(refresh,waiting));
            Check(Nodes(page).OfType<TextBlock>().Any(label=>label.Text=="catalog-refreshed") && Nodes(otherPage).OfType<TextBlock>().Any(label=>label.Text=="catalog-refreshed") && handler.Requests==1,"all waiting version pages display the shared refreshed catalog");
            var unavailable=new CatalogHandler(); using var offlineClient=new HttpClient(unavailable);
            var failedRefresh=Load(page,true,offlineClient);
            unavailable.Response.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            Wait(failedRefresh);
            Check(Nodes(page).OfType<TextBlock>().Any(label=>label.Text=="catalog-refreshed"),"offline refresh preserves the working in-memory version catalog");
        }
        finally { catalog.SetValue(null,previous); window.Config.Version=previousVersion; Localization.SetLanguage(previousLanguage); }
        return checks;
    }
    static void Wait(Task task)
    {
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame();
            var dispatcher=Dispatcher.CurrentDispatcher;
            _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    sealed class CatalogHandler : HttpMessageHandler
    {
        public int Requests;
        public readonly TaskCompletionSource<HttpResponseMessage> Response=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) { Requests++; return Response.Task; }
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
}
