namespace Ncx.Readers;

/// <summary>
/// The contour sections the structure pass made of a file (language 4.7.1, D65): the SUB section each cycle block names
/// as its contour, and the source blocks it moved into those sections, both by the line of the source block.
/// </summary>
internal sealed record ContourLayout
{
    /// <summary>
    /// The layout of a file without contour sections.
    /// </summary>
    public static ContourLayout None { get; } = new();

    /// <summary>
    /// The NAME of the SUB section of the contour each cycle block names, by the line of the cycle block.
    /// </summary>
    public IReadOnlyDictionary<int, string> Cycles { get; init; } = new Dictionary<int, string>();

    /// <summary>
    /// The lines of the source blocks that stand in a contour section, trivia included.
    /// </summary>
    public IReadOnlySet<int> Moved { get; init; } = new HashSet<int>();
}
