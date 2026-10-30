using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MistikLauncher;

public partial class MainWindow
{
    CancellationTokenSource? _modUpdateCancellation;
    ModAutoUpdater? _preparedModUpdate;
    string? _modUpdateProfile;
    string? _checkedModUpdateProfile;
    DateTime _lastModUpdateCheck;
    bool _modUpdatesStopped;

    internal async void QueueBackgroundModUpdates()
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(new Action(QueueBackgroundModUpdates));
            return;
        }
        if (_modUpdatesStopped || !IsLoaded || launchPreparing || gameRunning) return;
        var profile = Config.Version ?? "";
        if (_modUpdateProfile != profile || !Config.ModAutoUpdate)
        {
            _modUpdateCancellation?.Cancel();
            _preparedModUpdate?.Dispose(); _preparedModUpdate = null;
        }
        if (!Config.ModAutoUpdate || string.IsNullOrWhiteSpace(profile) || _modUpdateCancellation != null || _preparedModUpdate != null ||
            (_checkedModUpdateProfile == profile && DateTime.UtcNow - _lastModUpdateCheck < TimeSpan.FromMinutes(30))) return;
        var loader = GameProfiles.Loader(App.GameDir, profile);
        if (loader is not ("fabric" or "quilt")) return;
        var game = GameProfiles.MinecraftVersion(App.GameDir, profile);
        _modUpdateProfile = profile;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        _modUpdateCancellation = cancellation;
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { MaxResponseContentBufferSize = 128L * 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MistikLauncher/" + LauncherUpdates.CurrentVersion);
            var update = await Task.Run(() => ModAutoUpdater.PrepareAsync(client, App.ModsDir, game, loader, cancellation.Token));
            if (cancellation.IsCancellationRequested || _modUpdatesStopped || launchPreparing || gameRunning || !Config.ModAutoUpdate || Config.Version != profile)
            { update.Dispose(); return; }
            _checkedModUpdateProfile = profile; _lastModUpdateCheck = DateTime.UtcNow;
            if (update.Count > 0) _preparedModUpdate = update;
            else update.Dispose();
        }
        catch (Exception error)
        { App.Log("Background mod update skipped; existing mods retained: " + error.Message); }
        finally
        {
            _modUpdateCancellation = null;
            if (!_modUpdatesStopped && Config.Version != profile) QueueBackgroundModUpdates();
        }
    }

    internal ModAutoUpdater? TakePreparedModUpdate(string profile)
    {
        _modUpdateCancellation?.Cancel();
        var update = _preparedModUpdate; _preparedModUpdate = null;
        if (gameRunning || !Config.ModAutoUpdate || _modUpdateProfile != profile || Config.Version != profile)
        { update?.Dispose(); return null; }
        return update;
    }

    void StopModUpdates()
    {
        _modUpdatesStopped = true;
        _modUpdateCancellation?.Cancel();
        _preparedModUpdate?.Dispose(); _preparedModUpdate = null;
    }
}
