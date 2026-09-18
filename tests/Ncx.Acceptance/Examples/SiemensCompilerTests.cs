using Ncx.Acceptance.Cli;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Compilers;
using Ncx.Compilers.Siemens;
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
/// The Siemens compiler end to end (phase 5, P5-02): ncx compile of MILLTURN_TRANSFER.ncx with millturn1 by name
/// writes the generic words of the example's own comments and the program of
/// tests/Ncx.Acceptance/Expected/MILLTURN_TRANSFER.millturn1.mpf (D104); that program converts back through the
/// Siemens reader to the blocks of the example except the differences listed with their grounds, and compiles back to
/// itself but for its header; under NCX_CORPUS the Hermle C22 U and the Burkhardt+Weber programs of the corpus read and
/// compile back to themselves under the comparison rules (M8).
/// </summary>
public sealed class SiemensCompilerTests : IDisposable
{
    private const string Example = "MILLTURN_TRANSFER.ncx";

    // What the example's comments write for its blocks with the generic words of millturn1.toml (phase 5, P5-02
    // acceptance).
    private static readonly string[] s_genericWords = ["COUPON(S2,S1)", "M68", "G0 Z2=-58", "M2=70", "S3=6000 M3=3"];

    // The blocks of the example that read back as other blocks, each with its grounds.
    private static readonly BlockDifference[] s_differences =
    [
        new()
        {
            Grounds = "a SINUMERIK unit names no channel, the job does (controller-mapping 7, CHANNEL), and CHANNEL=1 "
                + "is the default (language 4.1)",
            Example = ["PROGRAM=BEGIN NAME=\"SHAFT_TRANSFER\" CHANNEL=1"],
            ReadBack = ["PROGRAM=BEGIN NAME=\"SHAFT_TRANSFER\""],
        },
        new()
        {
            Grounds = "the reader writes the complete header of D34 from the defaults of the configuration and the "
                + "codes of the first block of the source after it",
            Example = ["FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF"],
            ReadBack = ["FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "FEED_MODE=PER_REV WORKPLANE=ZX DIAMETER=ON"],
        },
        new()
        {
            Grounds = "SECTION is a plain comment on SINUMERIK (language 4.1), which the reader keeps as trivia (D92)",
            Example = ["SECTION=\"TURN OD\""],
            ReadBack = [],
        },
        new()
        {
            Grounds = "T3 D3: the turret is the default holder (virtual machine 3.8 rule 2), and D is one register for "
                + "length and radius, OFFSET (controller-mapping 3)",
            Example = ["TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3"],
            ReadBack = ["TOOL=3 OFFSET=3"],
        },
        new()
        {
            Grounds = "SECTION is a plain comment on SINUMERIK (language 4.1), which the reader keeps as trivia (D92)",
            Example = ["SECTION=\"TRANSFER\""],
            ReadBack = [],
        },
        new()
        {
            Grounds = "D222: the [workpiece] template of millturn1.toml is the datum G55, which the reader reads as "
                + "ORIGIN=2, and the ORIGIN=2 after it selects the datum the control has already",
            Example = ["WORKPIECE=SUB", "ORIGIN=2"],
            ReadBack = ["ORIGIN=2"],
        },
        new()
        {
            Grounds = "SECTION is a plain comment on SINUMERIK (language 4.1), which the reader keeps as trivia (D92)",
            Example = ["SECTION=\"MILL FLAT\""],
            ReadBack = [],
        },
        new()
        {
            Grounds = "T12 D12 is the change template of its own line and S3=6000 M3=3 the main line (machine-config "
                + "3), the turret the default holder and D one register (controller-mapping 3)",
            Example = ["TOOL:TURRET1=12 OFFSET:LEN=12 OFFSET:RAD=12 SPINDLE:TOOL=CW RPM:TOOL=6000"],
            ReadBack = ["TOOL=12 OFFSET=12", "SPINDLE:TOOL=CW RPM:TOOL=6000"],
        },
        new()
        {
            Grounds = "C is the axis of the workpiece holder, C2 after WORKPIECE=SUB (virtual machine 3.8 rule 3), "
                + "written with its letter C2= (D30), which the reader keeps as the machine axis C2",
            Example = ["RAPID X=20 C=0"],
            ReadBack = ["RAPID X=20 C2=0"],
        },
        new()
        {
            Grounds = "C is the axis of the workpiece holder, C2 after WORKPIECE=SUB (virtual machine 3.8 rule 3), "
                + "written with its letter C2= (D30), which the reader keeps as the machine axis C2",
            Example = ["LINE C=90 F=300"],
            ReadBack = ["LINE C2=90 F=300"],
        },
    ];

    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());

    public void Dispose()
    {
        _project.Dispose();
    }

    // Phase 5, P5-02: ncx compile MILLTURN_TRANSFER.ncx --machine millturn1 writes a program with the generic words of
    // the example's own comments, COUPON(S2,S1), M68, G0 Z2=-58, M2=70, S3=6000 M3=3 (D104), the whole program as
    // tests/Ncx.Acceptance/Expected/MILLTURN_TRANSFER.millturn1.mpf has it.
    [Fact]
    public void Compile_MillturnTransferWithMillturn1_WritesTheGenericWordsOfTheExampleComments()
    {
        string file = _project.WriteInWorkingDirectory(Example, Fixture.ReadText(Example));
        string output = Path.Combine(_project.WorkingDirectory, "out");
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = file,
            MachineFile = "millturn1",
            WorkingDirectory = _project.WorkingDirectory,
            ToolFolder = _project.ToolFolder,
        };

        int exitCode = CompileCommand.Run(settings, output, Program.Compilers(), error);

        Assert.True(exitCode == 0, error.ToString());
        Assert.DoesNotContain(": ERROR ", error.ToString(), StringComparison.Ordinal);
        string compiled = File.ReadAllText(Path.Combine(output, "MILLTURN_TRANSFER.mpf"));
        List<string> lines = LinesWithoutNumbers(compiled);
        foreach (string words in s_genericWords)
        {
            Assert.Contains(words, lines);
        }

        CliHarness.AssertExpectedFile("MILLTURN_TRANSFER.millturn1.mpf", compiled);
    }

    // Phase 5, P5-02: the program the compiler writes converts back through the Siemens reader to the blocks of the
    // example, except the differences listed with their grounds, and checks without an ERROR (virtual machine 1).
    [Fact]
    public void Compile_MillturnTransfer_ConvertsBackToTheBlocksOfTheExampleExceptTheListedDifferences()
    {
        MachineConfig machine = MillTurn();
        string compiled = Compile(Fixture.ReadText(Example), machine);

        NcxProgram read = new SiemensReader().Read(new SourceFile("MILLTURN_TRANSFER.mpf", compiled), machine,
            new ReadOptions());

        Assert.False(read.Diagnostics.HasErrors, read.Diagnostics.ToText());
        List<string> example = BlocksOf(Parser.Parse(Fixture.ReadText(Example), Example, new ParserOptions()));
        List<string> readBack = BlocksOf(read);
        Assert.Equal(Expected(example), readBack);
    }

    // Phase 5, P5-02: what the reader reads back compiles to the same program, but for the header, which the reader
    // writes from the defaults of D34 first, and the comments of SECTION, which the reader keeps as trivia (D92) and no
    // compiler writes.
    [Fact]
    public void Compile_MillturnTransferReadBack_CompilesToTheSameProgramButItsHeader()
    {
        MachineConfig machine = MillTurn();
        string compiled = Compile(Fixture.ReadText(Example), machine);
        NcxProgram read = new SiemensReader().Read(new SourceFile("MILLTURN_TRANSFER.mpf", compiled), machine,
            new ReadOptions());

        string again = Compile(NcxWriter.Write(read), machine);

        Assert.Equal(AfterHeader(LinesWithoutNumbers(compiled)), AfterHeader(LinesWithoutNumbers(again)));
    }

    // Phase 5 exit, M8: under NCX_CORPUS the Hermle C22 U program and the Burkhardt+Weber programs read and compile
    // back to themselves under the comparison rules of phase 3 (block numbers, comments and blank lines left out,
    // numbers compared by value, the modal lines before the first motion compared per G group).
    [CorpusFact]
    public void RoundTrip_HermleAndBurkhardtWeberPrograms_CompileBackToThemselves()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        MachineConfig machine = Mill();
        var problems = new List<string>();
        int programs = 0;
        foreach (string path in Directory.EnumerateFiles(corpus, "*.mpf", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            bool hermle = source.Contains("\"HERMLE\"", StringComparison.OrdinalIgnoreCase);
            bool burkhardtWeber = source.Contains("$P_UIFR", StringComparison.OrdinalIgnoreCase)
                && source.Contains("NEWCOORD", StringComparison.OrdinalIgnoreCase);
            if (!hermle && !burkhardtWeber)
            {
                continue;
            }

            programs++;
            NcxProgram read = new SiemensReader().Read(new SourceFile(Path.GetFileName(path), source), machine,
                new ReadOptions());
            CompileResult result = ((ICompiler)new SiemensCompiler()).Compile(
                Parser.Parse(NcxWriter.Write(read), path, new ParserOptions()), machine, new CompileOptions());
            if (result.Diagnostics.HasErrors || result.Files.Count != 1)
            {
                problems.Add($"{path}: {result.Diagnostics.ToText()}");
                continue;
            }

            if (SiemensEquivalence.Compare(source, result.Files[0].Text) is string difference)
            {
                problems.Add($"{path}:\n{difference}");
            }
        }

        Assert.True(programs > 0, "The corpus holds no Hermle or Burkhardt+Weber program.");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    // The example with every listed difference applied, in the order of the example.
    private static List<string> Expected(List<string> example)
    {
        var expected = new List<string>(example);
        foreach (BlockDifference difference in s_differences)
        {
            int index = IndexOf(expected, difference.Example);
            Assert.True(index >= 0, $"The difference ({difference.Grounds}) finds no block {difference.Example[0]}.");
            expected.RemoveRange(index, difference.Example.Count);
            expected.InsertRange(index, difference.ReadBack);
        }

        return expected;
    }

    private static int IndexOf(List<string> blocks, IReadOnlyList<string> sequence)
    {
        for (int index = 0; index + sequence.Count <= blocks.Count; index++)
        {
            bool found = true;
            for (int offset = 0; offset < sequence.Count && found; offset++)
            {
                found = blocks[index + offset] == sequence[offset];
            }

            if (found)
            {
                return index;
            }
        }

        return -1;
    }

    // The blocks of a program as canonical NCX writes their words, without their comments and without the trivia.
    private static List<string> BlocksOf(NcxProgram program)
    {
        var blocks = new List<string>();
        foreach (Block block in program.Blocks)
        {
            var words = new List<string>();
            foreach (Word word in block.Words)
            {
                words.Add(word.ToCanonical());
            }

            blocks.Add(string.Join(" ", words));
        }

        return blocks;
    }

    // The lines of a program after its unit header and its modal lines, the header of the program, without comments.
    private static List<string> AfterHeader(List<string> lines)
    {
        int first = lines.FindIndex(line => line.Length > 0 && line[0] != '%' && !line.StartsWith('G'));
        return lines.GetRange(first, lines.Count - first).FindAll(line => !line.StartsWith(';'));
    }

    private static List<string> LinesWithoutNumbers(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
        {
            int blank = line.IndexOf(' ', StringComparison.Ordinal);
            bool numbered = line.Length > 1 && line[0] == 'N' && char.IsAsciiDigit(line[1]) && blank > 0;
            lines.Add(numbered ? line.Substring(blank + 1) : line);
        }

        return lines;
    }

    private static string Compile(string ncx, MachineConfig machine)
    {
        NcxProgram program = Parser.Parse(ncx, Example, new ParserOptions());
        CompileResult result = ((ICompiler)new SiemensCompiler()).Compile(program, machine, new CompileOptions());
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        return Assert.Single(result.Files).Text;
    }

    private static MachineConfig MillTurn()
    {
        return Load("machines/millturn1.toml");
    }

    private static MachineConfig Mill()
    {
        return Load("machines/siemens-840dsl-mill.toml");
    }

    // A machine file of the repository with the Siemens cycle catalog its [cycles] names (machine-config 6).
    private static MachineConfig Load(string relativePath)
    {
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "siemens.toml"), Controller.Siemens, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    // Blocks of the example that read back as other blocks, and why.
    private sealed record BlockDifference
    {
        public required string Grounds { get; init; }

        public required IReadOnlyList<string> Example { get; init; }

        public required IReadOnlyList<string> ReadBack { get; init; }
    }
}
