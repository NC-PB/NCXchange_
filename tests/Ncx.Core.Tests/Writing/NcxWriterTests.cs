using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Writing;

/// <summary>
/// The canonical writer: the words of a block in the order of the rank table, numbers as read, strings escaped, the
/// comment in column 57, comment-only and blank lines as read, the line ending of the file (language 2 rule 7, 5 rules 6
/// and 7; D90, D92, D93; phase 0, P0-06).
/// </summary>
public sealed class NcxWriterTests
{
    // The column of the semicolon of a block's comment (language 5 rule 7).
    private const int CommentColumn = 57;

    // A hand-written block in free order formats into the canonical order: the verb, the axis words, F, the tool
    // words, then the comment in column 57 (language 5 rules 6 and 7; phase 0, P0-06).
    [Fact]
    public void Rule6_BlockInFreeOrder_WritesTheCanonicalOrder()
    {
        string written = Canonical("Y=2 LINE COMP=LEFT X=7 F=200 ; note");

        Assert.Equal("LINE X=7 Y=2 F=200 COMP=LEFT                            ; note", written);
    }

    // The canonical text is a fixed point: formatting it again changes nothing (language 2 rule 7; phase 0, P0-06).
    [Fact]
    public void Rule6_CanonicalBlock_IsStableOnASecondPass()
    {
        string once = Canonical("Y=2 LINE COMP=LEFT X=7 F=200 ; note");

        Assert.Equal(once, Canonical(once));
    }

    // SKIP stands first, as the slash does on every control, then the structural words; the header block of the
    // examples puts FEED_MODE and COMP before the state words; the tool words stand before the spindle words;
    // CYCLE_RETRACT stands before CYCLE_F (language 5 rule 6, D90 buckets 1, 8 to 13).
    [Theory]
    [InlineData("LINE X=1 SKIP", "SKIP LINE X=1")]
    [InlineData("LINE X=1 SKIP=3", "SKIP=3 LINE X=1")]
    [InlineData("NUMBER=1 NAME=\"SHAFT\" PROGRAM=BEGIN", "PROGRAM=BEGIN NAME=\"SHAFT\" NUMBER=1")]
    [InlineData(
        "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN COMP=OFF CYCLE=OFF",
        "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF")]
    [InlineData("RPM=1592 OFFSET:RAD=1 TOOL=1 OFFSET:LEN=1", "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1592")]
    [InlineData(
        "CYCLE=DRILL CYCLE_F=565 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CLEARANCE=5 SURFACE=0",
        "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565")]
    [InlineData("SHIFT=RESET COOLANT=ON", "COOLANT=ON SHIFT=RESET")]
    public void Rule6_WordsOfTheRankTable_StandInItsOrder(string line, string expected)
    {
        Assert.Equal(expected, Canonical(line));
    }

    // Words of one key with several addresses sort by the address text (language 5 rule 6).
    [Theory]
    [InlineData("COOLANT:THROUGH=ON COOLANT:AIR=ON", "COOLANT:AIR=ON COOLANT:THROUGH=ON")]
    [InlineData("VAR:Q3=1 VAR:Q1=2", "VAR:Q1=2 VAR:Q3=1")]
    public void Rule6_WordsOfOneKey_SortByTheAddressText(string line, string expected)
    {
        Assert.Equal(expected, Canonical(line));
    }

    // A machine axis word has no catalog entry and stands behind X Y Z A B C, alphabetically by letter and then by
    // number, the absolute words before the incremental ones, whatever the machine (D90 bucket 3, D93).
    [Fact]
    public void D93_MachineAxisWords_StandBehindTheStandardAxesByLetterThenNumber()
    {
        Assert.Equal("RAPID X=1 C2=5 W=3 Z2=2 IX=2 IZ2=1", Canonical("RAPID IZ2=1 W=3 X=1 Z2=2 C2=5 IX=2"));
    }

    // The native parameters of a CYCLE:controller=n block stand after the cycle words in source order (D90 bucket 13,
    // D94).
    [Fact]
    public void D94_NativeParameters_StandAfterTheCycleWordsInSourceOrder()
    {
        Assert.Equal("CYCLE:HEIDENHAIN=251 Q218=60 Q215=0", Canonical("Q218=60 CYCLE:HEIDENHAIN=251 Q215=0"));
    }

    // Every value form writes back as it was read: numbers untouched, trailing zeros included, identifiers, lists,
    // strings, expressions, bare words (language 2 rule 5; language 3, value types; phase 0, F8).
    [Theory]
    [InlineData("SUB=BEGIN NAME=100")]
    [InlineData("SUB=BEGIN NAME=SLOTS")]
    [InlineData("PROGRAM=BEGIN NAME=\"SLOT ROW\" NUMBER=3 CHANNEL=1")]
    [InlineData("TOOL=\"DRILL_D8\"")]
    [InlineData("TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3")]
    [InlineData("RETRACT")]
    [InlineData("RETRACT=50")]
    [InlineData("TOLERANCE=OFF")]
    [InlineData("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH")]
    [InlineData("MIRROR=X,Y")]
    [InlineData("MIRROR=OFF")]
    [InlineData("SHIFT X=1")]
    [InlineData("SHIFT=RESET")]
    [InlineData("HOME X Z")]
    [InlineData("CYLINDER=30")]
    [InlineData("SPINDLE_SYNC=MAIN,SUB")]
    [InlineData("LINE X=10.50 Y=-7.025 F=0.05")]
    [InlineData("ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50")]
    [InlineData("CYCLE_CALL X={$Q1} Y=10")]
    [InlineData("JUMP=1 IF={$Q3 < $Q2}")]
    [InlineData("RAW:FANUC=\"G411 P1\"")]
    public void DesignRule5_ValueForm_WritesBackAsRead(string line)
    {
        Assert.Equal(line, Canonical(line));
    }

    // ncx format takes no machine file and does not run the virtual machine, so a bare TOOL stays bare (D91).
    [Fact]
    public void D91_BareTool_StaysBare()
    {
        Assert.Equal("TOOL", Canonical("TOOL"));
    }

    // A quote and a backslash inside a string are written with a backslash in front (language 3, string), whether the
    // string was read or built.
    [Fact]
    public void LanguageString_QuoteAndBackslash_AreWrittenWithTheirEscapes()
    {
        var block = new Block
        {
            Line = 1,
            Words =
            [
                new Word
                {
                    Key = "COMMENT",
                    Value = new StringValue("SIDE \"MILL\" D10 \\ OK"),
                    Definition = WordCatalog.Lookup("COMMENT"),
                },
            ],
        };
        string expected = """
            COMMENT="SIDE \"MILL\" D10 \\ OK"
            """;

        Assert.Equal(expected, NcxWriter.WriteBlock(block));
        Assert.Equal(expected, Canonical(expected));
    }

    // An expression is written as the canonical text of its tree between braces (language 4.12; phase 0, P0-05).
    [Fact]
    public void Language412_Expression_IsWrittenAsItsCanonicalText()
    {
        Assert.Equal("VAR:Q1={$Q1 + 20}", Canonical("VAR:Q1={$Q1+20}"));
    }

    // Keys, addresses and identifiers are uppercase; the words stand one space apart from the first column; a block
    // without a comment ends with its last word (language 3, Case and Word; 5 rule 7).
    [Theory]
    [InlineData("line x=7 comp=left", "LINE X=7 COMP=LEFT")]
    [InlineData("  LINE\tX=7    Y=2", "LINE X=7 Y=2")]
    [InlineData("LINE X=1   ", "LINE X=1")]
    public void Rule7_WordsOfTheBlock_StandOneSpaceApart(string line, string expected)
    {
        Assert.Equal(expected, Canonical(line));
    }

    // The semicolon of the comment stands in column 57 (language 5 rule 7, D92).
    [Fact]
    public void Rule7_ShortBlock_PutsTheSemicolonInColumn57()
    {
        string written = Canonical("RAPID Z=2 ; H10");

        Assert.Equal("RAPID Z=2                                               ; H10", written);
        Assert.Equal(CommentColumn, ColumnOf(';', written));
    }

    // Words that end in column 53 leave the three spaces up to column 57 (language 5 rule 7).
    [Fact]
    public void Rule7_WordsEndingInColumn53_PutTheSemicolonInColumn57()
    {
        string words = "COMMENT=\"" + new string('A', 43) + "\"";

        string written = Canonical(words + " ; c");

        Assert.Equal(53, words.Length);
        Assert.Equal(words + "   ; c", written);
        Assert.Equal(CommentColumn, ColumnOf(';', written));
    }

    // Words that reach column 54 or later are followed by three spaces and the comment (language 5 rule 7).
    [Fact]
    public void Rule7_WordsReachingColumn54_TakeThreeSpacesBeforeTheComment()
    {
        string words = "COMMENT=\"" + new string('A', 44) + "\"";

        Assert.Equal(54, words.Length);
        Assert.Equal(words + "   ; c", Canonical(words + " ; c"));
    }

    // The long header block of the examples takes three spaces before its comment (language 5 rule 7; the examples).
    [Fact]
    public void Rule7_LongBlock_TakesThreeSpacesBeforeTheComment()
    {
        string written = Canonical("UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN COMP=OFF CYCLE=OFF ; header state");

        Assert.Equal("FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF   ; header state", written);
    }

    // The comment keeps its text as read, from the semicolon to the end of the line (D92).
    [Theory]
    [InlineData(";no space")]
    [InlineData(";   spaced  ")]
    [InlineData("; a \"quote\"; and a second semicolon")]
    public void Rule7_Comment_KeepsItsTextAsRead(string comment)
    {
        Assert.Equal("LINE X=1" + new string(' ', 48) + comment, Canonical("LINE X=1 " + comment));
    }

    // Comment-only lines and blank lines are trivia, kept in place and written back as read, before FILE=BEGIN, between
    // the blocks and after FILE=END; two blank lines in a row stay two (language 3, Block; D92).
    [Fact]
    public void D92_TriviaAroundAndBetweenBlocks_IsWrittenBackAsRead()
    {
        string text = Lines(
            "; NCX test: comment-only lines before FILE=BEGIN",
            "   ; an indented comment line",
            "",
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"TRIVIA\"                             ; H0 BEGIN PGM TRIVIA MM",
            "; a comment line between blocks",
            "",
            "",
            "RAPID X=1 Y=2                                           ; after two blank lines",
            "PROGRAM=END",
            "FILE=END",
            "    ",
            "; after FILE=END");

        Assert.Equal(text, Format(text));
    }

    // A program keeps the line ending of its file (language 3, Encoding; phase 0, P0-06).
    [Fact]
    public void Language3_CrlfFile_IsWrittenWithCrlf()
    {
        string text = Lines("; CRLF", "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"CRLF\"", "PROGRAM=END", "FILE=END")
            .Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(text, Format(text));
    }

    // A file with mixed line endings keeps the line ending of its first line break on every line (language 3,
    // Encoding; PAR001).
    [Fact]
    public void Language3_MixedLineEndings_AreWrittenWithTheFirstLineEnding()
    {
        string text = "FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\nPROGRAM=END\r\nFILE=END\n";

        Assert.Equal("FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\r\nPROGRAM=END\r\nFILE=END\r\n", Format(text));
    }

    // The last line always ends with a line break (phase 0, P0-06).
    [Fact]
    public void Language3_LastLineWithoutLineBreak_GetsOne()
    {
        string text = "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"L\"\nPROGRAM=END\nFILE=END";

        Assert.Equal(text + "\n", Format(text));
    }

    // A program that was never a file, such as one a reader built, is written with LF (phase 0, P0-06).
    [Fact]
    public void Language3_ProgramThatWasNeverAFile_IsWrittenWithLf()
    {
        string text = Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"BUILT\"", "PROGRAM=END", "FILE=END");
        NcxProgram program = ParseFile(text.Replace("\n", "\r\n", StringComparison.Ordinal)) with { LineEnding = null };

        Assert.Equal(text, NcxWriter.Write(program));
    }

    // Programs and subprograms are written in file order, as they stand in the file (language 4.13; phase 0, P0-06).
    [Fact]
    public void Language413_ProgramsAndSubprograms_AreWrittenInFileOrder()
    {
        string text = Lines(
            "FILE=BEGIN NCX=1",
            "SUB=BEGIN NAME=100",
            "LINE IX=30 F=800",
            "SUB=END",
            "PROGRAM=BEGIN NAME=\"FIRST\" NUMBER=1",
            "CALL=100",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"SECOND\" NUMBER=2",
            "CALL=100 TIMES=2",
            "PROGRAM=END",
            "FILE=END");

        Assert.Equal(3, ParseFile(text).Sections.Count);
        Assert.Equal(text, Format(text));
    }

    // ncx format never writes a generated block; the writer writes it when it is asked to (language 4.15,
    // architecture 4.1).
    [Fact]
    public void Language415_GeneratedBlock_IsWrittenOnlyWhenAsked()
    {
        string text = Lines(
            "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"CLUTCH\"", "COOLANT:THROUGH=ON", "PROGRAM=END", "FILE=END");
        NcxProgram program = ParseFile(text);
        var blocks = new List<Block>(program.Blocks);
        blocks.Insert(2, new Block
        {
            Line = 3,
            Words = [new Word { Key = "SPINDLE", Value = new IdentValue("OFF"), Definition = WordCatalog.Lookup("SPINDLE") }],
            IsGenerated = true,
            OriginLine = 3,
        });
        NcxProgram expanded = program with { Blocks = blocks };

        Assert.Equal(text, NcxWriter.Write(expanded));
        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"CLUTCH\"",
                "SPINDLE=OFF",
                "COOLANT:THROUGH=ON",
                "PROGRAM=END",
                "FILE=END"),
            NcxWriter.Write(expanded, new WriterOptions { IncludeGenerated = true }));
    }

    // One line read as a block of a user file, without diagnostics, and written in canonical form.
    private static string Canonical(string line)
    {
        var diagnostics = new Diagnostics("test.ncx");
        Block? block = Parser.ParseBlock(line, 1, new ParserOptions(), diagnostics);

        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        Assert.NotNull(block);
        return NcxWriter.WriteBlock(block);
    }

    // A whole file read as a user file, without an ERROR, and written in canonical form.
    private static string Format(string text)
    {
        return NcxWriter.Write(ParseFile(text));
    }

    private static NcxProgram ParseFile(string text)
    {
        NcxProgram program = Parser.Parse(text, "test.ncx", new ParserOptions());

        Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
        return program;
    }

    // The lines of a file, each ended with LF.
    private static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }

    // The 1-based column of the first occurrence of a character.
    private static int ColumnOf(char character, string line)
    {
        return line.IndexOf(character, StringComparison.Ordinal) + 1;
    }
}
