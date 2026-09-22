using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;

namespace MistikLauncher
{
    // â”€â”€ Config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public class LauncherConfig
    {
        [JsonProperty("quick_links",ObjectCreationHandling=ObjectCreationHandling.Replace)] public List<string> QuickLinks { get; set; } = new() { "Dash","Vers","Mods","Skin","Server","Settings" };
        [JsonProperty("window_buttons")] public string WindowButtons { get; set; } = "MacOS";
        [JsonProperty("close_lighting")] public string CloseLighting { get; set; } = "Theme";
        [JsonProperty("close_rgb")] public string CloseRgb { get; set; } = "#FFB000";
        [JsonProperty("launcher_auto_update")] public bool LauncherAutoUpdate { get; set; } = true;
        [JsonProperty("auto_mcs_update")] public bool AutoMcsAutoUpdate { get; set; } = true;
        [JsonProperty("user")]       public string User       { get; set; } = "Oyuncu";
        [JsonProperty("version")]    public string Version    { get; set; } = "1.21";
        [JsonProperty("ram")]        public int    Ram        { get; set; } = 4;
        [JsonProperty("lang")]       public string Lang       { get; set; } = "TÃ¼rkÃ§e";
        [JsonProperty("accent")]     public string Accent     { get; set; } = "Amber";
        [JsonProperty("skin_type")]  public string SkinType   { get; set; } = "default";
        [JsonProperty("skin_user")]  public string SkinUser   { get; set; } = "";
        [JsonProperty("auth_type")]  public string AuthType   { get; set; } = "offline";
        [JsonProperty("role")]       public string Role       { get; set; } = "KullanÄ±cÄ±";
        [JsonProperty("opt_turbo")]  public bool   OptTurbo   { get; set; } = true;
        [JsonProperty("opt_fps")]    public bool   OptFps     { get; set; } = true;
        [JsonProperty("auto_close")] public bool   AutoClose  { get; set; } = true;
        [JsonProperty("friends")]    public List<string> Friends     { get; set; } = new();
        [JsonProperty("friend_codes")] public List<string> FriendCodes { get; set; } = new();
        [JsonProperty("version_code")] public string VersionCode { get; set; } = App.LocalVersion;
        [JsonProperty("open_count")]   public int OpenCount   { get; set; } = 0;
        [JsonProperty("github_user")]  public string GithubUser { get; set; } = "Musta";
        [JsonProperty("tunnel_gateway")] public int TunnelGateway { get; set; } = 0; // 0=bore.pub 1=Özel SSH
        [JsonProperty("tunnel_custom_host")] public string TunnelCustomHost { get; set; } = "";
        [JsonProperty("tunnel_custom_subdomain")] public string TunnelCustomSubdomain { get; set; } = "";
        [JsonProperty("tunnel_port")] public int TunnelPort { get; set; } = 25565;
        [JsonProperty("last_synced_version")] public string LastSyncedVersion { get; set; } = "";

        // ── Kernel Optimizasyonları ──
        [JsonProperty("kern_priority")] public bool KernelPriority { get; set; } = false;
        [JsonProperty("kern_timer")]    public bool KernelTimer    { get; set; } = false;
        [JsonProperty("kern_affinity")] public bool KernelAffinity { get; set; } = false;
        [JsonProperty("kern_power")]    public bool KernelPower    { get; set; } = false;
        [JsonProperty("kern_nagle")]    public bool KernelNagle    { get; set; } = false;
        [JsonProperty("kern_gpu")]      public bool KernelGpu      { get; set; } = false;
    }

    public static class ConfigManager
    {
        static readonly string Path = System.IO.Path.Combine(App.AppData, "config.json");
        static readonly byte[] Header = Encoding.ASCII.GetBytes("MLUC1\n");

        private static readonly object Gate = new();
        public static LauncherConfig Normalize(LauncherConfig cfg)
        {
            cfg.Lang = cfg.Lang == "English" || cfg.Lang == "en" ? "English" : "Turkce";
            cfg.Ram = Math.Clamp(cfg.Ram, 1, 32);
            cfg.TunnelPort = Math.Clamp(cfg.TunnelPort, 1, 65535);
              cfg.User = Regex.IsMatch(cfg.User ?? "", @"^[A-Za-z0-9_]{3,16}$") ? cfg.User! : "Player";
              cfg.Version ??= "";
              cfg.Version = GameProfiles.SafeId(cfg.Version) && cfg.Version.Length<=120 ? cfg.Version : "1.21";
              cfg.Friends ??= new(); cfg.FriendCodes ??= new();
              // Migrate stale configs from pre-6.x builds; this value is informational and
              // must always describe the binary that is currently running.
              cfg.VersionCode = App.LocalVersion;
              cfg.QuickLinks=(cfg.QuickLinks ?? new()).Where(x=>new[]{"Dash","Vers","Mods","Skin","Server","Settings"}.Contains(x)).Distinct().Take(6).ToList();
              cfg.Role = "User";
              cfg.Accent = ColorThemes.Names.Contains(cfg.Accent) ? cfg.Accent : "Amber";
              cfg.WindowButtons = MainWindow.WindowButtonStyles.Contains(cfg.WindowButtons) ? cfg.WindowButtons : "MacOS";
              if(cfg.CloseLighting=="Rainbow") cfg.CloseLighting="RGB";
              cfg.CloseLighting = new[]{"Theme","RGB","Off"}.Contains(cfg.CloseLighting) ? cfg.CloseLighting : "Theme";
              cfg.CloseRgb = Regex.IsMatch(cfg.CloseRgb ?? "", "^#[0-9A-Fa-f]{6}$") ? cfg.CloseRgb!.ToUpperInvariant() : "#FFB000";
              cfg.AuthType = cfg.AuthType == "elyby" ? "elyby" : "offline";
              cfg.SkinType = new[]{"local","username","default"}.Contains(cfg.SkinType) ? cfg.SkinType : "default";
              if(cfg.SkinType=="username" && !Regex.IsMatch(cfg.SkinUser ?? "", @"^[A-Za-z0-9_]{3,16}$")) cfg.SkinUser=cfg.User;
              return cfg;
        }
        public static LauncherConfig Load()
        {
            lock (Gate)
            {
                try
                {
                    if (File.Exists(Path)) return Read(Path);
                }
                catch (Exception ex)
                {
                    App.Log("Configuration recovery: " + ex.Message);
                    try
                    {
                        if (File.Exists(Path + ".bak"))
                            return Read(Path + ".bak");
                    }
                    catch (Exception backupError) { App.Log("Configuration backup recovery: " + backupError.Message); }
                }
                return Normalize(new());
            }
        }
        public static event Action<LauncherConfig>? Saved;
        static bool Encrypted(byte[] bytes) => bytes.AsSpan().StartsWith(Header);
        static LauncherConfig Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                if (Encrypted(bytes))
                {
                    byte[] plain = WindowsSecret.Transform(bytes[Header.Length..], false);
                    try { return Normalize(JsonConvert.DeserializeObject<LauncherConfig>(Encoding.UTF8.GetString(plain)) ?? new()); }
                    finally { CryptographicOperations.ZeroMemory(plain); }
                }
                return Normalize(JsonConvert.DeserializeObject<LauncherConfig>(Encoding.UTF8.GetString(bytes)) ?? new());
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        static byte[] Protect(byte[] plain)
        {
            byte[] cipher = WindowsSecret.Transform(plain, true);
            byte[] result = new byte[Header.Length + cipher.Length];
            Header.CopyTo(result, 0);
            cipher.CopyTo(result, Header.Length);
            CryptographicOperations.ZeroMemory(cipher);
            return result;
        }
        static void ProtectLegacyBackup()
        {
            if (!File.Exists(Path + ".bak")) return;
            byte[] backup = File.ReadAllBytes(Path + ".bak");
            try
            {
                if (Encrypted(backup)) return;
                string temporary = Path + ".bak.tmp";
                File.WriteAllBytes(temporary, Protect(backup));
                File.Move(temporary, Path + ".bak", true);
            }
            finally { CryptographicOperations.ZeroMemory(backup); }
        }
        public static void Save(LauncherConfig cfg)
        {
            lock (Gate)
            {
                Normalize(cfg);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                string temporary = Path + ".tmp";
                byte[] plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cfg));
                try { File.WriteAllBytes(temporary, Protect(plain)); }
                finally { CryptographicOperations.ZeroMemory(plain); }
                if (!File.Exists(Path))
                {
                    File.Move(temporary, Path);
                    ProtectLegacyBackup();
                }
                else
                {
                    byte[] previous = File.ReadAllBytes(Path);
                    try
                    {
                        bool valid;
                        try { Read(Path); valid = true; }
                        catch { valid = false; }
                        if (valid && Encrypted(previous)) File.Replace(temporary, Path, Path + ".bak");
                        else
                        {
                            // Never replace a healthy backup with a broken primary.
                            string backupTemporary = Path + ".bak.tmp";
                            if (valid) File.WriteAllBytes(backupTemporary, Protect(previous));
                            File.Replace(temporary, Path, null);
                            if (valid) File.Move(backupTemporary, Path + ".bak", true);
                            else ProtectLegacyBackup();
                        }
                    }
                    finally { CryptographicOperations.ZeroMemory(previous); }
                }
            }
            Saved?.Invoke(cfg);
        }
    }

    // ——— Server list ——————————————————————————————————————————————————
    public record ServerEntry(string Name, string Ip, int Port, string Mode, string Ver, int Max, string Color, string Icon);

    public static class App
    {
        public static readonly string AppData = Environment.GetEnvironmentVariable("MISTIK_DATA_DIR") ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
        public static readonly string GameDir  = System.IO.Path.Combine(AppData, "game");
        public static readonly string ModsDir  = System.IO.Path.Combine(GameDir, "mods");
        public static readonly string LogFile  = System.IO.Path.Combine(AppData, "launcher.log");
        public static string LocalVersion => "v" + typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
        public static bool AdminAccessEnabled => false;

        public static readonly List<ServerEntry> Servers = new()
        {
            new("CraftRise", "play.craftrise.com.tr", 25565, "Tum Oyunlar",  "1.8-1.21",  5000,   "#00A3FF", "🚀"),
            new("Hypixel",   "mc.hypixel.net",          25565,"Mini Oyunlar","1.8-1.21",200000,"#FFB100","🌟"),
            new("GomeMC",    "play.gomemc.com",       25565, "Turkiye PvP",  "1.8.9",     2000,   "#FF4B4B", "⚔️"),
            new("CubeCraft","play.cubecraft.net",       25565,"Mini Oyunlar","1.8-1.21",30000, "#00D4AA","🎮"),
            new("Wynncraft","play.wynncraft.com",       25565,"MMORPG",      "1.12-1.21",5000, "#888888","🛡️"),
        };

        // Accepts 6 or 7 args (max optional)

        public static readonly List<ChangelogEntry> Changelog = new()
        {
            new("v6.0.10","2026-09-22","#FFB000", new[]{
                "Ayarlar ve yedekleri Windows kullanıcı hesabına bağlı olarak şifrelendi; eski kayıtlar otomatik taşınır",
                "Ana panel ve ayarlarda gereksiz ikinci çizim kaldırıldı; sayfa geçişleri hızlandı",
                "Güncelleme kartına tema rengini izleyen ilerleme çubuğu eklendi"
            }),
            new("v6.0.9","2026-09-20","#FFB000", new[]{
                "Eski kurulum kayıtlarındaki yol biçimi güncelleme yardımcısıyla uyumlu hale getirildi",
                "Kayıt onarımı yalnızca Windows kurulum kaydı ve güvenli göreli dosya listesi doğrulanınca yapılır"
            }),
            new("v6.0.8","2026-09-19","#FFB000", new[]{
                "Önbelleğe alınmış sayfalarda gereksiz ikinci dil ağacı taraması kaldırıldı",
                "Sayfa geçişlerinde yeniden çizim ve görsel takılma daha da azaltıldı"
            }),
            new("v6.0.7","2026-09-19","#FFB000", new[]{
                "Güncelleme hız, indirilen boyut ve tahmini kalan süreyi gösterir",
                "İndirme ilerleme olayları sınırlanarak güncelleme sırasında UI kasması azaltıldı",
                "Sayfa geçişindeki gereksiz ilk çift render kaldırıldı"
            }),
            new("v6.0.6","2026-09-19","#FFB000", new[]{
                "Mod senkronizasyonu UI thread'inden arka plana taşındı; geçişlerde donma azaltıldı",
                "Arka plan taraması sırasında yapılan son sürüm seçimi kaybolmaz",
                "Senkronizasyon hatalarında WPF dispatcher kullanılarak çapraz thread hatası önlendi"
            }),
            new("v6.0.5","2026-09-19","#FFB000", new[]{
                "Güncelleme ekranında hedef sürüm açıkça gösterilir",
                "Eski paketlerin yeni kurulumu geri alması downgrade korumasıyla engellenir",
                "Güncelleme yardımcısı seçilen release sürümünü doğrular"
            }),
            new("v6.0.4","2026-09-19","#FFB000", new[]{
                "GitHub API rate limitlerinde son doğrulanmış sürüm metadata önbelleği kullanılır",
                "Launcher ve Auto-MCS güncellemeleri ETag ile gereksiz istekleri azaltır",
                "Güncelleme hataları mevcut sürümü korur ve yeniden denemeyi güvenli yapar"
            }),
            new("v6.0.3","2026-09-19","#FFB000", new[]{
                "Modern ana panel, renk temaları ve pencere düğmesi stilleri yenilendi",
                "Türkçe/İngilizce dil geçişi ve sürüm güncelleme akışı sağlamlaştırıldı",
                "Forge sürüm seçimi, skin önizleme ve yerel skin uygulama hataları düzeltildi"
            }),
            new("v5.5.2","2026-06-07","#FF3300", new[]{ 
                "Karakter cilt (skin) kilitleme hataları düzeltildi",
                "Minecraft açıkken skin değiştirmede uyarı penceresi eklendi",
                "CustomSkinLoader ve Ely.by skin çakışması uyarısı detaylandırıldı"
            }),
            new("v5.5.1","2026-06-07","#FF6B00", new[]{ 
                "CustomSkinLoader Cilt Yaması entegrasyonu Karakter Odasına eklendi",
                "Mod Sürüm Taşıyıcı hedef sürüm çakışmaları ve sürüm listesi düzeltildi",
                "Auto-MCS sunucu indiricisi GitHub 403 engellemeleri tamamen çözüldü"
            }),
            new("v5.5.0","2026-06-03","#00FFCC", new[]{ 
                "Otomatik sertifika kurulumu eklendi (SmartScreen uyarısı kaldırıldı)",
                "Güncelleme kısır döngüsü düzeltildi",
                "Kararlı sürüm yayınlandı",
                "Gelişmiş NVIDIA App ve optimizasyon entegrasyonu"
            }),
            new("v5.4.0","2026-06-01","#00FFCC", new[]{ 
                "Bağımsız .NET Framework 4.8 Kaldırıcısı (Uninstaller) eklendi",
                "Kilitli dosya güncelleme çakışmaları çözüldü",
                "Hızlı ve sessiz AppData kurulumu entegre edildi"
            }),
            new("v5.3.0","2026-05-27","#2EB82E", new[]{ 
                "Toplu Mod Sürüm Taşıyıcı (Bulk Mod Migrator) eklendi – Kurulu modları tek tıkla farklı sürümlere taşır",
                "Sürüm değiştirince modların askıdan indirilmemesi hatası tamamen giderildi",
                "NVIDIA App & GeForce Experience tam keşif desteği – Launcher sistem tarafından otomatik algılanır",
                "Fiziksel RAM güvenlik kilidi eklendi – Shader açarken çökme ve Out of Memory hataları önlendi",
                "Büyük Bellek Sayfaları (Large Pages) desteği ile veri okuma hızı maksimize edildi" 
            }),
            new("v5.2.0","2026-05-27","#FF6B00", new[]{ "Kernel düzeyinde oyun optimizasyonları (İşlem Önceliği, Timer 1ms, CPU Affinity, Güç Planı, Nagle)", "Ayarlardan açılıp kapatılabilir toggle sistemi", "Oyun kapanınca tüm değişiklikler otomatik geri alınır" }),
            new("v5.1.0","2026-05-26","#00FFCC", new[]{ "Seçilebilir Ely.by & Çevrimdışı cilt sistemi entegrasyonu", "Akıllı ve optimize edilmiş kütüphane/asset yükleyicisi", "Gelişmiş kararlılık ve performans motoru güncellemeleri" }),
            new("v5.0.0","2026-05-19","#00A3FF", new[]{ "C# WPF'e geçiş – antivirüs false-positive yok","MQTT relay sistemi – IP paylaşılmaz","Otomatik SSH oyun tüneli (Serveo.net)","Gerçek skin önizleme galerisi" }),
            new("v4.3.0","2026-05-18","#2EB82E", new[]{ "P2P arkadaş sistemi eklendi","Skin yaması (CustomSkinLoader)","Performans iyileştirmeleri" }),
            new("v4.0.0","2026-05-16","#FFB100", new[]{ "Sürüm Yöneticisi","Mod Merkezi (Modrinth)","Bulut güncellemeler" }),
        };

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(AppData);
                File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }

    // ── HWID / IP Cihaz Bilgisi ──────────────────────────────────────────────
    public static class DeviceInfo
    {
        static string? _cachedHwid;
        static string? _cachedPublicIp;
        static string? _cachedMachineName;
        static string? _cachedCpuModel;

        /// <summary>Donanıma özgü benzersiz kimlik (MotherBoard SN + CPU ID hash)</summary>
        public static string GetHWID()
        {
            if (_cachedHwid != null) return _cachedHwid;
            try
            {
                string raw = "";
                // Anakart seri numarası
                try
                {
                    using var mbSearcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                    foreach (var obj in mbSearcher.Get())
                        raw += obj["SerialNumber"]?.ToString() ?? "";
                }
                catch { }

                // İşlemci ID
                try
                {
                    using var cpuSearcher = new System.Management.ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                    foreach (var obj in cpuSearcher.Get())
                        raw += obj["ProcessorId"]?.ToString() ?? "";
                }
                catch { }

                // Disk seri numarası (yedek)
                try
                {
                    using var diskSearcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0");
                    foreach (var obj in diskSearcher.Get())
                        raw += obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                }
                catch { }

                if (string.IsNullOrWhiteSpace(raw))
                    raw = Environment.MachineName + Environment.UserName;

                var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw));
                _cachedHwid = BitConverter.ToString(hash).Replace("-", "")[..16].ToUpper();
            }
            catch
            {
                _cachedHwid = "UNKNOWN";
            }
            return _cachedHwid;
        }

        /// <summary>Public IP adresi (ipify.org API)</summary>
        public static async Task<string> GetPublicIpAsync()
        {
            if (_cachedPublicIp != null) return _cachedPublicIp;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                // Birden fazla servis dene
                string[] apis = {
                    "https://api.ipify.org",
                    "https://icanhazip.com",
                    "https://ifconfig.me/ip"
                };
                foreach (var api in apis)
                {
                    try
                    {
                        _cachedPublicIp = (await http.GetStringAsync(api)).Trim();
                        if (!string.IsNullOrEmpty(_cachedPublicIp)) return _cachedPublicIp;
                    }
                    catch { continue; }
                }
            }
            catch { }
            _cachedPublicIp = "Bilinmiyor";
            return _cachedPublicIp;
        }

        /// <summary>Bilgisayar adı</summary>
        public static string GetMachineName()
        {
            if (_cachedMachineName != null) return _cachedMachineName;
            try { _cachedMachineName = Environment.MachineName; }
            catch { _cachedMachineName = "Bilinmiyor"; }
            return _cachedMachineName;
        }

        /// <summary>İşletim sistemi sürümü</summary>
        public static string GetOSVersion()
        {
            try { return Environment.OSVersion.ToString(); }
            catch { return "Bilinmiyor"; }
        }

        /// <summary>Windows kullanıcı adı</summary>
        public static string GetWindowsUser()
        {
            try { return Environment.UserName; }
            catch { return "Bilinmiyor"; }
        }

        /// <summary>İşlemci modeli (WMI Win32_Processor.Name ve Registry fallback)</summary>
        public static string GetCpuModel()
        {
            if (_cachedCpuModel != null) return _cachedCpuModel;
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    _cachedCpuModel = obj["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(_cachedCpuModel)) return _cachedCpuModel;
                }
            }
            catch { }

            // Registry Fallback
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    string? name = key.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        _cachedCpuModel = name.Trim();
                        return _cachedCpuModel;
                    }
                }
            }
            catch { }

            _cachedCpuModel = "Bilinmiyor";
            return _cachedCpuModel;
        }


        /// <summary>Tüm cihaz bilgilerini tek seferde topla</summary>
        public static async Task<DeviceReport> CollectAsync()
        {
            var hwid = GetHWID();
            var ip = await GetPublicIpAsync();
            return new DeviceReport
            {
                HWID = hwid,
                PublicIP = ip,
                MachineName = GetMachineName(),
                WindowsUser = GetWindowsUser(),
                OSVersion = GetOSVersion(),
                CpuModel = GetCpuModel(),
                LauncherVersion = App.LocalVersion,
                CollectedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };
        }
    }

    public class DeviceReport
    {
        [JsonProperty("hwid")]             public string HWID            { get; set; } = "";
        [JsonProperty("public_ip")]        public string PublicIP        { get; set; } = "";
        [JsonProperty("machine_name")]     public string MachineName     { get; set; } = "";
        [JsonProperty("windows_user")]     public string WindowsUser     { get; set; } = "";
        [JsonProperty("os_version")]       public string OSVersion       { get; set; } = "";
        [JsonProperty("cpu_model")]        public string CpuModel        { get; set; } = "";
        [JsonProperty("launcher_version")] public string LauncherVersion { get; set; } = "";
        [JsonProperty("collected_at")]     public string CollectedAt     { get; set; } = "";
    }

    public record ChangelogEntry(string Ver, string Date, string Color, string[] Items);

    // â”€â”€ Minecraft Ping â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static class McPing
    {
        public static async Task<(bool online, int players, int max, int ping)>
            PingAsync(string host, int port = 25565, int timeoutMs = 3000)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var tcp = new System.Net.Sockets.TcpClient();
                var ct = new CancellationTokenSource(timeoutMs);
                await tcp.ConnectAsync(host, port, ct.Token);
                using var ns = tcp.GetStream();
                ns.ReadTimeout = timeoutMs;
                // Handshake
                var hs = BuildHandshake(host, port);
                await ns.WriteAsync(hs); await ns.WriteAsync(new byte[] { 0x01, 0x00 });
                await ns.FlushAsync();
                // Read length
                var lenBuf = new byte[5]; int r = 0;
                while (r < 2) r += await ns.ReadAsync(lenBuf.AsMemory(r));
                var respBuf = new byte[4096]; int total = 0;
                try { while ((r = await ns.ReadAsync(respBuf.AsMemory(total))) > 0) total += r; }
                catch { }
                sw.Stop();
                int ping = (int)sw.ElapsedMilliseconds;
                var json = ExtractJson(respBuf, total);
                if (json == null) return (false, 0, 0, 0);
                var d = JsonConvert.DeserializeObject<dynamic>(json)!;
                int pl  = (int)(d.players?.online ?? 0);
                int mx  = (int)(d.players?.max    ?? 0);
                return (true, pl, mx, ping);
            }
            catch { return (false, 0, 0, 0); }
        }

        static byte[] BuildHandshake(string host, int port)
        {
            var buf = new List<byte> { 0x00 };
            buf.AddRange(WriteVarInt(-1)); // protocol ver
            var hostBytes = Encoding.UTF8.GetBytes(host);
            buf.AddRange(WriteVarInt(hostBytes.Length)); buf.AddRange(hostBytes);
            buf.Add((byte)(port >> 8)); buf.Add((byte)(port & 0xFF));
            buf.AddRange(WriteVarInt(1));
            var pkt = new List<byte>();
            pkt.AddRange(WriteVarInt(buf.Count)); pkt.AddRange(buf);
            return pkt.ToArray();
        }

        static byte[] WriteVarInt(int v)
        {
            var b = new List<byte>();
            uint uv = (uint)v;
            do { byte c = (byte)(uv & 0x7F); uv >>= 7; if (uv != 0) c |= 0x80; b.Add(c); } while (uv != 0);
            return b.ToArray();
        }

        static string? ExtractJson(byte[] buf, int len)
        {
            for (int i = 0; i < len - 1; i++)
                if (buf[i] == '{') { var s = Encoding.UTF8.GetString(buf, i, len - i); var e = s.LastIndexOf('}'); return e > 0 ? s[..(e+1)] : null; }
            return null;
        }
    }

    // â”€â”€ MQTT Relay (IP yok, oda kodu bazlÄ±) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public class MistikRelay : IAsyncDisposable
    {
        const string Broker   = "broker.emqx.io";
        const int    BrokerPort = 8883;
        const string TopicBase = "mistik_ultra_v2/players";
        const string ReqBase   = "mistik_ultra_v2/requests";
        const string RespBase  = "mistik_ultra_v2/responses";

        public string Username  { get; }
        public string RoomCode  { get; }
        public bool   Connected { get; private set; }
        public string? TunnelAddress { get; private set; }

        public event Action<List<PeerInfo>>? OnUpdate;
        public event Action<string?>?        OnTunnelReady;
        public event Action<string, string>? OnFriendRequestReceived;
        public event Action<string, string>? OnFriendRequestAccepted;
        public event Action<string, string, string>? OnUpdateNotification;
        public event Action<string>?        OnTunnelLog;

        readonly Dictionary<string, PeerInfo> _peers = new();
        IMqttClient? _client;
        PeerInfo _myInfo = new();
        CancellationTokenSource _cts = new();
        Process? _tunnelProc;
        int _openedPort = 25565;

        public MistikRelay(string username)
        {
            Username = username;
            RoomCode = GenerateCode(username);
        }

        static string GenerateCode(string u)
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes("mistik_ultra_" + u));
            return BitConverter.ToString(h).Replace("-","")[..6].ToUpper();
        }

        public async Task<(bool ok, string msg)> StartAsync(PeerInfo myInfo)
        {
            _myInfo = myInfo with { User = Username, RoomCode = RoomCode };
            try
            {
                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += OnMessage;
                _client.DisconnectedAsync += OnDisconnect;

                var opts = new MqttClientOptionsBuilder()
                    .WithTcpServer(Broker, BrokerPort)
                    .WithTlsOptions(options => options.UseTls())
                    .WithClientId($"mistik_{Username}_{Environment.TickCount64}")
                    .WithWillTopic($"{TopicBase}/{Username}")
                    .WithWillPayload(JsonConvert.SerializeObject(new { user = Username, offline = true }))
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(20))
                    .Build();

                await _client.ConnectAsync(opts, _cts.Token);
                await _client.SubscribeAsync($"{TopicBase}/#");
                await _client.SubscribeAsync($"{ReqBase}/{RoomCode}");
                await _client.SubscribeAsync($"{RespBase}/{RoomCode}");
                await _client.SubscribeAsync("mistik_ultra_v2/updates");
                Connected = true;

                _ = HeartbeatLoopAsync();
                _ = CleanupLoopAsync();
                return (true, "Bağlandı");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public void UpdateStatus(string status, string ver, string server)
        {
            _myInfo = _myInfo with { Status = status, Ver = ver, Server = server };
        }

        async Task HeartbeatLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                await Publish(); await Task.Delay(10000, _cts.Token).ContinueWith(_ => { });
            }
        }

        async Task CleanupLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                lock (_peers)
                {
                    var stale = new List<string>();
                    foreach (var kv in _peers)
                        if (now - kv.Value.LastSeen > 25) stale.Add(kv.Key);
                    foreach (var k in stale) _peers.Remove(k);
                    if (stale.Count > 0) OnUpdate?.Invoke(GetOnlinePlayers());
                }
                await Task.Delay(5000, _cts.Token).ContinueWith(_ => { });
            }
        }

        async Task Publish()
        {
            if (_client == null || !Connected) return;
            var payload = JsonConvert.SerializeObject(_myInfo);
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic($"{TopicBase}/{Username}")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            try { await _client.PublishAsync(msg); } catch { }
        }

        public async Task SendFriendRequestAsync(string targetCode)
        {
            if (_client == null || !Connected) return;
            var payload = JsonConvert.SerializeObject(new { from_user = Username, from_code = RoomCode });
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic($"{ReqBase}/{targetCode}")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg);
        }

        public async Task AcceptFriendRequestAsync(string targetCode)
        {
            if (_client == null || !Connected) return;
            var payload = JsonConvert.SerializeObject(new { from_user = Username, from_code = RoomCode, accepted = true });
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic($"{RespBase}/{targetCode}")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg);
        }

        public async Task PublishUpdateAsync(string ver, string url, string changelog)
        {
            if (_client == null || !Connected) return;
            var payload = JsonConvert.SerializeObject(new { version = ver, url = url, changelog = changelog });
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("mistik_ultra_v2/updates")
                .WithPayload(payload)
                .WithRetainFlag(true)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg);
        }

        Task OnMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var json = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                if (topic == "mistik_ultra_v2/updates")
                {
                    var upd = JsonConvert.DeserializeObject<UpdateMessage>(json);
                    if (upd != null)
                    {
                        OnUpdateNotification?.Invoke(upd.Version, upd.Url, upd.Changelog);
                    }
                }
                else if (topic.StartsWith(ReqBase))
                {
                    var req = JsonConvert.DeserializeObject<dynamic>(json);
                    string fromUser = req?.from_user ?? "";
                    string fromCode = req?.from_code ?? "";
                    if (!string.IsNullOrEmpty(fromUser) && fromCode != RoomCode)
                    {
                        OnFriendRequestReceived?.Invoke(fromUser, fromCode);
                    }
                }
                else if (topic.StartsWith(RespBase))
                {
                    var resp = JsonConvert.DeserializeObject<dynamic>(json);
                    string fromUser = resp?.from_user ?? "";
                    string fromCode = resp?.from_code ?? "";
                    bool accepted = resp?.accepted ?? false;
                    if (accepted && !string.IsNullOrEmpty(fromUser) && fromCode != RoomCode)
                    {
                        OnFriendRequestAccepted?.Invoke(fromUser, fromCode);
                    }
                }
                else if (topic.StartsWith(TopicBase))
                {
                    var d = JsonConvert.DeserializeObject<PeerInfo>(json);
                    if (d == null || d.User == Username) return Task.CompletedTask;
                    lock (_peers)
                    {
                        if (d.Offline) _peers.Remove(d.User);
                        else _peers[d.User] = d with { LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                    }
                    OnUpdate?.Invoke(GetOnlinePlayers());
                }
            }
            catch { }
            return Task.CompletedTask;
        }

        Task OnDisconnect(MqttClientDisconnectedEventArgs _) { Connected = false; return Task.CompletedTask; }

        public List<PeerInfo> GetOnlinePlayers()
        {
            lock (_peers) return new List<PeerInfo>(_peers.Values);
        }

        // ─── playit.gg yardımcısı ──────────────────────────────────────────────────
        static string PlayitExePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MistikLauncher");
            return Path.Combine(dir, "playit.exe");
        }

        static async Task<bool> EnsurePlayitAsync(Action<string> log)
        {
            string playitPath = PlayitExePath();
            if (File.Exists(playitPath)) return true;

            log("[SİSTEM] playit.exe indiriliyor (ilk kullanım, ~12 MB)...");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(playitPath)!);
                string url = "https://github.com/playit-cloud/playit-agent/releases/download/v0.17.1/playit-windows-x86_64-signed.exe";
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                var data = await http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(playitPath, data);

                if (File.Exists(playitPath)) { log("[SİSTEM] ✅ playit.exe hazır!"); return true; }
                log("[HATA] playit.exe indirilemedi."); return false;
            }
            catch (Exception ex) { log($"[HATA] playit.exe indirilemedi: {ex.Message}"); return false; }
        }

        // ─── bore.pub yardımcısı ───────────────────────────────────────────────────
        static string BoreExePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MistikLauncher");
            return Path.Combine(dir, "bore.exe");
        }

        static async Task<bool> EnsureBoreAsync(Action<string> log)
        {
            string borePath = BoreExePath();
            if (File.Exists(borePath)) return true;

            log("[SİSTEM] bore.exe indiriliyor (ilk kullanım, ~3 MB)...");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(borePath)!);
                string url = "https://github.com/ekzhang/bore/releases/download/v0.5.0/bore-v0.5.0-x86_64-pc-windows-msvc.zip";
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var data = await http.GetByteArrayAsync(url);
                string zip = borePath + ".zip";
                await File.WriteAllBytesAsync(zip, data);
                System.IO.Compression.ZipFile.ExtractToDirectory(zip, Path.GetDirectoryName(borePath)!, overwriteFiles: true);
                File.Delete(zip);

                // Self-healing: if bore.exe is not in the root, look recursively and move it to the root!
                if (!File.Exists(borePath))
                {
                    var files = Directory.GetFiles(Path.GetDirectoryName(borePath)!, "bore.exe", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        File.Move(files[0], borePath, overwrite: true);
                    }
                }

                if (File.Exists(borePath)) { log("[SİSTEM] ✅ bore.exe hazır!"); return true; }
                log("[HATA] bore.exe ZIP'ten çıkarılamadı."); return false;
            }
            catch (Exception ex) { log($"[HATA] bore.exe indirilemedi: {ex.Message}"); return false; }
        }

        // ─── SSH Oyun Tüneli ───────────────────────────────────────────────────────
        // gateway: "playit.gg" | "bore.pub" | "custom"
        public void StartTunnel(int localPort = 25565, string gateway = "playit.gg",
                                 string? customSubdomain = null, string? customHost = null)
        {
            // Clean up any existing tunnels first to prevent conflicts
            StopTunnel();

            _openedPort = localPort;

            // Otomatik olarak tüm server.properties dosyalarını çevrimdışı moda ayarla
            EnforceOfflineModeInProperties();

            // ── UPnP (Otomatik Modem Port Yönlendirme) ───────────────────────────
            if (gateway == "upnp")
            {
                OnTunnelLog?.Invoke("[SİSTEM] 🎯 UPnP otomatik port yönlendirme başlatılıyor...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (ok, msg) = await MistikUpnp.AddUpnpPortMappingWithTimeoutAsync(localPort);
                        if (ok)
                        {
                            OnTunnelLog?.Invoke($"[SİSTEM] ✓ Modem port yönlendirme başarılı! Yerel IP: {MistikUpnp.GetLocalIPAddress()}");
                            OnTunnelLog?.Invoke("[SİSTEM] Dış IP adresi sorgulanıyor...");
                            string? publicIp = await MistikUpnp.GetPublicIPAddressAsync();
                            if (!string.IsNullOrEmpty(publicIp))
                            {
                                string addr = $"{publicIp}:{localPort}";
                                TunnelAddress = addr;
                                _myInfo = _myInfo with { Tunnel = addr };
                                _ = Publish();
                                OnTunnelReady?.Invoke(addr);
                                OnTunnelLog?.Invoke($"[SİSTEM] ✅ Bağlantı Başarılı! Arkadaşlarına ver: {addr}");
                            }
                            else
                            {
                                OnTunnelLog?.Invoke("[UYARI] Dış IP adresi alınamadı. Ancak port modeminizde açıldı!");
                                OnTunnelReady?.Invoke($"DışIP:{localPort}");
                            }
                        }
                        else
                        {
                            OnTunnelLog?.Invoke($"[HATA] Modem portu açamadı: {msg}");
                            OnTunnelLog?.Invoke("[İPUCU] Modem arayüzünden UPnP özelliğinin açık olduğunu kontrol edin veya playit.gg seçin.");
                            OnTunnelReady?.Invoke(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnTunnelLog?.Invoke($"[HATA] UPnP işlemi sırasında hata: {ex.Message}");
                        OnTunnelReady?.Invoke(null);
                    }
                });
                return;
            }

            // ── PLAYIT.GG — hesap bazlı tünel, yüksek stabilite ──────────────────
            if (gateway == "playit.gg")
            {
                OnTunnelLog?.Invoke("[SİSTEM] 🎯 playit.gg tüneli başlatılıyor...");
                OnTunnelLog?.Invoke("[İPUCU] playit.gg ilk kez başlatılıyorsa, doğrulamak için konsoldaki linke tıklayın.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Kill any existing playit processes to prevent conflicts
                        try
                        {
                            foreach (var proc in Process.GetProcessesByName("playit"))
                            {
                                try { proc.Kill(true); } catch { }
                            }
                        }
                        catch { }

                        bool ok = await EnsurePlayitAsync(msg => OnTunnelLog?.Invoke(msg));
                        if (!ok) { OnTunnelReady?.Invoke(null); return; }

                        var secretFile = Path.Combine(Path.GetDirectoryName(PlayitExePath())!, "playit.toml");

                        // ── Doğru komut: --secret_path <toml> --stdout start ──────────
                        var psi = new ProcessStartInfo
                        {
                            FileName               = PlayitExePath(),
                            WorkingDirectory       = Path.GetDirectoryName(PlayitExePath())!,
                            Arguments              = $"--secret_path \"{secretFile}\" start",
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            CreateNoWindow         = true
                        };

                        try { _tunnelProc = Process.Start(psi)!; }
                        catch (Exception ex) { OnTunnelLog?.Invoke($"[HATA] {ex.Message}"); OnTunnelReady?.Invoke(null); return; }

                        void NotifyPlayit(string addr)
                        {
                            if (TunnelAddress != null) return;
                            TunnelAddress = addr;
                            _myInfo = _myInfo with { Tunnel = addr };
                            _ = Publish();
                            OnTunnelReady?.Invoke(addr);
                            OnTunnelLog?.Invoke($"[SİSTEM] ✅ Bağlantı Başarılı! Arkadaşlarına ver: {addr}");
                        }

                        bool claimOpened  = false;
                        bool agentStarted = false;


                        // ── stdout okuyucu ────────────────────────────────────────────
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                string? line;
                                while (_tunnelProc != null && (line = _tunnelProc.StandardOutput.ReadLine()) != null)
                                {
                                    line = Regex.Replace(line, @"\x1b\[[0-9;]*[a-zA-Z]", "");
                                    OnTunnelLog?.Invoke($"[PLAYIT] {line}");

                                    var claimMatch = Regex.Match(line, @"https?://playit\.gg/claim/[\w\-]+");
                                    if (claimMatch.Success)
                                    {
                                        OnTunnelLog?.Invoke($"[SİSTEM] 🔑 LİNK YAKALANDI → {claimMatch.Value}");
                                        if (!claimOpened)
                                        {
                                            claimOpened = true;
                                            try { Process.Start(new ProcessStartInfo(claimMatch.Value) { UseShellExecute = true }); } catch { }
                                            OnTunnelLog?.Invoke("[SİSTEM] 🌐 Doğrulama sayfası tarayıcıda açıldı.");
                                        }
                                    }

                                    // Ajan başladığına dair TUI çıktısı
                                    if (!agentStarted && line.Contains("tunnel running", StringComparison.OrdinalIgnoreCase))
                                    {
                                        agentStarted = true;
                                        OnTunnelLog?.Invoke("[SİSTEM] ✔ Ajan bağlantısı kuruldu, tünel adresi bekleniyor...");
                                    }

                                    // Adresi yeni regex ile yakala: port olmak zorunda değil (joinmc.link için)
                                    var m = Regex.Match(line, @"([\w\-\.]+\.(?:ply\.gg|playit\.gg|joinmc\.link|playit\.cloud))(:(\d+))?");
                                    if (m.Success) 
                                    {
                                        string addrStr = m.Groups[3].Success ? $"{m.Groups[1].Value}:{m.Groups[3].Value}" : m.Groups[1].Value;
                                        NotifyPlayit(addrStr);
                                    }
                                }
                            }
                            catch { }

                            if (TunnelAddress == null)
                            {
                                OnTunnelLog?.Invoke("[HATA] playit kapandı — tünel sonlandı.");
                                OnTunnelReady?.Invoke(null);
                            }
                        });


                        // ── stderr okuyucu ────────────────────────────────────────────
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                string? line;
                                while (_tunnelProc != null && (line = _tunnelProc.StandardError.ReadLine()) != null)
                                {
                                    line = Regex.Replace(line, @"\x1b\[[0-9;]*[a-zA-Z]", "");
                                    OnTunnelLog?.Invoke($"[PLAYIT] {line}");

                                    var claimMatch = Regex.Match(line, @"https?://playit\.gg/claim/[\w\-]+");
                                    if (claimMatch.Success)
                                    {
                                        OnTunnelLog?.Invoke($"[SİSTEM] 🔑 LİNK YAKALANDI → {claimMatch.Value}");
                                        if (!claimOpened)
                                        {
                                            claimOpened = true;
                                            try { Process.Start(new ProcessStartInfo(claimMatch.Value) { UseShellExecute = true }); } catch { }
                                        }
                                    }

                                    if (!agentStarted && line.Contains("tunnel running", StringComparison.OrdinalIgnoreCase))
                                    {
                                        agentStarted = true;
                                        OnTunnelLog?.Invoke("[SİSTEM] ✔ Ajan bağlantısı kuruldu (stderr), tünel adresi bekleniyor...");
                                    }

                                    var m = Regex.Match(line, @"([\w\-\.]+\.(?:ply\.gg|playit\.gg|joinmc\.link|playit\.cloud))(:(\d+))?");
                                    if (m.Success) 
                                    {
                                        string addrStr = m.Groups[3].Success ? $"{m.Groups[1].Value}:{m.Groups[3].Value}" : m.Groups[1].Value;
                                        NotifyPlayit(addrStr);
                                    }
                                }
                            }
                            catch { }
                        });
                    }
                    catch (Exception ex)
                    {

                        OnTunnelLog?.Invoke($"[HATA] Tünel arka plan görevi çöktü: {ex.Message}");
                        OnTunnelReady?.Invoke(null);
                    }
                });
                return;
            }

            // ── BORE.PUB — gerçek TCP tüneli, hesap gerektirmez ──────────────────
            if (gateway == "bore.pub")
            {
                OnTunnelLog?.Invoke("[SİSTEM] 🎯 bore.pub TCP tüneli başlatılıyor...");
                OnTunnelLog?.Invoke($"[BİLGİ] Yerel port: {localPort} → bore.pub:XXXXX");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Kill any existing bore processes to prevent conflicts
                        try
                        {
                            foreach (var proc in Process.GetProcessesByName("bore"))
                            {
                                try { proc.Kill(true); } catch { }
                            }
                        }
                        catch { }

                        bool ok = await EnsureBoreAsync(msg => OnTunnelLog?.Invoke(msg));
                        if (!ok) { OnTunnelReady?.Invoke(null); return; }

                        var psi = new ProcessStartInfo
                        {
                            FileName               = BoreExePath(),
                            Arguments              = $"local {localPort} --to bore.pub",
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            CreateNoWindow         = true
                        };
                        OnTunnelLog?.Invoke($"[DEBUG] {psi.FileName} {psi.Arguments}");

                        try { _tunnelProc = Process.Start(psi)!; }
                        catch (Exception ex) { OnTunnelLog?.Invoke($"[HATA] {ex.Message}"); OnTunnelReady?.Invoke(null); return; }

                        void NotifyBore(string addr)
                        {
                            if (TunnelAddress != null) return;
                            TunnelAddress = addr;
                            _myInfo = _myInfo with { Tunnel = addr };
                            _ = Publish();
                            OnTunnelReady?.Invoke(addr);
                            OnTunnelLog?.Invoke($"[SİSTEM] ✅ Bağlantı Başarılı! Arkadaşlarına ver: {addr}");
                        }

                        // bore output: "listening at bore.pub:PORT" on stderr
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                string? line;
                                while (_tunnelProc != null && (line = _tunnelProc.StandardError.ReadLine()) != null)
                                {
                                    // Strip ANSI escape color sequences from line to prevent regex match failure
                                    line = Regex.Replace(line, @"\x1b\[[0-9;]*[a-zA-Z]", "");
                                    OnTunnelLog?.Invoke($"[BORE] {line}");
                                    var m = Regex.Match(line, @"listening at ([\w\.\-]+):(\d+)");
                                    if (m.Success) NotifyBore($"{m.Groups[1].Value}:{m.Groups[2].Value}");
                                }
                            }
                            catch { }
                            if (TunnelAddress == null)
                            {
                                OnTunnelLog?.Invoke("[HATA] bore kapandı — bağlantı kurulamadı.");
                                OnTunnelReady?.Invoke(null);
                            }
                        });

                        // stdout da oku
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                string? line;
                                while (_tunnelProc != null && (line = _tunnelProc.StandardOutput.ReadLine()) != null)
                                {
                                    // Strip ANSI escape color sequences from line to prevent regex match failure
                                    line = Regex.Replace(line, @"\x1b\[[0-9;]*[a-zA-Z]", "");
                                    OnTunnelLog?.Invoke($"[BORE] {line}");
                                    var m = Regex.Match(line, @"listening at ([\w\.\-]+):(\d+)");
                                    if (m.Success) NotifyBore($"{m.Groups[1].Value}:{m.Groups[2].Value}");
                                }
                            }
                            catch { }
                        });
                    }
                    catch (Exception ex)
                    {
                        OnTunnelLog?.Invoke($"[HATA] Tünel arka plan görevi çöktü: {ex.Message}");
                        OnTunnelReady?.Invoke(null);
                    }
                });
                return; // bore kendi task'ında çalışıyor
            }

            // ── SSH tabanlı servisler ─────────────────────────────────────────────
            string ssh = FindSsh();
            if (string.IsNullOrEmpty(ssh))
            {
                OnTunnelLog?.Invoke("[HATA] OpenSSH bulunamadı!");
                OnTunnelReady?.Invoke(null);
                return;
            }
            OnTunnelLog?.Invoke($"[SİSTEM] SSH: {ssh}");

            // Ensure SSH key exists
            EnsureSshKey();

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string keyPath = Path.Combine(userProfile, ".ssh", "id_ed25519");
            if (!File.Exists(keyPath)) keyPath = Path.Combine(userProfile, ".ssh", "id_rsa");
            string keyArg = File.Exists(keyPath) ? $"-i \"{keyPath}\" " : "";

            string commonOpts = "-o StrictHostKeyChecking=no -o ServerAliveInterval=30 -o ConnectTimeout=20 -o BatchMode=yes ";
            string args;

            // serveo.net or custom SSH
            string target;
            if (gateway == "serveo.net")
            {
                target = "serveo.net";
            }
            else
            {
                target = !string.IsNullOrEmpty(customHost) ? customHost.Trim() : "nokey@localhost.run";
            }

            string portOpt = "";
            int atIdx = target.IndexOf('@');
            string hostPart = atIdx >= 0 ? target[(atIdx + 1)..] : target;
            if (hostPart.Contains(":"))
            {
                int colonIdx = hostPart.LastIndexOf(':');
                string possiblePort = hostPart[(colonIdx + 1)..];
                if (int.TryParse(possiblePort, out _))
                {
                    portOpt = $"-p {possiblePort} ";
                    string hostOnly = hostPart[..colonIdx];
                    target = atIdx >= 0 ? $"{target[..(atIdx + 1)]}{hostOnly}" : hostOnly;
                }
            }

            args = $"{commonOpts}{keyArg}{portOpt}-R 0:localhost:{localPort} {target}";
            OnTunnelLog?.Invoke($"[SİSTEM] SSH tüneli başlatılıyor → {target} (Yerel Port: {localPort})...");

            // SSH komutunu log'a yaz — kullanıcı tam olarak ne çalıştığını görsün
            OnTunnelLog?.Invoke($"[DEBUG] SSH komutu: ssh {args}");

            var psi = new ProcessStartInfo
            {
                FileName               = ssh,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            try { _tunnelProc = Process.Start(psi)!; }
            catch (Exception ex)
            {
                OnTunnelLog?.Invoke($"[HATA] Tünel başlatılırken sistem hatası oluştu: {ex.Message}");
                OnTunnelReady?.Invoke(null);
                return;
            }

            // Helper: fire tunnel ready once
            void NotifyReady(string addr)
            {
                if (TunnelAddress != null) return;
                TunnelAddress = addr;
                _myInfo = _myInfo with { Tunnel = addr };
                _ = Publish();
                OnTunnelReady?.Invoke(addr);
                OnTunnelLog?.Invoke($"[SİSTEM] ✅ Bağlantı Başarılı! Adresiniz: {addr}");
            }

            // ── Read stdout ─────────────────────────────────────────────────────────
            Task.Run(() =>
            {
                try
                {
                    string? line;
                    while (_tunnelProc != null && (line = _tunnelProc.StandardOutput.ReadLine()) != null)
                    {
                        OnTunnelLog?.Invoke($"[OUT] {line}");

                        // serveo.net TCP: "Forwarding TCP connections from serveo.net:XXXXX"
                        var mServeo = Regex.Match(line, @"Forwarding TCP connections from (serveo\.net):(\d+)");
                        if (mServeo.Success) { NotifyReady($"{mServeo.Groups[1].Value}:{mServeo.Groups[2].Value}"); continue; }

                        // localhost.run TCP: "tunnelXXXX.lhr.life listens on port 25565"
                        var m = Regex.Match(line, @"([\w\-]+\.lhr\.(?:life|pro|run)).*?(\d{4,5})");
                        if (m.Success) { NotifyReady($"{m.Groups[1].Value}:{m.Groups[2].Value}"); continue; }

                        // localhost.run TCP (short form): just hostname match → port 25565
                        m = Regex.Match(line, @"([\w\-]+\.lhr\.(?:life|pro|run))");
                        if (m.Success) { NotifyReady($"{m.Groups[1].Value}:{localPort}"); continue; }

                        // Generic "Allocated port NNNNN"
                        m = Regex.Match(line, @"[Aa]llocated port (\d+)");
                        if (m.Success)
                        {
                            string h = customHost ?? gateway;
                            if (h.Contains("@")) h = h[(h.IndexOf('@') + 1)..];
                            if (h.Contains(":")) h = h[..h.LastIndexOf(':')];
                            NotifyReady($"{h}:{m.Groups[1].Value}");
                        }
                    }
                }
                catch (Exception ex) { OnTunnelLog?.Invoke($"[HATA] Çıkış kanalı: {ex.Message}"); }

                // Process exited
                if (TunnelAddress == null)
                {
                    OnTunnelLog?.Invoke("[HATA] SSH tüneli kapandı — bağlantı kurulamadı.");
                    OnTunnelLog?.Invoke("[İPUCU] Özel SSH veya serveo.net seçerek tekrar deneyin.");
                    OnTunnelReady?.Invoke(null);
                }
            });

            // ── Read stderr ─────────────────────────────────────────────────────────
            Task.Run(() =>
            {
                try
                {
                    string? line;
                    while (_tunnelProc != null && (line = _tunnelProc.StandardError.ReadLine()) != null)
                    {
                        // serveo.net TCP in stderr
                        var mServeo = Regex.Match(line, @"Forwarding TCP connections from (serveo\.net):(\d+)");
                        if (mServeo.Success)
                        {
                            NotifyReady($"{mServeo.Groups[1].Value}:{mServeo.Groups[2].Value}");
                        }
                        // localhost.run sends tunnel URL on stderr too
                        else if (Regex.IsMatch(line, @"([\w\-]+\.lhr\.(?:life|pro|run))"))
                        {
                            var mLhr = Regex.Match(line, @"([\w\-]+\.lhr\.(?:life|pro|run))");
                            if (mLhr.Success) NotifyReady($"{mLhr.Groups[1].Value}:25565");
                        }
                        else OnTunnelLog?.Invoke($"[UYARI] {line}");
                    }
                }
                catch (Exception ex) { OnTunnelLog?.Invoke($"[HATA] Hata kanalı okuma hatası: {ex.Message}"); }
            });
        }

        public void StopTunnel()
        {
            try { _tunnelProc?.Kill(true); } catch { }
            _tunnelProc  = null;
            TunnelAddress = null;
            _myInfo = _myInfo with { Tunnel = null };

            // UPnP portunu kapat
            try { _ = MistikUpnp.RemoveUpnpPortMappingAsync(_openedPort); } catch { }

            // Kill any stray playit or bore processes using standard Process.Kill
            try
            {
                foreach (var proc in Process.GetProcessesByName("playit"))
                {
                    try { proc.Kill(true); } catch { }
                }
            }
            catch { }

            try
            {
                foreach (var proc in Process.GetProcessesByName("bore"))
                {
                    try { proc.Kill(true); } catch { }
                }
            }
            catch { }

            try
            {
                foreach (var proc in Process.GetProcessesByName("ssh"))
                {
                    try { proc.Kill(true); } catch { }
                }
            }
            catch { }

            // Force-kill all playit, bore, and ssh processes using taskkill to guarantee complete release
            try
            {
                var psiPlayit = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/f /im playit.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiPlayit)?.WaitForExit(1000);
            }
            catch {}

            try
            {
                var psiBore = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/f /im bore.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiBore)?.WaitForExit(1000);
            }
            catch {}

            try
            {
                var psiSsh = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/f /im ssh.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiSsh)?.WaitForExit(1000);
            }
            catch {}
        }

        static string FindSsh()
        {
            string[] candidates = {
                @"C:\Windows\System32\OpenSSH\ssh.exe",
                @"C:\Program Files\Git\usr\bin\ssh.exe",
                "ssh"
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            try { Process.Start(new ProcessStartInfo("ssh", "-V") { CreateNoWindow = true })?.WaitForExit(500); return "ssh"; }
            catch { return ""; }
        }

        private void EnsureSshKey()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string sshDir = Path.Combine(userProfile, ".ssh");
                string keyPath = Path.Combine(sshDir, "id_ed25519");
                string rsaPath = Path.Combine(sshDir, "id_rsa");

                if (File.Exists(keyPath) || File.Exists(rsaPath))
                {
                    return; // Key already exists
                }

                OnTunnelLog?.Invoke("[SİSTEM] SSH anahtarı bulunamadı, otomatik oluşturuluyor...");
                Directory.CreateDirectory(sshDir);

                string keygenPath = @"C:\Windows\System32\OpenSSH\ssh-keygen.exe";
                if (!File.Exists(keygenPath))
                {
                    keygenPath = "ssh-keygen";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = keygenPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("ed25519");
                psi.ArgumentList.Add("-N"); psi.ArgumentList.Add("");
                psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(keyPath);
                psi.ArgumentList.Add("-q");

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                }

                if (File.Exists(keyPath))
                {
                    OnTunnelLog?.Invoke("[SİSTEM] SSH anahtarı (ed25519) başarıyla oluşturuldu.");
                }
                else
                {
                    OnTunnelLog?.Invoke("[UYARI] SSH anahtarı otomatik oluşturulamadı, bağlantı başarısız olabilir.");
                }
            }
            catch (Exception ex)
            {
                OnTunnelLog?.Invoke($"[UYARI] SSH anahtarı oluşturulurken hata: {ex.Message}");
            }
        }

        // Compatibility hook: never weaken server authentication when opening a tunnel.
        public void EnforceOfflineModeInProperties()
        {
            OnTunnelLog?.Invoke(Localization.T("serverAuthPreserved"));
        }

        private void SearchPropertiesRecursively(string currentDir, List<string> foundFiles, int depth)
        {
            if (depth > 5) return; // Safely increase depth to 5 levels to reach nested folders

            try
            {
                foreach (var file in Directory.GetFiles(currentDir, "server.properties"))
                {
                    foundFiles.Add(file);
                }

                foreach (var subDir in Directory.GetDirectories(currentDir))
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    var nameStr = dirInfo.Name;

                    // Skip hidden/system, OS paths, dev folders, and heavy Minecraft subfolders to ensure instant execution
                    if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 || 
                        (dirInfo.Attributes & FileAttributes.System) != 0 ||
                        nameStr.StartsWith(".") ||
                        nameStr.Equals("AppData", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("Program Files", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("world", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("world_nether", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("world_the_end", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("saves", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("resourcepacks", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("shaderpacks", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("mods", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("assets", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("libraries", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("versions", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("cache", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("crash-reports", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                        nameStr.Equals("tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    SearchPropertiesRecursively(subDir, foundFiles, depth + 1);
                }
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            StopTunnel();
            if (_client != null && Connected)
            {
                var payload = JsonConvert.SerializeObject(new { user = Username, offline = true });
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic($"{TopicBase}/{Username}").WithPayload(payload).Build();
                try { await _client.PublishAsync(msg); await _client.DisconnectAsync(); } catch { }
            }
        }
    }

    public record PeerInfo
    {
        [JsonProperty("user")]       public string User      { get; init; } = "";
        [JsonProperty("room_code")]  public string RoomCode  { get; init; } = "";
        [JsonProperty("status")]     public string Status    { get; init; } = "";
        [JsonProperty("ver")]        public string Ver       { get; init; } = "";
        [JsonProperty("server")]     public string Server    { get; init; } = "";
        [JsonProperty("tunnel")]     public string? Tunnel   { get; init; }
        [JsonProperty("offline")]    public bool   Offline   { get; init; }
        [JsonIgnore]                 public long   LastSeen  { get; init; }
    }

    // â”€â”€ Modrinth API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”�    // ─── UPnP Otomatik Port Yönlendirme Yardımcısı (Pure C# - No COM Deadlocks) ───
    public static class MistikUpnp
    {
        private static string? _cachedControlUrl;

        public static string GetLocalIPAddress()
        {
            using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint)
                    {
                        return endPoint.Address.ToString();
                    }
                }
                catch { }
            }
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        public static async Task<string?> GetPublicIPAddressAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                string ip = await client.GetStringAsync("https://api.ipify.org");
                return ip.Trim();
            }
            catch
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    string ip = await client.GetStringAsync("https://icanhazip.com");
                    return ip.Trim();
                }
                catch { return null; }
            }
        }

        private static System.Net.Sockets.UdpClient CreateUdpClient(string localIp)
        {
            try
            {
                return new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(localIp), 0));
            }
            catch
            {
                return new System.Net.Sockets.UdpClient();
            }
        }

        private static async Task<string?> DiscoverControlUrlAsync(int timeoutMs = 2500)
        {
            if (!string.IsNullOrEmpty(_cachedControlUrl)) return _cachedControlUrl;

            var ssdpQuery = "M-SEARCH * HTTP/1.1\r\n" +
                            "HOST: 239.255.255.250:1900\r\n" +
                            "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
                            "MAN: \"ssdp:discover\"\r\n" +
                            "MX: 2\r\n\r\n";

            string localIp = GetLocalIPAddress();
            byte[] reqBytes = Encoding.ASCII.GetBytes(ssdpQuery);
            using var udp = CreateUdpClient(localIp);
            udp.Client.ReceiveTimeout = timeoutMs;
            udp.Client.SendTimeout = timeoutMs;

            var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("239.255.255.250"), 1900);
            try
            {
                await udp.SendAsync(reqBytes, reqBytes.Length, ep);
                
                var receiveEp = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                byte[] res = udp.Receive(ref receiveEp);
                string resp = Encoding.ASCII.GetString(res);
                var match = Regex.Match(resp, @"LOCATION:\s*(http://[^\r\n]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string locationUrl = match.Groups[1].Value.Trim();
                    string? controlUrl = await GetControlUrlFromLocationAsync(locationUrl);
                    if (!string.IsNullOrEmpty(controlUrl))
                    {
                        _cachedControlUrl = controlUrl;
                        return controlUrl;
                    }
                }
            }
            catch { }

            // Secondary search with ST: upnp:rootdevice
            try
            {
                var ssdpQuery2 = "M-SEARCH * HTTP/1.1\r\n" +
                                 "HOST: 239.255.255.250:1900\r\n" +
                                 "ST: upnp:rootdevice\r\n" +
                                 "MAN: \"ssdp:discover\"\r\n" +
                                 "MX: 2\r\n\r\n";
                byte[] reqBytes2 = Encoding.ASCII.GetBytes(ssdpQuery2);
                await udp.SendAsync(reqBytes2, reqBytes2.Length, ep);
                
                var receiveEp = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                byte[] res = udp.Receive(ref receiveEp);
                string resp = Encoding.ASCII.GetString(res);
                var match = Regex.Match(resp, @"LOCATION:\s*(http://[^\r\n]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string locationUrl = match.Groups[1].Value.Trim();
                    string? controlUrl = await GetControlUrlFromLocationAsync(locationUrl);
                    if (!string.IsNullOrEmpty(controlUrl))
                    {
                        _cachedControlUrl = controlUrl;
                        return controlUrl;
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<string?> GetControlUrlFromLocationAsync(string locationUrl)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                http.DefaultRequestHeaders.ConnectionClose = true;
                string xml = await http.GetStringAsync(locationUrl);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                System.Xml.Linq.XNamespace ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
                
                var services = doc.Descendants(ns + "service");
                foreach (var s in services)
                {
                    string serviceType = s.Element(ns + "serviceType")?.Value ?? "";
                    if (serviceType.Contains("WANIPConnection") || serviceType.Contains("WANPPPConnection"))
                    {
                        string controlSubUrl = s.Element(ns + "controlURL")?.Value ?? "";
                        var uri = new Uri(locationUrl);
                        var controlUri = new Uri(uri, controlSubUrl);
                        return controlUri.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        private static async Task<bool> SendSoapActionAsync(string controlUrl, string action, string soapBody)
        {
            try
            {
                string reqXml = "<?xml version=\"1.0\"?>\r\n" +
                                "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n" +
                                "  <SOAP-ENV:Body>\r\n" +
                                soapBody +
                                "  </SOAP-ENV:Body>\r\n" +
                                "</SOAP-ENV:Envelope>";

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                http.DefaultRequestHeaders.ConnectionClose = true;
                var content = new StringContent(reqXml, Encoding.UTF8, "text/xml");
                
                string serviceType = controlUrl.Contains("WANPPPConnection") ? "urn:schemas-upnp-org:service:WANPPPConnection:1" : "urn:schemas-upnp-org:service:WANIPConnection:1";
                content.Headers.Add("SOAPAction", $"\"{serviceType}#{action}\"");

                var resp = await http.PostAsync(controlUrl, content);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<(bool success, string message)> AddUpnpPortMappingWithTimeoutAsync(int port, string description = "Mistik Launcher", int timeoutMs = 4000)
        {
            try
            {
                string? controlUrl = await DiscoverControlUrlAsync(2500);
                if (string.IsNullOrEmpty(controlUrl))
                {
                    return (false, "Yerel aginizda UPnP destekli bir modem bulunamadi. UPnP ayarinin modeminizde acik oldugundan emin olun.");
                }

                string localIp = GetLocalIPAddress();

                // 1. Clear existing mappings first
                await RemoveUpnpPortMappingAsync(port);

                // 2. Add TCP Mapping
                string soapBodyTcp = $"    <u:AddPortMapping xmlns:u=\"{(controlUrl.Contains("WANPPPConnection") ? "urn:schemas-upnp-org:service:WANPPPConnection:1" : "urn:schemas-upnp-org:service:WANIPConnection:1")}\">\r\n" +
                                     $"      <NewRemoteHost></NewRemoteHost>\r\n" +
                                     $"      <NewExternalPort>{port}</NewExternalPort>\r\n" +
                                     $"      <NewProtocol>TCP</NewProtocol>\r\n" +
                                     $"      <NewInternalPort>{port}</NewInternalPort>\r\n" +
                                     $"      <NewInternalClient>{localIp}</NewInternalClient>\r\n" +
                                     $"      <NewEnabled>1</NewEnabled>\r\n" +
                                     $"      <NewPortMappingDescription>{description}</NewPortMappingDescription>\r\n" +
                                     $"      <NewLeaseDuration>0</NewLeaseDuration>\r\n" +
                                     $"    </u:AddPortMapping>\r\n";

                bool okTcp = await SendSoapActionAsync(controlUrl, "AddPortMapping", soapBodyTcp);
                if (!okTcp)
                {
                    return (false, "Modem port yonlendirme istegini reddetti.");
                }

                // 3. Add UDP Mapping
                string soapBodyUdp = $"    <u:AddPortMapping xmlns:u=\"{(controlUrl.Contains("WANPPPConnection") ? "urn:schemas-upnp-org:service:WANPPPConnection:1" : "urn:schemas-upnp-org:service:WANIPConnection:1")}\">\r\n" +
                                     $"      <NewRemoteHost></NewRemoteHost>\r\n" +
                                     $"      <NewExternalPort>{port}</NewExternalPort>\r\n" +
                                     $"      <NewProtocol>UDP</NewProtocol>\r\n" +
                                     $"      <NewInternalPort>{port}</NewInternalPort>\r\n" +
                                     $"      <NewInternalClient>{localIp}</NewInternalClient>\r\n" +
                                     $"      <NewEnabled>1</NewEnabled>\r\n" +
                                     $"      <NewPortMappingDescription>{description}</NewPortMappingDescription>\r\n" +
                                     $"      <NewLeaseDuration>0</NewLeaseDuration>\r\n" +
                                     $"    </u:AddPortMapping>\r\n";
                
                await SendSoapActionAsync(controlUrl, "AddPortMapping", soapBodyUdp); // Optional, don't fail if UDP fails

                return (true, "Basarili");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<bool> RemoveUpnpPortMappingAsync(int port)
        {
            try
            {
                string? controlUrl = await DiscoverControlUrlAsync(1500);
                if (string.IsNullOrEmpty(controlUrl)) return false;

                string serviceType = controlUrl.Contains("WANPPPConnection") ? "urn:schemas-upnp-org:service:WANPPPConnection:1" : "urn:schemas-upnp-org:service:WANIPConnection:1";

                string soapBodyTcp = $"    <u:DeletePortMapping xmlns:u=\"{serviceType}\">\r\n" +
                                     $"      <NewRemoteHost></NewRemoteHost>\r\n" +
                                     $"      <NewExternalPort>{port}</NewExternalPort>\r\n" +
                                     $"      <NewProtocol>TCP</NewProtocol>\r\n" +
                                     $"    </u:DeletePortMapping>\r\n";

                string soapBodyUdp = $"    <u:DeletePortMapping xmlns:u=\"{serviceType}\">\r\n" +
                                     $"      <NewRemoteHost></NewRemoteHost>\r\n" +
                                     $"      <NewExternalPort>{port}</NewExternalPort>\r\n" +
                                     $"      <NewProtocol>UDP</NewProtocol>\r\n" +
                                     $"    </u:DeletePortMapping>\r\n";

                await SendSoapActionAsync(controlUrl, "DeletePortMapping", soapBodyTcp);
                await SendSoapActionAsync(controlUrl, "DeletePortMapping", soapBodyUdp);
                return true;
            }
            catch { return false; }
        }
    }
    public class UpdateMessage
    {
        [JsonProperty("version")]   public string Version   { get; set; } = "";
        [JsonProperty("url")]       public string Url       { get; set; } = "";
        [JsonProperty("changelog")] public string Changelog { get; set; } = "";
    }

    // ── Firebase Realtime Database Analytics ─────────────────────────────────────
    // Google Firebase REST API ile kullanıcı veritabanı.
    // Firebase Console: https://console.firebase.google.com
    // Veritabanı URL'sini kendi projenizle değiştirin.
    // Compatibility surface: telemetry and unauthenticated remote administration removed.
    public static class MistikAnalytics
    {
        public static Task TrackSessionStartAsync(string username, string launcherVersion, string selectedGameVersion) => Task.CompletedTask;
        public static Task TrackGameLaunchAsync(string username, string gameVersion, int ramGb) => Task.CompletedTask;
        public static Task TrackModInstallAsync(string username, string modName, string modVersion, string gameVersion) => Task.CompletedTask;
        public static Task TrackServerStartAsync(string username, string serverVersion, int port) => Task.CompletedTask;
        public static Task TrackSessionEndAsync(string username) => Task.CompletedTask;
        public static Task TrackVersionChangeAsync(string username, string newVersion) => Task.CompletedTask;
        public static Task TrackFriendAddedAsync(string username, string friendName) => Task.CompletedTask;
        public static Task TrackCrashAsync(string username, string errorMessage, string stackTrace) => Task.CompletedTask;
        public static Task<string?> GetAllUsersAsync() => Task.FromResult<string?>(null);
        public static Task<string?> GetStatsAsync() => Task.FromResult<string?>(null);
        public static string GetExactOSName() => Environment.OSVersion.ToString();
        public static Task TrackBanUserAsync(string username, bool banned) => Task.CompletedTask;
        public static Task SendAlertMessageAsync(string username, string message) => Task.CompletedTask;
        public static Task SendRemoteModAsync(string username, string modName, string modUrl) => Task.CompletedTask;
        public static Task DeleteUserLogsAsync(string username) => Task.CompletedTask;
        public static Task SyncInstalledModsAsync(string username, System.Collections.Generic.List<string> modNames) => Task.CompletedTask;
        public static Task SendBroadcastMessageAsync(string message) => Task.CompletedTask;
        public static Task BanMultipleUsersAsync(List<string> usernames, bool ban) => Task.CompletedTask;
        public static Task DeleteAllCrashLogsAsync() => Task.CompletedTask;
        public static Task<string?> ExportDatabaseAsync() => Task.FromResult<string?>(null);
        public static Task<int> CleanInactiveUsersAsync(int daysThreshold) => Task.FromResult(0);
        public static Task TrackGpuInfoAsync(string user, string gpuName) => Task.CompletedTask;
        public static Task<string> UploadFileToCatboxAsync(string filePath) => Task.FromResult("Remote services disabled");
    }
}
