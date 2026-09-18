using System.Text.RegularExpressions;
using Ncx.Compilers.Fanuc;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// Compiles NCX text with the Fanuc compiler for a machine file given as text: a vertical mill on a Fanuc 30i as
/// machines/fanuc-mill-30i.toml writes it, without block numbers so that a test reads the lines alone, and a two-path
/// lathe of G-code system A.
/// </summary>
internal static partial class FanucCompile
{
    /// <summary>
    /// The start block of the Fanuc sources (controllers fanuc.md 1).
    /// </summary>
    public const string SourceHeader = "header = \"G0 G40\\nG80 G90 G94 G98\"";

    /// <summary>
    /// The wait marks M100 to M199 of the lathe, with the paths as a list (machine-config 5).
    /// </summary>
    public const string DefaultSync = "[sync]\nwait = \"M{mark} P{paths}\"\npaths = \"list\"\nmark_range = [100, 199]";

    /// <summary>
    /// The four [format] options of the habits of the Fanuc sources (implementation 13, Risks).
    /// </summary>
    public const string SourceHabits = SourceHeader + """

        plane_with_first_motion = true
        motion_code_after_tool_change = true
        length_offset_with_tool_axis = true
        """;

    /// <summary>
    /// The mill with [format] options of a test, no block numbers, LF, program_end M30.
    /// </summary>
    /// <param name="options">Further keys of [format].</param>
    /// <param name="extra">Further tables of the machine file.</param>
    public static string Mill(string options = SourceHabits, string extra = "")
    {
        return $$"""
            [machine]
            name = "Test mill"
            controller = "fanuc"
            dialect = "30i"
            s_binds_to_spindle_word = true
            channels = [1]
            units_default = "MM"
            default_spindle = "S1"
            default_holder = "H1"
            default_workpiece = "TABLE1"

            [format]
            decimal_separator = "."
            decimals = { X = 3, Y = 3, Z = 3, F = 3, S = 0 }
            trailing_zeros = false
            block_numbers = { enabled = false }
            line_ending = "LF"
            program_end = "M30"
            sub_end = "M99"
            program_layout = "one_file"
            comment_charset = "ASCII"
            {{options}}

            [tool_change]
            change = "T{tool} M6"
            preload = "T{tool}"
            unload = "T0 M6"

            [home]
            template = "G28 {axes}"
            point = "G30 P{point} {axes}"

            [setpos]
            template = "G92 {axes}"

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
            RPM = "S{rpm}"
            VC = "G96 S{value}"
            CSS_OFF = "G97"
            RPM_MAX = "G92 S{value}"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }

            [func]
            RIGID_TAP = { ON = "M29" }

            [tolerance]
            ON = "G5.1 Q1"
            OFF = "G5.1 Q0"

            [variables]
            map = { Q = "#1", QL = "#", QR = "#5" }

            [system_variables]
            SYS_POS_X = "#5041"
            SYS_WEAR_Z = "#11{index:03}"

            {{extra}}
            """;
    }

    /// <summary>
    /// A two-path lathe of G-code system A: X in diameters with U, Z with W, the main spindle MAIN with its C axis and
    /// the driven tool TOOL, the turret T1 with the turret form of the tool change, the wait marks M100 to M199.
    /// </summary>
    /// <param name="sync">The [sync] table.</param>
    /// <param name="extra">Further tables of the machine file.</param>
    public static string Lathe(string sync = DefaultSync, string extra = "")
    {
        return $$"""
            [machine]
            name = "Test lathe"
            controller = "fanuc"
            gcode_system = "A"
            s_binds_to_spindle_word = true
            channels = [1, 2]
            units_default = "MM"
            default_spindle = "S1"
            default_holder = "T1"
            default_workpiece = "S1"

            [format]
            decimals = { X = 3, Z = 3, C = 3, F = 3, S = 0 }
            block_numbers = { enabled = false }
            line_ending = "LF"
            program_end = "M30"
            sub_end = "M99"

            [tool_change]
            change = "T{tool:02}{offset:02}"

            [home]
            template = "G28 {axes}"

            [setpos]
            template = "G50 {axes}"

            [roles]
            MAIN = "S1"
            TOOL = "S3"
            TURRET1 = "T1"

            [[resource]]
            id = "S1"
            type = "work_spindle"
            axis = "C1"

            [[resource]]
            id = "S3"
            type = "tool_spindle"

            [[resource]]
            id = "T1"
            type = "tool_holder"
            spindle = "S3"

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

            [spindle.MAIN]
            CW = "M3"
            CCW = "M4"
            OFF = "M5"
            RPM = "S{rpm}"
            VC = "G96 S{value}"
            CSS_OFF = "G97"
            RPM_MAX = "G50 S{value}"

            [spindle.TOOL]
            CW = "M88"
            CCW = "M89"
            OFF = "M90"
            RPM = "S{rpm}"

            [spindle_mode.MAIN]
            AXIS = "M45"
            SPINDLE = "M46"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }

            {{sync}}

            {{extra}}
            """;
    }

    /// <summary>
    /// Compiles a program for a machine.
    /// </summary>
    public static CompileResult Run(string ncx, string machineToml)
    {
        var machineDiagnostics = new Diagnostics("test.toml");
        MachineConfig machine = MachineConfigLoader.LoadText(machineToml, machineDiagnostics)
            ?? throw new InvalidOperationException("The test machine does not load:\n" + machineDiagnostics.ToText());
        Assert.False(machineDiagnostics.HasErrors, machineDiagnostics.ToText());
        NcxProgram program = Parser.Parse(ncx, "T.ncx", new ParserOptions());
        return new FanucCompiler().Compile(program, machine, new CompileOptions());
    }

    /// <summary>
    /// A file of one program named "T", number 1, with the complete header of D34 and the given blocks.
    /// </summary>
    public static string Program(params string[] blocks)
    {
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
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
    /// The lines of the compiled program between its start block and its end on the mill of the sources: every line
    /// after G80 G90 G94 G98 up to M30.
    /// </summary>
    public static string Body(string ncxBlocks)
    {
        string text = TextOf(Run(Program(ncxBlocks.Split('\n')), Mill()));
        const string Start = "%\nO0001 (T)\nG0 G40\nG80 G90 G94 G98\n";
        const string End = "M30\n%\n";
        Assert.StartsWith(Start, text, StringComparison.Ordinal);
        Assert.EndsWith(End, text, StringComparison.Ordinal);
        return text.Substring(Start.Length, text.Length - Start.Length - End.Length);
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

    /// <summary>
    /// The one ERROR of a compile that stops it, with its code.
    /// </summary>
    public static Diagnostic ErrorOf(CompileResult result)
    {
        Assert.Empty(result.Files);
        var errors = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            if (diagnostic.Severity == Severity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        return Assert.Single(errors);
    }

    [GeneratedRegex("^CMP[0-9]{3}$")]
    private static partial Regex CompilerCode();
}
