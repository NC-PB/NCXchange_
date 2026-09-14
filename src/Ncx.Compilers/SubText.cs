using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The written text of one subprogram: the lines of its first walk, which stand for every walk, since each SUB section
/// is written once, not once per CALL (virtual machine 3.9, D99).
/// </summary>
internal sealed class SubText
{
    /// <summary>
    /// The subprogram section.
    /// </summary>
    public required Section Sub { get; init; }

    /// <summary>
    /// The lines of its first walk, SUB=BEGIN to SUB=END.
    /// </summary>
    public required List<OutputLine> Lines { get; init; }

    /// <summary>
    /// The CALL block of its first walk; null for a subprogram that no program calls.
    /// </summary>
    public Block? FirstCall { get; init; }
}
