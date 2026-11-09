using System.IO.Abstractions;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Imaging;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Processing;

public sealed class ImageProcessor(
    IFileSystem fileSystem,
    IImageCodec codec,
    IOrientationDetector orientationDetector,
    IImageTransformer transformer,
    IOptions<ProcessingOptions> processingOptions,
    IOptions<ScanLayoutOptions> layoutOptions) : IImageProcessor
{
    private readonly ProcessingOptions _options = processingOptions.Value;
    private readonly ScanLayoutOptions _layout = layoutOptions.Value;

    public ImageProcessingResult Process(string imagePath, int sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var sourceFileName = fileSystem.Path.GetFileName(imagePath);
        var path = _options.Rename ? Rename(imagePath, sequenceNumber) : imagePath;

        var rotateOrFlip = _options.Rotate180 || _options.FlipHorizontal;
        Orientation? orientation = null;

        // Load, transform and encode only once, so the image is re-compressed a single time at most.
        if (_options.Crop || rotateOrFlip)
        {
            var image = codec.Load(path);
            try
            {
                if (_options.Crop)
                {
                    orientation = orientationDetector.Detect(image);
                    var region = orientation == Orientation.Portrait ? _layout.PortraitCrop : _layout.LandscapeCrop;
                    image = Replace(image, transformer.Crop(image, region));
                }

                if (rotateOrFlip)
                {
                    image = Replace(image, transformer.RotateFlip(image, _options.Rotate180, _options.FlipHorizontal));
                }

                codec.SaveJpeg(image, path, _options.JpegQuality);
            }
            finally
            {
                image.Dispose();
            }
        }

        return new ImageProcessingResult(sourceFileName, path, orientation, _options.Rename, _options.Crop, rotateOrFlip);
    }

    private string Rename(string imagePath, int sequenceNumber)
    {
        var directory = fileSystem.Path.GetDirectoryName(imagePath) ?? string.Empty;
        var newFileName = SequentialFileName.Create(
            _options.RenamePrefix,
            _options.RenameNumberFormat,
            sequenceNumber,
            fileSystem.Path.GetExtension(imagePath));
        var newPath = fileSystem.Path.Combine(directory, newFileName);

        if (string.Equals(newPath, imagePath, StringComparison.Ordinal))
        {
            return imagePath;
        }

        if (fileSystem.File.Exists(newPath))
        {
            throw new IOException($"Cannot rename to '{newFileName}': a file with that name already exists.");
        }

        fileSystem.File.Move(imagePath, newPath);
        return newPath;
    }

    private static SKBitmap Replace(SKBitmap previous, SKBitmap next)
    {
        previous.Dispose();
        return next;
    }
}
