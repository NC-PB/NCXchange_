using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Writing;

/// <summary>
/// The builder a reader assembles its program with: Begin(line).Verb(...).Word(key, addr, value)...End() per block,
/// the words sorted into canonical order at End() (architecture 7, code-guidelines 5 Builder; phase 0, P0-06).
/// </summary>
public sealed class NcxBuilderTests
{
    // A reader discovers the words of a block in source order; the builder sorts them at End() into the order of the
    // rank table and marks the verb (code-guidelines 5, Builder; language 5 rules 1 and 6).
    [Fact]
    public void End_WordsInSourceOrder_AreSortedIntoTheCanonicalOrder()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        builder.Begin(12)
            .Word("Y", null, Integer(2))
            .Verb("LINE")
            .Word("COMP", null, Ident("LEFT"))
            .Word("X", null, Integer(7))
            .Word("F", null, Integer(200))
            .End();
        Block block = Assert.Single(builder.Build().Blocks);

        Assert.Equal("LINE X=7 Y=2 F=200 COMP=LEFT", string.Join(' ', CanonicalWords(block)));
        Assert.Equal("LINE", block.Verb?.Key);
        Assert.Equal(12, block.Line);
    }

    // A verb with a value, ARC=CW, is the verb of its block (language 4.3, 5 rule 1).
    [Fact]
    public void Verb_WithAValue_IsTheVerbOfItsBlock()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        builder.Begin(1).Word("R", null, Integer(5)).Verb("ARC", Ident("CW")).Word("X", null, Integer(2)).End();
        Block block = Assert.Single(builder.Build().Blocks);

        Assert.Equal("ARC=CW X=2 R=5", NcxWriter.WriteBlock(block));
        Assert.Equal("ARC=CW", block.Verb?.ToCanonical());
    }

    // A machine axis word has no catalog entry and stands behind the standard axes (D93); the native parameters of a
    // CYCLE:controller=n block keep their source order after the cycle words (D94).
    [Fact]
    public void End_MachineAxisAndNativeParameters_TakeTheirPlacesWithoutAMachineFile()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        builder.Begin(1).Word("Z2", null, Integer(-58)).Verb("RAPID").Word("C", null, Integer(0)).End();
        builder.Begin(2)
            .Word("Q215", null, Integer(0))
            .Word("CYCLE", "HEIDENHAIN", Integer(251))
            .Word("Q218", null, Integer(60))
            .End();
        IReadOnlyList<Block> blocks = builder.Build().Blocks;

        Assert.Equal("RAPID C=0 Z2=-58", NcxWriter.WriteBlock(blocks[0]));
        Assert.Null(blocks[0].Find("Z2")?.Definition);
        Assert.Equal("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60", NcxWriter.WriteBlock(blocks[1]));
    }

    // Raw(controller, text) is the word RAW:controller="text" of the block, source text kept verbatim (language 4.1).
    [Fact]
    public void Raw_ControllerAndText_IsTheRawWordOfTheBlock()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        builder.Begin(4).Raw("NAKAMURA", "G300 \"CUT\"").End();

        Assert.Equal("""
            RAW:NAKAMURA="G300 \"CUT\""
            """, NcxWriter.WriteBlock(Assert.Single(builder.Build().Blocks)));
    }

    // A built program is written in the order of the calls, trivia where they were added, and reads back as the same
    // program: the structure pass finds its frame and its program, the writer writes it with LF (D92; language 3,
    // Encoding; language 4.13; phase 0, P0-06).
    [Fact]
    public void Build_WrittenAndReadBack_IsTheSameProgram()
    {
        var diagnostics = new Diagnostics("BUILT.ncx");
        var builder = new NcxBuilder(diagnostics);

        builder.Trivia("; built by a reader");
        builder.Begin(1).Word("NCX", null, Integer(1)).Word("FILE", null, Ident("BEGIN")).End();
        builder.Begin(2)
            .Word("NAME", null, new StringValue("BUILT"))
            .Word("PROGRAM", null, Ident("BEGIN"))
            .Comment("; O0001 (BUILT)")
            .End();
        builder.Begin(3)
            .Word("RPM", null, Integer(1592))
            .Word("TOOL", null, Integer(1))
            .Word("OFFSET", "RAD", Integer(1))
            .Word("OFFSET", "LEN", Integer(1))
            .End();
        builder.Trivia("");
        builder.Begin(5)
            .Verb("RAPID")
            .Word("COMP", null, Ident("OFF"))
            .Word("Y", null, Decimal("-7.025"))
            .Word("X", null, Decimal("50.4"))
            .End();
        builder.Begin(6).Raw("FANUC", "G411 P1").End();
        builder.Begin(7).Word("PROGRAM", null, Ident("END")).End();
        builder.Begin(8).Word("FILE", null, Ident("END")).End();
        builder.Trivia("; end of the source");
        NcxProgram program = builder.Build();
        string written = NcxWriter.Write(program);

        Assert.Equal(
            Lines(
                "; built by a reader",
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"BUILT\"                              ; O0001 (BUILT)",
                "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1592",
                "",
                "RAPID X=50.4 Y=-7.025 COMP=OFF",
                "RAW:FANUC=\"G411 P1\"",
                "PROGRAM=END",
                "FILE=END",
                "; end of the source"),
            written);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        Assert.Equal("BUILT", Assert.Single(program.Programs).Name);
        Assert.Equal(1, program.FileBegin?.Line);
        Assert.Equal(8, program.FileEnd?.Line);
        NcxProgram readBack = Parser.Parse(written, "BUILT.ncx", new ParserOptions());
        Assert.True(readBack.Diagnostics.Items.Count == 0, readBack.Diagnostics.ToText());
        Assert.Equal(written, NcxWriter.Write(readBack));
    }

    // The builder's contract with the reader that calls it: a word belongs to a block that has begun, a block holds at
    // least one word and at most one verb, a comment starts with its semicolon, a trivia line holds no word, and the
    // program is built once with every block ended; a broken contract is a programmer error (code-guidelines 6;
    // language 3, Block and Comment; 5 rule 1).
    [Fact]
    public void Word_WithoutBegin_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        Assert.Throws<InvalidOperationException>(() => builder.Word("X", null, Integer(1)));
    }

    [Fact]
    public void Begin_WhileABlockIsOpen_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1).Verb("LINE");

        Assert.Throws<InvalidOperationException>(() => builder.Begin(2));
    }

    [Fact]
    public void Verb_KeyThatIsNoVerb_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1);

        Assert.Throws<ArgumentException>(() => builder.Verb("X"));
        Assert.Throws<ArgumentException>(() => builder.Verb("SHIFT", Ident("RESET")));
    }

    [Fact]
    public void BlockRule1_TwoVerbs_Throw()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        Assert.Throws<InvalidOperationException>(() => builder.Begin(1).Verb("LINE").Verb("RAPID").End());
    }

    [Fact]
    public void End_BlockWithoutWords_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1).Comment("; a comment alone is trivia");

        Assert.Throws<InvalidOperationException>(builder.End);
    }

    [Theory]
    [InlineData("no semicolon")]
    [InlineData("; two\nlines")]
    public void Comment_ThatIsNoCommentOfOneLine_Throws(string comment)
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1).Verb("LINE");

        Assert.Throws<ArgumentException>(() => builder.Comment(comment));
    }

    [Theory]
    [InlineData("LINE X=1")]
    [InlineData("; two\nlines")]
    [InlineData("; carriage return\r")]
    public void Trivia_ThatIsNoCommentOnlyOrBlankLine_Throws(string text)
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));

        Assert.Throws<ArgumentException>(() => builder.Trivia(text));
    }

    [Fact]
    public void Trivia_WhileABlockIsOpen_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1).Verb("LINE");

        Assert.Throws<InvalidOperationException>(() => builder.Trivia("; between the words"));
    }

    [Fact]
    public void Build_WhileABlockIsOpen_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Begin(1).Verb("LINE");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Twice_Throws()
    {
        var builder = new NcxBuilder(new Diagnostics("built.ncx"));
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    private static List<string> CanonicalWords(Block block)
    {
        var words = new List<string>();
        foreach (Word word in block.Words)
        {
            words.Add(word.ToCanonical());
        }

        return words;
    }

    private static IntegerValue Integer(long number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }

    private static DecimalValue Decimal(string text)
    {
        return new DecimalValue(decimal.Parse(text, CultureInfo.InvariantCulture), text);
    }

    private static IdentValue Ident(string name)
    {
        return new IdentValue(name);
    }

    // The lines of a file, each ended with LF.
    private static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }
}
