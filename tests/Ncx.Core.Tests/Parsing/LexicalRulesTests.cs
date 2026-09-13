using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The lexical rules of language 3: one line one block, words separated by whitespace, KEY[:ADDR][=VALUE], the
/// comment outside strings, strings with their escapes, expressions kept for the expression parser, lowercase
/// normalized, the line ending of the file; a malformed word is a PAR ERROR with its column and nothing is thrown.
/// </summary>
public sealed class LexicalRulesTests
{
    // Keys, addresses and identifiers are uppercase; a parser may accept lowercase and normalize (language 3, Case).
    [Fact]
    public void Case_LowercaseKeysAddressesAndIdentifiers_AreNormalized()
    {
        Block block = ParseText.CleanBlock("line x=1.5 f=200 comp=left spindle:main=cw");

        Assert.Equal("LINE", block.Verb?.Key);
        Assert.Equal("X=1.5", block.Words[1].ToCanonical());
        Assert.Equal("COMP=LEFT", block.Words[3].ToCanonical());
        Assert.Equal("SPINDLE:MAIN=CW", block.Words[4].ToCanonical());
    }

    // Case normalization touches keys, addresses and identifiers, never the content of a string (language 3, Case,
    // string).
    [Fact]
    public void Case_LowercaseInAString_IsKept()
    {
        Block block = ParseText.CleanBlock("comment=\"Mixed Case\" spindle_sync=main,sub");

        Assert.Equal(new StringValue("Mixed Case"), block.Words[0].Value);
        Assert.Equal(new ListValue(["MAIN", "SUB"]), block.Words[1].Value);
    }

    // A ; inside a string does not start a comment; the first ; outside a string does, and the block keeps the
    // comment from the semicolon on, as read (language 3, Comment, string; D92).
    [Fact]
    public void Comment_SemicolonInsideAString_DoesNotEndTheBlock()
    {
        Block block = ParseText.CleanBlock("COMMENT=\"A;B\" ; the real comment; with a second ;");

        Assert.Equal(new StringValue("A;B"), Assert.Single(block.Words).Value);
        Assert.Equal("; the real comment; with a second ;", block.Comment);
    }

    // \" is a quote and \\ a backslash inside a string, and a string may contain spaces (language 3, string).
    [Fact]
    public void String_WithEscapesAndSpaces_IsOneWord()
    {
        Block block = ParseText.CleanBlock("""COMMENT="SIDE \"MILL\" D10 \\ X" """);

        Assert.Equal(new StringValue("SIDE \"MILL\" D10 \\ X"), Assert.Single(block.Words).Value);
    }

    // An expression is kept as one word although it holds spaces, and its text goes to the expression parser, whose
    // canonical print it keeps (language 3, expression; 4.12).
    [Fact]
    public void Expression_WithSpaces_IsOneWordWithItsTree()
    {
        Block block = ParseText.CleanBlock("VAR:Q1={$q1   +   20} ; next");

        var value = Assert.IsType<ExprValue>(Assert.Single(block.Words).Value);
        Assert.Equal("$Q1 + 20", value.Text);
        Assert.NotNull(value.Tree);
        Assert.Equal("; next", block.Comment);
    }

    // Words are separated by whitespace, tabs and several blanks among them (language 3, Word).
    [Fact]
    public void Word_SeparatedByTabsAndBlanks_AreWords()
    {
        Block block = ParseText.CleanBlock("  RAPID\tX=1    Y=2\t");

        Assert.Equal(["RAPID", "X=1", "Y=2"], WordTexts(block));
    }

    // CRLF input reports CRLF, and no carriage return is left in a word, a comment or a trivia line (language 3,
    // Encoding).
    [Fact]
    public void Encoding_CrLfInput_ReportsCrLf()
    {
        NcxProgram program = ParseText.File(
            "; head\r\nFILE=BEGIN NCX=1\r\nPROGRAM=BEGIN ; start\r\nPROGRAM=END\r\n\r\nFILE=END\r\n");

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        Assert.Equal(LineEnding.CrLf, program.LineEnding);
        Assert.Equal("; start", program.Blocks[1].Comment);
        Assert.Equal([new Trivia(1, "; head"), new Trivia(5, "")], program.Trivia);
    }

    // LF input reports LF (language 3, Encoding).
    [Fact]
    public void Encoding_LfInput_ReportsLf()
    {
        NcxProgram program = ParseText.File("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n");

        Assert.Equal(LineEnding.Lf, program.LineEnding);
    }

    // The first line break of the file sets its line ending; a line that ends otherwise is a WARNING, once
    // (language 3, Encoding).
    [Fact]
    public void Encoding_MixedLineEndings_IsWarningAtTheFirstOtherLine()
    {
        NcxProgram program = ParseText.File("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\r\nPROGRAM=END\r\nFILE=END\n");

        Diagnostic warning = ParseText.Single(program.Diagnostics, DiagnosticCodes.MixedLineEndings);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(2, warning.Line);
        Assert.Equal(LineEnding.Lf, program.LineEnding);
        Assert.False(program.Diagnostics.HasErrors);
    }

    // A word that is not KEY, KEY=VALUE or KEY:ADDR=VALUE is an ERROR with the column where it breaks (language 3,
    // Word, KEY, ADDR).
    [Theory]
    [InlineData("RAPID 1X=2", 7)]
    [InlineData("RAPID X:=1", 9)]
    [InlineData("RAPID X-1", 8)]
    [InlineData("RAPID :X=1", 7)]
    public void Word_Malformed_IsErrorWithTheColumn(string text, int column)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, DiagnosticCodes.MalformedWord);
        Assert.Contains($"column {column}", diagnostic.Message, StringComparison.Ordinal);
    }

    // A value is an integer, a decimal, an identifier, a list, a string or an expression; a number has no plus sign,
    // no leading and no trailing dot (language 2 rule 5, language 3, value types).
    [Theory]
    [InlineData("LINE X=1. F=100")]
    [InlineData("LINE X=.5 F=100")]
    [InlineData("LINE X=+5 F=100")]
    [InlineData("LINE X= F=100")]
    [InlineData("LINE X==1 F=100")]
    [InlineData("SPINDLE_SYNC=MAIN,")]
    [InlineData("SPINDLE_SYNC=MAIN,,SUB")]
    [InlineData("COMMENT=\"A\"B")]
    [InlineData("VAR:Q1={1}2")]
    [InlineData("LINE X=1.2.3 F=100")]
    public void Value_Malformed_IsError(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        ParseText.Single(diagnostics, DiagnosticCodes.MalformedValue);
    }

    // A string ends with its quote, an expression with its brace (language 3, string, expression).
    [Theory]
    [InlineData("COMMENT=\"OPEN ; no comment", "PAR004")]
    [InlineData("COMMENT=\"A\\B\"", "PAR005")]
    [InlineData("VAR:Q1={$Q1 + 1", "PAR006")]
    public void Value_StringOrExpressionNotClosed_IsError(string text, string code)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        ParseText.Single(diagnostics, code);
    }

    // An expression that the grammar of language 4.12 does not accept is the ERROR of the expression parser, on the
    // line of its block (P0-05).
    [Fact]
    public void Expression_NotOfTheGrammar_IsTheErrorOfTheExpressionParser()
    {
        NcxProgram program = ParseText.File(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nVAR:Q1={$Q1 +}\nPROGRAM=END\nFILE=END\n");

        Diagnostic diagnostic = ParseText.Single(program.Diagnostics, DiagnosticCodes.ExpressionMissingOperand);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal("VAR:Q1={$Q1 +}", program.Blocks[2].SourceText);
    }

    // An integer is kept as a number with its text; one too large for that is an ERROR, not an exception
    // (language 3, integer; code-guidelines 6).
    [Fact]
    public void Integer_TooLarge_IsError()
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block("ORIGIN=99999999999999999999999", diagnostics);

        ParseText.Single(diagnostics, DiagnosticCodes.NumberOutOfRange);
    }

    // Numbers are kept with their text, never reformatted (language 2 rule 5).
    [Fact]
    public void Number_Parsed_KeepsItsText()
    {
        Block block = ParseText.CleanBlock("RAPID X=50.40 Y=-7.025 Z=007");

        Assert.Equal(new DecimalValue(50.4m, "50.40"), block.Words[1].Value);
        Assert.Equal(new DecimalValue(-7.025m, "-7.025"), block.Words[2].Value);
        Assert.Equal(new IntegerValue(7, "007"), block.Words[3].Value);
    }

    // The parser never throws for user input; it returns the program with diagnostics (P0-04 scope).
    [Theory]
    [InlineData("")]
    [InlineData("\"")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("=")]
    [InlineData(":")]
    [InlineData("@")]
    [InlineData("X=\"\\")]
    [InlineData("X={")]
    [InlineData("X=:")]
    [InlineData("\r")]
    [InlineData("\n\n\n")]
    [InlineData("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nSUB=BEGIN\nSUB=END\nPROGRAM=END\nPROGRAM=END\nFILE=END\nX")]
    [InlineData("ä=1 X=ä ä")]
    public void Parse_AnyText_ReturnsTheProgramWithoutThrowing(string text)
    {
        NcxProgram program = ParseText.File(text);

        Assert.NotNull(program);
        Assert.Equal("test.ncx", program.FileName);
    }

    private static List<string> WordTexts(Block block)
    {
        var texts = new List<string>();
        foreach (Word word in block.Words)
        {
            texts.Add(word.ToCanonical());
        }

        return texts;
    }
}
