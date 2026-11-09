using System.Globalization;

namespace ReversalFilmScanner.Rework.Processing;

public static class SequentialFileName
{
    /// <summary>Builds e.g. <c>Prefix_000001.JPG</c> from prefix, number format, index and extension.</summary>
    public static string Create(string prefix, string numberFormat, int index, string extension)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(numberFormat);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(extension);

        if (prefix.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Prefix '{prefix}' contains characters that are not allowed in file names.", nameof(prefix));
        }

        if (extension.Length > 0 && !extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        return prefix + index.ToString(numberFormat, CultureInfo.InvariantCulture) + extension;
    }
}
