using System.IO.Abstractions;
using ReversalFilmScanner.Rework.Cli;
using Spectre.Console;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // First Ctrl+C: finish the current image and restore all files. A second one terminates immediately.
    if (!cts.IsCancellationRequested)
    {
        e.Cancel = true;
        cts.Cancel();
    }
};

var consoleIsInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;

return new ScanReworkApp(AnsiConsole.Console, new FileSystem(), consoleIsInteractive).Run(args, cts.Token);
