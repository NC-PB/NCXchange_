using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The chain of transforms the reader wrote, in program order (language 4.2, D31). A Heidenhain cycle 7 replaces the
/// earlier cycle 7, and so do cycle 8, cycle 10 and a PLANE function the earlier one of their kind: the reader writes
/// the RESET of the replaced entry before the new word (language 4.2; controller-mapping 1, SHIFT). ORIGIN empties the
/// chain.
/// </summary>
internal sealed class HeidenhainChain
{
    private readonly List<HeidenhainChainEntry> _entries = [];

    /// <summary>
    /// The entries in program order.
    /// </summary>
    public IReadOnlyList<HeidenhainChainEntry> Entries => _entries;

    /// <summary>
    /// False where the reader does not know the chain: in a subprogram whose callers leave different chains, after a
    /// call of a subprogram that changes it, after a transform kept as RAW (virtual machine 3.9).
    /// </summary>
    public bool Known { get; private set; } = true;

    /// <summary>
    /// Empties the chain, as ORIGIN does (language 4.2).
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        Known = true;
    }

    /// <summary>
    /// Forgets the chain: the reader no longer knows its entries.
    /// </summary>
    public void Forget()
    {
        _entries.Clear();
        Known = false;
    }

    /// <summary>
    /// Takes the entries of another chain, the one every caller of a subprogram leaves (virtual machine 3.9).
    /// </summary>
    /// <param name="entries">The entries.</param>
    public void Restore(IReadOnlyList<HeidenhainChainEntry> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        Known = true;
    }

    /// <summary>
    /// The chain as text, to tell whether two callers leave the same chain; null while it is not known.
    /// </summary>
    public string? ToText()
    {
        if (!Known)
        {
            return null;
        }

        var entries = new List<string>();
        foreach (HeidenhainChainEntry entry in _entries)
        {
            entries.Add(entry.Block.ToText());
        }

        return string.Join(" | ", entries);
    }

    /// <summary>
    /// The kind of the tilt the chain holds, TILT or TILT_AXIS; null when it holds none.
    /// </summary>
    public string? TiltKind()
    {
        foreach (HeidenhainChainEntry entry in _entries)
        {
            if (entry.Kind is "TILT" or "TILT_AXIS")
            {
                return entry.Kind;
            }
        }

        return null;
    }

    // A new transform of a kind replaces the earlier one of that kind on the control (controllers heidenhain.md 3). The
    // reader writes the RESET of the earlier entry and the new entry, and only where the RESET cuts that one entry, the
    // last of the chain, so that it removes nothing else and means the same under both readings of SHIFT=RESET, the
    // shifts or the last shift (wave-1 question #95; language 4.2, virtual machine 2.1).
    // TODO(question): heidenhain 3 does not say where a new cycle 7, 8, 10 or PLANE stands against the transforms that
    // were programmed after the one it replaces (a cycle 10 after the cycle 7 that a new cycle 7 replaces), in the
    // place of the old one or at the end of the chain (language 4.2, D31); the reader keeps such a block RAW.

    /// <summary>
    /// Replaces the entry of a kind with a new one, or removes it: writes the blocks of the change in order.
    /// </summary>
    /// <param name="kind">SHIFT, MIRROR, ROTATE, TILT or TILT_AXIS; TILT and TILT_AXIS replace each other.</param>
    /// <param name="next">The new entry; null to remove the entry of the kind.</param>
    /// <param name="blocks">Where the NCX blocks of the change go, in order.</param>
    /// <returns>False when the change cannot be written without removing other entries, or the chain is not
    /// known.</returns>
    public bool TryReplace(string kind, HeidenhainChainEntry? next, List<HeidenhainDraftBlock> blocks)
    {
        if (!Known)
        {
            return false;
        }

        int found = -1;
        int count = 0;
        for (int index = 0; index < _entries.Count; index++)
        {
            if (SameKind(_entries[index].Kind, kind))
            {
                found = index;
                count++;
            }
        }

        if (count > 1 || (count == 1 && found != _entries.Count - 1))
        {
            return false;
        }

        if (count == 1)
        {
            blocks.Add(ResetOf(_entries[found].Kind));
            _entries.RemoveAt(found);
        }

        if (next is not null)
        {
            blocks.Add(next.Block);
            _entries.Add(next);
        }

        return true;
    }

    /// <summary>
    /// The block that removes the last entry of a kind: SHIFT=RESET, MIRROR=OFF, ROTATE=RESET, TILT=RESET,
    /// TILT_AXIS=RESET (language 4.2; wave-1 question #101 for MIRROR=OFF).
    /// </summary>
    /// <param name="kind">The kind of the entry.</param>
    public static HeidenhainDraftBlock ResetOf(string kind)
    {
        return new HeidenhainDraftBlock().Add(kind, new IdentValue(kind == "MIRROR" ? "OFF" : "RESET"));
    }

    // TILT and TILT_AXIS are one tilted plane, which a new PLANE or cycle 19 replaces (controllers heidenhain.md 3).
    private static bool SameKind(string first, string second)
    {
        return first == second || (first is "TILT" or "TILT_AXIS" && second is "TILT" or "TILT_AXIS");
    }
}
