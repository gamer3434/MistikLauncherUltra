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

namespace MistikLauncherUltra
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
        [JsonProperty("version_code")] public string VersionCode { get; set; } = "v5.0.0";
        [JsonProperty("open_count")]   public int OpenCount   { get; set; } = 0;
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

    // â”€â”€ Server list â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public record ServerEntry(string Name, string Ip, int Port, string Mode, string Ver, int Max, string Color, string Icon);

    public static class App
    {
        public static readonly string AppData = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mistik_ultra");
        public static readonly string GameDir  = System.IO.Path.Combine(AppData, "game");
        public static readonly string ModsDir  = System.IO.Path.Combine(GameDir, "mods");
        public static readonly string LogFile  = System.IO.Path.Combine(AppData, "launcher.log");
        public const  string LocalVersion = "v5.0.0";
        public const  string AdminPassword = "mistik3434";

        public static readonly List<ServerEntry> Servers = new()
        {
            new("CraftRise", "play.craftrise.com.tr", 25565, "Tum Oyunlar",  "1.8-1.21",  5000,   "#00A3FF", "O"),
            new("Hypixel",   "mc.hypixel.net",          25565,"Mini Oyunlar","1.8-1.21",200000,"#FFB100","ğŸŒŸ"),
            new("GomeMC",    "play.gomemc.com",       25565, "Turkiye PvP",  "1.8.9",     2000,   "#FF4B4B", "K"),
            new("CubeCraft","play.cubecraft.net",       25565,"Mini Oyunlar","1.8-1.21",30000, "#00D4AA","ğŸ®"),
            new("Wynncraft","play.wynncraft.com",       25565,"MMORPG",      "1.12-1.21",5000, "#888888","ğŸ—¡ï¸"),
        };

        // Accepts 6 or 7 args (max optional)

        public static readonly List<ChangelogEntry> Changelog = new()
        {
            new("v5.0.0","2026-05-19","#00A3FF", new[]{ "C# WPF'e geÃ§iÅŸ â€“ antivirÃ¼s false-positive yok","MQTT relay sistemi â€“ IP paylaÅŸÄ±lmaz","Otomatik SSH oyun tÃ¼neli (Serveo.net)","GerÃ§ek skin Ã¶nizleme galerisi" }),
            new("v4.3.0","2026-05-18","#2EB82E", new[]{ "P2P arkadaÅŸ sistemi eklendi","Skin yamasÄ± (CustomSkinLoader)","Performans iyileÅŸtirmeleri" }),
            new("v4.0.0","2026-05-16","#FFB100", new[]{ "SÃ¼rÃ¼m YÃ¶neticisi","Mod Merkezi (Modrinth)","Bulut gÃ¼ncellemeler" }),
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
                    if (upd != null && upd.Version != App.LocalVersion)
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

        // ─── SSH Oyun Tüneli ───────────────────────────────────────────────────────
        public void StartTunnel(int localPort = 25565, string gateway = "serveo.net", string? customSubdomain = null)
        {
            string subdomain = !string.IsNullOrEmpty(customSubdomain) ? customSubdomain.Trim().ToLower() : $"mistik{RoomCode.ToLower()}";
            string ssh = FindSsh();
            if (string.IsNullOrEmpty(ssh))
            {
                OnTunnelLog?.Invoke("[HATA] Bilgisayarınızda OpenSSH bulunamadı! Lütfen Windows İsteğe Bağlı Özellikler'den OpenSSH İstemcisi'ni etkinleştirin.");
                OnTunnelReady?.Invoke(null);
                return;
            }

            OnTunnelLog?.Invoke($"[SİSTEM] SSH İstemcisi bulundu: {ssh}");
            
            string args = "";
            if (gateway == "serveo.net")
            {
                args = $"-o StrictHostKeyChecking=no -o ServerAliveInterval=10 -o BatchMode=yes " +
                       $"-R {subdomain}:25565:localhost:{localPort} serveo.net";
                OnTunnelLog?.Invoke($"[SİSTEM] Serveo tüneli başlatılıyor (Yerel Port: {localPort}, Özel Adres: {subdomain}.serveo.net)...");
            }
            else // localhost.run
            {
                args = $"-o StrictHostKeyChecking=no -o ServerAliveInterval=10 -o BatchMode=yes " +
                       $"-R 80:localhost:{localPort} nokey@localhost.run";
                OnTunnelLog?.Invoke($"[SİSTEM] Localhost.run tüneli başlatılıyor (Yerel Port: {localPort}, Rastgele Alt Alan Adı)...");
            }

            var psi = new ProcessStartInfo
            {
                FileName  = ssh,
                Arguments = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            try
            {
                _tunnelProc = Process.Start(psi)!;
            }
            catch (Exception ex)
            {
                OnTunnelLog?.Invoke($"[HATA] Tünel başlatılırken sistem hatası oluştu: {ex.Message}");
                OnTunnelReady?.Invoke(null);
                return;
            }

            // Read stdout
            Task.Run(() =>
            {
                try
                {
                    string? line;
                    while (_tunnelProc != null && (line = _tunnelProc.StandardOutput.ReadLine()) != null)
                    {
                        OnTunnelLog?.Invoke($"[INFO] {line}");

                        bool found = false;
                        var mServeo = Regex.Match(line, @"Forwarding TCP connections from (\S+)");
                        if (mServeo.Success)
                        {
                            TunnelAddress = mServeo.Groups[1].Value;
                            found = true;
                        }
                        
                        var mLocalhostRun = Regex.Match(line, @"(\S+\.lhr\.(?:life|pro|run))");
                        if (mLocalhostRun.Success)
                        {
                            TunnelAddress = mLocalhostRun.Groups[1].Value;
                            found = true;
                        }

                        if (found && TunnelAddress != null)
                        {
                            _myInfo = _myInfo with { Tunnel = TunnelAddress };
                            _ = Publish();
                            OnTunnelReady?.Invoke(TunnelAddress);
                            OnTunnelLog?.Invoke($"[SİSTEM] Bağlantı Başarılı! Adresiniz: {TunnelAddress}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnTunnelLog?.Invoke($"[HATA] Çıkış kanalı okuma hatası: {ex.Message}");
                }
            });

            // Read stderr
            Task.Run(() =>
            {
                try
                {
                    string? line;
                    while (_tunnelProc != null && (line = _tunnelProc.StandardError.ReadLine()) != null)
                    {
                        OnTunnelLog?.Invoke($"[UYARI] {line}");
                    }
                }
                catch (Exception ex)
                {
                    OnTunnelLog?.Invoke($"[HATA] Hata kanalı okuma hatası: {ex.Message}");
                }
            });
        }

        public void StopTunnel()
        {
            try { _tunnelProc?.Kill(true); } catch { }
            _tunnelProc  = null;
            TunnelAddress = null;
            _myInfo = _myInfo with { Tunnel = null };
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

    // â”€â”€ Modrinth API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static class ModrinthApi
    {
        static readonly HttpClient Http = new() { BaseAddress = new Uri("https://api.modrinth.com/v2/") };

        public static async Task<List<ModResult>> SearchAsync(string query, string version = "")
        {
            var url = $"search?query={Uri.EscapeDataString(query)}&facets=[[\"project_type:mod\"]]&limit=20";
            if (!string.IsNullOrEmpty(version))
                url += $"&facets=[[\"project_type:mod\"],[\"versions:{version}\"]]";
            try
            {
                var json = await Http.GetStringAsync(url);
                var d    = JsonConvert.DeserializeObject<dynamic>(json)!;
                var list = new List<ModResult>();
                foreach (var h in d.hits)
                    list.Add(new ModResult
                    {
                        Id          = h.project_id ?? "",
                        Name        = h.title      ?? "",
                        Description = h.description ?? "",
                        Downloads   = (int)(h.downloads ?? 0),
                        Url         = $"https://modrinth.com/mod/{h.slug}"
                    });
                return list;
            }
            catch { return new(); }
        }

        public static async Task<string?> GetLatestDownloadUrl(string projectId, string version)
        {
            try
            {
                var json = await Http.GetStringAsync($"project/{projectId}/version?game_versions=[\"{version}\"]&loaders=[\"fabric\",\"forge\",\"quilt\"]");
                var versions = JsonConvert.DeserializeObject<dynamic>(json)!;
                if (versions.Count == 0) return null;
                var files = versions[0].files;
                return (string?)files[0].url;
            }
            catch { return null; }
        }
    }

    public class ModResult
    {
        public string Id          { get; set; } = "";
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public int    Downloads   { get; set; }
        public string Url         { get; set; } = "";
    }

    public class UpdateMessage
    {
        [JsonProperty("version")]   public string Version   { get; set; } = "";
        [JsonProperty("url")]       public string Url       { get; set; } = "";
        [JsonProperty("changelog")] public string Changelog { get; set; } = "";
    }
}



