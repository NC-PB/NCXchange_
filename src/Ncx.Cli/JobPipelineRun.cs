using Ncx.Core.Jobs;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// What one run of a job gives its command: the diagnostics of the manifest, the machine, the job and every file, the
/// result of the job, and the exit code (architecture 10, D97).
/// </summary>
internal sealed record JobPipelineRun
{
    /// <summary>
    /// Every diagnostic in the order it was reported: reading and loading the manifest, the machine file and the files
    /// of the channels, then the job itself, then every file with its parser, expander and virtual machine (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// False when the manifest, the machine file or a file of a channel could not be read: the job did not start (D97).
    /// </summary>
    public required bool InputsRead { get; init; }

    /// <summary>
    /// How the job ran; null when it did not start, because an input could not be read or loaded with an ERROR.
    /// </summary>
    public JobResult? Result { get; init; }

    /// <summary>
    /// The exit code of the job: 2 when an input could not be read, which is decided before the run starts and takes
    /// precedence; otherwise 1 with an ERROR, or with a WARNING under --strict, and 0 without (D97, architecture 10).
    /// </summary>
    /// <param name="strict">--strict.</param>
    public int ExitCode(bool strict)
    {
        return InputsRead ? ExitCodes.OfRun(Diagnostics, strict) : ExitCodes.NotStarted;
    }
}
