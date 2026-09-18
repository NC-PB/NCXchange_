using Ncx.Core.Machine;
using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 1 of controllers siemens.md 12, the units of the output, and what every block shares: the comments, the skip
/// levels, RAW:SIEMENS verbatim (rule 6), the name of a unit, the block numbers.
/// </summary>
public sealed class SiemensProgramFrameTests
{
    // A program and the subprogram it calls, each with the words of its unit.
    private const string ProgramWithSub = """
        FILE=BEGIN NCX=1
        PROGRAM=BEGIN NAME="SHAFT"
        FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
        RAPID X=0 Y=0 Z=10
        CALL=100
        PROGRAM=END
        SUB=BEGIN NAME=100
        RAPID IX=5
        SUB=END
        FILE=END
        """;

    // Two programs on the channels 1 and 2 without NAME, which both take the name of the file, T.
    private const string TwoUnnamedPrograms = """
        FILE=BEGIN NCX=1
        PROGRAM=BEGIN CHANNEL=1
        FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
        RAPID X=1
        PROGRAM=END
        PROGRAM=BEGIN CHANNEL=2
        FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
        RAPID X=2
        PROGRAM=END
        FILE=END
        """;

    // Siemens 12 rule 1: one output file per NCX file with the %_N_NAME_MPF and _SPF headers under one_file; PROC NAME
    // at the top of every subprogram and RET, sub_end, at its end; M30, program_end, at the program end; a subprogram
    // named by a number is L100 (siemens 8).
    [Fact]
    public void ProgramLayoutOneFile_ProgramAndSubprogram_AreUnitsWithTheirHeadersInOneFile()
    {
        CompileResult result = Run(ProgramWithSub, Mill());

        Assert.Equal("T.mpf", Assert.Single(result.Files).Name);
        Assert.Equal(Lines(
            "%_N_SHAFT_MPF",
            "G17 G40 G90 G94 G71",
            "G0 X0 Y0 Z10",
            "L100",
            "M30",
            "%_N_L100_SPF",
            "PROC L100",
            "G0 X=IC(5)",
            "RET"), TextOf(result));
    }

    // Siemens 12 rule 1: under program_layout = "file_per_program" one file per unit, named after it.
    [Fact]
    public void ProgramLayoutFilePerProgram_EveryUnit_IsAFileOfItsOwn()
    {
        CompileResult result = Run(ProgramWithSub, Replaced(MillFile, "program_layout = \"one_file\"",
            "program_layout = \"file_per_program\""));

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(["SHAFT.mpf", "L100.spf"], result.Files.Select(file => file.Name));
        Assert.Equal(["G17 G40 G90 G94 G71", "G0 X0 Y0 Z10", "L100", "M30"], LinesOf(result.Files[0].Text));
        Assert.Equal(["PROC L100", "G0 X=IC(5)", "RET"], LinesOf(result.Files[1].Text));
    }

    // Siemens 12 rule 1: one file per unit, so two programs without NAME, which both take the name of the file, are two
    // units; the later one is written as T_2, as the framework names the files of two programs of one name, with a
    // WARNING (language 2 rule 8: nothing is dropped silently).
    [Fact]
    public void ProgramLayoutFilePerProgram_TwoProgramsOfOneName_AreTwoFilesWithAWarning()
    {
        CompileResult result = Run(TwoUnnamedPrograms, TwoChannels("file_per_program"));

        Assert.Equal(["T.mpf", "T_2.mpf"], result.Files.Select(file => file.Name));
        Assert.Equal(["G17 G40 G90 G94 G71", "G0 X1", "M30"], LinesOf(result.Files[0].Text));
        Assert.Equal(["G17 G40 G90 G94 G71", "G0 X2", "M30"], LinesOf(result.Files[1].Text));
        Assert.Equal(["CMP503"], CompilerCodes(result));
    }

    // Siemens 1: the units of an archive are named apart as well, %_N_T_MPF and %_N_T_2_MPF, with the WARNING.
    [Fact]
    public void ProgramLayoutOneFile_TwoProgramsOfOneName_AreTwoUnitsWithAWarning()
    {
        CompileResult result = Run(TwoUnnamedPrograms, TwoChannels("one_file"));

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "G0 X1", "M30", "%_N_T_2_MPF", "G17 G40 G90 G94 G71",
            "G0 X2", "M30"), TextOf(result));
        Assert.Equal(["CMP503"], CompilerCodes(result));
    }

    // Siemens 12 rule 1: RET or M17 per sub_end at the end of a subprogram (machine-config 2).
    [Fact]
    public void SubEndM17_Subprogram_EndsWithM17()
    {
        CompileResult result = Run(ProgramWithSub, Replaced(MillFile, "sub_end = \"RET\"", "sub_end = \"M17\""));

        Assert.EndsWith(Lines("PROC L100", "G0 X=IC(5)", "M17"), TextOf(result), StringComparison.Ordinal);
    }

    // Siemens 12 rule 1 with siemens 8: a subprogram with parameters declares them in its PROC, REAL each, the calls
    // give them by position, empty where a call gives none, and the caller announces it with EXTERN.
    [Fact]
    public void SubprogramWithArguments_ProcDeclaresThemAndTheCallerAnnouncesItWithExtern()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=ROW ARG:DEPTH=5 ARG:PITCH=2.5
            CALL=ROW ARG:PITCH=3
            PROGRAM=END
            SUB=BEGIN NAME=ROW
            VAR:R1={$DEPTH * 2}
            SUB=END
            FILE=END
            """;

        Assert.Equal(Lines(
            "%_N_MAIN_MPF",
            "EXTERN ROW(REAL, REAL)",
            "G17 G40 G90 G94 G71",
            "ROW(5,2.5)",
            "ROW(,3)",
            "M30",
            "%_N_ROW_SPF",
            "PROC ROW(REAL DEPTH, REAL PITCH)",
            "R1=DEPTH*2",
            "RET"), TextOf(Run(program, Mill())));
    }

    // Siemens 1: a SINUMERIK name holds letters, digits and underscores; another character is an underscore, with a
    // WARNING.
    [Fact]
    public void ProgramName_WithABlank_IsWrittenWithAnUnderscoreAndAWarning()
    {
        CompileResult result = Run(Program(MillHeader).Replace("NAME=\"T\"", "NAME=\"2.5D fraesen\"",
            StringComparison.Ordinal), Mill());

        Assert.StartsWith("%_N_2_5D_FRAESEN_MPF\n", TextOf(result), StringComparison.Ordinal);
        Assert.Equal(["CMP501"], CompilerCodes(result));
    }

    // Machine-config 2: block numbers per block_numbers on every block, not on the unit header, the PROC, a comment
    // line or a skipped line.
    [Fact]
    public void BlockNumbers_OfTheMachine_StandOnTheBlocksButNotOnHeadersCommentsAndSkippedLines()
    {
        CompileResult result = Run(Program(MillHeader, "COMMENT=\"ROUGH\"", "SKIP RAPID X=10"), Mill());

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal("%_N_T_MPF\r\nN10 G17 G40 G90 G94 G71\r\n; ROUGH\r\n/G0 X10\r\nN20 M30\r\n",
            Assert.Single(result.Files).Text);
    }

    // Controller-mapping 1, COMMENT and SECTION: a comment with a semicolon, SECTION a plain comment on the other
    // controllers (language 4.1), in the charset of the machine (machine-config 2).
    [Fact]
    public void CommentAndSection_AreSemicolonLinesInTheCharsetOfTheMachine()
    {
        Assert.Equal(Lines("; SCHRUPPEN", "; AENDERUNG"),
            MillBody("SECTION=\"SCHRUPPEN\"", "COMMENT=\"ÄNDERUNG\""));
    }

    // Controller-mapping 1, SKIP: / skips on the first level, /n on level n (siemens 1).
    [Fact]
    public void Skip_FirstAndThirdLevel_IsTheSlashInFrontOfTheBlock()
    {
        Assert.Equal(Lines("/G0 X10", "/3G0 X20"), MillBody("SKIP RAPID X=10", "SKIP=3 RAPID X=20"));
    }

    // Siemens 12 rule 6: RAW:SIEMENS blocks verbatim, with their own N number where they carry one; block_numbers
    // gives that number no other line, since N numbers must be unique for the block search (siemens 1).
    [Fact]
    public void RawSiemens_IsWrittenVerbatim()
    {
        CompileResult result = Run(Program(MillHeader, "RAW:SIEMENS=\"MSG(\\\"ROUGHING\\\")\"",
            "RAW:SIEMENS=\"N20 STOPRE\""), Mill());

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal("%_N_T_MPF\r\nN10 G17 G40 G90 G94 G71\r\nN30 MSG(\"ROUGHING\")\r\nN20 STOPRE\r\nN40 M30\r\n",
            Assert.Single(result.Files).Text);
    }

    // Siemens 1: N numbers must be unique for the block search. A RAW line keeps the number of its source block,
    // skipped or not (siemens 12 rule 6), and the numbering of block_numbers passes over every number a line of the
    // file carries, so that each number names one block (machine-config 2).
    [Fact]
    public void BlockNumbers_BesideRawLinesWithNumbersOfTheirOwn_AreUnique()
    {
        CompileResult result = Run(Program(MillHeader, "RAPID X=0", "RAW:SIEMENS=\"N20 MSG(\\\"OP1\\\")\"",
            "LINE X=10 F=100", "RAW:SIEMENS=\"/N40 STOPRE\"", "RAPID X=20"), Mill());

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        string text = Assert.Single(result.Files).Text;
        Assert.Equal(Lines(
            "%_N_T_MPF",
            "N10 G17 G40 G90 G94 G71",
            "N30 G0 X0",
            "N20 MSG(\"OP1\")",
            "N50 G1 G90 G94 X10 F100",
            "/N40 STOPRE",
            "N60 G0 G90 X20",
            "N70 M30"), text.ReplaceLineEndings("\n"));
        List<string> numbers = BlockNumbersOf(text);
        Assert.Equal(numbers.Distinct().Count(), numbers.Count);
    }

    // Language 4.1, RAW: RAW of another controller compiles only to it, an ERROR here (D5, CMP001 of the framework).
    [Fact]
    public void RawFanuc_OnASiemensMachine_IsAnError()
    {
        CompileResult result = Run(Program(MillHeader, "RAW:FANUC=\"G411 X1. I9090\""), Mill());

        Assert.Equal(["CMP001"], CompilerCodes(result));
        Assert.Empty(result.Files);
    }

    // Machine-config 5, ROTARY_FEED_MM_MIN: without a template in [transform] the option is not written, with a
    // WARNING (D86).
    [Fact]
    public void RotaryFeed_WithoutATemplate_IsAWarningAndNotWritten()
    {
        CompileResult result = Run(Program(MillHeader, "ROTARY_FEED=MM_MIN"), Mill());

        Assert.Equal(["CMP533"], CompilerCodes(result));
    }

    // The N numbers of the lines of a text, the skipped ones too, in their order (siemens 1).
    private static List<string> BlockNumbersOf(string text)
    {
        var numbers = new List<string>();
        foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string unskipped = line.TrimStart('/');
            if (unskipped.StartsWith('N') && unskipped.Length > 1 && char.IsAsciiDigit(unskipped[1]))
            {
                numbers.Add(new string(unskipped.Skip(1).TakeWhile(char.IsAsciiDigit).ToArray()));
            }
        }

        return numbers;
    }

    // The mill with the channels 1 and 2 and a program_layout.
    private static MachineConfig TwoChannels(string layout)
    {
        return Variant(MillFile, text => text
            .Replace("program_layout = \"one_file\"", "program_layout = \"" + layout + "\"", StringComparison.Ordinal)
            .Replace("channels = [1]", "channels = [1, 2]", StringComparison.Ordinal));
    }
}
