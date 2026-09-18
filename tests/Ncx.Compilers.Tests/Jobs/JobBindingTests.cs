using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// The channel-bound words in a job (virtual machine 3.8 rule 2a; machine-config 5; D56; implementation 16, P6-02): a
/// word the machine accepts only from one channel moves to the program of that channel at the same mark, a word it
/// needs in every channel program is duplicated into each behind a generated SYNC, and what the job compiler cannot
/// place is an ERROR.
/// </summary>
public sealed class JobBindingTests
{
    // Implementation 16, P6-02 tests first: a two-channel job with a SPINDLE_SYNC word in channel 1 and
    // [spindle_sync] channel = 2 compiles the word into channel 2's program at the same mark.
    [Fact]
    public void BoundWord_SpindleSyncInChannel1BoundToChannel2_MovesToChannel2AtTheSameMark()
    {
        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110", "DWELL=1", "SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nG4 P1000\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM96\nM111\nM30\n%\n", texts[1]);
    }

    // D56: the words of a block bound to another channel move, the other words of the block stay.
    [Fact]
    public void BoundWord_InABlockWithOtherWords_MovesAloneAndTheBlockStays()
    {
        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB COOLANT=ON"),
            JobCompile.Program(1000, "SYNC=110")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM8\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM96\nM30\n%\n", texts[1]);
    }

    // D56 and the TODO(question) of JobCompiler.Destination: a word that no mark stands before stands after the
    // header of the program of its channel.
    [Fact]
    public void BoundWord_BeforeEveryMark_StandsAfterTheHeaderOfTheOtherProgram()
    {
        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110"), JobCompile.Program(1000, "FUNC:DOOR=OPEN", "SYNC=110")));

        Assert.Equal("%\nO1000 (T)\nM62\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[1]);
    }

    // Implementation 16, P6-02 tests first: a channels = "all" word appears in both programs behind a generated wait,
    // the first mark of mark_range that the job does not use (the TODO(question) of JobCompiler.GeneratedMark).
    [Fact]
    public void AllChannelsWord_InChannel1_StandsInBothProgramsBehindAGeneratedWait()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "DWELL=1", "SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nG4 P1000\nM100\nM96\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM96\nM111\nM30\n%\n", texts[1]);
    }

    // Virtual machine 3.8 rule 2a, D56: every function the compiler writes is bound, also a word an expansion rule
    // generates: FUNC:CLAMP=ON, which [tool_change] pre generates before the tool change of channel 1, stands in both
    // programs behind a generated wait.
    [Fact]
    public void AllChannelsWord_GeneratedByAnExpansionRule_StandsInBothProgramsBehindAGeneratedWait()
    {
        string machine = JobCompile.Machine(toolChange: "pre = [\"FUNC:CLAMP=ON\"]",
            functions: "CLAMP = { ON = \"M70\", OFF = \"M71\", channels = \"all\" }");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "TOOL=1 OFFSET=1", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM70\nT0101\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM70\nM111\nM30\n%\n", texts[1]);
    }

    // Virtual machine 3.8 rule 2a, D56: FUNC:DOOR=OPEN, which [tool_change] pre generates before the tool change of
    // channel 2 and which only channel 1 may command, moves to the program of channel 1 at the same mark.
    [Fact]
    public void BoundWord_GeneratedByAnExpansionRule_MovesToItsChannelAtTheSameMark()
    {
        string machine = JobCompile.Machine(toolChange: "pre = [\"FUNC:DOOR=OPEN\"]");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "TOOL=2 OFFSET=2", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM62\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nT0202\nM111\nM30\n%\n", texts[1]);
    }

    // Virtual machine 3.8 rule 2a, D56, architecture 9: FUNC:DOOR=OPEN, which the rewriter of a plugin writes before a
    // dwell of channel 2, moves to the program of channel 1; the rewriter learns channel 2, the channel of the job, for
    // a program without CHANNEL (machine-config 8, D106).
    [Fact]
    public void BoundWord_GeneratedByARewriter_MovesToItsChannelAndTheRewriterLearnsTheChannelOfTheJob()
    {
        var rewriter = new DwellRewriter();
        (JobManifest job, Dictionary<string, string> files) = JobCompile.JobOf(
            JobCompile.Program(1000, "SYNC=110"), JobCompile.Program(1000, "SYNC=110", "DWELL=1"));

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(), job, files,
            new CompileOptions { Rewriters = [rewriter] }));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM62\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nG4 P1000\nM30\n%\n", texts[1]);
        Assert.Equal(["2"], rewriter.Channels);
    }

    // D201 (open, its recommendation): the SYNC generated for a word in a SKIP=2 block is skipped on switch 2 with the
    // word in every program, so that no program skips the wait and runs the code (virtual machine 3.8 rule 2a).
    [Fact]
    public void AllChannelsWord_InABlockOfSkipSwitch2_ItsWaitIsSkippedOnSwitch2()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SKIP=2 SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\n/2M100\n/2M96\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\n/2M100\n/2M96\nM111\nM30\n%\n", texts[1]);
    }

    // D201 (open, its recommendation): a word moved from a SKIP=2 block is skipped on switch 2 in the program of its
    // channel.
    [Fact]
    public void BoundWord_InABlockOfSkipSwitch2_MovesSkippedOnSwitch2()
    {
        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110", "SKIP=2 SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\n/2M96\nM111\nM30\n%\n", texts[1]);
    }

    // The TODO(question) of JobCompiler.Duplicate: a word bound to every channel that every channel program already
    // writes behind the same mark, as a job read from the programs of such a machine does, stays as it stands.
    [Fact]
    public void AllChannelsWord_AlreadyInEveryProgramBehindTheSameMark_StaysAsItStands()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB"),
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM96\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM96\nM30\n%\n", texts[1]);
    }

    // Virtual machine 3.7 (marks are matched in execution order) and 3.8 rule 2a: the words of two blocks of one
    // channel between the same marks stand in both programs in one order, each behind a generated wait, so that the
    // paths command each code at the same wait.
    [Fact]
    public void AllChannelsWords_TwoBlocksOfOneChannelBetweenTheSameMarks_StandInBothProgramsInOneOrder()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"",
            functions: "CLAMP = { ON = \"M70\", OFF = \"M71\", channels = \"all\" }");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB", "DWELL=1", "FUNC:CLAMP=ON", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM96\nG4 P1000\nM100\nM70\nM111\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM96\nM100\nM70\nM111\nM30\n%\n", texts[1]);
    }

    // Virtual machine 3.7 and 3.8 rule 2a: the words of two channels behind different marks stand in both programs in
    // the order of the marks, each behind a generated wait.
    [Fact]
    public void AllChannelsWords_OfTwoChannelsBehindDifferentMarks_StandInBothProgramsInTheOrderOfTheMarks()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"",
            functions: "CLAMP = { ON = \"M70\", OFF = \"M71\", channels = \"all\" }");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111", "FUNC:CLAMP=ON")));

        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM96\nM111\nM100\nM70\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG18 G99 G40 G80\nM110\nM100\nM96\nM111\nM100\nM70\nM30\n%\n", texts[1]);
    }

    // The TODO(question) of JobCompiler.Duplicate: the words of two channels duplicated between the same marks would
    // stand in the programs in two orders, and the generated waits pair in execution order (virtual machine 3.7), so
    // that the paths would command different codes at one wait: CMP705.
    [Fact]
    public void AllChannelsWords_OfTwoChannelsBetweenTheSameMarks_IsCmp705()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"",
            functions: "CLAMP = { ON = \"M70\", OFF = \"M71\", channels = \"all\" }");

        CompileResult result = JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            JobCompile.Program(1000, "SYNC=110", "FUNC:CLAMP=ON", "SYNC=111"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.AllChannelsWordsOfTwoChannels, error.Code);
        Assert.Equal("C2.ncx", error.File);
        Assert.Equal(5, error.Line);
    }

    // The TODO(question) of JobCompiler.Duplicate: before every mark, the start of the programs is the same place for
    // the words of every channel: CMP705.
    [Fact]
    public void AllChannelsWords_OfTwoChannelsBeforeEveryMark_IsCmp705()
    {
        string machine = JobCompile.Machine(spindleSync: "channels = \"all\"",
            functions: "CLAMP = { ON = \"M70\", OFF = \"M71\", channels = \"all\" }");

        CompileResult result = JobCompile.Run(machine,
            JobCompile.Program(1000, "SPINDLE_SYNC=MAIN,SUB", "SYNC=110"),
            JobCompile.Program(1000, "FUNC:CLAMP=ON", "SYNC=110"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.AllChannelsWordsOfTwoChannels, error.Code);
        Assert.Equal("C2.ncx", error.File);
        Assert.Equal(4, error.Line);
    }

    // D98 and the risks of phase 6: a diagnostic on a moved word names the block it stands after in the program of its
    // channel as its line and the block it came from as its OriginLine. Here the sub spindle of channel 2 is a C axis
    // there, and SPINDLE_SYNC needs it as a spindle (virtual machine 3.8 rule 5).
    [Fact]
    public void BoundWord_DiagnosticOnTheMovedWord_NamesTheMarkAndTheLineItCameFrom()
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110", "DWELL=1", "SPINDLE_SYNC=MAIN,SUB"),
            JobCompile.Program(1000, "SPINDLE_MODE:SUB=AXIS", "SYNC=110"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal("C2.ncx", error.File);
        Assert.Equal(5, error.Line);
        Assert.Equal(6, error.OriginLine);
    }

    // D56, machine-config 8: a word bound to a channel the job runs no program on has nowhere to go: CMP702.
    [Fact]
    public void BoundWord_ChannelTheJobDoesNotRun_IsCmp702()
    {
        var job = new JobManifest
        {
            Machine = "test.toml",
            Channels =
            [
                new ChannelProgram { Id = 2, File = "C2.ncx" },
                new ChannelProgram { Id = 3, File = "C3.ncx" },
            ],
        };

        CompileResult result = JobCompile.Run(JobCompile.Machine(channels: "[1, 2, 3]"), job,
            new Dictionary<string, string>
            {
                ["C2.ncx"] = JobCompile.Program(1000, "SYNC=110", "FUNC:DOOR=OPEN"),
                ["C3.ncx"] = JobCompile.Program(1000, "SYNC=110"),
            });

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.BoundChannelNotInJob, error.Code);
        Assert.Equal(5, error.Line);
    }

    // The TODO(question) of JobCompiler.BindWords: a bound word in a subprogram runs at every CALL and has no one place
    // in the program of another channel: CMP703.
    [Fact]
    public void BoundWord_InASubprogram_IsCmp703()
    {
        string first = string.Join('\n',
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SYNC=110",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "SPINDLE_SYNC=MAIN,SUB",
            "SUB=END",
            "FILE=END") + "\n";

        CompileResult result = JobCompile.Run(JobCompile.Machine(), first, JobCompile.Program(1000, "SYNC=110"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.BoundWordInSubprogram, error.Code);
        Assert.Equal(8, error.Line);
    }

    // Language 4.8, WITH, and the TODO(question) of JobCompiler.Destination: channel 2 takes no part in the mark before
    // the word bound to it, so its program has no same mark: CMP704.
    [Fact]
    public void BoundWord_OwningChannelTakesNoPartInTheMarkBefore_IsCmp704()
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(channels: "[1, 2, 3]"),
            JobCompile.Program(1000, "SYNC=110 WITH=1,3", "SPINDLE_SYNC=MAIN,SUB"), JobCompile.Program(1000),
            JobCompile.Program(1000, "SYNC=110 WITH=1,3"));

        Assert.Equal(DiagnosticCodes.NoSameMarkInChannel, JobCompile.ErrorOf(result).Code);
    }

    // Machine-config 5, D56: without a mark_range the job compiler has no mark for the SYNC a word bound to every
    // channel needs: CMP711.
    [Fact]
    public void AllChannelsWord_MachineWithoutMarkRange_IsCmp711()
    {
        string machine = JobCompile.Machine("[sync]\nwait = \"M{mark}\"", spindleSync: "channels = \"all\"");

        CompileResult result = JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110", "SPINDLE_SYNC=MAIN,SUB"), JobCompile.Program(1000, "SYNC=110"));

        Assert.Equal(DiagnosticCodes.NoMarkForGeneratedSync, JobCompile.ErrorOf(result).Code);
    }
}
