namespace Ncx.Readers;

/// <summary>
/// What a reader of a controller family tells the structure pass about one source block: its role in the file, the
/// name and number of a program or subprogram it begins, the label it carries and the labels it jumps to (language
/// 4.13). The structure pass writes the words of the file structure from it; the reader writes the other words of the
/// block.
/// </summary>
public sealed record SourceStructure
{
    /// <summary>
    /// The structure of an ordinary block without a label.
    /// </summary>
    public static SourceStructure None { get; } = new();

    /// <summary>
    /// The role of the block in the structure of the file.
    /// </summary>
    public StructureRole Role { get; init; }

    /// <summary>
    /// The name of the program or subprogram the block begins, "2.5D FRAESEN" of BEGIN PGM; null when the source
    /// gives none, and then a program takes the comment of its begin block as its name (controller-mapping 1,
    /// Oxxxx (name)) and a subprogram its number.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The number of the program or subprogram the block begins, 1 of O0001; null when the source gives none.
    /// </summary>
    public long? Number { get; init; }

    /// <summary>
    /// The channel the program the block begins runs on, 2 of the Nakamura file O1000.P-2 (controller-mapping 7,
    /// CHANNEL); written as CHANNEL on its PROGRAM=BEGIN (language 4.1, 4.14). Null when the source names none.
    /// </summary>
    public long? Channel { get; init; }

    /// <summary>
    /// The label the block carries, "22" of N22, "5" of LBL 5; a LABEL when a block of its program or subprogram
    /// jumps to it (controller-mapping 6, LABEL). Null for a block without one.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The labels the block jumps to or repeats from, "300" of GOTO 300 (language 4.9, JUMP and REPEAT).
    /// </summary>
    public IReadOnlyList<string> LabelsUsed { get; init; } = [];

    /// <summary>
    /// The subprograms the block calls, each by the name of the section it enters, or by its number without leading
    /// zeros where the source names it by number: "100" of M98 P100 and of M98 P0100, which enter O0100 (language
    /// 4.9, CALL; controller-mapping 6). A section of the file that a block calls is a subprogram, since a CALL of a
    /// program is an ERROR (language 4.13).
    /// </summary>
    public IReadOnlyList<string> Calls { get; init; } = [];

    /// <summary>
    /// The contour a cycle of the block names by the labels of its first and its last block, P10 Q20 of a Fanuc G71
    /// (language 4.7, CONTOUR; machine-config 6, contour); null for a block that names none. The structure pass turns
    /// the range into a SUB section of the file, whose name the reader learns for the block (D65, language 4.7.1).
    /// </summary>
    public SourceContour? Contour { get; init; }
}
