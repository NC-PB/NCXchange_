using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// Reads Fanuc snippets with the Fanuc reader against a small mill and a small system A lathe, each with the Fanuc
/// cycle catalog of the repository, and writes the result the way ncx convert writes it.
/// </summary>
internal static class FanucRead
{
    /// <summary>
    /// A vertical mill on a Fanuc 30i: axes X Y Z with their reference point at 0, the milling spindle TOOL with M3 M4
    /// M5 and M19, the coolant M8/M9, rigid tapping M29, two system variables, the Fanuc tool change.
    /// </summary>
    public const string MillToml = """
        [machine]
        name = "Test mill"
        controller = "fanuc"
        units_default = "MM"
        default_spindle = "S1"
        default_holder = "H1"
        default_workpiece = "TABLE1"

        [tool_change]
        change = "T{tool} M6"
        preload = "T{tool}"
        unload = "T0 M6"

        [roles]
        TOOL = "S1"
        TABLE = "TABLE1"

        [[resource]]
        id = "S1"
        type = "tool_spindle"

        [[resource]]
        id = "H1"
        type = "tool_holder"
        spindle = "S1"
        magazine = true

        [[resource]]
        id = "TABLE1"
        type = "table"

        [[axis]]
        id = "X1"
        ncx = "X"
        letter = "X"
        kind = "linear"
        home = 0

        [[axis]]
        id = "Y1"
        ncx = "Y"
        letter = "Y"
        kind = "linear"
        home = 0

        [[axis]]
        id = "Z1"
        ncx = "Z"
        letter = "Z"
        kind = "linear"
        home = 0

        [spindle.TOOL]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"
        ORIENT = "M19"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }

        [func]
        RIGID_TAP = { ON = "M29" }

        [cycles]
        catalog = "fanuc.toml"

        [system_variables]
        SYS_POS_X = "#5041"
        SYS_WEAR_Z = "#11{index:03}"
        """;

    /// <summary>
    /// A two-path lathe in G-code system A of the builder "nakamura": X in diameters with U, Z with W, the C axes of
    /// the main and the sub spindle with H, the spindles MAIN, SUB and TOOL, the C-axis mode, the synchronous spindles,
    /// the workpiece codes, the wait marks M100 to M199 with a path list, polar and cylinder interpolation of the
    /// builder, its ATC macro G340 and the builder codes G411 and G300.
    /// </summary>
    public const string LatheToml = """
        [machine]
        name = "Test lathe"
        controller = "fanuc"
        builder = "nakamura"
        gcode_system = "A"
        units_default = "MM"
        channels = [1, 2]
        default_spindle = "S1"
        default_holder = "T1"
        default_workpiece = "S1"

        [tool_change]
        change = "G340 T{tool:02}{offset:02}. A{next:02}."
        preload = "G341 T{next:02}."

        [roles]
        MAIN = "S1"
        SUB = "S2"
        TOOL = "S3"
        TURRET1 = "T1"

        [[resource]]
        id = "S1"
        type = "work_spindle"
        axis = "C1"

        [[resource]]
        id = "S2"
        type = "work_spindle"
        axis = "C2"

        [[resource]]
        id = "S3"
        type = "tool_spindle"

        [[resource]]
        id = "T1"
        type = "tool_holder"
        spindle = "S3"
        magazine = true

        [[axis]]
        id = "X1"
        ncx = "X"
        letter = "X"
        incremental_letter = "U"
        programming = "diameter"
        kind = "linear"
        home = 0

        [[axis]]
        id = "Z1"
        ncx = "Z"
        letter = "Z"
        incremental_letter = "W"
        kind = "linear"
        home = 0

        [[axis]]
        id = "C1"
        ncx = "C"
        letter = "C"
        incremental_letter = "H"
        kind = "rotary"
        owner = "S1"
        home = 0

        [[axis]]
        id = "C2"
        ncx = "C2"
        letter = "C"
        incremental_letter = "H"
        kind = "rotary"
        owner = "S2"
        home = 0

        [[axis]]
        id = "B1"
        ncx = "B"
        letter = "B"
        kind = "linear"
        owner = "S2"
        home = 0

        [spindle.MAIN]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"

        [spindle.SUB]
        CW = "M53"
        CCW = "M54"
        OFF = "M55"

        [spindle.TOOL]
        CW = "M88"
        CCW = "M89"
        OFF = "M90"

        [spindle_mode.MAIN]
        AXIS = "M91"
        SPINDLE = "M41"

        [spindle_sync]
        ON = "M96"
        OFF = "M97"
        PHASE = "M92"

        [workpiece]
        MAIN = "G54 M428"
        SUB = "G59 M427"

        [sync]
        wait = "M{mark} P{paths}"
        mark_range = [100, 199]
        paths = "list"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }

        [transform]
        POLAR_ON = "G112"
        POLAR_OFF = "G113"
        CYLINDER_ON = "G107 C{r}"
        CYLINDER_OFF = "G107 C0"

        [cycles]
        catalog = "fanuc.toml"

        [raw]
        known = ["G411", "G300"]

        [system_variables]
        SYS_WEAR_Z = "#11{index:03}"
        """;

    /// <summary>
    /// The test mill with the Fanuc cycle catalog.
    /// </summary>
    public static MachineConfig Mill()
    {
        return Load(MillToml, "mill.toml");
    }

    /// <summary>
    /// The test lathe with the Fanuc cycle catalog.
    /// </summary>
    public static MachineConfig Lathe()
    {
        return Load(LatheToml, "lathe.toml");
    }

    /// <summary>
    /// A machine file of the repository, machines/ or docs/spec/examples/machines/, loaded by its path, with the cycle
    /// catalog its [cycles] names from cycles/ (machine-config 6).
    /// </summary>
    /// <param name="relativePath">The path from the repository root, "machines/fanuc-mill-30i.toml".</param>
    public static MachineConfig LoadMachineFile(string relativePath)
    {
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return WithCatalog(machine);
    }

    /// <summary>
    /// Reads a Fanuc source.
    /// </summary>
    public static NcxProgram Program(string source, MachineConfig? machine = null)
    {
        return new FanucReader().Read(new SourceFile("test.nc", source), machine ?? Mill(), new ReadOptions());
    }

    /// <summary>
    /// The canonical text of a Fanuc source.
    /// </summary>
    public static string Text(string source, MachineConfig? machine = null)
    {
        return NcxWriter.Write(Program(source, machine));
    }

    /// <summary>
    /// The canonical text of a Fanuc source read from a file of this name, "O1000.P-2".
    /// </summary>
    public static string TextOfFile(string fileName, string source, MachineConfig? machine = null)
    {
        return NcxWriter.Write(new FanucReader().Read(new SourceFile(fileName, source), machine ?? Mill(),
            new ReadOptions()));
    }

    /// <summary>
    /// What ncx check finds in the text the reader wrote: the diagnostics of a STATIC run of the parsed text on the
    /// machine; a reader produces a program that check can run (code-guidelines 4, virtual machine 1).
    /// </summary>
    public static Diagnostics Check(string text, MachineConfig machine)
    {
        NcxProgram parsed = Parser.Parse(text, "read.ncx", new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        var diagnostics = new Diagnostics("read.ncx");
        new VirtualMachine(machine, VmOptions.ForMachine(machine), diagnostics).Run(parsed);
        return diagnostics;
    }

    /// <summary>
    /// The blocks a snippet reads into, between the header after PROGRAM=BEGIN and PROGRAM=END, each line ended with
    /// LF; the snippet is framed with %, O0001 and M30.
    /// </summary>
    public static string Body(string snippet, MachineConfig? machine = null)
    {
        return BodyOf(Text(Frame(snippet), machine));
    }

    /// <summary>
    /// The program of a snippet framed with %, O0001 and M30.
    /// </summary>
    public static NcxProgram FramedProgram(string snippet, MachineConfig? machine = null)
    {
        return Program(Frame(snippet), machine);
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
        return "%\nO0001\n" + snippet + "\nM30\n%\n";
    }

    private static MachineConfig Load(string toml, string name)
    {
        var diagnostics = new Diagnostics(name);
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return WithCatalog(machine);
    }

    private static MachineConfig WithCatalog(MachineConfig machine)
    {
        var diagnostics = new Diagnostics("cycles/fanuc.toml");
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "fanuc.toml"), Controller.Fanuc, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }
}
