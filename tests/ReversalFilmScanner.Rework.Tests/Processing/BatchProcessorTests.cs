using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Processing;

namespace ReversalFilmScanner.Rework.Tests.Processing;

public sealed class BatchProcessorTests
{
    private const string InputDirectory = "/scans";

    private readonly MockFileSystem _fileSystem = new();
    private readonly IImageProcessor _imageProcessor = Substitute.For<IImageProcessor>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly ProcessingOptions _options = new() { InputDirectory = InputDirectory, RenameStartIndex = 10 };

    public BatchProcessorTests()
    {
        _fileSystem.AddDirectory(InputDirectory);

        // Default behaviour: "process" an image by appending a marker so we can see which files were touched.
        _imageProcessor.Process(Arg.Any<string>(), Arg.Any<int>()).Returns(call =>
        {
            var path = call.ArgAt<string>(0);
            _fileSystem.File.AppendAllText(path, "+processed");
            return new ImageProcessingResult(_fileSystem.Path.GetFileName(path), path, null, false, false, false);
        });
    }

    private BatchProcessor CreateSut() => new(
        _fileSystem,
        _imageProcessor,
        new ImageFileLocator(_fileSystem),
        _time,
        Options.Create(_options),
        NullLogger<BatchProcessor>.Instance);

    private void AddImages(params string[] names)
    {
        foreach (var name in names)
        {
            _fileSystem.AddFile($"{InputDirectory}/{name}", new MockFileData(name));
        }
    }

    private string[] RemainingDirectories() => _fileSystem.Directory.GetDirectories(InputDirectory);

    [Fact]
    public void Run_InputDirectoryMissing_Throws()
    {
        _options.InputDirectory = "/does-not-exist";

        Assert.Throws<DirectoryNotFoundException>(() => CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Run_NoImages_ReturnsEmptyResultWithoutCreatingDirectories()
    {
        AddImages("notes.txt");

        var result = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Processed);
        Assert.Empty(RemainingDirectories());
        _imageProcessor.DidNotReceiveWithAnyArgs().Process(default!, default);
    }

    [Fact]
    public void Run_ProcessesImagesInWorkingDirectory_InSortedOrder_WithSequenceNumbers()
    {
        AddImages("b.jpg", "a.JPG", "c.jpg");

        CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("/working/a.JPG", StringComparison.Ordinal)), 10);
            _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("/working/b.jpg", StringComparison.Ordinal)), 11);
            _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("/working/c.jpg", StringComparison.Ordinal)), 12);
        });
    }

    [Fact]
    public void Run_Success_MovesResultsBackAndRemovesProcessDirectory()
    {
        AddImages("a.jpg", "b.jpg", "notes.txt");

        var result = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.BackupDirectory);
        Assert.Equal(2, result.Processed.Count);
        Assert.Equal("a.jpg+processed", _fileSystem.File.ReadAllText("/scans/a.jpg"));
        Assert.Equal("b.jpg+processed", _fileSystem.File.ReadAllText("/scans/b.jpg"));
        Assert.Equal("notes.txt", _fileSystem.File.ReadAllText("/scans/notes.txt"));
        Assert.Empty(RemainingDirectories());
    }

    [Fact]
    public void Run_Success_ReturnsResultFileNames()
    {
        AddImages("a.jpg");
        _imageProcessor.Process(Arg.Any<string>(), Arg.Any<int>()).Returns(call =>
        {
            var newPath = call.ArgAt<string>(0).Replace("a.jpg", "Prefix_000010.jpg", StringComparison.Ordinal);
            _fileSystem.File.Move(call.ArgAt<string>(0), newPath);
            return new ImageProcessingResult("a.jpg", newPath, null, true, false, false);
        });

        CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["/scans/Prefix_000010.jpg"], _fileSystem.Directory.GetFiles(InputDirectory));
    }

    [Fact]
    public void Run_ImageFails_ContinuesAndKeepsBackupOfOriginals()
    {
        AddImages("a.jpg", "b.jpg", "c.jpg");
        _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("b.jpg", StringComparison.Ordinal)), Arg.Any<int>())
            .Throws(new InvalidDataException("broken"));

        var result = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Processed.Count);
        Assert.Equal([new ImageFailure("b.jpg", "broken")], result.Failures);

        // Processed results are handed back, the failed image stays as it was.
        Assert.Equal("a.jpg+processed", _fileSystem.File.ReadAllText("/scans/a.jpg"));
        Assert.Equal("b.jpg", _fileSystem.File.ReadAllText("/scans/b.jpg"));
        Assert.Equal("c.jpg+processed", _fileSystem.File.ReadAllText("/scans/c.jpg"));

        // Untouched originals of every image are kept, the working directory is gone.
        Assert.NotNull(result.BackupDirectory);
        Assert.Equal("a.jpg", _fileSystem.File.ReadAllText($"{result.BackupDirectory}/a.jpg"));
        Assert.Equal(3, _fileSystem.Directory.GetFiles(result.BackupDirectory).Length);
        Assert.False(_fileSystem.Directory.Exists(_fileSystem.Path.Combine(result.BackupDirectory, "..", "working")));
    }

    [Fact]
    public void Run_UnexpectedException_RestoresFilesKeepsBackupAndRethrows()
    {
        AddImages("a.jpg", "b.jpg");
        _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("b.jpg", StringComparison.Ordinal)), Arg.Any<int>())
            .Throws(new NotSupportedException("boom"));

        Assert.Throws<NotSupportedException>(() => CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("a.jpg+processed", _fileSystem.File.ReadAllText("/scans/a.jpg"));
        Assert.Equal("b.jpg", _fileSystem.File.ReadAllText("/scans/b.jpg"));
        var processDirectory = Assert.Single(RemainingDirectories());
        Assert.Equal(2, _fileSystem.Directory.GetFiles(_fileSystem.Path.Combine(processDirectory, "backup")).Length);
    }

    [Fact]
    public void Run_CancelledBeforeStart_ProcessesNothingAndRestoresFiles()
    {
        AddImages("a.jpg", "b.jpg");

        var result = CreateSut().Run(cancellationToken: new CancellationToken(canceled: true));

        Assert.True(result.Cancelled);
        Assert.False(result.Succeeded);
        Assert.Empty(result.Processed);
        Assert.Equal("a.jpg", _fileSystem.File.ReadAllText("/scans/a.jpg"));
        Assert.NotNull(result.BackupDirectory);
    }

    [Fact]
    public void Run_CancelledDuringRun_FinishesCurrentImageAndStops()
    {
        AddImages("a.jpg", "b.jpg", "c.jpg");
        using var cts = new CancellationTokenSource();
        _imageProcessor.When(p => p.Process(Arg.Is<string>(s => s.EndsWith("a.jpg", StringComparison.Ordinal)), Arg.Any<int>()))
            .Do(_ => cts.Cancel());

        var result = CreateSut().Run(cancellationToken: cts.Token);

        Assert.True(result.Cancelled);
        Assert.Single(result.Processed);
        Assert.Equal("a.jpg+processed", _fileSystem.File.ReadAllText("/scans/a.jpg"));
        Assert.Equal("b.jpg", _fileSystem.File.ReadAllText("/scans/b.jpg"));
        Assert.Equal("c.jpg", _fileSystem.File.ReadAllText("/scans/c.jpg"));
    }

    [Fact]
    public void Run_ReportsProgressForEveryImage()
    {
        AddImages("a.jpg", "b.jpg");
        _imageProcessor.Process(Arg.Is<string>(p => p.EndsWith("b.jpg", StringComparison.Ordinal)), Arg.Any<int>())
            .Throws(new IOException("locked"));
        var reports = new List<BatchProgress>();

        CreateSut().Run(new InlineProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(
            [new BatchProgress(1, 2, "a.jpg", Failed: false), new BatchProgress(2, 2, "b.jpg", Failed: true)],
            reports);
    }

    [Fact]
    public void Run_MeasuresDurationWithTimeProvider()
    {
        AddImages("a.jpg");
        _imageProcessor.When(p => p.Process(Arg.Any<string>(), Arg.Any<int>())).Do(_ => _time.Advance(TimeSpan.FromSeconds(3)));

        var result = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(_time.Start, result.StartedAt);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Duration);
    }

    [Fact]
    public void Run_TwoRuns_UseDistinctProcessIds()
    {
        AddImages("a.jpg");

        var first = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);
        var second = CreateSut().Run(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(first.ProcessId, second.ProcessId);
    }

    private sealed class InlineProgress(Action<BatchProgress> handler) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => handler(value);
    }
}
