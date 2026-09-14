using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Jobs;

/// <summary>
/// The job scheduler (virtual machine 3.7, architecture 5.4, D39): one virtual machine per channel program of a job
/// manifest (machine-config 8, D15), advanced in rounds, in which every channel that is neither finished nor waiting
/// executes one block. SYNC marks pair the channels in execution order, WAIT_CHANNEL waits for a channel to finish,
/// START_CHANNEL starts the program of a channel; all channels waiting with no releasable mark is the deadlock ERROR,
/// and two channels commanding a spindle or an axis of [shared] between two marks a WARNING (D20). The job words are in
/// JobRunner.Words.cs, the releases and the deadlock in JobRunner.Waits.cs.
/// </summary>
public sealed partial class JobRunner
{
    private readonly MachineConfig _machine;
    private readonly VmOptions _options;
    private readonly ExecutionMode _mode;
    private readonly Diagnostics _diagnostics;
    private readonly int _channelCount;
    private readonly SharedResources _shared;
    private readonly List<ChannelRun> _channels = [];
    private readonly List<SyncEvent> _timeline = [];

    // The files whose pre-pass a channel of the job has run: the rules about a block as it is written are reported once
    // per file (virtual machine 3.6, 5), also when two channels run programs of one file (language 4.14).
    private readonly HashSet<NcxProgram> _prePassed = new(ReferenceEqualityComparer.Instance);

    private int _round;
    private bool _stopped;

    /// <summary>
    /// A job of the channel programs of a manifest on one machine.
    /// </summary>
    /// <param name="job">The job manifest: its channels, and the spindles and axes they share (machine-config
    /// 8).</param>
    /// <param name="machine">The machine of the job: the machine its manifest names, unless the command line names
    /// another.</param>
    /// <param name="options">The options of the run of every channel.</param>
    /// <param name="mode">STATIC for ncx check, INTERPRETED for ncx analyze (virtual machine 1).</param>
    /// <param name="diagnostics">Where the diagnostics about the job itself go, for the manifest file (D98).</param>
    public JobRunner(JobManifest job, MachineConfig machine, VmOptions options, ExecutionMode mode,
        Diagnostics diagnostics)
    {
        _machine = machine;
        _options = options;
        _mode = mode;
        _diagnostics = diagnostics;
        _shared = new SharedResources(job, machine);

        // The channels of the job are the channels of its manifest (machine-config 8); SYNC in a job of one channel is
        // a WARNING and nothing waits (virtual machine 3.7).
        var ids = new HashSet<int>();
        foreach (ChannelProgram channel in job.Channels)
        {
            ids.Add(channel.Id);
        }

        _channelCount = ids.Count;
    }

    /// <summary>
    /// Adds the program of one [[channel]] of the manifest, before Run: a virtual machine for the channel, to which the
    /// listeners of the channel subscribe (virtual machine 7).
    /// </summary>
    /// <param name="channel">The [[channel]] of the manifest.</param>
    /// <param name="program">Its file, parsed and expanded; channels that run programs of one file share it.</param>
    /// <param name="startValues">The start values of the variables of the file, from its vars file; null without one
    /// (virtual machine 2.7, 3.6).</param>
    /// <param name="externalPrograms">Loads the external programs a CALL names in INTERPRETED mode (virtual machine
    /// 3.6); null for none.</param>
    /// <returns>The channel, whose virtual machine a listener subscribes to.</returns>
    public ChannelRun Add(ChannelProgram channel, NcxProgram program,
        IReadOnlyDictionary<string, Value>? startValues = null, Func<string, NcxProgram?>? externalPrograms = null)
    {
        // One virtual machine per channel program (virtual machine 3.7), with the state of its channel.
        // TODO(question): virtual machine 2.8 makes the resources per job, shared by all channels, and 2.3 and 2.4 keep
        // the tool and spindle state per resource, but no document says whether a channel sees the state another
        // channel set (the spindle the other path started), and a shared holder would let a TOOL without a role on path
        // 2 change the turret of path 1, since it reaches default_holder (D219); every channel keeps its own state of
        // the resources, as a channel program checked alone does, and the job checks the resources of [shared] between
        // marks (SharedResources), until that is answered.
        var vm = new VirtualMachine.VirtualMachine(_machine, _options, program.Diagnostics, _mode, startValues)
        {
            ExternalPrograms = externalPrograms,
            JobChannels = _channelCount,
        };
        var run = new ChannelRun(channel, program, vm);
        _channels.Add(run);
        return run;
    }

    /// <summary>
    /// Runs the job in rounds until every channel has finished, an ERROR stops it, or no channel can go on, which is
    /// the deadlock (virtual machine 3.7, architecture 5.4). A STATIC job then walks the rest of each file, as ncx
    /// check walks a file (virtual machine 1, 3.9, D99).
    /// </summary>
    public JobResult Run()
    {
        if (ChannelsNamedOnce())
        {
            StartChannels();
            while (!_stopped && RunRound())
            {
                // One round after the other, until no channel can execute a block (virtual machine 3.7).
            }

            // No channel can execute a block, and not all have finished: the deadlock (virtual machine 3.7).
            if (!_stopped && !AllFinished())
            {
                ReportDeadlock();
                _stopped = true;
            }

            if (!_stopped && _mode == ExecutionMode.Static)
            {
                FinishStaticFiles();
            }
        }

        // A job that stops gives up the runs of its channels where they stand (virtual machine 2.9).
        foreach (ChannelRun channel in _channels)
        {
            channel.Vm.Abandon();
        }

        return new JobResult { Channels = _channels, Timeline = _timeline, Rounds = _round, Stopped = _stopped };
    }

    // A channel runs one program of the job: the manifest names each channel once (machine-config 8; virtual machine
    // 2.8, 3.7). False after the ERROR, which stops the job before its first round.
    private bool ChannelsNamedOnce()
    {
        var ids = new HashSet<int>();
        foreach (ChannelRun channel in _channels)
        {
            if (!ids.Add(channel.Channel))
            {
                _diagnostics.Error(1, DiagnosticCodes.ChannelTwiceInJob,
                    $"The job manifest names channel {channel.Channel} twice; a channel runs one program of the job "
                    + "(machine-config 8, virtual machine 3.7).");
                _stopped = true;
            }
        }

        return !_stopped;
    }

    // Every channel starts with the job, except a channel that a START_CHANNEL of the job names, which waits for it
    // (language 4.8). A channel whose run an ERROR stops before its first block stops the job; the others still begin,
    // so that the pre-pass of every file is reported (virtual machine 3.6).
    // TODO(question): language 4.8 and virtual machine 3.7 do not say which channels of a job start with it and which
    // wait for a START_CHANNEL, nor what a START_CHANNEL of a channel that runs or has run does; a channel that a
    // START_CHANNEL of any file of the job names waits for it, every other channel starts with the job, and a
    // START_CHANNEL of a channel that runs or has run starts nothing and warns, until that is answered.
    private void StartChannels()
    {
        HashSet<int> startedByWord = ChannelsStartedByWord();
        foreach (ChannelRun channel in _channels)
        {
            if (startedByWord.Contains(channel.Channel))
            {
                channel.AwaitsStart = true;
                continue;
            }

            Begin(channel, channel.ManifestProgram);
        }
    }

    // The run of a channel begins with its program on its channel (virtual machine 3.6, 3.7): the program the manifest
    // or the START_CHANNEL names, the first of the file without one.
    private void Begin(ChannelRun channel, string? programName)
    {
        channel.Started = true;
        channel.ProgramName = programName;
        bool prePass = _prePassed.Add(channel.Program);
        if (!channel.Vm.Begin(channel.Program, programName, channel.Channel, prePass))
        {
            channel.Stopped = true;
            _stopped = true;
            return;
        }

        channel.Section = channel.Vm.State.Program.Section;
        CheckHeaderChannel(channel);
    }

    // One round (virtual machine 3.7): every channel that is neither finished nor waiting executes one block, then the
    // waits that have come are released. An ERROR stops the run (2.9): the job stops once every channel has executed
    // its block of the round. False when no channel could execute a block.
    private bool RunRound()
    {
        var runnable = new List<ChannelRun>();
        foreach (ChannelRun channel in _channels)
        {
            if (channel.Runnable)
            {
                runnable.Add(channel);
            }
        }

        if (runnable.Count == 0)
        {
            return false;
        }

        _round++;
        foreach (ChannelRun channel in runnable)
        {
            StepChannel(channel);
        }

        foreach (ChannelRun channel in _channels)
        {
            _stopped |= channel.Stopped;
        }

        if (_stopped)
        {
            return false;
        }

        ReleaseWaits();
        return true;
    }

    // The channel executes one block; the spindles and axes of [shared] it commands and its job words take effect
    // after the block (virtual machine 3.7). The program ends at its PROGRAM=END: the channel is finished (2.8).
    private void StepChannel(ChannelRun channel)
    {
        StepResult step = channel.Vm.Step();
        if (step.Executed is Block block)
        {
            _shared.Commanded(channel, block);
            if (!ApplyJobWords(channel, block))
            {
                channel.Stopped = true;
                return;
            }
        }

        if (step.Stopped)
        {
            channel.Stopped = true;
        }
        else if (step.Ended)
        {
            channel.Finished = true;
        }
    }

    private bool AllFinished()
    {
        foreach (ChannelRun channel in _channels)
        {
            if (!channel.Finished)
            {
                return false;
            }
        }

        return true;
    }

    private ChannelRun? FindChannel(int id)
    {
        foreach (ChannelRun channel in _channels)
        {
            if (channel.Channel == id)
            {
                return channel;
            }
        }

        return null;
    }

    // After a STATIC job the rest of each file is walked once, as ncx check walks a file: the programs no channel ran
    // and the subprograms no walk of the job called (virtual machine 1, 3.9, D99); then every channel counts its
    // unresolved expressions and raises FILE_END. The first channel of each file walks its rest.
    private void FinishStaticFiles()
    {
        var walked = new HashSet<NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (ChannelRun channel in _channels)
        {
            bool walksRest = walked.Add(channel.Program);
            List<Section> restPrograms = walksRest ? RestPrograms(channel.Program) : [];
            List<Section> restSubs = walksRest ? RestSubs(channel.Program) : [];
            if (!channel.Vm.FinishStaticJob(restPrograms, restSubs))
            {
                channel.Stopped = true;
                _stopped = true;
                return;
            }
        }
    }

    // The programs of a file that no channel of the job ran.
    private List<Section> RestPrograms(NcxProgram program)
    {
        var rest = new List<Section>();
        foreach (Section section in program.Programs)
        {
            bool ran = false;
            foreach (ChannelRun channel in _channels)
            {
                ran |= ReferenceEquals(channel.Program, program) && channel.Section == section;
            }

            if (!ran)
            {
                rest.Add(section);
            }
        }

        return rest;
    }

    // The subprograms of a file that no walk of the job called (D99).
    private List<Section> RestSubs(NcxProgram program)
    {
        var rest = new List<Section>();
        foreach (Section sub in program.Subs)
        {
            bool called = false;
            foreach (ChannelRun channel in _channels)
            {
                called |= ReferenceEquals(channel.Program, program) && channel.Vm.CalledSubs.Contains(sub);
            }

            if (!called)
            {
                rest.Add(sub);
            }
        }

        return rest;
    }
}
