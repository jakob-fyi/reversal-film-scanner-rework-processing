using System.IO.Abstractions.TestingHelpers;
using ReversalFilmScanner.Rework.Imaging;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests.Imaging;

public sealed class SkiaImageCodecTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly SkiaImageCodec _codec;

    public SkiaImageCodecTests() => _codec = new SkiaImageCodec(_fileSystem);

    [Fact]
    public void SaveJpeg_ThenLoad_RoundTripsSizeAndColour()
    {
        _fileSystem.AddDirectory("/scans");
        using var image = TestImages.Solid(32, 16, new SKColor(200, 100, 50));

        _codec.SaveJpeg(image, "/scans/a.jpg", 95);
        using var loaded = _codec.Load("/scans/a.jpg");

        Assert.Equal(32, loaded.Width);
        Assert.Equal(16, loaded.Height);
        TestImages.AssertColorNear(new SKColor(200, 100, 50), loaded.GetPixel(16, 8));
    }

    [Fact]
    public void SaveJpeg_WritesJpegSignature()
    {
        using var image = TestImages.Solid(4, 4, SKColors.White);

        _codec.SaveJpeg(image, "/a.jpg", 100);

        var bytes = _fileSystem.File.ReadAllBytes("/a.jpg");
        Assert.Equal([0xFF, 0xD8], bytes[..2]);
    }

    [Fact]
    public void SaveJpeg_OverwritesExistingFile()
    {
        _fileSystem.AddFile("/a.jpg", new MockFileData(new byte[100_000]));
        using var image = TestImages.Solid(4, 4, SKColors.White);

        _codec.SaveJpeg(image, "/a.jpg", 100);

        Assert.True(_fileSystem.FileInfo.New("/a.jpg").Length < 100_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void SaveJpeg_InvalidQuality_Throws(int quality)
    {
        using var image = TestImages.Solid(4, 4, SKColors.White);

        Assert.Throws<ArgumentOutOfRangeException>(() => _codec.SaveJpeg(image, "/a.jpg", quality));
        Assert.False(_fileSystem.File.Exists("/a.jpg"));
    }

    [Fact]
    public void Load_NotAnImage_ThrowsInvalidDataException()
    {
        _fileSystem.AddFile("/broken.jpg", new MockFileData("definitely not a jpeg"));

        Assert.Throws<InvalidDataException>(() => _codec.Load("/broken.jpg"));
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException() =>
        Assert.Throws<FileNotFoundException>(() => _codec.Load("/missing.jpg"));
}
