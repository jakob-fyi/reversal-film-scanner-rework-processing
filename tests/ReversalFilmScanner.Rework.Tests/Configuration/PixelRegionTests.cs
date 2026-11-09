using ReversalFilmScanner.Rework.Configuration;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests.Configuration;

public sealed class PixelRegionTests
{
    [Theory]
    [InlineData(0, 0, 10, 10, true)]
    [InlineData(5, 5, 5, 5, true)]
    [InlineData(6, 0, 5, 5, false)]
    [InlineData(0, 6, 5, 5, false)]
    [InlineData(0, 0, 11, 1, false)]
    [InlineData(0, 0, 0, 1, false)]
    [InlineData(-1, 0, 1, 1, false)]
    public void FitsInto_ChecksBoundsOfA10x10Image(int x, int y, int width, int height, bool expected)
    {
        var region = new PixelRegion(x, y, width, height);

        Assert.Equal(expected, region.FitsInto(10, 10));
    }

    [Fact]
    public void FitsInto_DoesNotOverflowForHugeValues()
    {
        var region = new PixelRegion(int.MaxValue, 0, int.MaxValue, 1);

        Assert.False(region.FitsInto(10, 10));
    }

    [Fact]
    public void ToRectI_UsesWidthAndHeight()
    {
        var rect = new PixelRegion(10, 20, 30, 40).ToRectI();

        Assert.Equal(new SKRectI(10, 20, 40, 60), rect);
    }

    [Fact]
    public void Area_IsWidthTimesHeight_WithoutOverflow()
    {
        Assert.Equal(1_500_000L, new PixelRegion(0, 0, 600, 2500).Area);
        Assert.Equal((long)int.MaxValue * 2, new PixelRegion(0, 0, int.MaxValue, 2).Area);
    }
}
