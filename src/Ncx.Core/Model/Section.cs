namespace Ncx.Core.Model;

/// <summary>
/// A program, PROGRAM=BEGIN ... PROGRAM=END, or a subprogram, SUB=BEGIN NAME= ... SUB=END, of a file as a range of
/// its blocks (language 4.13).
/// </summary>
public sealed record Section
{
    /// <summary>
    /// Whether the section is a program or a subprogram.
    /// </summary>
    public required SectionKind Kind { get; init; }

    /// <summary>
    /// The value of NAME in the BEGIN block: the content of a string, an identifier or the digits of an integer, the
    /// name by which CALL finds a subprogram (language 4.1, 4.9); null for a program without NAME.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The program number of NUMBER (language 4.1); null without it.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// The channel the program runs on, from its CHANNEL header word, 1 without one (language 4.1, 4.14). A
    /// subprogram has no CHANNEL word and keeps the default.
    /// </summary>
    public int Channel { get; init; } = 1;

    /// <summary>
    /// The index of the PROGRAM=BEGIN or SUB=BEGIN block in the blocks of the program.
    /// </summary>
    public required int FirstBlock { get; init; }

    /// <summary>
    /// The index of the last block of the section, PROGRAM=END or SUB=END (language 4.13), in the blocks of the
    /// program.
    /// </summary>
    public required int LastBlock { get; init; }
}
