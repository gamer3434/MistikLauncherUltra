using System.IO;
using System.Net;
using System.Net.Http;
using MistikLauncher;

static class AuthlibInjectorTests
{
    public static async Task<int> Live(string directory)
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "authlib-injector.jar");
        File.WriteAllText(target, "old injector");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string installed = await AuthlibInjector.EnsureAsync(directory, client);
        if (installed != target || File.ReadAllText(target + ".bak") != "old injector")
            throw new Exception("Official injector upgrade did not preserve the previous JAR.");
        using var handler = new Stub("missing");
        using var offline = new HttpClient(handler);
        await AuthlibInjector.EnsureAsync(directory, offline);
        if (handler.Requests != 0) throw new Exception("Verified injector cache reached the network.");
        File.WriteAllText(target, "second old injector");
        await AuthlibInjector.EnsureAsync(directory, client);
        if (File.ReadAllText(target + ".bak") != "second old injector")
            throw new Exception("Repeated injector repair did not refresh the backup.");
        File.Delete(target);
        await AuthlibInjector.EnsureAsync(directory, client);
        Console.WriteLine("PASS official injector installation, verified offline reuse, upgrade backup and repeated repair");
        return 4;
    }

    public static async Task<int> Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "authlib-injector.jar");
        byte[] original = new byte[12000];
        original[0] = 42;
        File.WriteAllBytes(target, original);
        int checks = 0;
        foreach (string mode in new[] { "corrupt", "missing", "oversized", "cancel" })
        {
            using var handler = new Stub(mode);
            using var client = new HttpClient(handler);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            bool rejected = false;
            try { await AuthlibInjector.EnsureAsync(directory, client, cancellation.Token); }
            catch (Exception error) when (error is HttpRequestException or InvalidDataException or OperationCanceledException) { rejected = true; }
            if (!rejected || !File.ReadAllBytes(target).SequenceEqual(original) || Directory.EnumerateFiles(directory, "*.tmp").Any())
                throw new Exception("Invalid injector download replaced the old file or left staging: " + mode);
            if (mode == "missing" && handler.Requests != 2) throw new Exception("Pinned fallback was not tried.");
            Console.WriteLine("PASS injector " + mode + " preserves the previous JAR and cleans staging"); checks++;
        }
        using (var held = File.Open(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        using (var handler = new Stub("corrupt"))
        using (var client = new HttpClient(handler))
        {
            bool rejected = false;
            try { await AuthlibInjector.EnsureAsync(directory, client); }
            catch (IOException) { rejected = true; }
            if (!rejected || handler.Requests != 0) throw new Exception("Concurrent injector replacement reached the network.");
            Console.WriteLine("PASS injector concurrent replacement stops before downloading"); checks++;
        }
        return checks;
    }

    sealed class Stub(string mode) : HttpMessageHandler
    {
        public int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            if (mode == "cancel") await Task.Delay(Timeout.Infinite, token);
            var response = new HttpResponseMessage(mode == "missing" ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            { Content = new ByteArrayContent(new byte[12000]) };
            if (mode == "oversized") response.Content.Headers.ContentLength = 9 * 1024 * 1024;
            return response;
        }
    }
}
