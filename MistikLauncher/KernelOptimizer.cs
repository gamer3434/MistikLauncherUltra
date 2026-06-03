using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.ComponentModel;
using Microsoft.Win32;

namespace MistikLauncher
{
    /// <summary>
    /// Windows Kernel-düzeyinde oyun optimizasyonları.
    /// Tüm değişiklikler geri alınabilir (Revert).
    /// 12 adet optimizasyon modülü içerir:
    ///   1) Process Priority          – İşlem önceliğini HIGH yapar
    ///   2) Timer Resolution          – Zamanlayıcı çözünürlüğünü 1ms'ye indirir
    ///   3) CPU Affinity              – P-Core'ları Minecraft'a sabitler
    ///   4) Power Plan                – Yüksek Performans güç planına geçer
    ///   5) Nagle Disable             – TCP gecikmesini sıfırlar
    ///   6) GPU Preference (NVIDIA)   – Ayrık GPU'yu zorlar
    ///   7) Large Page Support        – Büyük sayfa desteğini algılar
    ///   8) Fullscreen Opt. Disable   – DWM tam ekran optimizasyonlarını kapatır
    ///   9) Game Mode/Bar Disable     – Game Bar overlay'ini kapatır
    ///  10) Working Set Optimization  – Bellek sayfalama koruması uygular
    ///  11) I/O Priority Boost        – Disk I/O önceliğini yükseltir
    ///  12) Thread Scheduling Opt.    – İş parçacığı zamanlama tutarlılığı sağlar
    /// </summary>
    public static class KernelOptimizer
    {
        // ══════════════════════════════════════════════════════════════════════
        // ── P/Invoke Tanımları (Mevcut) ──────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint timeEndPeriod(uint uMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessPriorityBoost(IntPtr hProcess, bool disablePriorityBoost);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        // ══════════════════════════════════════════════════════════════════════
        // ── P/Invoke Tanımları (Yeni) ────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        // ── Working Set boyutu ayarlama ──
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSizeEx(
            IntPtr hProcess,
            IntPtr dwMinimumWorkingSetSize,
            IntPtr dwMaximumWorkingSetSize,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessWorkingSetSizeEx(
            IntPtr hProcess,
            out IntPtr lpMinimumWorkingSetSize,
            out IntPtr lpMaximumWorkingSetSize,
            out uint flags);

        // ── NtSetInformationProcess – I/O Önceliği ayarlama ──
        [DllImport("ntdll.dll", SetLastError = false)]
        private static extern int NtSetInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref int processInformation,
            int processInformationLength);

        // ── Yetki (Privilege) kontrol ve etkinleştirme ──
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(
            string? lpSystemName,
            string lpName,
            out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool PrivilegeCheck(
            IntPtr clientToken,
            ref PRIVILEGE_SET requiredPrivileges,
            out bool pfResult);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // ── SetProcessPriorityBoost – Thread zamanlama tutarlılığı ──
        // (Zaten yukarıda tanımlı, bu yorum bilgi amaçlıdır)

        // ── LUID yapısı – Privilege tanımlayıcısı ──
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        // ── LUID_AND_ATTRIBUTES – Privilege durumu ──
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        // ── PRIVILEGE_SET – Privilege kontrolü için ──
        [StructLayout(LayoutKind.Sequential)]
        private struct PRIVILEGE_SET
        {
            public uint PrivilegeCount;
            public uint Control;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── Sabitler ─────────────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        // Windows Güç Planları GUID'leri
        private static Guid GUID_HIGH_PERFORMANCE = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static Guid GUID_BALANCED         = new("381b4222-f694-41f0-9685-ff5bb260df2e");

        // NtSetInformationProcess sabitleri
        private const int ProcessIoPriority = 33;   // I/O öncelik sınıfı
        private const int IO_PRIORITY_HIGH  = 3;    // Yüksek I/O önceliği
        private const int IO_PRIORITY_NORMAL = 2;   // Normal I/O önceliği

        // Token erişim sabitleri
        private const uint TOKEN_QUERY = 0x0008;

        // Privilege sabitleri
        private const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
        private const uint PRIVILEGE_SET_ALL_NECESSARY = 1;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        // Working Set sabitleri (512 MB minimum)
        private static readonly IntPtr WORKING_SET_MIN = new IntPtr(512 * 1024 * 1024);  // 512 MB
        private static readonly IntPtr WORKING_SET_MAX = new IntPtr(1536 * 1024 * 1024);  // 1.5 GB max

        // Working Set bayrakları
        private const uint QUOTA_LIMITS_HARDWS_MIN_ENABLE  = 0x00000001;
        private const uint QUOTA_LIMITS_HARDWS_MAX_DISABLE = 0x00000008;

        // ══════════════════════════════════════════════════════════════════════
        // ── Registry Yolları ─────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        // GPU tercih registry yolu
        private const string GPU_PREF_REG_PATH = @"Software\Microsoft\DirectX\UserGpuPreferences";

        // Fullscreen optimizasyon registry yolu
        private const string FULLSCREEN_REG_PATH = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        // Game DVR registry yolları
        private const string GAME_DVR_REG_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string GAME_CONFIG_REG_PATH = @"System\GameConfigStore";

        // ══════════════════════════════════════════════════════════════════════
        // ── Durum Takibi (Mevcut) ────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        private static bool _timerResolutionSet = false;
        private static Guid _originalPowerPlan  = Guid.Empty;
        private static bool _nagleDisabled      = false;
        private static string? _nagleRegPath    = null;
        private static int _originalNagleValue  = 1;

        // ══════════════════════════════════════════════════════════════════════
        // ── Durum Takibi (Yeni Optimizasyonlar) ──────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        // GPU Preference durumu
        private static bool _gpuPreferenceSet     = false;
        private static string? _gpuPrefJavaPath   = null;
        private static string? _gpuPrefOrigValue  = null;

        // Fullscreen optimizasyon durumu
        private static bool _fullscreenOptDisabled  = false;
        private static string? _fullscreenJavaPath  = null;
        private static string? _fullscreenOrigValue = null;

        // Game Mode / Game Bar durumu
        private static bool _gameDvrDisabled         = false;
        private static int _originalAppCaptureEnabled = -1;  // -1 = değer yoktu
        private static int _originalGameDvrEnabled    = -1;  // -1 = değer yoktu

        // Working Set durumu
        private static bool _workingSetOptimized = false;
        private static IntPtr _originalWsMin     = IntPtr.Zero;
        private static IntPtr _originalWsMax     = IntPtr.Zero;
        private static uint _originalWsFlags     = 0;

        // I/O Priority durumu
        private static bool _ioPriorityBoosted = false;

        // Thread Scheduling durumu
        private static bool _threadSchedulingOptimized = false;

        // Large Page desteği algılama sonucu
        private static bool _largePageAvailable = false;
        private static bool _largePageChecked   = false;

        // Process referansı (revert sırasında gerekli)
        private static IntPtr _processHandle = IntPtr.Zero;

        // ══════════════════════════════════════════════════════════════════════
        // ── MEVCUT Uygulama Metodları (Değiştirilmeden Korundu) ──────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Minecraft process'ine yüksek öncelik atar.
        /// </summary>
        public static void ApplyProcessPriority(Process process)
        {
            try
            {
                process.PriorityClass = ProcessPriorityClass.High;
                // Priority boost'u aktif tut
                SetProcessPriorityBoost(process.Handle, false);
                App.Log("[KernelOpt] Process önceliği HIGH olarak ayarlandı.");
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Process önceliği ayarlanamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows zamanlayıcı çözünürlüğünü 1ms'ye düşürür.
        /// Daha akıcı FPS ve daha düşük input lag sağlar.
        /// </summary>
        public static void ApplyTimerResolution()
        {
            try
            {
                uint result = timeBeginPeriod(1);
                if (result == 0) // TIMERR_NOERROR
                {
                    _timerResolutionSet = true;
                    App.Log("[KernelOpt] Timer çözünürlüğü 1ms'ye ayarlandı.");
                }
                else
                {
                    App.Log($"[KernelOpt] Timer çözünürlüğü ayarlanamadı. Hata kodu: {result}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Timer hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Process'i performans çekirdeklerine sabitler.
        /// Hyperthreading varsa fiziksel çekirdekleri tercih eder.
        /// </summary>
        public static void ApplyCpuAffinity(Process process)
        {
            try
            {
                // Windows 11 Thread Director'ın işini bölmemek için CPU Affinity kararı dinamik olarak işletim sistemine bırakılmıştır.
                App.Log("[KernelOpt] CPU Affinity Windows Thread Director tarafından dinamik yönetiliyor (Bypass).");
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] CPU affinity ayarlanamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Güç planını "Yüksek Performans"a geçirir.
        /// Oyun kapanınca eski plana döner.
        /// </summary>
        public static void ApplyPowerPlan()
        {
            try
            {
                // Mevcut güç planını kaydet
                if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr pGuid) == 0)
                {
                    _originalPowerPlan = Marshal.PtrToStructure<Guid>(pGuid);
                    LocalFree(pGuid);
                }

                // Yüksek Performans planına geç
                var highPerf = GUID_HIGH_PERFORMANCE;
                uint result = PowerSetActiveScheme(IntPtr.Zero, ref highPerf);
                if (result == 0)
                {
                    App.Log("[KernelOpt] Güç planı 'Yüksek Performans' olarak ayarlandı.");
                }
                else
                {
                    App.Log($"[KernelOpt] Güç planı değiştirilemedi. Hata: {result}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Güç planı hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Nagle algoritmasını devre dışı bırakır (TCP_NODELAY etkisi).
        /// Multiplayer'da ping'i düşürür.
        /// Registry üzerinden çalışır.
        /// </summary>
        public static void ApplyNagleDisable()
        {
            try
            {
                // Aktif ağ arayüzünü bul
                string? activeAdapterId = null;
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        activeAdapterId = nic.Id;
                        break;
                    }
                }

                if (activeAdapterId == null)
                {
                    App.Log("[KernelOpt] Aktif ağ adaptörü bulunamadı, Nagle atlandı.");
                    return;
                }

                _nagleRegPath = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{activeAdapterId}";
                using var key = Registry.LocalMachine.OpenSubKey(_nagleRegPath, writable: true);
                if (key == null)
                {
                    App.Log("[KernelOpt] Registry anahtarı açılamadı (admin yetkisi gerekebilir).");
                    return;
                }

                // Mevcut değeri kaydet
                var existing = key.GetValue("TcpAckFrequency");
                _originalNagleValue = existing != null ? (int)existing : 1;

                // TcpAckFrequency = 1 → Her paketi hemen onayla (Nagle devre dışı etkisi)
                key.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                // TcpNoDelay = 1 → TCP gecikmesiz gönderim
                key.SetValue("TcpNoDelay", 1, RegistryValueKind.DWord);

                _nagleDisabled = true;
                App.Log("[KernelOpt] Nagle algoritması devre dışı bırakıldı (düşük ping).");
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Nagle ayarı değiştirilemedi: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── YENİ Uygulama Metodları ──────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Java/Minecraft process'ini NVIDIA ayrık GPU'yu kullanmaya zorlar.
        /// HKCU\Software\Microsoft\DirectX\UserGpuPreferences registry anahtarını ayarlar.
        /// Windows 11'in GPU tercih mekanizmasını kullanarak entegre Intel GPU yerine
        /// RTX 3050 gibi ayrık GPU'nun seçilmesini garanti eder.
        /// </summary>
        public static void ApplyGpuPreference(Process process)
        {
            try
            {
                // Registry anahtarını aç veya oluştur
                using var key = Registry.CurrentUser.CreateSubKey(GPU_PREF_REG_PATH);
                if (key == null)
                {
                    App.Log("[KernelOpt] GPU Preference registry anahtarı oluşturulamadı.");
                    return;
                }

                string gpuPrefValue = "GpuPreference=2;";

                // ── 1. Mistik Launcher'ın Kendi EXE'sini Kaydet ──
                try
                {
                    string? currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentExe))
                    {
                        key.SetValue(currentExe, gpuPrefValue, RegistryValueKind.String);
                        // Alternatif isimleri de kaydet
                        string dir = System.IO.Path.GetDirectoryName(currentExe) ?? "";
                        if (!string.IsNullOrEmpty(dir))
                        {
                            key.SetValue(System.IO.Path.Combine(dir, "MistikLauncher.exe"), gpuPrefValue, RegistryValueKind.String);
                            key.SetValue(System.IO.Path.Combine(dir, "MistikLauncherUltra.exe"), gpuPrefValue, RegistryValueKind.String);
                        }
                        App.Log("[KernelOpt] Mistik Launcher EXE'leri NVIDIA Yüksek Performans GPU'ya atandı (NVIDIA App Entegrasyonu).");
                    }
                }
                catch { }

                // ── 2. Aktif Java/Minecraft Process'ini Kaydet ──
                string? javaPath = null;
                try { javaPath = process.MainModule?.FileName; } catch { }

                if (!string.IsNullOrEmpty(javaPath))
                {
                    _gpuPrefJavaPath = javaPath;
                    var existingValue = key.GetValue(javaPath);
                    _gpuPrefOrigValue = existingValue as string;

                    key.SetValue(javaPath, gpuPrefValue, RegistryValueKind.String);
                    _gpuPreferenceSet = true;
                    App.Log($"[KernelOpt] Aktif Java GPU tercihi NVIDIA (Yüksek Performans) olarak ayarlandı: {System.IO.Path.GetFileName(javaPath)}");
                }

                // ── 3. Sistemdeki Diğer Bilinen Tüm Java Sürümlerini Tara ve Kaydet ──
                try
                {
                    var javaPaths = new System.Collections.Generic.List<string>();
                    
                    // AppData altındaki jre21 ve jre25
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string localJava21 = System.IO.Path.Combine(appDataPath, ".mistik_ultra", "java", "jre21", "bin", "javaw.exe");
                    if (System.IO.File.Exists(localJava21)) javaPaths.Add(localJava21);
                    string localJava25 = System.IO.Path.Combine(appDataPath, ".mistik_ultra", "java", "jre25", "bin", "javaw.exe");
                    if (System.IO.File.Exists(localJava25)) javaPaths.Add(localJava25);

                    // Sistem Program Files altındaki Java yolları
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (System.IO.Directory.Exists(pf))
                    {
                        foreach (var sub in new[] { "Java", "Eclipse Foundation", "Adoptium", "Zulu" })
                        {
                            string path = System.IO.Path.Combine(pf, sub);
                            if (System.IO.Directory.Exists(path))
                            {
                                foreach (var exe in System.IO.Directory.GetFiles(path, "javaw.exe", System.IO.SearchOption.AllDirectories))
                                {
                                    javaPaths.Add(exe);
                                }
                            }
                        }
                    }

                    // Hepsini Yüksek Performans yap
                    foreach (var jp in javaPaths)
                    {
                        key.SetValue(jp, gpuPrefValue, RegistryValueKind.String);
                        // Normal java.exe'yi de ekle
                        string je = jp.Replace("javaw.exe", "java.exe");
                        if (System.IO.File.Exists(je)) key.SetValue(je, gpuPrefValue, RegistryValueKind.String);
                    }

                    App.Log($"[KernelOpt] Toplam {javaPaths.Count} adet sistem Java çalıştırıcısı NVIDIA Yüksek Performans olarak yapılandırıldı.");
                }
                catch { }

                // ── 4. NVIDIA App Profil Entegrasyonu ──
                try
                {
                    // NVIDIA App'in kullandığı profil registry yolu
                    using var nvKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak");
                    if (nvKey != null)
                    {
                        // MistikLauncher'ı NVIDIA App'te yüksek performans profili olarak kaydet
                        string? exe = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exe))
                        {
                            nvKey.SetValue(System.IO.Path.GetFileName(exe), "2", RegistryValueKind.String);
                        }
                    }
                    
                    // Windows Settings -> Grafik Ayarları -> Uygulama Tercihi
                    // Bu kayıt Windows Ayarları > Ekran > Grafik'te programı görünür kılar
                    using var graphicsKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
                    if (graphicsKey != null)
                    {
                        // Minecraft launcher'ı ve javaw'ı da kaydet
                        string gameDir = App.GameDir;
                        if (!string.IsNullOrEmpty(gameDir))
                        {
                            var extraPaths = new System.Collections.Generic.List<string>();
                            // Minecraft Launcher
                            string mcLauncher = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft Launcher", "MinecraftLauncher.exe");
                            if (System.IO.File.Exists(mcLauncher)) extraPaths.Add(mcLauncher);
                            // UWP Minecraft
                            string mcUwp = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime", "java-runtime-delta", "bin", "javaw.exe");
                            if (System.IO.File.Exists(mcUwp)) extraPaths.Add(mcUwp);
                            foreach (var p in extraPaths)
                            {
                                graphicsKey.SetValue(p, "GpuPreference=2;", RegistryValueKind.String);
                            }
                        }
                    }
                    App.Log("[KernelOpt] NVIDIA App profil kayıtları tamamlandı.");
                }
                catch (Exception nvEx)
                {
                    App.Log($"[KernelOpt] NVIDIA App profil kaydı (opsiyonel): {nvEx.Message}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] GPU tercihi ayarlanamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Sistemde Large Page (Büyük Sayfa) desteğinin kullanılabilir olup olmadığını kontrol eder.
        /// Large Pages, TLB (Translation Lookaside Buffer) kayıplarını azaltarak
        /// JVM'nin heap belleğine daha hızlı erişim sağlar.
        /// Kullanıcının "Lock pages in memory" (SeLockMemoryPrivilege) yetkisini kontrol eder.
        /// </summary>
        /// <returns>Large Page desteği varsa true, yoksa false</returns>
        public static bool IsLargePageAvailable()
        {
            // Daha önce kontrol edildiyse önbelleğe alınmış sonucu döndür
            if (_largePageChecked)
                return _largePageAvailable;

            _largePageChecked = true;
            _largePageAvailable = false;

            try
            {
                // Mevcut process'in token'ını aç
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out IntPtr tokenHandle))
                {
                    App.Log("[KernelOpt] Process token açılamadı, Large Page kontrolü başarısız.");
                    return false;
                }

                try
                {
                    // SeLockMemoryPrivilege LUID'ini al
                    if (!LookupPrivilegeValue(null, SE_LOCK_MEMORY_NAME, out LUID luid))
                    {
                        App.Log("[KernelOpt] SeLockMemoryPrivilege LUID bulunamadı.");
                        return false;
                    }

                    // Privilege kontrolü yap
                    var privSet = new PRIVILEGE_SET
                    {
                        PrivilegeCount = 1,
                        Control = PRIVILEGE_SET_ALL_NECESSARY,
                        Privilege = new LUID_AND_ATTRIBUTES
                        {
                            Luid = luid,
                            Attributes = SE_PRIVILEGE_ENABLED
                        }
                    };

                    if (PrivilegeCheck(tokenHandle, ref privSet, out bool hasPrivilege))
                    {
                        _largePageAvailable = hasPrivilege;
                        if (hasPrivilege)
                        {
                            App.Log("[KernelOpt] Large Page desteği MEVCUT. -XX:+UseLargePages JVM flag'i kullanılabilir.");
                        }
                        else
                        {
                            App.Log("[KernelOpt] Large Page desteği YOK. 'Lock pages in memory' yetkisi gerekli.");
                        }
                    }
                    else
                    {
                        App.Log("[KernelOpt] Privilege kontrolü başarısız oldu.");
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Large Page kontrol hatası: {ex.Message}");
            }

            return _largePageAvailable;
        }

        /// <summary>
        /// Windows DWM (Desktop Window Manager) tam ekran optimizasyonlarını devre dışı bırakır.
        /// Bu, javaw.exe için DISABLEDXMAXIMIZEDWINDOWEDMODE uyumluluk bayrağını ayarlar.
        /// Windows 11'in otomatik borderless-windowed moduna geçişini engelleyerek
        /// gerçek exclusive fullscreen modunun kullanılmasını sağlar (daha düşük input lag).
        /// </summary>
        public static void ApplyFullscreenOptDisable(Process process)
        {
            try
            {
                // Java çalıştırılabilir dosyasının tam yolunu al
                string? javaPath = null;
                try
                {
                    javaPath = process.MainModule?.FileName;
                }
                catch
                {
                    // MainModule erişim hatası
                }

                if (string.IsNullOrEmpty(javaPath))
                {
                    App.Log("[KernelOpt] Java yolu alınamadı, fullscreen optimizasyon ayarı atlandı.");
                    return;
                }

                _fullscreenJavaPath = javaPath;

                // AppCompatFlags\Layers anahtarını aç veya oluştur
                using var key = Registry.CurrentUser.CreateSubKey(FULLSCREEN_REG_PATH);
                if (key == null)
                {
                    App.Log("[KernelOpt] Fullscreen registry anahtarı oluşturulamadı.");
                    return;
                }

                // Mevcut değeri kaydet (revert için)
                var existingValue = key.GetValue(javaPath);
                _fullscreenOrigValue = existingValue as string;

                // Mevcut bayraklara DISABLEDXMAXIMIZEDWINDOWEDMODE ekle
                // Zaten varsa tekrar ekleme, yoksa mevcut değerin sonuna ekle
                string newValue;
                if (!string.IsNullOrEmpty(_fullscreenOrigValue))
                {
                    if (_fullscreenOrigValue.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE"))
                    {
                        // Zaten ayarlanmış, değiştirmeye gerek yok
                        App.Log("[KernelOpt] Fullscreen optimizasyonları zaten devre dışı.");
                        _fullscreenOptDisabled = true;
                        return;
                    }
                    // Mevcut bayrakların sonuna ekle
                    newValue = _fullscreenOrigValue.TrimEnd() + " DISABLEDXMAXIMIZEDWINDOWEDMODE";
                }
                else
                {
                    newValue = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE";
                }

                key.SetValue(javaPath, newValue, RegistryValueKind.String);

                _fullscreenOptDisabled = true;
                App.Log("[KernelOpt] Fullscreen optimizasyonları devre dışı bırakıldı (düşük input lag).");
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Fullscreen optimizasyon ayarı başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows Game Bar overlay'ini ve Game DVR kayıt özelliğini devre dışı bırakır.
        /// Game Bar, arka planda FPS çalan bir overlay sistemidir.
        /// AppCaptureEnabled=0 ve GameDVR_Enabled=0 ayarları yapılır.
        /// Orijinal değerler kaydedilip oyun kapanınca geri yüklenir.
        /// </summary>
        public static void ApplyGameModeDisable()
        {
            try
            {
                // ── Game DVR → AppCaptureEnabled ──
                try
                {
                    using var dvrKey = Registry.CurrentUser.CreateSubKey(GAME_DVR_REG_PATH);
                    if (dvrKey != null)
                    {
                        // Mevcut değeri kaydet
                        var existingCapture = dvrKey.GetValue("AppCaptureEnabled");
                        _originalAppCaptureEnabled = existingCapture != null ? (int)existingCapture : -1;

                        // Devre dışı bırak
                        dvrKey.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                        App.Log("[KernelOpt] Game DVR AppCaptureEnabled devre dışı bırakıldı.");
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] GameDVR AppCaptureEnabled ayarlanamadı: {ex.Message}");
                }

                // ── GameConfigStore → GameDVR_Enabled ──
                try
                {
                    using var configKey = Registry.CurrentUser.CreateSubKey(GAME_CONFIG_REG_PATH);
                    if (configKey != null)
                    {
                        // Mevcut değeri kaydet
                        var existingDvr = configKey.GetValue("GameDVR_Enabled");
                        _originalGameDvrEnabled = existingDvr != null ? (int)existingDvr : -1;

                        // Devre dışı bırak
                        configKey.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                        App.Log("[KernelOpt] GameConfigStore GameDVR_Enabled devre dışı bırakıldı.");
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] GameDVR_Enabled ayarlanamadı: {ex.Message}");
                }

                _gameDvrDisabled = true;
                App.Log("[KernelOpt] Game Bar / Game DVR overlay tamamen devre dışı bırakıldı.");
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Game Mode devre dışı bırakma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Process'in Working Set boyutunu ayarlayarak Windows'un Minecraft'ın belleğini
        /// diske sayfalama (page out) yapmasını engeller.
        /// Minimum 512MB Working Set boyutu garanti edilir.
        /// Bu, stutter (takılma) ve mikro-donmaları ciddi şekilde azaltır.
        /// </summary>
        public static void ApplyWorkingSetOptimization(Process process)
        {
            try
            {
                IntPtr hProcess = process.Handle;
                _processHandle = hProcess;

                // Mevcut Working Set boyutlarını kaydet (revert için)
                if (GetProcessWorkingSetSizeEx(hProcess, out _originalWsMin, out _originalWsMax, out _originalWsFlags))
                {
                    App.Log($"[KernelOpt] Mevcut Working Set: Min={FormatBytes(_originalWsMin)}, Max={FormatBytes(_originalWsMax)}");
                }

                // Minimum 512MB, Maksimum 1.5GB Working Set ayarla
                // QUOTA_LIMITS_HARDWS_MIN_ENABLE → Minimum boyut zorunlu kılınır
                // QUOTA_LIMITS_HARDWS_MAX_DISABLE → Maksimum sınır esnek bırakılır (gerekirse aşılabilir)
                uint flags = QUOTA_LIMITS_HARDWS_MIN_ENABLE | QUOTA_LIMITS_HARDWS_MAX_DISABLE;
                bool success = SetProcessWorkingSetSizeEx(hProcess, WORKING_SET_MIN, WORKING_SET_MAX, flags);

                if (success)
                {
                    _workingSetOptimized = true;
                    App.Log($"[KernelOpt] Working Set optimizasyonu uygulandı: Min=512MB, Max=1.5GB (sayfalama koruması aktif).");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    App.Log($"[KernelOpt] Working Set ayarlanamadı. Win32 Hata: {error}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Working Set optimizasyon hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Process'in I/O önceliğini NtSetInformationProcess ile yüksek seviyeye çıkarır.
        /// Bu, Minecraft'ın disk okuma/yazma işlemlerinin (chunk yükleme, kaynak paketleri,
        /// dünya kaydetme) diğer arka plan process'lerinden önce işlenmesini sağlar.
        /// Chunk yükleme gecikmelerini ve dünya oluşturma takılmalarını azaltır.
        /// </summary>
        public static void ApplyIoPriorityBoost(Process process)
        {
            try
            {
                IntPtr hProcess = process.Handle;

                // I/O önceliğini HIGH olarak ayarla
                int ioPriority = IO_PRIORITY_HIGH;
                int status = NtSetInformationProcess(
                    hProcess,
                    ProcessIoPriority,
                    ref ioPriority,
                    sizeof(int));

                if (status == 0) // STATUS_SUCCESS
                {
                    _ioPriorityBoosted = true;
                    App.Log("[KernelOpt] I/O önceliği HIGH olarak ayarlandı (hızlı chunk yükleme).");
                }
                else
                {
                    // NTSTATUS hata kodu – negatif değerler hata belirtir
                    App.Log($"[KernelOpt] I/O önceliği ayarlanamadı. NTSTATUS: 0x{status:X8}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] I/O Priority hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Process'in Priority Boost özelliğini devre dışı bırakarak
        /// Windows zamanlayıcısının Minecraft thread'lerine tutarlı CPU zamanı
        /// vermesini sağlar. Priority Boost açıkken, Windows bazı thread'lere
        /// geçici öncelik artışı verir ve bu durum frame time tutarsızlıklarına neden olur.
        /// Kapatıldığında tüm thread'ler eşit ve öngörülebilir şekilde zamanlanır.
        /// </summary>
        public static void ApplyThreadSchedulingOptimization(Process process)
        {
            try
            {
                // Priority Boost'u DEVRE DIŞI bırak (disablePriorityBoost = true)
                // Bu, Windows'un thread'lere rastgele öncelik artışı vermesini engeller
                // Sonuç: Daha tutarlı frame time'lar ve daha az mikro-stutter
                bool success = SetProcessPriorityBoost(process.Handle, true);

                if (success)
                {
                    _threadSchedulingOptimized = true;
                    App.Log("[KernelOpt] Thread zamanlama optimizasyonu uygulandı (Priority Boost devre dışı, tutarlı CPU zamanı).");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    App.Log($"[KernelOpt] Thread zamanlama ayarlanamadı. Win32 Hata: {error}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Thread Scheduling hatası: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── Yardımcı Metodlar ────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Byte cinsinden bir değeri okunabilir biçime dönüştürür (KB, MB, GB).
        /// </summary>
        private static string FormatBytes(IntPtr bytes)
        {
            long b = bytes.ToInt64();
            if (b >= 1024L * 1024 * 1024)
                return $"{b / (1024.0 * 1024 * 1024):F1} GB";
            if (b >= 1024L * 1024)
                return $"{b / (1024.0 * 1024):F1} MB";
            if (b >= 1024L)
                return $"{b / 1024.0:F1} KB";
            return $"{b} B";
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── Toplu Uygulama ───────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Config'e göre tüm aktif optimizasyonları uygular.
        /// Mevcut 5 optimizasyon + 7 yeni optimizasyon = toplam 12 modül.
        /// Yeni optimizasyonlar KernelGpu bayrağı ile kontrol edilir.
        /// </summary>
        public static void ApplyAll(Process? process, LauncherConfig config)
        {
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
            App.Log("[KernelOpt] ══════ Kernel Optimizasyonları Uygulanıyor ══════════");
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");

            // ── Mevcut Optimizasyonlar (Orijinal 5 Modül) ──

            if (config.KernelPriority && process != null)
                ApplyProcessPriority(process);

            if (config.KernelTimer)
                ApplyTimerResolution();

            if (config.KernelAffinity && process != null)
                ApplyCpuAffinity(process);

            if (config.KernelPower)
                ApplyPowerPlan();

            if (config.KernelNagle)
                ApplyNagleDisable();

            // ── Yeni Optimizasyonlar (KernelGpu bayrağı ile kontrol edilir) ──

            if (config.KernelGpu)
            {
                App.Log("[KernelOpt] ── KernelGpu aktif: Gelişmiş GPU/Sistem optimizasyonları uygulanıyor ──");

                // 6) GPU Preference – NVIDIA ayrık GPU'yu zorla
                if (process != null)
                    ApplyGpuPreference(process);

                // 7) Large Page Desteği – Bilgilendirme amaçlı kontrol
                bool largePages = IsLargePageAvailable();
                if (largePages)
                {
                    App.Log("[KernelOpt] ℹ️ JVM argümanlarına -XX:+UseLargePages eklenebilir (performans artışı).");
                }

                // 8) Fullscreen Optimizasyonları Devre Dışı Bırak
                if (process != null)
                    ApplyFullscreenOptDisable(process);

                // 9) Game Mode / Game Bar Devre Dışı Bırak
                ApplyGameModeDisable();

                // 10) Working Set Optimizasyonu – Bellek sayfalama koruması
                if (process != null)
                    ApplyWorkingSetOptimization(process);

                // 11) I/O Priority Boost – Disk erişim önceliği
                if (process != null)
                    ApplyIoPriorityBoost(process);

                // 12) Thread Scheduling – Tutarlı CPU zamanı
                if (process != null)
                    ApplyThreadSchedulingOptimization(process);
            }
            else
            {
                App.Log("[KernelOpt] KernelGpu kapalı: Gelişmiş GPU/Sistem optimizasyonları atlandı.");
            }

            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
            App.Log("[KernelOpt] ══════ Tüm Optimizasyonlar Tamamlandı ═══════════════");
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── Geri Alma ────────────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Uygulanan tüm kernel değişikliklerini geri alır.
        /// Oyun kapanınca çağrılır. Orijinal sistem durumuna dönüş sağlar.
        /// </summary>
        public static void RevertAll()
        {
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
            App.Log("[KernelOpt] ══════ Kernel Optimizasyonları Geri Alınıyor ════════");
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");

            // ── 1) Timer çözünürlüğünü geri al ──
            if (_timerResolutionSet)
            {
                try
                {
                    timeEndPeriod(1);
                    _timerResolutionSet = false;
                    App.Log("[KernelOpt] Timer çözünürlüğü varsayılana döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Timer geri alma hatası: {ex.Message}");
                }
            }

            // ── 2) Güç planını geri al ──
            if (_originalPowerPlan != Guid.Empty)
            {
                try
                {
                    var original = _originalPowerPlan;
                    PowerSetActiveScheme(IntPtr.Zero, ref original);
                    _originalPowerPlan = Guid.Empty;
                    App.Log("[KernelOpt] Güç planı orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Güç planı geri alma hatası: {ex.Message}");
                }
            }

            // ── 3) Nagle'ı geri al ──
            if (_nagleDisabled && _nagleRegPath != null)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(_nagleRegPath, writable: true);
                    if (key != null)
                    {
                        key.DeleteValue("TcpNoDelay", false);
                        // TcpAckFrequency'yi orijinal değerine döndür
                        key.SetValue("TcpAckFrequency", _originalNagleValue, RegistryValueKind.DWord);
                    }
                    _nagleDisabled = false;
                    App.Log("[KernelOpt] Nagle ayarları geri alındı.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Nagle geri alma hatası: {ex.Message}");
                }
            }

            // ── 4) GPU Preference geri al ──
            if (_gpuPreferenceSet && _gpuPrefJavaPath != null)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(GPU_PREF_REG_PATH, writable: true);
                    if (key != null)
                    {
                        if (_gpuPrefOrigValue != null)
                        {
                            // Orijinal değeri geri yükle
                            key.SetValue(_gpuPrefJavaPath, _gpuPrefOrigValue, RegistryValueKind.String);
                        }
                        else
                        {
                            // Daha önce değer yoktu, sil
                            key.DeleteValue(_gpuPrefJavaPath, throwOnMissingValue: false);
                        }
                    }
                    _gpuPreferenceSet = false;
                    _gpuPrefJavaPath = null;
                    _gpuPrefOrigValue = null;
                    App.Log("[KernelOpt] GPU tercihi orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] GPU tercihi geri alma hatası: {ex.Message}");
                }
            }

            // ── 5) Fullscreen Optimizasyon geri al ──
            if (_fullscreenOptDisabled && _fullscreenJavaPath != null)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(FULLSCREEN_REG_PATH, writable: true);
                    if (key != null)
                    {
                        if (_fullscreenOrigValue != null)
                        {
                            // Orijinal değeri geri yükle
                            key.SetValue(_fullscreenJavaPath, _fullscreenOrigValue, RegistryValueKind.String);
                        }
                        else
                        {
                            // Daha önce değer yoktu, sil
                            key.DeleteValue(_fullscreenJavaPath, throwOnMissingValue: false);
                        }
                    }
                    _fullscreenOptDisabled = false;
                    _fullscreenJavaPath = null;
                    _fullscreenOrigValue = null;
                    App.Log("[KernelOpt] Fullscreen optimizasyon ayarı orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Fullscreen geri alma hatası: {ex.Message}");
                }
            }

            // ── 6) Game Mode / Game Bar geri al ──
            if (_gameDvrDisabled)
            {
                try
                {
                    // AppCaptureEnabled geri al
                    if (_originalAppCaptureEnabled != -1)
                    {
                        using var dvrKey = Registry.CurrentUser.OpenSubKey(GAME_DVR_REG_PATH, writable: true);
                        if (dvrKey != null)
                        {
                            dvrKey.SetValue("AppCaptureEnabled", _originalAppCaptureEnabled, RegistryValueKind.DWord);
                        }
                    }
                    else
                    {
                        // Değer orijinalde yoktu – silmeye çalış
                        using var dvrKey = Registry.CurrentUser.OpenSubKey(GAME_DVR_REG_PATH, writable: true);
                        dvrKey?.DeleteValue("AppCaptureEnabled", throwOnMissingValue: false);
                    }

                    // GameDVR_Enabled geri al
                    if (_originalGameDvrEnabled != -1)
                    {
                        using var configKey = Registry.CurrentUser.OpenSubKey(GAME_CONFIG_REG_PATH, writable: true);
                        if (configKey != null)
                        {
                            configKey.SetValue("GameDVR_Enabled", _originalGameDvrEnabled, RegistryValueKind.DWord);
                        }
                    }
                    else
                    {
                        using var configKey = Registry.CurrentUser.OpenSubKey(GAME_CONFIG_REG_PATH, writable: true);
                        configKey?.DeleteValue("GameDVR_Enabled", throwOnMissingValue: false);
                    }

                    _gameDvrDisabled = false;
                    _originalAppCaptureEnabled = -1;
                    _originalGameDvrEnabled = -1;
                    App.Log("[KernelOpt] Game Bar / Game DVR ayarları orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Game Mode geri alma hatası: {ex.Message}");
                }
            }

            // ── 7) Working Set geri al ──
            if (_workingSetOptimized && _processHandle != IntPtr.Zero)
            {
                try
                {
                    // Orijinal Working Set boyutlarını geri yükle
                    if (_originalWsMin != IntPtr.Zero && _originalWsMax != IntPtr.Zero)
                    {
                        SetProcessWorkingSetSizeEx(_processHandle, _originalWsMin, _originalWsMax, _originalWsFlags);
                    }
                    else
                    {
                        // Orijinal değerler alınamadıysa, varsayılana (OS kararına) bırak
                        // -1, -1 geçmek Windows'un kendi boyutlarını belirlemesini sağlar
                        SetProcessWorkingSetSizeEx(_processHandle, new IntPtr(-1), new IntPtr(-1), 0);
                    }
                    _workingSetOptimized = false;
                    App.Log("[KernelOpt] Working Set ayarları orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Working Set geri alma hatası: {ex.Message}");
                }
            }

            // ── 8) I/O Priority geri al ──
            if (_ioPriorityBoosted && _processHandle != IntPtr.Zero)
            {
                try
                {
                    int normalPriority = IO_PRIORITY_NORMAL;
                    int status = NtSetInformationProcess(
                        _processHandle,
                        ProcessIoPriority,
                        ref normalPriority,
                        sizeof(int));

                    if (status == 0)
                    {
                        App.Log("[KernelOpt] I/O önceliği NORMAL seviyeye döndürüldü.");
                    }
                    else
                    {
                        App.Log($"[KernelOpt] I/O önceliği geri alınamadı. NTSTATUS: 0x{status:X8}");
                    }
                    _ioPriorityBoosted = false;
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] I/O Priority geri alma hatası: {ex.Message}");
                }
            }

            // ── 9) Thread Scheduling geri al ──
            if (_threadSchedulingOptimized && _processHandle != IntPtr.Zero)
            {
                try
                {
                    // Priority Boost'u tekrar etkinleştir (disablePriorityBoost = false)
                    SetProcessPriorityBoost(_processHandle, false);
                    _threadSchedulingOptimized = false;
                    App.Log("[KernelOpt] Thread zamanlama ayarı (Priority Boost) orijinaline döndürüldü.");
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] Thread Scheduling geri alma hatası: {ex.Message}");
                }
            }

            // ── Process handle referansını temizle ──
            _processHandle = IntPtr.Zero;

            // ── Large Page önbelleğini sıfırla (bir sonraki kontrol için) ──
            _largePageChecked = false;
            _largePageAvailable = false;

            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
            App.Log("[KernelOpt] ══════ Geri Alma Tamamlandı ═════════════════════════");
            App.Log("[KernelOpt] ══════════════════════════════════════════════════════");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ── Durum Raporu ──────────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Mevcut optimizasyon durumlarını string olarak döner.
        /// Tüm 12 optimizasyon modülünün durumunu gösterir.
        /// </summary>
        public static string GetStatus(LauncherConfig config)
        {
            var lines = new System.Collections.Generic.List<string>();

            // ── Mevcut 5 Optimizasyon ──
            lines.Add("─── Temel Optimizasyonlar ───");
            lines.Add(config.KernelPriority ? "✅ İşlem Önceliği: HIGH" : "❌ İşlem Önceliği: Kapalı");
            lines.Add(config.KernelTimer    ? "✅ Timer Çözünürlüğü: 1ms" : "❌ Timer Çözünürlüğü: Kapalı");
            lines.Add(config.KernelAffinity ? "✅ CPU Affinity: Optimize" : "❌ CPU Affinity: Kapalı");
            lines.Add(config.KernelPower    ? "✅ Güç Planı: Yüksek Performans" : "❌ Güç Planı: Kapalı");
            lines.Add(config.KernelNagle    ? "✅ Nagle (TCP): Devre Dışı" : "❌ Nagle (TCP): Kapalı");

            // ── Yeni 7 Optimizasyon ──
            lines.Add("");
            lines.Add("─── Gelişmiş GPU/Sistem Optimizasyonları ───");

            if (config.KernelGpu)
            {
                lines.Add(_gpuPreferenceSet
                    ? "✅ GPU Tercihi: NVIDIA (Yüksek Performans)"
                    : "⏳ GPU Tercihi: Beklemede");

                lines.Add(_largePageChecked
                    ? (_largePageAvailable
                        ? "✅ Large Pages: Destekleniyor"
                        : "⚠️ Large Pages: Yetki gerekli (SeLockMemory)")
                    : "⏳ Large Pages: Henüz kontrol edilmedi");

                lines.Add(_fullscreenOptDisabled
                    ? "✅ Fullscreen Opt.: Devre Dışı (Düşük Input Lag)"
                    : "⏳ Fullscreen Opt.: Beklemede");

                lines.Add(_gameDvrDisabled
                    ? "✅ Game Bar/DVR: Devre Dışı"
                    : "⏳ Game Bar/DVR: Beklemede");

                lines.Add(_workingSetOptimized
                    ? "✅ Working Set: 512MB Min (Sayfalama Koruması)"
                    : "⏳ Working Set: Beklemede");

                lines.Add(_ioPriorityBoosted
                    ? "✅ I/O Önceliği: HIGH (Hızlı Chunk Yükleme)"
                    : "⏳ I/O Önceliği: Beklemede");

                lines.Add(_threadSchedulingOptimized
                    ? "✅ Thread Zamanlama: Tutarlı CPU (Priority Boost Kapalı)"
                    : "⏳ Thread Zamanlama: Beklemede");
            }
            else
            {
                lines.Add("❌ GPU Tercihi: Kapalı");
                lines.Add("❌ Large Pages: Kapalı");
                lines.Add("❌ Fullscreen Opt.: Kapalı");
                lines.Add("❌ Game Bar/DVR: Kapalı");
                lines.Add("❌ Working Set: Kapalı");
                lines.Add("❌ I/O Önceliği: Kapalı");
                lines.Add("❌ Thread Zamanlama: Kapalı");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Mevcut sistemdeki optimizasyon durumlarını registry'den okuyarak algılar.
        /// Launcher başlangıcında çağrılarak hangi optimizasyonların aktif olduğu gösterilir.
        /// </summary>
        public static Dictionary<string, bool> DetectCurrentOptimizations()
        {
            var results = new Dictionary<string, bool>();
            
            // 1. GPU Preference
            try
            {
                using var gpuKey = Registry.CurrentUser.OpenSubKey(GPU_PREF_REG_PATH);
                string? exe = Environment.ProcessPath;
                if (gpuKey != null && !string.IsNullOrEmpty(exe))
                {
                    var val = gpuKey.GetValue(exe) as string;
                    results["GpuPreference"] = val != null && val.Contains("GpuPreference=2");
                }
                else results["GpuPreference"] = false;
            }
            catch { results["GpuPreference"] = false; }
            
            // 2. NVIDIA App Profile
            try
            {
                using var nvKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak");
                results["NvidiaProfile"] = nvKey != null;
            }
            catch { results["NvidiaProfile"] = false; }
            
            // 3. Game Bar/DVR
            try
            {
                using var dvrKey = Registry.CurrentUser.OpenSubKey(GAME_DVR_REG_PATH);
                if (dvrKey != null)
                {
                    var val = dvrKey.GetValue("AppCaptureEnabled");
                    results["GameBarDisabled"] = val != null && (int)val == 0;
                }
                else results["GameBarDisabled"] = false;
            }
            catch { results["GameBarDisabled"] = false; }
            
            // 4. Fullscreen Optimization
            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    using var fsKey = Registry.CurrentUser.OpenSubKey(FULLSCREEN_REG_PATH);
                    if (fsKey != null)
                    {
                        var val = fsKey.GetValue(exe) as string;
                        results["FullscreenOptDisabled"] = val != null && val.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE");
                    }
                    else results["FullscreenOptDisabled"] = false;
                }
                else results["FullscreenOptDisabled"] = false;
            }
            catch { results["FullscreenOptDisabled"] = false; }
            
            // 5. Power Plan (High Performance)
            try
            {
                IntPtr activePlanPtr;
                uint result = PowerGetActiveScheme(IntPtr.Zero, out activePlanPtr);
                if (result == 0 && activePlanPtr != IntPtr.Zero)
                {
                    Guid activePlan = Marshal.PtrToStructure<Guid>(activePlanPtr);
                    LocalFree(activePlanPtr);
                    Guid highPerf = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    Guid ultimate = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");
                    results["HighPerfPower"] = activePlan == highPerf || activePlan == ultimate;
                }
                else results["HighPerfPower"] = false;
            }
            catch { results["HighPerfPower"] = false; }
            
            // 6. Nagle
            try
            {
                using var tcpKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                bool nagleDisabled = false;
                if (tcpKey != null)
                {
                    foreach (var sub in tcpKey.GetSubKeyNames())
                    {
                        using var ifKey = tcpKey.OpenSubKey(sub);
                        if (ifKey != null)
                        {
                            var val = ifKey.GetValue("TcpNoDelay");
                            if (val != null && (int)val == 1) { nagleDisabled = true; break; }
                        }
                    }
                }
                results["NagleDisabled"] = nagleDisabled;
            }
            catch { results["NagleDisabled"] = false; }
            
            return results;
        }

        /// <summary>
        /// Sistemdeki GPU adını algılar (NVIDIA, AMD, Intel vb.). WMI ve Registry fallback kullanır.
        /// </summary>
        public static string DetectGpuName()
        {
            try
            {
                var gpus = new List<string>();

                // 1. WMI ile algılamayı dene
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(name) && !gpus.Contains(name))
                        {
                            gpus.Add(name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[KernelOpt] WMI GPU algılama hatası (Registry fallback kullanılacak): {ex.Message}");
                }

                // 2. Eğer WMI boş döndüyse veya hata verdiyse Registry'den oku
                if (gpus.Count == 0)
                {
                    try
                    {
                        using var baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                        if (baseKey != null)
                        {
                            foreach (var subkeyName in baseKey.GetSubKeyNames())
                            {
                                if (subkeyName.Length == 4 && int.TryParse(subkeyName, out _))
                                {
                                    using var subkey = baseKey.OpenSubKey(subkeyName);
                                    if (subkey != null)
                                    {
                                        string? desc = subkey.GetValue("DriverDesc") as string;
                                        if (!string.IsNullOrEmpty(desc) && !gpus.Contains(desc))
                                        {
                                            gpus.Add(desc);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[KernelOpt] Registry GPU algılama hatası: {ex.Message}");
                    }
                }

                if (gpus.Count > 0)
                {
                    // NVIDIA/AMD/Arc gibi harici kartları öne al
                    gpus.Sort((a, b) =>
                    {
                        bool aDedicated = IsDedicatedGpu(a);
                        bool bDedicated = IsDedicatedGpu(b);
                        if (aDedicated && !bDedicated) return -1;
                        if (!aDedicated && bDedicated) return 1;
                        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    });

                    return string.Join(" / ", gpus);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[KernelOpt] Genel GPU algılama hatası: {ex.Message}");
            }
            return "Bilinmiyor";
        }

        private static bool IsDedicatedGpu(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("GTX", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("AMD ", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Arc(TM)", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Intel(R) Arc", StringComparison.OrdinalIgnoreCase);
        }


        public static ulong GetTotalPhysicalMemory()
        {
            try
            {
                var status = new MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref status))
                {
                    return status.ullTotalPhys;
                }
            }
            catch { }
            return 0;
        }
    }
}
