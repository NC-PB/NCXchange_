using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// What the write pass writes into while it walks: a program, or one walk of a subprogram, each with the target state
/// of its own, which starts unknown (virtual machine 3.9, D99).
/// </summary>
internal sealed class WriteContext
{
    /// <summary>
    /// What the target control has active while this part is written.
    /// </summary>
    public required TargetState Target { get; init; }

    /// <summary>
    /// The lines written so far.
    /// </summary>
    public required List<OutputLine> Lines { get; init; }

    /// <summary>
    /// The program written, or the program whose call this walk belongs to; null for the walk of a subprogram that no
    /// program calls (D99).
    /// </summary>
    public ProgramText? Program { get; init; }

    /// <summary>
    /// The subprogram of a walk; null for a program.
    /// </summary>
    public Section? Sub { get; init; }

    /// <summary>
    /// The CALL block of a walk; null for a program and for the walk of a subprogram that no program calls.
    /// </summary>
    public Block? Call { get; init; }

    /// <summary>
    /// Adds the lines of one block.
    /// </summary>
    public void Add(IReadOnlyList<string> lines, Block block)
    {
        foreach (string line in lines)
        {
            Lines.Add(new OutputLine(line, block));
        }
    }
}
