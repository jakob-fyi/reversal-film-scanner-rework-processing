using System.IO.Abstractions.TestingHelpers;
using ReversalFilmScanner.Rework.Configuration;
using Spectre.Console.Testing;

namespace ReversalFilmScanner.Rework.Cli.Tests;

public sealed class InteractiveWizardTests : IDisposable
{
    private readonly TestConsole _console = new();
    private readonly MockFileSystem _fileSystem = new(new Dictionary<string, MockFileData>
    {
        ["/scans/a.jpg"] = new(""),
        ["/scans/b.JPG"] = new(""),
        ["/empty/notes.txt"] = new(""),
    });

    public InteractiveWizardTests()
    {
        _console.Profile.Capabilities.Interactive = true;
        _console.Profile.Width = 120;
    }

    private Dictionary<string, string?>? RunWizard(ProcessingOptions? defaults = null) =>
        new InteractiveWizard(_console, _fileSystem).Run(defaults ?? new ProcessingOptions());

    [Fact]
    public void AcceptingDefaults_ReturnsOverridesForAllSteps()
    {
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Enter);  // steps: keep preselection (rename, crop, rotate)
        _console.Input.PushKey(ConsoleKey.Enter);  // prefix: default
        _console.Input.PushKey(ConsoleKey.Enter);  // start: default
        _console.Input.PushTextWithEnter("y");     // confirm

        var result = RunWizard();

        Assert.NotNull(result);
        Assert.Equal("/scans", result["Processing:InputDirectory"]);
        Assert.Equal("true", result["Processing:Rename"]);
        Assert.Equal("true", result["Processing:Crop"]);
        Assert.Equal("true", result["Processing:Rotate180"]);
        Assert.Equal("false", result["Processing:FlipHorizontal"]);
        Assert.Equal("Prefix_", result["Processing:RenamePrefix"]);
        Assert.Equal("1", result["Processing:RenameStartIndex"]);
        Assert.Contains("Found 2 image(s)", _console.Output, StringComparison.Ordinal);
        Assert.Contains("Prefix_000001.jpg", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TogglingSteps_AndCustomRenameSettings_AreReturned()
    {
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.DownArrow);   // -> Crop
        _console.Input.PushKey(ConsoleKey.DownArrow);   // -> Rotate180
        _console.Input.PushKey(ConsoleKey.Spacebar);    // deselect Rotate180
        _console.Input.PushKey(ConsoleKey.DownArrow);   // -> FlipHorizontal
        _console.Input.PushKey(ConsoleKey.Spacebar);    // select FlipHorizontal
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("Urlaub_");
        _console.Input.PushTextWithEnter("100");
        _console.Input.PushTextWithEnter("y");

        var result = RunWizard();

        Assert.NotNull(result);
        Assert.Equal("false", result["Processing:Rotate180"]);
        Assert.Equal("true", result["Processing:FlipHorizontal"]);
        Assert.Equal("Urlaub_", result["Processing:RenamePrefix"]);
        Assert.Equal("100", result["Processing:RenameStartIndex"]);
        Assert.Contains("Urlaub_000100.jpg", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameNotSelected_DoesNotAskForPrefix()
    {
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Spacebar);    // deselect Rename
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        var result = RunWizard();

        Assert.NotNull(result);
        Assert.Equal("false", result["Processing:Rename"]);
        Assert.False(result.ContainsKey("Processing:RenamePrefix"));
        Assert.DoesNotContain("prefix", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownDirectory_IsRejectedAndAskedAgain()
    {
        _console.Input.PushTextWithEnter("/does-not-exist");
        _console.Input.PushTextWithEnter("\"/scans\"");  // quoted, as when dragged into the terminal
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        var result = RunWizard();

        Assert.Contains("Directory not found", _console.Output, StringComparison.Ordinal);
        Assert.Equal("/scans", result?["Processing:InputDirectory"]);
    }

    [Fact]
    public void DirectoryWithoutImages_CanBeAbandoned()
    {
        _console.Input.PushTextWithEnter("/empty");
        _console.Input.PushTextWithEnter("n");  // choose another directory? no

        Assert.Null(RunWizard());
        Assert.Contains("No *.jpg images found", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectoryWithoutImages_CanBeReplaced()
    {
        _console.Input.PushTextWithEnter("/empty");
        _console.Input.PushTextWithEnter("y");  // choose another directory? yes
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Spacebar); // deselect rename to keep the script short
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        Assert.Equal("/scans", RunWizard()?["Processing:InputDirectory"]);
    }

    [Fact]
    public void DecliningConfirmation_ReturnsNull()
    {
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("n");

        Assert.Null(RunWizard());
    }

    [Fact]
    public void NoStepSelected_ReturnsNull()
    {
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Enter);

        var result = RunWizard(new ProcessingOptions { Rename = false, Crop = false, Rotate180 = false, FlipHorizontal = false });

        Assert.Null(result);
        Assert.Contains("nothing to do", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredDirectory_IsOfferedAsDefault()
    {
        _console.Input.PushKey(ConsoleKey.Enter);       // accept default directory
        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        var result = RunWizard(new ProcessingOptions { InputDirectory = "/scans" });

        Assert.Equal("/scans", result?["Processing:InputDirectory"]);
    }

    [Fact]
    public void TerminalWithoutAnsi_AsksYesNoPerStep()
    {
        _console.EmitAnsiSequences = false;
        _console.Profile.Capabilities.Ansi = false;
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushTextWithEnter("n");  // rename
        _console.Input.PushTextWithEnter("y");  // crop
        _console.Input.PushTextWithEnter("n");  // rotate
        _console.Input.PushTextWithEnter("y");  // flip
        _console.Input.PushTextWithEnter("y");  // confirm

        var result = RunWizard();

        Assert.NotNull(result);
        Assert.Equal("false", result["Processing:Rename"]);
        Assert.Equal("true", result["Processing:Crop"]);
        Assert.Equal("false", result["Processing:Rotate180"]);
        Assert.Equal("true", result["Processing:FlipHorizontal"]);
    }

    [Theory]
    [InlineData("  /scans  ", "/scans")]
    [InlineData("'/my scans'", "/my scans")]
    [InlineData("\"/my scans\"", "/my scans")]
    public void NormalizePath_TrimsWhitespaceAndQuotes(string input, string expected) =>
        Assert.Equal(expected, InteractiveWizard.NormalizePath(input));

    [Fact]
    public void NormalizePath_ExpandsHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(home + "/scans", InteractiveWizard.NormalizePath("~/scans"));
    }

    public void Dispose() => _console.Dispose();
}
