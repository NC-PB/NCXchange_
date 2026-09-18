using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The flow of custom macro B (controllers fanuc.md 1, 7; controller-mapping 6; language 4.9): LABEL as the block
/// number N that GOTO names, JUMP as GOTO or IF [ ] GOTO, JUMP=END as the program end, the M99 loop of a main program,
/// VAR as #n = expression through [variables] map, CALL as M98 P L, or G65 P with the letter arguments of ARG. The
/// structured forms are never reconstructed (phase 3, P3-06).
/// </summary>
internal static class FanucFlow
{
    // The local variables of G65 and the letters that set them (controllers fanuc.md 7): #1 is A, #2 B, ...
    private static readonly Dictionary<int, string> s_letters = new()
    {
        [1] = "A",
        [2] = "B",
        [3] = "C",
        [4] = "I",
        [5] = "J",
        [6] = "K",
        [7] = "D",
        [8] = "E",
        [9] = "F",
        [11] = "H",
        [13] = "M",
        [17] = "Q",
        [18] = "R",
        [19] = "S",
        [20] = "T",
        [21] = "U",
        [22] = "V",
        [23] = "W",
        [24] = "X",
        [25] = "Y",
        [26] = "Z",
    };

    /// <summary>
    /// A LABEL is the block number of a line of its own, the target of GOTO (controllers fanuc.md 1: N block numbers
    /// matter as jump targets); the label of the M99 loop writes nothing (D210). A jump reaches the label with the
    /// modal codes of the block it jumps from, so the target state is unknown after it.
    /// </summary>
    public static void WriteLabel(FanucBlock write)
    {
        if (write.Block.Find("LABEL") is not Word label)
        {
            return;
        }

        write.Written(label);
        string name = label.Value.ToCanonical();
        if (name == write.Labels.LoopLabel)
        {
            return;
        }

        if (write.Labels.NumberOf(name) is int number)
        {
            write.Writer("N" + number.ToString(CultureInfo.InvariantCulture));
        }

        // The STATIC walk does not follow JUMP (virtual machine 1), so the codes the target state holds at the label
        // are those of the block before it, and a jump arrives with those of its own block: every modal code, G0 or
        // G1, G90 or G91, F, D, S, stands again at its next use (language 2 rules 2 and 3; controllers fanuc.md 10
        // rule 1). The M99 loop returns to the start block of the program, whose lines run again.
        if (write.Labels.IsJumpTarget(name))
        {
            write.MakeTargetUnknown();
        }
    }

    /// <summary>
    /// The variables, the jump and the call of the block, in lines after the main line.
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        foreach (Word word in block.Words)
        {
            if (word.Key == "VAR" && word.Addr is string name)
            {
                write.Written(word);
                WriteVariable(write, name, word.Value);
            }
        }

        write.Written("IF");
        if (block.Find("JUMP") is Word jump)
        {
            write.Written(jump);
            WriteJump(write, jump);
        }

        if (block.Find("CALL") is Word call)
        {
            write.Written(call);
            write.Written("ARG");
            write.Written("TIMES");
            WriteCall(write, call);
        }

        if (block.Find("REPEAT") is Word repeat)
        {
            // TODO(question): controller-mapping 6 writes REPEAT with TIMES as "counter variable and IF GOTO
            // (compiler)" and names no variable for the counter, which may clash with a variable of the program; the
            // block is an ERROR until that is answered (D212, D213).
            write.Written(repeat);
            write.Written("TIMES");
            write.Error(DiagnosticCodes.FanucRepeatNotWritten,
                "REPEAT needs a counter variable and IF GOTO on Fanuc, and no document names the variable that holds "
                + "the count (controller-mapping 6, REPEAT + TIMES).");
        }
    }

    // #n = e assigns (controllers fanuc.md 7): a V name is #n (D33), a Heidenhain name the variable of [variables] map
    // (machine-config 7).
    private static void WriteVariable(FanucBlock write, string name, Value value)
    {
        string? variable = FanucExpressions.Variable(write, name, null);
        string? text = value switch
        {
            IntegerValue or DecimalValue => value.ToCanonical(),
            ExprValue { Tree: not null } expression => FanucExpressions.Expression(write, expression.Tree),
            _ => null,
        };
        if (text is null && value is not ExprValue)
        {
            write.Error(DiagnosticCodes.FanucExpressionNotWritable,
                $"VAR:{name}={value.ToCanonical()}: custom macro B holds numbers only (controllers fanuc.md 7).");
        }

        if (variable is not null && text is not null)
        {
            write.Write(variable + " = " + text);
        }
    }

    // GOTO n to the block Nn, IF [c] GOTO n under the condition (controllers fanuc.md 7; controller-mapping 6);
    // JUMP=END ends the program, as a GOTO to the block with M30 does (controller-mapping 1, JUMP=END; D88); the jump
    // to the label after the header is the M99 of a main program, which jumps to its first block (controller-mapping
    // 6; D210, recommendation).
    private static void WriteJump(FanucBlock write, Word jump)
    {
        string target = jump.Value.ToCanonical();
        string? condition = write.Block.Find("IF")?.Value is ExprValue { Tree: not null } test
            ? FanucExpressions.Condition(write, test.Tree)
            : null;
        bool conditional = write.Block.Has("IF");
        if (conditional && condition is null)
        {
            return;
        }

        if (target == "END")
        {
            if (!conditional)
            {
                FanucProgramFrame.WriteEnd(write, "");
            }
            else if (write.Labels.EndLabel is int end)
            {
                write.Write("IF " + condition + " GOTO " + end.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                write.Error(DiagnosticCodes.FanucConditionalEndInSub,
                    "A conditional JUMP=END in a subprogram has no Fanuc form: a GOTO stays in its program "
                    + "(controllers fanuc.md 7; language 4.9, JUMP).");
            }

            return;
        }

        if (!conditional && target == write.Labels.LoopLabel)
        {
            write.Write("M99");
            return;
        }

        if (write.Labels.NumberOf(target) is not int number)
        {
            return;
        }

        string jumpText = "GOTO " + number.ToString(CultureInfo.InvariantCulture);
        write.Write(conditional ? "IF " + condition + " " + jumpText : jumpText);
    }

    // M98 P L calls a subprogram L times, G65 P with the letter arguments a macro (controllers fanuc.md 1, 7;
    // controller-mapping 6, CALL and TIMES, CALL and ARG); a subprogram is called by its O number, an external program
    // by the number of its file name O9010 (language 4.9).
    // TODO(question): D215: the file name of CALL="name" is open; a name O followed by digits is called by its number,
    // any other external name is an ERROR, until D215 is answered.
    private static void WriteCall(FanucBlock write, Word call)
    {
        if (write.Block.Has("IF"))
        {
            write.Error(DiagnosticCodes.FanucConditionalCall,
                "IF with CALL has no Fanuc form: IF [ ] THEN assigns and IF [ ] GOTO jumps (controllers fanuc.md 7).");
            return;
        }

        string name = call.Value is StringValue external ? external.Content : call.Value.ToCanonical();
        string digits = name.Length > 1 && name[0] == 'O' ? name.Substring(1) : name;
        if (digits.Length == 0 || digits.Length > 8 || !digits.All(char.IsAsciiDigit))
        {
            write.Error(DiagnosticCodes.FanucCallOfNoProgramNumber,
                $"CALL={call.Value.ToCanonical()} names no Fanuc program number, which M98 P and G65 P call "
                + "(controllers fanuc.md 1, 7).");
            return;
        }

        string program = digits.TrimStart('0').PadLeft(4, '0');
        var words = new List<string>();
        bool macro = write.Block.Has("ARG");
        words.Add(macro ? "G65" : "M98");
        words.Add("P" + program);
        if (write.Block.Find("TIMES") is Word times
            && FanucExpressions.WordValue(write, "L", times.Value, 1m) is string count)
        {
            words.Add("L" + count);
        }

        if (macro && !AddArguments(write, words))
        {
            return;
        }

        write.Write(string.Join(" ", words));
    }

    // ARG:Vn is the letter that sets #n in the standard table (controllers fanuc.md 7; language 4.9).
    private static bool AddArguments(FanucBlock write, List<string> words)
    {
        foreach (Word argument in write.Block.Words)
        {
            if (argument.Key != "ARG")
            {
                continue;
            }

            string? name = argument.Addr;
            int local = 0;
            bool isLocal = name is not null && name.Length > 1 && name[0] == 'V'
                && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out local);
            if (!isLocal || !s_letters.TryGetValue(local, out string? letter))
            {
                write.Error(DiagnosticCodes.FanucArgumentWithoutLetter,
                    $"ARG:{name} has no letter of G65: the letters set #1 to #26 by the standard table (controllers "
                    + "fanuc.md 7).");
                return false;
            }

            if (FanucExpressions.WordValue(write, "X", argument.Value, 1m) is string value)
            {
                words.Add(letter + value);
            }
        }

        return true;
    }
}
