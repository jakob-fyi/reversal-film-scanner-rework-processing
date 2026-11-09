using System.IO.Abstractions.TestingHelpers;
using SkiaSharp;
using Spectre.Console.Testing;

namespace ReversalFilmScanner.Rework.Cli.Tests;

/// <summary>Runs the complete command line application against an in-memory file system.</summary>
public sealed class ScanReworkAppTests : IDisposable
{
    private readonly TestConsole _console = new();
    private readonly MockFileSystem _fileSystem = new();

    public ScanReworkAppTests()
    {
        _console.Profile.Width = 200;
        _fileSystem.AddDirectory("/scans");
    }

    private void AddImage(string path, int width = 60, int height = 40)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.White);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        _fileSystem.AddFile(path, new MockFileData(data.ToArray()));
    }

    private int Run(bool interactiveConsole, params string[] args) =>
        new ScanReworkApp(_console, _fileSystem, interactiveConsole).Run(args, TestContext.Current.CancellationToken);

    [Fact]
    public void Help_PrintsUsageAndSucceeds()
    {
        var exitCode = Run(false, "--help");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[<directory>] [options]", _console.Output, StringComparison.Ordinal);
        Assert.Contains("--prefix", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOption_ReturnsInvalidConfiguration()
    {
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(false, "--bogus"));
        Assert.Contains("Unrecognized", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void NonInteractive_ProcessesDirectory()
    {
        AddImage("/scans/b.jpg");
        AddImage("/scans/a.jpg");

        var exitCode = Run(false, "/scans", "--crop", "false", "-p", "Dia_", "-s", "7");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["/scans/Dia_000007.jpg", "/scans/Dia_000008.jpg"], _fileSystem.Directory.GetFiles("/scans").Order());
        Assert.Contains("Succeeded", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigFile_OverridesScanLayout()
    {
        AddImage("/scans/a.jpg");
        var configFile = Path.Combine(Path.GetTempPath(), $"scan-rework-{Guid.NewGuid():N}.json");
        File.WriteAllText(configFile, """
            {
              "ScanLayout": {
                "LeftProbe": { "X": 0, "Y": 0, "Width": 10, "Height": 10 },
                "RightProbe": { "X": 50, "Y": 0, "Width": 10, "Height": 10 },
                "PortraitCrop": { "X": 20, "Y": 0, "Width": 20, "Height": 40 },
                "LandscapeCrop": { "X": 0, "Y": 10, "Width": 60, "Height": 20 }
              }
            }
            """);

        try
        {
            var exitCode = Run(false, "/scans", "--config", configFile, "--rename", "false");

            Assert.Equal(ExitCodes.Success, exitCode);
            using var result = SKBitmap.Decode(_fileSystem.File.ReadAllBytes("/scans/a.jpg"));
            Assert.Equal((60, 20), (result.Width, result.Height));
        }
        finally
        {
            File.Delete(configFile);
        }
    }

    [Fact]
    public void MissingConfigFile_ReturnsInvalidConfiguration() =>
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(false, "/scans", "--config", "/no/such/file.json"));

    [Fact]
    public void ImageTooSmallForDefaultLayout_ReturnsPartialFailureAndKeepsBackup()
    {
        AddImage("/scans/a.jpg");

        var exitCode = Run(false, "/scans");

        Assert.Equal(ExitCodes.PartialFailure, exitCode);
        Assert.Contains("Failed images", _console.Output, StringComparison.Ordinal);
        Assert.Single(_fileSystem.Directory.GetDirectories("/scans"));
    }

    [Theory]
    [InlineData("-q", "0")]
    [InlineData("-q", "101")]
    [InlineData("-e", "*.jpg")]
    public void InvalidOptionValues_ReturnInvalidConfiguration(string option, string value)
    {
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(false, "/scans", option, value));
        Assert.Contains("Invalid configuration", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDirectory_ReturnsInvalidConfiguration()
    {
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(false, "/does-not-exist"));
        Assert.Contains("does not exist", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgumentsWithoutTerminal_ReportsMissingDirectory()
    {
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(false));
        Assert.Contains("InputDirectory", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Interactive_AbortedByUser_ChangesNothing()
    {
        AddImage("/scans/a.jpg");
        _console.Profile.Capabilities.Interactive = true;
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("n");

        var exitCode = Run(true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Aborted", _console.Output, StringComparison.Ordinal);
        Assert.Equal(["/scans/a.jpg"], _fileSystem.Directory.GetFiles("/scans"));
    }

    [Fact]
    public void Interactive_ConfirmedRun_ProcessesImages()
    {
        AddImage("/scans/a.jpg");
        _console.Profile.Capabilities.Interactive = true;
        _console.Input.PushTextWithEnter("/scans");
        _console.Input.PushKey(ConsoleKey.DownArrow);   // -> Crop
        _console.Input.PushKey(ConsoleKey.Spacebar);    // deselect Crop (default layout does not fit 60x40)
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("Slide_");
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        var exitCode = Run(true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["/scans/Slide_000001.jpg"], _fileSystem.Directory.GetFiles("/scans"));
        Assert.Contains("Succeeded", _console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandLineValues_ArePreselectedInInteractiveMode()
    {
        AddImage("/scans/a.jpg");
        _console.Profile.Capabilities.Interactive = true;
        _console.Input.PushKey(ConsoleKey.Enter);       // directory from command line is the default
        _console.Input.PushKey(ConsoleKey.Enter);       // steps preselected from command line: rename only
        _console.Input.PushKey(ConsoleKey.Enter);       // prefix default from command line
        _console.Input.PushKey(ConsoleKey.Enter);
        _console.Input.PushTextWithEnter("y");

        var exitCode = Run(true, "-i", "/scans", "--crop", "false", "--rotate", "false", "-p", "X_");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["/scans/X_000001.jpg"], _fileSystem.Directory.GetFiles("/scans"));
    }

    public void Dispose() => _console.Dispose();
}
