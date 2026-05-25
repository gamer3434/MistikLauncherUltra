using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace MistikLauncherUltra.Windows
{
    public partial class UninstallerWindow : Window
    {
        public UninstallerWindow()
        {
            InitializeComponent();
            MouseLeftButtonDown += (_, e) => DragMove();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            UninstallBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(() => PerformUninstall());
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaldırma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                UninstallBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void PerformUninstall()
        {
            UpdateProgress("Kısayollar temizleniyor...", 30);
            DeleteShortcuts();

            UpdateProgress("Kayıt defteri temizleniyor...", 60);
            DeleteRegistry();

            UpdateProgress("Mistik Launcher siliniyor...", 90);
            
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
            
            // To safely delete ourselves without triggering Vanguard with hidden cmd,
            // we copy ourselves to TEMP, run with a special --do-uninstall flag, and exit.
            string tempExe = Path.Combine(Path.GetTempPath(), "MistikUninstaller_Final.exe");
            string currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                File.Copy(currentExe, tempExe, true);
                
                // We use UseShellExecute = true so it runs independently
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = $"--do-uninstall \"{appDataFolder}\"",
                    UseShellExecute = true
                });
            }
        }

        private void DeleteShortcuts()
        {
            try
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string start = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                
                string lnk1 = Path.Combine(desk, "Mistik Launcher.lnk");
                string lnk2 = Path.Combine(start, "Mistik Launcher.lnk");

                if (File.Exists(lnk1)) File.Delete(lnk1);
                if (File.Exists(lnk2)) File.Delete(lnk2);
            }
            catch { }
        }

        private void DeleteRegistry()
        {
            try
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient", false);
            }
            catch { }
        }

        private void UpdateProgress(string text, int value)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = text;
                UninstallProgress.Value = value;
            });
        }
    }
}
