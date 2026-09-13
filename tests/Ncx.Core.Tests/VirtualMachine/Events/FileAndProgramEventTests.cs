using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// The events of the structure of a file (virtual machine 7): FILE_BEGIN and FILE_END, PROGRAM_BEGIN and PROGRAM_END
/// with the run statistics, SUB_BEGIN and SUB_END with the caller, SECTION.
/// </summary>
public sealed class FileAndProgramEventTests
{
    // VM 7, rows FILE_BEGIN, PROGRAM_BEGIN, STATE_CHANGE, PROGRAM_END, FILE_END: a run opens with FILE_BEGIN and closes
    // with FILE_END, each with the file name and the programs and subprograms found.
    [Fact]
    public void FileBeginAndEnd_Run_FrameEveryOtherEvent()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("UNITS=MM", "PROGRAM=END"));

        Assert.Equal(
            [
                "FILE_BEGIN(1): file test.ncx, programs \"TEST\", subprograms none",
                "PROGRAM_BEGIN(2): name \"TEST\", number 0",
                "STATE_CHANGE(3): UNITS ? -> MM",
                "PROGRAM_END(4): name \"TEST\", number 0, blocks 3, distance 0",
                "FILE_END(5): file test.ncx, programs \"TEST\", subprograms none",
            ],
            listener.Lines);
    }

    // VM 7, row FILE_BEGIN: the payload names the programs and the subprograms the pre-pass found.
    [Fact]
    public void FileBegin_FileWithASubprogram_NamesTheProgramsAndTheSubprograms()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        FileEvent begin = Assert.IsType<FileEvent>(listener.Events[0]);
        Assert.Equal("FILE_BEGIN", begin.Kind);
        Assert.Equal(EventPhase.Begin, begin.Phase);
        Assert.Equal("test.ncx", begin.FileName);
        Assert.Equal("TEST", Assert.Single(begin.Programs).Name);
        Assert.Equal("10", Assert.Single(begin.Subs).Name);
        Assert.Equal(EventPhase.End, Assert.IsType<FileEvent>(listener.Events[^1]).Phase);
    }

    // VM 7, row PROGRAM_BEGIN: name and number of the program.
    [Fact]
    public void ProgramBegin_ProgramBeginBlock_CarriesNameAndNumber()
    {
        FakeListener listener = EventRuns.Execute("PROGRAM=BEGIN NAME=\"SHAFT\" NUMBER=7");

        ProgramEvent begin = Assert.Single(listener.Of<ProgramEvent>());
        Assert.Equal(EventPhase.Begin, begin.Phase);
        Assert.Equal("SHAFT", begin.Name);
        Assert.Equal(7, begin.Number);
        Assert.Equal("PROGRAM_BEGIN(1): name \"SHAFT\", number 7", begin.ToString());
    }

    // VM 7, row PROGRAM_END: the run statistics, the blocks executed from PROGRAM=BEGIN to PROGRAM=END and the distance
    // summed from the MOTION lengths that are known.
    [Fact]
    public void ProgramEnd_AfterMotions_CarriesTheRunStatistics()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=30 F=100", "LINE Y=40", "PROGRAM=END"));

        ProgramEvent end = listener.Of<ProgramEvent>()[^1];
        Assert.Equal(EventPhase.End, end.Phase);
        Assert.Equal(6, end.Blocks);
        Assert.Equal(70m, end.Distance);
        Assert.Equal("PROGRAM_END(7): name \"TEST\", number 0, blocks 6, distance 70", end.ToString());
    }

    // VM 7, rows SUB_BEGIN and SUB_END: raised at SUB=BEGIN and SUB=END with the name and the caller, the program or
    // the subprogram whose CALL entered it (D99: STATIC mode follows every CALL).
    [Fact]
    public void SubBeginAndEnd_Calls_NameTheSubprogramAndItsCaller()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "CALL=20", "SUB=END", "SUB=BEGIN NAME=20", "SUB=END"));

        Assert.Equal(
            [
                "SUB_BEGIN(5): name 10, caller \"TEST\"",
                "SUB_BEGIN(8): name 20, caller 10",
                "SUB_END(9): name 20, caller 10",
                "SUB_END(7): name 10, caller \"TEST\"",
            ],
            SubLines(listener));
        SubEvent inner = listener.Of<SubEvent>()[1];
        Assert.Equal("20", inner.Name);
        Assert.Equal(SectionKind.Sub, inner.Caller?.Kind);
    }

    // VM 7, row SUB_BEGIN; VM 3.9, D99: a subprogram that no program of the file calls is walked once from the default
    // entry state and has no caller.
    [Fact]
    public void SubBegin_SubprogramNothingCalls_HasNoCaller()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        Assert.Equal(["SUB_BEGIN(4): name 10, caller none", "SUB_END(5): name 10, caller none"], SubLines(listener));
        Assert.Null(listener.Of<SubEvent>()[0].Caller);
    }

    // VM 7, row SECTION: the text of the SECTION word.
    [Fact]
    public void Section_SectionWord_CarriesTheText()
    {
        FakeListener listener = EventRuns.Execute("SECTION=\"ROUGHING\"");

        SectionEvent section = Assert.Single(listener.Of<SectionEvent>());
        Assert.Equal("ROUGHING", section.Text);
        Assert.Equal("SECTION(1): \"ROUGHING\"", section.ToString());
    }

    private static List<string> SubLines(FakeListener listener)
    {
        var lines = new List<string>();
        foreach (SubEvent sub in listener.Of<SubEvent>())
        {
            lines.Add(sub.ToString());
        }

        return lines;
    }
}
