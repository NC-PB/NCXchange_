namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A chamfer CHF or a rounding RND as the element before it prepared it (D58, language 4.3): the chamfer line or the
/// rounding arc its block writes, where the corner ends, the corner point the blocks up to the element after it count
/// from, and what the reader writes otherwise than the source in that element: its incremental words less the way the
/// corner went, the R of an arc by its radius, the ANGLE of an arc by its sweep; or why the corner stays RAW (D5).
/// </summary>
internal sealed record HeidenhainCorner
{
    /// <summary>
    /// The line of the CHF or RND block.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Why the corner stays RAW; null for a corner the reader expands.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// The chamfer line or the rounding arc; null for a rounding between two elements in one direction, which writes
    /// nothing.
    /// </summary>
    public HeidenhainDraftBlock? Block { get; init; }

    /// <summary>
    /// The first axis of the working plane, X under a Z tool axis.
    /// </summary>
    public string First { get; init; } = "";

    /// <summary>
    /// The second axis of the working plane, Y under a Z tool axis.
    /// </summary>
    public string Second { get; init; } = "";

    /// <summary>
    /// Where the corner ends in the working plane, which is where the element after it begins; null for a corner kept
    /// RAW.
    /// </summary>
    public HeidenhainPoint? End { get; init; }

    /// <summary>
    /// The corner point, where the element before the corner ends in the source and the blocks up to the element after
    /// it count from (HeidenhainCorners.CurrentPosition); null for a corner kept RAW.
    /// </summary>
    public HeidenhainPoint? Point { get; init; }

    /// <summary>
    /// The direction the corner ends in; null where it is not known.
    /// </summary>
    public HeidenhainPoint? Tangent { get; init; }

    /// <summary>
    /// The line of the contour element after the corner, up to which the corner holds; the line of the CHF or RND block
    /// for a corner kept RAW.
    /// </summary>
    public int NextLine { get; init; }

    /// <summary>
    /// The way from the corner point to the end of the corner per axis of the plane, which the incremental words of the
    /// element after the corner are written less; empty where that element has none.
    /// </summary>
    public Dictionary<string, decimal> Shift { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// True where the rounding leaves a half turn or less of a CR after it of more, and its R turns positive (language
    /// 4.3, R).
    /// </summary>
    public bool NextRadiusTurns { get; init; }

    /// <summary>
    /// The degrees the rounding takes off the sweep ANGLE of the CP IPA after it (D84); 0 for another element.
    /// </summary>
    public decimal NextSweepTrim { get; init; }
}
