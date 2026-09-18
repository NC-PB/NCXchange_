using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The frame of a Fanuc file (controllers fanuc.md 1, 10 rule 3; controller-mapping 1, 6): % opens and closes the file,
/// O with four digits and the name in parentheses begins a program, the start block of the machine follows,
/// program_end ends it; a subprogram is an O program of its own ending with sub_end, which the framework places after
/// the end of the program that calls it (D99).
/// </summary>
internal static class FanucProgramFrame
{
    // Oxxxx: four digits, eight on the 30i (controllers fanuc.md 1).
    private const int LeastDigits = 4;
    private const int MostDigits = 8;

    /// <summary>
    /// Writes a block of the structure of the file: FILE, PROGRAM, SUB and RETURN; false for any other block.
    /// </summary>
    public static bool Write(FanucBlock write)
    {
        Block block = write.Block;
        if (block.Has("FILE"))
        {
            // % opens and closes the file (controllers fanuc.md 1; controller-mapping 1, FILE=BEGIN and FILE=END).
            write.Written("FILE");
            write.Written("NCX");
            write.Write("%");
            return true;
        }

        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            WriteProgramBegin(write);
            return true;
        }

        if (block.Has("PROGRAM", null, "END"))
        {
            WriteProgramEnd(write);
            return true;
        }

        if (block.Has("SUB", null, "BEGIN"))
        {
            WriteSubBegin(write);
            return true;
        }

        if (block.Has("SUB", null, "END") || block.Has("RETURN"))
        {
            WriteReturn(write);
            return true;
        }

        return false;
    }

    /// <summary>
    /// A COMMENT or SECTION of the block as a comment line in parentheses, transliterated per comment_charset
    /// (controllers fanuc.md 10 rule 5; controller-mapping 1, COMMENT).
    /// </summary>
    public static void WriteComment(FanucBlock write)
    {
        foreach (string key in new[] { "COMMENT", "SECTION" })
        {
            if (write.Block.Find(key)?.Value is StringValue text)
            {
                write.Writer("(" + write.CommentText(text.Content) + ")");
                write.Written(key);
            }
        }
    }

    /// <summary>
    /// The end of the program as program_end writes it, M30 or M2 (D48, D49), with the block number a conditional
    /// JUMP=END names.
    /// </summary>
    public static void WriteEnd(FanucBlock write, string prefix)
    {
        string? end = write.Render(write.Machine.Format?.ProgramEnd, "[format] program_end (machine-config 2, D49)",
            new TemplateValues());
        if (end is not null)
        {
            write.Write(prefix + end);
        }
    }

    // O with the program number and the name in parentheses, the name shown on the screen (controllers fanuc.md 1;
    // controller-mapping 1, PROGRAM=BEGIN); then the start block of the machine.
    // TODO(question): a program without NUMBER has no O number, and no document says what the Fanuc compiler writes
    // for it; it is an ERROR until that is answered.
    private static void WriteProgramBegin(FanucBlock write)
    {
        foreach (string key in new[] { "PROGRAM", "NAME", "NUMBER", "CHANNEL" })
        {
            write.Written(key);
        }

        int? number = write.Step.Section?.Number;
        if (number is not int programNumber)
        {
            write.Error(DiagnosticCodes.FanucProgramWithoutNumber,
                "The program has no NUMBER, and a Fanuc program is named by its O number (controllers fanuc.md 1; "
                + "language 4.1, NUMBER).");
            return;
        }

        string? name = write.Step.Section?.Name;
        string o = ProgramNumber(write, programNumber.ToString(CultureInfo.InvariantCulture));
        write.Write(name is null ? o : o + " (" + write.CommentText(name) + ")");
        WriteHeader(write);
    }

    // The start block (controllers fanuc.md 1: N10 G0 G40, N20 G80 G90 G94 G98): the lines of [format] header, whose G
    // codes the control has active afterwards; without one G90, which fanuc 10 rule 1 writes once in the header, where
    // the G-code system has it. The units of units_default are active on the control at the start (controller-mapping
    // 1, UNITS: G20/G21, else TOML).
    // TODO(question): D34 has the writers of NCX emit a complete header, and no document says whether the Fanuc
    // compiler writes G20/G21 when UNITS equals the units_default of the machine; the control starts in units_default,
    // so G21 is written only where the program's units differ, until that is answered.
    private static void WriteHeader(FanucBlock write)
    {
        if (write.Machine.Machine.UnitsDefault is string units)
        {
            string? code = FanucCodes.UnitsCode(units == "INCH"
                ? Ncx.Core.VirtualMachine.State.Units.Inch
                : Ncx.Core.VirtualMachine.State.Units.Mm, write.System);
            if (code is not null)
            {
                write.Target.Set(FanucCodes.Units, code);
            }
        }

        string? header = write.Machine.Format?.Header;
        if (header is null)
        {
            if (write.System != GcodeSystem.A)
            {
                write.Write("G90");
                write.Target.Set(FanucCodes.Distance, "G90");
            }

            return;
        }

        string? text = write.Render(header, "[format] header", new TemplateValues());
        if (text is null)
        {
            return;
        }

        foreach (string line in text.Split('\n'))
        {
            write.Write(line.Trim());
            foreach (string token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (FanucCodes.GroupOf(token, write.System) is string group)
                {
                    write.Target.Set(group, token);
                }
            }
        }
    }

    // PROGRAM=END writes program_end, with the block number of a conditional JUMP=END in front (controllers fanuc.md
    // 10 rule 3; language 4.9, JUMP=END).
    private static void WriteProgramEnd(FanucBlock write)
    {
        // A length offset that still waits for the tool axis stands before the end, never dropped (language 2 rule 8;
        // the TODO(question) of OutputFormat).
        write.Written("PROGRAM");
        FanucToolWords.WriteWaitingLength(write);
        string prefix = write.Labels.EndLabel is int end ? "N" + end.ToString(CultureInfo.InvariantCulture) + " " : "";
        WriteEnd(write, prefix);
    }

    // A subprogram is an O program of its own, its NAME the program number (controllers fanuc.md 1; controller-mapping
    // 6, SUB).
    private static void WriteSubBegin(FanucBlock write)
    {
        write.Written("SUB");
        write.Written("NAME");
        string? name = write.Step.Section?.Name;
        if (name is null || name.Length == 0 || name.Length > MostDigits || !name.All(char.IsAsciiDigit))
        {
            write.Error(DiagnosticCodes.FanucSubNameNotAProgramNumber,
                $"SUB {name} has no Fanuc program number: a subprogram is an O program named by up to eight digits "
                + "(controllers fanuc.md 1).");
            return;
        }

        write.Write(ProgramNumber(write, name));
    }

    // SUB=END and RETURN write sub_end, M99 (machine-config 2); RETURN in a program is treated as JUMP=END (virtual
    // machine 3.6) and writes program_end.
    private static void WriteReturn(FanucBlock write)
    {
        write.Written("SUB");
        write.Written("RETURN");
        write.Written("SKIP");
        FanucToolWords.WriteWaitingLength(write);
        if (write.Step.Section?.Kind == SectionKind.Program)
        {
            WriteEnd(write, "");
            return;
        }

        string? end = write.Render(write.Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)",
            new TemplateValues());
        if (end is not null)
        {
            write.Write(end);
        }
    }

    // O with at least four digits, O0001 (controllers fanuc.md 1).
    private static string ProgramNumber(FanucBlock write, string digits)
    {
        string trimmed = digits.TrimStart('0');
        if (trimmed.Length > MostDigits)
        {
            write.Error(DiagnosticCodes.FanucSubNameNotAProgramNumber,
                $"O{digits} has more than eight digits (controllers fanuc.md 1).");
        }

        return "O" + trimmed.PadLeft(LeastDigits, '0');
    }
}
