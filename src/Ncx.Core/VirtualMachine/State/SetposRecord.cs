namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The record of a setpos shift that SETPOS took against the machine position of an axis known in the MACHINE frame
/// only (virtual machine 3.4, D101). It stands as long as that setpos shift does. A small immutable value, replaced
/// whole when it changes.
/// </summary>
/// <param name="ChainShift">The sum of the known SHIFT entries of the chain on the axis at the SETPOS; a shift from an
/// expression is UNKNOWN and in no sum (virtual machine 1). The store took in only the known shifts appended or removed
/// since then, so the machine position is the stored value plus the known shifts of the chain on the axis minus this
/// sum.</param>
/// <param name="UnknownShifts">The SHIFT entries of the chain at the SETPOS that hold the shift on the axis as UNKNOWN
/// (virtual machine 1). The declared value stands in a frame that holds their unknown shift; a motion in a frame that
/// holds exactly these entries moves the machine by as much as the workpiece coordinate, and once one of them is cut or
/// another such entry stands, a motion leaves the machine position unknown.</param>
/// <param name="Holder">The workpiece holder at the SETPOS; null when the machine names none. Only under the machine's
/// default workpiece holder does a motion move the machine by as much as the workpiece coordinate: the frame of any
/// other holder has +Z out of its own chuck, and the machine reaches it through its mirror or datum convention, which
/// readers and compilers apply outside the VM (virtual machine 3.4, D57).</param>
/// <param name="Turned">True when the frame of the SETPOS turned or mirrored the axis against the machine frame: a
/// ROTATE of a plane the axis is in, a MIRROR of the axis, a TILT or TILT_AXIS. The declared value then relates to the
/// machine position only through a conversion the kinematics module makes, so a motion of the axis leaves the machine
/// position unknown; while nothing moves, ORIGIN and a change of the frame still find it (virtual machine 3.4, 10,
/// D101).</param>
/// <param name="MachinePositionKnown">True while the machine position of the axis follows from the store through the
/// record. A motion of the axis that can have moved it in the machine frame by another amount than its workpiece
/// coordinate makes it false; the next motion of the axis that moves it by as much makes it true again. A motion that
/// does not move the axis leaves it as it was (virtual machine 3.4, 10, D57, D101).</param>
public readonly record struct SetposRecord(
    decimal ChainShift,
    IReadOnlyList<TransformEntry> UnknownShifts,
    string? Holder,
    bool Turned,
    bool MachinePositionKnown);
