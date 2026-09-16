using System.Windows;
using System.IO;
namespace MistikLauncher;
public partial class Application : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Localization.SetLanguage(ConfigManager.Load().Lang);
        DispatcherUnhandledException += (_, args) =>
        {
            App.Log(args.Exception.ToString());
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
                throw new InvalidOperationException("Uninstall is disabled in this preview. Remove the portable application folder manually.");
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
