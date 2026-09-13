using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// One step of the reading plan of the structure pass: a trivia line, a source block to read, or a block of the file
/// structure to write with its line and words (language 4.13).
/// </summary>
internal sealed record ReadStep
{
    /// <summary>
    /// What the step does.
    /// </summary>
    public required ReadStepKind Kind { get; init; }

    /// <summary>
    /// The source block the step belongs to; a written block stands among the NCX blocks of this source block.
    /// </summary>
    public required SourceBlock Source { get; init; }

    /// <summary>
    /// The line of a written block, which its diagnostics cite; the line of the source block it stands for, which is
    /// not always Source (a PROGRAM=END written after the section moved in front of it keeps the line of the M30).
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// The words of a written block.
    /// </summary>
    public IReadOnlyList<Word> Words { get; init; } = [];

    /// <summary>
    /// True when a written block stands in the place of its source block and carries its block skip: LABEL, JUMP,
    /// RETURN (language 4.1, SKIP; controller-mapping 6).
    /// </summary>
    public bool CarriesSkip { get; init; }

    /// <summary>
    /// True when a written block may take the comment of its source block, as the first NCX block read from it
    /// (controller-mapping 1).
    /// </summary>
    public bool TakesComment { get; init; }

    /// <summary>
    /// True when the comment of the source block became a word of the written block, the NAME of Oxxxx (name)
    /// (controller-mapping 1).
    /// </summary>
    public bool UsesComment { get; init; }
}
