using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The flow of a SINUMERIK block (controllers siemens.md 8, 12; controller-mapping 1, 6; language 4.9, 4.13): labels as
/// NAME:, JUMP as GOTOF or GOTOB with IF, JUMP=END to a label before the program end, the structured loops never
/// reconstructed; CALL as NAME, NAME(1,2) with the PROC parameters, NAME P3, EXTCALL for a program outside the file;
/// REPEAT label P=; RETURN as sub_end; VAR as R1= or the declared name.
/// </summary>
internal static class SiemensFlow
{
    /// <summary>
    /// LABEL=name as NAME: in a line of its own before the lines of the block, which a jump enters there (controllers
    /// siemens.md 8; controller-mapping 6, LABEL). A jump arrives at the label with what the control has active at the
    /// jump, so the target state is unknown after it.
    /// </summary>
    public static void WriteLabel(SiemensBlock write)
    {
        if (write.Block.Find("LABEL") is not Word label)
        {
            return;
        }

        write.Written(label);
        write.Write(SiemensFile.LabelOf(label.Value) + ":");

        // The STATIC walk does not follow JUMP (virtual machine 1), so the target state at the label is that of the
        // block before it, and a jump, from after the label on a later pass too, arrives with that of its own block:
        // every modal code, the feed that a CYCLE_F left (D29), the edge, the datum and the master spindle stand again
        // at their next use (language 2 rule 2; controller-mapping 5, CYCLE_F; controllers siemens.md 5, 12 rule 3).
        // Every jump ends the modal call before it (WriteJump, WriteRepeat, the calls, RAW whose text may jump), so
        // the modal call is off after the label where it is off before it, and unknown otherwise: the next call at a
        // position arms it again, the next motion ends it (siemens 7; controller-mapping 5, CYCLE_CALL).
        bool modalCallOff = SiemensCycles.ModalCallOff(write);
        write.MakeTargetUnknown();
        if (modalCallOff)
        {
            SiemensCycles.ModalCallEnded(write);
        }
    }

    /// <summary>
    /// VAR:name=value as NAME=value, the assignments of the block in one line (controllers siemens.md 8;
    /// controller-mapping 6, VAR; language 4.9).
    /// </summary>
    public static void WriteVariables(SiemensBlock write)
    {
        var assignments = new List<string>();
        foreach (Word word in write.Block.Words)
        {
            if (word.Key != "VAR" || word.Addr is not string variable)
            {
                continue;
            }

            write.Written(word);
            string? name = SiemensExpressions.VariableName(write, variable);
            string? value = word.Value switch
            {
                StringValue text => "\"" + text.Content + "\"",
                _ => SiemensExpressions.ValueText(write, word.Value, variable),
            };
            if (name is not null && value is not null)
            {
                assignments.Add(name + "=" + value);
            }
        }

        if (assignments.Count > 0)
        {
            write.Write(string.Join(" ", assignments));
        }
    }

    /// <summary>
    /// CALL, JUMP, REPEAT and RETURN in lines of their own after the block (controllers siemens.md 8; language 4.9).
    /// </summary>
    public static void Write(SiemensBlock write, string fileStem)
    {
        WriteCall(write, fileStem);
        WriteJump(write);
        WriteRepeat(write);
        WriteReturn(write);
    }

    // NAME, NAME(1, , 3) with the parameters of its PROC, NAME P3; IF around it as IF ... ENDIF; a program outside the
    // file with EXTCALL (controllers siemens.md 8; controller-mapping 6, CALL, TIMES, ARG). MCALL alone stands before
    // the call where a modal call may be armed, a CYCLE_CALL of the same block included, so that no block with a
    // position of the called program calls it and every subprogram starts without one (siemens 7).
    private static void WriteCall(SiemensBlock write, string fileStem)
    {
        if (write.Block.Find("CALL") is not Word call)
        {
            return;
        }

        write.Written(call);
        write.Written("ARG");
        write.Written("TIMES");
        string? condition = Condition(write);
        if (write.File.SubNamed(call.Value) is Section sub)
        {
            string name = write.File.UnitNameOf(sub, fileStem, write);
            string? text = CallText(write, sub, name);
            if (text is null)
            {
                return;
            }

            SiemensCycles.EndModalCall(write);
            WriteConditional(write, condition, text);
            return;
        }

        // EXTCALL("NAME") calls a program outside the file (controller-mapping 6, CALL).
        if (write.Block.Has("ARG") || write.Block.Has("TIMES"))
        {
            write.Error(DiagnosticCodes.SiemensExternalCallNotWritable,
                $"{call.ToCanonical()} calls a program outside the file with arguments or a repeat count, which "
                + "EXTCALL does not take (controllers siemens.md 8; controller-mapping 6, CALL).");
            return;
        }

        string target = call.Value is StringValue external ? external.Content : call.Value.ToCanonical();
        SiemensCycles.EndModalCall(write);
        WriteConditional(write, condition, "EXTCALL(\"" + target + "\")");
    }

    // The call of a subprogram of the file: its arguments by the positions of its PROC parameters, empty where the
    // call gives none, and P with TIMES (controllers siemens.md 8).
    private static string? CallText(SiemensBlock write, Section sub, string name)
    {
        IReadOnlyList<string> parameters = write.File.ParametersOf(sub);
        string text = name;
        if (parameters.Count > 0)
        {
            var values = new List<string>();
            foreach (string parameter in parameters)
            {
                Word? argument = write.Block.Find("ARG", parameter);
                string? value = argument is null ? "" : SiemensExpressions.ValueText(write, argument.Value, "ARG");
                if (value is null)
                {
                    return null;
                }

                values.Add(value);
            }

            text += "(" + string.Join(",", values) + ")";
        }

        if (write.Block.Find("TIMES") is Word times)
        {
            if (SiemensExpressions.ValueText(write, times.Value, "TIMES") is not string count)
            {
                return null;
            }

            text += times.Value is ExprValue ? " P=" + count : " P" + count;
        }

        return text;
    }

    // JUMP=label as GOTOF where the label stands after the jump, GOTOB where it stands before or in the block of the
    // jump, which a jump to its own label runs again; JUMP=END to the label before the program end; IF in front where
    // the block has one (controllers siemens.md 8: GOTOF forward, GOTOB backward; controller-mapping 1, JUMP=END, and
    // 6, JUMP + IF; language 4.9). MCALL alone stands before a jump to a label where a modal call may be armed, so
    // that every jump arrives at its label without one (siemens 7; WriteLabel).
    private static void WriteJump(SiemensBlock write)
    {
        if (write.Block.Find("JUMP") is not Word jump)
        {
            return;
        }

        write.Written(jump);
        Section? section = write.Step.Section;
        bool toEnd = jump.Value.ToCanonical() == "END";
        if (toEnd && section is { Kind: SectionKind.Sub })
        {
            WriteJumpToEndInSubprogram(write, section);
            return;
        }

        string? condition = Condition(write);
        string text;
        if (toEnd && section is not null)
        {
            text = "GOTOF " + write.File.EndLabelOf(section);
        }
        else
        {
            Block? target = section is null ? null : write.File.LabelBlock(section, jump.Value.ToCanonical());
            string direction = target is not null && target.Line <= write.Block.Line ? "GOTOB " : "GOTOF ";
            text = direction + SiemensFile.LabelOf(jump.Value);
            SiemensCycles.EndModalCall(write);
        }

        write.Write(condition is null ? text : "IF " + condition + " " + text);
    }

    // JUMP=END in a subprogram "continues at the PROGRAM=END of the current program" (language 4.9), ending the
    // program from the call (virtual machine 3.6); the target of a GOTOF is a label of the subprogram's own unit
    // (controllers siemens.md 8), where the label before the M30 of the program does not stand.
    // TODO(question): the documents give a SINUMERIK subprogram no form that ends the program: siemens 1 names M17 and
    // RET as the returns and does not say what M30 does in a subprogram, and controller-mapping 6 keeps RET("label")
    // as RAW; JUMP=END in a subprogram is an ERROR, until that is answered.
    private static void WriteJumpToEndInSubprogram(SiemensBlock write, Section sub)
    {
        write.Written("IF");
        write.Error(DiagnosticCodes.SiemensJumpToEndInSubprogram,
            $"JUMP=END in the subprogram {sub.Name ?? sub.Number?.ToString(CultureInfo.InvariantCulture)} ends the "
            + "program from the call (language 4.9; virtual machine 3.6), and a SINUMERIK subprogram has no form that "
            + "ends the program: a GOTOF goes to a label of its own unit (controllers siemens.md 8).");
    }

    // REPEAT=label TIMES=n as REPEAT LABEL P=n, the blocks from the label to this one n more times; without TIMES once
    // more, as D212 recommends (controllers siemens.md 8; controller-mapping 6, REPEAT + TIMES). MCALL alone stands
    // before it where a modal call may be armed, as before a jump (WriteJump).
    // TODO(question): D212: REPEAT without TIMES repeats the blocks once more and is written without P, until D212 is
    // answered.
    private static void WriteRepeat(SiemensBlock write)
    {
        if (write.Block.Find("REPEAT") is not Word repeat)
        {
            return;
        }

        write.Written(repeat);
        write.Written("TIMES");
        string text = "REPEAT " + SiemensFile.LabelOf(repeat.Value);
        if (write.Block.Find("TIMES") is Word times)
        {
            if (SiemensExpressions.ValueText(write, times.Value, "TIMES") is not string count)
            {
                return;
            }

            text += " P=" + count;
        }

        SiemensCycles.EndModalCall(write);
        write.Write(text);
    }

    // RETURN in a subprogram is sub_end, RET or M17 (machine-config 2); outside a subprogram it is a WARNING of the
    // virtual machine and ends the program as JUMP=END does (language 4.9, RETURN).
    private static void WriteReturn(SiemensBlock write)
    {
        if (write.Block.Find("RETURN") is not Word ret)
        {
            return;
        }

        write.Written(ret);
        if (write.Step.Section is Section { Kind: SectionKind.Program } program)
        {
            write.Write("GOTOF " + write.File.EndLabelOf(program));
            return;
        }

        if (write.Render(write.Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)",
            new TemplateValues()) is string text)
        {
            write.Write(text);
        }
    }

    // The IF of the block as the condition of its JUMP or CALL (language 4.9, IF).
    private static string? Condition(SiemensBlock write)
    {
        if (write.Block.Find("IF") is not Word condition)
        {
            return null;
        }

        write.Written(condition);
        return condition.Value is ExprValue expression
            ? SiemensExpressions.Text(write, expression)
            : condition.Value.ToCanonical();
    }

    // A call under IF: a jump over the call where the condition does not hold, IF NOT (cond) GOTOF, the call, the
    // label; the Siemens compiler writes IF ... GOTOF chains and never reconstructs the structured forms
    // (controller-mapping 6, structured loops; siemens 8). The label is reached from the call and from the jump, so
    // the block after the call meets what the control has active at the jump (SiemensFile.MeetJumpsOverCalls).
    private static void WriteConditional(SiemensBlock write, string? condition, string text)
    {
        if (condition is null)
        {
            write.Write(text);
            return;
        }

        string label = "SKIP_CALL_" + write.Block.Line.ToString(CultureInfo.InvariantCulture);
        write.Write("IF NOT (" + condition + ") GOTOF " + label);
        write.File.JumpOverCall(write.Target);
        write.Write(text);
        write.Write(label + ":");
    }

    /// <summary>
    /// The PROC line of a subprogram with the parameters its calls give, REAL each: PROC NAME(REAL DEPTH, REAL
    /// COUNT) (controllers siemens.md 8, 12 rule 1; controller-mapping 6, SUB=BEGIN and CALL + ARG).
    /// </summary>
    public static string ProcLine(SiemensBlock write, Section sub, string name)
    {
        IReadOnlyList<string> parameters = write.File.ParametersOf(sub);
        if (parameters.Count == 0)
        {
            return "PROC " + name;
        }

        // A parameter is a variable of the subprogram and takes its SINUMERIK name (language 4.9, ARG).
        var declared = new List<string>();
        foreach (string parameter in parameters)
        {
            declared.Add("REAL " + (SiemensExpressions.VariableName(write, parameter) ?? parameter));
        }

        return "PROC " + name + "(" + string.Join(", ", declared) + ")";
    }

    /// <summary>
    /// EXTERN NAME(REAL, REAL) for every subprogram with parameters that the blocks of a unit call (controllers
    /// siemens.md 8).
    /// </summary>
    public static void WriteExterns(SiemensBlock write, Section unit, string fileStem)
    {
        foreach (Section sub in write.File.CalledWithParameters(unit))
        {
            var types = new List<string>();
            for (int index = 0; index < write.File.ParametersOf(sub).Count; index++)
            {
                types.Add("REAL");
            }

            write.Write("EXTERN " + write.File.UnitNameOf(sub, fileStem, write) + "(" + string.Join(", ", types)
                + ")");
        }
    }
}
