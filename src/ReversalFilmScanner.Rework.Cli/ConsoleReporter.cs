using System.Globalization;
using ReversalFilmScanner.Rework.Processing;
using Spectre.Console;

namespace ReversalFilmScanner.Rework.Cli;

internal static class ConsoleReporter
{
    /// <summary>Runs the batch while showing a live progress bar.</summary>
    public static BatchResult RunWithProgress(IAnsiConsole console, IBatchProcessor processor, CancellationToken cancellationToken) =>
        console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(context =>
            {
                var task = context.AddTask("Preparing", maxValue: 1);
                var result = processor.Run(
                    new SynchronousProgress<BatchProgress>(p =>
                    {
                        task.MaxValue = p.Total;
                        task.Value = p.Completed;
                        task.Description = Markup.Escape(p.CurrentFile);
                    }),
                    cancellationToken);

                task.Description = "Done";
                task.Value = task.MaxValue;
                task.StopTask();
                return result;
            });

    public static void RenderSummary(IAnsiConsole console, BatchResult result)
    {
        var status = result switch
        {
            { Cancelled: true } => "[yellow]Cancelled[/]",
            { Succeeded: true } => "[green]Succeeded[/]",
            _ => "[red]Completed with errors[/]",
        };

        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Result[/]").HideHeaders();
        table.AddColumn("Key");
        table.AddColumn("Value");
        table.AddRow("Status", status);
        table.AddRow("Process", result.ProcessId.ToString());
        table.AddRow("Processed", result.Processed.Count.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Failed", result.Failures.Count.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Duration", result.Duration.ToString(@"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture));
        if (result.BackupDirectory is not null)
        {
            table.AddRow("Originals", Markup.Escape(result.BackupDirectory));
        }

        console.Write(table);

        if (result.Failures.Count > 0)
        {
            var failures = new Table().Border(TableBorder.Rounded).Title("[red]Failed images[/]");
            failures.AddColumn("File");
            failures.AddColumn("Error");
            foreach (var failure in result.Failures)
            {
                failures.AddRow(Markup.Escape(failure.SourceFileName), Markup.Escape(failure.Error));
            }

            console.Write(failures);
        }
    }

    /// <summary><see cref="Progress{T}"/> posts to the thread pool; this one reports inline so the UI stays in order.</summary>
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
