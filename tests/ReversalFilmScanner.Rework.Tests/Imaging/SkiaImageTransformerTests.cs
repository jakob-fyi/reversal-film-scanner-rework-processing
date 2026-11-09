using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests.Imaging;

public sealed class SkiaImageTransformerTests
{
    private readonly SkiaImageTransformer _transformer = new();

    /// <summary>3x2 bitmap where every pixel has a unique colour encoding its position.</summary>
    private static SKBitmap Pattern()
    {
        var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                bitmap.SetPixel(x, y, ColorAt(x, y));
            }
        }

        return bitmap;
    }

    private static SKColor ColorAt(int x, int y) => new((byte)(10 + (x * 50)), (byte)(20 + (y * 100)), 77);

    [Fact]
    public void Crop_ReturnsRegionWithExpectedSizeAndPixels()
    {
        using var source = TestImages.Solid(10, 8, SKColors.White);
        TestImages.Fill(source, new PixelRegion(2, 3, 4, 2), SKColors.Red);

        using var cropped = _transformer.Crop(source, new PixelRegion(2, 3, 4, 2));

        Assert.Equal(4, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.All(cropped.Pixels, p => Assert.Equal(SKColors.Red, p));
    }

    [Fact]
    public void Crop_ResultIsIndependentOfSource()
    {
        using var source = TestImages.Solid(10, 10, SKColors.Blue);
        using var cropped = _transformer.Crop(source, new PixelRegion(0, 0, 5, 5));

        source.Erase(SKColors.Green);

        Assert.Equal(SKColors.Blue, cropped.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(6, 0, 5, 5)]
    [InlineData(0, 0, 11, 1)]
    [InlineData(0, 9, 1, 2)]
    public void Crop_RegionOutsideImage_Throws(int x, int y, int width, int height)
    {
        using var source = TestImages.Solid(10, 10, SKColors.White);

        Assert.Throws<ArgumentOutOfRangeException>(() => _transformer.Crop(source, new PixelRegion(x, y, width, height)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RotateFlip_MapsEveryPixelExactly(bool rotate180, bool flipHorizontal)
    {
        using var source = Pattern();

        using var result = _transformer.RotateFlip(source, rotate180, flipHorizontal);

        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                // Reference implementation: the pixel-wise mapping of the original System.Drawing version.
                var srcX = x;
                var srcY = y;
                if (rotate180)
                {
                    srcX = 2 - srcX;
                    srcY = 1 - srcY;
                }

                if (flipHorizontal)
                {
                    srcX = 2 - srcX;
                }

                Assert.Equal(ColorAt(srcX, srcY), result.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void RotateFlip_DoesNotModifySource()
    {
        using var source = Pattern();

        using var _ = _transformer.RotateFlip(source, rotate180: true, flipHorizontal: true);

        Assert.Equal(ColorAt(0, 0), source.GetPixel(0, 0));
        Assert.Equal(ColorAt(2, 1), source.GetPixel(2, 1));
    }

    [Fact]
    public void RotateFlip_Twice_RestoresOriginal()
    {
        using var source = Pattern();

        using var once = _transformer.RotateFlip(source, rotate180: true, flipHorizontal: false);
        using var twice = _transformer.RotateFlip(once, rotate180: true, flipHorizontal: false);

        Assert.Equal(source.Pixels, twice.Pixels);
    }
}
