namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// On table kinematics, whether the table turns or only the coordinate system, the value of ROT on a TILT or
/// TILT_AXIS (language 4.2, D82).
/// </summary>
public enum TiltRot
{
    /// <summary>
    /// ROT=TABLE: the table turns so that the workpiece faces the tool; the default.
    /// </summary>
    Table,

    /// <summary>
    /// ROT=COORD: only the coordinate system rotates.
    /// </summary>
    Coord,
}
