using System.Windows;
using System.IO;
namespace MistikLauncher;
public partial class Application : System.Windows.Application
{
    internal LauncherMetrics? Metrics { get; set; }
    protected override void OnExit(ExitEventArgs e)
    {
        // Flush only the online marker; crash/power loss is handled by dashboard heartbeat expiry.
        Metrics?.StopAsync().GetAwaiter().GetResult();
        base.OnExit(e);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        foreach (var name in new[] { "account.refresh.dpapi", "account.refresh.dpapi.tmp" })
        {
            try { File.Delete(Path.Combine(App.AppData, name)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { App.Log("Obsolete launcher credential cleanup failed: " + ex.GetType().Name); }
        }
        Localization.SetLanguage(ConfigManager.Load().Lang);
        DispatcherUnhandledException += (_, args) =>
        {
            App.Log(args.Exception.ToString());
            if (Metrics != null) _ = Metrics.ReportErrorAsync("MLU-APP");
            MessageBox.Show(Localization.T("error") + "\n" + args.Exception.Message, "Mistik Launcher");
            args.Handled = true;
        };
        if (e.Args.Length >= 2 && e.Args[0] == "--do-uninstall")
        {
            StartupUri = null;
            try
            {
                var target = ReleaseSecurity.ValidateUninstallTarget(e.Args[1]);
                // Explicit uninstall only; preserve game data until a separate migration is ready.
                throw new InvalidOperationException(Localization.T("portableOnly"));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Localization.T("error")); }
            Shutdown(); return;
        }
        if (e.Args.Contains("--install") || e.Args.Contains("--uninstall"))
        {
            StartupUri = null;
            MessageBox.Show(Localization.T("portableOnly"), "Mistik Launcher");
            Shutdown(); return;
        }
        base.OnStartup(e);
    }
}
