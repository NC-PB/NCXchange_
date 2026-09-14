using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// A program is the blocks of a file, the sections they form and the trivia around them (language 4.13, D92).
/// </summary>
public sealed class NcxProgramTests
{
    // A file holds one or more programs and any number of subprograms in any order (language 4.13).
    [Fact]
    public void Sections_TwoProgramsAndOneSub_AreListedByKindInFileOrder()
    {
        var shaft = new Section
        {
            Kind = SectionKind.Program,
            Name = "SHAFT",
            Number = 1,
            FirstBlock = 1,
            LastBlock = 3,
        };
        var sub = new Section { Kind = SectionKind.Sub, Name = "100", FirstBlock = 4, LastBlock = 6 };
        var secondOperation = new Section
        {
            Kind = SectionKind.Program,
            Name = "SHAFT_OP2",
            Number = 2,
            FirstBlock = 7,
            LastBlock = 9,
        };
        var program = new NcxProgram
        {
            FileName = "SHAFT.ncx",
            Blocks = [],
            Sections = [shaft, sub, secondOperation],
            Diagnostics = new Diagnostics("SHAFT.ncx"),
        };

        Assert.Equal([shaft, sub, secondOperation], program.Sections);
        Assert.Equal([shaft, secondOperation], program.Programs);
        Assert.Equal([sub], program.Subs);
    }

    // CHANNEL is a header word with the default 1 (language 4.1).
    [Fact]
    public void Section_ProgramWithoutChannel_RunsOnChannelOne()
    {
        var program = new Section { Kind = SectionKind.Program, FirstBlock = 1, LastBlock = 40 };

        Assert.Equal(1, program.Channel);
        Assert.Null(program.Name);
        Assert.Null(program.Number);
    }

    [Fact]
    public void Section_ProgramOnChannelTwo_ReadsItsHeaderBack()
    {
        var program = new Section
        {
            Kind = SectionKind.Program,
            Name = "SUB_SPINDLE",
            Number = 2000,
            Channel = 2,
            FirstBlock = 60,
            LastBlock = 90,
        };

        Assert.Equal(SectionKind.Program, program.Kind);
        Assert.Equal("SUB_SPINDLE", program.Name);
        Assert.Equal(2000, program.Number);
        Assert.Equal(2, program.Channel);
        Assert.Equal(60, program.FirstBlock);
        Assert.Equal(90, program.LastBlock);
    }

    // Comment-only and blank lines are not blocks but trivia, kept with their line numbers (D92).
    [Fact]
    public void Trivia_CommentAndBlankLines_AreReadBackWithTheirLineNumbers()
    {
        var fileBegin = new Block
        {
            Line = 3,
            Words = [new Word { Key = "FILE", Value = new IdentValue("BEGIN") }, NcxVersionOne()],
        };
        var fileEnd = new Block { Line = 5, Words = [new Word { Key = "FILE", Value = new IdentValue("END") }] };
        var program = new NcxProgram
        {
            FileName = "PATTERN_LOOP.ncx",
            LineEnding = LineEnding.Lf,
            Blocks = [fileBegin, fileEnd],
            FileBegin = fileBegin,
            FileEnd = fileEnd,
            Trivia =
            [
                new Trivia(1, "; NCX example"),
                new Trivia(2, ""),
                new Trivia(4, "   "),
                new Trivia(6, "; Notes"),
            ],
            Diagnostics = new Diagnostics("PATTERN_LOOP.ncx"),
        };

        Assert.Equal(
            [new Trivia(1, "; NCX example"), new Trivia(2, ""), new Trivia(4, "   "), new Trivia(6, "; Notes")],
            program.Trivia);
        Assert.Same(fileBegin, program.FileBegin);
        Assert.Same(fileEnd, program.FileEnd);
        Assert.Equal(LineEnding.Lf, program.LineEnding);
    }

    // A program that a reader built was never a file and has no line ending of its own (phase 0, P0-06).
    [Fact]
    public void LineEnding_ProgramBuiltWithoutAFile_IsNone()
    {
        var program = new NcxProgram
        {
            FileName = "BOHREN.ncx",
            Blocks = [],
            Diagnostics = new Diagnostics("BOHREN.ncx"),
        };

        Assert.Null(program.LineEnding);
        Assert.Empty(program.Sections);
        Assert.Empty(program.Trivia);
        Assert.Null(program.FileBegin);
        Assert.Null(program.FileEnd);
    }

    private static Word NcxVersionOne()
    {
        return new Word { Key = "NCX", Value = new IntegerValue(1, "1") };
    }
}
