using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// A listener that records every event it receives, and each as the line kind(line): payload (code-guidelines 8: a
/// small hand-written fake).
/// </summary>
internal sealed class FakeListener : IVmListener
{
    /// <summary>
    /// The events in the order the virtual machine raised them.
    /// </summary>
    public List<VmEvent> Events { get; } = [];

    /// <summary>
    /// The events as lines of text, TOOL_BEGIN(10): tool 1, holder H1, rpm 1592.
    /// </summary>
    public List<string> Lines { get; } = [];

    public void On(VmEvent vmEvent)
    {
        Events.Add(vmEvent);
        Lines.Add(vmEvent.ToString());
    }

    /// <summary>
    /// The kinds of the events in order, FILE_BEGIN, PROGRAM_BEGIN, ...
    /// </summary>
    public List<string> Kinds()
    {
        var kinds = new List<string>();
        foreach (VmEvent vmEvent in Events)
        {
            kinds.Add(vmEvent.Kind);
        }

        return kinds;
    }

    /// <summary>
    /// The lines of the events of one kind.
    /// </summary>
    public List<string> LinesOf(string kind)
    {
        var lines = new List<string>();
        foreach (VmEvent vmEvent in Events)
        {
            if (vmEvent.Kind == kind)
            {
                lines.Add(vmEvent.ToString());
            }
        }

        return lines;
    }

    /// <summary>
    /// The events of one record type, MotionEvent.
    /// </summary>
    public List<T> Of<T>()
        where T : VmEvent
    {
        var events = new List<T>();
        foreach (VmEvent vmEvent in Events)
        {
            if (vmEvent is T typed)
            {
                events.Add(typed);
            }
        }

        return events;
    }
}
