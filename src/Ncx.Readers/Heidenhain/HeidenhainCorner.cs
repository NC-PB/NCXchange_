namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A chamfer CHF or a rounding RND as the line before it prepared it (D58, language 4.3): the chamfer line or the
/// rounding arc its block writes, where the corner ends, and the way the corner went, which the incremental words of the
/// line after it are written less; or why the corner stays RAW (D5).
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
    /// The chamfer line or the rounding arc; null for a rounding between two lines in one direction, which writes
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
    /// Where the corner ends in the working plane, which is where the line after it begins; null for a corner kept RAW.
    /// </summary>
    public HeidenhainPoint? End { get; init; }

    /// <summary>
    /// The direction the corner ends in; null where it is not known.
    /// </summary>
    public HeidenhainPoint? Tangent { get; init; }

    /// <summary>
    /// The line of the L block after the corner.
    /// </summary>
    public int NextLine { get; init; }

    /// <summary>
    /// The way from the corner point to the end of the corner per axis of the plane, which the incremental words of the
    /// line after the corner are written less; empty where that line has none.
    /// </summary>
    public Dictionary<string, decimal> Shift { get; init; } = new(StringComparer.Ordinal);
}
