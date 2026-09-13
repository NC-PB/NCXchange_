using System.Collections.ObjectModel;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// One entry of the transform chain: a SHIFT, ROTATE, MIRROR, TILT or TILT_AXIS in the order the program wrote it,
/// applied to the frame that was active where it stands (language 4.2, virtual machine 2.1, D31, D82). Immutable: the
/// chain grows at its end and is cut from its end, and an entry never changes.
/// </summary>
public sealed record TransformEntry
{
    /// <summary>
    /// The word that appended the entry.
    /// </summary>
    public required TransformKind Kind { get; init; }

    /// <summary>
    /// SHIFT: the shift of each axis by its NCX name; an omitted axis is 0 (language 4.2). Empty for the other kinds.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Shift { get; init; } = ReadOnlyDictionary<string, decimal>.Empty;

    /// <summary>
    /// ROTATE: the rotation of the working plane about the tool axis in degrees (language 4.2); 0 for the other kinds.
    /// </summary>
    public decimal Angle { get; init; }

    /// <summary>
    /// MIRROR: the mirrored axes by their NCX names (language 4.2). Empty for the other kinds.
    /// </summary>
    public IReadOnlyList<string> Mirrored { get; init; } = [];

    /// <summary>
    /// TILT: the spatial angles A, B, C; TILT_AXIS: the rotary axis angles A, B, C; in degrees by their names (language
    /// 4.2, D82). Empty for the other kinds.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Angles { get; init; } = ReadOnlyDictionary<string, decimal>.Empty;

    /// <summary>
    /// MOVE of a TILT or TILT_AXIS: how the machine reaches the plane; STAY when the block has no MOVE (language 4.2,
    /// D82).
    /// </summary>
    public TiltMove Move { get; init; } = TiltMove.Stay;

    /// <summary>
    /// ROT of a TILT or TILT_AXIS: whether the table turns or only the coordinate system; TABLE when the block has no
    /// ROT (language 4.2, D82).
    /// </summary>
    public TiltRot Rot { get; init; } = TiltRot.Table;
}
