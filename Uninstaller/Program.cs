using System.Diagnostics;
using System.Runtime.InteropServices;
using MistikLauncher.Installation;
internal static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int MessageBox(IntPtr owner,string text,string title,uint flags);
    [STAThread] static int Main(string[] args)
    {
        try {
            if(args.Length==3 && args[0]=="--finish") {
                var root=InstallEngine.ValidateRoot(args[1]);
                var state=InstallEngine.ReadState(root);
                var parent=Process.GetProcessById(int.Parse(args[2]));
                if(!Path.GetFullPath(parent.MainModule!.FileName).Equals(Path.Combine(root,"MistikUninstall.exe"),StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid uninstall parent");
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ready"),"ready");
                if(!parent.WaitForExit(30000)) throw new IOException("Close launcher and retry / Launcher'ı kapatıp tekrar deneyin.");
                InstallEngine.Uninstall(root);
                MessageBox(IntPtr.Zero,"Removed. Settings, mods and worlds were kept.\nKaldırıldı. Ayarlar, modlar ve dünyalar korundu.","Mistik Launcher",0x40); return 0;
            }
            var target=InstallEngine.ValidateRoot(AppContext.BaseDirectory); InstallEngine.ReadState(target);
            if(MessageBox(IntPtr.Zero,"Remove Mistik Launcher? Game data will be kept.\nMistik Launcher kaldırılsın mı? Oyun verileri korunacak.","Mistik Launcher",0x24)!=6) return 0;
            var temp=Path.Combine(Path.GetTempPath(),"MistikUninstall-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            var helper=Path.Combine(temp,"MistikUninstall.exe"); File.Copy(Environment.ProcessPath!,helper);
            var start=new ProcessStartInfo(helper) { UseShellExecute=false }; start.ArgumentList.Add("--finish"); start.ArgumentList.Add(target); start.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(start);
            var ready=Path.Combine(temp,"ready"); var deadline=DateTime.UtcNow.AddSeconds(30);
            while(!File.Exists(ready) && DateTime.UtcNow<deadline) Thread.Sleep(100);
            if(!File.Exists(ready)) throw new IOException("Uninstaller did not start / Kaldırıcı başlatılamadı.");
            return 0;
        } catch(Exception error) { MessageBox(IntPtr.Zero,error.Message,"Mistik Launcher — Error / Hata",0x10); return 1; }
    }
}
