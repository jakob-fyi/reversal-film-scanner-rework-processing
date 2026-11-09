using ReversalFilmScanner.Rework.Imaging;

namespace ReversalFilmScanner.Rework.Processing;

/// <summary>Outcome of processing a single image.</summary>
/// <param name="SourceFileName">File name before processing.</param>
/// <param name="ResultPath">Path of the image after processing (inside the working directory).</param>
/// <param name="Orientation">Detected orientation, or <see langword="null"/> if cropping was disabled.</param>
public sealed record ImageProcessingResult(
    string SourceFileName,
    string ResultPath,
    Orientation? Orientation,
    bool Renamed,
    bool Cropped,
    bool RotatedOrFlipped);

/// <summary>An image that could not be processed.</summary>
public sealed record ImageFailure(string SourceFileName, string Error);

/// <summary>Summary of a batch run.</summary>
/// <param name="BackupDirectory">
/// Directory holding the untouched originals. Only kept (non-<see langword="null"/>) if the run failed or was cancelled.
/// </param>
public sealed record BatchResult(
    Guid ProcessId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<ImageProcessingResult> Processed,
    IReadOnlyList<ImageFailure> Failures,
    bool Cancelled,
    string? BackupDirectory)
{
    public TimeSpan Duration => EndedAt - StartedAt;

    public bool Succeeded => !Cancelled && Failures.Count == 0;
}

/// <summary>Progress notification sent after each image.</summary>
/// <param name="Completed">Number of images handled so far (processed or failed).</param>
/// <param name="CurrentFile">File name of the image that was just handled.</param>
public sealed record BatchProgress(int Completed, int Total, string CurrentFile, bool Failed);
