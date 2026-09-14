using Ncx.Compilers.Tests.Fakes;
using Ncx.Config;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The tool table of D10: {kind} from the table, mapped by kind_map (machine-config 3, D52), and without the table, or
/// for a tool it lacks, the default kind with the warning block at the head of the program.
/// </summary>
public sealed class ToolTableWarningTests
{
    // The Mori Seiki change writes the tool kind with {kind} (machine-config 3).
    private const string ChangeWithKind = """
        [tool_change]
        change = "T{tool} M6 D{kind}"
        kind_map = { ROTARY = "0.", TURNING = "1." }
        """;

    private static readonly string s_twoTools = FakeCompile.Program(
        "TOOL=1", "RAPID X=0 Y=0 Z=5", "TOOL=2", "RAPID Z=10");

    // D10: without the tool table {kind} takes the default and the compiler writes a warning block at the head of the
    // program that names the expected file and the tools it lacks.
    [Fact]
    public void Kind_WithoutToolTable_TakesTheDefaultAndWritesTheWarningBlock()
    {
        CompileResult result = FakeCompile.Run(s_twoTools, FakeMachines.Mill(toolChange: ChangeWithKind));

        Assert.Equal(
            "%\nO1 (T)\n(WARNING: TOOL DATA MISSING, D10)\n"
            + "(EXPECTED TOOL TABLE: NONE, THE MACHINE FILE NAMES NO TOOL_TABLE)\n"
            + "(TOOLS WITHOUT DATA: 1, 2 - KIND ROTARY WRITTEN, CHECK BEFORE RUNNING)\n"
            + "N10 T1 M6 D0.\nN20 G0 X0. Y0. Z5.\nN30 T2 M6 D0.\nN40 Z10.\nN50 M30\n%\n",
            FakeCompile.TextOf(result));
    }

    // D10: a tool table the machine names and that is missing is named as the expected file.
    [Fact]
    public void Warning_ToolTableNamedButMissing_NamesTheExpectedFile()
    {
        var options = new CompileOptions { ToolTableFile = "machines/mill.tools.toml" };

        CompileResult result = FakeCompile.Run(s_twoTools, FakeMachines.Mill(toolChange: ChangeWithKind),
            options: options);

        Assert.Contains("\n(EXPECTED TOOL TABLE: machines/mill.tools.toml)\n", FakeCompile.TextOf(result),
            StringComparison.Ordinal);
    }

    // Machine-config 3, D52: {kind} comes from the tool table, mapped by kind_map; with every tool described, there is
    // no warning block.
    [Fact]
    public void Kind_FromTheToolTable_IsMappedByKindMapWithoutWarning()
    {
        var table = new ToolTable
        {
            Numbered = new Dictionary<int, ToolData>
            {
                [1] = new ToolData { Kind = "TURNING" },
                [2] = new ToolData { Kind = "ROTARY" },
            },
        };

        CompileResult result = FakeCompile.Run(s_twoTools, FakeMachines.Mill(toolChange: ChangeWithKind),
            options: new CompileOptions { ToolTable = table, ToolTableFile = "mill.tools.toml" });

        Assert.Equal(
            "%\nO1 (T)\nN10 T1 M6 D1.\nN20 G0 X0. Y0. Z5.\nN30 T2 M6 D0.\nN40 Z10.\nN50 M30\n%\n",
            FakeCompile.TextOf(result));
    }

    // D10: a tool the table lacks takes the default kind, and only it is named.
    [Fact]
    public void Warning_ToolTheTableLacks_IsTheOnlyToolNamed()
    {
        var table = new ToolTable
        {
            Numbered = new Dictionary<int, ToolData> { [1] = new ToolData { Kind = "TURNING" } },
        };

        CompileResult result = FakeCompile.Run(s_twoTools, FakeMachines.Mill(toolChange: ChangeWithKind),
            options: new CompileOptions { ToolTable = table, ToolTableFile = "mill.tools.toml" });

        string text = FakeCompile.TextOf(result);
        Assert.Contains("\n(TOOLS WITHOUT DATA: 2 - KIND ROTARY WRITTEN, CHECK BEFORE RUNNING)\n", text,
            StringComparison.Ordinal);
        Assert.Contains("\nN30 T2 M6 D0.\n", text, StringComparison.Ordinal);
    }

    // D10 (the TODO(question) of ToolDataWarning): a machine whose templates never write {kind} needs no tool data,
    // and its programs get no warning block.
    [Fact]
    public void Warning_TemplatesWithoutKind_IsNotWritten()
    {
        string text = FakeCompile.TextOf(FakeCompile.Run(s_twoTools, FakeMachines.Mill()));

        Assert.DoesNotContain("TOOL DATA", text, StringComparison.Ordinal);
    }
}
