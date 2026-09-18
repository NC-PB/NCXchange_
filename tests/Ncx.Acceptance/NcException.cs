namespace Ncx.Acceptance;

/// <summary>
/// A difference between a source program and a compiled program that the documents cannot settle, grounded in its
/// question and never a silent one (phase 3, comparison rules; <see cref="NcComparer"/>): the lines of the source it
/// stands for and the lines the compiled program writes instead, both as NC text that the comparison normalizes like
/// every other line.
/// </summary>
internal sealed record NcException
{
    /// <summary>
    /// The question or the rule the difference is grounded in: "D163: Q213 of CHIP_BREAK".
    /// </summary>
    public required string Grounds { get; init; }

    /// <summary>
    /// The lines of the source the exception stands for, in order; empty for lines only the compiled program has.
    /// </summary>
    public IReadOnlyList<string> Source { get; init; } = [];

    /// <summary>
    /// The lines the compiled program writes instead, in order; empty for lines only the source has.
    /// </summary>
    public IReadOnlyList<string> Compiled { get; init; } = [];
}
