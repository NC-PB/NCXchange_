using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

// What the job scheduler asks of the virtual machine of a channel (virtual machine 2.8, 3.7; architecture 5.4;
// Jobs/JobRunner): how many channels the job has, for SYNC in a single-channel job; the waits at the marks with their
// SYNC_WAIT and SYNC_RELEASE events; the spindles and axes each block commands, for the resources the channels share;
// and the rest of the file after a STATIC walk of a job.
public sealed partial class VirtualMachine
{
    // The keys of the words that command a spindle, which step 2 resolves to it (virtual machine 3.8 rules 1, 2, 4).
    private static readonly string[] s_spindleKeys =
        ["SPINDLE", "RPM", "CSS", "VC", "RPM_MAX", "ORIENT", "SPINDLE_MODE"];

    // The block that ran last, with what step 2 resolved (virtual machine 3.8); null after a block that did not run.
    private BlockContext? _lastContext;

    /// <summary>
    /// The number of channels of the job whose channel this virtual machine runs; null outside a job. SYNC in a
    /// single-channel job is a WARNING and nothing waits (virtual machine 3.7); a file checked on its own decides by
    /// its programs (ChannelValidation).
    /// </summary>
    internal int? JobChannels { get; init; }

    /// <summary>
    /// The subprograms the walks of this run entered by CALL (D99).
    /// </summary>
    internal IReadOnlySet<Section> CalledSubs => _calledSubs;

    /// <summary>
    /// The channel waits at a mark, or is released from it: channel.waitingAt changes, and SYNC_WAIT or SYNC_RELEASE
    /// is raised to the listeners of the channel with the state around the change (virtual machine 2.8, 3.7, 7).
    /// </summary>
    /// <param name="block">The SYNC block the channel waits at.</param>
    /// <param name="waitingAt">The mark the channel waits at from now on; null when it is released.</param>
    /// <param name="mark">The mark m of SYNC=m.</param>
    /// <param name="channels">The channels that take part, WITH or all channels of the job (language 4.8).</param>
    /// <param name="round">The round of the scheduler.</param>
    /// <returns>The event, which the timeline of the job keeps.</returns>
    internal SyncEvent ChangeWait(Block block, int? waitingAt, int mark, IReadOnlyList<int> channels, int round)
    {
        // The Before of the event is the After of the events before it, so that a listener misses no change (virtual
        // machine 7); without a listener nobody keeps that chain.
        ChannelSnapshot before = _lastAfter is ChannelSnapshot last && ReferenceEquals(_eventState, _state)
            ? last
            : _state.Snapshot();
        _state.WaitingAt = waitingAt;
        ChannelSnapshot after = _state.Snapshot();
        var sync = new SyncEvent
        {
            Channel = _state.ChannelId,
            Block = block,
            Before = before,
            After = after,
            Mark = mark,
            Channels = channels,
            Round = round,
            Released = waitingAt is null,
        };
        if (_listeners.Count > 0)
        {
            Publish([sync], after);
        }

        return sync;
    }

    /// <summary>
    /// The spindles the last executed block commands, by resource id, as step 2 resolved them: the spindle of each
    /// spindle word and both spindles of SPINDLE_SYNC (virtual machine 3.7, 3.8).
    /// </summary>
    internal List<string> CommandedSpindles()
    {
        var spindles = new List<string>();
        if (_lastContext is not BlockContext context)
        {
            return spindles;
        }

        foreach (KeyValuePair<Word, string> resolved in context.ResourceOf)
        {
            if (Array.IndexOf(s_spindleKeys, resolved.Key.Key) >= 0 && !spindles.Contains(resolved.Value))
            {
                spindles.Add(resolved.Value);
            }
        }

        foreach (string spindle in context.SyncSpindles)
        {
            if (!spindles.Contains(spindle))
            {
                spindles.Add(spindle);
            }
        }

        return spindles;
    }

    /// <summary>
    /// The axes the axis words of the last executed block command, by the NCX name the position store keeps them under,
    /// as step 2 resolved them (virtual machine 3.7, 3.8 rule 3).
    /// </summary>
    internal List<string> CommandedAxes()
    {
        var axes = new List<string>();
        if (_lastContext is not BlockContext context)
        {
            return axes;
        }

        foreach (string axis in context.AxisOf.Values)
        {
            if (!axes.Contains(axis))
            {
                axes.Add(axis);
            }
        }

        return axes;
    }

    /// <summary>
    /// Ends the STATIC walk of a channel of a job: the channel that walks the rest of its file walks the programs of
    /// the file that no channel ran, each from the state of a channel at the start of a run, and the subprograms that
    /// no walk called, from the default entry state, as a STATIC run of the file walks them (virtual machine 1, 3.9,
    /// D99); then the count of the expressions left unresolved (5) and FILE_END (7).
    /// </summary>
    /// <param name="restPrograms">The programs of the file no channel of the job ran; empty for the other
    /// channels.</param>
    /// <param name="restSubs">The subprograms of the file no walk of the job called; empty for the other
    /// channels.</param>
    /// <returns>False when an ERROR stopped the walk of the rest.</returns>
    internal bool FinishStaticJob(IReadOnlyList<Section> restPrograms, IReadOnlyList<Section> restSubs)
    {
        NcxProgram program = _stepsProgram ?? throw new InvalidOperationException("A job walk needs its program.");
        ChannelState channelState = _state;
        foreach (Section section in restPrograms)
        {
            StartWalk(NewState(section.Channel), suppressCallerRules: false);
            _state.Program.Section = section;
            if (!WalkToEnd(section, recordFirstVerb: section == program.Programs[0]))
            {
                return false;
            }
        }

        if (!WalkUncalledSubs(restSubs))
        {
            return false;
        }

        // The expressions STATIC mode left unresolved are counted and reported once (virtual machine 5), and the state
        // after the run is the one the channel's program left.
        _validation.EndRun();
        StartWalk(channelState, suppressCallerRules: false);
        RaiseFileEvent(program, EventPhase.End);
        return true;
    }
}
