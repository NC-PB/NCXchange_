using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Jobs;

/// <summary>
/// How a job ran (virtual machine 3.7, architecture 5.4): every channel with the diagnostics of its file, the waits and
/// releases at the marks in the order the scheduler raised them, and whether an ERROR stopped the job.
/// </summary>
public sealed record JobResult
{
    /// <summary>
    /// The channels in the order of the job manifest (machine-config 8).
    /// </summary>
    public required IReadOnlyList<ChannelRun> Channels { get; init; }

    /// <summary>
    /// The timeline of the marks: SYNC_WAIT and SYNC_RELEASE of every channel in the order the scheduler raised them,
    /// each with its mark, its channels and its round (virtual machine 3.7, 7).
    /// </summary>
    public required IReadOnlyList<SyncEvent> Timeline { get; init; }

    /// <summary>
    /// The rounds the job ran; in each, every channel that was neither finished nor waiting executed one block (D39).
    /// </summary>
    public required int Rounds { get; init; }

    /// <summary>
    /// True when an ERROR stopped the job: one of a channel, or the deadlock at SYNC (virtual machine 2.9, 3.7).
    /// </summary>
    public required bool Stopped { get; init; }
}
