using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using MistikLauncher.Updates;
class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int MessageBox(nint window,string text,string title,uint type);
    static int Main(string[] args)
    {
        UpdatePlan? plan=null;
        try
        {
            if(args.Length!=1) return 2;
            plan=JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(args[0])) ?? throw new InvalidDataException("Missing update plan.");
            using var parent=Process.GetProcessById(plan.ParentId);
            if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStarted || !string.Equals(parent.MainModule?.FileName,UpdateEngine.SafePath(plan.Target,"MistikLauncher.exe"),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update parent identity mismatch.");
            File.WriteAllText(args[0]+".ready","ready");
            if(!parent.WaitForExit(60000)) throw new IOException("Launcher is still running.");
            UpdateEngine.Apply(plan.Payload,plan.Target,plan.ExpectedVersion);
            Process.Start(new ProcessStartInfo(UpdateEngine.SafePath(plan.Target,"MistikLauncher.exe")) { UseShellExecute=true, WorkingDirectory=plan.Target });
            return 0;
        }
        catch(Exception ex)
        {
            string message=plan?.Language=="tr"?"Güncelleme tamamlanamadı. Önceki dosyalar korundu.\n":"Update failed. Previous files were preserved.\n";
            MessageBox(0,message+ex.Message,"Mistik Launcher",0x10);
            return 1;
        }
    }
}
