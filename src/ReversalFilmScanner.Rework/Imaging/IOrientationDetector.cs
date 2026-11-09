using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

/// <summary>Determines whether a scanned slide is mounted in portrait or landscape orientation.</summary>
public interface IOrientationDetector
{
    Orientation Detect(SKBitmap image);
}
