using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

/// <summary>Reads and writes images from/to the (abstracted) file system.</summary>
public interface IImageCodec
{
    SKBitmap Load(string path);

    void SaveJpeg(SKBitmap image, string path, int quality);
}
