using System.Text.RegularExpressions;
using Ncx.Compilers.Siemens;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Tests.Fixtures;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Compiles NCX text with the Siemens compiler for the 840D sl machines of the repository, machines/siemens-840dsl-
/// mill.toml and the mill-turn machines/millturn1.toml (D104), each with the Siemens cycle catalog of cycles/, or for
/// a variant of one of them, and reads the result.
/// </summary>
internal static partial class SiemensCompile
{
    /// <summary>
    /// The header of the programs of the mill: the complete header of D34.
    /// </summary>
    public const string MillHeader = "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF";

    /// <summary>
    /// The header of the programs of the mill-turn, as MILLTURN_TRANSFER writes it.
    /// </summary>
    public const string MillTurnHeader = "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF";

    /// <summary>
    /// The machine file of the mill.
    /// </summary>
    public const string MillFile = "machines/siemens-840dsl-mill.toml";

    /// <summary>
    /// The machine file of the mill-turn.
    /// </summary>
    public const string MillTurnFile = "machines/millturn1.toml";

    // Two rotary axes of a table that no spindle owns, and the transformations of the mill-turn, for the 5-axis words
    // (controllers siemens.md 6).
    private const string RotaryTable = """

        [[axis]]
        id = "A1"
        ncx = "A"
        letter = "A"
        kind = "rotary"
        home = 0

        [[axis]]
        id = "C1"
        ncx = "C"
        letter = "C"
        kind = "rotary"
        home = 0

        """;

    private static readonly Lazy<MachineConfig> s_mill = new(() => Load(MillFile));
    private static readonly Lazy<MachineConfig> s_millTurn = new(() => Load(MillTurnFile));

    /// <summary>
    /// The 840D sl vertical mill of machines/, with the Siemens cycle catalog.
    /// </summary>
    public static MachineConfig Mill()
    {
        return s_mill.Value;
    }

    /// <summary>
    /// The 840D sl mill-turn of machines/, millturn1.toml (D104), with the Siemens cycle catalog.
    /// </summary>
    public static MachineConfig MillTurn()
    {
        return s_millTurn.Value;
    }

    /// <summary>
    /// The mill with a rotary table of two axes A and C and the [transform] table of the mill-turn: TRAORI, TRAFOOF and
    /// CYCLE800 (controllers siemens.md 4, 6).
    /// </summary>
    public static MachineConfig FiveAxis()
    {
        string millTurn = File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), MillTurnFile));
        int start = millTurn.IndexOf("\n[transform]", StringComparison.Ordinal);
        int end = millTurn.IndexOf("\n[retract]", StringComparison.Ordinal);
        string transform = millTurn.Substring(start, end - start);
        return Variant(MillFile, text => text.Insert(text.IndexOf("\n[positions]", StringComparison.Ordinal),
            RotaryTable) + transform);
    }

    /// <summary>
    /// A machine file of the repository with a change of its text, with the Siemens cycle catalog.
    /// </summary>
    /// <param name="relativePath">The path from the repository root.</param>
    /// <param name="change">What changes in its text.</param>
    public static MachineConfig Variant(string relativePath, Func<string, string> change)
    {
        string text = change(File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), relativePath)));
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.LoadText(text, diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return WithCatalog(machine, diagnostics);
    }

    /// <summary>
    /// A machine file of the repository with a line of it replaced: sub_end = "M17" for sub_end = "RET".
    /// </summary>
    public static MachineConfig Replaced(string relativePath, string line, string replacement)
    {
        return Variant(relativePath, text =>
        {
            Assert.Contains(line, text, StringComparison.Ordinal);
            return text.Replace(line, replacement, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Compiles an NCX file for a machine.
    /// </summary>
    public static CompileResult Run(string ncx, MachineConfig machine, CompileOptions? options = null)
    {
        NcxProgram program = Parser.Parse(ncx, "T.ncx", new ParserOptions());
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        return ((ICompiler)new SiemensCompiler()).Compile(program, machine, options ?? new CompileOptions());
    }

    /// <summary>
    /// A file of one program named "T" with its header and the given blocks (language 4.1, 4.13).
    /// </summary>
    public static string Program(string header, params string[] blocks)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"", header };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return Lines(lines.ToArray());
    }

    /// <summary>
    /// The lines a program of the mill writes between its header line and its M30, without block numbers.
    /// </summary>
    public static string MillBody(params string[] blocks)
    {
        return Body(Run(Program(MillHeader, blocks), Mill()));
    }

    /// <summary>
    /// The lines a program of the mill-turn writes between its header line and its M30, without block numbers.
    /// </summary>
    public static string MillTurnBody(params string[] blocks)
    {
        return Body(Run(Program(MillTurnHeader, blocks), MillTurn()));
    }

    /// <summary>
    /// The lines of the one file of a compile without an ERROR between the header line of its first program and the
    /// last M30, without block numbers.
    /// </summary>
    public static string Body(CompileResult result)
    {
        List<string> lines = LinesOf(TextOf(result));
        int end = lines.LastIndexOf("M30");
        Assert.True(end > 1, string.Join('\n', lines));
        return Lines([.. lines.GetRange(2, end - 2)]);
    }

    /// <summary>
    /// The text of the one output file of a compile without an ERROR, without block numbers, with LF line endings.
    /// </summary>
    public static string TextOf(CompileResult result)
    {
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        return Lines([.. LinesOf(Assert.Single(result.Files).Text)]);
    }

    /// <summary>
    /// The lines of a text without their block numbers.
    /// </summary>
    public static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
        {
            lines.Add(BlockNumber().Replace(line, ""));
        }

        return lines;
    }

    /// <summary>
    /// Lines joined with LF, the last one ended too.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return lines.Length == 0 ? "" : string.Join('\n', lines) + "\n";
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
    /// The codes of the diagnostics of the compiler, CMP, in the order reported.
    /// </summary>
    public static List<string> CompilerCodes(CompileResult result)
    {
        return Codes(result).FindAll(code => code.StartsWith("CMP", StringComparison.Ordinal));
    }

    private static MachineConfig Load(string relativePath)
    {
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return WithCatalog(machine, diagnostics);
    }

    private static MachineConfig WithCatalog(MachineConfig machine, Diagnostics diagnostics)
    {
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "siemens.toml"), Controller.Siemens, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    // N10 in front of a line (machine-config 2, block_numbers).
    [GeneratedRegex("^N[0-9]+ ", RegexOptions.CultureInvariant)]
    private static partial Regex BlockNumber();
}
