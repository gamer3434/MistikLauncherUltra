using System.IO;
using System.IO.Compression;
using MistikLauncher;
using MistikLauncher.Pages;

static class ModCatalogTests
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        Directory.CreateDirectory(root);
        void Jar(string name, string id)
        {
            using var zip = ZipFile.Open(Path.Combine(root, name), ZipArchiveMode.Create);
            using var writer = new StreamWriter(zip.CreateEntry("fabric.mod.json").Open());
            writer.Write("{\"id\":\"" + id + "\"}");
        }
        Jar("renamed-hud.jar", "mistik_hud"); Jar("mistik-hud.jar", "personal_mod");
        Jar("old-hud.jar.disabled", "mistik_hud");
        var personal = File.ReadAllBytes(Path.Combine(root, "mistik-hud.jar"));
        var own = File.ReadAllBytes(Path.Combine(root, "renamed-hud.jar"));
        ModFiles.RetireMistikHud(root);
        Check(File.ReadAllBytes(Path.Combine(root, "mistik-hud.jar")).SequenceEqual(personal) &&
            !File.Exists(Path.Combine(root, "renamed-hud.jar")) && !File.Exists(Path.Combine(root, "old-hud.jar.disabled")),
            "HUD retirement identifies metadata and preserves personal filename collisions");
        Check(Directory.GetFiles(Path.Combine(root, ".retired")).Length == 2 &&
            Directory.GetFiles(Path.Combine(root, ".retired")).Any(path => File.ReadAllBytes(path).SequenceEqual(own)),
            "Removed HUD is retained outside the game loader in an exact backup");
        ModFiles.RetireMistikHud(root);
        Check(Directory.GetFiles(Path.Combine(root, ".retired")).Length == 2, "HUD retirement is idempotent");
        var html = (string)typeof(ModProjectView).GetMethod("DescriptionHtml", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { "# Title\n\n**Bold**\n\n<script>alert(1)</script>\n\n![Screen](https://cdn.modrinth.com/data/image.png)" })!;
        Check(html.Contains("<h1>Title</h1>") && html.Contains("<strong>Bold</strong>") && html.Contains("<img") && !html.Contains("<script>") && html.Contains("default-src 'none'"),
            "Project Markdown renders rich content while raw HTML and active content remain blocked");
        object? ImageUrl(string value) => typeof(ModProjectView).GetMethod("ImageUrl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { value });
        Check(ImageUrl("https://cdn.modrinth.com/data/icon.png") != null &&
            new[] { "file:///C:/Windows/test.png", "http://cdn.modrinth.com/x", "https://cdn.modrinth.com.attacker.invalid/x", "https://user@cdn.modrinth.com/x", "https://127.0.0.1/x" }
                .All(url => ImageUrl(url) == null),
            "Catalog images accept only official HTTPS CDN URLs");
        Uri? GalleryImageUrl(string? raw, string? preview) => (Uri?)typeof(ModProjectView).GetMethod("GalleryImageUrl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object?[] { raw, preview });
        Check(GalleryImageUrl("https://cdn.modrinth.com/data/original.png", "https://cdn.modrinth.com/data/preview.webp")?.AbsolutePath.EndsWith("original.png") == true &&
            GalleryImageUrl("https://attacker.invalid/original.png", "https://cdn.modrinth.com/data/preview.webp")?.AbsolutePath.EndsWith("preview.webp") == true,
            "Gallery prefers the verified full-size source and falls back to its verified preview");
        Check(typeof(App).Assembly.GetManifestResourceNames().All(name => !name.EndsWith("mistik-hud.jar")), "Launcher no longer distributes the retired HUD");
        using var picture = new SkiaSharp.SKBitmap(12, 6);
        picture.Erase(SkiaSharp.SKColors.Green);
        using var encodedPicture = SkiaSharp.SKImage.FromBitmap(picture);
        foreach (var format in new[] { SkiaSharp.SKEncodedImageFormat.Png, SkiaSharp.SKEncodedImageFormat.Webp })
        {
            using var encoded = encodedPicture.Encode(format, 90);
            var decoded = ModCatalogImages.Decode(encoded.ToArray(), 8);
            Check(decoded.IsFrozen && decoded.PixelWidth == 8 && decoded.PixelHeight == 4,
                "Catalog " + format + " image decodes at bounded size off the UI thread");
        }
        bool oversized = false;
        try { ModCatalogImages.Decode(new byte[8 * 1024 * 1024 + 1], 96); } catch (InvalidDataException) { oversized = true; }
        Check(oversized, "Oversized catalog images are rejected before decoding");
        return checks;
    }
}
