using Ncx.Compilers.Heidenhain;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Tests.Fixtures;

namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// Compiles NCX snippets with the Heidenhain compiler for the iTNC 530 of the repository,
/// machines/heidenhain-itnc530.toml with the cycle catalog cycles/heidenhain.toml, as ncx compile --machine
/// heidenhain-itnc530 compiles them, and reads the Klartext it writes.
/// </summary>
internal static class HeidenhainCompile
{
    private const string MachineFile = "machines/heidenhain-itnc530.toml";

    private static readonly Lazy<MachineConfig> s_mill = new(() => Load(ReadMachineFile()));

    /// <summary>
    /// The iTNC 530 vertical mill of machines/, with the Heidenhain cycle catalog.
    /// </summary>
    public static MachineConfig Mill()
    {
        return s_mill.Value;
    }

    /// <summary>
    /// The iTNC 530 of machines/ with one text of its file replaced, with the Heidenhain cycle catalog.
    /// </summary>
    /// <param name="text">A text of machines/heidenhain-itnc530.toml.</param>
    /// <param name="replacement">The text that replaces it.</param>
    public static MachineConfig MillWith(string text, string replacement)
    {
        string toml = ReadMachineFile();
        Assert.Contains(text, toml, StringComparison.Ordinal);
        return Load(toml.Replace(text, replacement, StringComparison.Ordinal));
    }

    /// <summary>
    /// The iTNC 530 of machines/ with tables added at the end of its file.
    /// </summary>
    /// <param name="tables">TOML tables the file does not have.</param>
    public static MachineConfig MillAnd(string tables)
    {
        return Load(ReadMachineFile() + "\n" + tables + "\n");
    }

    /// <summary>
    /// Compiles an NCX text.
    /// </summary>
    public static CompileResult Run(string ncx, MachineConfig? machine = null)
    {
        NcxProgram program = Parser.Parse(ncx, "T.ncx", new ParserOptions());
        return new HeidenhainCompiler().Compile(program, machine ?? Mill(), new CompileOptions());
    }

    /// <summary>
    /// A file of one program named T with the units and the plane and the given blocks, every line ended with LF
    /// (language 4.1, 4.13). The header writes nothing but BEGIN PGM T MM.
    /// </summary>
    public static string Program(params string[] blocks)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"", "UNITS=MM WORKPLANE=XY" };
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
    /// The lines between BEGIN PGM and the M30 of the one output file of a compile without an ERROR, without their
    /// block numbers, joined with LF.
    /// </summary>
    public static string Body(CompileResult result)
    {
        List<string> lines = LinesOf(TextOf(result));
        int end = lines.FindLastIndex(line => line == "M30");
        Assert.True(end > 0, string.Join('\n', lines));
        return string.Join('\n', lines.GetRange(1, end - 1));
    }

    /// <summary>
    /// The lines of a Klartext file without their block numbers.
    /// </summary>
    public static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            int space = line.IndexOf(' ', StringComparison.Ordinal);
            bool numbered = space > 0 && line.Substring(0, space).All(char.IsAsciiDigit);
            lines.Add(numbered ? line.Substring(space + 1) : line);
        }

        return lines;
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
    /// The diagnostic of a code, which the compile must have reported once.
    /// </summary>
    public static Diagnostic Single(CompileResult result, string code)
    {
        return Assert.Single(result.Diagnostics.Items, diagnostic => diagnostic.Code == code);
    }

    private static string ReadMachineFile()
    {
        return File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), MachineFile));
    }

    private static MachineConfig Load(string toml)
    {
        var diagnostics = new Diagnostics("heidenhain-itnc530.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        var catalogDiagnostics = new Diagnostics("cycles/heidenhain.toml");
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "heidenhain.toml"), Controller.Heidenhain,
            catalogDiagnostics);
        Assert.True(catalog is not null, catalogDiagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }
}
