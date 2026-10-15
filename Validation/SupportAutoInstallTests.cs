using System.Reflection;
using MistikLauncher;
using Newtonsoft.Json;

static class SupportAutoInstallTests
{
    public static int Run(MainWindow window)
    {
        int checks = 0, confirmations = 0, installations = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        var original = window.Config;
        var config = JsonConvert.DeserializeObject<LauncherConfig>(JsonConvert.SerializeObject(original))!;
        var method = typeof(MainWindow).GetMethod("ApplySupportRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var request = new SupportRequest("0123456789abcdef0123456789abcdef", "AABB0011", "Automatic fixture", config.Version, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        bool Confirm(string message, string title) { confirmations++; return false; }
        Task Install() { installations++; return Task.CompletedTask; }
        void Apply(Func<string, string, bool>? confirm = null, CancellationToken cancellation = default)
            => ((Task)method.Invoke(window, new object[] { request, cancellation, confirm ?? Confirm, (Func<Task>)Install })!).GetAwaiter().GetResult();
        try
        {
            var upgraded = JsonConvert.DeserializeObject<LauncherConfig>("{\"support_sharing_enabled\":true,\"support_requests_enabled\":true}")!;
            Check(!upgraded.SupportAutoInstallEnabled && !new LauncherConfig().SupportAutoInstallEnabled,
                "upgrading suggestion consent never grants automatic installation and new installs default to manual approval");
            var preference = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            preference.Config = new LauncherConfig(); // No support service: preference tests cannot contact Firebase.
            preference.SetSupportSharing(true, true, true).GetAwaiter().GetResult();
            Check(ConfigManager.Load().SupportAutoInstallEnabled, "explicit automatic installation consent persists in encrypted settings");
            preference.SetSupportSharing(true, false, true).GetAwaiter().GetResult();
            Check(!preference.Config.SupportAutoInstallEnabled && !ConfigManager.Load().SupportAutoInstallEnabled,
                "disabling suggestions also revokes and saves automatic installation consent");
            preference.SetSupportSharing(false, true, true).GetAwaiter().GetResult();
            Check(!preference.Config.SupportRequestsEnabled && !preference.Config.SupportAutoInstallEnabled,
                "disabled support cannot retain suggestion or automatic installation authority");
            Check(!ConfigManager.Normalize(new LauncherConfig { SupportAutoInstallEnabled = true }).SupportAutoInstallEnabled,
                "normalization rejects automatic consent without its required support permissions");
            window.Config = config;
            config.SupportSharingEnabled = config.SupportRequestsEnabled = config.SupportAutoInstallEnabled = true;
            Apply();
            Check(confirmations == 0 && installations == 1, "explicit automatic consent installs without invoking the confirmation dialog");
            confirmations = installations = 0; config.SupportAutoInstallEnabled = false;
            Apply();
            Check(confirmations == 1 && installations == 0, "manual mode still asks and rejection performs no installation");
            confirmations = installations = 0;
            Apply((_, _) => { confirmations++; return true; });
            Check(confirmations == 1 && installations == 1, "manual acceptance still installs exactly once");
            confirmations = installations = 0; config.SupportAutoInstallEnabled = true; config.SupportSharingEnabled = false;
            Apply(); config.SupportSharingEnabled = true; config.SupportRequestsEnabled = false; Apply(); config.SupportRequestsEnabled = true;
            Check(confirmations == 0 && installations == 0, "automatic mode cannot bypass missing support or suggestion consent");
            var profile = config.Version; config.Version = "different-profile"; Apply(); config.Version = profile;
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Apply(cancellation: cancelled.Token);
            Check(confirmations == 0 && installations == 0, "automatic mode rejects changed profiles and cancelled requests before installation");
            foreach (var name in new[] { "launchPreparing", "gameRunning" })
            {
                var field = typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
                var previous = field.GetValue(window);
                try { field.SetValue(window, true); Apply(); }
                finally { field.SetValue(window, previous); }
            }
            Check(confirmations == 0 && installations == 0, "automatic installation remains blocked while preparing or running the game");
            config.SupportAutoInstallEnabled = false;
            Apply((_, _) => { config.SupportRequestsEnabled = false; return true; });
            Check(installations == 0, "consent revoked during a manual dialog is rechecked before installation");
            return checks;
        }
        finally { window.Config = original; ConfigManager.Save(original); }
    }
}
