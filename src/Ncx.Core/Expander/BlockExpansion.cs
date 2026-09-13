using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// One block of the file while the expander works on it: the generated blocks before it, the block itself as the
/// rewriters and the limits left it, and the generated blocks after it (architecture 5.5). The blocks an expansion puts
/// around it must stand inside the program or subprogram of the block (language 4.13).
/// </summary>
internal sealed class BlockExpansion
{
    public BlockExpansion(Block origin, Section? section, int index)
    {
        Origin = origin;
        Block = origin;
        Section = section;

        // A block before a BEGIN block or after an END block would stand outside every program and subprogram, and so
        // would one around a block outside every section, FILE=BEGIN and FILE=END (language 4.1, 4.13).
        CanInsertBefore = section is not null && index != section.FirstBlock;
        CanInsertAfter = section is not null && index != section.LastBlock;
    }

    /// <summary>
    /// The block of the file, the origin of every block generated for it.
    /// </summary>
    public Block Origin { get; }

    /// <summary>
    /// The block itself: the block of the file, or the block a rewriter or the limits rewrote in its place.
    /// </summary>
    public Block Block { get; set; }

    /// <summary>
    /// The program or subprogram the block stands in; null for FILE=BEGIN and FILE=END.
    /// </summary>
    public Section? Section { get; }

    /// <summary>
    /// The generated blocks before the block, in their order.
    /// </summary>
    public List<Block> Before { get; } = [];

    /// <summary>
    /// The generated blocks after the block, in their order.
    /// </summary>
    public List<Block> After { get; } = [];

    /// <summary>
    /// True when a block may stand before the block: inside its section and not before its BEGIN block.
    /// </summary>
    public bool CanInsertBefore { get; }

    /// <summary>
    /// True when a block may stand after the block: inside its section and not after its END block.
    /// </summary>
    public bool CanInsertAfter { get; }

    /// <summary>
    /// Puts generated blocks around the block, inside those already there: the blocks of the first rule stand
    /// outermost, those of the rewriters inside the rules, next to the block (architecture 5.5). False, and nothing is
    /// put, when a block would stand outside every section (language 4.13).
    /// </summary>
    public bool Surround(IReadOnlyList<Block> before, IReadOnlyList<Block> after)
    {
        if ((before.Count > 0 && !CanInsertBefore) || (after.Count > 0 && !CanInsertAfter))
        {
            return false;
        }

        Before.AddRange(before);
        After.InsertRange(0, after);
        return true;
    }

    /// <summary>
    /// Puts a rewritten block in place of the block. False, and the block stays, for a block that opens or closes the
    /// file, a program or a subprogram (language 4.1, 4.13).
    /// </summary>
    public bool Replace(Block rewritten)
    {
        if (!CanInsertBefore || !CanInsertAfter)
        {
            return false;
        }

        Block = rewritten;
        return true;
    }
}
