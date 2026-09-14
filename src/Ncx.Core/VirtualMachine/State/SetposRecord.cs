namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The record of a setpos shift that SETPOS took against the machine position of an axis known in the MACHINE frame
/// only (virtual machine 3.4, D101). It stands as long as that setpos shift does. A small immutable value, replaced
/// whole when it changes.
/// </summary>
/// <param name="ChainShift">The sum of the SHIFT entries of the chain on the axis at the SETPOS, a shift from an
/// expression counted as the 0 its entry keeps (wave-1 question #100). The store took in only the shifts appended or
/// removed since then, so the machine position is the stored value plus the shifts of the chain on the axis minus this
/// sum.</param>
/// <param name="UnknownShifts">The SHIFT entries of the chain at the SETPOS whose shift on the axis came from an
/// expression (virtual machine 1). The declared value stands in a frame that holds their unknown shift; a motion in a
/// frame that holds exactly these entries moves the machine by as much as the workpiece coordinate, and once one of
/// them is cut or another such entry stands, a motion leaves the machine position unknown.</param>
/// <param name="Holder">The workpiece holder at the SETPOS; null when the machine names none. Only under the machine's
/// default workpiece holder does a motion move the machine by as much as the workpiece coordinate: the frame of any
/// other holder has +Z out of its own chuck, and the machine reaches it through its mirror or datum convention, which
/// readers and compilers apply outside the VM (virtual machine 3.4, D57).</param>
/// <param name="MachinePositionKnown">True while the machine position of the axis follows from the store through the
/// record. A motion that can have moved the axis in the machine frame by another amount than its workpiece coordinate
/// makes it false; the next motion of the axis that moves it by as much makes it true again (virtual machine 3.4, 10,
/// D57, D101).</param>
public readonly record struct SetposRecord(
    decimal ChainShift,
    IReadOnlyList<TransformEntry> UnknownShifts,
    string? Holder,
    bool MachinePositionKnown);
