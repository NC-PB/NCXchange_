using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// A template is parsed once, in its constructor, into literal text and placeholders with their format suffixes
/// (machine-config introduction, code-guidelines 7).
/// </summary>
public sealed class TemplateParseTests
{
    // The machine file the templates of these tests stand in.
    private const string MachineFile = "machine.toml";

    // Templates are strings with {placeholders}; every placeholder the introduction lists is read by its name
    // (machine-config introduction).
    [Theory]
    [InlineData("tool")]
    [InlineData("name")]
    [InlineData("offset")]
    [InlineData("next")]
    [InlineData("kind")]
    [InlineData("rpm")]
    [InlineData("axis")]
    [InlineData("axes")]
    [InlineData("angle")]
    [InlineData("b")]
    [InlineData("c")]
    [InlineData("mark")]
    [InlineData("channels")]
    [InlineData("paths")]
    [InlineData("r")]
    [InlineData("d")]
    [InlineData("value")]
    [InlineData("index")]
    [InlineData("a")]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("z")]
    [InlineData("move")]
    [InlineData("dir")]
    [InlineData("distance")]
    [InlineData("tol")]
    [InlineData("rotary")]
    [InlineData("mode")]
    public void Constructor_EveryPlaceholderOfTheIntroduction_IsReadByItsName(string name)
    {
        Template template = Parse("W{" + name + "}");

        Placeholder placeholder = Assert.Single(template.Placeholders);
        Assert.Equal(name, placeholder.Name);
        Assert.Equal("", placeholder.Format);
        Assert.Equal(0, placeholder.Width);
    }

    // In expansion rules {position:NAME} stands for the axis words of that entry of [positions]; the entry belongs
    // to the name the value is found by, because one rule may name two positions (machine-config introduction and
    // 5a, D100).
    [Fact]
    public void Constructor_Position_IsNamedWithItsEntry()
    {
        Template template = Parse("RAPID {position:tool_change} FRAME=MACHINE");

        Placeholder placeholder = Assert.Single(template.Placeholders);
        Assert.Equal("position:tool_change", placeholder.Name);
        Assert.Equal("", placeholder.Format);
        Assert.Equal(0, placeholder.Width);
        Assert.True(placeholder.IsText);
    }

    // A format suffix pads numbers: {tool:02} writes 04 (machine-config introduction); the six-digit Nakamura ATC
    // pads to three and the Mori Seiki tool spindle to four (machine-config 3).
    [Theory]
    [InlineData("T{tool:02}", "02", 2)]
    [InlineData("T{tool:03}", "03", 3)]
    [InlineData("T{tool:04}", "04", 4)]
    public void Constructor_FormatSuffix_ReadsTheWidth(string text, string format, int width)
    {
        Placeholder placeholder = Assert.Single(Parse(text).Placeholders);

        Assert.Equal("tool", placeholder.Name);
        Assert.Equal(format, placeholder.Format);
        Assert.Equal(width, placeholder.Width);
    }

    // The template keeps its text as written and lists its placeholders in the order they stand (machine-config 3,
    // the Nakamura tool spindle).
    [Fact]
    public void Constructor_NakamuraToolChange_ListsItsPlaceholdersInOrder()
    {
        Template template = Parse("G340 T{tool:02}{offset:02}. A{next:02}.");

        string[] names = ["tool", "offset", "next"];
        Assert.Equal("G340 T{tool:02}{offset:02}. A{next:02}.", template.Text);
        Assert.Equal(names, Names(template));
    }

    // A template may span lines with \n (machine-config 3); the placeholders of every line are the template's.
    [Fact]
    public void Constructor_TemplateWithLineBreak_ListsThePlaceholdersOfBothLines()
    {
        Template template = Parse("""
            T="{name}"
            TC({offset},,,{kind},{b},{c})
            """);

        string[] names = ["name", "offset", "kind", "b", "c"];
        Assert.Equal(names, Names(template));
    }

    // A template without braces is literal text only, like most function values (machine-config 5).
    [Fact]
    public void Constructor_FunctionValue_HasNoPlaceholder()
    {
        Template template = Parse("M8");

        Assert.Empty(template.Placeholders);
        Assert.Equal("M8", template.Text);
    }

    // The tool name, the axis letter, the axis list, the words of the MOVE option, the channel list of a wait and
    // the axis words of a position are words; every other placeholder is a number (machine-config introduction
    // and 5).
    [Theory]
    [InlineData("name", true)]
    [InlineData("axis", true)]
    [InlineData("axes", true)]
    [InlineData("move", true)]
    [InlineData("channels", true)]
    [InlineData("tool", false)]
    [InlineData("kind", false)]
    [InlineData("b", false)]
    [InlineData("dir", false)]
    [InlineData("mode", false)]
    [InlineData("value", false)]
    [InlineData("point", false)]
    public void IsText_Placeholder_IsTrueForTheValuesThatAreWords(string name, bool isText)
    {
        Placeholder placeholder = Assert.Single(Parse("{" + name + "}").Placeholders);

        Assert.Equal(isText, placeholder.IsText);
    }

    // The placeholder as the template writes it, for the messages that name it.
    [Theory]
    [InlineData("T{tool} M6", "{tool}")]
    [InlineData("T{tool:02}", "{tool:02}")]
    [InlineData("RAPID {position:tool_change}", "{position:tool_change}")]
    public void ToString_Placeholder_IsWrittenAsTheTemplateWritesIt(string text, string written)
    {
        Placeholder placeholder = Assert.Single(Parse(text).Placeholders);

        Assert.Equal(written, placeholder.ToString());
    }

    // A placeholder that is opened and never closed leaves the template unusable (machine-config introduction).
    [Fact]
    public void Constructor_UnclosedPlaceholder_IsCfgError()
    {
        Diagnostic diagnostic = ParseWithOneDiagnostic("T{tool M6", line: 7);

        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, diagnostic.Code);
        Assert.Equal(7, diagnostic.Line);
        Assert.Equal(
            """The template "T{tool M6" opens a placeholder that is never closed (machine-config introduction).""",
            diagnostic.Message);
    }

    // A brace opened inside another placeholder means the first one was never closed; the second is read.
    [Fact]
    public void Constructor_PlaceholderOpenedTwice_ReportsTheFirstAndReadsTheSecond()
    {
        var diagnostics = new Diagnostics(MachineFile);

        var template = new Template("T{tool {offset}", 7, diagnostics);

        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, Assert.Single(diagnostics.Items).Code);
        Assert.Equal("offset", Assert.Single(template.Placeholders).Name);
    }

    [Fact]
    public void Constructor_ClosingBraceWithoutPlaceholder_IsCfgError()
    {
        Diagnostic diagnostic = ParseWithOneDiagnostic("T{tool}} M6", line: 7);

        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, diagnostic.Code);
        Assert.Equal(
            """The template "T{tool}} M6" closes a placeholder that was never opened (machine-config introduction).""",
            diagnostic.Message);
    }

    // A placeholder is a name in braces; braces around anything else are not one (machine-config introduction).
    [Theory]
    [InlineData("T{} M6", "{}")]
    [InlineData("T{to ol} M6", "{to ol}")]
    [InlineData("T{:02}", "{:02}")]
    [InlineData("T{tool:} M6", "{tool:}")]
    [InlineData("RAPID {position:} FRAME=MACHINE", "{position:}")]
    public void Constructor_BracesWithoutAPlaceholderName_IsCfgError(string text, string braces)
    {
        Diagnostic diagnostic = ParseWithOneDiagnostic(text, line: 7);

        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, diagnostic.Code);
        Assert.Equal(
            "The template \"" + text + "\" has " + braces
            + ", which is not a placeholder: a placeholder is a name in braces (machine-config introduction).",
            diagnostic.Message);
    }

    // A format suffix pads numbers with zeros to its width; any other suffix is not a format (machine-config
    // introduction).
    [Theory]
    [InlineData("T{tool:2}", "2", "{tool:2}")]
    [InlineData("T{tool:00}", "00", "{tool:00}")]
    [InlineData("T{tool:0x}", "0x", "{tool:0x}")]
    public void Constructor_UnknownFormatSuffix_IsCfgError(string text, string suffix, string braces)
    {
        Diagnostic diagnostic = ParseWithOneDiagnostic(text, line: 7);

        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCodes.TemplateFormatUnknown, diagnostic.Code);
        Assert.Equal(
            "The template \"" + text + "\" has the format suffix " + suffix + " in " + braces
            + "; a format suffix is a width padded with zeros, such as 02 (machine-config introduction).",
            diagnostic.Message);
    }

    // The message quotes the template as the machine file writes it, \n for a line break and \" for a quote, so
    // that the user finds it in the file.
    [Fact]
    public void Constructor_UnclosedPlaceholderOnTheSecondLine_QuotesTheTemplateAsTheFileWritesIt()
    {
        Diagnostic diagnostic = ParseWithOneDiagnostic("""
            T="{name}"
            TC({offset
            """, line: 7);

        Assert.Equal(
            """The template "T=\"{name}\"\nTC({offset" opens a placeholder that is never closed """
            + "(machine-config introduction).",
            diagnostic.Message);
    }

    // A template of the machine file that parses without a diagnostic.
    private static Template Parse(string text)
    {
        var diagnostics = new Diagnostics(MachineFile);
        var template = new Template(text, 1, diagnostics);
        Assert.Empty(diagnostics.Items);
        return template;
    }

    // A template text with one mistake, and the one diagnostic it gives.
    private static Diagnostic ParseWithOneDiagnostic(string text, int line)
    {
        var diagnostics = new Diagnostics(MachineFile);
        _ = new Template(text, line, diagnostics);
        return Assert.Single(diagnostics.Items);
    }

    private static List<string> Names(Template template)
    {
        var names = new List<string>();
        foreach (Placeholder placeholder in template.Placeholders)
        {
            names.Add(placeholder.Name);
        }

        return names;
    }
}
