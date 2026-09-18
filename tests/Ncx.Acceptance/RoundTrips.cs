using Ncx.Acceptance.Cli;
using Ncx.Acceptance.Examples;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance;

/// <summary>
/// The round trips of phase 3, the matrix of milestone M6 in one place (implementation 13, P3-07; architecture 11;
/// code-guidelines 8), every chain run through the command line as the user runs it, ncx convert, ncx compile, ncx
/// format and ncx check with the machines by name:
/// <list type="bullet">
/// <item>2.5D_FRAESEN: converted from both sources and compared with the blocks of the example, except the readings
/// its notes list (D217); the example compiled to both controllers and compared with the sources.</item>
/// <item>BOHREN and 3D_FRAESEN: converted from both sources and compared with the frozen expected file, which is the
/// Fanuc reading byte for byte, so that the Heidenhain reading is compared with the Fanuc one except the listed
/// readings; the frozen file compiled to the other controller and compared with its source.</item>
/// <item>Every mill source converted and compiled back to its own controller, and compared with itself: the round trip
/// without loss, except the differences the documents cannot settle, each an NcException grounded in its question.
/// The Heidenhain readings keep BLK FORM as RAW:HEIDENHAIN, which compiles to Klartext only (D5, D94), so the frozen
/// Fanuc readings stand for their programs on the Fanuc side.</item>
/// <item>The five .ncx examples format clean without a machine file (D91) and check without an ERROR without and with
/// their machine files (D103, D104); the Nakamura pair converts and checks as single files (the job is phase
/// 6).</item>
/// </list>
/// The cells that the reader and compiler tasks hold for their own milestones (ConvertExampleTests, FanucCompilerTests,
/// HeidenhainCompilerTests, ExampleFormatTests, ExampleCheckTests, ExampleMachineCheckTests) run here again through the
/// command line, with the lists of differences those tests keep, so that every list stands in one place.
/// </summary>
public sealed class RoundTrips : IDisposable
{
    private const string FanucMill = "fanuc-mill-30i";
    private const string HeidenhainMill = "heidenhain-itnc530";
    private const string Nakamura = "nakamura-ntjx";

    // The blocks of Expected/3D_FRAESEN.ncx, the Fanuc reading, that the Heidenhain reading of 3D_FRAESEN.h writes
    // otherwise, in their order: the readings the notes of 2.5D_FRAESEN list for the same header and the same ends
    // (D217): NUMBER only from O0001; BLK FORM as RAW:HEIDENHAIN (heidenhain 7 rule 9); SECTION from * - where a
    // Fanuc comment line is trivia (controller-mapping 1, D92); the offsets and the speed with TOOL CALL, not with
    // G43 H and M3 (note 2, D7; heidenhain 7 rule 2, fanuc 9 rule 2); COMP=OFF from R0; the M91 moves where G28 reads
    // as HOME (note 4).
    private static readonly KeyValuePair<string, string>[] s_heidenhainReadingOf3DFraesen =
    [
        new("PROGRAM=BEGIN NAME=\"3D FRAESEN\" NUMBER=1", "PROGRAM=BEGIN NAME=\"3D FRAESEN\""),
        new("ORIGIN=1", "ORIGIN=1\nRAW:HEIDENHAIN=\"2 BLK FORM 0.1 Z X0 Y0 Z-55\"\n"
            + "RAW:HEIDENHAIN=\"3 BLK FORM 0.2 X100 Y100 Z0\"\nSECTION=\"SIDE MILL D10 L35 SD10\""),
        new("TOOL=1", "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=3183"),
        new("SPINDLE=CW RPM=3183", "SPINDLE=CW\nSECTION=\"SCHRUPPEN\""),
        new("RAPID X=-6.964 Y=88.621", "RAPID X=-6.964 Y=88.621 COMP=OFF"),
        new("RAPID Z=2 OFFSET:LEN=1", "RAPID Z=2"),
        new("HOME Z", "RAPID Z=0 FRAME=MACHINE"),
        new("HOME X Y", "RAPID X=0 Y=0 FRAME=MACHINE"),
    ];

    private readonly CliHarness _cli = new();

    /// <summary>
    /// Every mill source with the machine of its controller: the Fanuc sources with the Fanuc mill, the Klartext
    /// sources with the iTNC 530 (implementation 13, P3-02, P3-05).
    /// </summary>
    public static TheoryData<string, string> MillSources()
    {
        return new TheoryData<string, string>
        {
            { "2.5D_FRAESEN.fanuc.nc", FanucMill },
            { "BOHREN.fanuc.nc", FanucMill },
            { "3D_FRAESEN.fanuc.nc", FanucMill },
            { "2.5D_FRAESEN.h", HeidenhainMill },
            { "BOHREN.h", HeidenhainMill },
            { "3D_FRAESEN.h", HeidenhainMill },
        };
    }

    /// <summary>
    /// The NCX program that stands for each mill program, compiled to the controller whose source it is not read from:
    /// the example for 2.5D_FRAESEN to both, the frozen Fanuc readings of BOHREN and 3D_FRAESEN to Klartext.
    /// </summary>
    public static TheoryData<string, string> ProgramsForTheOtherController()
    {
        return new TheoryData<string, string>
        {
            { "2.5D_FRAESEN.ncx", FanucMill },
            { "2.5D_FRAESEN.ncx", HeidenhainMill },
            { "BOHREN.ncx", HeidenhainMill },
            { "3D_FRAESEN.ncx", HeidenhainMill },
        };
    }

    public void Dispose()
    {
        _cli.Dispose();
    }

    // M6: 2.5D_FRAESEN reads from both sources into the blocks of the example, comments and trivia stripped on both
    // sides, except the readings the example's notes list for each source (D217; FanucReaderTests,
    // HeidenhainReaderTests).
    [Theory]
    [InlineData("2.5D_FRAESEN.fanuc.nc", FanucMill)]
    [InlineData("2.5D_FRAESEN.h", HeidenhainMill)]
    public void Convert_25DFraesenFromEitherSource_IsTheExampleExceptTheReadingsItsNotesList(string source,
        string machine)
    {
        string ncx = Convert(source, machine);

        KeyValuePair<string, string>[] reading = machine == FanucMill
            ? FanucReaderTests.s_fanucReading
            : HeidenhainReaderTests.s_heidenhainReading;
        Assert.Equal(string.Join('\n', ConvertExampleTests.ExampleBlocks(reading)),
            string.Join('\n', ConvertExampleTests.BlocksOf(ncx)));
    }

    // Implementation 13, P3-07: the Fanuc sources of BOHREN and 3D_FRAESEN read into their frozen expected files byte
    // for byte, each reviewed against the specification before it was frozen (Expected/README.md).
    [Theory]
    [InlineData("BOHREN")]
    [InlineData("3D_FRAESEN")]
    public void Convert_FanucSourceOfBohrenOr3DFraesen_IsTheFrozenExpectedFile(string program)
    {
        string ncx = Convert(program + ".fanuc.nc", FanucMill);

        CliHarness.AssertExpectedFile(program + ".ncx", ncx);
    }

    // Implementation 13, P3-07: the Klartext sources of BOHREN and 3D_FRAESEN read into the frozen expected files, the
    // Fanuc readings, except the readings the two sources write otherwise, each listed with its rule or question
    // (HeidenhainReaderTests for BOHREN, s_heidenhainReadingOf3DFraesen here).
    [Theory]
    [InlineData("BOHREN")]
    [InlineData("3D_FRAESEN")]
    public void Convert_HeidenhainSourceOfBohrenOr3DFraesen_IsTheFrozenFileExceptTheListedReadings(string program)
    {
        string frozen = ExpectedFile(program + ".ncx");

        string ncx = Convert(program + ".h", HeidenhainMill);

        List<string> expected = program == "BOHREN"
            ? HeidenhainReaderTests.BohrenReading(frozen)
            : WithReadings(ConvertExampleTests.BlocksOf(frozen), s_heidenhainReadingOf3DFraesen);
        Assert.Equal(string.Join('\n', expected), string.Join('\n', ConvertExampleTests.BlocksOf(ncx)));
    }

    // M6: every mill source converts and compiles back to its own controller without loss, under the comparison rules
    // (NcComparer), except the differences the documents cannot settle, each grounded in its question.
    [Theory]
    [MemberData(nameof(MillSources))]
    public void RoundTrip_SourceThroughNcxBackToItsController_ReproducesTheSourceExceptTheListedDifferences(
        string source, string machine)
    {
        Convert(source, machine);

        string compiled = Compile(_cli.PathOf(NcxFileOf(source)), machine);

        string? difference = new NcComparer(Machine(machine)).Compare(Fixture.ReadText("sources/" + source), compiled,
            RoundTripDifferences(source));
        Assert.True(difference is null, difference);
    }

    // M5, M6: the NCX program of each mill program compiles to the other controller, and the result equals that
    // controller's source under the comparison rules, except the differences the documents cannot settle.
    [Theory]
    [MemberData(nameof(ProgramsForTheOtherController))]
    public void Compile_ProgramForTheOtherController_IsItsSourceExceptTheListedDifferences(string program,
        string machine)
    {
        string text = program == "2.5D_FRAESEN.ncx" ? Fixture.ReadText(program) : ExpectedFile(program);
        string ncx = _cli.WriteFile(program, program == "BOHREN.ncx"
            ? HeidenhainCompilerTests.WithoutRigidTap(text)
            : text);

        string compiled = Compile(ncx, machine);

        string name = Path.GetFileNameWithoutExtension(program);
        string source = "sources/" + name + (machine == FanucMill ? ".fanuc.nc" : ".h");
        string? difference = new NcComparer(Machine(machine)).Compare(Fixture.ReadText(source), compiled,
            CompileDifferences(program, machine, text));
        Assert.True(difference is null, difference);
    }

    // D91, P0-06: every .ncx example formats clean without a machine file, ncx format --check exits 0.
    [Theory]
    [MemberData(nameof(ExampleFormatTests.Examples), MemberType = typeof(ExampleFormatTests))]
    public void Format_Example_IsCanonicalWithoutAMachineFile(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("format", file, "--check");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("", _cli.Output);
    }

    // D103: every .ncx example checks without an ERROR without a machine file, against the built-in default machine.
    [Theory]
    [MemberData(nameof(ExampleFormatTests.Examples), MemberType = typeof(ExampleFormatTests))]
    public void Check_ExampleWithoutAMachineFile_RaisesNoError(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("check", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
    }

    // D103, D104: every .ncx example checks without an ERROR with its machine files by name.
    [Theory]
    [MemberData(nameof(ExampleMachineCheckTests.ExamplesWithTheirMachines),
        MemberType = typeof(ExampleMachineCheckTests))]
    public void Check_ExampleWithItsMachine_RaisesNoError(string example, string machine)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("check", file, "--machine", machine);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
    }

    // Implementation 13, P3-07: each path of the Nakamura pair converts with its machine, and the NCX file it writes
    // checks as a single file with the machine without an ERROR; the pair as a job is phase 6 (Jobs/).
    [Theory]
    [InlineData("NAKAMURA_WY250L_O1000.path1.nc")]
    [InlineData("NAKAMURA_WY250L_O1000.path2.nc")]
    public void ConvertAndCheck_NakamuraPathAsASingleFile_RaisesNoError(string source)
    {
        Convert(source, Nakamura);

        int exitCode = _cli.Run("check", _cli.PathOf(NcxFileOf(source)), "--machine", Nakamura);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The blocks of a program as a reading gives them: each block the reading writes otherwise replaced by the lines
    /// of its reading, by none where the source has no block there, in the order the readings are listed.
    /// </summary>
    /// <param name="blocks">The blocks of the expected program, canonical and without comments.</param>
    /// <param name="reading">The blocks the reading writes otherwise, each with its lines.</param>
    internal static List<string> WithReadings(List<string> blocks, KeyValuePair<string, string>[] reading)
    {
        var expected = new List<string>();
        int difference = 0;
        foreach (string block in blocks)
        {
            if (difference < reading.Length && reading[difference].Key == block)
            {
                expected.AddRange(reading[difference].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                difference++;
                continue;
            }

            expected.Add(block);
        }

        Assert.Equal(reading.Length, difference);
        return expected;
    }

    // The differences of each round trip that the documents cannot settle, in their order. The Fanuc sources come back
    // as they are.
    private static List<NcException> RoundTripDifferences(string source)
    {
        return source switch
        {
            "2.5D_FRAESEN.h" or "3D_FRAESEN.h" => [HeidenhainCompilerTests.s_programStart],
            "BOHREN.h" =>
            [
                HeidenhainCompilerTests.s_programStart,
                ChipBreaks("24"),
                ChipBreaks("3"),
            ],
            _ => [],
        };
    }

    // The differences of each program compiled to the other controller, in their order: those HeidenhainCompilerTests
    // and FanucCompilerTests keep for the example and for BOHREN, and for 3D_FRAESEN those of the 2.5D example, the
    // M137 of the header and the stock definition the Fanuc reading has no block for (D217).
    private static List<NcException> CompileDifferences(string program, string machine, string text)
    {
        if (program == "2.5D_FRAESEN.ncx")
        {
            return machine == FanucMill
                ? [FanucCompilerTests.s_movesOfNote4]
                : HeidenhainCompilerTests.Differences25DFraesen();
        }

        return program == "BOHREN.ncx"
            ? HeidenhainCompilerTests.BohrenDifferences(text.ReplaceLineEndings("\n"))
            :
            [
                HeidenhainCompilerTests.s_programStart,
                HeidenhainCompilerTests.StockDefinition("BLK FORM 0.1 Z X0 Y0 Z-55", "BLK FORM 0.2 X100 Y100 Z0"),
            ];
    }

    // D163 with wave-1 question #20: the Klartext reader reads every cycle 203 as PECK, the first entry of the catalog
    // for it, whose Q213 it does not keep (RDR301), and the compiler writes Q213=0 for PECK; BOHREN.h writes 24 and 3.
    private static NcException ChipBreaks(string breaks)
    {
        return new NcException
        {
            Grounds = $"D163 and wave-1 question #20: cycle 203 with Q213={breaks} reads as PECK and compiles with "
                + "Q213=0",
            Source = [HeidenhainCompilerTests.Cycle203("565,487", breaks)],
            Compiled = [HeidenhainCompilerTests.Cycle203("565,487", "0")],
        };
    }

    // ncx convert of an example source with its machine by name, the NCX text into a file of the temporary folder
    // named after the source: no ERROR of the reader or of the check (architecture 7, 10).
    private string Convert(string source, string machine)
    {
        string file = _cli.CopyExample("sources/" + source);
        string ncx = _cli.PathOf(NcxFileOf(source));

        int exitCode = _cli.Run("convert", file, "--machine", machine, "--output", ncx);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
        return File.ReadAllText(ncx);
    }

    // ncx compile of an NCX file with the machine by name into a folder of its own: no ERROR, one file written
    // (architecture 8, 10).
    private string Compile(string ncx, string machine)
    {
        string folder = _cli.PathOf("out-" + machine);

        int exitCode = _cli.Run("compile", ncx, "--machine", machine, "--output", folder);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
        return File.ReadAllText(Assert.Single(Directory.GetFiles(folder)));
    }

    // The NCX file a source converts into: 2.5D_FRAESEN.fanuc.nc into 2.5D_FRAESEN.fanuc.ncx.
    private static string NcxFileOf(string source)
    {
        return Path.GetFileNameWithoutExtension(source) + ".ncx";
    }

    // A frozen expected file of tests/Ncx.Acceptance/Expected/, read through the repository root (tests/README.md).
    private static string ExpectedFile(string name)
    {
        return File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", name));
    }

    // A shipped machine file of machines/, whose [format] and controller the comparison takes.
    private static MachineConfig Machine(string name)
    {
        var diagnostics = new Diagnostics(name + ".toml");
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", name + ".toml"), diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }
}
