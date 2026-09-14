using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The structures of the high-level language lowered to LABEL, JUMP and IF (controllers siemens.md 8, 11 rule 6;
/// controller-mapping 6, structured loops): IF cond ... ELSE ... ENDIF jumps over the branch that does not run, WHILE
/// cond ... ENDWHILE tests at the head, FOR R1 = a TO b ... ENDFOR counts R1 up by 1 while it is at most b, LOOP ...
/// ENDLOOP runs until a jump leaves it, REPEAT ... UNTIL cond tests at the end. The Siemens compiler writes them back
/// as IF ... GOTOF chains; the structured forms are never reconstructed.
/// </summary>
internal static class SiemensStructures
{
    /// <summary>
    /// Reads ELSE, ENDIF, WHILE, ENDWHILE, FOR, ENDFOR, LOOP, ENDLOOP, REPEAT and UNTIL.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="statement">The statement of the block.</param>
    public static void Read(SiemensBlock block, SourceWord statement)
    {
        block.MarkRead(statement);
        switch (statement.Address)
        {
            case "ELSE":
                ReadElse(block);
                break;
            case "ENDIF":
                Close(block, "IF", structure => Label(block, block.Draft.Main, structure.EndLabel));
                break;
            case "WHILE":
                ReadWhile(block, statement.Text);
                break;
            case "ENDWHILE":
                Close(block, "WHILE", structure =>
                {
                    block.Draft.Main.Add("JUMP", new IdentValue(structure.BeginLabel));
                    Label(block, After(block), structure.EndLabel);
                });
                break;
            case "FOR":
                ReadFor(block, statement.Text);
                break;
            case "ENDFOR":
                Close(block, "FOR", structure => EndFor(block, structure));
                break;
            case "LOOP":
                Open(block, "LOOP", null, null, block.Draft.Main);
                break;
            case "ENDLOOP":
                Close(block, "LOOP", structure =>
                {
                    block.Draft.Main.Add("JUMP", new IdentValue(structure.BeginLabel));
                    Label(block, After(block), structure.EndLabel);
                });
                break;
            case "REPEAT" when UntilProblem(block) is string problem:
                block.Draft.KeepAsRaw(problem);
                OpenRaw(block, "REPEAT");
                break;
            case "REPEAT":
                Open(block, "REPEAT", null, null, block.Draft.Main);
                break;
            case "UNTIL":
                Close(block, "REPEAT", structure => Until(block, structure, statement.Text));
                break;
        }
    }

    /// <summary>
    /// Reads IF cond, which opens IF ... ELSE ... ENDIF: a jump over the first branch when the condition fails, to the
    /// ELSE branch where the structure has one, else to its end.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="condition">The condition as the source writes it.</param>
    public static void ReadIf(SiemensBlock block, string condition)
    {
        if (SiemensFlow.Condition(block, condition, negate: true) is not Value test)
        {
            OpenRaw(block, "IF");
            return;
        }

        bool hasElse = HasElse(block);
        SiemensState state = block.Siemens;
        string name = state.NewStructureName("IF", "_ELSE", "_END");
        var structure = new SiemensStructure
        {
            Kind = "IF",
            BeginLabel = name,
            EndLabel = name + "_END",
            ElseLabel = hasElse ? name + "_ELSE" : null,
            Line = block.Line,
        };
        state.Structures.Add(structure);
        block.Draft.Main.Add("JUMP", new IdentValue(structure.ElseLabel ?? structure.EndLabel)).Add("IF", test);
    }

    /// <summary>
    /// Keeps a block that stands in a structure kept as RAW as RAW too, since NCX cannot branch on the condition the
    /// structure runs it under, and follows the structures the block opens and closes, so that the end of the RAW
    /// structure is found; what the block may change becomes unknown, as the control may or may not run it (D5;
    /// controllers siemens.md 8). False for a block outside every structure kept as RAW.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static bool ReadInRaw(SiemensBlock block)
    {
        List<SiemensStructure> structures = block.Siemens.Structures;
        SiemensStructure? raw = structures.Find(structure => structure.Raw);
        if (raw is null)
        {
            return false;
        }

        SourceWord? statement = SiemensUnits.StatementOf(block.Source);
        block.MarkAllRead();
        if (OpenedKind(statement) is string kind)
        {
            OpenRaw(block, kind);
        }
        else if (statement?.Address == "ELSE" && structures[^1].Kind != "IF")
        {
            EndWithoutBegin(block, "ELSE");
            return true;
        }
        else if (ClosedKind(statement) is string closed)
        {
            if (structures[^1].Kind != closed)
            {
                EndWithoutBegin(block, closed);
                return true;
            }

            structures.RemoveAt(structures.Count - 1);
        }

        block.Draft.KeepAsRaw($"the block stands in the {raw.Kind} of line {raw.Line}, whose condition has no NCX "
            + "form; NCX cannot branch on it, so the structure is kept as RAW as a whole (controllers siemens.md 8, "
            + "D5)");
        SiemensCalls.ForgetChangesOf(block);
        return true;
    }

    /// <summary>
    /// The kind of the structure a statement opens, IF, WHILE, FOR, LOOP or REPEAT; null for another statement, an IF
    /// with a jump among them (controllers siemens.md 8).
    /// </summary>
    /// <param name="statement">The statement of a block; null for a block without one.</param>
    public static string? OpenedKind(SourceWord? statement)
    {
        return statement?.Address switch
        {
            "IF" when !IsJump(statement.Text) => "IF",
            "WHILE" or "FOR" => statement.Address,
            "LOOP" or "REPEAT" when statement.Text.Length == 0 => statement.Address,
            _ => null,
        };
    }

    /// <summary>
    /// The kind of the structure a statement ends, IF of ENDIF, REPEAT of UNTIL; null for another statement
    /// (controllers siemens.md 8).
    /// </summary>
    /// <param name="statement">The statement of a block; null for a block without one.</param>
    public static string? ClosedKind(SourceWord? statement)
    {
        return statement?.Address switch
        {
            "UNTIL" => "REPEAT",
            "ENDIF" or "ENDWHILE" or "ENDFOR" or "ENDLOOP" when statement.Text.Length == 0 => statement.Address[3..],
            _ => null,
        };
    }

    /// <summary>
    /// True for the statement of an IF with a jump, a conditional jump and not a structure (controllers siemens.md 8).
    /// </summary>
    /// <param name="text">The statement after IF.</param>
    public static bool IsJump(string text)
    {
        return IndexOfWord(text, "GOTOF") >= 0 || IndexOfWord(text, "GOTOB") >= 0 || IndexOfWord(text, "GOTO") >= 0
            || IndexOfWord(text, "GOTOC") >= 0 || IndexOfWord(text, "GOTOS") >= 0;
    }

    /// <summary>
    /// Reports the structures that no end closed before the end of their section (controllers siemens.md 8).
    /// </summary>
    /// <param name="state">The facts of the file.</param>
    /// <param name="diagnostics">The diagnostics of the file.</param>
    public static void ReportOpen(SiemensState state, Diagnostics diagnostics)
    {
        foreach (SiemensStructure structure in state.Structures)
        {
            diagnostics.Warning(structure.Line, DiagnosticCodes.SiemensStructureNotClosed,
                $"The {structure.Kind} of line {structure.Line} is not closed before the end of its program or "
                + "subprogram; its jump back or its end label is missing (controllers siemens.md 8).");
        }

        state.Structures.Clear();
    }

    // ELSE ends the first branch with a jump to the end and begins the second at its label.
    private static void ReadElse(SiemensBlock block)
    {
        List<SiemensStructure> structures = block.Siemens.Structures;
        SiemensStructure? top = structures.Count > 0 ? structures[^1] : null;
        if (top is not { Kind: "IF", ElseLabel: string elseLabel })
        {
            EndWithoutBegin(block, "ELSE");
            return;
        }

        block.Draft.Main.Add("JUMP", new IdentValue(top.EndLabel));
        Label(block, After(block), elseLabel);
    }

    // WHILE cond: the label of the head, then the jump over the loop when the condition fails.
    private static void ReadWhile(SiemensBlock block, string condition)
    {
        if (SiemensFlow.Condition(block, condition, negate: true) is not Value test)
        {
            OpenRaw(block, "WHILE");
            return;
        }

        SiemensStructure structure = Open(block, "WHILE", null, null, block.Draft.Main);
        After(block).Add("JUMP", new IdentValue(structure.EndLabel)).Add("IF", test);
    }

    // FOR R1 = 1 TO 10: VAR:R1=1, the label of the head, the jump over the loop once R1 is beyond the end.
    private static void ReadFor(SiemensBlock block, string text)
    {
        int equals = text.IndexOf('=', StringComparison.Ordinal);
        int to = IndexOfWord(text, "TO");
        string variable = equals < 0 ? "" : text.Substring(0, equals).Trim().ToUpperInvariant();
        string? name = SiemensVariables.IsVariable(variable) ? SiemensVariables.NameOf(variable) : null;
        if (name is null || to < equals)
        {
            block.Draft.KeepAsRaw("FOR is not written FOR variable = start TO end (controllers siemens.md 8)");
            OpenRaw(block, "FOR");
            return;
        }

        Value? start = SiemensExpression.ValueOf(block, text.Substring(equals + 1, to - equals - 1).Trim(),
            out string? problem);
        string? end = start is null ? null : SiemensExpression.ToText(block, text.Substring(to + 2), out problem);
        Value? beyond = end is null ? null : SiemensExpression.Parse(block, "$" + name + " > (" + end + ")",
            out problem);
        if (start is null || beyond is null)
        {
            block.Draft.KeepAsRaw($"FOR has no NCX form: {problem}");
            OpenRaw(block, "FOR");
            return;
        }

        block.Draft.Main.Add("VAR", name, start);
        var head = new SiemensDraftBlock();
        block.Draft.After.Add(head);
        SiemensStructure structure = Open(block, "FOR", name, null, head);
        After(block).Add("JUMP", new IdentValue(structure.EndLabel)).Add("IF", beyond);
    }

    // ENDFOR counts the variable up by 1 and jumps back to the head; the loop ends at its label.
    private static void EndFor(SiemensBlock block, SiemensStructure structure)
    {
        string variable = structure.Variable!;
        if (SiemensExpression.Parse(block, "$" + variable + " + 1", out string? problem) is not Value next)
        {
            block.Draft.KeepAsRaw(problem!);
            return;
        }

        block.Draft.Main.Add("VAR", variable, next);
        After(block).Add("JUMP", new IdentValue(structure.BeginLabel));
        Label(block, After(block), structure.EndLabel);
    }

    // UNTIL cond jumps back to the head of the REPEAT while the condition fails.
    private static void Until(SiemensBlock block, SiemensStructure structure, string condition)
    {
        if (SiemensFlow.Condition(block, condition, negate: true) is Value test)
        {
            block.Draft.Main.Add("JUMP", new IdentValue(structure.BeginLabel)).Add("IF", test);
        }
    }

    // A structure whose condition, or whose FOR, has no NCX form opens as a RAW structure: NCX cannot branch on it, so
    // the structure is kept as RAW as a whole, and its ELSE and its end stay RAW with it, without the ERROR of an end
    // that no structure opened (D5; controllers siemens.md 8). The block that opens it is RAW already.
    // TODO(question): controller-mapping 6 keeps only the system variables of IF $P_SEARCH OR $P_SIM GOTOF BEGINN as
    // RAW, so the blocks such a jump skips stay NCX blocks, and does not say what becomes of the blocks of a structure
    // whose condition has no NCX form; they are kept as RAW with it, the least committal reading, since NCX cannot make
    // their execution depend on the condition.
    private static void OpenRaw(SiemensBlock block, string kind)
    {
        block.Siemens.Structures.Add(new SiemensStructure
        {
            Kind = kind,
            BeginLabel = "",
            EndLabel = "",
            Line = block.Line,
            Raw = true,
        });
    }

    // REPEAT ... UNTIL tests at its end: where the condition of its UNTIL has no NCX form, NCX cannot jump back on it,
    // and the structure is kept as RAW as a whole from the REPEAT on (D5; controllers siemens.md 8). Null where the
    // UNTIL has an NCX form, or where no UNTIL closes the REPEAT.
    private static string? UntilProblem(SiemensBlock block)
    {
        SiemensUnit? unit = block.Unit;
        int index = unit?.Blocks.IndexOf(block.Source) ?? -1;
        int depth = 0;
        for (int next = index + 1; index >= 0 && next < unit!.Blocks.Count; next++)
        {
            SourceWord? statement = SiemensUnits.StatementOf(unit.Blocks[next]);
            if (OpenedKind(statement) == "REPEAT")
            {
                depth++;
            }
            else if (ClosedKind(statement) == "REPEAT" && depth-- == 0)
            {
                string? text = SiemensExpression.ToText(block, statement!.Text, out string? problem);
                if (text is not null && SiemensExpression.Parse(block, "NOT (" + text + ")", out problem) is not null)
                {
                    return null;
                }

                return $"the condition {statement.Text.Trim()} of the UNTIL of line {unit.Blocks[next].Line} has no "
                    + $"NCX form: {problem}; NCX cannot jump back on it, so the REPEAT is kept as RAW as a whole";
            }
        }

        return null;
    }

    // A structure opens with the label of its head in the block given.
    private static SiemensStructure Open(SiemensBlock block, string kind, string? variable, string? elseLabel,
        SiemensDraftBlock head)
    {
        SiemensState state = block.Siemens;
        string name = state.NewStructureName(kind, "", "_END");
        var structure = new SiemensStructure
        {
            Kind = kind,
            BeginLabel = name,
            EndLabel = name + "_END",
            ElseLabel = elseLabel,
            Variable = variable,
            Line = block.Line,
        };
        state.Structures.Add(structure);
        Label(block, head, name);
        return structure;
    }

    // The innermost structure closes when it is of the kind of the end; any other end is an ERROR and stays RAW.
    private static void Close(SiemensBlock block, string kind, Action<SiemensStructure> close)
    {
        List<SiemensStructure> structures = block.Siemens.Structures;
        if (structures.Count == 0 || structures[^1].Kind != kind)
        {
            EndWithoutBegin(block, kind);
            return;
        }

        SiemensStructure structure = structures[^1];
        structures.RemoveAt(structures.Count - 1);
        close(structure);
    }

    private static void EndWithoutBegin(SiemensBlock block, string kind)
    {
        block.Diagnostics.Error(block.Line, DiagnosticCodes.SiemensStructureEndWithoutBegin,
            $"The end of {kind} closes no {kind} of its program or subprogram; the block is kept as RAW (controllers "
            + "siemens.md 8).");
        block.Draft.KeepAsRaw($"the end of {kind} closes no {kind}");
    }

    // A label of the lowering: a jump enters the block after it with a state the blocks before it do not tell
    // (virtual machine 3.6).
    private static void Label(SiemensBlock block, SiemensDraftBlock target, string label)
    {
        target.Add("LABEL", new IdentValue(label));
        block.Siemens.ForgetPositions();
        block.Facts.Written.Clear();
    }

    // A new block after the blocks the source block reads into so far.
    private static SiemensDraftBlock After(SiemensBlock block)
    {
        var next = new SiemensDraftBlock();
        block.Draft.After.Add(next);
        return next;
    }

    // An ELSE at the depth of the IF stands before its ENDIF (controllers siemens.md 8).
    private static bool HasElse(SiemensBlock block)
    {
        SiemensUnit? unit = block.Unit;
        if (unit is null)
        {
            return false;
        }

        int depth = 0;
        bool after = false;
        foreach (SourceBlock candidate in unit.Blocks)
        {
            if (ReferenceEquals(candidate, block.Source))
            {
                after = true;
                continue;
            }

            SourceWord? statement = after ? SiemensUnits.StatementOf(candidate) : null;
            switch (statement?.Address)
            {
                case "IF" when !IsJump(statement.Text):
                    depth++;
                    break;
                case "ELSE" when depth == 0:
                    return true;
                case "ENDIF" when depth == 0:
                    return false;
                case "ENDIF":
                    depth--;
                    break;
            }
        }

        return false;
    }

    // The place of a word of a statement, not a part of a longer name; -1 where the statement has none.
    private static int IndexOfWord(string text, string word)
    {
        int position = 0;
        while (position < text.Length)
        {
            int found = text.IndexOf(word, position, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return -1;
            }

            bool starts = found == 0 || !(char.IsAsciiLetterOrDigit(text[found - 1]) || text[found - 1] == '_');
            int end = found + word.Length;
            bool ends = end == text.Length || !(char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_');
            if (starts && ends)
            {
                return found;
            }

            position = found + 1;
        }

        return -1;
    }
}
