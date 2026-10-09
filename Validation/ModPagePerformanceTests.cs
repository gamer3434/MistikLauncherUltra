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
        var bundleRoot=Path.Combine(root,"curseforge-bundle"); Directory.CreateDirectory(bundleRoot);
        var bundle=new[] {
            new CurseForgeModFile("dependency-new.jar",jar) { CompatibleFilenames=new[]{"dependency-new.jar","dependency-old.jar"} },
            new CurseForgeModFile("main-new.jar",jar) { CompatibleFilenames=new[]{"main-new.jar","main-old.jar"} }
        };
        var installBundle=typeof(ModManagerPage).GetMethod("InstallCurseForgeBundle",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var conflict in new[]{"main-old.jar","dependency-old.jar","dependency-old.jar.disabled"})
        {
            string existing=Path.Combine(bundleRoot,conflict); File.WriteAllBytes(existing,jar); bool rejected=false;
            try { installBundle.Invoke(null,new object[]{bundleRoot,bundle}); } catch(TargetInvocationException error) when(error.InnerException is IOException) { rejected=true; }
            Check(rejected && Directory.GetFiles(bundleRoot).Length==1 && File.ReadAllBytes(existing).SequenceEqual(jar),
                "CurseForge rejects existing alternate mod version before any bundle write: "+conflict);
            File.Delete(existing);
        }
        installBundle.Invoke(null,new object[]{bundleRoot,bundle});
        Check(bundle.All(file=>File.ReadAllBytes(Path.Combine(bundleRoot,file.Filename)).SequenceEqual(jar)),"CurseForge installs the verified bundle after conflict preflight succeeds");
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
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
        Check(Result("new result") && !Result("old result"),"late superseded search cannot replace the latest result cards");
        var failed=Search("late-failure"); var fresh=Search("fresh");
        transport.Pending["fresh"].Complete("fresh result"); Wait(fresh);
        transport.Pending["late-failure"].Response.SetException(new HttpRequestException("obsolete request failed")); Wait(failed);
        Check(Result("fresh result") && !Nodes(results).OfType<TextBlock>().Any(text=>text.Text.Contains("obsolete request failed",StringComparison.Ordinal)),
            "late superseded search failure cannot replace current results with an error");
        var clearing=Search("clear-pending"); Wait(Search(""));
        transport.Pending["clear-pending"].Complete("obsolete cleared result"); Wait(clearing);
        Check(results.Children.Count==0,"clearing a search cancels pending work and leaves no stale results or loading card");
        var source=(ComboBox)fields.GetField("_sourceBox",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;
        Check(source.Items.Cast<string>().SequenceEqual(new[]{"Modrinth","CurseForge"}) && source.SelectedIndex==0,
            "mod search defaults to Modrinth and offers the explicit CurseForge source");
        var oldKey=window.Config.CurseForgeApiKey; var oldVersion=window.Config.Version;
        try
        {
            window.Config.CurseForgeApiKey="";
            var switching=Search("source-switch-pending"); int before=transport.Requests;
            source.SelectedIndex=1;
            Check(transport.Requests==before && transport.CurseForgeRequests==0 && transport.Pending["source-switch-pending"].Token.IsCancellationRequested &&
                Nodes(results).OfType<TextBlock>().Any(text=>text.Name=="CurseForgeKeyRequired") &&
                Nodes(results).OfType<Button>().Any(button=>button.Name=="CurseForgeSettings") && Nodes(results).OfType<Button>().Any(button=>button.Name=="CurseForgeOfficialSearch"),
                "CurseForge without a key cancels the previous source and shows settings and official website help without an HTTP request");
            transport.Pending["source-switch-pending"].Complete("obsolete Modrinth result"); Wait(switching);
            Check(!Result("obsolete Modrinth result") && Nodes(results).OfType<TextBlock>().Any(text=>text.Name=="CurseForgeKeyRequired"),
                "late Modrinth result cannot replace the selected CurseForge key help");
            Wait(Search(""));
            Check(transport.CurseForgeRequests==0 && Nodes(results).OfType<TextBlock>().Any(text=>text.Name=="CurseForgeKeyRequired"),
                "missing CurseForge key retains helpful actions even with an empty search");
            window.Config.CurseForgeApiKey="fixture-api-key"; window.Config.Version="98.1-fabric";
            Wait(Search("cf-current"));
            Check(Result("CurseForge fixture") && transport.CurseForgeRequests==1 &&
                Nodes(results).OfType<TextBlock>().Any(text=>text.Text=="CurseForge") &&
                Nodes(results).OfType<Button>().Any(button=>Equals(button.Content,Localization.T("cfOpenWebsite"))),
                "authenticated CurseForge search displays normalized metadata source caption and official website action");
            Wait(Search("")); source.SelectedIndex=0;
            Check(results.Children.Count==0 && source.SelectedIndex==0,"switching back to Modrinth invalidates CurseForge results");
        }
        finally { window.Config.CurseForgeApiKey=oldKey; window.Config.Version=oldVersion; }
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
        public int Requests,CurseForgeRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;
            if(request.RequestUri!.Host=="api.curseforge.com")
            {
                CurseForgeRequests++;
                if(!request.Headers.TryGetValues("x-api-key",out var keys) || keys.Single()!="fixture-api-key") throw new Exception("Missing fixture API header.");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(new JObject {
                    ["data"]=new JArray(new JObject { ["id"]=123,["gameId"]=432,["classId"]=6,["name"]="CurseForge fixture",["summary"]="fixture",
                        ["downloadCount"]=2,["slug"]="fixture-mod" }) }.ToString()) });
            }
            string query=Uri.UnescapeDataString(request.RequestUri!.Query.TrimStart('?').Split('&').Single(value=>value.StartsWith("query=",StringComparison.Ordinal))[6..]);
            if(query=="optimization") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"hits\":[]}") });
            var pending=new PendingSearch(cancellationToken); Pending.Add(query,pending); return pending.Response.Task;
        }
    }
}
