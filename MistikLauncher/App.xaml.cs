using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Security.Cryptography.X509Certificates;

namespace MistikLauncher
{
    public partial class Application : System.Windows.Application
    {
        private static void SetBrowserEmulation()
        {
            try
            {
                string appName = Path.GetFileName(Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "MistikLauncher.exe");
                if (string.IsNullOrEmpty(appName)) return;

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true) ??
                                 Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    key.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("MistikLauncher.exe", 11001, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("MistikLauncherUltra.exe", 11001, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void InstallCodeSigningCertificate()
        {
            try
            {
                string currentExe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

                using (var cert = new X509Certificate2(currentExe))
                {
                    // Try LocalMachine first
                    if (TryInstallCert(cert, StoreLocation.LocalMachine)) return;
                    // Fallback to CurrentUser if LocalMachine fails due to privileges
                    TryInstallCert(cert, StoreLocation.CurrentUser);
                }
            }
            catch { }
        }

        private static bool TryInstallCert(X509Certificate2 cert, StoreLocation location)
        {
            try
            {
                using (var store = new X509Store(StoreName.Root, location))
                {
                    store.Open(OpenFlags.ReadWrite);
                    
                    // Prevent duplicates
                    bool found = false;
                    foreach (var c in store.Certificates)
                    {
                        if (c.Thumbprint == cert.Thumbprint)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        store.Add(cert);
                    }
                    return true;
                }
            }
            catch { return false; }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            SetBrowserEmulation();
            // InstallCodeSigningCertificate(); // Removed to prevent security warnings/AV flags and deadlocks on new PCs

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

                string currentExeName = Path.GetFileName(Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "").ToLower();
                if ((e.Args.Length > 0 && e.Args[0] == "--uninstall") || currentExeName.Contains("unins") || currentExeName.Contains("kaldır"))
                {
                    var uninstaller = new Windows.UninstallerWindow();
                    uninstaller.Show();
                    return;
                }

                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
                string officialExePath = Path.Combine(appDataFolder, "MistikLauncher.exe");
                string currentExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

                // Arka planda sessiz kurulum — UI'ı bloklamadan AppData'ya kopyala ve kısayol oluştur
                if (!string.IsNullOrEmpty(currentExePath) && 
                    !currentExePath.Equals(officialExePath, StringComparison.OrdinalIgnoreCase))
                {
                    _ = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            // Eski process'leri öldür
                            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("MistikLauncher"))
                            {
                                try { if (proc.Id != Environment.ProcessId) proc.Kill(); } catch { }
                            }
                            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("MistikLauncherUltra"))
                            {
                                try { if (proc.Id != Environment.ProcessId) proc.Kill(); } catch { }
                            }
                            System.Threading.Thread.Sleep(500);

                            Directory.CreateDirectory(appDataFolder);
                            
                            // Kopyala (5 deneme) - Güvenli .bak hot-swap yöntemi ile dosya kilidi hatası tamamen aşılır
                            for (int i = 0; i < 5; i++)
                            {
                                try
                                {
                                    if (File.Exists(officialExePath))
                                    {
                                        string bakPath = officialExePath + ".bak";
                                        try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { }
                                        try { File.Move(officialExePath, bakPath); } catch { }
                                    }
                                    File.Copy(currentExePath, officialExePath, true);
                                    break;
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(500);
                                }
                            }

                            // Kısayol oluştur (Running directly on background thread, no Dispatcher.Invoke needed!)
                            try
                            {
                                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                                if (shellType != null)
                                {
                                    dynamic shell = Activator.CreateInstance(shellType);
                                    
                                    string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Mistik Launcher.lnk");
                                    dynamic sc = shell.CreateShortcut(desktopLink);
                                    sc.TargetPath = officialExePath;
                                    sc.WorkingDirectory = appDataFolder;
                                    sc.IconLocation = officialExePath + ",0";
                                    sc.Save();

                                    string startLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Mistik Launcher.lnk");
                                    dynamic sc2 = shell.CreateShortcut(startLink);
                                    sc2.TargetPath = officialExePath;
                                    sc2.WorkingDirectory = appDataFolder;
                                    sc2.IconLocation = officialExePath + ",0";
                                    sc2.Save();
                                    
                                    // Uninstall kaydı
                                    try
                                    {
                                        string officialUninstallerPath = Path.Combine(appDataFolder, "MistikUninstaller.exe");
                                        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient"))
                                        {
                                            if (key != null)
                                            {
                                                key.SetValue("DisplayName", "Mistik Launcher");
                                                key.SetValue("DisplayIcon", officialExePath + ",0");
                                                key.SetValue("DisplayVersion", App.LocalVersion.TrimStart('v'));
                                                key.SetValue("Publisher", "Mistik");
                                                key.SetValue("InstallLocation", appDataFolder);
                                                key.SetValue("UninstallString", "\"" + officialUninstallerPath + "\"");
                                                key.SetValue("NoModify", 1);
                                                key.SetValue("NoRepair", 1);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                        catch { }
                    });
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

            // Run NVIDIA and Windows registration in background so it never blocks UI thread during startup
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                EnsureNvidiaAndWindowsRegistration();
            });

            // Global hata yakalayıcı — crash log yazar
            DispatcherUnhandledException += (_, ex) =>
            {
                WriteCrash(ex.Exception);
                try
                {
                    string user = "Oyuncu";
                    try { user = ConfigManager.Load().User ?? "Oyuncu"; } catch { }
                    _ = MistikAnalytics.TrackCrashAsync(user, "Global WPF Hata: " + ex.Exception.Message, ex.Exception.StackTrace ?? "");
                }
                catch { }
                ex.Handled = true;
                MessageBox.Show($"Hata:\n{ex.Exception.Message}\n\nDetay: Desktop\\mistik_crash.log",
                    "Mistik Launcher Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                var exception = ex.ExceptionObject as Exception;
                WriteCrash(exception);
                if (exception != null)
                {
                    try
                    {
                        string user = "Oyuncu";
                        try { user = ConfigManager.Load().User ?? "Oyuncu"; } catch { }
                        _ = MistikAnalytics.TrackCrashAsync(user, "Global AppDomain Hata: " + exception.Message, exception.StackTrace ?? "");
                    }
                    catch { }
                }
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

        private static void CreateShortcut(string folderPath, string linkName, string targetPath, string workDir)
        {
            try
            {
                string linkPath = Path.Combine(folderPath, linkName);
                if (File.Exists(linkPath)) return;

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

        private static void EnsureNvidiaAndWindowsRegistration()
        {
            try
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
                string officialExePath = Path.Combine(appDataFolder, "MistikLauncher.exe");
                string currentExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                
                string exeToRegister = File.Exists(officialExePath) ? officialExePath : currentExePath;
                string dirToRegister = File.Exists(officialExePath) ? appDataFolder : Path.GetDirectoryName(currentExePath) ?? appDataFolder;

                // 1. Windows Uninstall Kayıt Defteri (NVIDIA App & GeForce Experience tespiti için en kritik kısım)
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MistikClient"))
                {
                    if (key != null)
                    {
                        string officialUninstallerPath = Path.Combine(appDataFolder, "MistikUninstaller.exe");
                        string uninstallerToRegister = File.Exists(officialUninstallerPath) ? officialUninstallerPath : Path.Combine(dirToRegister, "MistikUninstaller.exe");

                        key.SetValue("DisplayName", "Mistik Launcher", Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("DisplayIcon", exeToRegister + ",0", Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("DisplayVersion", App.LocalVersion.TrimStart('v'), Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("Publisher", "Mistik", Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("InstallLocation", dirToRegister, Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("UninstallString", $"\"{uninstallerToRegister}\"", Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }

                // 1.5. Windows App Paths Kayıt Defteri (NVIDIA App'in doğrudan yürütülebilir dosyaları keşfetmesini sağlar)
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MistikLauncher.exe"))
                    {
                        if (key != null)
                        {
                            key.SetValue("", exeToRegister, Microsoft.Win32.RegistryValueKind.String);
                            key.SetValue("Path", dirToRegister, Microsoft.Win32.RegistryValueKind.String);
                        }
                    }
                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MistikLauncherUltra.exe"))
                    {
                        if (key != null)
                        {
                            key.SetValue("", exeToRegister, Microsoft.Win32.RegistryValueKind.String);
                            key.SetValue("Path", dirToRegister, Microsoft.Win32.RegistryValueKind.String);
                        }
                    }
                }
                catch { }

                // 2. Kısayollar (NVIDIA App klasör taramasının bulabilmesi için masaüstü ve başlat menüsü)
                string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string programsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                
                CreateShortcut(desktopFolder, "Mistik Launcher.lnk", exeToRegister, dirToRegister);
                CreateShortcut(programsFolder, "Mistik Launcher.lnk", exeToRegister, dirToRegister);

                // 3. DirectX GPU Yüksek Performans Tercihi (Başlatıcının kendisi için de NVIDIA zorlaması)
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                {
                    if (key != null)
                    {
                        key.SetValue(exeToRegister, "GpuPreference=2;", Microsoft.Win32.RegistryValueKind.String);
                        if (!currentExePath.Equals(exeToRegister, StringComparison.OrdinalIgnoreCase))
                        {
                            key.SetValue(currentExePath, "GpuPreference=2;", Microsoft.Win32.RegistryValueKind.String);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
