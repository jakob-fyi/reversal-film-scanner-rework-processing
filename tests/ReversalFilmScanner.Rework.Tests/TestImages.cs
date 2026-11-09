using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests;

/// <summary>Builders for small synthetic scans so tests run fast and without sample files.</summary>
internal static class TestImages
{
    public const int Width = 60;
    public const int Height = 40;

    /// <summary>Scaled-down layout for a 60x40 "scan".</summary>
    public static ScanLayoutOptions Layout() => new()
    {
        LeftProbe = new PixelRegion(5, 5, 10, 30),
        RightProbe = new PixelRegion(45, 5, 10, 30),
        BlackRgbSumThreshold = 45,
        PortraitBlackPercentageThreshold = 95,
        PortraitCrop = new PixelRegion(20, 0, 20, 40),
        LandscapeCrop = new PixelRegion(0, 10, 60, 20),
    };

    public static IOptions<ScanLayoutOptions> LayoutOptions() => Options.Create(Layout());

    public static SKBitmap Solid(int width, int height, SKColor color, SKColorType colorType = SKColorType.Bgra8888)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, colorType, SKAlphaType.Premul));
        bitmap.Erase(color);
        return bitmap;
    }

    public static void Fill(SKBitmap bitmap, PixelRegion region, SKColor color)
    {
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = color, BlendMode = SKBlendMode.Src };
        canvas.DrawRect(SKRect.Create(region.X, region.Y, region.Width, region.Height), paint);
    }

    /// <summary>A landscape scan: bright everywhere.</summary>
    public static SKBitmap Landscape() => Solid(Width, Height, SKColors.White);

    /// <summary>A portrait scan: the probe areas are covered by the black slide frame.</summary>
    public static SKBitmap Portrait()
    {
        var bitmap = Landscape();
        var layout = Layout();
        Fill(bitmap, layout.LeftProbe, SKColors.Black);
        Fill(bitmap, layout.RightProbe, SKColors.Black);
        return bitmap;
    }

    public static byte[] Jpeg(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    public static MockFileData JpegFile(SKBitmap bitmap) => new(Jpeg(bitmap));

    public static SKBitmap Decode(byte[] bytes) =>
        SKBitmap.Decode(bytes) ?? throw new InvalidOperationException("Not an image.");

    /// <summary>JPEG is lossy - compare colours with a tolerance.</summary>
    public static void AssertColorNear(SKColor expected, SKColor actual, int tolerance = 12)
    {
        Assert.True(
            Math.Abs(expected.Red - actual.Red) <= tolerance &&
            Math.Abs(expected.Green - actual.Green) <= tolerance &&
            Math.Abs(expected.Blue - actual.Blue) <= tolerance,
            $"Expected colour close to {expected} but was {actual}.");
    }
}
