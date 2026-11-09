using System.Globalization;
using System.IO.Abstractions;
using ReversalFilmScanner.Rework.Configuration;
using ReversalFilmScanner.Rework.Processing;
using Spectre.Console;

namespace ReversalFilmScanner.Rework.Cli;

/// <summary>Guided mode: asks for all processing settings and returns them as configuration overrides.</summary>
internal sealed class InteractiveWizard(IAnsiConsole console, IFileSystem fileSystem)
{
    internal enum Step
    {
        Rename,
        Crop,
        Rotate180,
        FlipHorizontal,
    }

    private readonly ImageFileLocator _locator = new(fileSystem);

    /// <returns>Configuration overrides, or <see langword="null"/> if the user aborted.</returns>
    public Dictionary<string, string?>? Run(ProcessingOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        console.Write(new FigletText("Scan Rework").Color(Color.Orange1));
        console.MarkupLine("[grey]Batch rework of scanned slides. Originals are backed up until the run succeeded.[/]");
        console.WriteLine();

        var (directory, images) = AskDirectory(defaults);
        if (directory is null)
        {
            return null;
        }

        var steps = AskSteps(defaults);
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Processing:InputDirectory"] = directory,
            ["Processing:Rename"] = Bool(steps.Contains(Step.Rename)),
            ["Processing:Crop"] = Bool(steps.Contains(Step.Crop)),
            ["Processing:Rotate180"] = Bool(steps.Contains(Step.Rotate180)),
            ["Processing:FlipHorizontal"] = Bool(steps.Contains(Step.FlipHorizontal)),
        };

        var prefix = defaults.RenamePrefix;
        var startIndex = defaults.RenameStartIndex;
        if (steps.Contains(Step.Rename))
        {
            prefix = console.Prompt(
                new TextPrompt<string>("File name [green]prefix[/]:")
                    .DefaultValue(defaults.RenamePrefix)
                    .AllowEmpty()
                    .Validate(p => p.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]The prefix contains characters that are not allowed in file names.[/]")));
            startIndex = console.Prompt(
                new TextPrompt<int>("First [green]number[/]:")
                    .DefaultValue(defaults.RenameStartIndex)
                    .Validate(n => n >= 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Must not be negative.[/]")));
            overrides["Processing:RenamePrefix"] = prefix;
            overrides["Processing:RenameStartIndex"] = startIndex.ToString(CultureInfo.InvariantCulture);
        }

        console.WriteLine();
        console.Write(Summary(directory, images, steps, prefix, startIndex, defaults));

        if (steps.Count == 0)
        {
            console.MarkupLine("[yellow]No step selected - nothing to do.[/]");
            return null;
        }

        return console.Confirm("Start processing?") ? overrides : null;
    }

    private (string? Directory, IReadOnlyList<string> Images) AskDirectory(ProcessingOptions defaults)
    {
        while (true)
        {
            var prompt = new TextPrompt<string>("Directory with the scanned [green]images[/]:")
                .Validate(input => fileSystem.Directory.Exists(NormalizePath(input))
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Directory not found.[/]"));
            if (!string.IsNullOrWhiteSpace(defaults.InputDirectory))
            {
                prompt.DefaultValue(defaults.InputDirectory);
            }

            var directory = NormalizePath(console.Prompt(prompt));
            var images = _locator.Find(directory, defaults.FileExtension);

            if (images.Count > 0)
            {
                console.MarkupLineInterpolated($"Found [green]{images.Count}[/] image(s) [grey](*.{defaults.FileExtension.TrimStart('.')})[/].");
                console.WriteLine();
                return (directory, images);
            }

            console.MarkupLineInterpolated($"[yellow]No *.{defaults.FileExtension.TrimStart('.')} images found in {directory}.[/]");
            if (!console.Confirm("Choose another directory?"))
            {
                return (null, []);
            }
        }
    }

    private List<Step> AskSteps(ProcessingOptions defaults)
    {
        if (!console.Profile.Capabilities.Ansi)
        {
            // Selection lists need ANSI cursor control - fall back to one yes/no question per step.
            console.MarkupLine("Which [green]steps[/] should be applied?");
            return Enum.GetValues<Step>()
                .Where(step => console.Confirm(Markup.Escape(Describe(step).Trim()), IsSelectedByDefault(step, defaults)))
                .ToList();
        }

        var prompt = new MultiSelectionPrompt<Step>()
            .Title("Which [green]steps[/] should be applied?")
            .NotRequired()
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle a step, [green]<enter>[/] to accept)[/]")
            .UseConverter(Describe)
            .AddChoices(Enum.GetValues<Step>());

        foreach (var step in Enum.GetValues<Step>().Where(step => IsSelectedByDefault(step, defaults)))
        {
            prompt.Select(step);
        }

        return console.Prompt(prompt);
    }

    private static bool IsSelectedByDefault(Step step, ProcessingOptions defaults) => step switch
    {
        Step.Rename => defaults.Rename,
        Step.Crop => defaults.Crop,
        Step.Rotate180 => defaults.Rotate180,
        Step.FlipHorizontal => defaults.FlipHorizontal,
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
    };

    internal static string Describe(Step step) => step switch
    {
        Step.Rename => "Rename        (sequential numbering)",
        Step.Crop => "Crop          (detect orientation, cut away the slide frame)",
        Step.Rotate180 => "Rotate 180°",
        Step.FlipHorizontal => "Flip          (mirror horizontally)",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
    };

    private Table Summary(string directory, IReadOnlyList<string> images, List<Step> steps, string prefix, int startIndex, ProcessingOptions defaults)
    {
        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Settings[/]").HideHeaders();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Directory", Markup.Escape(directory));
        table.AddRow("Images", images.Count.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Steps", steps.Count == 0 ? "[grey]none[/]" : string.Join(", ", steps));
        if (steps.Contains(Step.Rename))
        {
            var extension = fileSystem.Path.GetExtension(images[0]);
            var example = SequentialFileName.Create(prefix, defaults.RenameNumberFormat, startIndex, extension);
            table.AddRow("File names", Markup.Escape($"{example}, ..."));
        }

        return table;
    }

    /// <summary>Accepts paths dragged into the terminal (quoted) and <c>~</c> for the home directory.</summary>
    internal static string NormalizePath(string input)
    {
        var path = input.Trim().Trim('"', '\'');
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }

        return path;
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
