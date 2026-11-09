namespace ReversalFilmScanner.Rework.Processing;

/// <summary>Processes all images of the configured input directory.</summary>
public interface IBatchProcessor
{
    /// <param name="progress">Optional receiver of per-image progress notifications.</param>
    /// <param name="cancellationToken">Stops after the current image; already processed files are kept.</param>
    BatchResult Run(IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default);
}
