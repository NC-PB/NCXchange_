using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// A program or a subprogram of a source file as the structure pass finds it: its begin block and the blocks after it
/// up to the next section or the end of the file (language 4.13).
/// </summary>
internal sealed class SourceSection
{
    /// <summary>
    /// A program or a subprogram; when the source does not name it (Fanuc O), decided by the calls of the file and, for
    /// a section no block calls, by its end (StructurePass.DecideKinds).
    /// </summary>
    public SectionKind Kind { get; set; }

    /// <summary>
    /// True while the source has not named the section a program or a subprogram (StructureRole.SectionBegin).
    /// </summary>
    public bool Undecided { get; init; }

    /// <summary>
    /// The index of the begin block among the source blocks; null for the program of the blocks that stand before the
    /// first begin, such as a Fanuc file without an O line.
    /// </summary>
    public int? Begin { get; init; }

    /// <summary>
    /// The indices of the blocks with words after the begin block, in file order.
    /// </summary>
    public List<int> Blocks { get; } = [];
}
