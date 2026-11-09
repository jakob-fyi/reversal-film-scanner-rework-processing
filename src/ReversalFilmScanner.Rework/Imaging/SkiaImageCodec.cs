using System.IO.Abstractions;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

public sealed class SkiaImageCodec(IFileSystem fileSystem) : IImageCodec
{
    public SKBitmap Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = fileSystem.File.OpenRead(path);
        return SKBitmap.Decode(stream)
            ?? throw new InvalidDataException($"'{path}' could not be decoded as an image.");
    }

    public void SaveJpeg(SKBitmap image, string path, int quality)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        // Encode into memory first so a failing encoder never leaves a truncated file behind.
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality)
            ?? throw new InvalidOperationException($"'{path}' could not be encoded as JPEG.");

        using var stream = fileSystem.File.Create(path);
        data.SaveTo(stream);
    }
}
