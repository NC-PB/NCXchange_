using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The written text of one program: its lines, where its head ends, its footer, the subprograms it calls and the tools
/// the tool table left without data (D10, D48, D99).
/// </summary>
internal sealed class ProgramText
{
    /// <summary>
    /// The program section.
    /// </summary>
    public required Section Program { get; init; }

    /// <summary>
    /// The step of its PROGRAM=BEGIN block.
    /// </summary>
    public required BlockStep Begin { get; init; }

    /// <summary>
    /// The lines of its blocks, PROGRAM=BEGIN to PROGRAM=END.
    /// </summary>
    public List<OutputLine> Lines { get; } = [];

    /// <summary>
    /// The number of lines of its head, the header and the lines of PROGRAM=BEGIN, after which the warning block of
    /// D10 stands.
    /// </summary>
    public int HeadEnd { get; set; }

    /// <summary>
    /// The lines of WriteFooter, which follow the subprograms placed after the program.
    /// </summary>
    public List<OutputLine> Footer { get; } = [];

    /// <summary>
    /// The subprograms its walk entered, directly or through another subprogram, in the order of their first call.
    /// </summary>
    public List<Section> CalledSubs { get; } = [];

    /// <summary>
    /// The tools whose {kind} took the default because the tool table does not describe them (D10).
    /// </summary>
    public List<ToolRef> ToolsWithoutData { get; } = [];
}
