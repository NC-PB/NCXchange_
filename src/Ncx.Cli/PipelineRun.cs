using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// What one run of the pipeline gives its command: the diagnostics of every stage, the program the virtual machine ran
/// and the exit code (architecture 10, D97).
/// </summary>
internal sealed record PipelineRun
{
    /// <summary>
    /// Every diagnostic in the order it was reported: reading the input and the machine file, loading the machine
    /// file, then the parser, the expander and the virtual machine (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// False when the NCX file or the machine file could not be read: the run did not start (D97).
    /// </summary>
    public required bool InputsRead { get; init; }

    /// <summary>
    /// The program the virtual machine ran, parsed and expanded; null when there was none to run, because an input
    /// could not be read or the machine file, the parser or the expander reported an ERROR (virtual machine 2.9).
    /// </summary>
    public NcxProgram? Program { get; init; }

    /// <summary>
    /// The byte order mark the NCX file began with, empty without one; it is no part of the text the parser reads
    /// (wave-1 question #80).
    /// </summary>
    public string ByteOrderMark { get; init; } = "";

    /// <summary>
    /// The exit code of the run: 2 when an input could not be read, which is decided before the run starts and takes
    /// precedence; otherwise 1 with an ERROR, or with a WARNING under --strict, and 0 without (D97, architecture 10).
    /// </summary>
    /// <param name="strict">--strict.</param>
    public int ExitCode(bool strict)
    {
        return InputsRead ? ExitCodes.OfRun(Diagnostics, strict) : ExitCodes.NotStarted;
    }
}
