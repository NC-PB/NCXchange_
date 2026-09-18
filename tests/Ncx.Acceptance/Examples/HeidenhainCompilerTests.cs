using System.Text.RegularExpressions;
using Ncx.Acceptance.Cli;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The Heidenhain compiler over the examples end to end through ncx compile, as dotnet run --project src/Ncx.Cli --
/// compile docs/spec/examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530 runs it (phase 3, P3-04; milestone M5):
/// examples/2.5D_FRAESEN.ncx is equivalent to sources/2.5D_FRAESEN.h under the comparison rules of phase 3, and
/// Expected/BOHREN.ncx, read from the Fanuc source, to sources/BOHREN.h, each except the differences the documents
/// cannot settle, every one grounded in its question (NcException); PATTERN_LOOP and INCREMENTAL_SUB, which check
/// clean with the machine, compile without an ERROR. The runs go through CompileCommand.Run with a working directory of
/// their own and the repository as the tool's folder, so that no test depends on the working directory of the test
/// process (code-guidelines 8).
/// </summary>
public sealed partial class HeidenhainCompilerTests : IDisposable
{
    private const string MachineName = "heidenhain-itnc530";

    // The block of Expected/BOHREN.ncx that the Fanuc tapping M29 S500 reads into.
    private const string RigidTap = "RPM=500 FUNC:RIGID_TAP=ON";

    /// <summary>
    /// The target state of a program starts unknown (P3-03), so the FEED_MODE=PER_MIN of the header is M137, which the
    /// Klartext sources, whose control has it after the end of the program before, do not write (the TODO(question) of
    /// HeidenhainFunctions, the header of a program).
    /// </summary>
    internal static readonly NcException s_programStart = new()
    {
        Grounds = "the question of the target state at the start of a program: M137 for the FEED_MODE=PER_MIN of the "
            + "header",
        Compiled = ["M137"],
    };

    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());

    // What the last compile wrote to the standard error: the diagnostics (D98).
    private string _error = "";

    public void Dispose()
    {
        _project.Dispose();
    }

    // M5: ncx compile examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530 is equivalent to 2.5D_FRAESEN.h under the
    // comparison rules (block numbers, formatting and comment placement ignored; the M30 the source leaves out is no
    // difference by them, wave-1 question #12 and D49), except the header's M137 and the stock definition the example
    // keeps as a comment (D217).
    [Fact]
    public void Compile_25DFraesen_IsEquivalentToTheHeidenhainSourceExceptTheListedDifferences()
    {
        string compiled = Compile("2.5D_FRAESEN.ncx", Fixture.ReadText("2.5D_FRAESEN.ncx"), "2.5D FRAESEN.h");

        Assert.Equal("", _error);
        string? difference = new NcComparer(Mill()).Compare(Fixture.ReadText("sources/2.5D_FRAESEN.h"), compiled,
            Differences25DFraesen());
        Assert.True(difference is null, difference);
    }

    // P3-04: Expected/BOHREN.ncx, from the Fanuc source (P3-02), compiles to a program equivalent to BOHREN.h, except
    // the differences listed here with their questions; the two WARNINGs are the feed and the dwell of the tapping,
    // which cycle 207 has no Q parameter for (CMP105).
    // TODO(question): FUNC:RIGID_TAP=ON, the M29 S500 of the Fanuc tapping, names no function of [func] of
    // heidenhain-itnc530.toml, which the virtual machine reports as the ERROR VM006, and cycle 207 taps rigidly without
    // it (heidenhain 5); its block, whose RPM=500 changes nothing, is taken out of the program before it is compiled.
    [Fact]
    public void Compile_Bohren_IsEquivalentToTheHeidenhainSourceExceptTheListedDifferences()
    {
        string frozen = File.ReadAllText(
            Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "BOHREN.ncx"))
            .ReplaceLineEndings("\n");

        string compiled = Compile("BOHREN.ncx", WithoutRigidTap(frozen), "BOHREN.h");

        Assert.Equal(["CMP105", "CMP105"], Codes(_error));
        string? difference = new NcComparer(Mill()).Compare(Fixture.ReadText("sources/BOHREN.h"), compiled,
            BohrenDifferences(frozen));
        Assert.True(difference is null, difference);
    }

    // Phase 3, P3-04 scope: the examples with Q parameters, labels, a conditional jump, a subprogram with incremental
    // words, JUMP=END and HOME compile for the machine without an ERROR.
    [Theory]
    [InlineData("PATTERN_LOOP.ncx", "PATTERN.h")]
    [InlineData("INCREMENTAL_SUB.ncx", "SLOT_ROW.h")]
    public void Compile_ExamplesThatCheckWithTheMachine_CompileWithoutAnError(string example, string output)
    {
        string compiled = Compile(example, Fixture.ReadText(example), output);

        Assert.StartsWith("0 BEGIN PGM ", compiled, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR", _error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The differences of 2.5D_FRAESEN.ncx compiled for the iTNC 530 against 2.5D_FRAESEN.h, in their order: the M137
    /// of the header and the stock definition the example keeps as a comment (D217).
    /// </summary>
    internal static List<NcException> Differences25DFraesen()
    {
        return [s_programStart, StockDefinition("BLK FORM 0.1 Z X0 Y0 Z-20", "BLK FORM 0.2 X100 Y100 Z0")];
    }

    /// <summary>
    /// The differences of Expected/BOHREN.ncx, the Fanuc reading, compiled for the iTNC 530 against BOHREN.h, in their
    /// order, each with its question.
    /// </summary>
    /// <param name="frozen">Expected/BOHREN.ncx with LF line endings, whose fourth operation the compiled program
    /// writes as single moves.</param>
    internal static List<NcException> BohrenDifferences(string frozen)
    {
        return
        [
            s_programStart,
            StockDefinition("BLK FORM 0.1 Z X0 Y0 Z-25", "BLK FORM 0.2 X100 Y20 Z0"),
            PlungeFeed(Cycle200("565,487"), Cycle200("565")),
            new NcException
            {
                Grounds = "D163 (Q213 of CHIP_BREAK is the number of infeeds, 19, where BOHREN.h writes 24) and the "
                    + "plunge feed F565 of the Fanuc source",
                Source = [Cycle203("565,487", "24")],
                Compiled = [Cycle203("565", "19")],
            },
            PlungeFeed(Cycle203("565,487", "0"), Cycle203("565", "0")),
            new NcException
            {
                Grounds = "D163: the fourth operation is cycle 203 with Q213=3 in BOHREN.h and single moves in the "
                    + "Fanuc source, which Expected/BOHREN.ncx keeps",
                Source =
                [
                    Cycle203("565,487", "3"), "CYCL CALL", "L X30 FMAX M99", "L X50 FMAX M99", "L X70 FMAX M99",
                    "L X90 FMAX M99",
                ],
                Compiled = SingleMoves(frozen),
            },
        ];
    }

    /// <summary>
    /// Expected/BOHREN.ncx without the block that the Fanuc tapping M29 S500 reads into, which the iTNC 530 does not
    /// compile (the TODO(question) of the BOHREN test).
    /// </summary>
    /// <param name="frozen">Expected/BOHREN.ncx with LF line endings.</param>
    internal static string WithoutRigidTap(string frozen)
    {
        Assert.Contains(RigidTap + "\n", frozen, StringComparison.Ordinal);
        return frozen.Replace(RigidTap + "\n", "", StringComparison.Ordinal);
    }

    /// <summary>
    /// D217 (wave-2 question #81), controllers heidenhain.md 7 rule 9: BLK FORM is RAW:HEIDENHAIN in the Heidenhain
    /// reading, and the NCX program holds no stock definition.
    /// </summary>
    internal static NcException StockDefinition(string first, string second)
    {
        return new NcException
        {
            Grounds = "D217 (wave-2 question #81) and heidenhain 7 rule 9: the NCX program holds no BLK FORM",
            Source = [first, second],
        };
    }

    // Expected/BOHREN.ncx keeps the Fanuc reading F565 of the plunge feed that BOHREN.h writes Q206=565,487
    // (Expected/README.md; the Heidenhain reading lists it, HeidenhainReaderTests).
    private static NcException PlungeFeed(string source, string compiled)
    {
        return new NcException
        {
            Grounds = "the plunge feed F565 of the Fanuc source, which Expected/BOHREN.ncx keeps, against Q206=565,487",
            Source = [source],
            Compiled = [compiled],
        };
    }

    private static string Cycle200(string feed)
    {
        return "CYCL DEF 200 BOHREN Q200=5 Q201=-21,732 Q206=" + feed + " Q202=21,732 Q210=0 Q203=0 Q204=5 Q211=0";
    }

    /// <summary>
    /// Cycle 203 of BOHREN.h as the comparison normalizes it, with its plunge feed Q206 and its number of chip breaks
    /// Q213.
    /// </summary>
    internal static string Cycle203(string feed, string breaks)
    {
        return "CYCL DEF 203 UNIVERSALBOHREN Q200=5 Q201=-21,732 Q206=" + feed + " Q202=1,2 Q210=0 Q203=0 Q204=5 "
            + "Q212=0 Q213=" + breaks + " Q205=1,2 Q211=0 Q208=MAX Q256=0,6";
    }

    // The fourth operation of Expected/BOHREN.ncx, the single moves of the Fanuc source from LINE Z=3.8 F=565 to the
    // block before the RAPID Z=5 in front of the first HOME Z, each as the L block the compiler writes for it: FMAX for
    // RAPID, F where the NCX block has it (heidenhain 8 rule 2).
    private static List<string> SingleMoves(string frozen)
    {
        List<string> lines = [.. frozen.Split('\n')];
        int first = lines.IndexOf("LINE Z=3.8 F=565");
        int home = lines.IndexOf("HOME Z");
        Assert.True(first > 0 && home > first, "Expected/BOHREN.ncx has no fourth operation of single moves.");
        var moves = new List<string>();
        for (int index = first; index < home - 1; index++)
        {
            string[] words = lines[index].Split(' ');
            var klartext = new List<string> { "L" };
            string feed = "";
            for (int word = 1; word < words.Length; word++)
            {
                string[] keyAndValue = words[word].Split('=');
                if (keyAndValue[0] == "F")
                {
                    feed = "F" + keyAndValue[1];
                }
                else
                {
                    klartext.Add(keyAndValue[0] + keyAndValue[1]);
                }
            }

            klartext.Add(words[0] == "RAPID" ? "FMAX" : feed);
            moves.Add(string.Join(" ", klartext).Trim());
        }

        return moves;
    }

    // Compiles an NCX text through ncx compile with the machine by name, and gives the text of the output file.
    private string Compile(string fileName, string ncx, string output)
    {
        string file = _project.WriteInWorkingDirectory(fileName, ncx);
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = file,
            MachineFile = MachineName,
            WorkingDirectory = _project.WorkingDirectory,
            ToolFolder = _project.ToolFolder,
        };

        int exitCode = CompileCommand.Run(settings, null, Ncx.Cli.Program.Compilers(), error);
        _error = error.ToString();
        Assert.True(exitCode == 0, _error);
        return File.ReadAllText(Path.Combine(_project.WorkingDirectory, "out", MachineName, output));
    }

    // The iTNC 530 of machines/, whose [format] the comparison formats the numbers with.
    private static MachineConfig Mill()
    {
        var diagnostics = new Diagnostics("heidenhain-itnc530.toml");
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", MachineName + ".toml"), diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }

    private static List<string> Codes(string diagnostics)
    {
        var codes = new List<string>();
        foreach (Match match in DiagnosticCode().Matches(diagnostics))
        {
            codes.Add(match.Groups[1].Value);
        }

        return codes;
    }

    [GeneratedRegex(@": (?:ERROR|WARNING|INFO) ([A-Z]+[0-9]{3}): ")]
    private static partial Regex DiagnosticCode();
}
