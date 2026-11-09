using System.ComponentModel.DataAnnotations;
using SkiaSharp;

namespace ReversalFilmScanner.Rework.Configuration;

/// <summary>Axis-aligned rectangle in pixel coordinates of the original scan.</summary>
public sealed record PixelRegion
{
    [Range(0, int.MaxValue)]
    public int X { get; init; }

    [Range(0, int.MaxValue)]
    public int Y { get; init; }

    [Range(1, int.MaxValue)]
    public int Width { get; init; }

    [Range(1, int.MaxValue)]
    public int Height { get; init; }

    public PixelRegion()
    {
    }

    public PixelRegion(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public long Area => (long)Width * Height;

    public SKRectI ToRectI() => SKRectI.Create(X, Y, Width, Height);

    /// <summary>Returns <see langword="true"/> if the region lies completely inside an image of the given size.</summary>
    public bool FitsInto(int imageWidth, int imageHeight) =>
        X >= 0 && Y >= 0 && Width > 0 && Height > 0 &&
        (long)X + Width <= imageWidth &&
        (long)Y + Height <= imageHeight;

    public override string ToString() => $"(x={X}, y={Y}, w={Width}, h={Height})";
}
