using Ncx.Acceptance.Cli;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Siemens;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The Siemens reader end to end (phase 5, P5-01): a hand-written 840D sl program with the constructs of the task
/// converts through ncx convert with machines/siemens-840dsl-mill.toml by name, its STATIC check finds no ERROR, its
/// output formats to itself and keeps RAW only for MSG, WORKPIECE and STOPRE (controllers siemens.md 11 rule 8). Under
/// NCX_CORPUS every .mpf of the corpus converts without a crash, and the Hermle C22 U program and the Burkhardt+Weber
/// programs read into programs that format to themselves; the report of the batch runner (P3-07) under
/// tests/corpus-reports/ is the evidence that they convert without loss.
/// </summary>
public sealed class SiemensReaderTests : IDisposable
{
    // A main program and a subprogram in one archive with the constructs of P5-01: the unit headers and ;$PATH, N
    // numbers, G groups, AC() and IC(), CHF and RND, CR, I=AC(), CIP, CT, AR, FB, the polar coordinates, ANG, G58,
    // ATRANS, AROT, AMIRROR, TRANS, CYCLE800, MCALL with CYCLE81 and CYCLE83 and HOLES1, CYCLE832, R parameters, FOR,
    // IF ELSE, WHILE, REPEAT UNTIL, CASE, PROC with a parameter and its calls, G53 with D0, SPOS, G74, and the RAW
    // statements MSG, WORKPIECE and STOPRE.
    private const string HandWritten = """
        %_N_P501_MPF
        ;$PATH=/_N_WKS_DIR/_N_P501_WPD
        ; Hand-written 840D sl program for the Siemens reader, P5-01
        N10 G17 G90 G94 G71
        N20 WORKPIECE(,"",,"BOX",112,0,-50,-80,0,0,100,100)
        N30 MSG("ROUGHING")
        N40 T1
        N50 M6
        N60 D1
        N70 G54
        N80 S3000 M3
        N90 G0 X0 Y0 Z10
        N100 G1 Z-2 F500
        N110 G1 X=AC(40) Y=IC(0) CHF=2
        N120 G1 Y40 RND=3
        N130 G1 X10
        N140 G2 X0 Y30 CR=10
        N150 G3 X10 Y20 I=AC(10) J=AC(30)
        N160 G1 X30 Y20
        N170 CIP X30 Y0 I1=40 J1=10
        N180 CT X20 Y-10
        N190 G2 AR=90 I=AC(20) J=AC(0)
        N200 G1 X0 Y0 FB=200
        N210 G111 X20 Y20
        N220 G1 AP=0 RP=10
        N230 G3 AP=90
        N240 G1 ANG=45 X40
        N250 G0 Z10
        N260 G58 X5
        N270 ATRANS Y5
        N280 AROT RPL=30
        N290 AMIRROR X0
        N300 TRANS
        N310 CYCLE800(1,"TABLE",0,57,0,0,0,0,30,0,0,0,0,0,100,1)
        N320 CYCLE800()
        N330 G0 X10 Y10
        N340 F200
        N350 MCALL CYCLE81(10,0,2,-5,)
        N360 X20 Y20
        N370 HOLES1(0,0,0,10,10,3)
        N380 MCALL
        N390 MCALL CYCLE83(10,0,2,-30,,5,,1,0,0,1,1,3)
        N400 X40 Y40
        N410 MCALL
        N420 CYCLE832(0.02,1,0.1)
        N430 R1=0
        N440 FOR R2=1 TO 3
        N450 R1=R1+R2
        N460 ENDFOR
        N470 IF R1==6
        N480 G0 X1
        N490 ELSE
        N500 G0 X2
        N510 ENDIF
        N520 WHILE R1>0
        N530 R1=R1-1
        N540 ENDWHILE
        N550 REPEAT
        N560 R1=R1+1
        N570 UNTIL R1>=2
        N580 CASE(R1) OF 1 GOTOF CASE_A DEFAULT GOTOF CASE_B
        N590 CASE_A: G0 Y1
        N600 CASE_B: G0 Y2
        N610 POCK(2.5)
        N620 POCK P2
        N630 CYCLE832(0,0,1)
        N640 STOPRE
        N650 G53 G0 Z0 D0
        N660 M5
        N670 SPOS=0
        N680 G74 Z1=0
        N690 M30
        %_N_POCK_SPF
        PROC POCK(REAL DEPTH=2)
        N10 G0 X0 Y0 Z5
        N20 G1 Z=-DEPTH F100
        N30 G0 Z5
        N40 RET

        """;

    // Blocks the constructs of the hand-written program read into, one or more per construct (controllers siemens.md
    // 11; controller-mapping, the Siemens column; D31, D58, D82, D83, D157).
    private static readonly string[] s_constructs =
    [
        "PROGRAM=BEGIN NAME=\"P501\"", "; $PATH=/_N_WKS_DIR/_N_P501_WPD", "PRELOAD=1", "TOOL=1", "OFFSET=1", "ORIGIN=1",
        "SPINDLE=CW RPM=3000", "LINE X=38.586 Y=0", "LINE X=40 Y=1.414", "ARC=CCW X=37 Y=40 R=3",
        "ARC=CW X=0 Y=30 R=10", "ARC=CCW X=10 Y=20 CENTER:X=10 CENTER:Y=30", "ARC=CW X=30 Y=0 CENTER:X=30 CENTER:Y=10",
        "ARC=CCW X=20 Y=-10 CENTER:X=30 CENTER:Y=-10", "ARC=CW CENTER:X=20 CENTER:Y=0 ANGLE=90", "LINE X=0 Y=0 F=200",
        "LINE X=30 Y=20 F=500", "ARC=CCW X=20 Y=30 CENTER:X=20 CENTER:Y=20", "LINE X=40 Y=50", "SHIFT X=5",
        "SHIFT Y=5", "ROTATE=30", "MIRROR=X", "SHIFT=RESET", "RETRACT", "TILT A=0 B=30 C=0 MOVE=STAY", "TILT=RESET",
        "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=200", "CYCLE_CALL X=20 Y=20",
        "CYCLE_CALL X=30 Y=0", "CYCLE=OFF",
        "CYCLE=PECK AXIS=Z SURFACE=0 CLEARANCE=2 DEPTH=-30 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=200 CYCLE_DWELL=0",
        "TOLERANCE=0.02 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF", "VAR:R1={$R1 + $R2}", "JUMP=FOR1_END IF={$R2 > (3)}",
        "JUMP=IF2_ELSE IF={NOT ($R1 == 6)}", "JUMP=WHILE3_END IF={NOT ($R1 > 0)}",
        "JUMP=REPEAT4 IF={NOT ($R1 >= 2)}", "JUMP=CASE_A IF={($R1) == 1}", "LABEL=CASE_B", "CALL=POCK ARG:DEPTH=2.5",
        "CALL=POCK ARG:DEPTH=2 TIMES=2", "RAPID Z=0 OFFSET=0 FRAME=MACHINE", "SPINDLE=OFF", "ORIENT=0", "HOME Z",
        "SUB=BEGIN NAME=POCK", "LINE Z={-$DEPTH} F=100", "SUB=END",
    ];

    // A second program with the constructs of P5-01 the first one leaves out: DEF, EXTERN and DEFINE in the definition
    // part, T="name" with D1 before M6, a macro, CHR, RNDM, TURN, G4 F and G4 S, the skip levels, G505, G59, ROT RPL,
    // MIRROR, ROTS, SCALE, G500, G153, SUPA, GOTOB, LOOP, REPEAT label P=, the ranges of REPEAT start end and REPEATB
    // as SUB sections, the call of a subprogram EXTERN announces, L100, CALL, EXTCALL, G75, G74, PRESETON, GOTOS with
    // IF, T0 then M6, T=0 before M30, and a subprogram that ends with M17.
    private const string HandWrittenSecond = """
        %_N_P501B_MPF
        ;$PATH=/_N_WKS_DIR/_N_P501_WPD
        ; Second hand-written 840D sl program for the Siemens reader, P5-01
        N10 DEF INT COUNT=2
        N20 EXTERN SUBEXT(REAL)
        N30 DEFINE SPINDLE_ON AS M3
        N40 G17 G90 G94 G71
        N50 G505
        N60 T="Mill_D10" D1
        N70 M6
        N80 S3000 SPINDLE_ON
        N90 G0 X0 Y0 Z10
        N100 G1 Z-1 F300
        N110 G1 X20 CHR=1
        N120 G1 Y20 RNDM=2
        N130 G1 X0
        N140 G1 Y0 RNDM=0
        N150 G0 Z10
        N160 G0 X30 Y0
        N170 G2 X30 Y0 I5 J0 TURN=1 Z5 F200
        N180 G4 F1
        N190 G4 S10
        /N200 G0 X1
        /2 N210 G0 X2
        N220 G59 X5
        N230 ROT RPL=30
        N240 MIRROR X0
        N250 ROTS X10 Y20
        N260 SCALE X2
        N270 G500
        N280 G54
        N290 G153 G0 Z0
        N300 SUPA G0 Z0
        N310 G0 X10 Y10 Z10
        N320 BACK: G0 X=COUNT
        N330 COUNT=COUNT-1
        N340 IF COUNT>0 GOTOB BACK
        N350 LOOP
        N360 IF COUNT<=0 GOTOF OUT
        N370 COUNT=COUNT-1
        N380 ENDLOOP
        N390 OUT: G0 Z20
        N400 ANF: G0 X1
        N410 ENDE: G0 X2
        N420 REPEAT ANF P=1
        N430 G0 Y5
        N440 REPEAT ANF ENDE P=2
        N450 REPEATB N430 P=2
        N460 SUBEXT(1.5)
        N470 L100
        N480 CALL "L100"
        N490 EXTCALL("/_N_EXT_DIR/_N_EXT1_SPF")
        N500 G75 FP=1 Z1=0
        N510 G74 X1=0
        N520 PRESETON(X,0)
        N530 IF COUNT==99 GOTOS
        N540 T0
        N550 M6
        N560 T1 D1
        N570 M6
        N580 M5
        N590 T=0
        N600 M30
        %_N_L100_SPF
        N10 G0 X0 Y0
        N20 G1 Z-1 F100
        N30 G0 Z10
        N40 M17

        """;

    // Blocks the constructs of the second program read into (controllers siemens.md 3 to 8, 11; controller-mapping 1 to
    // 3, 6, REPEAT + TIMES; D58, D212).
    private static readonly string[] s_secondConstructs =
    [
        "VAR:COUNT=2", "; N30 DEFINE SPINDLE_ON AS M3", "ORIGIN=5", "PRELOAD=\"Mill_D10\" OFFSET=1",
        "TOOL=\"Mill_D10\"", "SPINDLE=CW RPM=3000", "LINE X=19 Y=0", "LINE X=20 Y=1", "ARC=CCW X=18 Y=20 R=2",
        "ARC=CW Z=5 CENTER:X=35 CENTER:Y=0 ANGLE=720 F=200", "DWELL=1", "DWELL=0.2", "SKIP RAPID X=1",
        "SKIP=2 RAPID X=2", "SHIFT X=5", "ROTATE=30", "MIRROR=X", "TILT A=10 B=20 C=0", "ORIGIN=0",
        "RAPID Z=0 FRAME=MACHINE", "LABEL=BACK", "JUMP=BACK IF={$COUNT > 0}", "LABEL=LOOP1", "JUMP=LOOP1",
        "LABEL=LOOP1_END", "TIMES=1 REPEAT=ANF", "CALL=REPEAT_ANF_ENDE TIMES=2", "CALL=REPEAT_N430 TIMES=2",
        "CALL=\"SUBEXT\" ARG:P1=1.5", "CALL=L100", "CALL=\"EXT1\"", "HOME Z POINT=1", "HOME X", "SETPOS X=0",
        "LABEL=START", "JUMP=START IF={$COUNT == 99}", "PRELOAD=0", "TOOL=0", "PRELOAD=1 OFFSET=1", "TOOL=1",
        "SUB=BEGIN NAME=REPEAT_ANF_ENDE", "SUB=BEGIN NAME=REPEAT_N430", "SUB=BEGIN NAME=L100", "SUB=END",
    ];

    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // P5-01: the hand-written 840D sl program converts with siemens-840dsl-mill.toml, and the STATIC check of the
    // command finds no ERROR; the output formats to itself (D91).
    [Fact]
    public void Convert_HandWrittenProgram_ConvertsAndChecksWithoutError()
    {
        AssertConvertsAndChecksWithoutError("P501.mpf", HandWritten);
    }

    // P5-01: the second hand-written program converts and checks without ERROR the same way, its tool changes, its
    // flow and its SUB sections of repeated ranges included (D91).
    [Fact]
    public void Convert_SecondHandWrittenProgram_ConvertsAndChecksWithoutError()
    {
        AssertConvertsAndChecksWithoutError("P501B.mpf", HandWrittenSecond);
    }

    // P5-01: every construct of the second program reads into its NCX words; RAW stays only for EXTERN, which
    // announces a subprogram outside the file to the control, and the factors of SCALE, which have no NCX word (D5).
    [Fact]
    public void Read_SecondHandWrittenProgram_ReadsEveryConstructAndKeepsRawWhereNcxHasNoWord()
    {
        string[] lines = NcxWriter.Write(new SiemensReader().Read(new SourceFile("P501B.mpf", HandWrittenSecond),
            Mill(), new ReadOptions())).Split('\n');

        foreach (string construct in s_secondConstructs)
        {
            Assert.Contains(construct, lines);
        }

        Assert.Equal(["RAW:SIEMENS=\"N20 EXTERN SUBEXT(REAL)\"", "RAW:SIEMENS=\"SCALE X2\""],
            lines.Where(line => line.StartsWith("RAW", StringComparison.Ordinal)));
    }

    // P5-01: every construct of the hand-written program reads into its NCX words, and RAW stays where siemens 11 rule
    // 8 keeps it: MSG, WORKPIECE and STOPRE.
    [Fact]
    public void Read_HandWrittenProgram_ReadsEveryConstructAndKeepsRawWhereTheDocumentSays()
    {
        string[] lines = NcxWriter.Write(new SiemensReader().Read(new SourceFile("P501.mpf", HandWritten), Mill(),
            new ReadOptions())).Split('\n');

        foreach (string construct in s_constructs)
        {
            Assert.Contains(construct, lines);
        }

        Assert.Equal(["RAW:SIEMENS=\"N20 WORKPIECE(,\\\"\\\",,\\\"BOX\\\",112,0,-50,-80,0,0,100,100)\"",
            "RAW:SIEMENS=\"N30 MSG(\\\"ROUGHING\\\")\"", "RAW:SIEMENS=\"N640 STOPRE\""],
            lines.Where(line => line.StartsWith("RAW", StringComparison.Ordinal)));
    }

    // Under NCX_CORPUS every .mpf of the corpus converts without a crash, the STATIC check of its program included
    // (P5-01 done when; controllers sample-corpus 3: a crash is a bug). The files are read against the 840D sl mill,
    // the batch of P3-07 reads them against the closest machine.
    [CorpusFact]
    public void Convert_EveryMpfOfTheCorpus_NeverCrashes()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        using var project = new ProjectHarness(Fixture.RepositoryRoot());
        ReaderRegistry readers = Program.Readers();
        var crashes = new List<string>();
        int files = 0;
        foreach (string path in Directory.EnumerateFiles(corpus, "*.mpf", SearchOption.AllDirectories))
        {
            files++;
            using var output = new StringWriter();
            using var error = new StringWriter();
            var settings = new RunSettings
            {
                File = path,
                MachineFile = "siemens-840dsl-mill",
                WorkingDirectory = project.WorkingDirectory,
                ToolFolder = project.ToolFolder,
            };
            try
            {
                ConvertCommand.Run(settings, null, readers, output, error);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
                or FormatException or IndexOutOfRangeException or OverflowException or NullReferenceException
                or KeyNotFoundException or InvalidCastException)
            {
                // The test lists every file that crashes, not only the first.
                crashes.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(files > 0, $"The corpus under {corpus} holds no .mpf file.");
        Assert.True(crashes.Count == 0, $"{crashes.Count} of {files} files crash:\n" + string.Join('\n', crashes));
    }

    // Under NCX_CORPUS the Hermle C22 U program (CYCLE800 with the kinematics "HERMLE") and the Burkhardt+Weber
    // programs ($P_UIFR writes and NEWCOORD) read into programs that parse back and format to themselves, without an
    // ERROR of the reader (P5-01 done when; controllers controller-mapping 11).
    [CorpusFact]
    public void Read_HermleAndBurkhardtWeberPrograms_FormatToThemselves()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        var hermle = new List<string>();
        var burkhardtWeber = new List<string>();
        foreach (string path in Directory.EnumerateFiles(corpus, "*.mpf", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            if (source.Contains("\"HERMLE\"", StringComparison.OrdinalIgnoreCase))
            {
                hermle.Add(path);
            }
            else if (source.Contains("$P_UIFR", StringComparison.OrdinalIgnoreCase)
                && source.Contains("NEWCOORD", StringComparison.OrdinalIgnoreCase))
            {
                burkhardtWeber.Add(path);
            }
        }

        Assert.True(hermle.Count > 0, "The corpus holds no Hermle program with CYCLE800(..., \"HERMLE\", ...).");
        Assert.True(burkhardtWeber.Count > 0, "The corpus holds no Burkhardt+Weber program with $P_UIFR and NEWCOORD.");
        var problems = new List<string>();
        foreach (string path in hermle.Concat(burkhardtWeber))
        {
            NcxProgram read = new SiemensReader().Read(new SourceFile(Path.GetFileName(path), File.ReadAllText(path)),
                Mill(), new ReadOptions());
            string text = NcxWriter.Write(read);
            NcxProgram parsed = Parser.Parse(text, path, new ParserOptions());
            if (read.Diagnostics.HasErrors || parsed.Diagnostics.Items.Count > 0 || NcxWriter.Write(parsed) != text)
            {
                problems.Add($"{path}: {read.Diagnostics.ToText()}{parsed.Diagnostics.ToText()}");
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    // ncx convert --machine siemens-840dsl-mill converts the program with exit code 0, its STATIC check finds no ERROR,
    // and the output parses back and formats to itself (D91).
    private void AssertConvertsAndChecksWithoutError(string fileName, string source)
    {
        string file = _cli.WriteFile(fileName, source);

        int exitCode = _cli.Run("convert", file, "--machine", "siemens-840dsl-mill");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
        NcxProgram parsed = Parser.Parse(_cli.Output, Path.ChangeExtension(fileName, ".ncx"), new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(_cli.Output, NcxWriter.Write(parsed));
    }

    // The 840D sl mill of the hand-written program, loaded by its path as ncx convert --machine siemens-840dsl-mill
    // loads it, with the Siemens cycle catalog its [cycles] names (machine-config 6).
    private static MachineConfig Mill()
    {
        const string relativePath = "machines/siemens-840dsl-mill.toml";
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "siemens.toml"), Controller.Siemens, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }
}
