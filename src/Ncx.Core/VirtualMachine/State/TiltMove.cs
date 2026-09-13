namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// How the machine reaches the plane of a TILT or TILT_AXIS, the value of MOVE (language 4.2, D82).
/// </summary>
public enum TiltMove
{
    /// <summary>
    /// MOVE=TURN: the rotary axes are positioned, the tool retracted first.
    /// </summary>
    Turn,

    /// <summary>
    /// MOVE=MOVE: the rotary axes are positioned while the tool tip stays on the workpiece.
    /// </summary>
    Move,

    /// <summary>
    /// MOVE=STAY: only the coordinate system rotates; the default.
    /// </summary>
    Stay,
}
