using ReversalFilmScanner.Rework.Configuration;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

public sealed class SkiaImageTransformer : IImageTransformer
{
    public SKBitmap Crop(SKBitmap source, PixelRegion region)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(region);

        if (!region.FitsInto(source.Width, source.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                $"Crop region {region} exceeds the image bounds ({source.Width}x{source.Height}).");
        }

        using var subset = new SKBitmap();
        if (!source.ExtractSubset(subset, region.ToRectI()))
        {
            throw new InvalidOperationException($"Could not extract crop region {region}.");
        }

        // ExtractSubset shares the pixel memory with the source - copy to get an independent bitmap.
        return subset.Copy() ?? throw new InvalidOperationException("Could not copy the cropped bitmap.");
    }

    public SKBitmap RotateFlip(SKBitmap source, bool rotate180, bool flipHorizontal)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Rotate180 == mirror on both axes, so the combination with a horizontal flip is a vertical flip.
        var mirrorX = rotate180 ^ flipHorizontal;
        var mirrorY = rotate180;

        var result = new SKBitmap(source.Info);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(mirrorX ? source.Width : 0, mirrorY ? source.Height : 0);
        canvas.Scale(mirrorX ? -1 : 1, mirrorY ? -1 : 1);

        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        // Nearest-neighbour sampling + integer translation/mirroring keeps every pixel value unchanged.
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
        canvas.Flush();

        return result;
    }
}
