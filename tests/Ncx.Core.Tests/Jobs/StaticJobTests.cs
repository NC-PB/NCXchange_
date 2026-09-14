using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The job in STATIC mode, the mode of ncx check (virtual machine 1, 3.7, 3.9; D99): the channel programs walk in
/// rounds with their calls, and afterwards the rest of each file is walked once, as ncx check walks a file;
/// INTERPRETED mode executes the programs the job runs and nothing else (3.9).
/// </summary>
public sealed class StaticJobTests
{
    // A file with a program no channel runs: its LINE with the spindle off.
    private const string FileWithAnotherProgram = """
        FILE=BEGIN NCX=1
        PROGRAM=BEGIN NAME="ONE"
        SYNC=100
        PROGRAM=END
        PROGRAM=BEGIN NAME="OTHER"
        UNITS=MM
        LINE X=1 F=100
        PROGRAM=END
        FILE=END
        """;

    // VM 1: STATIC mode walks every program of the file; a job check walks the program no channel runs after the job.
    [Fact]
    public void StaticJob_ProgramNoChannelRuns_IsWalkedAfterTheJob()
    {
        JobHarness job = Run(ExecutionMode.Static, FileWithAnotherProgram);

        job.AssertFinished();
        Diagnostic spindleOff = job.Single(DiagnosticCodes.SpindleOffBeforeLine);
        Assert.Equal(7, spindleOff.Line);
    }

    // VM 3.9: in INTERPRETED mode the other programs of the file are not executed unless the job runs them.
    [Fact]
    public void InterpretedJob_ProgramNoChannelRuns_IsNotExecuted()
    {
        JobHarness job = Run(ExecutionMode.Interpreted, FileWithAnotherProgram);

        job.AssertFinished();
        Assert.Empty(job.Codes());
    }

    // D99: a subprogram that no walk of the job called is walked once from the default entry state after the job.
    [Fact]
    public void StaticJob_SubprogramNothingCalls_IsWalkedOnce()
    {
        string file = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="ONE"
            SYNC=100
            PROGRAM=END
            SUB=BEGIN NAME=9
            SPINDLE:NOPE=CW
            SUB=END
            FILE=END
            """;

        JobHarness job = Run(ExecutionMode.Static, file);

        job.AssertFinished();
        Assert.Equal(6, job.Single(DiagnosticCodes.NotCheckedNoMachineFile).Line);
    }

    // The job of the tests: channel 1 runs the program ONE of the file, channel 2 a program that waits at mark 100.
    private static JobHarness Run(ExecutionMode mode, string file)
    {
        return JobHarness.Run(new JobSetup
        {
            Files = [file, JobHarness.File("SYNC=100", "PROGRAM=END")],
            Channels =
            [
                new ChannelProgram { Id = 1, File = "ch1.ncx", Program = "ONE" },
                new ChannelProgram { Id = 2, File = "ch2.ncx" },
            ],
            Mode = mode,
        });
    }
}
