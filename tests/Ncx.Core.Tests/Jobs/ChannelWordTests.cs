using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The channel words of language 4.8 in a job (virtual machine 3.7): WITH, WAIT_CHANNEL, START_CHANNEL, SYNC in a job
/// of one channel, and the channels a job and its programs name (language 4.14, machine-config 8).
/// </summary>
public sealed class ChannelWordTests
{
    // Language 4.8, VM 3.7: WITH=1,2 on a three-channel job releases channels 1 and 2 without channel 3, which runs on.
    [Fact]
    public void With_OneAndTwoInAThreeChannelJob_ReleasesWithoutChannelThree()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100 WITH=1,2", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SYNC=100 WITH=1,2", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(["SYNC=100 round 3"], job.Releases());
        Assert.Contains("1 SYNC_RELEASE(3): mark 100, channels 1,2, round 3", job.Timeline());
        Assert.DoesNotContain(job.Result.Timeline, sync => sync.Channel == 3);
    }

    // Language 4.8: the channel of the SYNC takes part although WITH leaves it out; WITH=2 on channel 1 waits for
    // channel 2 and channel 1.
    [Fact]
    public void With_LeavesOutTheChannelOfTheSync_ItTakesPartAnyway()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100 WITH=2", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SYNC=100 WITH=1", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Contains("1 SYNC_RELEASE(3): mark 100, channels 1,2, round 3", job.Timeline());
    }

    // Language 4.8 (the TODO(question) of JobRunner.ChannelNamed): a WITH that names a channel the job does not run is
    // an ERROR, and the job stops.
    [Fact]
    public void With_ChannelTheJobDoesNotRun_IsError()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100 WITH=1,3", "PROGRAM=END"),
            JobHarness.File("SYNC=100", "PROGRAM=END"));

        Diagnostic missing = job.Single(DiagnosticCodes.ChannelNotInJob);
        Assert.Equal("ch1.ncx", missing.File);
        Assert.Equal(
            "WITH=1,3 names a channel the job does not run; the channels of the job are 1, 2 (language 4.8, "
            + "machine-config 8).", missing.Message);
        Assert.True(job.Result.Stopped);
    }

    // VM 3.7: WAIT_CHANNEL=2 waits until channel 2 has finished; channel 1 executes its next block in the round after
    // channel 2 executed PROGRAM=END (round 4).
    [Fact]
    public void WaitChannel_OtherChannel_WaitsUntilItHasFinished()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("WAIT_CHANNEL=2", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(6, job.Result.Rounds);
        Assert.True(job.IndexOf(2, "PROGRAM_END") < job.IndexOf(1, "STATE_CHANGE", line: 4));
    }

    // VM 3.7: WAIT_CHANNEL of a channel that has finished already does not hold the channel beyond its round.
    [Fact]
    public void WaitChannel_ChannelFinishedAlready_GoesOnInTheNextRound()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "UNITS=MM", "WAIT_CHANNEL=2", "PROGRAM=END"),
            JobHarness.File("PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(5, job.Result.Rounds);
    }

    // Language 4.8: START_CHANNEL=2 starts the program of channel 2, which waits for it and executes its first block
    // in the round after the word (the TODO(question) of JobRunner.StartChannels).
    [Fact]
    public void StartChannel_ChannelNamedByStartChannel_StartsInTheRoundAfterTheWord()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "START_CHANNEL=2", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(6, job.Result.Rounds);
        Assert.True(job.IndexOf(1, "STATE_CHANGE", line: 3) < job.IndexOf(2, "PROGRAM_BEGIN"));
        Assert.True(job.IndexOf(2, "PROGRAM_BEGIN") < job.IndexOf(1, "PROGRAM_END"));
    }

    // Language 4.8: the NAME of START_CHANNEL selects the program the started channel runs.
    [Fact]
    public void StartChannel_WithName_RunsTheNamedProgram()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("START_CHANNEL=2 NAME=\"SECOND\"", "PROGRAM=END"),
            """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            UNITS=MM
            PROGRAM=END
            FILE=END
            """);

        job.AssertFinished();
        ProgramEvent begin = Assert.IsType<ProgramEvent>(job.Events[job.IndexOf(2, "PROGRAM_BEGIN")]);
        Assert.Equal("SECOND", begin.Name);
        Assert.Equal("SECOND", job.Result.Channels[1].ProgramName);
    }

    // Language 4.8: a START_CHANNEL of a channel that runs or has run already starts nothing and warns.
    [Fact]
    public void StartChannel_ChannelRunningAlready_WarnsAndStartsNothing()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("START_CHANNEL=2", "START_CHANNEL=2", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Diagnostic warning = job.Single(DiagnosticCodes.ChannelAlreadyStarted);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(4, warning.Line);
    }

    // VM 3.7, 5: SYNC in a single-channel job is a WARNING, and the channel does not wait.
    [Fact]
    public void Sync_SingleChannelJob_WarnsAndDoesNotWait()
    {
        JobHarness job = JobHarness.Run(JobHarness.File("SYNC=100", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal([DiagnosticCodes.SyncInSingleChannelJob], job.Codes());
        Assert.Empty(job.Result.Timeline);
        Assert.Equal(4, job.Result.Rounds);
    }

    // VM 3.7: in a job of two channels the SYNC of a file whose programs all run on one channel is no single-channel
    // SYNC; the pre-pass does not warn.
    [Fact]
    public void Sync_TwoChannelJob_NoSingleChannelWarning()
    {
        JobHarness job = JobHarness.Run(JobHarness.File("SYNC=100", "PROGRAM=END"), JobHarness.File("SYNC=100",
            "PROGRAM=END"));

        job.AssertFinished();
        Assert.Empty(job.Codes());
    }

    // Language 4.14, machine-config 8 (the TODO(question) of JobRunner.CheckHeaderChannel): a program whose header
    // names another channel than the job runs it on warns, and runs on the job's channel.
    [Fact]
    public void Header_ChannelOtherThanTheJobs_WarnsAndRunsOnTheJobsChannel()
    {
        JobHarness job = JobHarness.Run(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"P\" CHANNEL=2\nSYNC=100\nPROGRAM=END\nFILE=END\n",
            JobHarness.File("SYNC=100", "PROGRAM=END"));

        job.AssertFinished();
        Diagnostic warning = job.Single(DiagnosticCodes.ChannelOtherThanHeader);
        Assert.Equal("ch1.ncx", warning.File);
        Assert.Equal(2, warning.Line);
        Assert.Contains("1 SYNC_WAIT(3): mark 100, channels 1,2, round 2", job.Timeline());
    }

    // Machine-config 8, VM 3.7: a channel runs one program; a manifest that names a channel twice is an ERROR on the
    // manifest, and the job does not run.
    [Fact]
    public void Manifest_ChannelTwice_IsErrorAndNothingRuns()
    {
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files = [JobHarness.File("PROGRAM=END"), JobHarness.File("PROGRAM=END")],
            Channels =
            [
                new ChannelProgram { Id = 1, File = "ch1.ncx" },
                new ChannelProgram { Id = 1, File = "ch2.ncx" },
            ],
        });

        Diagnostic twice = job.Single(DiagnosticCodes.ChannelTwiceInJob);
        Assert.Equal("test.ncxjob.toml", twice.File);
        Assert.True(job.Result.Stopped);
        Assert.Equal(0, job.Result.Rounds);
    }

    // Language 4.14, machine-config 8: two channels may run two programs of one file; the file is checked once.
    [Fact]
    public void Manifest_TwoProgramsOfOneFile_RunOnTheirChannelsAndTheFileIsCheckedOnce()
    {
        string file = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="SHAFT"
            RAW:FANUC="G4"
            SYNC=100
            PROGRAM=END
            PROGRAM=BEGIN NAME="SHAFT_CH2"
            SYNC=100
            PROGRAM=END
            FILE=END
            """;
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files = [file],
            Channels =
            [
                new ChannelProgram { Id = 1, File = "ch1.ncx" },
                new ChannelProgram { Id = 2, File = "ch1.ncx", Program = "SHAFT_CH2" },
            ],
        });

        job.AssertFinished();
        Assert.Equal(["SYNC=100 round 3"], job.Releases());
        Assert.Equal([DiagnosticCodes.RawPresent], job.Codes());
    }
}
