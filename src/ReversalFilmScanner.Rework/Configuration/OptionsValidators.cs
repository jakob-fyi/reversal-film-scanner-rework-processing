using Microsoft.Extensions.Options;

namespace ReversalFilmScanner.Rework.Configuration;

/// <summary>Source-generated (reflection-free) DataAnnotations validator for <see cref="ProcessingOptions"/>.</summary>
[OptionsValidator]
public sealed partial class ProcessingOptionsValidator : IValidateOptions<ProcessingOptions>;

/// <summary>Source-generated (reflection-free) DataAnnotations validator for <see cref="ScanLayoutOptions"/>.</summary>
[OptionsValidator]
public sealed partial class ScanLayoutOptionsValidator : IValidateOptions<ScanLayoutOptions>;
