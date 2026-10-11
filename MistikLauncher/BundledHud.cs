using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public static class BundledHud
{
    public const string Filename = "mistik-hud.jar";
    const string Resource = "MistikLauncher.mistik-hud.jar";

    public static void Install(string gameDirectory)
    {
        using var resource = typeof(BundledHud).Assembly.GetManifestResourceStream(Resource)
            ?? throw new IOException("The bundled Mistik HUD is missing. Reinstall the launcher.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Invalid bundled HUD size.");
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            var metadata = zip.GetEntry("fabric.mod.json");
            if (metadata == null || metadata.Length > 64 * 1024) throw new InvalidDataException("Missing HUD metadata.");
            using var reader = new StreamReader(metadata.Open());
            var mod = JObject.Parse(reader.ReadToEnd(), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (mod["id"]?.ToString() != "mistik_hud" || string.IsNullOrWhiteSpace(mod["version"]?.ToString()) ||
                mod["depends"]?["minecraft"]?.ToString() != "1.21.1")
                throw new InvalidDataException("The bundled HUD does not support Minecraft 1.21.1.");
        }
        var root = Path.Combine(gameDirectory, "mods");
        ModFiles.EnsureRoot(root);
        var target = Path.Combine(root, Filename);
        var disabled = target + ".disabled";
        foreach (var mod in ModFiles.List(root))
        {
            var id = HudSettings.ModId(mod);
            if ((mod == target || mod == disabled) && id != "mistik_hud")
                throw new IOException("A personal mod already uses the Mistik HUD filename.");
            if (id == "mistik_hud" && mod != target && mod != disabled)
                throw new IOException("Remove the other Mistik HUD JAR before installing this version.");
        }
        if (File.Exists(target) && File.Exists(disabled)) throw new IOException("Both enabled and disabled Mistik HUD files exist.");
        var previousPath = File.Exists(target) ? target : File.Exists(disabled) ? disabled : null;
        var previousBytes = previousPath == null ? null : File.ReadAllBytes(previousPath);
        var legacy = HudSettings.LegacyMods(gameDirectory).Where(ModFiles.Enabled).ToArray();
        foreach (var mod in legacy)
            if (File.Exists(mod + ".disabled")) throw new IOException("A previous HUD has conflicting enabled and disabled files.");
        var moved = new List<string>();
        bool installed = false;
        try
        {
            ModFiles.Install(root, Filename, bytes, Convert.ToHexString(SHA512.HashData(bytes)));
            installed = true;
            foreach (var mod in legacy) moved.Add(ModFiles.Toggle(root, mod));
            HudSettings.SetEnabled(gameDirectory, true);
        }
        catch
        {
            foreach (var mod in moved.AsEnumerable().Reverse()) ModFiles.Toggle(root, mod);
            if (installed)
            {
                if (File.Exists(target)) File.Delete(target);
                if (File.Exists(disabled)) File.Delete(disabled);
                if (previousPath != null) File.WriteAllBytes(previousPath, previousBytes!);
            }
            throw;
        }
    }
}
