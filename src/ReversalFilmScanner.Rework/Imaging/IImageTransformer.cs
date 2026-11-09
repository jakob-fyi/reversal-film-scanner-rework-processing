using ReversalFilmScanner.Rework.Configuration;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

/// <summary>Pure in-memory image transformations. Implementations never mutate the source bitmap.</summary>
public interface IImageTransformer
{
    /// <summary>Returns a new bitmap containing only <paramref name="region"/> of <paramref name="source"/>.</summary>
    SKBitmap Crop(SKBitmap source, PixelRegion region);

    /// <summary>Returns a new bitmap that is rotated by 180 degrees and/or mirrored horizontally.</summary>
    SKBitmap RotateFlip(SKBitmap source, bool rotate180, bool flipHorizontal);
}
