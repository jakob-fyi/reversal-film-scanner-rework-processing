namespace ReversalFilmScanner.Rework.Cli;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int PartialFailure = 1;
    public const int InvalidConfiguration = 2;
    public const int FatalError = 3;
    public const int Cancelled = 130;
}
