using System.Text.RegularExpressions;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Fakes;

/// <summary>
/// Compiles an NCX text with the fake compiler for a machine file given as text, and reads the result.
/// </summary>
internal static partial class FakeCompile
{
    /// <summary>
    /// Compiles a program for a machine.
    /// </summary>
    /// <param name="ncx">The NCX text.</param>
    /// <param name="machineToml">The machine file.</param>
    /// <param name="controller">The family of the fake compiler.</param>
    /// <param name="options">The options; none by default.</param>
    public static CompileResult Run(string ncx, string machineToml, Controller controller = Controller.Fanuc,
        CompileOptions? options = null)
    {
        var machineDiagnostics = new Diagnostics("fake.toml");
        MachineConfig machine = MachineConfigLoader.LoadText(machineToml, machineDiagnostics)
            ?? throw new InvalidOperationException("The fake machine does not load:\n" + machineDiagnostics.ToText());
        NcxProgram program = Parser.Parse(ncx, "T.ncx", new ParserOptions());
        return new FakeCompiler(controller).Compile(program, machine, options ?? new CompileOptions());
    }

    /// <summary>
    /// A file of one program named "T", number 1, with its header and the given blocks, every line ended with LF
    /// (language 4.1, 4.13).
    /// </summary>
    public static string Program(params string[] blocks)
    {
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
        };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return Lines(lines.ToArray());
    }

    /// <summary>
    /// The lines of a file, each ended with LF.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// The text of the one output file of a compile without an ERROR.
    /// </summary>
    public static string TextOf(CompileResult result)
    {
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        return Assert.Single(result.Files).Text;
    }

    /// <summary>
    /// The codes of the diagnostics of a compile, in the order reported.
    /// </summary>
    public static List<string> Codes(CompileResult result)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// The CMP diagnostics of a compile, the compiler's own.
    /// </summary>
    public static List<Diagnostic> CompilerDiagnostics(CompileResult result)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            if (CompilerCode().IsMatch(diagnostic.Code))
            {
                diagnostics.Add(diagnostic);
            }
        }

        return diagnostics;
    }

    [GeneratedRegex("^CMP[0-9]{3}$")]
    private static partial Regex CompilerCode();
}
