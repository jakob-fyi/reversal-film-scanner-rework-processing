using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;

namespace ReversalFilmScanner.Rework.Processing;

/// <summary>
/// Orchestrates a batch run:
/// <list type="number">
/// <item>copy all images into a backup directory and move them into a working directory,</item>
/// <item>process every image in the working directory,</item>
/// <item>move the results back into the input directory,</item>
/// <item>delete the backup - unless an image failed or the run was cancelled.</item>
/// </list>
/// </summary>
public sealed partial class BatchProcessor(
    IFileSystem fileSystem,
    IImageProcessor imageProcessor,
    ImageFileLocator imageFileLocator,
    TimeProvider timeProvider,
    IOptions<ProcessingOptions> options,
    ILogger<BatchProcessor> logger) : IBatchProcessor
{
    internal const string ProcessDirectoryPrefix = "rework-";

    private readonly ProcessingOptions _options = options.Value;

    public BatchResult Run(IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var inputDirectory = fileSystem.Path.GetFullPath(_options.InputDirectory);
        if (!fileSystem.Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException($"Input directory '{inputDirectory}' does not exist.");
        }

        var processId = Guid.CreateVersion7(timeProvider.GetUtcNow());
        var startedAt = timeProvider.GetUtcNow();
        var images = imageFileLocator.Find(inputDirectory, _options.FileExtension);

        LogStarting(processId, images.Count, inputDirectory);

        if (images.Count == 0)
        {
            return new BatchResult(processId, startedAt, timeProvider.GetUtcNow(), [], [], Cancelled: false, BackupDirectory: null);
        }

        var processDirectory = fileSystem.Path.Combine(inputDirectory, ProcessDirectoryPrefix + processId.ToString("N"));
        var backupDirectory = fileSystem.Path.Combine(processDirectory, "backup");
        var workingDirectory = fileSystem.Path.Combine(processDirectory, "working");

        fileSystem.Directory.CreateDirectory(backupDirectory);
        fileSystem.Directory.CreateDirectory(workingDirectory);

        foreach (var image in images)
        {
            fileSystem.File.Copy(image, fileSystem.Path.Combine(backupDirectory, fileSystem.Path.GetFileName(image)));
        }

        foreach (var image in images)
        {
            fileSystem.File.Move(image, fileSystem.Path.Combine(workingDirectory, fileSystem.Path.GetFileName(image)));
        }

        LogPrepared(backupDirectory);

        var processed = new List<ImageProcessingResult>(images.Count);
        var failures = new List<ImageFailure>();
        var cancelled = false;
        var completed = false;

        try
        {
            for (var i = 0; i < images.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    LogCancelled(images.Count - i);
                    break;
                }

                var fileName = fileSystem.Path.GetFileName(images[i]);
                var workingPath = fileSystem.Path.Combine(workingDirectory, fileName);

                try
                {
                    var result = imageProcessor.Process(workingPath, _options.RenameStartIndex + i);
                    processed.Add(result);
                    var resultFileName = fileSystem.Path.GetFileName(result.ResultPath);
                    var orientation = result.Orientation?.ToString() ?? "n/a";
                    LogImageProcessed(i + 1, images.Count, fileName, resultFileName, orientation);
                    progress?.Report(new BatchProgress(i + 1, images.Count, fileName, Failed: false));
                }
                catch (Exception ex) when (IsImageError(ex))
                {
                    failures.Add(new ImageFailure(fileName, ex.Message));
                    LogImageFailed(ex, i + 1, images.Count, fileName);
                    progress?.Report(new BatchProgress(i + 1, images.Count, fileName, Failed: true));
                }
            }

            completed = true;
        }
        finally
        {
            // Always hand the (partially) processed files back to the user - even on unexpected errors.
            foreach (var file in fileSystem.Directory.GetFiles(workingDirectory))
            {
                fileSystem.File.Move(file, fileSystem.Path.Combine(inputDirectory, fileSystem.Path.GetFileName(file)));
            }

            if (!completed)
            {
                LogBackupKept(backupDirectory);
            }
        }

        string? keptBackup = null;
        if (failures.Count == 0 && !cancelled)
        {
            fileSystem.Directory.Delete(processDirectory, recursive: true);
        }
        else
        {
            fileSystem.Directory.Delete(workingDirectory, recursive: true);
            keptBackup = backupDirectory;
            LogBackupKept(backupDirectory);
        }

        return new BatchResult(processId, startedAt, timeProvider.GetUtcNow(), processed, failures, cancelled, keptBackup);
    }

    /// <summary>Errors that only affect a single image; anything else aborts the whole batch.</summary>
    private static bool IsImageError(Exception ex) =>
        ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException;

    [LoggerMessage(Level = LogLevel.Information, Message = "Process {ProcessId}: found {ImageCount} image(s) in '{Directory}'")]
    private partial void LogStarting(Guid processId, int imageCount, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Originals backed up to '{BackupDirectory}'")]
    private partial void LogPrepared(string backupDirectory);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Current}/{Total}] {SourceFile} -> {ResultFile} (orientation: {Orientation})")]
    private partial void LogImageProcessed(int current, int total, string sourceFile, string resultFile, string orientation);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Current}/{Total}] {SourceFile} could not be processed")]
    private partial void LogImageFailed(Exception exception, int current, int total, string sourceFile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancellation requested - {Remaining} image(s) left unprocessed")]
    private partial void LogCancelled(int remaining);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Originals were kept in '{BackupDirectory}'")]
    private partial void LogBackupKept(string backupDirectory);
}
