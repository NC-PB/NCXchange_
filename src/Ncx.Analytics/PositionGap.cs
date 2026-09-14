namespace Ncx.Analytics;

/// <summary>
/// Why an axis that a motion moves is not known at both ends in one frame, so that the motion has no known length or no
/// known tool vector change (virtual machine 2.2, 3.4, 7): the reasons the segment length and the tool vector change
/// give for the motions they skip (implementation 14, P4-03).
/// </summary>
internal enum PositionGap
{
    /// <summary>
    /// The axis stands still, or is known at both ends in one frame.
    /// </summary>
    None,

    /// <summary>
    /// Unknown where the motion starts: the start of a program on an axis without home, after a HOME without a
    /// reference point, after a change of the frame or of the workpiece holder (D100, virtual machine 3.4).
    /// </summary>
    StartUnknown,

    /// <summary>
    /// Unknown where the motion ends: a bare RETRACT without limits, a target from an expression STATIC mode does not
    /// evaluate (virtual machine 1, 3.1a).
    /// </summary>
    EndUnknown,

    /// <summary>
    /// Known at both ends, in two frames: a motion of the workpiece frame from a position known only in the MACHINE
    /// frame, after HOME or a FRAME=MACHINE move (D35).
    /// </summary>
    TwoFrames,
}
