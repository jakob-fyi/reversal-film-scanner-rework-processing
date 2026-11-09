namespace ReversalFilmScanner.Rework.Processing;

/// <summary>Runs the configured rework steps (rename, crop, rotate/flip) on a single image file.</summary>
public interface IImageProcessor
{
    /// <param name="imagePath">Path of the image file. The file is modified in place / renamed.</param>
    /// <param name="sequenceNumber">Number used for renaming.</param>
    ImageProcessingResult Process(string imagePath, int sequenceNumber);
}
