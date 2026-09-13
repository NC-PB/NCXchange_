using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The templates of a machine file are parsed when the loader reads their keys, so that a template that cannot be
/// parsed leaves the file with an ERROR on the line of its key (machine-config introduction; phase 2: a bad file
/// reports the line; wave-1 question #61).
/// </summary>
public sealed class MachineConfigLoaderTemplateTests
{
    private const string MachineFile = "templates.toml";

    // Machine-config introduction, wave-1 question #61: a placeholder that is never closed leaves the template
    // unusable; the loader reports it on the line of its key and gives no machine, because the run stops on ERROR.
    [Fact]
    public void Template_UnclosedPlaceholderOfTheToolChange_IsErrorOnTheLineOfItsKey()
    {
        var diagnostics = new Diagnostics(MachineFile);

        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Malformed"
            controller = "fanuc"

            [tool_change]
            change = "T{tool M6"
            """, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, error.Code);
        Assert.Equal(6, error.Line);
        Assert.Equal(
            """The template "T{tool M6" opens a placeholder that is never closed (machine-config introduction).""",
            error.Message);
        Assert.Null(machine);
    }

    // A function value is a template as well (machine-config 5): a format suffix that is no width padded with zeros is
    // reported on the line of its state.
    [Fact]
    public void Template_UnknownFormatSuffixOfAFunctionState_IsErrorOnTheLineOfItsState()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Malformed"
            controller = "fanuc"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }
            HIGH_PRESSURE = { ON = "H7={value:x2} M7", OFF = "M9" }
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.TemplateFormatUnknown, error.Code);
        Assert.Equal(7, error.Line);
    }

    // The clamps of a rotary axis, the [workpiece] selection and the native system variables are templates of the
    // machine file too (machine-config 4, 5, 7): braces around no name are reported on their lines.
    [Fact]
    public void Template_BracesAroundNoName_AreErrorsOnTheLinesOfTheirKeys()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Malformed"
            controller = "fanuc"

            [[axis]]
            id = "C1"
            ncx = "C"
            kind = "rotary"
            clamp = { ON = "M10 {}", OFF = "M11" }

            [workpiece]
            MAIN = "G54 M{ }"

            [system_variables]
            SYS_WEAR_Z = "#11{index:03}"
            SYS_POS_X = "#{-}"
            """);

        var lines = new List<int>();
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, diagnostic.Code);
            lines.Add(diagnostic.Line);
        }

        Assert.Equal([9, 12, 16], lines);
    }

    // Wave-1 question #61: a template text that several keys write is parsed once and reported on the first of them.
    [Fact]
    public void Template_SameTextUnderTwoKeys_IsReportedOnceOnTheFirstKey()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Malformed"
            controller = "fanuc"
            default_spindle = "S1"

            [[resource]]
            id = "S1"
            type = "work_spindle"

            [[resource]]
            id = "S2"
            type = "work_spindle"

            [spindle.MAIN]
            RPM = "S{rpm"

            [spindle.SUB]
            RPM = "S{rpm"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(15, error.Line);
    }

    // The NCX text of an expansion rule is no template: the expander parses it, and its braces are expressions
    // (machine-config 5a, language 4.12).
    [Fact]
    public void ExpansionRule_ExpressionInBraces_IsNoTemplateAndNotReported()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Rules"
            controller = "fanuc"

            [func]
            PALLET_CHANGE = { RUN = "M60", pre = ["VAR:Q1={$Q1 + 1}", "RAPID {position:tool_change} FRAME=MACHINE"] }
            """);

        Assert.Equal("", diagnostics.ToText());
    }

    // Every template of a machine file that parses is loaded as written, its M and G codes normalized (D105).
    [Fact]
    public void Template_ThatParses_LoadsWithoutDiagnostic()
    {
        var diagnostics = new Diagnostics(MachineFile);

        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Nakamura tool change"
            controller = "fanuc"

            [tool_change]
            change = "G340 T{tool:02}{offset:02}. A{next:02}."
            unload = "G00 T{tool:02}00"
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.Equal("G340 T{tool:02}{offset:02}. A{next:02}.", machine?.ToolChange?.Change);
        Assert.Equal("G0 T{tool:02}00", machine?.ToolChange?.Unload);
    }

    // The diagnostics of loading a machine file.
    private static Diagnostics Report(string toml)
    {
        var diagnostics = new Diagnostics(MachineFile);
        MachineConfigLoader.LoadText(toml, diagnostics);
        return diagnostics;
    }
}
