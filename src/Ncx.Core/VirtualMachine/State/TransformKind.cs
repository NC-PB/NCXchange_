namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The word that appended an entry to the transform chain (language 4.2, virtual machine 2.1, D31, D82).
/// </summary>
public enum TransformKind
{
    /// <summary>
    /// SHIFT X= Y= Z=: a datum shift.
    /// </summary>
    Shift,

    /// <summary>
    /// ROTATE=: a rotation of the working plane about the tool axis.
    /// </summary>
    Rotate,

    /// <summary>
    /// MIRROR=: mirrored axes.
    /// </summary>
    Mirror,

    /// <summary>
    /// TILT A= B= C=: a tilted working plane by spatial angles.
    /// </summary>
    Tilt,

    /// <summary>
    /// TILT_AXIS A= B= C=: a tilted working plane by the rotary axis positions of the machine (D82).
    /// </summary>
    TiltAxis,
}
