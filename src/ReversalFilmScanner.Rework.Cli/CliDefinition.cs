using System.CommandLine;
using System.Globalization;

namespace ReversalFilmScanner.Rework.Cli;

/// <summary>
/// Command line surface. Only options the user actually specified are turned into configuration overrides,
/// so <c>appsettings.json</c> / environment variables keep providing the defaults.
/// </summary>
internal sealed class CliDefinition
{
    public Argument<string?> Directory { get; } = new("directory")
    {
        Description = "Directory containing the scanned images. Omit it to start the interactive mode.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool> Interactive { get; } = new("--interactive", "-i")
    {
        Description = "Start the guided interactive mode (default when started without arguments).",
    };

    public Option<FileInfo?> Config { get; } = new("--config", "-c")
    {
        Description = "Additional JSON settings file, e.g. a scan layout profile for another scanner.",
        HelpName = "file",
    };

    public Option<string?> Extension { get; } = new("--extension", "-e")
    {
        Description = "File extension of the images to process [default: jpg]",
        HelpName = "ext",
    };

    public Option<bool?> Rename { get; } = new("--rename")
    {
        HelpName = "true|false",
        Description = "Rename images to <prefix><number> [default: true]",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> Prefix { get; } = new("--prefix", "-p")
    {
        Description = "Prefix for renamed files [default: Prefix_]",
        HelpName = "text",
    };

    public Option<int?> Start { get; } = new("--start", "-s")
    {
        Description = "First sequence number used for renaming [default: 1]",
        HelpName = "number",
    };

    public Option<string?> NumberFormat { get; } = new("--number-format")
    {
        Description = ".NET numeric format of the sequence number [default: 000000]",
        HelpName = "format",
    };

    public Option<bool?> Crop { get; } = new("--crop")
    {
        HelpName = "true|false",
        Description = "Detect orientation and crop to the slide frame [default: true]",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> Rotate { get; } = new("--rotate")
    {
        HelpName = "true|false",
        Description = "Rotate images by 180 degrees [default: true]",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> Flip { get; } = new("--flip")
    {
        HelpName = "true|false",
        Description = "Mirror images horizontally [default: false]",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<int?> Quality { get; } = new("--quality", "-q")
    {
        Description = "JPEG quality (1-100) used when re-encoding [default: 100]",
        HelpName = "1-100",
    };

    public RootCommand CreateRootCommand()
    {
        // Without this, a mistyped option such as "--rotat" would silently be taken as the directory.
        Directory.Validators.Add(result =>
        {
            if (result.Tokens is [{ Value: var token }] && token.StartsWith('-'))
            {
                result.AddError($"Unrecognized option '{token}'.");
            }
        });

        return new RootCommand(
            "Batch rework of slides scanned with the reversal film scanner: rename, detect orientation, crop, rotate and flip.")
        {
            Directory, Interactive, Config, Extension, Rename, Prefix, Start, NumberFormat, Crop, Rotate, Flip, Quality,
        };
    }

    /// <summary>Converts all explicitly specified command line values into configuration keys.</summary>
    public Dictionary<string, string?> ToConfigurationOverrides(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (parseResult.GetValue(Directory) is { Length: > 0 } directory)
        {
            overrides["Processing:InputDirectory"] = directory;
        }

        AddIfSpecified(parseResult, Extension, "Processing:FileExtension", overrides);
        AddFlagIfSpecified(parseResult, Rename, "Processing:Rename", overrides);
        AddIfSpecified(parseResult, Prefix, "Processing:RenamePrefix", overrides);
        AddIfSpecified(parseResult, Start, "Processing:RenameStartIndex", overrides);
        AddIfSpecified(parseResult, NumberFormat, "Processing:RenameNumberFormat", overrides);
        AddFlagIfSpecified(parseResult, Crop, "Processing:Crop", overrides);
        AddFlagIfSpecified(parseResult, Rotate, "Processing:Rotate180", overrides);
        AddFlagIfSpecified(parseResult, Flip, "Processing:FlipHorizontal", overrides);
        AddIfSpecified(parseResult, Quality, "Processing:JpegQuality", overrides);

        return overrides;
    }

    /// <summary>Interactive mode is used when requested explicitly, or when started without any argument from a terminal.</summary>
    public bool IsInteractive(ParseResult parseResult, bool consoleIsInteractive) =>
        parseResult.GetValue(Interactive) ||
        (consoleIsInteractive && parseResult.Tokens.Count == 0);

    private static void AddIfSpecified<T>(ParseResult parseResult, Option<T> option, string key, Dictionary<string, string?> overrides)
    {
        if (parseResult.GetResult(option) is not null && parseResult.GetValue(option) is { } value)
        {
            overrides[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    // A flag given without value ("--crop") means true.
    private static void AddFlagIfSpecified(ParseResult parseResult, Option<bool?> option, string key, Dictionary<string, string?> overrides)
    {
        if (parseResult.GetResult(option) is not null)
        {
            overrides[key] = (parseResult.GetValue(option) ?? true) ? "true" : "false";
        }
    }
}
