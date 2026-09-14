using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

/// <summary>
/// One channel of a job while it runs (virtual machine 2.8, 3.7; architecture 5.4): the virtual machine of the channel,
/// the program it runs, the mark it waits at and whether it has finished. The job scheduler advances it by one block
/// per round while it is neither finished nor waiting.
/// </summary>
public sealed class ChannelRun
{
    internal ChannelRun(ChannelProgram channel, NcxProgram program, VirtualMachine.VirtualMachine vm)
    {
        Channel = channel.Id;
        ManifestProgram = channel.Program;
        ProgramName = channel.Program;
        Program = program;
        Vm = vm;
    }

    /// <summary>
    /// channel.id: the channel the job runs the program on, the id of its [[channel]] (virtual machine 2.8,
    /// machine-config 8).
    /// </summary>
    public int Channel { get; }

    /// <summary>
    /// The file of the channel, parsed and expanded; the channels that run programs of one file share it.
    /// </summary>
    public NcxProgram Program { get; }

    /// <summary>
    /// The NAME of the program that runs: the program of the [[channel]], or the NAME of the START_CHANNEL that started
    /// the channel; null for the first program of the file (language 4.8, 4.13; D48).
    /// </summary>
    public string? ProgramName { get; internal set; }

    /// <summary>
    /// The virtual machine of the channel; a listener subscribes to it before the job runs (virtual machine 7).
    /// </summary>
    public VirtualMachine.VirtualMachine Vm { get; }

    /// <summary>
    /// What the file of the channel reported: the parser, the expander, the virtual machine and the rules of the job on
    /// its blocks (D98).
    /// </summary>
    public Diagnostics Diagnostics => Program.Diagnostics;

    /// <summary>
    /// channel.waitingAt: the mark the channel waits at; null for none (virtual machine 2.8).
    /// </summary>
    public int? WaitingAt => Vm.State.WaitingAt;

    /// <summary>
    /// channel.finished: the program of the channel has ended (virtual machine 2.8).
    /// </summary>
    public bool Finished { get; internal set; }

    /// <summary>
    /// True when an ERROR stopped the run of the channel (virtual machine 2.9).
    /// </summary>
    public bool Stopped { get; internal set; }

    /// <summary>
    /// The program of the [[channel]]; null for the first program of the file (D48).
    /// </summary>
    internal string? ManifestProgram { get; }

    /// <summary>
    /// True once the run of the channel has begun: with the job, or at the START_CHANNEL that starts it.
    /// </summary>
    internal bool Started { get; set; }

    /// <summary>
    /// True while the channel waits for a START_CHANNEL of the job to start it (language 4.8).
    /// </summary>
    internal bool AwaitsStart { get; set; }

    /// <summary>
    /// The program section that runs, once the run has begun.
    /// </summary>
    internal Section? Section { get; set; }

    /// <summary>
    /// The SYNC block the channel waits at; null while it waits at no mark.
    /// </summary>
    internal Block? MarkBlock { get; set; }

    /// <summary>
    /// The channels that take part in the SYNC the channel waits at, the channel itself among them, in ascending order
    /// (language 4.8).
    /// </summary>
    internal IReadOnlyList<int> Participants { get; set; } = [];

    /// <summary>
    /// The channel whose end a WAIT_CHANNEL of this channel waits for; null for none (language 4.8).
    /// </summary>
    internal int? WaitingFor { get; set; }

    /// <summary>
    /// The WAIT_CHANNEL block the channel waits at; null while it waits for no channel.
    /// </summary>
    internal Block? ChannelWaitBlock { get; set; }

    /// <summary>
    /// The file of the block the channel stands at, which a diagnostic of the job names: its own, or the file of an
    /// external program a CALL runs (virtual machine 2.9, 3.6).
    /// </summary>
    internal string CurrentFile => Vm.Diagnostics.File;

    /// <summary>
    /// A channel that is neither finished nor waiting executes one block per round (virtual machine 3.7).
    /// </summary>
    internal bool Runnable => Started && !Finished && !Stopped && WaitingAt is null && WaitingFor is null;
}
