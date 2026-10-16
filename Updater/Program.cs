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
            string planPath=Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            plan=JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(planPath)) ?? throw new InvalidDataException("Missing update plan.");
            using var parent=Process.GetProcessById(plan.ParentId);
            if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStarted || !string.Equals(parent.MainModule?.FileName,UpdateEngine.SafePath(plan.Target,"MistikLauncher.exe"),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update parent identity mismatch.");
            File.WriteAllText(planPath+".ready","ready");
            if(!parent.WaitForExit(60000)) throw new IOException("Launcher is still running.");
            UpdateEngine.Apply(plan.Payload,plan.Target,plan.ExpectedVersion,allowDowngrade:plan.AllowDowngrade);
            Process.Start(new ProcessStartInfo(UpdateEngine.SafePath(plan.Target,"MistikLauncher.exe")) { UseShellExecute=true, WorkingDirectory=plan.Target });
            return 0;
        }
        catch(Exception ex)
        {
            string? report=null;
            try {
                report=Path.Combine(AppContext.BaseDirectory,"update-error.txt");
                File.WriteAllText(report,ex.ToString());
            } catch(IOException) { report=null; } catch(UnauthorizedAccessException) { report=null; }
            bool tr=plan?.Language=="tr";
            string message=tr?"Güncelleme tamamlanamadı. Launcher'ı kapatıp yeniden deneyin. Gerekirse onarıcıyı kullanın.":"Update failed. Close the launcher and retry. Use the repair tool if needed.";
            if(report!=null) message+=(tr?"\n\nAyrıntılı rapor: ":"\n\nFull report: ")+(report.Length>500?report[..499]+"…":report);
            MessageBox(0,message,"Mistik Launcher",0x10);
            return 1;
        }
    }
}
