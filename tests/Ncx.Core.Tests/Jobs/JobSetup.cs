using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// A job of the tests: the files of its channels as texts, and what the manifest and the run say beyond them
/// (machine-config 8; virtual machine 1).
/// </summary>
internal sealed record JobSetup
{
    /// <summary>
    /// The files, the n-th named "chn.ncx"; channel n runs the n-th file unless Channels says otherwise.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    /// INTERPRETED, the mode of ncx analyze, unless the test runs STATIC, the mode of ncx check.
    /// </summary>
    public ExecutionMode Mode { get; init; } = ExecutionMode.Interpreted;

    /// <summary>
    /// [shared] spindles of the manifest.
    /// </summary>
    public IReadOnlyList<string> SharedSpindles { get; init; } = [];

    /// <summary>
    /// [shared] axes of the manifest.
    /// </summary>
    public IReadOnlyList<string> SharedAxes { get; init; } = [];

    /// <summary>
    /// The [[channel]] tables of the manifest, naming the files "ch1.ncx", "ch2.ncx", ...; null for channel n running
    /// the n-th file.
    /// </summary>
    public IReadOnlyList<ChannelProgram>? Channels { get; init; }

    /// <summary>
    /// The machine; null for the built-in machine of D103.
    /// </summary>
    public MachineConfig? Machine { get; init; }
}
