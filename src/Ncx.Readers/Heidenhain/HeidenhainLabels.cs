namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The labels of a Klartext file in two passes (controllers heidenhain.md 1, 7 rule 3; controller-mapping 6): the first
/// collects the calls, the repeats and the jumps, the second decides every LBL. An LBL n closed by LBL 0 and called
/// without REP is a subprogram, a SUB section of the file; an LBL used by REP or as the target of FN 9 to FN 12 is a
/// LABEL; an FN jump to an LBL that the end of the program follows is JUMP=END.
/// </summary>
internal sealed class HeidenhainLabels
{
    /// <summary>
    /// The name of every subprogram by the line of its LBL block.
    /// </summary>
    public Dictionary<int, string> SubBegins { get; } = [];

    /// <summary>
    /// The lines of the LBL 0 blocks that close a subprogram.
    /// </summary>
    public HashSet<int> SubEnds { get; } = [];

    /// <summary>
    /// The names of the subprograms.
    /// </summary>
    public HashSet<string> Subs { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The places among the blocks with words of the first and the last block of every subprogram, by name.
    /// </summary>
    public Dictionary<string, int> SubFirst { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The place of the LBL 0 of every subprogram, by name.
    /// </summary>
    public Dictionary<string, int> SubLast { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The labels a REP or an FN jump uses, each a LABEL.
    /// </summary>
    public HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of the FN jumps whose label the end of the program follows: JUMP=END (controller-mapping 1).
    /// </summary>
    public HashSet<int> JumpsToTheEnd { get; } = [];

    /// <summary>
    /// The labels that the end of the program follows and that only JUMP=END jumps reach: their LBL writes nothing, the
    /// jumps continue at PROGRAM=END (controller-mapping 1, JUMP=END).
    /// </summary>
    public HashSet<string> EndTargets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The label a block names after its keyword LBL: "1" of LBL 1 and of CALL LBL 1 REP 3, "NAME" of LBL "NAME"; null
    /// when it names none.
    /// </summary>
    /// <param name="block">A block with the word LBL.</param>
    public static string? LabelOf(SourceBlock block)
    {
        for (int index = 0; index < block.Words.Count; index++)
        {
            SourceWord word = block.Words[index];
            if (word.Address != "LBL")
            {
                continue;
            }

            string text = word.Text.Length > 0 || index + 1 >= block.Words.Count
                ? word.Text
                : block.Words[index + 1].Text;
            text = text.Trim('"').Trim();
            if (text.Length == 0)
            {
                return null;
            }

            // A number label without its leading zeros, LBL 01 is LBL 1; a name as written.
            bool digits = true;
            foreach (char character in text)
            {
                digits &= char.IsAsciiDigit(character);
            }

            if (!digits)
            {
                return text;
            }

            string number = text.TrimStart('0');
            return number.Length == 0 ? "0" : number;
        }

        return null;
    }

    /// <summary>
    /// Tells whether a block is a label, LBL n or LBL 0, and not a call of one.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsLabel(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address == "LBL";
    }

    /// <summary>
    /// Tells whether a block is CALL LBL, with or without REP.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsLabelCall(SourceBlock block)
    {
        return block.Words.Count > 1 && block.Words[0].Address == "CALL" && block.Words[1].Address == "LBL";
    }

    /// <summary>
    /// Tells whether a block repeats a program part, CALL LBL n REP k.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsRepeat(SourceBlock block)
    {
        return IsLabelCall(block) && block.Find("REP") is not null;
    }

    /// <summary>
    /// Tells whether a block is a conditional jump, FN 9 to FN 12 ... GOTO LBL n (controllers heidenhain.md 6).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsJump(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address == "FN" && block.Words[0].Number is >= 9 and <= 12
            && block.Find("GOTO") is not null;
    }

    /// <summary>
    /// Tells whether a block ends the program, M30 or M2 (controllers heidenhain.md 1).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool Ends(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M30" or "M2")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Classifies the labels of the blocks with words of a file.
    /// </summary>
    /// <param name="blocks">The blocks with words, in file order.</param>
    public static HeidenhainLabels Classify(IReadOnlyList<SourceBlock> blocks)
    {
        // First pass: the end of the program, the calls, the repeats, the jumps and where every label stands.
        var labels = new HeidenhainLabels();
        var called = new HashSet<string>(StringComparer.Ordinal);
        var jumps = new List<int>();
        int mainEnd = -1;
        for (int index = 0; index < blocks.Count; index++)
        {
            SourceBlock block = blocks[index];
            string? label = LabelOf(block);
            if (mainEnd < 0 && Ends(block) && !block.BlockSkip)
            {
                mainEnd = index;
            }

            if (IsRepeat(block) && label is not null)
            {
                labels.Targets.Add(label);
            }
            else if (IsLabelCall(block) && label is not null)
            {
                called.Add(label);
            }
            else if (IsJump(block) && label is not null)
            {
                jumps.Add(index);
            }
        }

        labels.DecideJumps(blocks, jumps);
        labels.DecideSubs(blocks, called, mainEnd);
        return labels;
    }

    // An FN jump to an LBL that the end of the program follows directly is JUMP=END, which needs no LABEL; every other
    // FN jump uses its label (controller-mapping 1, JUMP=END; language 4.9).
    private void DecideJumps(IReadOnlyList<SourceBlock> blocks, List<int> jumps)
    {
        foreach (int jump in jumps)
        {
            string label = LabelOf(blocks[jump])!;
            int target = FindLabel(blocks, label);
            bool endFollows = target >= 0 && target + 1 < blocks.Count && Ends(blocks[target + 1])
                && !blocks[target + 1].BlockSkip;
            if (endFollows)
            {
                JumpsToTheEnd.Add(blocks[jump].Line);
                EndTargets.Add(label);
            }
            else
            {
                Targets.Add(label);
            }
        }
    }

    // Subprograms are LBL n ... LBL 0 sections after the M30 inside the program (controllers heidenhain.md 1); an LBL n
    // is one when a CALL LBL n without REP calls it, no REP or FN jump uses it as a label, and an LBL 0 closes it
    // (heidenhain 7 rule 3). A section inside another subprogram is none.
    // TODO(question): heidenhain 7 rule 3 does not say what an LBL section is that CALL LBL calls and that stands
    // before the M30 of its program, or in a program without M30 (the main flow runs through it as well), nor what an
    // LBL is that is called and also used by REP or an FN jump; such an LBL is no subprogram, and the CALL LBL of it is
    // kept RAW.
    private void DecideSubs(IReadOnlyList<SourceBlock> blocks, HashSet<string> called, int mainEnd)
    {
        int insideUntil = -1;
        for (int index = 0; index < blocks.Count; index++)
        {
            SourceBlock block = blocks[index];
            string? label = IsLabel(block) ? LabelOf(block) : null;
            if (label is null || label == "0" || index <= insideUntil || !called.Contains(label)
                || Targets.Contains(label) || mainEnd < 0 || index < mainEnd || Subs.Contains(label))
            {
                continue;
            }

            int closing = index + 1;
            while (closing < blocks.Count && !(IsLabel(blocks[closing]) && LabelOf(blocks[closing]) == "0"))
            {
                closing++;
            }

            if (closing >= blocks.Count)
            {
                continue;
            }

            SubBegins[block.Line] = label;
            SubEnds.Add(blocks[closing].Line);
            Subs.Add(label);
            SubFirst[label] = index;
            SubLast[label] = closing;
            insideUntil = closing;
        }
    }

    private static int FindLabel(IReadOnlyList<SourceBlock> blocks, string label)
    {
        for (int index = 0; index < blocks.Count; index++)
        {
            if (IsLabel(blocks[index]) && LabelOf(blocks[index]) == label)
            {
                return index;
            }
        }

        return -1;
    }
}
