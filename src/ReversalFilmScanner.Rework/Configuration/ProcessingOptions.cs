using System.ComponentModel.DataAnnotations;

namespace ReversalFilmScanner.Rework.Configuration;

/// <summary>
/// Controls which steps of the batch rework are executed and on which directory.
/// Bound from the <c>Processing</c> configuration section.
/// </summary>
public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    /// <summary>Directory containing the scanned images. Results are written back into it.</summary>
    [Required(AllowEmptyStrings = false)]
    public string InputDirectory { get; set; } = string.Empty;

    /// <summary>File extension of the images to process (with or without leading dot, case-insensitive).</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(@"^\.?[A-Za-z0-9]+$", ErrorMessage = "FileExtension must be a single extension such as 'jpg'.")]
    public string FileExtension { get; set; } = "jpg";

    /// <summary>Rename images to <see cref="RenamePrefix"/> + sequential number.</summary>
    public bool Rename { get; set; } = true;

    public string RenamePrefix { get; set; } = "Prefix_";

    [Range(0, int.MaxValue)]
    public int RenameStartIndex { get; set; } = 1;

    /// <summary>.NET custom numeric format string applied to the sequence number, e.g. <c>000000</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string RenameNumberFormat { get; set; } = "000000";

    /// <summary>Detect orientation and crop the image to the actual slide frame.</summary>
    public bool Crop { get; set; } = true;

    /// <summary>Rotate the image by 180 degrees (the scanner captures slides upside down).</summary>
    public bool Rotate180 { get; set; } = true;

    /// <summary>Mirror the image horizontally (for slides inserted the wrong way round).</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>JPEG quality (1-100) used when an image has to be re-encoded.</summary>
    [Range(1, 100)]
    public int JpegQuality { get; set; } = 100;
}
