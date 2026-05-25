using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MistikLauncher
{
    public partial class Application : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // ── Self-Installer Integration ──────────────────────────────────────────
            try
            {
                // Handle the final phase of uninstall (running from %TEMP%)
                if (e.Args.Length >= 2 && e.Args[0] == "--do-uninstall")
                {
                    string targetFolder = e.Args[1];
                    // Wait a bit to ensure the main process has fully exited
                    System.Threading.Thread.Sleep(2000);
                    try {
                        if (Directory.Exists(targetFolder)) {
                            Directory.Delete(targetFolder, true);
                        }
                    } catch { }
                    MessageBox.Show("Mistik Launcher ve tüm verileri sisteminizden başarıyla silindi.", "Kaldırma Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Windows.Application.Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--uninstall")
                {
                    var uninstaller = new Windows.UninstallerWindow();
                    uninstaller.Show();
                    return;
                }

                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
                string officialExePath = Path.Combine(appDataFolder, "MistikLauncher.exe");
                string currentExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (!string.IsNullOrEmpty(currentExePath) && 
                    !currentExePath.Equals(officialExePath, StringComparison.OrdinalIgnoreCase))
                {
                    var installer = new Windows.InstallerWindow();
                    installer.Show();
                    return;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "mistik_install_error.log");
                    File.WriteAllText(log, $"[{DateTime.Now}] Kurulum hatası: {ex}");
                }
                catch { }
            }

            base.OnStartup(e);

            // Global hata yakalayıcı — crash log yazar
            DispatcherUnhandledException += (_, ex) =>
            {
                WriteCrash(ex.Exception);
                ex.Handled = true;
                MessageBox.Show($"Hata:\n{ex.Exception.Message}\n\nDetay: Desktop\\mistik_crash.log",
                    "Mistik Launcher Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                WriteCrash(ex.ExceptionObject as Exception);
            };
        }

        static void WriteCrash(Exception? ex)
        {
            try
            {
                string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "mistik_crash.log");
                File.AppendAllText(log, $"\n=== {DateTime.Now} ===\n{ex}\n");
            }
            catch { }
        }
    }
}
