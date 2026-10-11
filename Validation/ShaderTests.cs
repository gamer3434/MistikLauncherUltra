using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

public static class ShaderTests
{
    public static int RunPage(MainWindow window)
    {
        string oldVersion = window.Config.Version;
        var oldContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        using var transport = new ShaderSearchTransport(); using var http = new HttpClient(transport);
        try
        {
            window.Config.Version = "98.1-fabric";
            var page = new ShaderManagerPage(window, http);
            var results = (StackPanel)typeof(ShaderManagerPage).GetField("results", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
            void Load()
            {
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            Load(); bool oldHits = results.Children.Count > 0 && transport.Facets.Count == 1;
            window.Config.Version = "98.2-fabric"; Load();
            var newest = transport.Facets.LastOrDefault();
            if (!oldHits || results.Children.Count == 0 || transport.Facets.Count != 2 || newest == null ||
                !newest.Select(group => group[0]!.ToString()).ToHashSet().SetEquals(new[] { "project_type:shader", "versions:98.2" }))
                throw new Exception("Cached shader page did not refresh the exact selected Minecraft version.");
            Console.WriteLine("PASS cached nonempty shader page re-searches exact Minecraft version after profile change"); return 1;
        }
        finally { window.Config.Version = oldVersion; SynchronizationContext.SetSynchronizationContext(oldContext); }
    }
    sealed class ShaderSearchTransport : HttpMessageHandler
    {
        public List<JArray> Facets { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string facets = request.RequestUri!.Query.TrimStart('?').Split('&').Single(part => part.StartsWith("facets=", StringComparison.Ordinal))[7..];
            Facets.Add(JArray.Parse(Uri.UnescapeDataString(facets)));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"hits\":[{\"project_type\":\"shader\",\"project_id\":\"fixture\",\"title\":\"Shader result\",\"description\":\"Fixture\"}]}") });
        }
    }
    static byte[] Pack(string entry = "shaders/main.fsh", string contents = "void main() {}")
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry(entry).Open())) writer.Write(contents);
        return memory.ToArray();
    }
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        bool Rejected(Action action) { try { action(); return false; } catch (IOException) { return true; } }
        var bytes = Pack(); string hash = Convert.ToHexString(SHA512.HashData(bytes));
        string target = ShaderFiles.Install(root, "pack.zip", bytes, hash);
        Check(File.ReadAllBytes(target).SequenceEqual(bytes) && !Directory.Exists(Path.Combine(root, "shaders")), "shader zip verified and kept compressed");
        Check(Rejected(() => ShaderFiles.Install(root, "pack.zip", bytes, new string('0', 128))) && File.ReadAllBytes(target).SequenceEqual(bytes), "shader digest mismatch preserves installed pack");
        Check(Rejected(() => ShaderFiles.Install(root, "../escaped.zip", bytes, hash)), "shader filename traversal rejected");
        var invalid = Pack("../shaders/main.fsh");
        Check(Rejected(() => ShaderFiles.Install(root, "unsafe.zip", invalid, Convert.ToHexString(SHA512.HashData(invalid)))) && !File.Exists(Path.Combine(root, "unsafe.zip")), "shader archive path traversal rejected before write");
        invalid = Pack("readme.txt");
        Check(Rejected(() => ShaderFiles.Install(root, "not-shader.zip", invalid, Convert.ToHexString(SHA512.HashData(invalid)))), "non shader archive rejected");
        var disabled = ShaderFiles.Toggle(root, target);
        Check(disabled == target + ".disabled" && File.ReadAllBytes(disabled).SequenceEqual(bytes), "shader disable preserves exact bytes");
        var update = Pack(contents: "void main() { /* update */ }");
        Check(ShaderFiles.Install(root, "pack.zip", update, Convert.ToHexString(SHA512.HashData(update))) == disabled &&
              !File.Exists(target) && Directory.GetFiles(Path.Combine(root, ".backups")).Any(p => File.ReadAllBytes(p).SequenceEqual(bytes)), "shader update preserves disabled state and backup");
        File.WriteAllBytes(target, bytes);
        Check(Rejected(() => ShaderFiles.Toggle(root, disabled)) && File.ReadAllBytes(target).SequenceEqual(bytes) && File.ReadAllBytes(disabled).SequenceEqual(update), "shader toggle collision retains both copies");
        Check(Rejected(() => ShaderFiles.Install(root, "pack.zip", bytes, hash)), "shader install collision retains enabled and disabled copies");
        File.Delete(target); ShaderFiles.Toggle(root, disabled);
        using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(Rejected(() => ShaderFiles.Install(root, "pack.zip", bytes, hash)) && !Directory.GetFiles(root, "*.tmp").Any(), "locked shader update cleans staging and preserves pack");
        var compatible = JObject.Parse("{ 'game_versions':['1.21.1'], 'loaders':['iris'] }");
        Check(ShaderFiles.Compatible(compatible, "1.21.1") && !ShaderFiles.Compatible(compatible, "1.21") &&
              !ShaderFiles.Compatible(JObject.Parse("{ 'game_versions':['1.21.1'], 'loaders':['fabric'] }"), "1.21.1") &&
              !ShaderFiles.Compatible(JObject.Parse("{ 'game_versions':['1.21.1'], 'loaders':['optifine'] }"), "1.21.1"), "shader metadata requires exact game and Iris support");
        return checks;
    }
}
