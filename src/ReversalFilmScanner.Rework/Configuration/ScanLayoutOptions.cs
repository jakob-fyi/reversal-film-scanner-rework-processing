using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ReversalFilmScanner.Rework.Configuration;

/// <summary>
/// Geometry of the scanner output. The defaults match the 6000x4000 px images produced by the
/// reversal film scanner (see README for the illustrated areas). Bound from the <c>ScanLayout</c> section.
/// </summary>
public sealed class ScanLayoutOptions
{
    public const string SectionName = "ScanLayout";

    /// <summary>Area left of the slide that is only covered by the slide frame in portrait orientation.</summary>
    [Required]
    [ValidateObjectMembers]
    public PixelRegion LeftProbe { get; set; } = new(1070, 700, 600, 2500);

    /// <summary>Area right of the slide that is only covered by the slide frame in portrait orientation.</summary>
    [Required]
    [ValidateObjectMembers]
    public PixelRegion RightProbe { get; set; } = new(4170, 700, 600, 2500);

    /// <summary>A pixel counts as black if R + G + B is less than or equal to this value.</summary>
    [Range(0, 765)]
    public int BlackRgbSumThreshold { get; set; } = 45;

    /// <summary>Average share of black probe pixels (percent) above which the image is considered portrait.</summary>
    [Range(0d, 100d)]
    public double PortraitBlackPercentageThreshold { get; set; } = 95d;

    /// <summary>Visible slide area for portrait images.</summary>
    [Required]
    [ValidateObjectMembers]
    public PixelRegion PortraitCrop { get; set; } = new(1800, 170, 2270, 3540);

    /// <summary>Visible slide area for landscape images.</summary>
    [Required]
    [ValidateObjectMembers]
    public PixelRegion LandscapeCrop { get; set; } = new(1170, 800, 3540, 2270);
}
