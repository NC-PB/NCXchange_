using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The frame of a Klartext program (controllers heidenhain.md 1; 8 rule 1; controller-mapping 1; language 4.1, 4.13):
/// BEGIN PGM name MM from the program name and the units, M30 per program_end for PROGRAM=END, END PGM name MM after
/// it and after the subprograms placed behind it, the sub_end LBL 0 for SUB=END, the comments as ; and * - blocks, and
/// RAW of the controller as the text it keeps. The block numbers are consecutive from 0, [format] block_numbers.
/// </summary>
internal static partial class HeidenhainProgramFrame
{
    // ; starts a comment block, * - a structuring block (controllers heidenhain.md 1; controller-mapping 1, COMMENT and
    // SECTION).
    private const string CommentMark = "; ";
    private const string SectionMark = "* - ";

    /// <summary>
    /// A comment block of Klartext: ; text.
    /// </summary>
    public static string CommentLine(string text)
    {
        return CommentMark + text;
    }

    /// <summary>
    /// Writes BEGIN PGM name MM from the name of the program and its units (heidenhain 8 rule 1); the file is named
    /// after the program (controllers heidenhain.md 1).
    /// </summary>
    public static void WriteBegin(HeidenhainBlock writing, Section program, ChannelSnapshot state)
    {
        if (UnitsOf(state) is not string units)
        {
            writing.Error(DiagnosticCodes.HeidenhainProgramWithoutUnits,
                "BEGIN PGM needs the units of the program, MM or INCH, and the program sets no UNITS (controllers "
                + "heidenhain.md 8 rule 1).");
            return;
        }

        writing.Line("BEGIN PGM " + NameOf(writing, program) + " " + units);
    }

    /// <summary>
    /// Writes END PGM name MM, after the M30 and the subprograms placed behind it (heidenhain 8 rules 1 and 6), with
    /// the units of its BEGIN PGM: BEGIN PGM name MM opens and END PGM name MM closes (controllers heidenhain.md 1),
    /// and a UNITS that changes them is reported where it stands (TakeUnits).
    /// </summary>
    public static void WriteEndOfProgram(HeidenhainBlock writing, Section program)
    {
        if (UnitsOf(writing.LookAhead.HeaderState(program)) is string units)
        {
            writing.Line("END PGM " + NameOf(writing, program) + " " + units);
        }
    }

    /// <summary>
    /// Writes the structure and the comments of a block: FILE=BEGIN and FILE=END are the start and the end of the file
    /// and write nothing, PROGRAM=BEGIN is BEGIN PGM (WriteBegin), SUB=BEGIN is LBL n, SECTION and COMMENT are the
    /// comment blocks, RAW the text the reader kept.
    /// </summary>
    public static void WriteStart(HeidenhainBlock writing)
    {
        writing.Take("SKIP");
        writing.Take("FILE");
        writing.Take("NCX");
        if (writing.Block.Has("PROGRAM", null, "BEGIN"))
        {
            // Klartext has one program per file, which BEGIN PGM names, no program number and one channel
            // (controller-mapping 1, PROGRAM=BEGIN; 7, CHANNEL).
            writing.Take("PROGRAM");
            writing.Take("NAME");
            writing.Take("NUMBER");
            writing.Take("CHANNEL");
        }
        else if (writing.Block.Has("SUB", null, "BEGIN"))
        {
            HeidenhainSubprograms.WriteBegin(writing);
        }

        TakeUnits(writing);
        WriteComments(writing);
        WriteRaw(writing);
    }

    /// <summary>
    /// Writes the end of a section: PROGRAM=END as program_end, M30, with the LBL of JUMP=END before it
    /// (controller-mapping 1, JUMP=END; D49); SUB=END as sub_end, LBL 0 (machine-config 2; heidenhain 8 rule 6).
    /// </summary>
    public static void WriteEnd(HeidenhainBlock writing)
    {
        if (writing.Block.Has("PROGRAM", null, "END"))
        {
            writing.Take("PROGRAM");
            if (HeidenhainFlow.JumpsToTheEnd(writing))
            {
                writing.Line("LBL " + HeidenhainFlow.EndLabel(writing).ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            writing.Template(writing.Machine.Format?.ProgramEnd, "[format] program_end (machine-config 2, D49)",
                new TemplateValues());
        }
        else if (writing.Block.Has("SUB", null, "END"))
        {
            writing.Take("SUB");
            WriteSubEnd(writing);
        }
    }

    /// <summary>
    /// Writes sub_end, "written for SUB=END and RETURN", LBL 0 on Heidenhain (machine-config 2).
    /// </summary>
    public static void WriteSubEnd(HeidenhainBlock writing)
    {
        writing.Template(writing.Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)", new TemplateValues());
    }

    // BEGIN PGM gives the units of the whole program (controllers heidenhain.md 1; controller-mapping 1, UNITS), the
    // LBL sections of its subprograms included, which stand between its M30 and its END PGM (heidenhain 8 rules 1, 6):
    // a UNITS that keeps the units of the BEGIN PGM of the file being written writes nothing, one that changes them has
    // no Klartext form, in the program and in the walk of a subprogram it calls alike.
    private static void TakeUnits(HeidenhainBlock writing)
    {
        if (writing.Take("UNITS") is not Word units || ProgramOf(writing) is not Section program)
        {
            return;
        }

        if (UnitsOf(writing.After) != UnitsOf(writing.LookAhead.HeaderState(program)))
        {
            writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                $"{units.ToCanonical()} changes the units that BEGIN PGM gives the program {NameOf(writing, program)}, "
                + "which Klartext keeps for the whole program and the LBL sections in its file (controllers "
                + "heidenhain.md 1; 8 rules 1 and 6).");
        }
    }

    // The program whose file the block is written into (program_layout = "file_per_program", D48): its own program,
    // or, in the walk of a subprogram, the program whose walk entered it, the last PROGRAM=BEGIN before it in walk
    // order (architecture 8, D99). The walk of a subprogram that no program calls follows the PROGRAM=END of the last
    // program and belongs to none: that subprogram stands in no output file, with the WARNING of the framework and its
    // TODO(question) (CompilerBase.FilePerProgram), and its UNITS has no BEGIN PGM to be compared with.
    private static Section? ProgramOf(HeidenhainBlock writing)
    {
        if (writing.Step.Section is Section { Kind: SectionKind.Program } own)
        {
            return own;
        }

        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = writing.Step.Index - 1; index >= 0; index--)
        {
            Block block = steps[index].Block;
            if (block.Has("PROGRAM", null, "END"))
            {
                return null;
            }

            if (block.Has("PROGRAM", null, "BEGIN"))
            {
                return steps[index].Section;
            }
        }

        return null;
    }

    // SECTION as the structuring block * - text, COMMENT as the comment block ; text, both in the charset of the
    // machine (controller-mapping 1, COMMENT and SECTION; machine-config 2).
    private static void WriteComments(HeidenhainBlock writing)
    {
        if (writing.Take("SECTION")?.Value is StringValue section)
        {
            writing.Line(SectionMark + writing.CommentText(section.Content));
        }

        if (writing.Take("COMMENT")?.Value is StringValue comment)
        {
            writing.Line(CommentMark + writing.CommentText(comment.Content));
        }
    }

    // RAW of the controller is written as the reader kept it (language 4.1, D5), without the block number of the
    // source: the compiler numbers the blocks consecutively itself (heidenhain 8 rule 1; heidenhain 1: the numbers are
    // part of the text, and LBL, not block numbers, are the jump targets). RAW of another controller stopped the
    // compile before (CMP001).
    private static void WriteRaw(HeidenhainBlock writing)
    {
        foreach (Word raw in writing.TakeAll("RAW"))
        {
            if (raw.Value is StringValue text)
            {
                writing.Line(SourceBlockNumber().Replace(text.Content, ""));
            }
        }
    }

    private static string? UnitsOf(ChannelSnapshot state)
    {
        return state.Frame.Units switch
        {
            Units.Mm => "MM",
            Units.Inch => "INCH",
            _ => null,
        };
    }

    // The name of the program, the name of the NCX file for a program without NAME, as the framework names its file.
    private static string NameOf(HeidenhainBlock writing, Section program)
    {
        return program.Name ?? Path.GetFileNameWithoutExtension(writing.Diagnostics.File);
    }

    // The block number in front of a Klartext block, 2 of "2 BLK FORM 0.1 Z X0 Y0 Z-20".
    [GeneratedRegex(@"^\s*[0-9]+(?=[\s/])\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SourceBlockNumber();
}
