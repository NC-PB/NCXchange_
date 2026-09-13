namespace Ncx.Core.Machine;

/// <summary>
/// [[axis]] programming: how the X axis of a lathe is programmed; the reader interprets the source with it, the
/// compiler writes it (machine-config 4, D60).
/// </summary>
public enum Programming
{
    /// <summary>
    /// programming = "diameter": X words are diameters.
    /// </summary>
    Diameter,

    /// <summary>
    /// programming = "radius": X words are radii.
    /// </summary>
    Radius,

    /// <summary>
    /// programming = "switchable": the program switches with DIAMETER, written through [diameter].
    /// </summary>
    Switchable,
}
