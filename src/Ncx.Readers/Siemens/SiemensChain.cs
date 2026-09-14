using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The chain of transforms the reader wrote, in program order (language 4.2, D31). ATRANS, AROT, AMIRROR and AROTS are
/// appended as written; TRANS, ROT, MIRROR, SCALE and ROTS delete the whole programmable frame before they act, so the
/// reader cuts the chain at its first entry and then appends (controllers siemens.md 4, 11 rule 4; controller-mapping
/// 1, SHIFT). ORIGIN empties the chain.
/// </summary>
internal sealed class SiemensChain
{
    private readonly List<SiemensChainEntry> _entries = [];

    /// <summary>
    /// The entries in program order.
    /// </summary>
    public IReadOnlyList<SiemensChainEntry> Entries => _entries;

    /// <summary>
    /// False where the reader does not know the chain: in a subprogram whose callers leave different chains, after a
    /// call of a subprogram that changes it, after a transform kept as RAW (virtual machine 3.9).
    /// </summary>
    public bool Known { get; private set; } = true;

    /// <summary>
    /// The block that removes the last entry of a kind: SHIFT=RESET, MIRROR=OFF, ROTATE=RESET, TILT=RESET,
    /// TILT_AXIS=RESET (language 4.2; D124 for MIRROR=OFF).
    /// </summary>
    /// <param name="kind">The kind of the entry.</param>
    public static SiemensDraftBlock ResetOf(string kind)
    {
        return new SiemensDraftBlock().Add(kind, new IdentValue(kind == "MIRROR" ? "OFF" : "RESET"));
    }

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
    /// Takes the entries and the knowledge of another chain.
    /// </summary>
    /// <param name="other">The chain to copy.</param>
    public void CopyFrom(SiemensChain other)
    {
        _entries.Clear();
        _entries.AddRange(other._entries);
        Known = other.Known;
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
        foreach (SiemensChainEntry entry in _entries)
        {
            entries.Add(entry.Instruction + ":" + entry.Block.ToText());
        }

        return string.Join(" | ", entries);
    }

    /// <summary>
    /// The tilt the chain holds, TILT or TILT_AXIS of CYCLE800, ROTS or AROTS; null when it holds none.
    /// </summary>
    public SiemensChainEntry? Tilt()
    {
        SiemensChainEntry? found = null;
        foreach (SiemensChainEntry entry in _entries)
        {
            if (entry.Kind is "TILT" or "TILT_AXIS")
            {
                found = entry;
            }
        }

        return found;
    }

    /// <summary>
    /// Removes the tilts of CYCLE800 where they are the last entries of the chain, as a new swivel or CYCLE800()
    /// replaces the whole swivel (controllers siemens.md 4): one RESET per tilt, the last first, since a RESET removes
    /// the last entry of its kind and everything after it (language 4.2, D31).
    /// </summary>
    /// <param name="blocks">Where the RESET blocks go, in order.</param>
    /// <returns>False when the chain is not known, or an entry of another instruction follows the first tilt of
    /// CYCLE800.</returns>
    public bool TryRemoveSwivel(List<SiemensDraftBlock> blocks)
    {
        if (!Known)
        {
            return false;
        }

        int first = _entries.FindIndex(entry => entry.Instruction == "CYCLE800");
        if (first < 0)
        {
            return true;
        }

        for (int index = first; index < _entries.Count; index++)
        {
            if (_entries[index].Instruction != "CYCLE800")
            {
                return false;
            }
        }

        while (_entries.Count > first)
        {
            blocks.Add(ResetOf(_entries[^1].Kind));
            _entries.RemoveAt(_entries.Count - 1);
        }

        return true;
    }

    /// <summary>
    /// Appends an entry (language 4.2, D31): writes its block.
    /// </summary>
    /// <param name="entry">The new entry.</param>
    /// <param name="blocks">Where the NCX blocks of the change go, in order.</param>
    public void Append(SiemensChainEntry entry, List<SiemensDraftBlock> blocks)
    {
        _entries.Add(entry);
        blocks.Add(entry.Block);
    }

    // TODO(question): siemens 4 says a replacing instruction deletes the earlier programmable frame instructions, and
    // CYCLE800 is none of them, while controller-mapping 1 (SHIFT) and siemens 11 rule 4 cut the NCX chain at its first
    // entry, which removes a tilt of CYCLE800 too; where the cut would remove such a tilt, the block stays RAW.

    /// <summary>
    /// Cuts the chain at its first entry, as a replacing frame instruction deletes the whole programmable frame
    /// (controllers siemens.md 4, 11 rule 4; controller-mapping 1, SHIFT): the RESET of the kind of the first entry
    /// removes the last entry of that kind and everything after it, so the reader writes it until the chain is empty,
    /// one block per kind where the kinds do not repeat.
    /// </summary>
    /// <param name="blocks">Where the RESET blocks go, in order.</param>
    /// <returns>False when the chain is not known, or the cut would remove a tilt of CYCLE800.</returns>
    public bool TryCut(List<SiemensDraftBlock> blocks)
    {
        if (!Known)
        {
            return false;
        }

        foreach (SiemensChainEntry entry in _entries)
        {
            if (entry.Instruction == "CYCLE800")
            {
                return false;
            }
        }

        while (_entries.Count > 0)
        {
            string kind = _entries[0].Kind;
            int last = _entries.FindLastIndex(entry => SameKind(entry.Kind, kind));
            blocks.Add(ResetOf(_entries[last].Kind));
            _entries.RemoveRange(last, _entries.Count - last);
        }

        return true;
    }

    /// <summary>
    /// Replaces the entry an instruction wrote with a new one, or removes it, where the RESET removes that one entry,
    /// the last of the chain (language 4.2, D31): CYCLE800 that swivels anew, G58 and G59 that set a part of the shift.
    /// </summary>
    /// <param name="old">The entry to replace; null to append.</param>
    /// <param name="next">The new entry; null to remove the old one.</param>
    /// <param name="blocks">Where the NCX blocks of the change go, in order.</param>
    /// <returns>False when the change cannot be written without removing other entries, or the chain is not
    /// known.</returns>
    public bool TryReplace(SiemensChainEntry? old, SiemensChainEntry? next, List<SiemensDraftBlock> blocks)
    {
        // The RESET of a kind cuts at the last entry of that kind (virtual machine 2.1), which is the old entry
        // where it is the last of the chain.
        if (!Known || (old is not null && (_entries.Count == 0 || !ReferenceEquals(_entries[^1], old))))
        {
            return false;
        }

        if (old is not null)
        {
            blocks.Add(ResetOf(old.Kind));
            _entries.RemoveAt(_entries.Count - 1);
        }

        if (next is not null)
        {
            Append(next, blocks);
        }

        return true;
    }

    /// <summary>
    /// The last entry that an instruction wrote; null when the chain holds none.
    /// </summary>
    /// <param name="instruction">The instruction, "TRANS".</param>
    public SiemensChainEntry? LastOf(string instruction)
    {
        return _entries.FindLast(entry => entry.Instruction == instruction);
    }

    private static bool SameKind(string first, string second)
    {
        return first == second;
    }
}
