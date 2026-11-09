using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using ReversalFilmScanner.Rework.Processing;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests.Processing;

public sealed class ImageProcessorTests
{
    private const string ImagePath = "/work/DSCF0001.JPG";

    private readonly MockFileSystem _fileSystem = new();
    private readonly IOrientationDetector _detector = Substitute.For<IOrientationDetector>();
    private readonly ProcessingOptions _options = new()
    {
        InputDirectory = "/scans",
        Rename = false,
        Crop = false,
        Rotate180 = false,
        FlipHorizontal = false,
    };

    private ImageProcessor CreateSut() => new(
        _fileSystem,
        new SkiaImageCodec(_fileSystem),
        _detector,
        new SkiaImageTransformer(),
        Options.Create(_options),
        TestImages.LayoutOptions());

    private byte[] AddImage(SKBitmap bitmap, string path = ImagePath)
    {
        var bytes = TestImages.Jpeg(bitmap);
        _fileSystem.AddFile(path, new MockFileData(bytes));
        return bytes;
    }

    private SKBitmap LoadResult(string path) => TestImages.Decode(_fileSystem.File.ReadAllBytes(path));

    [Fact]
    public void Process_AllStepsDisabled_LeavesFileUntouched()
    {
        using var bitmap = TestImages.Landscape();
        var original = AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 1);

        Assert.Equal(new ImageProcessingResult("DSCF0001.JPG", ImagePath, null, false, false, false), result);
        Assert.Equal(original, _fileSystem.File.ReadAllBytes(ImagePath));
        _detector.DidNotReceiveWithAnyArgs().Detect(default!);
    }

    [Fact]
    public void Process_RenameOnly_MovesFileWithoutReEncoding()
    {
        _options.Rename = true;
        _options.RenamePrefix = "Dia_";
        _options.RenameNumberFormat = "0000";
        using var bitmap = TestImages.Landscape();
        var original = AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 12);

        Assert.Equal("/work/Dia_0012.JPG", result.ResultPath);
        Assert.True(result.Renamed);
        Assert.False(_fileSystem.File.Exists(ImagePath));
        Assert.Equal(original, _fileSystem.File.ReadAllBytes(result.ResultPath));
    }

    [Fact]
    public void Process_RenameTargetExists_ThrowsAndKeepsBothFiles()
    {
        _options.Rename = true;
        _options.RenamePrefix = "Dia_";
        using var bitmap = TestImages.Landscape();
        AddImage(bitmap);
        AddImage(bitmap, "/work/Dia_000001.JPG");

        Assert.Throws<IOException>(() => CreateSut().Process(ImagePath, 1));

        Assert.True(_fileSystem.File.Exists(ImagePath));
        Assert.True(_fileSystem.File.Exists("/work/Dia_000001.JPG"));
    }

    [Fact]
    public void Process_RenameToSameName_IsANoOp()
    {
        _options.Rename = true;
        _options.RenamePrefix = "DSCF";
        _options.RenameNumberFormat = "0000";
        using var bitmap = TestImages.Landscape();
        AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 1);

        Assert.Equal(ImagePath, result.ResultPath);
        Assert.True(_fileSystem.File.Exists(ImagePath));
    }

    [Theory]
    [InlineData(Orientation.Portrait, 20, 40)]
    [InlineData(Orientation.Landscape, 60, 20)]
    public void Process_Crop_UsesRegionOfDetectedOrientation(Orientation orientation, int expectedWidth, int expectedHeight)
    {
        _options.Crop = true;
        _detector.Detect(Arg.Any<SKBitmap>()).Returns(orientation);
        using var bitmap = TestImages.Landscape();
        AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 1);

        Assert.Equal(orientation, result.Orientation);
        Assert.True(result.Cropped);
        using var saved = LoadResult(ImagePath);
        Assert.Equal(expectedWidth, saved.Width);
        Assert.Equal(expectedHeight, saved.Height);
    }

    [Fact]
    public void Process_Crop_CutsOutTheConfiguredArea()
    {
        _options.Crop = true;
        _detector.Detect(Arg.Any<SKBitmap>()).Returns(Orientation.Landscape);
        using var bitmap = TestImages.Solid(TestImages.Width, TestImages.Height, SKColors.Black);
        TestImages.Fill(bitmap, TestImages.Layout().LandscapeCrop, SKColors.Red);
        AddImage(bitmap);

        CreateSut().Process(ImagePath, 1);

        using var saved = LoadResult(ImagePath);
        TestImages.AssertColorNear(SKColors.Red, saved.GetPixel(2, 2));
        TestImages.AssertColorNear(SKColors.Red, saved.GetPixel(saved.Width - 3, saved.Height - 3));
    }

    [Fact]
    public void Process_Rotate180_TurnsImageUpsideDown()
    {
        _options.Rotate180 = true;
        using var bitmap = TestImages.Solid(TestImages.Width, TestImages.Height, SKColors.Blue);
        TestImages.Fill(bitmap, new PixelRegion(0, 0, TestImages.Width, TestImages.Height / 2), SKColors.Red);
        AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 1);

        Assert.True(result.RotatedOrFlipped);
        Assert.Null(result.Orientation);
        using var saved = LoadResult(ImagePath);
        TestImages.AssertColorNear(SKColors.Blue, saved.GetPixel(30, 5));
        TestImages.AssertColorNear(SKColors.Red, saved.GetPixel(30, 35));
    }

    [Fact]
    public void Process_FlipHorizontal_MirrorsImage()
    {
        _options.FlipHorizontal = true;
        using var bitmap = TestImages.Solid(TestImages.Width, TestImages.Height, SKColors.Blue);
        TestImages.Fill(bitmap, new PixelRegion(0, 0, TestImages.Width / 2, TestImages.Height), SKColors.Red);
        AddImage(bitmap);

        CreateSut().Process(ImagePath, 1);

        using var saved = LoadResult(ImagePath);
        TestImages.AssertColorNear(SKColors.Blue, saved.GetPixel(5, 20));
        TestImages.AssertColorNear(SKColors.Red, saved.GetPixel(55, 20));
    }

    [Fact]
    public void Process_AllSteps_RenamesCropsAndRotatesTheRenamedFile()
    {
        _options.Rename = true;
        _options.Crop = true;
        _options.Rotate180 = true;
        _detector.Detect(Arg.Any<SKBitmap>()).Returns(Orientation.Portrait);
        using var bitmap = TestImages.Portrait();
        AddImage(bitmap);

        var result = CreateSut().Process(ImagePath, 3);

        Assert.Equal("/work/Prefix_000003.JPG", result.ResultPath);
        Assert.Equal(Orientation.Portrait, result.Orientation);
        using var saved = LoadResult(result.ResultPath);
        Assert.Equal((20, 40), (saved.Width, saved.Height));
    }

    [Fact]
    public void Process_CorruptImage_ThrowsInvalidDataException()
    {
        _options.Crop = true;
        _fileSystem.AddFile(ImagePath, new MockFileData("garbage"));

        Assert.Throws<InvalidDataException>(() => CreateSut().Process(ImagePath, 1));
    }
}
