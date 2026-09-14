using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The frame chain written in program order on every target (D31, language 4.2): what a block removed from the chain
/// is cancelled from the end, then what it appended is written in the order the program wrote it, so that the
/// mathematics of the frame stays the same on every machine. A compiler of a family says how its controller cancels and
/// writes one entry; the order is the same for all of them.
/// </summary>
public static class ChainWriter
{
    /// <summary>
    /// What a block did to the chain, from the chain before and after it.
    /// </summary>
    /// <param name="before">The frame before the block.</param>
    /// <param name="after">The frame after the block.</param>
    public static ChainChange Between(FrameSnapshot before, FrameSnapshot after)
    {
        // The chain grows at its end and is cut from its end, and an entry never changes (language 4.2, virtual
        // machine 2.1): the entries both chains share stand at their start, the same entry in both.
        int kept = 0;
        while (kept < before.Chain.Count && kept < after.Chain.Count
            && ReferenceEquals(before.Chain[kept], after.Chain[kept]))
        {
            kept++;
        }

        // What was cut goes from the end, the last entry first (language 4.2: unwound from the end only).
        var removed = new List<TransformEntry>();
        for (int index = before.Chain.Count - 1; index >= kept; index--)
        {
            removed.Add(before.Chain[index]);
        }

        var appended = new List<TransformEntry>();
        for (int index = kept; index < after.Chain.Count; index++)
        {
            appended.Add(after.Chain[index]);
        }

        return new ChainChange { Removed = removed, Appended = appended };
    }

    /// <summary>
    /// Writes what a block did to the chain in the order of D31: every removed entry cancelled, the last first, then
    /// every appended entry in program order.
    /// </summary>
    /// <param name="before">The frame before the block.</param>
    /// <param name="after">The frame after the block.</param>
    /// <param name="writeCancel">Cancels one entry in the controller's form: G69, CYCL DEF 7 with 0.</param>
    /// <param name="writeEntry">Writes one entry in the controller's form: G52 X60., CYCL DEF 10 ROT+30.</param>
    public static void Write(FrameSnapshot before, FrameSnapshot after, Action<TransformEntry> writeCancel,
        Action<TransformEntry> writeEntry)
    {
        ChainChange change = Between(before, after);
        foreach (TransformEntry entry in change.Removed)
        {
            writeCancel(entry);
        }

        foreach (TransformEntry entry in change.Appended)
        {
            writeEntry(entry);
        }
    }
}
