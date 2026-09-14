using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The jumps and the program-part repeats of a SINUMERIK program (controllers siemens.md 8, 11 rule 6;
/// controller-mapping 1 and 6; language 4.9, 4.13): GOTOF, GOTOB and GOTO as JUMP, with IF as JUMP with IF, to a label
/// or a block number of the unit, JUMP=END to a label directly before the end of the program; GOTOS as a JUMP to the
/// label after the header; CASE lowered to JUMP with IF per case; REPEAT LABEL P=3 as REPEAT with TIMES, a repeated
/// range that does not end before its REPEAT as a CALL with TIMES of the SUB section made of it; the structures
/// IF, WHILE, FOR, LOOP, REPEAT ... UNTIL are SiemensStructures, DEF and the assignments SiemensVariables, the calls
/// SiemensCalls.
/// </summary>
internal static class SiemensFlow
{
    private static readonly string[] s_jumps = ["GOTOF", "GOTOB", "GOTO", "GOTOC", "GOTOS"];

    /// <summary>
    /// Reads the statement of a block, its calls and its assignments.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        SourceWord? statement = SiemensUnits.StatementOf(block.Source);
        switch (statement?.Address)
        {
            case "GOTOF" or "GOTOB" or "GOTO":
                block.MarkRead(statement);
                ReadJump(block, statement.Text, null);
                return;
            case "GOTOS":
                block.MarkRead(statement);
                ReadGotos(block, statement.Text, null);
                return;
            case "IF":
                ReadIf(block, statement);
                return;
            case "CASE":
                ReadCase(block, statement);
                return;
            case "REPEAT" when statement.Text.Length > 0:
                ReadRepeat(block, statement);
                return;
            case "REPEATB":
                ReadRepeat(block, statement);
                return;
            case "DEF":
                SiemensVariables.ReadDefinition(block, statement);
                return;
            case "DEFINE":
                ReadDefine(block);
                return;
            case "WHILE" or "FOR" or "UNTIL" or "REPEAT" or "ELSE" or "ENDIF" or "ENDWHILE" or "ENDFOR" or "LOOP"
                or "ENDLOOP" when statement.Text.Length == 0 || statement.Address is "WHILE" or "FOR" or "UNTIL":
                SiemensStructures.Read(block, statement);
                return;
        }

        SiemensCalls.Read(block);
        if (!block.Draft.IsRaw)
        {
            SiemensVariables.ReadAssignments(block);
        }
    }

    /// <summary>
    /// The label a jump writes: END for a label directly before the end of the program, the label where the unit has
    /// it; null for a computed target, a label the unit does not have, one NCX cannot write (controller-mapping 1, 6).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="target">The target as the source writes it, "LAB1", "N300".</param>
    public static Value? LabelValue(SiemensBlock block, string target)
    {
        string label = target.ToUpperInvariant();
        if (label.Length > 1 && label[0] == 'N' && SiemensNumbers.WholeNumber(label.Substring(1)) is not null)
        {
            label = SiemensUnits.NumberLabel(label.Substring(1));
        }

        SiemensUnit? unit = block.Unit;
        if (unit is null)
        {
            return null;
        }

        if (unit.EndTargets.Contains(label))
        {
            return new IdentValue("END");
        }

        return unit.Targets.Contains(label) ? new IdentValue(label) : null;
    }

    /// <summary>
    /// The condition of IF, UNTIL or WHILE as an NCX expression, optionally negated; null, with the block kept RAW,
    /// where NCX cannot express it (language 4.12).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="condition">The condition as the source writes it.</param>
    /// <param name="negate">True for the condition under which the jump over the structure is taken.</param>
    public static Value? Condition(SiemensBlock block, string condition, bool negate)
    {
        string? text = SiemensExpression.ToText(block, condition, out string? problem);
        Value? value = text is null
            ? null
            : SiemensExpression.Parse(block, negate ? "NOT (" + text + ")" : text, out problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"the condition {condition.Trim()} has no NCX form: {problem}");
        }

        return value;
    }

    // GOTOF LABEL, GOTOB LABEL and GOTO LABEL continue at the label, forward, backward or both; with IF only when the
    // condition holds (controllers siemens.md 8; controller-mapping 6, JUMP and IF).
    private static void ReadJump(SiemensBlock block, string target, string? condition)
    {
        string first = target.Trim();
        int end = SiemensScanner.ReadIdentifier(first, 0);
        Value? label = end == first.Length && end > 0 ? LabelValue(block, first) : null;
        if (label is null)
        {
            block.Draft.KeepAsRaw($"the jump to {first} names no label of its program or subprogram that NCX can "
                + "write, "
                + "or a computed target (controller-mapping 6, LABEL and JUMP)");
            return;
        }

        Value? test = condition is null ? null : Condition(block, condition, negate: false);
        if (condition is not null && test is null)
        {
            return;
        }

        block.Draft.Main.Add("JUMP", label);
        if (test is not null)
        {
            block.Draft.Main.Add("IF", test);
        }
    }

    // GOTOS jumps to the start of the program, with IF only when the condition holds: JUMP to the label after the
    // header (controllers siemens.md 8; controller-mapping 1, JUMP=END).
    private static void ReadGotos(SiemensBlock block, string rest, string? condition)
    {
        if (block.Siemens.StartLabel is not string start || rest.Trim().Length > 0)
        {
            block.Draft.KeepAsRaw("GOTOS jumps to the start of a program, and this is none (controller-mapping 1)");
            return;
        }

        Value? test = condition is null ? null : Condition(block, condition, negate: false);
        if (condition is not null && test is null)
        {
            return;
        }

        block.Draft.Main.Add("JUMP", new IdentValue(start));
        if (test is not null)
        {
            block.Draft.Main.Add("IF", test);
        }
    }

    // IF cond GOTOF LABEL is a conditional jump; IF cond alone opens the structure IF ... ELSE ... ENDIF
    // (controllers siemens.md 8).
    private static void ReadIf(SiemensBlock block, SourceWord statement)
    {
        block.MarkRead(statement);
        string text = statement.Text;
        int jump = JumpAt(text, out string keyword);
        if (jump < 0)
        {
            SiemensStructures.ReadIf(block, text);
            return;
        }

        if (keyword == "GOTOC")
        {
            block.Draft.KeepAsRaw("GOTOC jumps without an alarm when the label is missing "
                + "(controller-mapping 6, JUMP)");
            return;
        }

        if (keyword == "GOTOS")
        {
            ReadGotos(block, text.Substring(jump + keyword.Length), text.Substring(0, jump));
            return;
        }

        ReadJump(block, text.Substring(jump + keyword.Length), text.Substring(0, jump));
    }

    // CASE(R1) OF 1 GOTOF A 2 GOTOF B DEFAULT GOTOF C: one JUMP with IF per case, the DEFAULT without IF
    // (controller-mapping 6, JUMP and IF).
    private static void ReadCase(SiemensBlock block, SourceWord statement)
    {
        block.MarkRead(statement);
        string text = statement.Text;
        int open = SiemensScanner.SkipBlanks(text, 0);
        int close = SiemensScanner.ReadGroup(text, open);
        string? selector = close < 0 ? null : SiemensExpression.ToText(block,
            text.Substring(open + 1, close - open - 2), out _);
        List<string> tokens = close < 0 ? [] : Tokens(text.Substring(close));
        if (selector is null || tokens.Count < 4 || tokens[0] != "OF" || (tokens.Count - 1) % 3 != 0)
        {
            block.Draft.KeepAsRaw("CASE is not written CASE(expression) OF value GOTOF label ... (controllers "
                + "siemens.md 8)");
            return;
        }

        var jumps = new List<SiemensDraftBlock>();
        for (int index = 1; index < tokens.Count; index += 3)
        {
            string value = tokens[index];
            if (Array.IndexOf(s_jumps, tokens[index + 1]) < 0 || tokens[index + 1] is "GOTOC" or "GOTOS"
                || LabelValue(block, tokens[index + 2]) is not Value label)
            {
                block.Draft.KeepAsRaw($"a case of CASE jumps to no label NCX can write (controller-mapping 6)");
                return;
            }

            var jump = new SiemensDraftBlock().Add("JUMP", label);
            if (value != "DEFAULT")
            {
                Value? test = SiemensNumbers.Parse(value) is null
                    ? null
                    : SiemensExpression.Parse(block, "(" + selector + ") == " + value, out _);
                if (test is null)
                {
                    block.Draft.KeepAsRaw($"the case {value} of CASE is no number (controller-mapping 6)");
                    return;
                }

                jump.Add("IF", test);
            }

            jumps.Add(jump);
        }

        if (jumps.Count == 0)
        {
            block.Draft.KeepAsRaw("CASE has no case");
            return;
        }

        foreach (Word word in jumps[0].Words)
        {
            block.Draft.Main.Add(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.AddRange(jumps.GetRange(1, jumps.Count - 1));
    }

    // REPEAT LABEL P=3 repeats the blocks from the label to the REPEAT: REPEAT=LABEL TIMES=3. REPEAT START END P=3,
    // REPEATB LABEL P=3 and REPEAT LABEL P=3 with an ENDLABEL: between them repeat a block range that ends before the
    // REPEAT: it reads as REPEAT where it ends directly before, and is lowered to a SUB section otherwise, which the
    // structure pass makes of a copy of the range and the REPEAT calls P times, CALL=name TIMES=3 (controller-mapping
    // 6, REPEAT + TIMES; controllers siemens.md 11 rule 6).
    // TODO(question): D212, a REPEAT without TIMES; a REPEAT without P writes none, which repeats the blocks once, as
    // D212 recommends, and its CALL none, which calls the SUB section once.
    private static void ReadRepeat(SiemensBlock block, SourceWord statement)
    {
        block.MarkRead(statement);
        if (block.Unit?.Repeats.GetValueOrDefault(block.Source) is not SiemensRepeat repeat)
        {
            block.Draft.KeepAsRaw("REPEAT names no label (controller-mapping 6, REPEAT)");
            return;
        }

        if (repeat.Problem is string problem)
        {
            block.Draft.KeepAsRaw(problem);
            return;
        }

        Value? times = repeat.Passes is int passes ? new IntegerValue(passes, repeat.PassesText!) : null;
        if (repeat.Adjacent)
        {
            if (LabelValue(block, repeat.Label) is not IdentValue start)
            {
                block.Draft.KeepAsRaw($"the repeat names {repeat.Label}, no label NCX can write (language 4.9, LABEL)");
                return;
            }

            block.Draft.Main.Add("REPEAT", start);
        }
        else if (block.RepeatSection is string section)
        {
            block.Draft.Main.Add("CALL", new IdentValue(section));
            block.Calls.Add(new KeyValuePair<string, int>(section, repeat.Passes ?? 1));
            block.Siemens.RepeatSections[section] = repeat.Blocks;
        }
        else
        {
            block.Draft.KeepAsRaw("the structure pass made no SUB section of the repeated range, whose first or last "
                + "block carries another label (controller-mapping 6, REPEAT + TIMES)");
            return;
        }

        if (times is not null)
        {
            block.Draft.Main.Add("TIMES", times);
        }
    }

    // DEFINE NAME AS text defines a macro, which the reader expands in the blocks after it (controller-mapping 6,
    // macros).
    // TODO(question): the documents say the reader expands DEFINE macros but not what it writes for the DEFINE block
    // itself; it is kept as a comment line, so that its text stays in the file.
    private static void ReadDefine(SiemensBlock block)
    {
        block.MarkAllRead();
        block.Draft.CommentLine = "; " + block.Source.Text.Trim();
    }

    // The place of the jump keyword of an IF, outside strings and groups; -1 when the IF has none.
    private static int JumpAt(string text, out string keyword)
    {
        keyword = "";
        int position = 0;
        while (position < text.Length)
        {
            char character = text[position];
            if (character == '"')
            {
                int end = SiemensScanner.ReadString(text, position);
                position = end < 0 ? text.Length : end;
            }
            else if (character is '(' or '[')
            {
                int end = SiemensScanner.ReadGroup(text, position);
                position = end < 0 ? text.Length : end;
            }
            else if (SiemensScanner.StartsIdentifier(character))
            {
                int end = SiemensScanner.ReadIdentifier(text, position);
                string word = text.Substring(position, end - position).ToUpperInvariant();
                if (Array.IndexOf(s_jumps, word) >= 0)
                {
                    keyword = word;
                    return position;
                }

                position = end;
            }
            else
            {
                position++;
            }
        }

        return -1;
    }

    /// <summary>
    /// The words of a statement in capitals, = and the numbers as tokens of their own.
    /// </summary>
    /// <param name="text">The statement after its keyword.</param>
    public static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        int position = 0;
        while (position < text.Length)
        {
            position = SiemensScanner.SkipBlanks(text, position);
            if (position >= text.Length)
            {
                break;
            }

            char character = text[position];
            int end = SiemensScanner.StartsIdentifier(character)
                ? SiemensScanner.ReadIdentifier(text, position)
                : char.IsAsciiDigit(character) || character is '-' or '+' or '.'
                    ? SiemensScanner.ReadNumber(text, position)
                    : position + 1;
            end = end <= position ? position + 1 : end;
            tokens.Add(text.Substring(position, end - position).ToUpperInvariant());
            position = end;
        }

        return tokens;
    }
}
