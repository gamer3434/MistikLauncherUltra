using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MistikLauncher;
using Localization = MistikLauncher.Localization;

class Program
{
    static int checks;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "MistikValidation", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("MISTIK_DATA_DIR", testRoot);
            Directory.CreateDirectory(testRoot);
            var config = new LauncherConfig { User="../bad", Ram=200, Lang="English", TunnelPort=-1, Role="Yonetici" };
            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();
            Check(loaded.User=="Player" && loaded.Ram==32 && loaded.TunnelPort==1 && loaded.Role=="User", "config validation");
            config.User="TestPlayer"; config.Ram=4; ConfigManager.Save(config);
            Check(File.Exists(Path.Combine(testRoot,"config.json.bak")), "atomic settings backup");
            File.WriteAllText(Path.Combine(testRoot,"config.json"), "{broken");
            Check(ConfigManager.Load().Ram==32 && ConfigManager.Load().User=="Player", "corrupt settings restores previous backup");
            ConfigManager.Save(config);
            Check(!ReleaseSecurity.AutomaticUpdatesEnabled && !MistikLauncher.App.AdminAccessEnabled, "unsafe remote controls disabled");
            bool rejected=false;
            try { ReleaseSecurity.ValidateUninstallTarget(Path.GetTempPath()); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected, "uninstall rejects arbitrary directory");
            Check(ReleaseSecurity.ValidateUninstallTarget(testRoot)==testRoot, "valid application directory recognized");
            var assembly=typeof(Localization).Assembly;
            using var tr=assembly.GetManifestResourceStream("MistikLauncher.Locales.tr.json")!;
            using var en=assembly.GetManifestResourceStream("MistikLauncher.Locales.en.json")!;
            var turkish=JsonSerializer.Deserialize<Dictionary<string,string>>(tr)!;
            var english=JsonSerializer.Deserialize<Dictionary<string,string>>(en)!;
            Check(turkish.Keys.Order().SequenceEqual(english.Keys.Order()), "locale keys match");
            Check(turkish.Values.All(v=>!string.IsNullOrWhiteSpace(v)) && english.Values.All(v=>!string.IsNullOrWhiteSpace(v)), "no empty translations");
            var application=new MistikLauncher.Application(); application.InitializeComponent();
            var window=new MainWindow { Width=1200, Height=820 };
            string output = args.Length>0 ? Path.GetFullPath(args[0]) : Path.Combine(Environment.CurrentDirectory,"docs","screenshots");
            Directory.CreateDirectory(output);
            foreach (var code in new[] { "tr", "en" })
            {
                window.SwitchLanguage(code);
                Check(Localization.T("play")== (code=="tr"?"Oyunu başlat":"Launch game"), "runtime language " + code);
                Check(ConfigManager.Load().Lang==(code=="tr"?"Turkce":"English"), "language persistence " + code);
                window.Navigate("Dash"); Capture(window,Path.Combine(output,"home-"+code+".png"));
                window.Navigate("Settings"); Capture(window,Path.Combine(output,"settings-"+code+".png"));
            }
            window.Close();
            Console.WriteLine($"{checks} checks passed. Test data: {testRoot}");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Capture(Window window,string path)
    {
        var frame=new DispatcherFrame();
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
        var root=(FrameworkElement)window.Content;
        root.Measure(new Size(1200,820)); root.Arrange(new Rect(0,0,1200,820)); root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1200,820,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path); encoder.Save(file);
    }
}


