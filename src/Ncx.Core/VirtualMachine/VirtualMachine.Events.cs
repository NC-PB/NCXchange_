using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

// The events of the virtual machine (virtual machine 7, architecture 5.3): the listeners, the chain of Before and After
// from one block to the next, the events of the file, and the distance and blocks the events count under each tool and
// under the program. Step 7, RaiseEvents in VirtualMachine.cs, raises the events of a block through BlockEvents.
public sealed partial class VirtualMachine
{
    private readonly List<IVmListener> _listeners = [];

    // The distance and the blocks under the tool in the spindle of each holder, from its TOOL_BEGIN on.
    private readonly Dictionary<string, RunStatistics> _underTool = new(StringComparer.Ordinal);

    // The distance and the blocks of the program that runs, from its PROGRAM_BEGIN on.
    private readonly RunStatistics _underProgram = new();

    // The After of the events raised last, the Before of the next block, and the channel state it was taken of.
    private ChannelSnapshot? _lastAfter;
    private ChannelState? _eventState;

    /// <summary>
    /// Subscribes a listener to every event the virtual machine raises from now on: analytics, trace, the kinematics
    /// module and plugins (virtual machine 7, architecture 5.3, 9). Every listener receives every event, in the order
    /// raised, with read-only snapshots (D61, D106).
    /// </summary>
    /// <param name="listener">The listener.</param>
    public void Subscribe(IVmListener listener)
    {
        _listeners.Add(listener);
        ContinueEventsFrom(_state);
    }

    // Every listener receives every event of the block in the order raised; their After becomes the Before of the next
    // block, so that a listener misses no change (virtual machine 7). A block that raised nothing changed nothing a
    // listener reads, and the chain goes on from the events before it.
    private void Publish(List<VmEvent> events, ChannelSnapshot after)
    {
        if (events.Count == 0)
        {
            return;
        }

        foreach (VmEvent vmEvent in events)
        {
            foreach (IVmListener listener in _listeners)
            {
                listener.On(vmEvent);
            }
        }

        _lastAfter = after;
        _eventState = _state;
    }

    // FILE_BEGIN at FILE=BEGIN, before the first block of the first program, and FILE_END at FILE=END, after the last
    // walk, with the file name and the programs and subprograms of the pre-pass (virtual machine 2.1, 7). The two file
    // blocks stand outside every program and subprogram and no walk executes them, so the run raises their events.
    private void RaiseFileEvent(NcxProgram program, EventPhase phase)
    {
        Block? block = phase == EventPhase.Begin ? program.FileBegin : program.FileEnd;
        if (_listeners.Count == 0 || block is null)
        {
            return;
        }

        // FILE_BEGIN carries the state the first program starts from, FILE_END the state the run leaves.
        if (phase == EventPhase.Begin)
        {
            RestartEvents(_state);
        }
        else
        {
            ContinueEventsFrom(_state);
        }

        ChannelSnapshot state = _lastAfter ?? _state.Snapshot();
        var fileEvent = new FileEvent
        {
            Channel = state.ChannelId,
            Block = block,
            Before = state,
            After = state,
            Phase = phase,
            FileName = program.FileName,
            Programs = program.Programs,
            Subs = program.Subs,
        };
        Publish([fileEvent], state);
    }

    // A walk on another channel state, a program after the first or a subprogram nothing calls (D99), starts the chain
    // of Before and After from the state it starts from; a walk on the same state goes on with it.
    private void ContinueEventsFrom(ChannelState state)
    {
        if (!ReferenceEquals(state, _eventState))
        {
            RestartEvents(state);
        }
    }

    // The start of the run, and a walk on another channel state (D99), start the chain of Before and After anew from the
    // state as it is now. Every change inside a walk is a block's, and comes with the After of that block (virtual
    // machine 7).
    private void RestartEvents(ChannelState state)
    {
        if (_listeners.Count == 0)
        {
            return;
        }

        _lastAfter = state.Snapshot();
        _eventState = state;
    }
}
