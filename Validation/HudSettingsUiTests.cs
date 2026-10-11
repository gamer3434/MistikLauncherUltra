using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using Localization = MistikLauncher.Localization;
using MistikLauncher.Pages;

public static class HudSettingsUiTests
{
    public static int Run(MainWindow window)
    {
        var isolated = Environment.GetEnvironmentVariable("MISTIK_DATA_DIR");
        if (string.IsNullOrEmpty(isolated) || !Path.GetFullPath(isolated).Equals(Path.GetFullPath(App.AppData), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HUD UI tests require an isolated launcher data directory.");
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        Directory.CreateDirectory(App.ModsDir);
        var fixture = Path.Combine(App.ModsDir, "hud-ui-fixture.jar");
        if (File.Exists(fixture) || File.Exists(fixture + ".disabled")) throw new IOException("HUD UI fixture already exists.");
        var originalMods = ModFiles.List(App.ModsDir).ToArray();
        var configDirectory = Path.Combine(App.GameDir, "config", "custom-hud");
        var paths = new[] { Path.Combine(configDirectory, "config.json"), Path.Combine(configDirectory, "profile1.txt"), Path.Combine(App.GameDir, "config", "ukus-armor-hud.toml") };
        var originals = paths.SelectMany(path => new[] { path, path + ".mistik-hud-backup" }).ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
        var language = Localization.Language;
        var user = window.Config.User;
        try
        {
            using (var archive = ZipFile.Open(fixture, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("fabric.mod.json").Open())) writer.Write("{\"id\":\"custom_hud\",\"version\":\"1\"}");
            Localization.SetLanguage("tr");
            var page = new ModernSettingsPage(window);
            CheckBox Checkbox() => Nodes(page).OfType<CheckBox>().Single(control => control.Name == "HudEnabledBox");
            Check(Checkbox().Content?.ToString() == "Oyun içi HUD açık", "settings exposes Turkish in game HUD checkbox");
            Checkbox().IsChecked = false; Checkbox().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!HudSettings.IsEnabled(App.GameDir) && File.Exists(fixture + ".disabled"), "settings HUD off click persists disabled state without deleting mod");
            Checkbox().IsChecked = true; Checkbox().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(HudSettings.IsEnabled(App.GameDir) && File.Exists(fixture) && File.Exists(Path.Combine(configDirectory, "profile1.txt")), "settings HUD on click restores mod and creates visible HUD profile");
            Localization.SetLanguage("en"); page.RefreshLanguage();
            Check(Checkbox().Content?.ToString() == "Enable in-game HUD" && Checkbox().IsChecked == true && window.Config.User == user,
                "HUD checkbox language refresh keeps enabled state and player name");
        }
        finally
        {
            foreach (var path in new[] { fixture, fixture + ".disabled" }) if (File.Exists(path)) File.Delete(path);
            foreach (var path in originalMods)
                if (!File.Exists(path))
                {
                    var counterpart = ModFiles.Enabled(path) ? path + ".disabled" : path[..^9];
                    if (File.Exists(counterpart)) File.Move(counterpart, path);
                }
            foreach (var item in originals)
                if (item.Value == null) { if (File.Exists(item.Key)) File.Delete(item.Key); }
                else { Directory.CreateDirectory(Path.GetDirectoryName(item.Key)!); File.WriteAllBytes(item.Key, item.Value); }
            Localization.SetLanguage(language);
        }
        return checks;
    }

    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var node in Nodes(child)) yield return node;
    }
}
