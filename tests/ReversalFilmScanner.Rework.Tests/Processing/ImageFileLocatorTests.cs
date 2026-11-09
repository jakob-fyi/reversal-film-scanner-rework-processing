using System.IO.Abstractions.TestingHelpers;
using ReversalFilmScanner.Rework.Processing;

namespace ReversalFilmScanner.Rework.Tests.Processing;

public sealed class ImageFileLocatorTests
{
    private readonly MockFileSystem _fileSystem = new(new Dictionary<string, MockFileData>
    {
        ["/scans/b.JPG"] = new(""),
        ["/scans/a.jpg"] = new(""),
        ["/scans/c.Jpg"] = new(""),
        ["/scans/notes.txt"] = new(""),
        ["/scans/archive.jpg.bak"] = new(""),
        ["/scans/sub/d.jpg"] = new(""),
    });

    [Theory]
    [InlineData("jpg")]
    [InlineData(".jpg")]
    [InlineData("JPG")]
    public void Find_MatchesExtensionCaseInsensitive_TopLevelOnly_Sorted(string extension)
    {
        var result = new ImageFileLocator(_fileSystem).Find("/scans", extension);

        Assert.Equal(["/scans/a.jpg", "/scans/b.JPG", "/scans/c.Jpg"], result);
    }

    [Fact]
    public void Find_NoMatches_ReturnsEmpty() =>
        Assert.Empty(new ImageFileLocator(_fileSystem).Find("/scans", "png"));
}
