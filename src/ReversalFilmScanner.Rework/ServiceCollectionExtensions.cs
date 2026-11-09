using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using ReversalFilmScanner.Rework.Processing;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scan rework services and binds <see cref="ProcessingOptions"/> and <see cref="ScanLayoutOptions"/>
    /// from the <c>Processing</c> and <c>ScanLayout</c> sections of <paramref name="configuration"/>.
    /// </summary>
    public static IServiceCollection AddScanRework(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ProcessingOptions>()
            .Bind(configuration.GetSection(ProcessingOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<ScanLayoutOptions>()
            .Bind(configuration.GetSection(ScanLayoutOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ProcessingOptions>, ProcessingOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ScanLayoutOptions>, ScanLayoutOptionsValidator>());

        services.TryAddSingleton<IFileSystem, FileSystem>();
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<IOrientationDetector, BlackPixelOrientationDetector>();
        services.TryAddSingleton<IImageTransformer, SkiaImageTransformer>();
        services.TryAddSingleton<IImageCodec, SkiaImageCodec>();
        services.TryAddSingleton<ImageFileLocator>();
        services.TryAddSingleton<IImageProcessor, ImageProcessor>();
        services.TryAddSingleton<IBatchProcessor, BatchProcessor>();

        return services;
    }
}
