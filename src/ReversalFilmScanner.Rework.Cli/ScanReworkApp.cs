using System.CommandLine;
using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Processing;
using Spectre.Console;

namespace ReversalFilmScanner.Rework.Cli;

/// <summary>Composition root of the command line tool. All I/O dependencies are injectable for tests.</summary>
internal sealed class ScanReworkApp(IAnsiConsole console, IFileSystem fileSystem, bool consoleIsInteractive)
{
    private readonly CliDefinition _cli = new();

    public int Run(string[] args, CancellationToken cancellationToken = default)
    {
        var rootCommand = _cli.CreateRootCommand();
        rootCommand.SetAction(parseResult => ExecuteSafely(parseResult, cancellationToken));

        var parseResult = rootCommand.Parse(args);
        var exitCode = parseResult.Invoke(new InvocationConfiguration
        {
            // Ctrl+C is handled by the caller: the current image is finished and the files are restored.
            ProcessTerminationTimeout = null,
            Output = console.Profile.Out.Writer,
            Error = console.Profile.Out.Writer,
        });

        // System.CommandLine reports parse errors with exit code 1, which already means "some images failed".
        return parseResult.Errors.Count > 0 ? ExitCodes.InvalidConfiguration : exitCode;
    }

    private int ExecuteSafely(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            return Execute(parseResult, cancellationToken);
        }
#pragma warning disable CA1031 // Top-level handler: report any fatal error instead of crashing with a stack dump.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            console.MarkupLine("[red]Fatal error:[/]");
            console.WriteException(ex, ExceptionFormats.ShortenPaths);
            return ExitCodes.FatalError;
        }
    }

    private int Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var interactive = _cli.IsInteractive(parseResult, consoleIsInteractive);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });

        if (parseResult.GetValue(_cli.Config) is { } configFile)
        {
            if (!configFile.Exists)
            {
                console.MarkupLineInterpolated($"[red]Settings file '{configFile.FullName}' does not exist.[/]");
                return ExitCodes.InvalidConfiguration;
            }

            builder.Configuration.AddJsonFile(configFile.FullName, optional: false);
        }

        builder.Configuration.AddInMemoryCollection(_cli.ToConfigurationOverrides(parseResult));

        if (interactive)
        {
            var defaults = builder.Configuration.GetSection(ProcessingOptions.SectionName).Get<ProcessingOptions>() ?? new();
            var answers = new InteractiveWizard(console, fileSystem).Run(defaults);
            if (answers is null)
            {
                console.MarkupLine("[grey]Aborted - no files were changed.[/]");
                return ExitCodes.Success;
            }

            builder.Configuration.AddInMemoryCollection(answers);
        }

        builder.Logging.ClearProviders();
        if (!interactive)
        {
            // The interactive mode shows a progress bar instead of log lines.
            builder.Logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
        }

        builder.Services.AddSingleton(fileSystem);
        builder.Services.AddScanRework(builder.Configuration);

        using var host = builder.Build();

        IBatchProcessor processor;
        try
        {
            processor = host.Services.GetRequiredService<IBatchProcessor>();
        }
        catch (OptionsValidationException ex)
        {
            foreach (var failure in ex.Failures)
            {
                console.MarkupLineInterpolated($"[red]Invalid configuration:[/] {failure}");
            }

            console.MarkupLine("Run with [blue]--help[/] for usage information, or without arguments for the interactive mode.");
            return ExitCodes.InvalidConfiguration;
        }

        BatchResult result;
        try
        {
            result = interactive
                ? ConsoleReporter.RunWithProgress(console, processor, cancellationToken)
                : processor.Run(progress: null, cancellationToken);
        }
        catch (DirectoryNotFoundException ex)
        {
            console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return ExitCodes.InvalidConfiguration;
        }
#pragma warning disable CA1031 // Top-level handler: report any fatal error instead of crashing with a stack dump.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            console.MarkupLine("[red]Fatal error - the originals are kept in the rework-* backup directory.[/]");
            console.WriteException(ex, ExceptionFormats.ShortenPaths);
            return ExitCodes.FatalError;
        }

        ConsoleReporter.RenderSummary(console, result);

        return result switch
        {
            { Cancelled: true } => ExitCodes.Cancelled,
            { Succeeded: false } => ExitCodes.PartialFailure,
            _ => ExitCodes.Success,
        };
    }
}
