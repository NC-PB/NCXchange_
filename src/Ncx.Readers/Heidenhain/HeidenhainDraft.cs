using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The NCX blocks one Klartext block reads into: its main block with the verb and the words of the block, the blocks
/// that stand before it (the RESET of a replaced transform, the RETRACT of a PLANE, a cycle written again before its
/// call) and those after it (the calls of a pattern, a second word of one key); or the reason the block is kept as RAW
/// (D5).
/// </summary>
internal sealed class HeidenhainDraft
{
    /// <summary>
    /// The blocks before the main block, in order.
    /// </summary>
    public List<HeidenhainDraftBlock> Before { get; } = [];

    /// <summary>
    /// The block that carries the words of the source block.
    /// </summary>
    public HeidenhainDraftBlock Main { get; } = new();

    /// <summary>
    /// The blocks after the main block, in order.
    /// </summary>
    public List<HeidenhainDraftBlock> After { get; } = [];

    /// <summary>
    /// Why the block is kept as RAW; null while it reads into NCX words.
    /// </summary>
    public string? RawReason { get; private set; }

    /// <summary>
    /// True when the block is kept as RAW.
    /// </summary>
    public bool IsRaw => RawReason is not null;

    /// <summary>
    /// The WARNINGs that say what the NCX blocks hold; the reader reports them only when it writes the blocks, not when
    /// it keeps the block as RAW (D98).
    /// </summary>
    public List<HeidenhainWarning> Warnings { get; } = [];

    /// <summary>
    /// Keeps the whole block as RAW, verbatim with a WARNING (D5, controller-mapping 9); the first reason stands.
    /// </summary>
    /// <param name="reason">Why, in the words of the machine: "BLK FORM describes the blank for the graphic".</param>
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

        foreach (HeidenhainDraftBlock block in After)
        {
            if (block.Verb is null && !block.Has(key, addr))
            {
                block.Add(key, addr, value);
                return;
            }
        }

        After.Add(new HeidenhainDraftBlock().Add(key, addr, value));
    }

    /// <summary>
    /// The blocks in the order they are written.
    /// </summary>
    public List<HeidenhainDraftBlock> InOrder()
    {
        var blocks = new List<HeidenhainDraftBlock>(Before) { Main };
        blocks.AddRange(After);
        return blocks;
    }
}
