using Ncx.Config.Templates;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// Lines of the Nakamura program as the machine writes them, without blanks between the words and often with a blank
/// at the end: a template matches them all the same (phase 2, risks: Template.Matches on free-form builder lines).
/// </summary>
public sealed class TemplateNakamuraLinesTests
{
    // The first path of the Nakamura WY250L program of the specification's examples.
    private const string NakamuraProgram = "sources/NAKAMURA_WY250L_O1000.path1.nc";

    // Each line is read from the program, so that it is the machine's own text, trailing blank and all. The templates
    // are those of nakamura-ntjx.toml where the file has one: M{mark} (the wait), G50 S{value} (RPM_MAX), G96 S{value}
    // (VC) with M3 (CW) in the same block; the turret change T{tool:02}{offset:02} that the file names in a comment,
    // after the G0 of the block; G411 is a builder macro that stays RAW, matched here for its spacing only.
    [Theory]
    [InlineData(4, "G411F12. ", "G411 F{value}", "value", 12)]
    [InlineData(32, "G0T0101", "G0 T{tool:02}{offset:02}", "tool", 1)]
    [InlineData(32, "G0T0101", "G0 T{tool:02}{offset:02}", "offset", 1)]
    [InlineData(33, "G96S120M03 ", "G96 S{value} M3", "value", 120)]
    [InlineData(49, "G0T0656", "G0 T{tool:02}{offset:02}", "tool", 6)]
    [InlineData(49, "G0T0656", "G0 T{tool:02}{offset:02}", "offset", 56)]
    [InlineData(78, "M106 ", "M{mark}", "mark", 106)]
    [InlineData(94, "G50S2000 ", "G50 S{value}", "value", 2000)]
    public void Matches_LineOfTheNakamuraProgram_ToleratesItsSpacing(
        int lineNumber,
        string line,
        string text,
        string name,
        int number)
    {
        Assert.Equal(line, NakamuraLine(lineNumber));
        Template template = Parse(text);

        Assert.True(template.Matches(line, out TemplateValues captured));
        Assert.True(captured.TryGetNumber(name, out decimal found));
        decimal expected = number;
        Assert.Equal(expected, found);
    }

    // M9, the coolant off of [coolant] STANDARD, with a blank at the end (line 113).
    [Fact]
    public void Matches_CoolantOffWithTrailingBlank_Matches()
    {
        string line = NakamuraLine(113);

        Assert.Equal("M9 ", line);
        Assert.True(Parse("M9").Matches(line, out _));
    }

    // G28 {axes} of [home] on G28U0 (line 111): the axis list is the rest of the line (machine-config 3).
    [Fact]
    public void Matches_HomeWithoutBlank_CapturesTheAxis()
    {
        string line = NakamuraLine(111);

        Assert.Equal("G28U0", line);
        Assert.True(Parse("G28 {axes}").Matches(line, out TemplateValues captured));
        Assert.True(captured.TryGetText("axes", out string? axes));
        Assert.Equal("U0", axes);
    }

    // A line of the program by its 1-based number; the Nakamura files end their lines with LF (implementation
    // 01-findings, line endings).
    private static string NakamuraLine(int lineNumber)
    {
        string[] lines = Fixture.ReadText(NakamuraProgram).Split('\n');
        return lines[lineNumber - 1];
    }

    // A template of the machine file that parses without a diagnostic.
    private static Template Parse(string text)
    {
        var diagnostics = new Diagnostics("nakamura-ntjx.toml");
        var template = new Template(text, 1, diagnostics);
        Assert.Empty(diagnostics.Items);
        return template;
    }
}
