using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The compiler side of a template: the placeholders are filled from the values, numbers padded by their format
/// suffix, and a missing value is a CFG ERROR (machine-config introduction).
/// </summary>
public sealed class TemplateRenderTests
{
    // The program being compiled, as the diagnostics name it.
    private const string ProgramFile = "PART.ncx";

    // A format suffix pads numbers and the decimal point after it is literal text: G340 T0101. A02. (machine-config
    // introduction and 3, the Nakamura tool spindle).
    [Fact]
    public void Render_NakamuraToolChange_WritesPaddedNumbersAndLiteralPoints()
    {
        Template template = Parse("G340 T{tool:02}{offset:02}. A{next:02}.");
        var values = new TemplateValues();
        values.Set("tool", 1);
        values.Set("offset", 1);
        values.Set("next", 2);
        var diagnostics = new Diagnostics(ProgramFile);

        string? text = template.Render(values, ToolBlock(line: 12), diagnostics);

        Assert.Equal("G340 T0101. A02.", text);
        Assert.Empty(diagnostics.Items);
    }

    // {tool:02} writes 04 and {tool:02}. writes 04. (machine-config introduction); the six-digit Nakamura ATC
    // prepares with three digits and the Mori Seiki tool spindle takes four (machine-config 3); #11{index:03} is
    // the Z wear register 99 as #11099 (machine-config 7).
    [Theory]
    [InlineData("T{tool:02}", "tool", 4, "T04")]
    [InlineData("T{tool:02}.", "tool", 4, "T04.")]
    [InlineData("G341 T{next:03}", "next", 9, "G341 T009")]
    [InlineData("T{tool:04}", "tool", 9001, "T9001")]
    [InlineData("#11{index:03}", "index", 99, "#11099")]
    public void Render_FormatSuffix_PadsTheNumberWithZeros(string text, string name, int number, string expected)
    {
        var values = new TemplateValues();
        values.Set(name, number);

        Assert.Equal(expected, Render(Parse(text), values));
    }

    // Without a suffix a number is written as given, its sign and its decimals included: the Siemens tilt cycle
    // with {dir} = -1 for MOVE=TURN (machine-config 5).
    [Fact]
    public void Render_NumbersWithoutSuffix_AreWrittenAsGiven()
    {
        Template template = Parse("""CYCLE800(1,"TC1",0,57,0,0,0,{a},{b},{c},0,0,0,{dir},100,1)""");
        var values = new TemplateValues();
        values.Set("a", 0);
        values.Set("b", 45.5m);
        values.Set("c", 0);
        values.Set("dir", -1);

        Assert.Equal("""CYCLE800(1,"TC1",0,57,0,0,0,0,45.5,0,0,0,0,-1,100,1)""", Render(template, values));
    }

    // Words are written as given: TOOL CALL 4 Z S1592 with the tool axis letter, G28 U0 W0 with the axis list of
    // HOME X Z (architecture 8; machine-config introduction, {axis} and {axes}).
    [Fact]
    public void Render_ToolCall_WritesTheAxisLetter()
    {
        Template template = Parse("TOOL CALL {tool} {axis} S{rpm}");
        var values = new TemplateValues();
        values.Set("tool", 4);
        values.Set("axis", "Z");
        values.Set("rpm", 1592);

        Assert.Equal("TOOL CALL 4 Z S1592", Render(template, values));
    }

    [Fact]
    public void Render_Home_WritesTheAxisList()
    {
        var values = new TemplateValues();
        values.Set("axes", "U0 W0");

        Assert.Equal("G28 U0 W0", Render(Parse("G28 {axes}"), values));
    }

    // A template may span lines with \n (machine-config 3): the DMG structure programming change renders two lines,
    // the T word and the TC call.
    [Fact]
    public void Render_TemplateWithLineBreak_WritesTwoLines()
    {
        Template template = Parse("""
            T="{name}"
            TC({offset},,,{kind},{b},{c})
            """);
        var values = new TemplateValues();
        values.Set("name", "MILL_D10");
        values.Set("offset", 1);
        values.Set("kind", 2);
        values.Set("b", 90);
        values.Set("c", 0);

        string? text = Render(template, values);

        Assert.NotNull(text);
        string[] lines = ["T=\"MILL_D10\"", "TC(1,,,2,90,0)"];
        Assert.Equal(lines, text.Split('\n'));
    }

    // A missing placeholder value leaves the template unusable and the compiler reports it: a CFG ERROR on the block,
    // naming the placeholder and the template, and no text (machine-config introduction, D98).
    [Fact]
    public void Render_MissingValue_IsCfgErrorNamingThePlaceholderAndTheTemplate()
    {
        Template template = Parse("T{tool} M6");
        var diagnostics = new Diagnostics(ProgramFile);

        string? text = template.Render(new TemplateValues(), ToolBlock(line: 12), diagnostics);

        Assert.Null(text);
        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCodes.TemplateValueMissing, diagnostic.Code);
        Assert.Equal(12, diagnostic.Line);
        Assert.Equal(
            """PART.ncx(12): ERROR CFG100: The template "T{tool} M6" has no value for {tool}; """
            + "a missing placeholder value leaves the template unusable (machine-config introduction).",
            diagnostic.ToText());
    }

    // Every missing placeholder is reported, as the template writes it.
    [Fact]
    public void Render_TwoMissingValues_ReportsEachPlaceholder()
    {
        Template template = Parse("G340 T{tool:02}{offset:02}. A{next:02}.");
        var values = new TemplateValues();
        values.Set("tool", 1);
        var diagnostics = new Diagnostics(ProgramFile);

        string? text = template.Render(values, ToolBlock(line: 12), diagnostics);

        Assert.Null(text);
        Assert.Collection(
            diagnostics.Items,
            offset => Assert.Contains("has no value for {offset:02};", offset.Message, StringComparison.Ordinal),
            next => Assert.Contains("has no value for {next:02};", next.Message, StringComparison.Ordinal));
    }

    // A placeholder that stands twice is one value, and missing it is one ERROR.
    [Fact]
    public void Render_SamePlaceholderMissingTwice_IsReportedOnce()
    {
        var diagnostics = new Diagnostics(ProgramFile);

        string? text = Parse("L725({angle},{angle})").Render(new TemplateValues(), ToolBlock(line: 12), diagnostics);

        Assert.Null(text);
        Assert.Single(diagnostics.Items);
    }

    // On a generated block the ERROR carries the line of the block it was generated for (D98).
    [Fact]
    public void Render_MissingValueOnAGeneratedBlock_CarriesTheOriginLine()
    {
        var block = new Block
        {
            Line = 21,
            Words = [new Word { Key = "TOOL", Value = new IntegerValue(4, "4") }],
            IsGenerated = true,
            OriginLine = 20,
        };
        var diagnostics = new Diagnostics(ProgramFile);

        _ = Parse("T{tool} M6").Render(new TemplateValues(), block, diagnostics);

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(21, diagnostic.Line);
        Assert.Equal(20, diagnostic.OriginLine);
    }

    // A value no placeholder of the template names is not written.
    [Fact]
    public void Render_ValueTheTemplateDoesNotName_IsNotWritten()
    {
        var values = new TemplateValues();
        values.Set("tool", 4);
        values.Set("next", 5);

        Assert.Equal("T4 M6", Render(Parse("T{tool} M6"), values));
    }

    // A template of the machine file that parses without a diagnostic.
    private static Template Parse(string text)
    {
        var diagnostics = new Diagnostics("machine.toml");
        var template = new Template(text, 1, diagnostics);
        Assert.Empty(diagnostics.Items);
        return template;
    }

    // Renders for a block of the program and asserts that nothing was reported.
    private static string? Render(Template template, TemplateValues values)
    {
        var diagnostics = new Diagnostics(ProgramFile);
        string? text = template.Render(values, ToolBlock(line: 12), diagnostics);
        Assert.Empty(diagnostics.Items);
        return text;
    }

    // The block the template is rendered for.
    private static Block ToolBlock(int line)
    {
        return new Block { Line = line, Words = [new Word { Key = "TOOL", Value = new IntegerValue(4, "4") }] };
    }
}
