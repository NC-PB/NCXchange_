using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// Labels, jumps, repeats and variables in Klartext (controllers heidenhain.md 1, 6; controller-mapping 1 and 6;
/// language 4.9): LABEL=n as LBL n, JUMP with IF as FN 9 to FN 12 IF a comparison b GOTO LBL n, JUMP without IF as
/// the jump whose condition always holds, JUMP=END to an LBL before the M30, RETURN as the sub_end LBL 0 in a
/// subprogram and as JUMP=END in a program, REPEAT=n TIMES=k as CALL LBL n REP k, and VAR:Qn as the formula Qn = ...
/// of the Q parameter.
/// </summary>
internal static class HeidenhainFlow
{
    // The jump whose condition always holds, FN 9 with two equal values (controllers heidenhain.md 6).
    private const string AlwaysTrue = "FN 9: IF +0 EQU +0";

    // TODO(question): heidenhain.md 1 gives LBL "NAME" for newer controls without saying whether the iTNC 530 is one;
    // a label or subprogram NAME that is no number is written in that form.

    /// <summary>
    /// A label or subprogram name as LBL writes it: the number, LBL 5, or the name in quotes, LBL "DRILL_ROW"
    /// (controllers heidenhain.md 1).
    /// </summary>
    public static string Label(Value value)
    {
        return value switch
        {
            IntegerValue number => number.Number.ToString(CultureInfo.InvariantCulture),
            StringValue name => "\"" + name.Content + "\"",
            _ => "\"" + value.ToCanonical() + "\"",
        };
    }

    /// <summary>
    /// Writes LABEL=n as LBL n where the block begins, the target of a jump and the start of a repeat (controller-
    /// mapping 6, LABEL).
    /// </summary>
    public static void WriteLabel(HeidenhainBlock writing)
    {
        if (writing.Take("LABEL") is Word label)
        {
            writing.Line("LBL " + Label(label.Value));
        }
    }

    /// <summary>
    /// Writes VAR:Qn=value as the formula of the Q parameter, Q1 = Q1 + 20 (controllers heidenhain.md 6;
    /// controller-mapping 6, VAR).
    /// </summary>
    public static void WriteVariables(HeidenhainBlock writing)
    {
        foreach (Word variable in writing.TakeAll("VAR"))
        {
            if (variable.Addr is not string name || !HeidenhainFormula.IsParameter(name))
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"{variable.ToCanonical()}: Klartext names its variables Q, QL and QR, and the machine "
                    + "configuration maps no other name to them (controllers heidenhain.md 6; language 4.9).");
                continue;
            }

            if (FormulaOf(writing, variable) is string formula)
            {
                writing.Line(name + " = " + formula);
            }
        }
    }

    /// <summary>
    /// Writes the flow words at the end of the block: REPEAT as CALL LBL n REP k, RETURN as the sub_end of the machine
    /// in a subprogram and as JUMP=END in a program, JUMP as FN 9 to FN 12.
    /// </summary>
    public static void WriteJump(HeidenhainBlock writing)
    {
        WriteRepeat(writing);

        // RETURN first: it pops the call, or ends a program (virtual machine 3.6), so a JUMP of the same block (RETURN
        // plus JUMP, D221) is written after it, where neither the virtual machine nor the control reaches it.
        var labels = new List<string>();
        if (ReturnLabel(writing) is string end)
        {
            labels.Add(end);
        }

        // JUMP=END continues at the end of the program: FN 9 to FN 12 to an LBL that ends with M30 (controller-mapping
        // 1, JUMP=END), the label the end of the program writes before its M30 (HeidenhainProgramFrame.WriteEnd).
        if (writing.Take("JUMP") is Word jump)
        {
            labels.Add(jump.Value is IdentValue { Name: "END" }
                ? EndLabel(writing).ToString(CultureInfo.InvariantCulture)
                : Label(jump.Value));
        }

        if (labels.Count == 0)
        {
            return;
        }

        string? test = writing.Take("IF") is Word condition ? Condition(writing, condition) : AlwaysTrue;
        if (test is null)
        {
            return;
        }

        foreach (string label in labels)
        {
            writing.Line(test + " GOTO LBL " + label);
        }
    }

    /// <summary>
    /// The formula of the value of a VAR or ARG word: a number as written, an expression as the Klartext formula; null
    /// when it has none, which is reported.
    /// </summary>
    public static string? FormulaOf(HeidenhainBlock writing, Word word)
    {
        string? problem = "a string has no formula of numbers";
        string? formula = word.Value switch
        {
            IntegerValue or DecimalValue => word.Value.ToCanonical().Replace(".", writing.Numbers.DecimalSeparator,
                StringComparison.Ordinal),
            ExprValue { Tree: ExprNode tree } => HeidenhainFormula.Of(tree, writing.Numbers.DecimalSeparator,
                out problem),
            _ => null,
        };
        if (formula is null)
        {
            writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
                $"{word.ToCanonical()}: {problem}, so the Q parameter has no Klartext formula (controllers "
                + "heidenhain.md 6).");
        }

        return formula;
    }

    /// <summary>
    /// Tells whether the program of the block jumps to its end: JUMP=END in its walk, or a RETURN of the program
    /// itself, which is treated as JUMP=END (language 4.9; virtual machine 3.6); its LBL stands before the M30
    /// (controller-mapping 1, JUMP=END).
    /// </summary>
    public static bool JumpsToTheEnd(HeidenhainBlock writing)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = writing.Step.Index - 1; index >= 0; index--)
        {
            BlockStep step = steps[index];
            bool returns = step.Section is Section { Kind: SectionKind.Program } && step.Block.Has("RETURN");
            if (returns || step.Block.Has("JUMP", null, "END"))
            {
                return true;
            }

            if (step.Block.Has("PROGRAM", null, "BEGIN"))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// The number of the LBL before the M30 that JUMP=END jumps to: one more than every numbered LABEL and SUB of the
    /// file, so that it names no other label of the output file, where the LBL sections of the subprograms stand next
    /// to the program's labels (controllers heidenhain.md 1).
    /// </summary>
    public static long EndLabel(HeidenhainBlock writing)
    {
        long highest = 0;
        foreach (BlockStep step in writing.LookAhead.Steps)
        {
            bool section = step.Block.Has("SUB", null, "BEGIN");
            foreach (Word word in step.Block.Words)
            {
                bool names = word.Key == "LABEL" || (section && word.Key == "NAME");
                if (names && word.Value is IntegerValue number && number.Number > highest)
                {
                    highest = number.Number;
                }
            }
        }

        return highest + 1;
    }

    // FN 9: IF +Q1 EQU +Q3, FN 10 unequal, FN 11 greater, FN 12 less (controllers heidenhain.md 6), with two values,
    // each a number or a Q parameter with its sign; <=, >=, AND, OR and NOT have no FN function.
    // TODO(question): heidenhain.md 6 writes the comparison of FN 9 as EQU and examples/PATTERN_LOOP.ncx that of FN 12
    // as LT; no document gives the words of FN 10 and FN 11, which are written NE and GT.
    private static string? Condition(HeidenhainBlock writing, Word condition)
    {
        string separator = writing.Numbers.DecimalSeparator;
        if (condition.Value is ExprValue { Tree: ExprNode tree }
            && HeidenhainFormula.Unwrapped(tree) is BinaryNode comparison)
        {
            string? function = comparison.Operator switch
            {
                BinaryOperator.Equal => "FN 9: IF {0} EQU {1}",
                BinaryOperator.NotEqual => "FN 10: IF {0} NE {1}",
                BinaryOperator.Greater => "FN 11: IF {0} GT {1}",
                BinaryOperator.Less => "FN 12: IF {0} LT {1}",
                _ => null,
            };
            string? left = HeidenhainFormula.Operand(comparison.Left, separator);
            string? right = HeidenhainFormula.Operand(comparison.Right, separator);
            if (function is not null && left is not null && right is not null)
            {
                return string.Format(CultureInfo.InvariantCulture, function, left, right);
            }
        }

        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"{condition.ToCanonical()}: FN 9 to FN 12 compare two values, numbers or Q parameters, for equal, "
            + "unequal, greater or less (controllers heidenhain.md 6); the condition has no Klartext form.");
        return null;
    }

    // CALL LBL n REP k repeats the part between LBL n and the call k more times (controllers heidenhain.md 1;
    // controller-mapping 6, REPEAT).
    // TODO(question): D212: REPEAT without TIMES is open; as its recommendation says, the compiler writes the count 1.
    private static void WriteRepeat(HeidenhainBlock writing)
    {
        if (writing.Take("REPEAT") is not Word repeat)
        {
            return;
        }

        string count = "1";
        if (writing.Take("TIMES") is Word times)
        {
            if (times.Value is not IntegerValue number)
            {
                HeidenhainNumbers.ReportValue(writing, times);
                return;
            }

            count = number.Number.ToString(CultureInfo.InvariantCulture);
        }

        writing.Line("CALL LBL " + Label(repeat.Value) + " REP " + count);
    }

    // RETURN returns to the caller before SUB=END is reached (language 4.9, 4.13), and sub_end is "written for SUB=END
    // and RETURN" (machine-config 2): in a subprogram the LBL 0 of the machine where the RETURN stands, which ends the
    // call where the control reaches it (controllers heidenhain.md 1). In a program, which no caller entered, RETURN is
    // treated as JUMP=END (language 4.9; virtual machine 3.6): the label of the LBL before the M30 is returned, for
    // WriteJump to write the jump to it with the IF of the block.
    // TODO(question): heidenhain 7 rule 3 reads "LBL n closed by LBL 0" as a subprogram, and no document says how a
    // reader takes an LBL 0 before the one that closes the section, which sub_end for RETURN writes; the compiler
    // writes it as machine-config 2 says.
    // TODO(question): D221: IF needs the JUMP or CALL of its block (language 5 rule 5) and makes the whole block
    // conditional (language 4.9: the block executes when the expression is not 0), so a RETURN under IF in a
    // subprogram returns only when the condition holds, and LBL 0 takes no condition; it is reported, as a CALL under
    // IF is.
    private static string? ReturnLabel(HeidenhainBlock writing)
    {
        if (writing.Take("RETURN") is not Word ret)
        {
            return null;
        }

        if (writing.Step.Section is Section { Kind: SectionKind.Program })
        {
            return EndLabel(writing).ToString(CultureInfo.InvariantCulture);
        }

        if (writing.Block.Find("IF") is Word condition)
        {
            writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                $"{ret.ToCanonical()} under {condition.ToCanonical()} returns from the subprogram only when the "
                + "condition holds, and the sub_end of the machine, LBL 0, takes no condition, so nothing is written "
                + "for the return (language 4.9; machine-config 2).");
            return null;
        }

        HeidenhainProgramFrame.WriteSubEnd(writing);
        return null;
    }
}
