using System.Net.Http;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace MistikLauncher;

public static class ModCatalogImages
{
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 8 * 1024 * 1024 };
    static readonly SemaphoreSlim Gate = new(4);
    static readonly Dictionary<string, BitmapSource> Cache = new();
    public static BitmapSource Decode(byte[] bytes, int width)
    {
        if (bytes.Length > 8 * 1024 * 1024 || width is < 1 or > 1200) throw new InvalidDataException("Invalid catalog image size.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported catalog image.");
        var info = codec.Info;
        if (info.Width > 4096 || info.Height > 4096) throw new InvalidDataException("Catalog image dimensions are too large.");
        using var decoded = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Invalid catalog image.");
        double scale = Math.Min(1d, (double)width / Math.Max(info.Width, info.Height));
        using var resized = new SKBitmap(new SKImageInfo(Math.Max(1, (int)(info.Width * scale)), Math.Max(1, (int)(info.Height * scale)), SKColorType.Bgra8888, SKAlphaType.Premul));
        if (!decoded.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear))) throw new InvalidDataException("Cannot resize catalog image.");
        var bitmap = BitmapSource.Create(resized.Width, resized.Height, 96, 96, PixelFormats.Pbgra32, null, resized.Bytes, resized.RowBytes);
        bitmap.Freeze(); return bitmap;
    }
    public static async Task<BitmapSource?> LoadAsync(Uri url, int width, CancellationToken token = default)
    {
        if (Pages.ModProjectView.ImageUrl(url.AbsoluteUri) == null) return null;
        string key = url.AbsoluteUri + ":" + width;
        lock (Cache) if (Cache.TryGetValue(key, out var cached)) return cached;
        try
        {
            await Gate.WaitAsync(token);
            try
            {
                var bytes = await Http.GetByteArrayAsync(url, token);
                var bitmap = await Task.Run(() => Decode(bytes, width), token);
                if (width <= 128) lock (Cache) { if (Cache.Count >= 64) Cache.Clear(); Cache[key] = bitmap; }
                return bitmap;
            }
            finally { Gate.Release(); }
        }
        catch (Exception) { return null; }
    }
}
