namespace ReversalFilmScanner.Rework.Cli.Tests;

public sealed class CliDefinitionTests
{
    private readonly CliDefinition _cli = new();

    private Dictionary<string, string?> Overrides(params string[] args) =>
        _cli.ToConfigurationOverrides(_cli.CreateRootCommand().Parse(args));

    [Fact]
    public void NoArguments_ProducesNoOverrides() =>
        Assert.Empty(Overrides());

    [Fact]
    public void AllOptions_AreMappedToConfigurationKeys()
    {
        var overrides = Overrides(
            "/scans", "-e", "jpeg", "-p", "Dia_", "-s", "5", "--number-format", "0000",
            "--rename", "false", "--crop", "false", "--rotate", "false", "--flip", "true", "-q", "90");

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["Processing:InputDirectory"] = "/scans",
                ["Processing:FileExtension"] = "jpeg",
                ["Processing:RenamePrefix"] = "Dia_",
                ["Processing:RenameStartIndex"] = "5",
                ["Processing:RenameNumberFormat"] = "0000",
                ["Processing:Rename"] = "false",
                ["Processing:Crop"] = "false",
                ["Processing:Rotate180"] = "false",
                ["Processing:FlipHorizontal"] = "true",
                ["Processing:JpegQuality"] = "90",
            },
            overrides);
    }

    [Theory]
    [InlineData("--rename", "Processing:Rename")]
    [InlineData("--crop", "Processing:Crop")]
    [InlineData("--rotate", "Processing:Rotate180")]
    [InlineData("--flip", "Processing:FlipHorizontal")]
    public void FlagWithoutValue_MeansTrue(string flag, string key) =>
        Assert.Equal("true", Overrides("/scans", flag)[key]);

    [Fact]
    public void UnspecifiedOptions_AreNotOverridden()
    {
        var overrides = Overrides("/scans", "--flip");

        Assert.Equal(["Processing:InputDirectory", "Processing:FlipHorizontal"], overrides.Keys);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("-x")]
    public void UnknownOption_IsAParseError(string option) =>
        Assert.NotEmpty(_cli.CreateRootCommand().Parse([option]).Errors);

    [Fact]
    public void InvalidNumber_IsAParseError() =>
        Assert.NotEmpty(_cli.CreateRootCommand().Parse(["/scans", "--start", "abc"]).Errors);

    [Theory]
    [InlineData(new string[0], true, true)]
    [InlineData(new string[0], false, false)]
    [InlineData(new[] { "-i" }, false, true)]
    [InlineData(new[] { "--interactive", "/scans" }, true, true)]
    [InlineData(new[] { "/scans" }, true, false)]
    [InlineData(new[] { "--crop", "false" }, true, false)]
    public void IsInteractive_WhenRequestedOrStartedWithoutArgumentsInATerminal(string[] args, bool consoleIsInteractive, bool expected)
    {
        var parseResult = _cli.CreateRootCommand().Parse(args);

        Assert.Equal(expected, _cli.IsInteractive(parseResult, consoleIsInteractive));
    }
}
