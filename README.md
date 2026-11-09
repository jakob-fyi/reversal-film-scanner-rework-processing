# Reversal Film Scanner Rework Processing
A small **.NET 10** command line tool to **batch** rework the scanned images from my [Reversal Film Scanner Project](https://github.com/JakobVesely/reversal-film-scanner).
It renames the images, analyzes their orientation, crops the actual slide based on that result and rotates/flips them.

## Usage

```bash
# Interactive, guided mode (directory picker, step selection, progress bar)
dotnet run --project src/ReversalFilmScanner.Rework.Cli

# Non-interactive (scripts, CI)
dotnet run --project src/ReversalFilmScanner.Rework.Cli -- ~/Desktop/Dia --prefix Dia_ --start 1 --rotate false
```

Running without arguments in a terminal starts the interactive mode; `-i` forces it (values given on the command
line are then preselected). `--help` lists all options:

| Option | Default | Description |
| --- | --- | --- |
| `<directory>` | | Directory with the scanned images |
| `-i, --interactive` | | Guided interactive mode |
| `-c, --config <file>` | | Additional JSON settings file (e.g. a scan layout for another scanner) |
| `-e, --extension <ext>` | `jpg` | Image file extension (case-insensitive) |
| `--rename <true\|false>` | `true` | Rename to `<prefix><number>` |
| `-p, --prefix <text>` | `Prefix_` | Rename prefix |
| `-s, --start <number>` | `1` | First sequence number |
| `--number-format <format>` | `000000` | .NET numeric format of the sequence number |
| `--crop <true\|false>` | `true` | Detect orientation and crop to the slide |
| `--rotate <true\|false>` | `true` | Rotate by 180° |
| `--flip <true\|false>` | `false` | Mirror horizontally |
| `-q, --quality <1-100>` | `100` | JPEG quality when re-encoding |

A flag without a value (`--flip`) means `true`. All settings, including the scan geometry (`ScanLayout`), can also be
set in [`appsettings.json`](src/ReversalFilmScanner.Rework.Cli/appsettings.json) or via environment variables
(`DOTNET_Processing__Rotate180=false`). Precedence: command line / interactive answers > `--config` file >
environment > `appsettings.json`.

**Safety:** all originals are copied to `<directory>/rework-<id>/backup` first. The backup is deleted only if every
image was processed successfully; on errors or cancellation (`Ctrl+C` finishes the current image) it is kept and its
path is printed. Each image is decoded and JPEG-encoded at most once.

Exit codes: `0` success, `1` some images failed, `2` invalid arguments/configuration, `3` fatal error, `130` cancelled.

## Project structure

```
src/ReversalFilmScanner.Rework        Core library (no console dependencies)
  Configuration/                      ProcessingOptions, ScanLayoutOptions (+ source-generated validation)
  Imaging/                            IOrientationDetector, IImageTransformer, IImageCodec (SkiaSharp)
  Processing/                         IImageProcessor (one image), IBatchProcessor (backup/working/restore)
src/ReversalFilmScanner.Rework.Cli    Composition root: System.CommandLine, Spectre.Console, Generic Host
tests/ReversalFilmScanner.Rework.Tests       Unit tests of the core library
tests/ReversalFilmScanner.Rework.Cli.Tests   Tests of argument mapping, interactive wizard and full CLI runs
```

The core uses dependency injection, the options pattern, `ILogger` (source-generated `LoggerMessage`),
`System.IO.Abstractions` for file access and `TimeProvider`, so everything runs against an in-memory file system in tests.

## Build & test

```bash
dotnet build
dotnet test
```

Tests use xUnit v3 on Microsoft.Testing.Platform, NSubstitute, `MockFileSystem`, `FakeTimeProvider` and
`Spectre.Console.Testing`. Package versions are managed centrally in `Directory.Packages.props`.

## Parts
### Analyze Orientation
The idea behind the process is to check out the areas of the scanned images, where landscape and portrait are not overlaping.
We analayze following areas `left` and `right` (rectagles between `P1 (x1, y1)` and `P2 (x2, y2)`):

Left: `P1 (1070, 700), P2 (1670, 3200)`

Right: `P1 (4170, 700), P2 (4770, 3200)`

These two areas have each a size of `600px` x `2500px` = `1.500.000`.

![Landscape](https://raw.githubusercontent.com/JakobVesely/reversal-film-scanner-orientation-analyzer/master/docs/areas_landscape.jpg)

![Portrait](https://raw.githubusercontent.com/JakobVesely/reversal-film-scanner-orientation-analyzer/master/docs/areas_portrait.jpg)

In our program we read the color of each of these 3 million pixels and check if the are "black" or not. If a high amout of "black" pixels are found, the image has to be a portrait oriented image.

```csharp
// BlackPixelOrientationDetector reads the raw BGRA/RGBA pixel buffer - R + G + B is independent of the channel order.
if (row[i] + row[i + 1] + row[i + 2] <= threshold)
{
    count++;
}
```

Complete ("real") black would be `RGB (0, 0, 0)`, white `RGB (255, 255, 255)`. So we sum up all three colors (RGB) and look if they are low enough that we can talk about a "black" pixel.

The threshold (`ScanLayout:BlackRgbSumThreshold`, default `45`) and both probe areas are configurable.

```csharp
return (double)blackPixels / region.Area * 100d;
```

Now we return the percentage of "black" pixels we found and if the average of both areas is higher than `95%`
(`ScanLayout:PortraitBlackPercentageThreshold`), the image is a portrait.

```csharp
return (left + right) / 2 > _layout.PortraitBlackPercentageThreshold
    ? Orientation.Portrait
    : Orientation.Landscape;
```
