namespace Ncx.Core.Machine;

/// <summary>
/// [machine] gcode_system: the G code system of a Fanuc lathe (machine-config 1).
/// </summary>
public enum GcodeSystem
{
    /// <summary>
    /// gcode_system = "A": G98/G99 feed, U/W incremental, G90 is the turning cycle.
    /// </summary>
    A,

    /// <summary>
    /// gcode_system = "B": G94/G95 feed, G90/G91 absolute and incremental.
    /// </summary>
    B,

    /// <summary>
    /// gcode_system = "C": as B.
    /// </summary>
    C,
}
