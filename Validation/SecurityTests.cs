using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MistikLauncher;

static class SecurityTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        var secret = Encoding.UTF8.GetBytes("fixture refresh token");
        var encrypted = WindowsSecret.Transform(secret, true);
        Check(!encrypted.SequenceEqual(secret) && WindowsSecret.Transform(encrypted, false).SequenceEqual(secret),
            "Windows user-bound encryption round trip");
        var tampered = (byte[])encrypted.Clone();
        tampered[^1] ^= 1;
        bool rejected = false;
        try { WindowsSecret.Transform(tampered, false); }
        catch (System.ComponentModel.Win32Exception) { rejected = true; }
        Check(rejected, "tampered encrypted data is rejected");

        rejected = false;
        try { SkinValidator.Validate(new byte[33]); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "malformed skin is rejected");
        var bitmap = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 64 * 4], 64 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        SkinValidator.Validate(stream.ToArray());
        Check(stream.Length > 0, "valid 64x64 PNG skin is accepted");
        return checks;
    }
}
