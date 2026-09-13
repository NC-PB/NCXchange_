namespace Ncx.Cli;

/// <summary>
/// The exit codes of every command (D97, architecture 10). Code 2 is decided before the run starts and takes
/// precedence; a run that has started ends with 0 or 1.
/// </summary>
internal static class ExitCodes
{
    // The run produced no ERROR.
    public const int NoError = 0;

    // The run produced at least one ERROR, or a WARNING under --strict, or format --check found a difference.
    public const int Error = 1;

    // A usage error, an input that cannot be read or a missing machine file where one is required: the run did not
    // start.
    public const int NotStarted = 2;
}
