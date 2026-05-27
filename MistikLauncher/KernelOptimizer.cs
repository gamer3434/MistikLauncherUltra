using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace MistikLauncher
{
    /// <summary>
    /// Windows Kernel-düzeyinde oyun optimizasyonları.
    /// Tüm değişiklikler geri alınabilir (Revert).
    /// GPU zorlaması YOK – donanım ömrünü korur.
    /// </summary>
    public static class KernelOptimizer
    {
        // ── P/Invoke Tanımları ──────────────────────────────────────────────

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

        // ── Sabitler ────────────────────────────────────────────────────────

        // Windows Güç Planları
        private static Guid GUID_HIGH_PERFORMANCE = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static Guid GUID_BALANCED         = new("381b4222-f694-41f0-9685-ff5bb260df2e");

        // ── Durum Takibi ────────────────────────────────────────────────────

        private static bool _timerResolutionSet = false;
        private static Guid _originalPowerPlan  = Guid.Empty;
        private static bool _nagleDisabled      = false;
        private static string? _nagleRegPath    = null;
        private static int _originalNagleValue  = 1;

        // ── Uygulama Metodları ──────────────────────────────────────────────

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
                int cpuCount = Environment.ProcessorCount;
                if (cpuCount <= 2)
                {
                    // 2 veya daha az çekirdek varsa tüm çekirdekleri kullan
                    App.Log("[KernelOpt] CPU sayısı az, affinity değiştirilmedi.");
                    return;
                }

                // İlk çekirdeği (0) OS'a bırak, geri kalanlarını Minecraft'a ver
                // Bu sayede OS işlemleri oyunu yavaşlatmaz
                long affinityMask = 0;
                for (int i = 1; i < cpuCount; i++)
                {
                    affinityMask |= (1L << i);
                }
                process.ProcessorAffinity = (IntPtr)affinityMask;
                App.Log($"[KernelOpt] CPU affinity ayarlandı: {cpuCount - 1} çekirdek (Çekirdek 0 OS'a ayrıldı).");
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

        // ── Toplu Uygulama ──────────────────────────────────────────────────

        /// <summary>
        /// Config'e göre tüm aktif optimizasyonları uygular.
        /// </summary>
        public static void ApplyAll(Process? process, LauncherConfig config)
        {
            App.Log("[KernelOpt] ══════ Kernel Optimizasyonları Uygulanıyor ══════");

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

            App.Log("[KernelOpt] ══════ Optimizasyonlar Tamamlandı ══════");
        }

        // ── Geri Alma ───────────────────────────────────────────────────────

        /// <summary>
        /// Uygulanan tüm kernel değişikliklerini geri alır.
        /// Oyun kapanınca çağrılır.
        /// </summary>
        public static void RevertAll()
        {
            App.Log("[KernelOpt] ══════ Kernel Optimizasyonları Geri Alınıyor ══════");

            // Timer çözünürlüğünü geri al
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

            // Güç planını geri al
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

            // Nagle'ı geri al (genellikle varsayılan zaten 1 olduğundan zararsız)
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

            App.Log("[KernelOpt] ══════ Geri Alma Tamamlandı ══════");
        }

        /// <summary>
        /// Mevcut optimizasyon durumlarını string olarak döner.
        /// </summary>
        public static string GetStatus(LauncherConfig config)
        {
            var lines = new System.Collections.Generic.List<string>();
            lines.Add(config.KernelPriority ? "✅ İşlem Önceliği: HIGH" : "❌ İşlem Önceliği: Kapalı");
            lines.Add(config.KernelTimer    ? "✅ Timer Çözünürlüğü: 1ms" : "❌ Timer Çözünürlüğü: Kapalı");
            lines.Add(config.KernelAffinity ? "✅ CPU Affinity: Optimize" : "❌ CPU Affinity: Kapalı");
            lines.Add(config.KernelPower    ? "✅ Güç Planı: Yüksek Performans" : "❌ Güç Planı: Kapalı");
            lines.Add(config.KernelNagle    ? "✅ Nagle (TCP): Devre Dışı" : "❌ Nagle (TCP): Kapalı");
            return string.Join("\n", lines);
        }
    }
}
