using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The NCX blocks one SINUMERIK block reads into: the blocks before its main block (the RESET of a cut chain, the
/// RETRACT of a CYCLE800, the label of a structure), its main block, and the blocks after it (the calls of a pattern,
/// the corner of a chamfer); the words NCX has no meaning for, kept as a RAW:SIEMENS word; or the reason the whole
/// block is kept as RAW (D5).
/// </summary>
internal sealed class SiemensDraft
{
    /// <summary>
    /// The blocks before the main block, in order.
    /// </summary>
    public List<SiemensDraftBlock> Before { get; } = [];

    /// <summary>
    /// The block that carries the words of the source block.
    /// </summary>
    public SiemensDraftBlock Main { get; } = new();

    /// <summary>
    /// The blocks after the main block, in order.
    /// </summary>
    public List<SiemensDraftBlock> After { get; } = [];

    /// <summary>
    /// The words of the block that NCX has no meaning for, as the source writes them, kept as one RAW:SIEMENS word of
    /// the main block (controllers siemens.md 2, 11 rule 8).
    /// </summary>
    public List<string> RawWords { get; } = [];

    /// <summary>
    /// Why the whole block is kept as RAW; null while it reads into NCX words.
    /// </summary>
    public string? RawReason { get; private set; }

    /// <summary>
    /// True when the whole block is kept as RAW.
    /// </summary>
    public bool IsRaw => RawReason is not null;

    /// <summary>
    /// True for a block kept as RAW that changes nothing the reader follows, a message or a stop of the block
    /// preparation, so that the reader keeps what it knows after it.
    /// </summary>
    public bool RawKeepsState { get; set; }

    /// <summary>
    /// A comment-only line the block is kept as, "; DEFINE M_ON AS M3": a line whose meaning the reader has folded into
    /// the blocks after it (D92); null for none.
    /// </summary>
    public string? CommentLine { get; set; }

    /// <summary>
    /// The WARNINGs that say what the NCX blocks hold; the reader reports them only when it writes the blocks (D98).
    /// </summary>
    public List<SiemensWarning> Warnings { get; } = [];

    /// <summary>
    /// Keeps the whole block as RAW, verbatim with a WARNING (D5, controller-mapping 9); the first reason stands.
    /// </summary>
    /// <param name="reason">Why, in the words of the machine.</param>
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

        foreach (SiemensDraftBlock block in After)
        {
            if (block.Verb is null && !block.Has(key, addr))
            {
                block.Add(key, addr, value);
                return;
            }
        }

        After.Add(new SiemensDraftBlock().Add(key, addr, value));
    }

    /// <summary>
    /// True while the blocks hold no NCX word, only words kept as RAW or nothing.
    /// </summary>
    public bool HoldsNoWord()
    {
        foreach (SiemensDraftBlock block in InOrder())
        {
            if (!block.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The blocks in the order they are written.
    /// </summary>
    public List<SiemensDraftBlock> InOrder()
    {
        var blocks = new List<SiemensDraftBlock>(Before) { Main };
        blocks.AddRange(After);
        return blocks;
    }
}
