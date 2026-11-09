using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReversalFilmScanner.Rework.Imaging;
using ReversalFilmScanner.Rework.Processing;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Tests;

/// <summary>Runs the real pipeline (DI, options binding, Skia) against an in-memory file system.</summary>
public sealed class EndToEndTests
{
    [Fact]
    public void Run_RenamesDetectsOrientationCropsAndRotates()
    {
        var fileSystem = new MockFileSystem();
        using (var landscape = TestImages.Landscape())
        using (var portrait = TestImages.Portrait())
        {
            fileSystem.AddFile("/scans/DSCF0001.JPG", TestImages.JpegFile(landscape));
            fileSystem.AddFile("/scans/DSCF0002.JPG", TestImages.JpegFile(portrait));
        }

        var layout = TestImages.Layout();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:InputDirectory"] = "/scans",
                ["Processing:RenamePrefix"] = "Slide_",
                ["Processing:RenameNumberFormat"] = "000",
                ["ScanLayout:LeftProbe:X"] = $"{layout.LeftProbe.X}",
                ["ScanLayout:LeftProbe:Y"] = $"{layout.LeftProbe.Y}",
                ["ScanLayout:LeftProbe:Width"] = $"{layout.LeftProbe.Width}",
                ["ScanLayout:LeftProbe:Height"] = $"{layout.LeftProbe.Height}",
                ["ScanLayout:RightProbe:X"] = $"{layout.RightProbe.X}",
                ["ScanLayout:RightProbe:Y"] = $"{layout.RightProbe.Y}",
                ["ScanLayout:RightProbe:Width"] = $"{layout.RightProbe.Width}",
                ["ScanLayout:RightProbe:Height"] = $"{layout.RightProbe.Height}",
                ["ScanLayout:PortraitCrop:X"] = $"{layout.PortraitCrop.X}",
                ["ScanLayout:PortraitCrop:Y"] = $"{layout.PortraitCrop.Y}",
                ["ScanLayout:PortraitCrop:Width"] = $"{layout.PortraitCrop.Width}",
                ["ScanLayout:PortraitCrop:Height"] = $"{layout.PortraitCrop.Height}",
                ["ScanLayout:LandscapeCrop:X"] = $"{layout.LandscapeCrop.X}",
                ["ScanLayout:LandscapeCrop:Y"] = $"{layout.LandscapeCrop.Y}",
                ["ScanLayout:LandscapeCrop:Width"] = $"{layout.LandscapeCrop.Width}",
                ["ScanLayout:LandscapeCrop:Height"] = $"{layout.LandscapeCrop.Height}",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IFileSystem>(fileSystem)
            .AddScanRework(configuration)
            .BuildServiceProvider();

        var result = provider.GetRequiredService<IBatchProcessor>().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal([Orientation.Landscape, Orientation.Portrait], result.Processed.Select(p => p.Orientation));
        Assert.Equal(["/scans/Slide_001.JPG", "/scans/Slide_002.JPG"], fileSystem.Directory.GetFiles("/scans").Order());
        Assert.Empty(fileSystem.Directory.GetDirectories("/scans"));

        using var first = SKBitmap.Decode(fileSystem.File.ReadAllBytes("/scans/Slide_001.JPG"));
        using var second = SKBitmap.Decode(fileSystem.File.ReadAllBytes("/scans/Slide_002.JPG"));
        Assert.Equal((60, 20), (first.Width, first.Height));
        Assert.Equal((20, 40), (second.Width, second.Height));
    }
}
