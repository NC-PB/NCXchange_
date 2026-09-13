namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The direction of a spindle, SPINDLE (language 4.5, virtual machine 2.4).
/// </summary>
public enum SpindleDirection
{
    /// <summary>
    /// SPINDLE=OFF (M5), the start value.
    /// </summary>
    Off,

    /// <summary>
    /// SPINDLE=CW (M3).
    /// </summary>
    Clockwise,

    /// <summary>
    /// SPINDLE=CCW (M4).
    /// </summary>
    Counterclockwise,
}
