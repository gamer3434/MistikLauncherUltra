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
        [JsonProperty("user")]       public string User       { get; set; } = "Oyuncu";
        [JsonProperty("version")]    public string Version    { get; set; } = "1.21";
        [JsonProperty("ram")]        public int    Ram        { get; set; } = 4;
        [JsonProperty("lang")]       public string Lang       { get; set; } = "TÃ¼rkÃ§e";
        [JsonProperty("accent")]     public string Accent     { get; set; } = "Blue";
        [JsonProperty("skin_type")]  public string SkinType   { get; set; } = "default";
        [JsonProperty("skin_user")]  public string SkinUser   { get; set; } = "";
        [JsonProperty("auth_type")]  public string AuthType   { get; set; } = "offline";
        [JsonProperty("role")]       public string Role       { get; set; } = "KullanÄ±cÄ±";
        [JsonProperty("opt_turbo")]  public bool   OptTurbo   { get; set; } = true;
        [JsonProperty("opt_fps")]    public bool   OptFps     { get; set; } = true;
        [JsonProperty("auto_close")] public bool   AutoClose  { get; set; } = true;
        [JsonProperty("friends")]    public List<string> Friends     { get; set; } = new();
        [JsonProperty("friend_codes")] public List<string> FriendCodes { get; set; } = new();
        [JsonProperty("version_code")] public string VersionCode { get; set; } = "v5.2.1";
        [JsonProperty("open_count")]   public int OpenCount   { get; set; } = 0;
        [JsonProperty("github_user")]  public string GithubUser { get; set; } = "Musta";
        [JsonProperty("tunnel_gateway")] public int TunnelGateway { get; set; } = 0; // 0=bore.pub 1=Özel SSH
        [JsonProperty("tunnel_custom_host")] public string TunnelCustomHost { get; set; } = "";
        [JsonProperty("tunnel_custom_subdomain")] public string TunnelCustomSubdomain { get; set; } = "";
        [JsonProperty("tunnel_port")] public int TunnelPort { get; set; } = 25565;
        [JsonProperty("last_synced_version")] public string LastSyncedVersion { get; set; } = "";

        // ── Kernel Optimizasyonları ──
        [JsonProperty("kern_priority")] public bool KernelPriority { get; set; } = true;
        [JsonProperty("kern_timer")]    public bool KernelTimer    { get; set; } = true;
        [JsonProperty("kern_affinity")] public bool KernelAffinity { get; set; } = false;
        [JsonProperty("kern_power")]    public bool KernelPower    { get; set; } = true;
        [JsonProperty("kern_nagle")]    public bool KernelNagle    { get; set; } = true;
        [JsonProperty("kern_gpu")]      public bool KernelGpu      { get; set; } = true;
    }

    public static class ConfigManager
    {
        static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".mistik_ultra", "config.json");

        public static LauncherConfig Load()
        {
            try
            {
                if (File.Exists(Path))
                    return JsonConvert.DeserializeObject<LauncherConfig>(
                               File.ReadAllText(Path)) ?? new LauncherConfig();
            }
            catch { }
            return new LauncherConfig();
        }

        public static void Save(LauncherConfig cfg)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
    }

    // ——— Server list ——————————————————————————————————————————————————
    public record ServerEntry(string Name, string Ip, int Port, string Mode, string Ver, int Max, string Color, string Icon);

    public static class App
    {
        public static readonly string AppData = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
        public static readonly string GameDir  = System.IO.Path.Combine(AppData, "game");
        public static readonly string ModsDir  = System.IO.Path.Combine(GameDir, "mods");
        public static readonly string LogFile  = System.IO.Path.Combine(AppData, "launcher.log");
        public const  string LocalVersion = "v5.4.0";
        public const  string AdminPassword = "mustafa3434";

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
        const int    BrokerPort = 1883;
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

        public void EnforceOfflineModeInProperties()
        {
            try
            {
                var scanDirs = new List<string>();

                // 1. Mistik AppData directory (.mistik_ultra / App.AppData)
                if (Directory.Exists(App.AppData))
                    scanDirs.Add(App.AppData);

                // 2. Current application directory
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                    scanDirs.Add(exeDir);

                OnTunnelLog?.Invoke("[SİSTEM] 🔍 'server.properties' dosyaları taranıyor...");
                int fixedCount = 0;

                foreach (var dir in scanDirs)
                {
                    var files = new List<string>();
                    SearchPropertiesRecursively(dir, files, 0);

                    foreach (var file in files)
                    {
                        try
                        {
                            var lines = File.ReadAllLines(file);
                            bool updated = false;
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].Trim().StartsWith("online-mode", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!lines[i].Contains("false"))
                                    {
                                        lines[i] = "online-mode=false";
                                        updated = true;
                                    }
                                }
                            }

                            if (!updated && !lines.Any(l => l.Trim().StartsWith("online-mode", StringComparison.OrdinalIgnoreCase)))
                            {
                                var newLines = new List<string>(lines) { "online-mode=false" };
                                lines = newLines.ToArray();
                                updated = true;
                            }

                            if (updated)
                            {
                                File.WriteAllLines(file, lines);
                                var displayPath = file.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
                                OnTunnelLog?.Invoke($"[SİSTEM] 🔧 Sunucu Çevrimdışı Ayarlandı (online-mode=false): {displayPath}");
                                OnTunnelLog?.Invoke("[İPUCU] Eğer Minecraft sunucunuz zaten açıksa, bu ayarın geçerli olması için sunucuyu kapatıp yeniden açın!");
                                App.Log($"Automatically set online-mode=false in {file}");
                                fixedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            OnTunnelLog?.Invoke($"[UYARI] {Path.GetFileName(file)} düzenlenirken hata: {ex.Message}");
                        }
                    }
                }

                if (fixedCount == 0)
                {
                    OnTunnelLog?.Invoke("[SİSTEM] ✅ Aktif 'server.properties' dosyası bulunamadı veya hepsi zaten çevrimdışı modda (online-mode=false).");
                }
            }
            catch (Exception ex)
            {
                OnTunnelLog?.Invoke($"[UYARI] 'server.properties' taraması sırasında hata: {ex.Message}");
            }
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
    public static class MistikAnalytics
    {
        // ⚠️ Bu URL'yi kendi Firebase projenizin URL'si ile değiştirin!
        // Firebase Console > Realtime Database > URL kopyala
        private const string FirebaseUrl = "https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com";
        
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static DateTime _sessionStart = DateTime.Now;
        private static bool _initialized = false;

        /// <summary>
        /// Kullanıcı oturum açtığında çağrılır. Temel bilgileri Firebase'e kaydeder.
        /// </summary>
        public static async Task TrackSessionStartAsync(string username, string launcherVersion, string selectedGameVersion)
        {
            _sessionStart = DateTime.Now;
            _initialized = true;

            var data = new
            {
                username = username,
                launcher_version = launcherVersion,
                game_version = selectedGameVersion,
                os = GetExactOSName(),
                machine_name = SanitizeKey(Environment.MachineName),
                ram_gb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824L),
                session_start = DateTime.UtcNow.ToString("o"),
                last_active = DateTime.UtcNow.ToString("o"),
                status = "online",
                open_count = ConfigManager.Load().OpenCount
            };

            await FirebasePutAsync($"users/{SanitizeKey(username)}/profile", data);
            await FirebasePutAsync($"users/{SanitizeKey(username)}/last_session_start", DateTime.UtcNow.ToString("o"));
            await IncrementDailyStatAsync("total_sessions");
            
            App.Log($"[Analytics] Oturum başladı: {username}");
        }

        /// <summary>
        /// Oyun başlatıldığında çağrılır. Hangi sürümün oynandığını kaydeder.
        /// </summary>
        public static async Task TrackGameLaunchAsync(string username, string gameVersion, int ramGb)
        {
            var data = new
            {
                version = gameVersion,
                ram_allocated = ramGb,
                launched_at = DateTime.UtcNow.ToString("o")
            };

            await FirebasePutAsync($"users/{SanitizeKey(username)}/last_game_launch", data);
            await FirebasePostAsync($"users/{SanitizeKey(username)}/game_history", data);
            await IncrementDailyStatAsync("total_game_launches");
            
            // Popüler sürümler istatistiği
            await IncrementCounterAsync($"stats/popular_versions/{SanitizeKey(gameVersion)}");

            App.Log($"[Analytics] Oyun başlatıldı: {username} -> {gameVersion}");

            // Veritabanı şişmesini önlemek için son 30 kayıtla sınırla
            _ = Task.Run(() => LimitNodeCountAsync(username, "game_history", 30));
        }

        /// <summary>
        /// Mod kurulduğunda çağrılır.
        /// </summary>
        public static async Task TrackModInstallAsync(string username, string modName, string modVersion, string gameVersion)
        {
            var data = new
            {
                mod_name = modName,
                mod_version = modVersion,
                game_version = gameVersion,
                installed_at = DateTime.UtcNow.ToString("o")
            };

            await FirebasePostAsync($"users/{SanitizeKey(username)}/installed_mods", data);
            await IncrementCounterAsync($"stats/popular_mods/{SanitizeKey(modName)}");
            await IncrementDailyStatAsync("total_mod_installs");

            App.Log($"[Analytics] Mod kuruldu: {username} -> {modName}");
        }

        /// <summary>
        /// Sunucu başlatıldığında çağrılır.
        /// </summary>
        public static async Task TrackServerStartAsync(string username, string serverVersion, int port)
        {
            var data = new
            {
                version = serverVersion,
                port = port,
                started_at = DateTime.UtcNow.ToString("o")
            };

            await FirebasePostAsync($"users/{SanitizeKey(username)}/server_history", data);
            await IncrementDailyStatAsync("total_server_starts");

            App.Log($"[Analytics] Sunucu başlatıldı: {username} -> {serverVersion}:{port}");

            // Veritabanı şişmesini önlemek için son 30 kayıtla sınırla
            _ = Task.Run(() => LimitNodeCountAsync(username, "server_history", 30));
        }

        /// <summary>
        /// Oturum kapanışında (launcher kapanırken) çağrılır.
        /// </summary>
        public static async Task TrackSessionEndAsync(string username)
        {
            if (!_initialized) return;

            var duration = (DateTime.Now - _sessionStart).TotalMinutes;
            var data = new
            {
                status = "offline",
                last_active = DateTime.UtcNow.ToString("o"),
                last_session_minutes = Math.Round(duration, 1)
            };

            await FirebasePatchAsync($"users/{SanitizeKey(username)}/profile", data);
            
            // Toplam oynama süresi güncelle
            await AddToCounterAsync($"users/{SanitizeKey(username)}/total_minutes", duration);

            App.Log($"[Analytics] Oturum bitti: {username} ({duration:F1} dakika)");
        }

        /// <summary>
        /// Sürüm değiştirildiğinde çağrılır.
        /// </summary>
        public static async Task TrackVersionChangeAsync(string username, string newVersion)
        {
            await FirebasePutAsync($"users/{SanitizeKey(username)}/profile/game_version", $"\"{newVersion}\"", raw: true);
            await IncrementCounterAsync($"stats/popular_versions/{SanitizeKey(newVersion)}");
        }

        /// <summary>
        /// Arkadaş ekleme işlemi kaydı.
        /// </summary>
        public static async Task TrackFriendAddedAsync(string username, string friendName)
        {
            var data = new { friend = friendName, added_at = DateTime.UtcNow.ToString("o") };
            await FirebasePostAsync($"users/{SanitizeKey(username)}/friend_events", data);
        }

        /// <summary>
        /// Alınan launcher/sistem hatalarını veya çökmelerini Firebase'e kaydeder.
        /// </summary>
        public static async Task TrackCrashAsync(string username, string errorMessage, string stackTrace)
        {
            var data = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                error = errorMessage,
                stack_trace = stackTrace,
                os = GetExactOSName(),
                launcher_version = App.LocalVersion
            };

            await FirebasePostAsync($"users/{SanitizeKey(username)}/crashes", data);
            await FirebasePostAsync("global_crashes", new { username = username, error = errorMessage, timestamp = DateTime.UtcNow.ToString("o") });
            
            App.Log($"[Analytics] Hata Firebase'e kaydedildi: {errorMessage}");

            // Veritabanı şişmesini önlemek için son 25 kayıtla sınırla
            _ = Task.Run(() => LimitNodeCountAsync(username, "crashes", 25));
        }

        // ── Admin: Tüm kullanıcıları getir ──────────────────────────
        /// <summary>
        /// Firebase'den tüm kullanıcı verilerini çeker (Admin paneli için).
        /// </summary>
        public static async Task<string?> GetAllUsersAsync()
        {
            try
            {
                var resp = await _http.GetStringAsync($"{FirebaseUrl}/users.json");
                return resp;
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics] Kullanıcı verileri alınamadı: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Firebase'den genel istatistikleri çeker.
        /// </summary>
        public static async Task<string?> GetStatsAsync()
        {
            try
            {
                var resp = await _http.GetStringAsync($"{FirebaseUrl}/stats.json");
                return resp;
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics] İstatistikler alınamadı: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Windows 11 algılama hatasını düzeltir. Microsoft NT 10.0 build 22000 ve üzerini Windows 11 olarak döner.
        /// </summary>
        public static string GetExactOSName()
        {
            try
            {
                var os = Environment.OSVersion;
                if (os.Platform == PlatformID.Win32NT)
                {
                    var vs = os.Version;
                    if (vs.Major == 10)
                    {
                        if (vs.Build >= 22000)
                            return $"Windows 11 (Build {vs.Build})";
                        else
                            return $"Windows 10 (Build {vs.Build})";
                    }
                }
                return os.ToString();
            }
            catch
            {
                return Environment.OSVersion.ToString();
            }
        }

        // ── İç yardımcı metodlar ────────────────────────────────────
        private static async Task FirebasePutAsync(string path, object data, bool raw = false)
        {
            try
            {
                string json = raw ? data.ToString()! : JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PutAsync($"{FirebaseUrl}/{path}.json", content);
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics PUT Error] {path}: {ex.Message}");
            }
        }

        private static async Task FirebasePostAsync(string path, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync($"{FirebaseUrl}/{path}.json", content);
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics POST Error] {path}: {ex.Message}");
            }
        }

        private static async Task FirebasePatchAsync(string path, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{FirebaseUrl}/{path}.json")
                {
                    Content = content
                };
                await _http.SendAsync(request);
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics PATCH Error] {path}: {ex.Message}");
            }
        }

        private static async Task IncrementCounterAsync(string path)
        {
            try
            {
                var resp = await _http.GetStringAsync($"{FirebaseUrl}/{path}.json");
                int current = 0;
                if (resp != null && resp != "null") int.TryParse(resp, out current);
                current++;
                var content = new StringContent(current.ToString(), Encoding.UTF8, "application/json");
                await _http.PutAsync($"{FirebaseUrl}/{path}.json", content);
            }
            catch { }
        }

        private static async Task AddToCounterAsync(string path, double value)
        {
            try
            {
                var resp = await _http.GetStringAsync($"{FirebaseUrl}/{path}.json");
                double current = 0;
                if (resp != null && resp != "null") double.TryParse(resp, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out current);
                current += value;
                var content = new StringContent(current.ToString(System.Globalization.CultureInfo.InvariantCulture), Encoding.UTF8, "application/json");
                await _http.PutAsync($"{FirebaseUrl}/{path}.json", content);
            }
            catch { }
        }

        private static async Task IncrementDailyStatAsync(string statName)
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await IncrementCounterAsync($"stats/daily/{today}/{statName}");
            await IncrementCounterAsync($"stats/totals/{statName}");
        }

        /// <summary>
        /// Firebase path'leri için güvenli anahtar oluşturur (. # $ [ ] / karakterlerini temizler).
        /// </summary>
        private static string SanitizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "unknown";
            return key.Replace(".", "_").Replace("#", "_").Replace("$", "_")
                      .Replace("[", "_").Replace("]", "_").Replace("/", "_");
        }

        public static async Task TrackBanUserAsync(string username, bool banned)
        {
            var data = new { banned = banned };
            await FirebasePatchAsync($"users/{SanitizeKey(username)}/profile", data);
        }

        public static async Task SendAlertMessageAsync(string username, string message)
        {
            var data = new { alert_message = message };
            await FirebasePatchAsync($"users/{SanitizeKey(username)}/profile", data);
        }

        public static async Task SendRemoteModAsync(string username, string modName, string modUrl)
        {
            var data = new { pending_mod_name = modName, pending_mod_url = modUrl };
            await FirebasePatchAsync($"users/{SanitizeKey(username)}/profile", data);
        }

        public static async Task DeleteUserLogsAsync(string username)
        {
            try
            {
                await _http.DeleteAsync($"{FirebaseUrl}/users/{SanitizeKey(username)}/crashes.json");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics DELETE Error] crashes: {ex.Message}");
            }
        }

        public static async Task SyncInstalledModsAsync(string username, System.Collections.Generic.List<string> modNames)
        {
            try
            {
                var data = new System.Collections.Generic.Dictionary<string, object>();
                foreach (var m in modNames)
                {
                    var key = SanitizeKey(m);
                    data[key] = new
                    {
                        mod_name = m,
                        mod_version = "Yerel/Manuel",
                        game_version = ConfigManager.Load().Version ?? "1.21",
                        installed_at = DateTime.UtcNow.ToString("o")
                    };
                }
                await FirebasePutAsync($"users/{SanitizeKey(username)}/installed_mods", data);
                App.Log($"[Analytics] {modNames.Count} adet mod Firebase'e senkronize edildi.");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics SyncMods Error] {ex.Message}");
            }
        }

        private static async Task LimitNodeCountAsync(string username, string nodeName, int maxCount)
        {
            try
            {
                string path = $"users/{SanitizeKey(username)}/{nodeName}";
                var jsonStr = await _http.GetStringAsync($"{FirebaseUrl}/{path}.json");
                if (string.IsNullOrEmpty(jsonStr) || jsonStr == "null") return;

                var token = Newtonsoft.Json.Linq.JToken.Parse(jsonStr);
                if (token is Newtonsoft.Json.Linq.JObject jo)
                {
                    if (jo.Count > maxCount)
                    {
                        var properties = jo.Properties()
                            .OrderBy(p => p.Value["timestamp"]?.ToString() ?? p.Value["launched_at"]?.ToString() ?? p.Value["started_at"]?.ToString() ?? p.Name)
                            .ToList();

                        int toRemove = properties.Count - maxCount;
                        for (int i = 0; i < toRemove; i++)
                        {
                            await _http.DeleteAsync($"{FirebaseUrl}/{path}/{properties[i].Name}.json");
                        }
                        App.Log($"[Analytics Cleanup] {nodeName} dugumundeki {toRemove} eski kayit temizlendi.");
                    }
                }
                else if (token is Newtonsoft.Json.Linq.JArray ja)
                {
                    if (ja.Count > maxCount)
                    {
                        var newList = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JToken>();
                        for (int i = ja.Count - maxCount; i < ja.Count; i++)
                        {
                            if (ja[i] != null && ja[i].Type != Newtonsoft.Json.Linq.JTokenType.Null)
                            {
                                newList.Add(ja[i]);
                            }
                        }
                        var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(newList), Encoding.UTF8, "application/json");
                        await _http.PutAsync($"{FirebaseUrl}/{path}.json", content);
                        App.Log($"[Analytics Cleanup] {nodeName} dizisindeki eski kayitlar temizlendi.");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics Cleanup Error] {nodeName}: {ex.Message}");
            }
        }

        // ── Yeni Admin/Yardımcı Metodlar ─────────────────────────────────────

        /// <summary>
        /// Firebase'deki TÜM kullanıcıların profile/alert_message alanına toplu mesaj yazar.
        /// Önce GetAllUsersAsync() ile tüm kullanıcıları çekip, her birine PATCH ile alert_message yazar.
        /// </summary>
        public static async Task SendBroadcastMessageAsync(string message)
        {
            try
            {
                var json = await GetAllUsersAsync();
                if (string.IsNullOrEmpty(json) || json == "null") return;

                var users = Newtonsoft.Json.Linq.JObject.Parse(json);
                foreach (var user in users.Properties())
                {
                    string username = user.Name;
                    var data = new { alert_message = message };
                    string patchJson = JsonConvert.SerializeObject(data);
                    var content = new StringContent(patchJson, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{FirebaseUrl}/users/{SanitizeKey(username)}/profile.json")
                    {
                        Content = content
                    };
                    await _http.SendAsync(request);
                }

                App.Log($"[Analytics] Toplu mesaj gönderildi: {message}");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics Broadcast Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Birden fazla kullanıcıyı tek seferde banlar veya ban kaldırır.
        /// Her biri için TrackBanUserAsync çağrısı yapar.
        /// </summary>
        public static async Task BanMultipleUsersAsync(List<string> usernames, bool ban)
        {
            try
            {
                foreach (var username in usernames)
                {
                    await TrackBanUserAsync(username, ban);
                }

                App.Log($"[Analytics] {usernames.Count} kullanıcı {(ban ? "banlandı" : "ban kaldırıldı")}.");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics BanMultiple Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Tüm kullanıcıların crashes verilerini siler.
        /// GetAllUsersAsync ile kullanıcıları çekip, her birinin crashes altını DELETE ile temizler.
        /// </summary>
        public static async Task DeleteAllCrashLogsAsync()
        {
            try
            {
                var json = await GetAllUsersAsync();
                if (string.IsNullOrEmpty(json) || json == "null") return;

                var users = Newtonsoft.Json.Linq.JObject.Parse(json);
                foreach (var user in users.Properties())
                {
                    string username = user.Name;
                    await _http.DeleteAsync($"{FirebaseUrl}/users/{SanitizeKey(username)}/crashes.json");
                }

                // Global crash loglarını da temizle
                await _http.DeleteAsync($"{FirebaseUrl}/global_crashes.json");

                App.Log("[Analytics] Tüm crash logları silindi.");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics DeleteAllCrash Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Firebase veritabanının tamamını JSON string olarak döndürür (GET /users.json).
        /// </summary>
        public static async Task<string?> ExportDatabaseAsync()
        {
            try
            {
                var resp = await _http.GetStringAsync($"{FirebaseUrl}/users.json");
                App.Log("[Analytics] Veritabanı dışa aktarıldı.");
                return resp;
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics Export Error] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// daysThreshold günden uzun süredir aktif olmayan kullanıcıları temizler.
        /// profile/last_active tarihini kontrol eder, eski olanları DELETE ile siler.
        /// Silinen kullanıcı sayısını döndürür.
        /// </summary>
        public static async Task<int> CleanInactiveUsersAsync(int daysThreshold)
        {
            int deletedCount = 0;
            try
            {
                var json = await GetAllUsersAsync();
                if (string.IsNullOrEmpty(json) || json == "null") return 0;

                var users = Newtonsoft.Json.Linq.JObject.Parse(json);
                var cutoffDate = DateTime.UtcNow.AddDays(-daysThreshold);

                foreach (var user in users.Properties())
                {
                    string username = user.Name;
                    try
                    {
                        var lastActiveStr = user.Value?["profile"]?["last_active"]?.ToString();
                        if (string.IsNullOrEmpty(lastActiveStr)) continue;

                        if (DateTime.TryParse(lastActiveStr, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastActive))
                        {
                            if (lastActive < cutoffDate)
                            {
                                await _http.DeleteAsync($"{FirebaseUrl}/users/{SanitizeKey(username)}.json");
                                deletedCount++;
                            }
                        }
                    }
                    catch
                    {
                        // Tek bir kullanıcı hata verirse devam et
                    }
                }

                App.Log($"[Analytics] {deletedCount} aktif olmayan kullanıcı temizlendi (eşik: {daysThreshold} gün).");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics CleanInactive Error] {ex.Message}");
            }
            return deletedCount;
        }

        /// <summary>
        /// Kullanıcının profile/gpu alanına GPU adını yazar (PATCH).
        /// </summary>
        public static async Task TrackGpuInfoAsync(string user, string gpuName)
        {
            try
            {
                var data = new { gpu = gpuName };
                string patchJson = JsonConvert.SerializeObject(data);
                var content = new StringContent(patchJson, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{FirebaseUrl}/users/{SanitizeKey(user)}/profile.json")
                {
                    Content = content
                };
                await _http.SendAsync(request);

                App.Log($"[Analytics] GPU bilgisi kaydedildi: {user} -> {gpuName}");
            }
            catch (Exception ex)
            {
                App.Log($"[Analytics GPU Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Seçilen yerel mod (.jar) dosyasını catbox.moe bulut sunucusuna yükler ve doğrudan indirme linkini döndürür.
        /// </summary>
        public static async Task<string> UploadFileToCatboxAsync(string filePath)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var content = new MultipartFormDataContent();
            
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            
            content.Add(new StringContent("fileupload"), "reqtype");
            content.Add(fileContent, "fileToUpload", System.IO.Path.GetFileName(filePath));
            
            var response = await client.PostAsync("https://catbox.moe/user/api.php", content);
            response.EnsureSuccessStatusCode();
            
            string fileUrl = await response.Content.ReadAsStringAsync();
            return fileUrl.Trim();
        }
    }
}



