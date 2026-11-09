using System.IO.Abstractions;

namespace ReversalFilmScanner.Rework.Processing;

/// <summary>Finds the images a batch run would process.</summary>
public sealed class ImageFileLocator(IFileSystem fileSystem)
{
    /// <summary>
    /// Returns all files directly inside <paramref name="directory"/> with the given extension
    /// (with or without leading dot, case-insensitive), sorted ordinally.
    /// </summary>
    public IReadOnlyList<string> Find(string directory, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalized = extension.StartsWith('.') ? extension : "." + extension;

        return fileSystem.Directory.EnumerateFiles(directory)
            .Where(path => string.Equals(fileSystem.Path.GetExtension(path), normalized, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
