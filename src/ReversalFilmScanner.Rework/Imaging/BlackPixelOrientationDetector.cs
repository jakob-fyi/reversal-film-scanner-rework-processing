using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Imaging;

/// <summary>
/// Detects the orientation by measuring the share of black pixels in two probe areas
/// that are covered by the (black) slide frame only when the slide is mounted in portrait orientation.
/// </summary>
public sealed class BlackPixelOrientationDetector(IOptions<ScanLayoutOptions> options) : IOrientationDetector
{
    private readonly ScanLayoutOptions _layout = options.Value;

    public Orientation Detect(SKBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var left = GetBlackPercentage(image, _layout.LeftProbe, _layout.BlackRgbSumThreshold);
        var right = GetBlackPercentage(image, _layout.RightProbe, _layout.BlackRgbSumThreshold);

        return (left + right) / 2 > _layout.PortraitBlackPercentageThreshold
            ? Orientation.Portrait
            : Orientation.Landscape;
    }

    /// <summary>Returns the percentage (0-100) of pixels in <paramref name="region"/> whose R+G+B sum is at most <paramref name="blackRgbSumThreshold"/>.</summary>
    public static double GetBlackPercentage(SKBitmap image, PixelRegion region, int blackRgbSumThreshold)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(region);

        if (!region.FitsInto(image.Width, image.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                $"Probe region {region} exceeds the image bounds ({image.Width}x{image.Height}).");
        }

        var blackPixels = image.ColorType is SKColorType.Bgra8888 or SKColorType.Rgba8888
            ? CountBlackPixelsFast(image, region, blackRgbSumThreshold)
            : CountBlackPixelsSlow(image, region, blackRgbSumThreshold);

        return (double)blackPixels / region.Area * 100d;
    }

    // R + G + B is independent of the channel order, so BGRA and RGBA can share one code path.
    private static long CountBlackPixelsFast(SKBitmap image, PixelRegion region, int threshold)
    {
        const int bytesPerPixel = 4;
        ReadOnlySpan<byte> pixels = image.GetPixelSpan();
        var rowBytes = image.RowBytes;
        long count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            var row = pixels.Slice((y * rowBytes) + (region.X * bytesPerPixel), region.Width * bytesPerPixel);
            for (var i = 0; i < row.Length; i += bytesPerPixel)
            {
                if (row[i] + row[i + 1] + row[i + 2] <= threshold)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static long CountBlackPixelsSlow(SKBitmap image, PixelRegion region, int threshold)
    {
        long count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            for (var x = region.X; x < region.X + region.Width; x++)
            {
                var color = image.GetPixel(x, y);
                if (color.Red + color.Green + color.Blue <= threshold)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
