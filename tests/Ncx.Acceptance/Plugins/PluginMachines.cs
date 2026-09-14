using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The machine files and programs of the plugin tests: a three-axis mill whose spindle S1 has the role MAIN, which
/// the CoolantClutchRule of code-guidelines 11 addresses, with the coolant channels STANDARD and THROUGH, once
/// without an expansion rule and once with the clutch rule of machine-config 5a, and a [format] for the compiler of
/// the tests (machine-config 1 to 5a).
/// </summary>
internal static class PluginMachines
{
    /// <summary>
    /// The mill without an expansion rule.
    /// </summary>
    public const string PlainMill = """
        [machine]
        name = "Plugin mill"
        controller = "fanuc"
        channels = [1]
        units_default = "MM"
        default_spindle = "S1"
        default_holder = "H1"
        default_workpiece = "TABLE1"

        [format]
        decimal_separator = "."
        decimals = { X = 3, Y = 3, Z = 3, F = 1, S = 0 }
        trailing_zeros = false
        block_numbers = { enabled = false, start = 10, step = 10 }
        line_ending = "LF"
        program_end = "M30"
        sub_end = "M99"
        program_layout = "one_file"

        [roles]
        MAIN = "S1"
        TABLE = "TABLE1"

        [[resource]]
        id = "S1"
        type = "tool_spindle"

        [[resource]]
        id = "H1"
        type = "tool_holder"
        spindle = "S1"

        [[resource]]
        id = "TABLE1"
        type = "table"

        [[axis]]
        id = "X1"
        ncx = "X"
        letter = "X"
        kind = "linear"

        [[axis]]
        id = "Y1"
        ncx = "Y"
        letter = "Y"
        kind = "linear"

        [[axis]]
        id = "Z1"
        ncx = "Z"
        letter = "Z"
        kind = "linear"

        [spindle.MAIN]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        THROUGH = { ON = "M51", OFF = "M9" }
        """;

    // The coolant channel THROUGH of the plain mill.
    private const string PlainThrough = "THROUGH = { ON = \"M51\", OFF = \"M9\" }";

    /// <summary>
    /// The mill whose through-spindle coolant engages its clutch only while the spindle stands: requires =
    /// { SPINDLE = "OFF" } with restore = ["SPINDLE"] (machine-config 5a).
    /// </summary>
    public static string ClutchMill => PlainMill.Replace(PlainThrough,
        "THROUGH = { ON = \"M51\", OFF = \"M9\", requires = { SPINDLE = \"OFF\" }, restore = [\"SPINDLE\"] }",
        StringComparison.Ordinal);

    /// <summary>
    /// Loads a machine file given as text.
    /// </summary>
    public static MachineConfig Load(string toml)
    {
        var diagnostics = new Diagnostics("plugin-mill.toml");
        return MachineConfigLoader.LoadText(toml, diagnostics)
            ?? throw new InvalidOperationException("The machine does not load:\n" + diagnostics.ToText());
    }

    /// <summary>
    /// Parses an NCX text as the file part.ncx and expands it for a machine with these rewriters.
    /// </summary>
    public static NcxProgram Expand(string ncx, string machineToml, IReadOnlyList<IProgramRewriter> rewriters)
    {
        NcxProgram program = Parser.Parse(ncx, "part.ncx", new ParserOptions());
        return Expander.Expand(program, Load(machineToml), rewriters);
    }

    /// <summary>
    /// The program in canonical form with its generated blocks (language 4.15).
    /// </summary>
    public static string WriteWithGenerated(NcxProgram program)
    {
        return NcxWriter.Write(program, new WriterOptions { IncludeGenerated = true });
    }

    /// <summary>
    /// The first block whose canonical text is this.
    /// </summary>
    public static Block Find(NcxProgram program, string blockText)
    {
        foreach (Block block in program.Blocks)
        {
            if (NcxWriter.WriteBlock(block) == blockText)
            {
                return block;
            }
        }

        throw new InvalidOperationException($"The program has no block {blockText}.");
    }
}
