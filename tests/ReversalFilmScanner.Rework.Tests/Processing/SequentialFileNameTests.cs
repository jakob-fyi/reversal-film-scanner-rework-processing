using System.Globalization;
using ReversalFilmScanner.Rework.Processing;

namespace ReversalFilmScanner.Rework.Tests.Processing;

public sealed class SequentialFileNameTests
{
    [Theory]
    [InlineData("Prefix_", "000000", 1, ".JPG", "Prefix_000001.JPG")]
    [InlineData("Prefix_", "000000", 1, "JPG", "Prefix_000001.JPG")]
    [InlineData("", "000", 42, ".jpg", "042.jpg")]
    [InlineData("Dia-", "0", 1234, ".jpg", "Dia-1234.jpg")]
    [InlineData("x", "000", 7, "", "x007")]
    [InlineData("Urlaub 1987 ", "0000", 0, ".jpeg", "Urlaub 1987 0000.jpeg")]
    public void Create_BuildsExpectedName(string prefix, string format, int index, string extension, string expected) =>
        Assert.Equal(expected, SequentialFileName.Create(prefix, format, index, extension));

    [Fact]
    public void Create_IsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A culture with "." as group separator (like de-DE) - built by hand, so it also works in invariant-globalization mode.
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberGroupSeparator = ".";
            CultureInfo.CurrentCulture = culture;

            Assert.Equal("1,234.jpg", SequentialFileName.Create(string.Empty, "#,##0", 1234, ".jpg"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("sub/dir_")]
    [InlineData("nul\0")]
    public void Create_PrefixWithInvalidFileNameChars_Throws(string prefix) =>
        Assert.Throws<ArgumentException>(() => SequentialFileName.Create(prefix, "000", 1, ".jpg"));

    [Fact]
    public void Create_NegativeIndex_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SequentialFileName.Create("p", "000", -1, ".jpg"));

    [Fact]
    public void Create_EmptyFormat_Throws() =>
        Assert.Throws<ArgumentException>(() => SequentialFileName.Create("p", " ", 1, ".jpg"));
}
