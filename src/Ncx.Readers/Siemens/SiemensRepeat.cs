namespace Ncx.Readers.Siemens;

/// <summary>
/// A program-part repeat of a unit (controllers siemens.md 8, 11 rule 6; controller-mapping 6, REPEAT + TIMES): REPEAT
/// LABEL P=3 repeats the blocks from the label to the REPEAT, or to the ENDLABEL: between them, REPEAT START END P=3
/// the blocks from START to END, REPEATB LABEL P=3 the block of the label, each P more times. A range that ends
/// directly before the repeat is REPEAT=LABEL TIMES=3, since language 4.9 repeats the blocks from a label to the REPEAT
/// block; any other range is lowered to a SUB section that the repeat calls P times (SourceStructure.Repeat).
/// </summary>
internal sealed class SiemensRepeat
{
    /// <summary>
    /// The label the repeat names first, in capitals, N40 for the block number.
    /// </summary>
    public string Label { get; private init; } = "";

    /// <summary>
    /// The passes P; null for a repeat without P.
    /// </summary>
    public int? Passes { get; private init; }

    /// <summary>
    /// The passes as the source writes them; null for a repeat without P.
    /// </summary>
    public string? PassesText { get; private init; }

    /// <summary>
    /// True when the range ends directly before the repeat: REPEAT=Label.
    /// </summary>
    public bool Adjacent { get; private init; }

    /// <summary>
    /// The blocks of a range that is lowered to a SUB section, in unit order; empty for any other repeat.
    /// </summary>
    public IReadOnlyList<SourceBlock> Blocks { get; private init; } = [];

    /// <summary>
    /// The label by which the repeat names the first block of the range, as the block carries it.
    /// </summary>
    public string FirstLabel { get; private init; } = "";

    /// <summary>
    /// The label by which the repeat names the last block of the range, as the block carries it: the second label,
    /// ENDLABEL, or the first label again for a range of one block.
    /// </summary>
    public string LastLabel { get; private init; } = "";

    /// <summary>
    /// Why the repeat stays RAW; null for one that reads.
    /// </summary>
    public string? Problem { get; private init; }

    /// <summary>
    /// True for a range that is lowered to a SUB section.
    /// </summary>
    public bool IsRange => Problem is null && !Adjacent && Blocks.Count > 0;

    /// <summary>
    /// The repeat of a REPEAT or REPEATB block among the blocks of its unit; null for another block. The label is
    /// searched in the blocks before the repeat, the nearest first.
    /// </summary>
    /// <param name="units">The units of the file, whose subprograms a range may not call.</param>
    /// <param name="unit">The unit of the block.</param>
    /// <param name="index">The index of the block among the blocks of the unit.</param>
    public static SiemensRepeat? Of(SiemensUnits units, SiemensUnit unit, int index)
    {
        SourceWord? statement = SiemensUnits.StatementOf(unit.Blocks[index]);
        bool single = statement?.Address == "REPEATB";
        if (statement is null || !(single || (statement.Address == "REPEAT" && statement.Text.Length > 0)))
        {
            return null;
        }

        List<string> tokens = SiemensFlow.Tokens(statement.Text);
        int labels = 0;
        while (labels < tokens.Count && labels < 2 && tokens[labels] != "P")
        {
            labels++;
        }

        int? passes = null;
        string? passesText = null;
        if (labels < tokens.Count)
        {
            passes = tokens.Count == labels + 3 && tokens[labels] == "P" && tokens[labels + 1] == "="
                ? SiemensNumbers.WholeNumber(tokens[labels + 2])
                : null;
            if (passes is not int times || times < 1)
            {
                return Fails("P of REPEAT counts its passes as a whole number (controller-mapping 6, TIMES)");
            }

            passesText = tokens[labels + 2];
        }

        if (labels == 0 || (single && labels > 1))
        {
            return Fails("REPEAT names no label, or REPEATB more than one (controllers siemens.md 8)");
        }

        string first = Normal(tokens[0]);
        int start = index - 1;
        while (start >= 0 && Carried(unit.Blocks[start], first) is null)
        {
            start--;
        }

        if (start < 0)
        {
            return Fails($"the label {first} of the repeat stands in no block before it (controllers siemens.md 8)");
        }

        int end = start;
        string last = labels == 2 ? Normal(tokens[1]) : first;
        if (labels == 2)
        {
            while (end < index && Carried(unit.Blocks[end], last) is null)
            {
                end++;
            }

            if (end == index)
            {
                return Fails($"the label {last} of the repeat stands in no block between {first} and the repeat "
                    + "(controllers siemens.md 8)");
            }
        }
        else if (!single)
        {
            // REPEAT LABEL: the blocks from the label to the repeat, or to the first ENDLABEL: after the label.
            while (end < index - 1 && !IsEndLabel(unit.Blocks[end]))
            {
                end++;
            }
        }

        if (end == index - 1)
        {
            return new SiemensRepeat { Label = first, Passes = passes, PassesText = passesText, Adjacent = true };
        }

        List<SourceBlock> range = unit.Blocks.GetRange(start, end - start + 1);
        if (RangeProblem(units, range) is string problem)
        {
            return Fails(problem);
        }

        return new SiemensRepeat
        {
            Label = first,
            Passes = passes,
            PassesText = passesText,
            Blocks = range,
            FirstLabel = Carried(range[0], first)!,
            LastLabel = labels == 2 ? Carried(range[^1], last)! : single ? Carried(range[0], first)!
                : SiemensUnits.LabelOf(range[^1])!,
        };

        SiemensRepeat Fails(string reason)
        {
            return new SiemensRepeat { Label = labels > 0 ? tokens[0] : "", Problem = reason };
        }
    }

    // A SUB section is entered and left as a whole (language 4.9, 4.13): the range holds no end or return, no jump, no
    // call of a subprogram of the file and no other repeat, and it closes every structure it opens, since the SUB
    // section lowers its structures on its own (controllers siemens.md 8).
    private static string? RangeProblem(SiemensUnits units, List<SourceBlock> range)
    {
        const string Cannot = ", which a SUB section, entered and left as a whole, cannot hold (controller-mapping 6, "
            + "REPEAT + TIMES; language 4.9)";
        var open = new List<string>();
        foreach (SourceBlock block in range)
        {
            SourceWord? statement = SiemensUnits.StatementOf(block);
            foreach (SourceWord word in block.Words)
            {
                if (SiemensBlock.CodeOf(word) is "M30" or "M2" or "M17"
                    || (word.Address == "RET" && word.Text.Length == 0))
                {
                    return "the repeated range holds an end or a return" + Cannot;
                }
            }

            if (statement?.Address is "REPEAT" or "REPEATB" && statement.Text.Length > 0)
            {
                return "the repeated range holds another repeat" + Cannot;
            }

            if (statement?.Address is "GOTOF" or "GOTOB" or "GOTO" or "GOTOC" or "GOTOS" or "CASE"
                || (statement?.Address == "IF" && SiemensStructures.IsJump(statement.Text)))
            {
                return "the repeated range holds a jump" + Cannot;
            }

            foreach (string name in SiemensCalls.NamesCalledBy(block))
            {
                if (units.FindSub(name) is not null)
                {
                    return "the repeated range calls a subprogram of the file" + Cannot;
                }
            }

            if (SiemensStructures.OpenedKind(statement) is string kind)
            {
                open.Add(kind);
            }
            else if (statement?.Address == "ELSE" && (open.Count == 0 || open[^1] != "IF"))
            {
                return "the repeated range holds an ELSE of a structure it does not hold" + Cannot;
            }
            else if (SiemensStructures.ClosedKind(statement) is string closed)
            {
                if (open.Count == 0 || open[^1] != closed)
                {
                    return "the repeated range closes a structure it does not open" + Cannot;
                }

                open.RemoveAt(open.Count - 1);
            }
        }

        return open.Count > 0 ? "the repeated range opens a structure it does not close" + Cannot : null;
    }

    // The label as the unit writes it, N40 of N040.
    private static string Normal(string token)
    {
        return token.Length > 1 && token[0] == 'N' && SiemensNumbers.WholeNumber(token.Substring(1)) is not null
            ? SiemensUnits.NumberLabel(token.Substring(1))
            : token;
    }

    // The label of the block that is the one given, NAME of NAME: or its block number N40; null for another block.
    private static string? Carried(SourceBlock block, string label)
    {
        if (SiemensUnits.LabelOf(block) is string name
            && string.Equals(name, label, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return SiemensUnits.NumberLabelOf(block) == label ? label : null;
    }

    // ENDLABEL: ends the range of a REPEAT LABEL (controllers siemens.md 8).
    private static bool IsEndLabel(SourceBlock block)
    {
        return string.Equals(SiemensUnits.LabelOf(block), "ENDLABEL", StringComparison.OrdinalIgnoreCase);
    }
}
