namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// A work spindle as a rotating spindle or as a positioning C axis, SPINDLE_MODE (language 4.5, virtual machine 2.4).
/// </summary>
public enum SpindleMode
{
    /// <summary>
    /// SPINDLE_MODE=SPINDLE: a rotating spindle, the start value.
    /// </summary>
    Spindle,

    /// <summary>
    /// SPINDLE_MODE=AXIS: a positioning C axis driven with C=.
    /// </summary>
    Axis,
}
