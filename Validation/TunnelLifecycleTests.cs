using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using MistikLauncher;

static class TunnelLifecycleTests
{
    public static async Task<int> Run(string root, string? fixture = null)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var relay=new MistikRelay("TunnelFixture");
        var type=typeof(MistikRelay);
        object? Field(string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(relay);
        object? Call(string name,params object?[] args)=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(relay,args);
        int Generation()=>(int)Field("_tunnelGeneration")!;
        var pending=new TaskCompletionSource<(bool success,string message)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removed=new List<int>(); int notifications=0;
        relay.OnTunnelReady+=_=>notifications++;
        Func<int,Task<bool>> remove=port=> { removed.Add(port); return Task.FromResult(true); };
        var stale=(Task)Call("StartUpnpTunnelAsync",Generation(),25565,
            (Func<int,Task<(bool,string)>>)(_=>pending.Task),(Func<Task<string?>>)(()=>Task.FromResult<string?>("192.0.2.1")),remove)!;
        relay.StopTunnel(); pending.SetResult((true,"fixture")); await stale;
        Check(removed.SequenceEqual(new[]{25565}) && relay.TunnelAddress==null && Field("_mappedPort")==null && notifications==0,
            "stopped UPnP request removes its own late mapping and never publishes a stale address");
        removed.Clear();
        await (Task)Call("StartUpnpTunnelAsync",Generation(),25565,
            (Func<int,Task<(bool,string)>>)(_=>Task.FromResult((false,"denied"))),
            (Func<Task<string?>>)(()=>Task.FromResult<string?>("192.0.2.1")),remove)!;
        Check(removed.Count==0 && Field("_mappedPort")==null,"failed UPnP creation does not remove an unowned router mapping");
        removed.Clear();
        await (Task)Call("StartUpnpTunnelAsync",Generation(),25565,
            (Func<int,Task<(bool,string)>>)(_=>Task.FromResult((true,"fixture"))),
            (Func<Task<string?>>)(()=>Task.FromException<string?>(new IOException("address unavailable"))),remove)!;
        Check(removed.SequenceEqual(new[]{25565}) && Field("_mappedPort")==null,"UPnP address lookup failure removes only the mapping just created");
        var router=new RouterHandler();
        using var routerClient=new HttpClient(router);
        var added=await MistikUpnp.AddUpnpPortMappingWithTimeoutAsync(25565,controlUrl:"http://router.invalid/control",client:routerClient);
        Check(added.success && router.Requests.SequenceEqual(new[]{("AddPortMapping","TCP")}),"UPnP creation requests TCP without clearing existing TCP or UDP mappings");
        router.Requests.Clear();
        Check(await MistikUpnp.RemoveUpnpPortMappingAsync(25565,"http://router.invalid/control",routerClient) && router.Requests.SequenceEqual(new[]{("DeletePortMapping","TCP")}),"UPnP cleanup leaves unrelated UDP mappings intact");
        router.Requests.Clear(); router.Status=HttpStatusCode.InternalServerError;
        Check(!await MistikUpnp.RemoveUpnpPortMappingAsync(25565,"http://router.invalid/control",routerClient),"UPnP cleanup reports router rejection rather than false success");
        if(fixture==null) return checks;

        string dir=Path.Combine(root,"tunnel-processes"); Directory.CreateDirectory(dir);
        var unrelatedExe=Path.Combine(dir,"ssh.exe"); File.Copy(fixture,unrelatedExe);
        ProcessStartInfo StartInfo(string exe,string marker) {
            var info=new ProcessStartInfo(exe) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(marker); return info;
        }
        var marker=Path.Combine(dir,"unrelated");
        using var unrelated=Process.Start(StartInfo(unrelatedExe,marker))!;
        Process? owned=null;
        try
        {
            for(int i=0;i<100 && !File.Exists(marker+".started");i++) await Task.Delay(50);
            if(!File.Exists(marker+".started")) throw new Exception("Unrelated SSH fixture failed to start.");
            int old=Generation(); relay.StopTunnel();
            owned=(Process?)Call("StartTunnelProcess",old,StartInfo(fixture,Path.Combine(dir,"stale")));
            Check(owned==null && !File.Exists(Path.Combine(dir,"stale.started")),"stopped tunnel download cannot start a late process");
            int current=Generation();
            owned=(Process?)Call("StartTunnelProcess",current,StartInfo(fixture,Path.Combine(dir,"owned")));
            if(owned==null) throw new Exception("Owned tunnel fixture failed to start.");
            Call("NotifyTunnelAddress",old,owned,"stale.example:25565");
            Check(relay.TunnelAddress==null,"old tunnel callback cannot replace the current address");
            Call("NotifyTunnelAddress",current,owned,"current.example:25565");
            Check(relay.TunnelAddress=="current.example:25565","current owned tunnel process may publish its address");
            using var observed=Process.GetProcessById(owned.Id);
            relay.StopTunnel();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await observed.WaitForExitAsync(timeout.Token);
            Check(observed.HasExited && !unrelated.HasExited && relay.TunnelAddress==null && Field("_tunnelProc")==null,
                "stopping the launcher tunnel preserves unrelated SSH processes");
        }
        finally { relay.StopTunnel(); if(!unrelated.HasExited) { unrelated.Kill(true); await unrelated.WaitForExitAsync(); } }
        return checks;
    }
    sealed class RouterHandler : HttpMessageHandler
    {
        public readonly List<(string action,string protocol)> Requests=new();
        public HttpStatusCode Status=HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var xml=XDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var action=xml.Descendants().First(node=>node.Name.LocalName is "AddPortMapping" or "DeletePortMapping");
            Requests.Add((action.Name.LocalName,action.Elements().Single(node=>node.Name.LocalName=="NewProtocol").Value));
            return new HttpResponseMessage(Status) { Content=new StringContent("fixture") };
        }
    }
}
