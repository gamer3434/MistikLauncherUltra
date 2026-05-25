using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace MistikLauncher.Windows
{
    public partial class InstallerWindow : Window
    {
        public InstallerWindow()
        {
            InitializeComponent();
            MouseLeftButtonDown += (_, e) => DragMove();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            InstallBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(() => PerformInstallation());

                MessageBox.Show(
                    "Mistik Launcher başarıyla kuruldu!\nArtık masaüstü kısayolunuzla oyuna girebilirsiniz.",
                    "Kurulum Tamamlandı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Launch installed exe
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
                string officialExePath = Path.Combine(appDataFolder, "MistikLauncher.exe");

                Process.Start(new ProcessStartInfo(officialExePath)
                {
                    WorkingDirectory = appDataFolder,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void PerformInstallation()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
            string officialExePath = Path.Combine(appDataFolder, "MistikLauncher.exe");
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

            Directory.CreateDirectory(appDataFolder);

            UpdateProgress("Dosyalar kopyalanıyor...", 20);
            
            // 1. Copy executable
            if (!currentExePath.Equals(officialExePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExePath, officialExePath, true);
            }

            // 2. Try copying pdb if exists
            string currentDir = Path.GetDirectoryName(currentExePath) ?? "";
            if (!string.IsNullOrEmpty(currentDir))
            {
                var pdbPath = Path.Combine(currentDir, "MistikLauncher.pdb");
                if (File.Exists(pdbPath))
                {
                    try { File.Copy(pdbPath, Path.Combine(appDataFolder, "MistikLauncher.pdb"), true); } catch { }
                }
            }

            UpdateProgress("Kısayollar oluşturuluyor...", 50);

            // 3. Create Shortcuts without PowerShell (Vanguard safe)
            CreateShortcut(Environment.SpecialFolder.Desktop, "Mistik Launcher.lnk", officialExePath, appDataFolder);
            CreateShortcut(Environment.SpecialFolder.StartMenu, "Mistik Launcher.lnk", officialExePath, appDataFolder);

            UpdateProgress("Denetim Masasına kaydediliyor...", 80);

            // 4. Registry Integration for Control Panel (Uninstall)
            RegisterUninstall(officialExePath, appDataFolder);

            UpdateProgress("Tamamlanıyor...", 100);
            System.Threading.Thread.Sleep(500); // Visual delay
        }

        private void CreateShortcut(Environment.SpecialFolder folder, string linkName, string targetPath, string workDir)
        {
            try
            {
                string folderPath = Environment.GetFolderPath(folder);
                string linkPath = Path.Combine(folderPath, linkName);

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(linkPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workDir;
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Save();
                }
            }
            catch { }
        }

        private void RegisterUninstall(string exePath, string appDir)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient");
                if (key != null)
                {
                    key.SetValue("DisplayName", "Mistik Launcher");
                    key.SetValue("DisplayIcon", exePath + ",0");
                    key.SetValue("DisplayVersion", "5.0.0");
                    key.SetValue("Publisher", "Mistik");
                    key.SetValue("InstallLocation", appDir);
                    // Use double quotes for the exe path in the command
                    key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
                    key.SetValue("NoModify", 1);
                    key.SetValue("NoRepair", 1);
                }
            }
            catch { }
        }

        private void UpdateProgress(string text, int value)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = text;
                InstallProgress.Value = value;
            });
        }
    }
}
