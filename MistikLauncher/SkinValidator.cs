using System.Buffers.Binary;
using System.IO;
using System.Windows.Media.Imaging;

namespace MistikLauncher;

public static class SkinValidator
{
    public static void Validate(byte[] bytes)
    {
        if (bytes.Length < 33 || bytes.Length > 65536 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != 64 ||
            !new[] { 32, 64 }.Contains(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4))))
            throw new InvalidDataException(Localization.T("cloudSkinInvalid"));
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (image.Frames[0].PixelWidth != 64 || !new[] { 32, 64 }.Contains(image.Frames[0].PixelHeight))
                throw new InvalidDataException();
        }
        catch { throw new InvalidDataException(Localization.T("cloudSkinInvalid")); }
    }
}
