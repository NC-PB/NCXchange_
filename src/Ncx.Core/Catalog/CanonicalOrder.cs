using Ncx.Core.Model;

namespace Ncx.Core.Catalog;

/// <summary>
/// The canonical order of the words of a block, language 5 rule 6 with the rank table of D90: mandatory for ncx format
/// and for every writer, free for hand-written files. A word of lower rank stands first, and the rank comes from the
/// catalog alone, never from a machine file (D93).
/// </summary>
internal static class CanonicalOrder
{
    /// <summary>
    /// The words of a block in canonical order. Words that the rule does not order against each other keep the order
    /// in which they were written, which is the rule for the native parameters of a CYCLE:controller=n block
    /// (language 5 rule 6, D94).
    /// </summary>
    /// <param name="block">The block; its words stay as they are.</param>
    public static IReadOnlyList<Word> Sort(Block block)
    {
        // Each word goes behind every word already placed that does not come after it, so that words which compare
        // equal keep their source order.
        var sorted = new List<Word>();
        foreach (Word word in block.Words)
        {
            int position = sorted.Count;
            while (position > 0 && Compare(sorted[position - 1], word, block) > 0)
            {
                position--;
            }

            sorted.Insert(position, word);
        }

        return sorted;
    }

    /// <summary>
    /// Compares two words of a block in canonical order: by rank; machine axes of one rank by letter and then by
    /// number; words of one key by the address text; zero for words the rule leaves in source order.
    /// </summary>
    /// <param name="first">A word of the block.</param>
    /// <param name="second">Another word of the block.</param>
    /// <param name="block">The block; with CYCLE:controller=n an unknown key in it is a native parameter.</param>
    public static int Compare(Word first, Word second, Block block)
    {
        int firstRank = RankOf(first, block);
        int byRank = firstRank.CompareTo(RankOf(second, block));
        if (byRank != 0)
        {
            return byRank;
        }

        // Machine axes sort alphabetically by letter, then by number: C2 before W before Z2 (D90 bucket 3, D93).
        bool machineAxes = firstRank is CanonicalRanks.MachineAxis or CanonicalRanks.IncrementalMachineAxis;
        if (machineAxes
            && WordCatalog.TryMachineAxis(first.Key, out MachineAxisWord? firstAxis)
            && WordCatalog.TryMachineAxis(second.Key, out MachineAxisWord? secondAxis))
        {
            return CompareMachineAxes(firstAxis, secondAxis);
        }

        // Words of one key with several addresses sort by the address text: COOLANT:AIR before COOLANT:THROUGH,
        // VAR:Q1 before VAR:Q3 (language 5 rule 6).
        if (first.Key == second.Key)
        {
            return string.CompareOrdinal(first.Addr ?? "", second.Addr ?? "");
        }

        return 0;
    }

    /// <summary>
    /// The rank of a word in its block (language 5 rule 6, D90).
    /// </summary>
    /// <param name="word">A word of the block.</param>
    /// <param name="block">The block; with CYCLE:controller=n an unknown key in it is a native parameter.</param>
    public static int RankOf(Word word, Block block)
    {
        WordDefinition? definition = word.Definition ?? WordCatalog.Lookup(word.Key);
        if (definition is not null)
        {
            return RankOfEntry(word, definition);
        }

        // In a block that carries CYCLE:controller=n every key the catalog does not know is a native parameter, one of
        // the machine-axis form included, ranked after the cycle words (language 4.7.1, 5 rule 6, D94).
        if (WordCatalog.IsNativeParameterAllowed(block))
        {
            return CanonicalRanks.NativeParameter;
        }

        // A machine axis word ranks behind X Y Z A B C, its incremental form behind IX to IC (D90 bucket 3, D93).
        if (WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis))
        {
            return machineAxis.IsIncremental ? CanonicalRanks.IncrementalMachineAxis : CanonicalRanks.MachineAxis;
        }

        // Any other key the catalog does not know is an ERROR of the parser, and its block keeps its source text
        // (architecture 4.1); it keeps its place in source order among the words the catalog does not know.
        return CanonicalRanks.NativeParameter;
    }

    // The entry gives the rank: the =RESET form of SHIFT, TILT and TILT_AXIS stands with the frame words, an address of
    // a fixed set has a rank of its own (OFFSET:LEN, CENTER:IX, TOLERANCE:ROTARY), and any other address sorts under
    // the rank of its word (language 5 rule 6, D90).
    private static int RankOfEntry(Word word, WordDefinition definition)
    {
        if (definition.ResetRank is int resetRank && WordCatalog.IsResetForm(word, definition))
        {
            return resetRank;
        }

        if (word.Addr is not null && definition.AddrRanks.TryGetValue(word.Addr, out int addrRank))
        {
            return addrRank;
        }

        return definition.CanonicalRank;
    }

    // By letter, then by number, an axis name of one letter before one with a number; the key text decides between
    // Z2 and Z02.
    private static int CompareMachineAxes(MachineAxisWord first, MachineAxisWord second)
    {
        int byLetter = first.Letter.CompareTo(second.Letter);
        if (byLetter != 0)
        {
            return byLetter;
        }

        int byNumber = (first.Number ?? -1).CompareTo(second.Number ?? -1);
        if (byNumber != 0)
        {
            return byNumber;
        }

        return string.CompareOrdinal(first.Key, second.Key);
    }
}
