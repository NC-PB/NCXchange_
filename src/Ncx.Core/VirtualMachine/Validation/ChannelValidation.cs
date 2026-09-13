using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// Channels and synchronization (language 4.8, 4.14; virtual machine 3.7, 5): SYNC in a single-channel job, and the two
/// rules of the job scheduler, the deadlock at SYNC and two channels on one spindle between marks.
/// </summary>
internal static class ChannelValidation
{
    // The stage that raises the rules of several channels (virtual machine 3.7).
    private const string JobScheduler = "the job scheduler";

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Channel",
        Summary = "Channels and their synchronization in a job (language 4.8, 4.14; VM 3.7, 5).",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.SyncInSingleChannelJob,
                "SYNC in a single-channel job; the channel does not wait.", "VM 3.7, 5"),
            ValidationRule.Error(DiagnosticCodes.SyncDeadlock,
                "Deadlock at SYNC: every channel waits and no mark can be released; the ERROR names the marks.",
                "VM 3.7, 5") with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.SpindleSharedBetweenMarks,
                "Two channels on one spindle between marks.", "VM 3.7, 5") with { RaisedBy = JobScheduler },
        ],
    };

    /// <summary>
    /// Tells whether the run of a file is a single-channel job: every program of the file runs on the same channel
    /// (language 4.1, CHANNEL; 4.14).
    /// </summary>
    // TODO(question): virtual machine 3.7 warns for "SYNC in a single-channel job" without saying which run is one when
    // a file is checked without a job manifest (machine-config 8); a file whose programs all run on one channel is a
    // single-channel job, and a file with programs on several channels is left to the job scheduler, until that is
    // answered.
    public static bool IsSingleChannelJob(NcxProgram program)
    {
        var channels = new HashSet<int>();
        foreach (Section section in program.Programs)
        {
            channels.Add(section.Channel);
        }

        return channels.Count <= 1;
    }

    /// <summary>
    /// SYNC in a single-channel job is a WARNING, and the channel does not wait: no other channel reaches the mark
    /// (virtual machine 3.7, 5).
    /// </summary>
    public static void CheckSync(Block block, bool singleChannelJob, Diagnostics diagnostics)
    {
        if (!singleChannelJob || block.Find("SYNC") is not Word sync)
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.SyncInSingleChannelJob,
            $"{sync.ToCanonical()} in a single-channel job: no other channel reaches the mark, and the channel does "
            + "not wait (virtual machine 3.7).");
    }
}
