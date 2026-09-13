namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The working plane of WORKPLANE, with the tool axis perpendicular to it (language 4.2, virtual machine 2.1).
/// </summary>
public enum Workplane
{
    /// <summary>
    /// WORKPLANE=XY, G17: the tool axis is Z.
    /// </summary>
    XY,

    /// <summary>
    /// WORKPLANE=ZX, G18: the tool axis is Y.
    /// </summary>
    ZX,

    /// <summary>
    /// WORKPLANE=YZ, G19: the tool axis is X.
    /// </summary>
    YZ,
}
