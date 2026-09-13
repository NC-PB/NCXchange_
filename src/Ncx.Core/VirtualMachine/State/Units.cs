namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The measurement units of a channel, set by UNITS=MM or UNITS=INCH (language 4.1) and UNKNOWN until then (virtual
/// machine 2.1).
/// </summary>
public enum Units
{
    /// <summary>
    /// No UNITS word yet, the start value (virtual machine 2.1); a motion now is an ERROR (virtual machine 3.1).
    /// </summary>
    Unknown,

    /// <summary>
    /// UNITS=MM: millimetres.
    /// </summary>
    Mm,

    /// <summary>
    /// UNITS=INCH: inches.
    /// </summary>
    Inch,
}
