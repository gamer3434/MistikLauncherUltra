using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;
using Localization = MistikLauncher.Localization;

static class ModPagePerformanceTests
{
    public static int Run(MainWindow window,string root)
    {
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try { return RunOnDispatcher(window,root); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    static int RunOnDispatcher(MainWindow window,string root)
    {
        Directory.CreateDirectory(root); int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        byte[] jar;
        using(var memory=new MemoryStream())
        {
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
            { using var writer=new StreamWriter(archive.CreateEntry("fabric.mod.json").Open()); writer.Write("{\"id\":\"fixture\",\"version\":\"1\"}"); }
            jar=memory.ToArray();
        }
        for(int i=0;i<95;i++) File.WriteAllBytes(Path.Combine(root,$"mod-{i:0000}.jar"),jar);
        using var transport=new SearchTransport(); using var client=new HttpClient(transport);
        var page=new ModManagerPage(window,client);
        var fields=typeof(ModManagerPage);
        var installed=(StackPanel)fields.GetField("_installedPanel",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        var results=(StackPanel)fields.GetField("_resultsPanel",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        var search=(TextBox)fields.GetField("_searchBox",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        var render=fields.GetMethod("RenderInstalledMods",BindingFlags.Instance|BindingFlags.NonPublic)!;
        void Render()=>render.Invoke(page,new object?[]{root});
        Button? More()=>Nodes(installed).OfType<Button>().SingleOrDefault(button=>button.Name=="ModLoadMore");
        int Cards()=>installed.Children.OfType<Border>().Count();
        Render();
        Check(Cards()==40 && More()!=null,"large installed mod list creates only the first forty cards");
        More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Cards()==80 && More()!=null,"installed mod load-more expands by one forty-card batch");
        var toggle=Nodes(installed.Children.OfType<Border>().First()).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("modDisable")));
        var gate=typeof(MainWindow).GetField("_modSyncGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        using var gateHeld=new ManualResetEventSlim(); using var releaseGate=new ManualResetEventSlim();
        var competingWork=Task.Run(()=> { lock(gate) { gateHeld.Set(); releaseGate.Wait(TimeSpan.FromSeconds(10)); } });
        if(!gateHeld.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Could not hold isolated mod gate.");
        try {
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!competingWork.IsCompleted && !toggle.IsEnabled,"mod toggle yields without blocking the dispatcher while another mod operation owns the gate");
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        finally { releaseGate.Set(); competingWork.GetAwaiter().GetResult(); }
        var toggleDeadline=DateTime.UtcNow.AddSeconds(5);
        while(!toggle.IsEnabled && DateTime.UtcNow<toggleDeadline) {
            Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(1);
        }
        Check(toggle.IsEnabled,"queued mod toggle completes and restores its controls after the competing operation releases the gate");
        Check(Cards()==80 && File.ReadAllBytes(Path.Combine(root,"mod-0000.jar.disabled")).SequenceEqual(jar) && !File.Exists(Path.Combine(root,"mod-0000.jar")),
            "mod toggle preserves expanded batch width and exact fixture bytes");
        More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Cards()==95 && More()==null && Nodes(installed).OfType<TextBlock>().Count(text=>text.Text.StartsWith("● mod-",StringComparison.Ordinal) || text.Text.StartsWith("○ mod-",StringComparison.Ordinal))==95,
            "all installed mods remain reachable once without duplicate cards");
        File.Delete(Path.Combine(root,"mod-0001.jar")); Render();
        Check(Cards()==94 && More()==null,"mod removal retains the expanded batch instead of resetting the visible list");

        var searchMethod=fields.GetMethod("SearchMods",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Task Search(string query) { search.Text=query; return (Task)searchMethod.Invoke(page,null)!; }
        bool Result(string title)=>Nodes(results).OfType<TextBlock>().Any(text=>text.Text==title);
        var old=Search("slow"); var latest=Search("fast");
        Check(transport.Pending["slow"].Token.IsCancellationRequested,"new mod search cancels the superseded HTTP request");
        transport.Pending["fast"].Complete("new result"); Wait(latest);
        transport.Pending["slow"].Complete("old result"); Wait(old);
        Check(Result("new result") && !Result("old result"),"late superseded search cannot replace the latest result cards: " + string.Join(" | ",Nodes(results).OfType<TextBlock>().Select(text=>text.Text)));
        var failed=Search("late-failure"); var fresh=Search("fresh");
        transport.Pending["fresh"].Complete("fresh result"); Wait(fresh);
        transport.Pending["late-failure"].Response.SetException(new HttpRequestException("obsolete request failed")); Wait(failed);
        Check(Result("fresh result") && !Nodes(results).OfType<TextBlock>().Any(text=>text.Text.Contains("obsolete request failed",StringComparison.Ordinal)),
            "late superseded search failure cannot replace current results with an error");
        var clearing=Search("clear-pending"); var browseAll=Search(""); Wait(browseAll);
        transport.Pending["clear-pending"].Complete("obsolete cleared result"); Wait(clearing);
        Check(transport.Pending["clear-pending"].Token.IsCancellationRequested &&
            Nodes(results).OfType<TextBlock>().Any(text=>text.Text.Contains("bulunamadı",StringComparison.OrdinalIgnoreCase)) &&
            !Nodes(results).OfType<TextBlock>().Any(text=>text.Text.Contains("obsolete cleared result",StringComparison.Ordinal)),
            "clearing a search browses the unfiltered catalog and ignores obsolete results");
        Check(!Nodes(page).OfType<ComboBox>().Any(box=>box.Name=="ModSource"),"single-source mod search needs no provider selector");
        var oldVersion=window.Config.Version;
        try {
            window.Config.Version="98.1-fabric";
            var filtered=Search("filtered");
            var facets=JArray.Parse(Uri.UnescapeDataString(transport.SearchUris["filtered"].Query.TrimStart('?').Split('&')
                .Single(value=>value.StartsWith("facets=",StringComparison.Ordinal))[7..]));
            var modFacet=facets.OfType<JArray>().SingleOrDefault();
            Check(facets.Count==1 && modFacet?.Count==1 && modFacet[0]!.ToString()=="project_type:mod" &&
                !Uri.UnescapeDataString(transport.SearchUris["filtered"].Query).Contains("versions:",StringComparison.Ordinal) &&
                !Uri.UnescapeDataString(transport.SearchUris["filtered"].Query).Contains("categories:",StringComparison.Ordinal),
                "Modrinth catalog searches mods across all game versions and loaders: " + facets.ToString(Newtonsoft.Json.Formatting.None));
            transport.Pending["filtered"].Complete("filtered result"); Wait(filtered);
            foreach(bool error in new[]{false,true})
            {
                window.Config.Version="98.1-fabric";
                string query=error ? "profile-error" : "profile-result";
                var stale=Search(query);
                window.Config.Version="98.2-neoforge";
                if(error) transport.Pending[query].Response.SetException(new HttpRequestException("obsolete profile failed"));
                else transport.Pending[query].Complete("obsolete profile result");
                Wait(stale);
                Check(Result("current profile result") && !Result("obsolete profile result") &&
                    !Nodes(results).OfType<TextBlock>().Any(text=>text.Text.Contains("obsolete profile failed",StringComparison.Ordinal)) &&
                    !Uri.UnescapeDataString(transport.SearchUris[query].Query).Contains("categories:",StringComparison.Ordinal) &&
                    !Uri.UnescapeDataString(transport.SearchUris[query].Query).Contains("versions:",StringComparison.Ordinal),
                    "profile change refreshes broad catalog results without narrowing away other supported builds or publishing stale "+(error ? "errors" : "results"));
            }
            window.Config.Version="98.1-fabric";
            var loaded=Search("profile-loaded"); transport.Pending["profile-loaded"].Complete("old cached profile result"); Wait(loaded);
            window.Config.Version="98.2-neoforge"; page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Wait(Dispatcher.CurrentDispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle).Task);
            Check(Result("current profile result") && !Result("old cached profile result"),
                "reopening the cached mod page refreshes Modrinth results when the selected profile changed");

            window.Config.Version="98.3";
            var vanilla=Search("filtered-vanilla");
            var vanillaFacets=JArray.Parse(Uri.UnescapeDataString(transport.SearchUris["filtered-vanilla"].Query.TrimStart('?').Split('&')
                .Single(value=>value.StartsWith("facets=",StringComparison.Ordinal))[7..]));
            var vanillaModFacet=vanillaFacets.OfType<JArray>().SingleOrDefault();
            Check(vanillaFacets.Count==1 && vanillaModFacet?.Count==1 && vanillaModFacet[0]!.ToString()=="project_type:mod",
                "vanilla profile can browse mods for every Minecraft version and loader");
            transport.Pending["filtered-vanilla"].Complete("vanilla result"); Wait(vanilla);

            window.Config.Version="98.1-fabric";
            var install=fields.GetMethod("InstallMod",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var runButton=fields.GetMethod("RunInstallButton",BindingFlags.Static|BindingFlags.NonPublic)!;
            var installButton=new Button { Content="KUR" }; int failures=0;
            Func<Task<bool>> realFailure=()=>(Task<bool>)install.Invoke(page,new object?[]{"fixture-failed","Fixture",null})!;
            Task RunButton(Func<Task<bool>> operation)=>(Task)runButton.Invoke(null,new object[]{installButton,operation,
                (Action<Exception>)(error=>{ if(error is not HttpRequestException) throw error; failures++; }),"OK"})!;
            for(int attempt=0;attempt<2;attempt++)
            {
                Wait(RunButton(realFailure));
                Check(installButton.IsEnabled && Equals(installButton.Content,"KUR") && failures==attempt+1,
                    "failed Modrinth metadata installation restores retry instead of a false OK: attempt "+(attempt+1));
            }
            var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var running=RunButton(()=>completion.Task);
            Check(!installButton.IsEnabled && Equals(installButton.Content,"..."),"mod installation disables duplicate clicks while its operation is pending");
            completion.SetResult(false); Wait(running);
            Check(installButton.IsEnabled && Equals(installButton.Content,"KUR"),"queued or skipped mod operation never reports active installation success");
            Wait(RunButton(()=>Task.FromResult(true)));
            Check(installButton.IsEnabled && Equals(installButton.Content,"OK"),"successful mod operation alone produces the installation success label");
        }
        finally { window.Config.Version=oldVersion; }
        return checks;
    }
    static void Wait(Task task)
    {
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame(); var dispatcher=Dispatcher.CurrentDispatcher;
            _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
    sealed class PendingSearch(CancellationToken token)
    {
        public CancellationToken Token=token;
        public readonly TaskCompletionSource<HttpResponseMessage> Response=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(string title)=>Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content=new StringContent(new JObject { ["hits"]=new JArray(new JObject { ["project_id"]="fixture",["title"]=title,["description"]="fixture",["downloads"]=1 }) }.ToString())
        });
    }
    sealed class SearchTransport : HttpMessageHandler
    {
        public readonly Dictionary<string,PendingSearch> Pending=new();
        public readonly Dictionary<string,Uri> SearchUris=new();
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;
            if(request.RequestUri!.AbsolutePath=="/v2/project/fixture-failed/version")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var queryValue=request.RequestUri!.Query.TrimStart('?').Split('&').FirstOrDefault(value=>value.StartsWith("query=",StringComparison.Ordinal));
            string query=queryValue is null ? "" : Uri.UnescapeDataString(queryValue[6..]);
            SearchUris[query]=request.RequestUri;
            if(query is "" or "optimization") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"hits\":[],\"total_hits\":0}") });
            if(query.StartsWith("profile-",StringComparison.Ordinal) && Pending.ContainsKey(query))
            {
                var refreshed=new PendingSearch(cancellationToken); refreshed.Complete("current profile result"); return refreshed.Response.Task;
            }
            var pending=new PendingSearch(cancellationToken); Pending.Add(query,pending); return pending.Response.Task;
        }
    }
}
