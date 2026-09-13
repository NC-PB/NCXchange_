using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The NCX blocks one Fanuc source block reads into: its main block with the words and the motion of the block, the
/// blocks that stand before it (the SHIFT=RESET of a G52, the head of a WHILE loop, the definition of a cycle) and
/// those after it (the calls of a K repeat, the second part of a chamfer); or the reason the block is kept as RAW (D5).
/// </summary>
internal sealed class FanucDraft
{
    /// <summary>
    /// The blocks before the main block, in order.
    /// </summary>
    public List<DraftBlock> Before { get; } = [];

    /// <summary>
    /// The block that carries the words of the source block.
    /// </summary>
    public DraftBlock Main { get; } = new();

    /// <summary>
    /// The blocks after the main block, in order.
    /// </summary>
    public List<DraftBlock> After { get; } = [];

    /// <summary>
    /// Why the block is kept as RAW; null while it reads into NCX words.
    /// </summary>
    public string? RawReason { get; private set; }

    /// <summary>
    /// True when the block is kept as RAW.
    /// </summary>
    public bool IsRaw => RawReason is not null;

    /// <summary>
    /// The WARNINGs that say what the NCX blocks hold, the MFUNC of an M code no table names among them (D98): the
    /// reader reports them only when it writes the blocks, not when it keeps the block as RAW.
    /// </summary>
    public List<(string Code, string Message)> Warnings { get; } = [];

    /// <summary>
    /// Keeps the whole block as RAW, verbatim with a WARNING (D5, controller-mapping 9); the first reason stands.
    /// </summary>
    /// <param name="reason">Why, in the words of the machine: "G10 writes the datum table".</param>
    public void KeepAsRaw(string reason)
    {
        RawReason ??= reason;
    }

    /// <summary>
    /// Adds a state word to the main block. A key stands once per block (language 5 rule 4), so a second word of the
    /// same key and address, M8 M9 in one block, goes into a block of its own after the main block, in source order.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="addr">The address; null for none.</param>
    /// <param name="value">The value.</param>
    public void AddState(string key, string? addr, Value value)
    {
        if (!Main.Has(key, addr))
        {
            Main.Add(key, addr, value);
            return;
        }

        foreach (DraftBlock block in After)
        {
            if (block.Verb is null && !block.Has(key, addr))
            {
                block.Add(key, addr, value);
                return;
            }
        }

        After.Add(new DraftBlock().Add(key, addr, value));
    }

    /// <summary>
    /// The blocks in the order they are written.
    /// </summary>
    public List<DraftBlock> InOrder()
    {
        var blocks = new List<DraftBlock>(Before) { Main };
        blocks.AddRange(After);
        return blocks;
    }
}
