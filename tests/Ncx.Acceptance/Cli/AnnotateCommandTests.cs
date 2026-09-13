using Ncx.Acceptance.Examples;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx annotate &lt;file&gt; and the shared options: a copy of the program through the canonical writer with the
/// previous values appended to each block's comment, which the parser keeps as the block's comment and the virtual
/// machine ignores (virtual machine 6, D92; architecture 10; P1-07).
/// </summary>
public sealed class AnnotateCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // P1-07: annotate runs on every example, exit 0 without a machine file (D97, D103).
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Annotate_Example_ExitsZero(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("annotate", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.NotEqual("", _cli.Output);
    }

    // P1-07, done when: the annotate output parses, and with the comments stripped it formats back to the original
    // (VM 6: the values are comments, which the parser keeps as the block's comment, D92).
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Annotate_Example_ParsesAndFormatsBackToTheOriginalWithoutComments(string example)
    {
        string file = _cli.CopyExample(example);

        _cli.Run("annotate", file);

        NcxProgram annotated = Parser.Parse(_cli.Output, example, new ParserOptions());
        NcxProgram original = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());
        Assert.False(annotated.Diagnostics.HasErrors, annotated.Diagnostics.ToText());
        Assert.Equal(NcxWriter.Write(WithoutComments(original)), NcxWriter.Write(WithoutComments(annotated)));
    }

    // VM 6, D92: annotate is a copy of the program; every line keeps its place and begins as the original line, the
    // values after it (every example is canonical, so its lines are what the canonical writer writes).
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Annotate_Example_KeepsEveryLineOfTheOriginalAtItsPlace(string example)
    {
        string file = _cli.CopyExample(example);

        _cli.Run("annotate", file);

        string[] originalLines = Fixture.ReadText(example).Split('\n');
        string[] annotatedLines = _cli.Output.Split('\n');
        Assert.Equal(originalLines.Length, annotatedLines.Length);
        for (int index = 0; index < originalLines.Length; index++)
        {
            Assert.StartsWith(originalLines[index], annotatedLines[index], StringComparison.Ordinal);
        }
    }

    // Phases table row 1, exit checklist: the annotate output of 2.5D_FRAESEN as an expected file.
    [Fact]
    public void Annotate_Fraesen25D_EqualsTheExpectedFile()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        _cli.Run("annotate", file);

        CliHarness.AssertExpectedFile("2.5D_FRAESEN.annotate.txt", _cli.Output);
    }

    // VM 6: LINE X=55.44 becomes LINE X=55.44 followed by the comment ; X 33.22 -> 55.44 in the canonical column.
    [Fact]
    public void Annotate_BlockWithoutComment_GetsThePreviousValueInTheCommentColumn()
    {
        string file = _cli.WriteFile("line.ncx",
            CliHarness.OneProgram("UNITS=MM F=100", "RAPID X=33.22", "LINE X=55.44"));

        int exitCode = _cli.Run("annotate", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Contains("LINE X=55.44                                            ; X 33.22 -> 55.44\n", _cli.Output,
            StringComparison.Ordinal);
    }

    // VM 6: an existing comment is kept and the values follow it after one space.
    [Fact]
    public void Annotate_BlockWithComment_KeepsItAndAppendsTheValuesAfterOneSpace()
    {
        string file = _cli.WriteFile("line.ncx",
            CliHarness.OneProgram("UNITS=MM F=100", "RAPID X=33.22", "LINE X=55.44 ; note"));

        _cli.Run("annotate", file);

        Assert.Contains("LINE X=55.44                                            ; note X 33.22 -> 55.44\n",
            _cli.Output, StringComparison.Ordinal);
    }

    // VM 6: several variables of one block follow each other in the order trace writes them; an unknown value is
    // written ? as in the payload of the events.
    [Fact]
    public void Annotate_BlockThatChangesSeveralVariables_WritesThemInTheOrderOfTrace()
    {
        string file = _cli.WriteFile("rapid.ncx", CliHarness.OneProgram("UNITS=MM", "RAPID X=10 Y=20"));

        _cli.Run("annotate", file);

        Assert.Contains("RAPID X=10 Y=20                                         ; X ? -> 10, Y ? -> 20\n",
            _cli.Output, StringComparison.Ordinal);
    }

    // VM 6, D99: annotate writes the values of the first walk of a block of a subprogram that STATIC mode walks at
    // several calls; the first of the four passes of CALL=100 TIMES=4 moves Y from 0 to 15.
    [Fact]
    public void Annotate_SubprogramCalledFourTimes_WritesTheValuesOfTheFirstWalk()
    {
        string file = _cli.CopyExample("INCREMENTAL_SUB.ncx");

        _cli.Run("annotate", file);

        string[] lines = _cli.Output.Split('\n');
        Assert.EndsWith("then G90 before M99 X 30 -> 0, Y 0 -> 15", lines[32], StringComparison.Ordinal);
    }

    // VM 6, D99: the values of the first walk, also when the first walk changed nothing: the LINE X=0 of the
    // subprogram finds X at 0 at its first call and at 5 at its second, and annotate writes no value for it.
    [Fact]
    public void Annotate_BlockThatChangesNothingAtItsFirstWalk_GetsNoValues()
    {
        string file = _cli.WriteFile("twice.ncx", CliHarness.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM F=100",
            "RAPID X=0",
            "CALL=S",
            "RAPID X=5",
            "CALL=S",
            "PROGRAM=END",
            "SUB=BEGIN NAME=S",
            "LINE X=0",
            "SUB=END",
            "FILE=END"));

        int exitCode = _cli.Run("annotate", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Contains("\nRAPID X=5                                               ; X 0 -> 5\n", _cli.Output,
            StringComparison.Ordinal);
        Assert.Contains("\nLINE X=0\n", _cli.Output, StringComparison.Ordinal);
    }

    // VM 3.10: generated blocks appear in annotate with their origin, as comment lines where the virtual machine
    // executes them, so that the copy stays a program of the file that the parser reads.
    [Fact]
    public void Annotate_GeneratedBlocks_StandAsCommentLinesWithTheirOrigin()
    {
        string program = CliHarness.OneProgram("UNITS=MM", "SPINDLE=CW RPM=1500", "COOLANT:THROUGH=ON");
        string file = _cli.WriteFile("clutch.ncx", program);
        string machine = _cli.WriteFile("clutch.toml", TestMachines.ClutchMill);

        int exitCode = _cli.Run("annotate", file, "--machine", machine);

        Assert.True(exitCode == 0, _cli.Error);
        string[] lines = _cli.Output.Split('\n');
        Assert.Equal("; generated by [coolant] THROUGH (restore): @SAVE=SPINDLE:TOOL", lines[4]);
        Assert.Equal("; generated by [coolant] THROUGH (requires): SPINDLE:TOOL=OFF ; SPINDLE:S1 CW -> OFF", lines[5]);
        Assert.StartsWith("COOLANT:THROUGH=ON ", lines[6], StringComparison.Ordinal);
        Assert.EndsWith("; COOLANT:THROUGH OFF -> ON", lines[6], StringComparison.Ordinal);
        Assert.Equal("; generated by [coolant] THROUGH (restore): @RESTORE=SPINDLE:TOOL ; SPINDLE:S1 OFF -> CW",
            lines[7]);
        NcxProgram annotated = Parser.Parse(_cli.Output, "clutch.ncx", new ParserOptions());
        NcxProgram original = Parser.Parse(program, "clutch.ncx", new ParserOptions());
        Assert.False(annotated.Diagnostics.HasErrors, annotated.Diagnostics.ToText());
        Assert.Equal(NcxWriter.Write(WithoutComments(original)), NcxWriter.Write(WithoutComments(annotated)));
    }

    // The annotate output is a program the virtual machine runs as the original: the values are comments it ignores
    // (VM 6), so ncx check reports the same.
    [Fact]
    public void Annotate_OutputOfPatternLoop_ChecksAsTheOriginal()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");
        _cli.Run("annotate", file);
        string annotatedFile = _cli.WriteFile("PATTERN_LOOP.annotated.ncx", _cli.Output);

        int exitCode = _cli.Run("check", annotatedFile);

        Assert.Equal(0, exitCode);
        CliHarness.AssertExpectedFile("PATTERN_LOOP.check.txt", _cli.Error.Replace(annotatedFile, "PATTERN_LOOP.ncx"));
    }

    // A byte order mark is kept where it was, as ncx format keeps it (wave-1 question #80).
    [Fact]
    public void Annotate_FileWithByteOrderMark_KeepsIt()
    {
        string file = _cli.PathOf("bom.ncx");
        File.WriteAllBytes(file, [0xEF, 0xBB, 0xBF, .. Fixture.ReadBytes("POLAR_FACE.ncx")]);

        int exitCode = _cli.Run("annotate", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.StartsWith("﻿; NCX example", _cli.Output, StringComparison.Ordinal);
    }

    // An ERROR of the parser means no run: nothing is written, exit 1 (D97).
    [Fact]
    public void Annotate_UnknownKey_WritesNothingAndExitsOne()
    {
        string file = _cli.WriteFile("unknown.ncx", CliHarness.OneProgram("LINE X=1 SPEED=5"));

        int exitCode = _cli.Run("annotate", file);

        Assert.Equal(1, exitCode);
        Assert.Equal("", _cli.Output);
    }

    // The help of annotate names the shared options (architecture 10, F28).
    [Fact]
    public void Annotate_Help_NamesTheSharedOptions()
    {
        int exitCode = _cli.Run("annotate", "--help");

        Assert.Equal(0, exitCode);
        foreach (string option in new[] { "--machine", "--strict", "--skip-blocks", "--expand-cycles" })
        {
            Assert.Contains(option, _cli.Output, StringComparison.Ordinal);
        }
    }

    // The program without its comments: no block comment and no comment-only line; the blank lines stay.
    private static NcxProgram WithoutComments(NcxProgram program)
    {
        var blocks = new List<Block>();
        foreach (Block block in program.Blocks)
        {
            blocks.Add(block with { Comment = null });
        }

        var trivia = new List<Trivia>();
        foreach (Trivia line in program.Trivia)
        {
            if (!line.Text.TrimStart().StartsWith(';'))
            {
                trivia.Add(line);
            }
        }

        return program with { Blocks = blocks, Trivia = trivia };
    }
}
