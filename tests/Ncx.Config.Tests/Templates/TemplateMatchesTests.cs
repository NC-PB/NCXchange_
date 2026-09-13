using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The reader side of a template: the text the compiler renders is recognized and its placeholders are captured, M
/// and G codes by number (architecture 6, machine-config introduction and 5, D105).
/// </summary>
public sealed class TemplateMatchesTests
{
    // G340 T0101. A02. changes to tool 1 with offset 1 and prepares tool 2 (controller-mapping 3, machine-config 3).
    [Fact]
    public void Matches_NakamuraToolChange_CapturesToolOffsetAndNext()
    {
        Template template = Parse("G340 T{tool:02}{offset:02}. A{next:02}.");

        Assert.True(template.Matches("G340 T0101. A02.", out TemplateValues captured));
        Assert.Equal(1m, Number(captured, "tool"));
        Assert.Equal(1m, Number(captured, "offset"));
        Assert.Equal(2m, Number(captured, "next"));
    }

    // G340 T001001 A009 with six digits on the 120-tool ATCs (controller-mapping 3, machine-config 3).
    [Fact]
    public void Matches_NakamuraSixDigitToolChange_ReadsThreeDigitsEach()
    {
        Template template = Parse("G340 T{tool:03}{offset:03} A{next:03}");

        Assert.True(template.Matches("G340 T001001 A009", out TemplateValues captured));
        Assert.Equal(1m, Number(captured, "tool"));
        Assert.Equal(1m, Number(captured, "offset"));
        Assert.Equal(9m, Number(captured, "next"));
    }

    // G361 B0 D1. performs the change with the index angle 0 and the kind 1., a turning tool (controller-mapping 3).
    [Fact]
    public void Matches_MoriToolChange_ReadsTheIndexAngleAndTheKind()
    {
        Template template = Parse("G361 B{b} D{kind}");

        Assert.True(template.Matches("G361 B0 D1.", out TemplateValues captured));
        Assert.Equal(0m, Number(captured, "b"));
        Assert.Equal(1m, Number(captured, "kind"));
    }

    // T{tool} M6 matches T4 M6 and not T4 (phase 2, P2-02): the whole text is the template.
    [Fact]
    public void Matches_ToolChange_MatchesT4M6()
    {
        Assert.True(Parse("T{tool} M6").Matches("T4 M6", out TemplateValues captured));
        Assert.Equal(4m, Number(captured, "tool"));
    }

    [Fact]
    public void Matches_ToolChange_DoesNotMatchT4Alone()
    {
        Assert.False(Parse("T{tool} M6").Matches("T4", out TemplateValues captured));
        Assert.False(captured.Has("tool"));
    }

    // A placeholder whose value is a number takes a number only.
    [Theory]
    [InlineData("TA M6")]
    [InlineData("T M6")]
    [InlineData("T4.4.4 M6")]
    public void Matches_NumberPlaceholder_DoesNotMatchWords(string text)
    {
        Assert.False(Parse("T{tool} M6").Matches(text, out _));
    }

    // M{mark} P{paths} matches M106 P12, the wait with a path list (machine-config 5, [sync]).
    [Fact]
    public void Matches_WaitWithPaths_CapturesMarkAndPaths()
    {
        Assert.True(Parse("M{mark} P{paths}").Matches("M106 P12", out TemplateValues captured));
        Assert.Equal(106m, Number(captured, "mark"));
        Assert.Equal(12m, Number(captured, "paths"));
    }

    // Readers compare codes by number, so the normalized M8 of the table matches a source M08 and M008 (machine-config
    // 5, D105).
    [Theory]
    [InlineData("M8")]
    [InlineData("M08")]
    [InlineData("M008")]
    public void Matches_M8_MatchesEverySpellingOfTheNumber(string text)
    {
        Assert.True(Parse("M8").Matches(text, out _));
    }

    [Theory]
    [InlineData("G1")]
    [InlineData("G01")]
    public void Matches_G1_MatchesEverySpellingOfTheNumber(string text)
    {
        Assert.True(Parse("G1").Matches(text, out _));
    }

    // By number means the whole number: M8 is not M80, M18 or M9.
    [Theory]
    [InlineData("M80")]
    [InlineData("M18")]
    [InlineData("M9")]
    [InlineData("M")]
    [InlineData("G8")]
    public void Matches_M8_DoesNotMatchAnotherCode(string text)
    {
        Assert.False(Parse("M8").Matches(text, out _));
    }

    // A template written with a leading zero compares by number just the same (D105).
    [Fact]
    public void Matches_TemplateWrittenM08_MatchesM8()
    {
        Assert.True(Parse("M08").Matches("M8", out _));
    }

    // A placeholder is read by its number too: M0106 P12 is the mark 106 (D105).
    [Fact]
    public void Matches_WaitWithPaths_ReadsTheMarkByNumber()
    {
        Assert.True(Parse("M{mark} P{paths}").Matches("M0106 P12", out TemplateValues captured));
        Assert.Equal(106m, Number(captured, "mark"));
        Assert.Equal(12m, Number(captured, "paths"));
    }

    // A G code with a decimal part keeps it: G7.1 is not G7.2 (machine-config 5, [transform]).
    [Fact]
    public void Matches_CylinderOn_ComparesTheDecimalGCodeByNumber()
    {
        Template template = Parse("G7.1 C{r}");

        Assert.True(template.Matches("G07.1 C30.", out TemplateValues captured));
        Assert.Equal(30m, Number(captured, "r"));
        Assert.False(template.Matches("G7.2 C30.", out _));
    }

    // Whitespace between words is tolerated: none, several blanks, a tab, blanks at both ends (phase 2, P2-02).
    [Theory]
    [InlineData("G96 S120")]
    [InlineData("G96S120")]
    [InlineData("G96   S120")]
    [InlineData("G96\tS120")]
    [InlineData("  G96 S120  ")]
    public void Matches_BlanksBetweenWords_AreTolerated(string text)
    {
        Assert.True(Parse("G96 S{value}").Matches(text, out TemplateValues captured));
        Assert.Equal(120m, Number(captured, "value"));
    }

    // The tool axis letter is a word: TOOL CALL 4 Z S1592 (architecture 8; machine-config introduction, {axis}).
    [Fact]
    public void Matches_ToolCall_CapturesTheAxisLetter()
    {
        Template template = Parse("TOOL CALL {tool} {axis} S{rpm}");

        Assert.True(template.Matches("TOOL CALL 4 Z S1592", out TemplateValues captured));
        Assert.Equal(4m, Number(captured, "tool"));
        Assert.Equal("Z", Text(captured, "axis"));
        Assert.Equal(1592m, Number(captured, "rpm"));
    }

    // The axis list of HOME in the machine's address form: G28 U0 W0 (architecture 8; machine-config 3, [home]).
    [Fact]
    public void Matches_Home_CapturesTheAxisList()
    {
        Assert.True(Parse("G28 {axes}").Matches("G28 U0 W0", out TemplateValues captured));
        Assert.Equal("U0 W0", Text(captured, "axes"));
    }

    // The Siemens wait names the channels as a list, 1,2 (machine-config 5, [sync]).
    [Fact]
    public void Matches_SiemensWait_CapturesTheChannelList()
    {
        Assert.True(Parse("WAITM({mark},{channels})").Matches("WAITM(1,1,2)", out TemplateValues captured));
        Assert.Equal(1m, Number(captured, "mark"));
        Assert.Equal("1,2", Text(captured, "channels"));
    }

    // The tool name between the quotes of T="NAME" (machine-config 3, Siemens).
    [Fact]
    public void Matches_ToolName_CapturesTheNameBetweenTheQuotes()
    {
        Assert.True(Parse("""T="{name}" M6""").Matches("""T="MILL_D10" M6""", out TemplateValues captured));
        Assert.Equal("MILL_D10", Text(captured, "name"));
    }

    // A template that spans lines with \n matches the same lines (machine-config 3): the DMG structure programming
    // change T="NAME" and TC(1,,,2,90,0) (controller-mapping 3).
    [Fact]
    public void Matches_TemplateWithLineBreak_MatchesTwoLines()
    {
        Assert.True(DmgToolChange().Matches("""
            T="NAME"
            TC(1,,,2,90,0)
            """, out TemplateValues captured));
        Assert.Equal("NAME", Text(captured, "name"));
        Assert.Equal(1m, Number(captured, "offset"));
        Assert.Equal(2m, Number(captured, "kind"));
        Assert.Equal(90m, Number(captured, "b"));
        Assert.Equal(0m, Number(captured, "c"));
    }

    // The CAM sources end their lines with CR LF (implementation 01-findings, line endings).
    [Fact]
    public void Matches_TemplateWithLineBreak_ToleratesCrLf()
    {
        Assert.True(DmgToolChange().Matches("T=\"NAME\"\r\nTC(1,,,2,90,0)\r\n", out _));
    }

    [Fact]
    public void Matches_TemplateWithLineBreak_DoesNotMatchOneLine()
    {
        Assert.False(DmgToolChange().Matches("""T="NAME" TC(1,,,2,90,0)""", out _));
    }

    // A placeholder that stands twice stands for one value: the text of two values is not this template's.
    [Fact]
    public void Matches_SamePlaceholderTwice_NeedsTheSameValue()
    {
        Template template = Parse("L725({angle},{angle})");

        Assert.True(template.Matches("L725(10,10.0)", out TemplateValues captured));
        Assert.Equal(10m, Number(captured, "angle"));
        Assert.False(template.Matches("L725(10,20)", out _));
    }

    // The same string describes both directions (architecture 6): every template the specification writes out,
    // rendered from a sample value for each placeholder, is matched back to the same values (machine-config
    // introduction, 3, 5, 5a and 7).
    [Theory]
    [InlineData("T{tool} M6")]
    [InlineData("M6")]
    [InlineData("T{tool}")]
    [InlineData("T0 M6")]
    [InlineData("TOOL CALL {tool} {axis} S{rpm}")]
    [InlineData("TOOL DEF {tool}")]
    [InlineData("""T="{name}" M6""")]
    [InlineData("T{tool:02}{offset:02}")]
    [InlineData("G340 T{tool:02}{offset:02}. A{next:02}.")]
    [InlineData("G341 T{next:02}.")]
    [InlineData("G340 T{tool:03}{offset:03} A{next:03}")]
    [InlineData("G341 T{next:03}")]
    [InlineData("G361 B{b} D{kind}")]
    [InlineData("T{tool:04}")]
    [InlineData("""
        T0
        G361
        """)]
    [InlineData("""
        T="{name}"
        TC({offset},,,{kind},{b},{c})
        """)]
    [InlineData("G28 {axes}")]
    [InlineData("G30 P{point} {axes}")]
    [InlineData("G330 A{point}.")]
    [InlineData("L711({order})")]
    [InlineData("G50 {axes}")]
    [InlineData("PRESETON({axis},{value})")]
    [InlineData("M19 S{angle}")]
    [InlineData("S{rpm}")]
    [InlineData("G96 S{value}")]
    [InlineData("G50 S{value}")]
    [InlineData("L707({angle})")]
    [InlineData("COUPON(S3,S4)")]
    [InlineData("L726({angle})")]
    [InlineData("G54 M428")]
    [InlineData("M{mark}")]
    [InlineData("M{mark} P{paths}")]
    [InlineData("WAITM({mark},{channels})")]
    [InlineData("START({channel})")]
    [InlineData("H7={value} M7")]
    [InlineData("G7.1 C{r}")]
    [InlineData("TRACYL({d})")]
    [InlineData("G43.4 H{offset}")]
    [InlineData("G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}")]
    [InlineData("""CYCLE800(1,"TC1",0,57,0,0,0,{a},{b},{c},0,0,0,{dir},100,1)""")]
    [InlineData("PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move}")]
    [InlineData("PLANE AXIAL A{a} B{b} C{c} {move}")]
    [InlineData("FGREF[{axis}]={radius}")]
    [InlineData("M140 MB MAX")]
    [InlineData("M140 MB{distance}")]
    [InlineData("G5.1 Q1")]
    [InlineData("""
        CYCL DEF 32.0 TOLERANZ
        CYCL DEF 32.1 T{tol}
        CYCL DEF 32.2 HSC-MODE:{mode} TA{rotary}
        """)]
    [InlineData("CYCLE832({tol},{mode},{rotary})")]
    [InlineData("#11{index:03}")]
    [InlineData("RG{index}")]
    [InlineData("RAPID {position:tool_change} FRAME=MACHINE")]
    public void Matches_TemplateOfTheSpecification_ReadsBackWhatItRenders(string text)
    {
        TemplateSamples.AssertRoundTrip(Parse(text));
    }

    // A text that is not the template's captures nothing.
    [Fact]
    public void Matches_NoMatch_CapturesNothing()
    {
        Assert.False(Parse("M{mark} P{paths}").Matches("M106", out TemplateValues captured));
        Assert.False(captured.Has("mark"));
    }

    // The DMG structure programming change, two lines (machine-config 3).
    private static Template DmgToolChange()
    {
        return Parse("""
            T="{name}"
            TC({offset},,,{kind},{b},{c})
            """);
    }

    // A template of the machine file that parses without a diagnostic.
    private static Template Parse(string text)
    {
        var diagnostics = new Diagnostics("machine.toml");
        var template = new Template(text, 1, diagnostics);
        Assert.Empty(diagnostics.Items);
        return template;
    }

    private static decimal Number(TemplateValues values, string name)
    {
        Assert.True(values.TryGetNumber(name, out decimal number), $"No number for {name}.");
        return number;
    }

    private static string Text(TemplateValues values, string name)
    {
        Assert.True(values.TryGetText(name, out string? text), $"No text for {name}.");
        return text;
    }
}
