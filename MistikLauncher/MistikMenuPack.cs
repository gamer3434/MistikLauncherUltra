using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MistikLauncher;

public static class MistikMenuPack
{
    public const string Filename = "Mistik-Menu-1.21.zip";
    const string Marker = "Mistik menu resource pack v1";

    public static void Install(string gameDirectory, string gameVersion)
    {
        if (gameVersion is not ("1.21" or "1.21.1")) throw new IOException("Mistik menu supports Minecraft 1.21 and 1.21.1.");
        var root = Path.GetFullPath(gameDirectory);
        var packDirectory = Path.Combine(root, "resourcepacks");
        var path = Path.Combine(packDirectory, Filename);
        var options = Path.Combine(root, "options.txt");
        CheckPath(path); CheckPath(options);
        var lines = File.Exists(options) ? File.ReadAllLines(options).ToList() : new List<string>();
        var selection = lines.FindIndex(line => line.StartsWith("resourcePacks:", StringComparison.Ordinal));
        if (lines.Count(line => line.StartsWith("resourcePacks:", StringComparison.Ordinal)) > 1) throw new IOException("Duplicate resource pack settings.");
        var packs = selection < 0 ? new JArray() : JArray.Parse(lines[selection]["resourcePacks:".Length..]);
        if (packs.Any(pack => pack.Type != JTokenType.String)) throw new IOException("Invalid resource pack settings.");
        var id = "file/" + Filename;
        if (!packs.Any(pack => pack.ToString() == id)) packs.Add(id);
        var updated = "resourcePacks:" + packs.ToString(Formatting.None);
        var optionsChanged = selection < 0 || lines[selection] != updated;
        if (selection < 0) lines.Add(updated); else lines[selection] = updated;

        if (File.Exists(path))
        {
            using var existing = ZipFile.OpenRead(path);
            var marker = existing.GetEntry("mistik-menu.txt");
            if (marker == null || marker.Length > 128) throw new IOException("A different resource pack already uses the Mistik menu filename.");
            using var reader = new StreamReader(marker.Open());
            if (reader.ReadToEnd() != Marker) throw new IOException("Existing Mistik menu pack was changed; keep it or rename it before reinstalling.");
        }
        else
        {
            Directory.CreateDirectory(packDirectory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create)) WritePack(archive);
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (!optionsChanged) return;
        var optionsTemporary = options + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var backup = options + ".mistik-menu-backup";
            CheckPath(backup);
            if (File.Exists(options) && !File.Exists(backup)) File.Copy(options, backup);
            File.WriteAllLines(optionsTemporary, lines, new UTF8Encoding(false));
            File.Move(optionsTemporary, options, overwrite: true);
        }
        finally { if (File.Exists(optionsTemporary)) File.Delete(optionsTemporary); }
    }

    static void CheckPath(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Mistik menu cannot modify linked paths.");
    }

    static void WritePack(ZipArchive archive)
    {
        void Bytes(string name, byte[] bytes) { using var stream = archive.CreateEntry(name).Open(); stream.Write(bytes); }
        void Text(string name, string text) => Bytes(name, Encoding.UTF8.GetBytes(text));
        void Image(string name, string data) => Bytes(name, Convert.FromBase64String(data));
        Text("mistik-menu.txt", Marker);
        Text("pack.mcmeta", "{\"pack\":{\"pack_format\":34,\"description\":\"Mistik menu: cyan branding, dark buttons and blue panorama\"}}");
        Image("assets/minecraft/textures/gui/title/minecraft.png", Title);
        Image("assets/minecraft/textures/gui/title/minceraft.png", Title);
        Image("assets/minecraft/textures/gui/title/edition.png", Edition);
        for (var index = 0; index < 6; index++) Image($"assets/minecraft/textures/gui/title/background/panorama_{index}.png", Panorama);
        foreach (var (name, image) in new[] { ("button", Button), ("button_highlighted", Highlighted), ("button_disabled", Disabled) })
        {
            Image($"assets/minecraft/textures/gui/sprites/widget/{name}.png", image);
            Text($"assets/minecraft/textures/gui/sprites/widget/{name}.png.mcmeta", "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":200,\"height\":20,\"border\":3}}}");
        }
    }

    const string Title = "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";
    const string Edition = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAAgCAYAAAD9qabkAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAP/SURBVHhe7Zrrbd0wDIUzQBfIAlmgC3SCTNANukFXuCtkhi6RLTKMXPBCFI6PSdmShQIFzvcrl6QoUaT1iP3yIoQQQgghhBBCCCGEEEIIIYQQQgghhBBCCPFPKaX82oBSyifbGKWUn2T3B3To43skL6V8bdv2rTmsQLvNZTwmBPt1zK+Nz3Rk+7lt24+eTxtXKeVRSnllvw7HjjEiWR+GjY3H1yH0j0zEHPrMbM5iieyu5pjH3CEcs1hIlOjoYaiFhTZhEfSKqJTyuzmsoN5l3A7hBaCU8lYLL8R8nfkEDsVrBLE/2Mbo9bFyAZiMOfSZ2ZzFktldyfGqeRAL4AQatrOQzWtgkxVBt4iseJvjoDiCdmkR2AMLdub7wxevukO+eyyRT7PFB4njdhvQN1u2M6I+Mkzvhryo9bgbM5PZZHJmNsegm5oHsQhMoO90fA3wIzDuhDcWgJ1v1CXtesX3cKNo50EynxT/c+dEzC/qm4d6zCbbsI+I2cJfETOS2WRyZjbHoJuaB7EIegCw2Ns1ABYG1A8tAFS4badtrSYWALAxwuO7k/mkmKITwHPXr0d4PA0crgFZHxGzhQ/+jamYkcwmkzOzOQbd1DyIRVAC3+BvP0Zi0aN+aAEwOd794NjaSNrtMF1tN1Q4PBaToY/K7oFK5qOdgtC26g59ZIyO3xhtc2U8mc2VHAR2l3PsjMYkFoMJrL99x3se4+D4/2W/o2RxEWRyWkw+0J+RtNuxaAHYUXf33b21tjmciFDG14CTPnbXi9HxG6NteP5Zb2Q2V2Ph9ldz7IzGJBYTLAC7osfjv+mjZHER9OQsi3ZUtnE5Mlo4JwX9zvYG6J+Ln0Gngt014KSP/3kBCNsakR3Lohw7ozGJxQQLABZ40/kOCb+nFoAq91OGvYNvR8azdkjwZmLoPoxxGnwCsN0d9R1av9wH+mNmCv9uzKw3MptMzmR2Zzl2ZuZBLIQXgCrzXb8l0XVRsjpFEMox6chZO4Ze4R3+g49EPqn4WozVvv1DqweeHqI+MmYL/27MTGaTyZnM7izHkd3IPIhFJAsAf/XXXjeBbHoBqLrDA3alHcK7tLWDu7pdX07fiePuhA8U2EZF2/r1e66R9RExW/grYkYym0zO9Ox6OXZm50EsIlkAdkdNPB6D7NYCwB+0GEm7A+jHCp71iD/U2ViCWJ8PEfw+vO4zsI0fxbM+Iu4U/mTMBwKbLHcHErvLOQab6XkQC4gWgCpv1wCUR8nKiiCTg35XyCC/VHxgb5/G2vf8u89jbXf2fntjQZ3FVdv57/AfhJFNrw/mbuFPxHwgsMlydyCxO8Sc5di5Ow9CCCGEEEIIIYQQQgghhBBCCCGEEEIIsZS/UUPFgrA9OjcAAAAASUVORK5CYII=";
    const string Panorama = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAPlSURBVHhe7dRLYhRUFADROyEdmnTS4U++JCCCiCCCiCDuf1XPuVuoMziTWkDN6e2/C2ia/wegwwAgzAAgzAAgbE5vfyygyQAgzAAgzAAgbPY3PxbQZAAQZgAQZgAQNvubfxbQZAAQZgAQZgAQZgAQZgAQNvub7wtoMgAIm/3z7wtoMgAIMwAIMwAIm/3zbwtoMgAIMwAIMwAIm/31twU0zf767wU0GQCEGQCEGQCEGQCEGQCEGQCEzf766wKaDADCDADCDADC5uTq6wKaDADCDADCDADC5uTqrwU0GQCEGQCEGQCEGQCEGQCEGQCEGQCEzcnVlwU0GQCEGQCEGQCEzfHllwU0GQCEGQCEGQCEzfHlnwtoMgAIMwAIMwAIMwAIMwAIm+PLzwtoMgAIm+OLzwtoMgAIMwAIMwAIm93FpwU0GQCEGQCEGQCEze780wKaZnf+xwKaDADCDADCDADCDADCDADCDADCZnf+cQFNBgBhBgBhBgBhszv7uIAmA4AwA4AwA4Cw2Z39voAmA4AwA4AwA4AwA4AwA4Cw2Z19WECTAUDY7J59WECTAUCYAUCYAUDYHD17v4AmA4AwA4AwA4CwOXr6fgFNc/T0twU0GQCEGQCEGQCEGQCEGQCEGQCEzdHTdwtoMgAIMwAIMwAIm3tP3i2gyQAgzAAgzAAgbO49+XUBTQYAYQYAYQYAYQYAYQYAYQYAYQYAYXPvydsFNBkAhBkAhBkAhM328dsFNBkAhBkAhBkAhM328S8LaDIACDMACDMACDMACDMACJvt4zcLaDIACJvtozcLaDIACDMACDMACJvto9cLaDIACDMACDMACJvtw9cLaJrtw58X0GQAEGYAEGYAEGYAEGYAEGYAEDbbh68W0GQAEGYAEGYAEDZ3H7xaQJMBQJgBQJgBQNjcffDTApoMAMIMAMIMAMIMAMIMAMIMAMIMAMLm7oOXC2gyAAgzAAgzAAibw/svF9BkABBmABBmABA2h/dfLKDJACDMACDMACDMACDMACBsDu/fLqDJACBsDk9vF9BkABBmABBmABA2m9ObBTQZAIQZAIQZAITNZn+zgKbZ7J8voMkAIMwAIMwAIMwAIMwAIMwAIGw2++sFNBkAhBkAhBkAhM3m5HoBTQYAYQYAYQYAYbM5uVpAkwFAmAFAmAFAmAFAmAFA2GxOLhfQZAAQNpvjywU0GQCEGQCEGQCEzcHxxQKaDADCDADCDADC5mB3sYCmOdidL6DJACDMACDMACDMACDMACDMACBsDnZnC2gyAAgzAAgzAAibO0dnC2gyAAgzAAgzAAibO0fPFtBkABBmABD2H4m4lYSQ8R8tAAAAAElFTkSuQmCC";
    const string Button = "iVBORw0KGgoAAAANSUhEUgAAAMgAAAAUCAYAAADIpHLKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAABdSURBVGhD7dMxDYAwAADBisABGwbQghPcl7AwkOYV3HDv4Mdx3RNYG2+2/QR+DALBIBAMAsEgEAwCwSAQDALBIBAMAsEgEAwCwSAQDALBIBAMAsEgEAwC4RsEWHsAFEY4nmRdkNAAAAAASUVORK5CYII=";
    const string Highlighted = "iVBORw0KGgoAAAANSUhEUgAAAMgAAAAUCAYAAADIpHLKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAABdSURBVGhD7dMxDYAwAADB6kABMhCBaLwgooSFgTSv4IZ7Bz/2657A2nizHSfwYxAIBoFgEAgGgWAQCAaBYBAIBoFgEAgGgWAQCAaBYBAIBoFgEAgGgWAQCN8gwNoD+34xaYCh7icAAAAASUVORK5CYII=";
    const string Disabled = "iVBORw0KGgoAAAANSUhEUgAAAMgAAAAUCAYAAADIpHLKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAABcSURBVGhD7dOhDYAwAADBroDDg2UA9h+sBIMgzU9w4n6DH8d1T2BtvNn2E/gxCASDQDAIBINAMAgEg0AwCASDQDAIBINAMAgEg0AwCASDQDAIBINAMAiEbxBg7QGES6rdvefYPwAAAABJRU5ErkJggg==";
}
