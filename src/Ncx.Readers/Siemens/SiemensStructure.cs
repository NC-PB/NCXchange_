namespace Ncx.Readers.Siemens;

/// <summary>
/// A structure of the high-level language the reader is inside, IF, WHILE, FOR, LOOP or REPEAT, with the labels its
/// lowering writes (controllers siemens.md 8; controller-mapping 6, structured loops).
/// </summary>
internal sealed class SiemensStructure
{
    /// <summary>
    /// IF, WHILE, FOR, LOOP or REPEAT.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The label at the head of the structure, which the loops jump back to.
    /// </summary>
    public required string BeginLabel { get; init; }

    /// <summary>
    /// The label after the structure, which the IF, WHILE and FOR jump to when the condition fails.
    /// </summary>
    public required string EndLabel { get; init; }

    /// <summary>
    /// The label of the ELSE branch of an IF; null for an IF without ELSE and for the loops.
    /// </summary>
    public string? ElseLabel { get; init; }

    /// <summary>
    /// The variable of a FOR, R1 of FOR R1 = 1 TO 10, and its end value as NCX text.
    /// </summary>
    public string? Variable { get; init; }

    /// <summary>
    /// The line of the block that opened the structure.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// True for a structure kept as RAW as a whole: its condition, or its FOR, has no NCX form, so NCX cannot branch
    /// on it, and the structure, its blocks and its end are RAW (D5); it has no labels.
    /// </summary>
    public bool Raw { get; init; }
}
