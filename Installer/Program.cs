using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MistikLauncher.Installation;
using MistikLauncher.Updates;

internal static class Program
{
#if MISTIK_REPAIR
    internal static bool RepairMode=>true;
#else
    internal static bool RepairMode=>false;
#endif
    internal static readonly string Version=Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    internal static bool Offline=>Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("MistikPayload.zip");
    [STAThread] static int Main(string[] args)
    {
        // Verify the actual EXE's embedded payload without touching an existing installation.
        if(args.Length==2 && args[0] is "--verify-package" or "--verify-download") {
            try { if(Offline!=(args[0]=="--verify-package")) throw new IOException("Wrong verification mode"); var report=Path.GetFullPath(args[1]); VerifyEmbedded(report).GetAwaiter().GetResult(); return 0; } catch(Exception error) { if(Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(args[1]))!).StartsWith("pc-test-",StringComparison.Ordinal)) File.WriteAllText(args[1],error.ToString()); return 1; }
        }
        if(args.Length==2 && args[0]=="--capture-preview") { var folder=Path.GetFullPath(args[1]); if(!Path.GetFileName(folder).StartsWith("pc-test-",StringComparison.Ordinal)) return 1; Directory.CreateDirectory(folder); var previewApp=new Application(); var window=new SetupWindow(); window.Capture(folder); return 0; }
        var app=new Application(); app.Run(new SetupWindow()); return 0;
    }
    static async Task VerifyEmbedded(string report)
    {
        if(!Path.GetFileName(Path.GetDirectoryName(report)!).StartsWith("pc-test-",StringComparison.Ordinal)) throw new IOException("Isolated pc-test-* report folder required: "+report);
        var work=CreateWork();
        try {
            var zip=await Payload(work,null,CancellationToken.None); var stage=Path.Combine(work,"payload"); UpdateEngine.Extract(zip,stage); var manifest=UpdateEngine.Verify(stage); if(manifest.Version!=Version) throw new IOException("Version mismatch");
            var fixture=Path.Combine(work,"MistikLauncherUltra"); InstallEngine.Install(stage,fixture,false,shell:false); if(InstallEngine.ReadState(fixture).Version!=Version) throw new IOException("Install lifecycle failed");
            if(RepairMode) {
                File.Delete(Path.Combine(fixture,"MistikLauncher.exe"));
                File.WriteAllText(Path.Combine(fixture,"MistikLauncher.dll"),"broken");
                File.WriteAllText(Path.Combine(fixture,"update-manifest.json"),"{broken");
                File.WriteAllText(Path.Combine(fixture,"user-test.txt"),"preserve");
                InstallEngine.Repair(stage,fixture,false,shell:false);
                UpdateEngine.VerifyInstalled(fixture,manifest);
                if(File.ReadAllText(Path.Combine(fixture,"user-test.txt"))!="preserve") throw new IOException("Repair changed user data");
            }
            File.WriteAllText(Path.Combine(fixture,"user-test.txt"),"preserve"); InstallEngine.Uninstall(fixture,false); if(File.ReadAllText(Path.Combine(fixture,"user-test.txt"))!="preserve") throw new IOException("User data not preserved");
            File.WriteAllText(report,JsonSerializer.Serialize(new { manifest.Version,Files=manifest.Files.Length,Verified=true,Lifecycle=true,Mode=RepairMode?"repair":Offline?"offline":"online" }));
        }
        finally { CleanupWork(work); }
    }
    internal static string CreateWork() { var work=Path.Combine(Path.GetTempPath(),"MistikSetup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); return work; }
    internal static void CleanupWork(string work) { if(Path.GetDirectoryName(Path.GetFullPath(work))!=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) || !Path.GetFileName(work).StartsWith("MistikSetup-",StringComparison.Ordinal)) throw new IOException("Invalid temporary folder"); if(Directory.Exists(work)) { UpdateEngine.SafePath(work,"cleanup-check"); Directory.Delete(work,true); } }
    internal static async Task<string> Payload(string work,IProgress<double>? progress,CancellationToken cancellation)
    {
        var zip=Path.Combine(work,"payload.zip");
        if(Offline) { using var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("MistikPayload.zip")!; using var output=File.Create(zip); await input.CopyToAsync(output,cancellation); progress?.Report(100); return zip; }
        using var http=new HttpClient { Timeout=TimeSpan.FromMinutes(15) }; http.DefaultRequestHeaders.UserAgent.ParseAdd("MistikSetup/"+Version);
        return await InstallEngine.DownloadPayloadAsync(work,Version,http,progress,cancellation);
    }
}

internal sealed class SetupWindow:Window
{
    bool English,Busy,Complete; CancellationTokenSource? cancellation;
    readonly TextBlock heading=new(),description=new(),locationLabel=new(),status=new();
    readonly TextBox location=new() { Text=InstallEngine.DefaultRoot,Padding=new Thickness(10),Margin=new Thickness(0,8,0,16) };
    readonly CheckBox shortcut=new() { IsChecked=true,Margin=new Thickness(0,0,0,24) };
    readonly ProgressBar progress=new() { Height=8,Minimum=0,Maximum=100,Margin=new Thickness(0,12,0,16) };
    readonly Button install=new() { Padding=new Thickness(22,10,22,10),Margin=new Thickness(8,0,0,0) },cancel=new() { Padding=new Thickness(22,10,22,10) };
    readonly ComboBox language=new() { Width=120,ItemsSource=new[]{"Türkçe","English"},SelectedIndex=0,Margin=new Thickness(0,0,0,24) };
    string T(string tr,string en)=>English?en:tr;
    public SetupWindow()
    {
        Title="Mistik Launcher Setup / Kurulum"; Width=680; Height=520; MinWidth=640; MinHeight=500; WindowStartupLocation=WindowStartupLocation.CenterScreen; FontFamily=new FontFamily("Segoe UI"); FontSize=14;
        if(Program.RepairMode) Title="Mistik Launcher — Onarıcı / Repair";
        Background=new SolidColorBrush(Color.FromRgb(18,18,21)); Foreground=Brushes.White;
        var content=new StackPanel { Margin=new Thickness(32) }; var scroll=new ScrollViewer { Content=content,Background=Background,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Content=scroll;
        shortcut.Foreground=Foreground;
        location.Background=new SolidColorBrush(Color.FromRgb(36,36,40)); location.Foreground=Foreground; location.CaretBrush=Foreground;
        location.BorderBrush=new SolidColorBrush(Color.FromRgb(90,90,100));
        cancel.Background=new SolidColorBrush(Color.FromRgb(36,36,40)); cancel.Foreground=Foreground;
        progress.Foreground=new SolidColorBrush(Color.FromRgb(255,176,0));
        language.HorizontalAlignment=HorizontalAlignment.Right; content.Children.Add(language);
        heading.FontSize=26; heading.FontWeight=FontWeights.SemiBold; content.Children.Add(heading);
        description.TextWrapping=TextWrapping.Wrap; description.Foreground=new SolidColorBrush(Color.FromRgb(190,190,198)); description.Margin=new Thickness(0,8,0,28); content.Children.Add(description);
        content.Children.Add(locationLabel); content.Children.Add(location); content.Children.Add(shortcut); content.Children.Add(progress);
        status.TextWrapping=TextWrapping.Wrap; status.MinHeight=48; content.Children.Add(status);
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0) }; buttons.Children.Add(cancel); buttons.Children.Add(install); content.Children.Add(buttons);
        install.Background=new SolidColorBrush(Color.FromRgb(255,176,0)); install.Foreground=Brushes.Black;
        System.Windows.Automation.AutomationProperties.SetName(language,"Dil / Language");
        System.Windows.Automation.AutomationProperties.SetName(location,"Kurulum konumu / Installation folder");
        language.SelectionChanged+=(_,_)=>{ English=language.SelectedIndex==1; Translate(); };
        install.Click+=async(_,_)=>{ if(Complete) { Process.Start(new ProcessStartInfo(Path.Combine(location.Text,"MistikLauncher.exe")) { UseShellExecute=true,WorkingDirectory=location.Text }); Close(); } else await Run(); };
        cancel.Click+=(_,_)=>{ if(Busy) cancellation?.Cancel(); else Close(); };
        Closing+=(_,e)=>{ if(Busy) { e.Cancel=true; cancellation?.Cancel(); } }; Translate();
    }
    void Translate()
    {
        heading.Text=T("Mistik Launcher kurulumu","Mistik Launcher setup");
        description.Text=Program.Offline?T("Yerel kurulum · İnternet bağlantısı gerekmez. Oyun verileri korunur.","Offline installation · No internet needed. Game data is preserved."):T("Online kurulum · Doğrulanmış uygulama dosyaları GitHub'dan indirilir.","Online installation · Verified application files are downloaded from GitHub.");
        locationLabel.Text=T("Kurulum klasörü","Installation folder"); shortcut.Content=T("Masaüstü kısayolu oluştur","Create desktop shortcut"); install.Content=Complete?T("Launcher'ı aç","Open launcher"):T("Kur","Install"); cancel.Content=Busy?T("İptal et","Cancel"):T("Kapat","Close");
        if(!Busy) status.Text=Complete?T("Kurulum tamamlandı.","Installation complete."):T("Sürüm: ","Version: ")+Program.Version;
        if(Program.RepairMode) {
            heading.Text=T("Mistik Launcher onarımı","Mistik Launcher repair");
            description.Text=T("Eksik ve bozuk program dosyaları doğrulanmış kopyalarıyla yenilenir. Ayarlar, modlar ve dünyalar korunur. Başlamadan önce launcher'ı kapatın.","Missing and damaged program files are restored from verified copies. Settings, mods and worlds are kept. Close the launcher before starting.");
            install.Content=Complete?T("Launcher'ı aç","Open launcher"):T("Onar","Repair");
            if(!Busy && Complete) status.Text=T("Onarım tamamlandı.","Repair complete.");
        }
    }
    internal void Capture(string folder)
    {
        var visual=(FrameworkElement)Content;
        foreach(var code in new[]{"tr","en"}) {
            language.SelectedIndex=code=="en"?1:0; Translate();
            visual.Measure(new Size(680,480)); visual.Arrange(new Rect(0,0,680,480)); visual.UpdateLayout();
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap(680,480,96,96,PixelFormats.Pbgra32); image.Render(visual);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image)); using var file=File.Create(Path.Combine(folder,(Program.RepairMode?"repair":"setup-"+(Program.Offline?"offline":"online"))+"-"+code+".png")); encoder.Save(file);
        }
    }
    async Task Run()
    {
        string? work=null; Busy=true; install.IsEnabled=false; location.IsEnabled=false; shortcut.IsEnabled=false; language.IsEnabled=false; cancellation=new CancellationTokenSource(); Translate();
        try {
            var root=InstallEngine.ValidateRoot(location.Text); location.Text=root;
            work=Program.CreateWork(); status.Text=T("Dosyalar hazırlanıyor…","Preparing files…");
            var downloadProgress=new Progress<double>(p=>progress.Value=p*0.75); var zip=await Program.Payload(work,downloadProgress,cancellation.Token);
            status.Text=Program.RepairMode?T("Dosyalar doğrulanıyor ve onarılıyor…","Verifying and repairing files…"):T("Dosyalar doğrulanıyor ve kuruluyor…","Verifying and installing files…"); var stage=Path.Combine(work,"payload"); bool desktop=shortcut.IsChecked==true;
            await Task.Run(()=>{ cancellation.Token.ThrowIfCancellationRequested(); UpdateEngine.Extract(zip,stage); if(UpdateEngine.Verify(stage).Version!=Program.Version) throw new IOException("Version mismatch / Sürüm uyuşmazlığı."); if(Program.RepairMode) InstallEngine.Repair(stage,root,desktop,cancellation.Token); else InstallEngine.Install(stage,root,desktop,cancellation.Token); });
            progress.Value=100; Complete=true;
        } catch(OperationCanceledException) { status.Text=Program.RepairMode?T("Onarım iptal edildi.","Repair cancelled."):T("Kurulum iptal edildi.","Installation cancelled."); }
        catch(Exception error) { status.Text=(Program.RepairMode?T("Onarım tamamlanamadı: ","Repair could not finish: "):T("Kurulum tamamlanamadı: ","Installation could not finish: "))+error.Message; }
        finally { Busy=false; install.IsEnabled=true; location.IsEnabled=!Complete; shortcut.IsEnabled=!Complete; language.IsEnabled=true; cancel.Content=T("Kapat","Close"); install.Content=Complete?T("Launcher'ı aç","Open launcher"):T("Tekrar dene","Retry"); if(Complete) status.Text=Program.RepairMode?T("Onarım tamamlandı. Oyun verileri korundu.","Repair complete. Game data preserved."):T("Kurulum tamamlandı. Oyun verileri korundu.","Installation complete. Game data preserved."); if(work!=null) Program.CleanupWork(work); cancellation.Dispose(); }
    }
}
