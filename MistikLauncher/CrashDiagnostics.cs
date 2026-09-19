using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncher;
public static class CrashDiagnostics
{
    public static string ManualReport()
    {
        var output = File.Exists(App.LogFile) ? Tail(App.LogFile) : "";
        return Report(null, DateTime.UtcNow.AddDays(-1), output);
    }

    public static string SaveReport(string report)
    {
        var directory = Path.Combine(App.AppData, "reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, report, Encoding.UTF8);
        return path;
    }

    public static string Redact(string text) => Regex.Replace(text, @"(?i)(access[_-]?token|refresh[_-]?token|id[_-]?token|authorization)([\s=:""']+)[^\s,""']+", "$1$2[REDACTED]");
    public static string Category(string text) => text.Contains("OutOfMemoryError",StringComparison.OrdinalIgnoreCase) || text.Contains("Could not reserve",StringComparison.OrdinalIgnoreCase) ? "MLU-MEMORY" :
        text.Contains("UnsupportedClassVersionError",StringComparison.OrdinalIgnoreCase) ? "MLU-JAVA" :
        text.Contains("ClassNotFoundException",StringComparison.OrdinalIgnoreCase) || text.Contains("Could not find or load main class",StringComparison.OrdinalIgnoreCase) ? "MLU-CLASSPATH" :
        text.Contains("Mixin",StringComparison.OrdinalIgnoreCase) ? "MLU-MOD-MIXIN" :
        text.Contains("Incompatible mods",StringComparison.OrdinalIgnoreCase) || text.Contains("requires",StringComparison.OrdinalIgnoreCase) && text.Contains("mod",StringComparison.OrdinalIgnoreCase) ? "MLU-MOD-DEPENDENCY" :
        text.Contains("ZipException",StringComparison.OrdinalIgnoreCase) || text.Contains("NoSuchFileException",StringComparison.OrdinalIgnoreCase) ? "MLU-FILE" : "MLU-EXIT";
    public static string Tail(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        stream.Seek(Math.Max(0,stream.Length-65536),SeekOrigin.Begin);
        using var reader=new StreamReader(stream); return reader.ReadToEnd();
    }
    public static string Report(int? exitCode,DateTime started,string output)
    {
        bool en=Localization.Language=="en";
        var files=new List<string>(); var text=output;
        var crash=Path.Combine(App.GameDir,"crash-reports");
        var candidates=new List<string> { Path.Combine(App.GameDir,"logs","latest.log") };
        if(Directory.Exists(crash)) candidates.AddRange(Directory.EnumerateFiles(crash,"crash-*.txt").OrderByDescending(File.GetLastWriteTimeUtc).Take(1));
        if(Directory.Exists(App.GameDir)) candidates.AddRange(Directory.EnumerateFiles(App.GameDir,"hs_err_pid*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(1));
        foreach(var file in candidates) try {
            if(File.Exists(file) && File.GetLastWriteTimeUtc(file)>=started) { text+="\n"+Tail(file); files.Add(file); }
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
        // Only explicit error lines implicate a mod; the loaded-mod list is not evidence of blame.
        var evidence=string.Join("\n",text.Split('\n').Where(line=>Regex.IsMatch(line,@"(?i)error|exception|caused by|mod file:|failed|requires|mixin")));
        var suspects=Directory.Exists(App.ModsDir)?Directory.EnumerateFiles(App.ModsDir,"*.jar").Where(file=>evidence.Contains(Path.GetFileName(file),StringComparison.OrdinalIgnoreCase)).Take(12).ToArray():Array.Empty<string>();
        var code=Category(text); if(exitCode==null && code=="MLU-EXIT") code="MLU-LAUNCH";
        string advice=code switch {
            "MLU-MEMORY"=>en?"Check free memory and reduce RAM allocation or heavy mods.":"Boş belleği kontrol edin; RAM tahsisini veya ağır modları azaltın.",
            "MLU-JAVA"=>en?"Select the Java version required by this Minecraft profile.":"Bu Minecraft profilinin gerektirdiği Java sürümünü kullanın.",
            "MLU-CLASSPATH"=>en?"A required class could not be loaded. Check the profile main class, game JAR and library/mod compatibility.":"Gerekli bir sınıf yüklenemedi. Profil ana sınıfını, oyun JAR dosyasını ve kütüphane/mod uyumunu kontrol edin.",
            "MLU-MOD-MIXIN" or "MLU-MOD-DEPENDENCY"=>en?"Check mod dependencies and Minecraft/loader compatibility. Disable suspected mods without deleting them.":"Mod bağımlılıklarını ve Minecraft/yükleyici uyumunu kontrol edin. Şüpheli modları silmeden devre dışı bırakın.",
            "MLU-FILE"=>en?"Check the named file; back up your data before reinstalling it.":"Belirtilen dosyayı kontrol edin; yeniden kurmadan önce verilerinizi yedekleyin.",
            _=>en?"The exit code alone cannot identify the cause. Review the error output below.":"Çıkış kodu tek başına nedeni belirleyemez. Aşağıdaki hata çıktısını inceleyin."
        };
        return code+"\n"+(en?"Exit code: ":"Çıkış kodu: ")+(exitCode?.ToString()??(en?"Not started":"Başlatılamadı"))+"\n\n"+advice+"\n\n"+
            (en?"Mods explicitly named in error lines (possible causes):":"Hata satırlarında adı geçen modlar (olası nedenler):")+"\n"+(suspects.Length>0?string.Join("\n",suspects):en?"No mod could be identified reliably.":"Güvenilir biçimde bir mod belirlenemedi.")+"\n\n"+
            (en?"Report files:":"Rapor dosyaları:")+"\n"+string.Join("\n",files)+"\n\n"+Redact(text.Length>131072?text[^131072..]:text);
    }
    public static void Show(Window owner,string report)
    {
        bool en=Localization.Language=="en";
        owner.Show(); owner.WindowState=WindowState.Normal; owner.Activate();
        var window=new Window { Owner=owner,Title=en?"Minecraft diagnostics":"Minecraft hata analizi",Width=760,Height=560,MinWidth=500,MinHeight=350,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(24,24,28)) };
        var layout=new DockPanel { Margin=new Thickness(22) };
        var heading=new TextBlock { Text=en?"Minecraft closed unexpectedly or could not start":"Minecraft beklenmedik biçimde kapandı veya açılamadı",Foreground=Brushes.White,FontSize=18,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16) };
        DockPanel.SetDock(heading,Dock.Top); layout.Children.Add(heading);
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0) };
        var copy=new Button { Content=en?"Copy report":"Raporu kopyala",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,10,0),MinWidth=112 };
        copy.Click+=(_,_)=> { try { Clipboard.SetText(report); } catch(System.Runtime.InteropServices.COMException) { } };
        var save=new Button { Content=en?"Save report":"Raporu kaydet",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,10,0),MinWidth=112 };
        var status=new TextBlock { Foreground=new SolidColorBrush(Color.FromRgb(173,190,214)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0),TextWrapping=TextWrapping.Wrap };
        save.Click+=(_,_)=> { try { status.Text=(en?"Saved: ":"Kaydedildi: ")+SaveReport(report); } catch(Exception ex) { status.Text=(en?"Save failed: ":"Kayıt başarısız: ")+ex.Message; } };
        var open=new Button { Content=en?"Open folder":"Klasörü aç",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,10,0),MinWidth=100 };
        open.Click+=(_,_)=> { try { var dir=Path.Combine(App.AppData,"reports"); Directory.CreateDirectory(dir); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute=true }); } catch { } };
        var close=new Button { Content=en?"Close":"Kapat",Padding=new Thickness(12,7,12,7) }; close.Click+=(_,_)=>window.Close();
        buttons.Children.Add(status); buttons.Children.Add(copy); buttons.Children.Add(save); buttons.Children.Add(open); buttons.Children.Add(close); DockPanel.SetDock(buttons,Dock.Bottom); layout.Children.Add(buttons);
        layout.Children.Add(new TextBox { Text=report,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=new SolidColorBrush(Color.FromRgb(15,15,18)),Foreground=Brushes.White,Padding=new Thickness(14),BorderBrush=Brushes.DimGray });
        window.Content=layout; window.Show();
    }
}
