using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers.Siemens;
using Ncx.Tests.Fixtures;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// Reads SINUMERIK snippets with the Siemens reader against the 840D sl mill of the repository,
/// machines/siemens-840dsl-mill.toml, or the mill-turn millturn1.toml, each with the Siemens cycle catalog of cycles/,
/// and writes the result the way ncx convert writes it.
/// </summary>
internal static class SiemensRead
{
    private static readonly Lazy<MachineConfig> s_mill = new(() => Load("machines/siemens-840dsl-mill.toml"));
    private static readonly Lazy<MachineConfig> s_millTurn = new(() => Load("machines/millturn1.toml"));
    private static readonly Lazy<MachineConfig> s_fiveAxis = new(FiveAxisMachine);

    // Two rotary axes of a table that no spindle owns, for the rotary-axes mode of CYCLE800 (controller-mapping 1,
    // TILT_AXIS).
    private const string RotaryTable = "\n[[axis]]\nid = \"A1\"\nncx = \"A\"\nletter = \"A\"\nkind = \"rotary\"\n"
        + "limits = [-120, 30]\nrapid = 10000\nmax_feed = 5000\nacceleration = 500\nhome = 0\n\n[[axis]]\nid = \"C1\"\n"
        + "ncx = \"C\"\nletter = \"C\"\nkind = \"rotary\"\nrapid = 10000\nmax_feed = 5000\n"
        + "acceleration = 500\nhome = 0\n";

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
    /// The 840D sl mill of machines/ with a rotary table of two axes A and C, which the rotary-axes mode of CYCLE800
    /// names (controller-mapping 1, TILT_AXIS).
    /// </summary>
    public static MachineConfig FiveAxis()
    {
        return s_fiveAxis.Value;
    }

    /// <summary>
    /// A machine file of the repository loaded by its path, with the Siemens cycle catalog (machine-config 6).
    /// </summary>
    /// <param name="relativePath">The path from the repository root.</param>
    public static MachineConfig Load(string relativePath)
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

    /// <summary>
    /// Reads a SINUMERIK source.
    /// </summary>
    public static NcxProgram Program(string source, MachineConfig? machine = null)
    {
        return new SiemensReader().Read(new SourceFile("TEST.mpf", source), machine ?? Mill(), new ReadOptions());
    }

    /// <summary>
    /// The canonical text of a SINUMERIK source.
    /// </summary>
    public static string Text(string source, MachineConfig? machine = null)
    {
        return NcxWriter.Write(Program(source, machine));
    }

    /// <summary>
    /// The program of a snippet framed as the main program TEST with M30.
    /// </summary>
    public static NcxProgram FramedProgram(string snippet, MachineConfig? machine = null)
    {
        return Program(Frame(snippet), machine);
    }

    /// <summary>
    /// The blocks a snippet reads into, between the header after PROGRAM=BEGIN and PROGRAM=END, each line ended with
    /// LF; the snippet is framed as the main program TEST with M30.
    /// </summary>
    public static string Body(string snippet, MachineConfig? machine = null)
    {
        return BodyOf(Text(Frame(snippet), machine));
    }

    /// <summary>
    /// The blocks a snippet reads into, as Body gives them, once check has run the whole program the reader wrote
    /// without an ERROR: a reader produces a program that check can run (code-guidelines 4; virtual machine 1).
    /// </summary>
    public static string CheckedBody(string snippet, MachineConfig? machine = null)
    {
        string text = Text(Frame(snippet), machine);
        AssertChecks(text, machine);
        return BodyOf(text);
    }

    /// <summary>
    /// Asserts that check runs a text the reader wrote without an ERROR (code-guidelines 4; virtual machine 1).
    /// </summary>
    public static void AssertChecks(string text, MachineConfig? machine = null)
    {
        Diagnostics diagnostics = Check(text, machine);
        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
    }

    /// <summary>
    /// The lines between the header after PROGRAM=BEGIN and PROGRAM=END of a canonical text.
    /// </summary>
    public static string BodyOf(string text)
    {
        List<string> lines = [.. text.Split('\n')];
        int begin = lines.FindIndex(line => line.StartsWith("PROGRAM=BEGIN", StringComparison.Ordinal));
        int end = lines.FindIndex(line => line.StartsWith("PROGRAM=END", StringComparison.Ordinal));
        return Lines([.. lines.GetRange(begin + 2, end - begin - 2)]);
    }

    /// <summary>
    /// Lines joined with LF, the last one ended too.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return lines.Length == 0 ? "" : string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// The RAW:SIEMENS block of a source text, its quotes and backslashes escaped as an NCX string escapes them
    /// (language 3).
    /// </summary>
    public static string Raw(string source)
    {
        return "RAW:SIEMENS=\"" + source.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>
    /// A block with its comment in column 57 (language 5 rule 7).
    /// </summary>
    public static string Commented(string words, string comment)
    {
        return words.PadRight(56) + comment;
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public static List<string> Codes(NcxProgram program)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// What ncx check finds in the text the reader wrote: the diagnostics of a STATIC run of the parsed text on the
    /// machine; a reader produces a program that check can run (code-guidelines 4, virtual machine 1).
    /// </summary>
    public static Diagnostics Check(string text, MachineConfig? machine = null)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        MachineConfig target = machine ?? Mill();
        var diagnostics = new Diagnostics("read.ncx");
        new VirtualMachine(target, VmOptions.ForMachine(target), diagnostics).Run(parsed);
        return diagnostics;
    }

    /// <summary>
    /// Asserts that the text parses back without a diagnostic and formats to itself, what ncx format does to the output
    /// of ncx convert (D91).
    /// </summary>
    public static void AssertFormatsToItself(string text)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
    }

    private static string Frame(string snippet)
    {
        return "%_N_TEST_MPF\n" + snippet + "\nM30\n";
    }

    // The rotary axes join the [[axis]] list of the mill directly behind its linear axes, in front of [positions]:
    // appended behind the other tables of the file, the machine held only the two rotary axes, and check found no X, Y
    // and Z.
    private static MachineConfig FiveAxisMachine()
    {
        string text = File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), "machines", "siemens-840dsl-mill.toml"));
        int positions = text.IndexOf("\n[positions]", StringComparison.Ordinal);
        Assert.True(positions >= 0, "The mill of machines/ has no [positions] table behind its axes.");
        var diagnostics = new Diagnostics("five-axis.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(text.Insert(positions, RotaryTable), diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "siemens.toml"), Controller.Siemens, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }
}
