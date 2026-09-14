using System.Diagnostics.CodeAnalysis;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// A listener of the virtual machine: analytics, trace, the kinematics module and plugins subscribe one with
/// VirtualMachine.Subscribe and read every event it raises (virtual machine 7, architecture 5.3, 9). The interface
/// lives in Ncx.Core with the virtual machine that calls it (D106). A listener observes: the Before and After of an
/// event are read-only snapshots, and nothing it does reaches the state of the virtual machine (D61).
/// </summary>
public interface IVmListener
{
    /// <summary>
    /// Receives one event, in the order the virtual machine raises them: the events of a block in the order of
    /// BlockEvents, the blocks in the order they run.
    /// </summary>
    /// <param name="vmEvent">The event; switch on its type, ToolEvent or MotionEvent, to read its payload.</param>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "The name of the specification: IVmListener.On(VmEvent) (architecture 5.3, D106).")]
    void On(VmEvent vmEvent);
}
