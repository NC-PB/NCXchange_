namespace Ncx.Core.VirtualMachine;

/// <summary>
/// How a run of the virtual machine ended; what it found is in its diagnostics (virtual machine 2.9).
/// </summary>
public sealed record RunResult
{
    /// <summary>
    /// True when an ERROR stopped the run: one the parser or the pre-pass reported before the first block, or one a
    /// block raised (virtual machine 2.9, 3.6).
    /// </summary>
    public required bool Stopped { get; init; }
}
