using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests.Imaging;

public sealed class BlackPixelOrientationDetectorTests
{
    private readonly BlackPixelOrientationDetector _detector = new(TestImages.LayoutOptions());

    [Fact]
    public void Detect_BrightProbes_ReturnsLandscape()
    {
        using var image = TestImages.Landscape();

        Assert.Equal(Orientation.Landscape, _detector.Detect(image));
    }

    [Fact]
    public void Detect_BlackProbes_ReturnsPortrait()
    {
        using var image = TestImages.Portrait();

        Assert.Equal(Orientation.Portrait, _detector.Detect(image));
    }

    [Fact]
    public void Detect_OnlyOneProbeBlack_ReturnsLandscape()
    {
        using var image = TestImages.Landscape();
        TestImages.Fill(image, TestImages.Layout().LeftProbe, SKColors.Black);

        Assert.Equal(Orientation.Landscape, _detector.Detect(image));
    }

    [Fact]
    public void Detect_ExactlyAtThreshold_ReturnsLandscape()
    {
        // 95 % black is not "greater than" 95 % - matches the original behaviour.
        var layout = TestImages.Layout();
        using var image = TestImages.Portrait();
        // Each probe is 10x30 = 300 px; 15 bright pixels per probe => exactly 95 % black.
        TestImages.Fill(image, layout.LeftProbe with { Height = 1, Width = 10 }, SKColors.White);
        TestImages.Fill(image, layout.LeftProbe with { Y = layout.LeftProbe.Y + 1, Height = 1, Width = 5 }, SKColors.White);
        TestImages.Fill(image, layout.RightProbe with { Height = 1, Width = 10 }, SKColors.White);
        TestImages.Fill(image, layout.RightProbe with { Y = layout.RightProbe.Y + 1, Height = 1, Width = 5 }, SKColors.White);

        Assert.Equal(95d, BlackPixelOrientationDetector.GetBlackPercentage(image, layout.LeftProbe, 45), 6);
        Assert.Equal(Orientation.Landscape, _detector.Detect(image));
    }

    [Theory]
    [InlineData(15, 15, 15, true)]  // sum 45 == threshold => black
    [InlineData(15, 15, 16, false)] // sum 46 > threshold => not black
    [InlineData(0, 0, 0, true)]
    [InlineData(45, 0, 0, true)]
    [InlineData(255, 255, 255, false)]
    public void GetBlackPercentage_UsesRgbSumThreshold(byte r, byte g, byte b, bool isBlack)
    {
        using var image = TestImages.Solid(4, 4, new SKColor(r, g, b));

        var percentage = BlackPixelOrientationDetector.GetBlackPercentage(image, new PixelRegion(0, 0, 4, 4), 45);

        Assert.Equal(isBlack ? 100d : 0d, percentage);
    }

    [Fact]
    public void GetBlackPercentage_HalfBlackRegion_Returns50()
    {
        using var image = TestImages.Solid(10, 10, SKColors.White);
        TestImages.Fill(image, new PixelRegion(0, 0, 5, 10), SKColors.Black);

        Assert.Equal(50d, BlackPixelOrientationDetector.GetBlackPercentage(image, new PixelRegion(0, 0, 10, 10), 45));
    }

    [Fact]
    public void GetBlackPercentage_OnlyCountsPixelsInsideRegion()
    {
        using var image = TestImages.Solid(10, 10, SKColors.White);
        TestImages.Fill(image, new PixelRegion(2, 3, 4, 5), SKColors.Black);

        Assert.Equal(100d, BlackPixelOrientationDetector.GetBlackPercentage(image, new PixelRegion(2, 3, 4, 5), 45));
        Assert.Equal(0d, BlackPixelOrientationDetector.GetBlackPercentage(image, new PixelRegion(6, 0, 4, 10), 45));
    }

    [Theory]
    [InlineData(SKColorType.Rgba8888)]
    [InlineData(SKColorType.Bgra8888)]
    [InlineData(SKColorType.Rgb565)]
    [InlineData(SKColorType.Gray8)]
    public void GetBlackPercentage_SupportsDifferentColorTypes(SKColorType colorType)
    {
        using var image = TestImages.Solid(10, 10, SKColors.White, colorType);
        TestImages.Fill(image, new PixelRegion(0, 0, 10, 3), SKColors.Black);

        Assert.Equal(30d, BlackPixelOrientationDetector.GetBlackPercentage(image, new PixelRegion(0, 0, 10, 10), 45), 6);
    }

    [Fact]
    public void Detect_ImageSmallerThanProbes_Throws()
    {
        using var image = TestImages.Solid(20, 20, SKColors.Black);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _detector.Detect(image));
        Assert.Contains("exceeds the image bounds", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => _detector.Detect(null!));
}
