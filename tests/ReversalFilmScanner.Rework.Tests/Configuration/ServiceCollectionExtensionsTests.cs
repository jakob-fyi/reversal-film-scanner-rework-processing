using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using ReversalFilmScanner.Rework.Processing;

namespace ReversalFilmScanner.Rework.Tests.Configuration;

public sealed class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        configure?.Invoke(services);
        services.AddScanRework(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AddScanRework_RegistersAllServices()
    {
        using var provider = BuildProvider(new() { ["Processing:InputDirectory"] = "/scans" });

        Assert.IsType<BatchProcessor>(provider.GetRequiredService<IBatchProcessor>());
        Assert.IsType<ImageProcessor>(provider.GetRequiredService<IImageProcessor>());
        Assert.IsType<BlackPixelOrientationDetector>(provider.GetRequiredService<IOrientationDetector>());
        Assert.IsType<SkiaImageTransformer>(provider.GetRequiredService<IImageTransformer>());
        Assert.IsType<SkiaImageCodec>(provider.GetRequiredService<IImageCodec>());
        Assert.IsType<FileSystem>(provider.GetRequiredService<IFileSystem>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddScanRework_KeepsPreviouslyRegisteredInfrastructure()
    {
        var fileSystem = new MockFileSystem();
        using var provider = BuildProvider(
            new() { ["Processing:InputDirectory"] = "/scans" },
            services => services.AddSingleton<IFileSystem>(fileSystem));

        Assert.Same(fileSystem, provider.GetRequiredService<IFileSystem>());
    }

    [Fact]
    public void AddScanRework_UsesDefaultsForUnsetValues()
    {
        using var provider = BuildProvider(new() { ["Processing:InputDirectory"] = "/scans" });

        var processing = provider.GetRequiredService<IOptions<ProcessingOptions>>().Value;
        var layout = provider.GetRequiredService<IOptions<ScanLayoutOptions>>().Value;

        Assert.Equal("jpg", processing.FileExtension);
        Assert.True(processing.Rename);
        Assert.True(processing.Crop);
        Assert.True(processing.Rotate180);
        Assert.False(processing.FlipHorizontal);
        Assert.Equal(new PixelRegion(1070, 700, 600, 2500), layout.LeftProbe);
        Assert.Equal(new PixelRegion(1800, 170, 2270, 3540), layout.PortraitCrop);
    }

    [Fact]
    public void AddScanRework_BindsConfigurationValues()
    {
        using var provider = BuildProvider(new()
        {
            ["Processing:InputDirectory"] = "/scans",
            ["Processing:Crop"] = "false",
            ["Processing:RenamePrefix"] = "Holiday_",
            ["ScanLayout:LeftProbe:X"] = "1",
            ["ScanLayout:PortraitBlackPercentageThreshold"] = "80.5",
        });

        var processing = provider.GetRequiredService<IOptions<ProcessingOptions>>().Value;
        var layout = provider.GetRequiredService<IOptions<ScanLayoutOptions>>().Value;

        Assert.False(processing.Crop);
        Assert.Equal("Holiday_", processing.RenamePrefix);
        Assert.Equal(new PixelRegion(1, 700, 600, 2500), layout.LeftProbe);
        Assert.Equal(80.5, layout.PortraitBlackPercentageThreshold);
    }

    public static TheoryData<string, string?> InvalidSettings => new()
    {
        { "Processing:InputDirectory", "" },
        { "Processing:JpegQuality", "0" },
        { "Processing:JpegQuality", "101" },
        { "Processing:RenameStartIndex", "-1" },
        { "Processing:FileExtension", "*.jpg" },
        { "ScanLayout:BlackRgbSumThreshold", "766" },
        { "ScanLayout:PortraitBlackPercentageThreshold", "101" },
        { "ScanLayout:PortraitCrop:Width", "0" },
        { "ScanLayout:RightProbe:X", "-5" },
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void InvalidConfiguration_IsRejected(string key, string? value)
    {
        var settings = new Dictionary<string, string?> { ["Processing:InputDirectory"] = "/scans", [key] = value };
        using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IBatchProcessor>());
    }
}
