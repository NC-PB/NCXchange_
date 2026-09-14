using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The rounds of the job scheduler (virtual machine 3.7, architecture 5.4, D39): every channel that is neither finished
/// nor waiting executes one block per round; SYNC=m marks the channel waiting at m, and when all participants wait at m
/// all are released; marks are matched in execution order, not by uniqueness.
/// </summary>
public sealed class JobRoundTests
{
    // P6-01 done when: a two-channel job with three marks in different orders passes. Channel 1 reaches each mark first
    // and waits; channel 2 reaches the first in round 4, the second waits for channel 1 until round 7, the third for
    // channel 2 until round 11; both end in round 12, whatever the numbers of the marks.
    [Theory]
    [InlineData(100, 200, 300)]
    [InlineData(300, 100, 200)]
    [InlineData(200, 300, 100)]
    public void Rounds_TwoChannelsThreeMarksInEveryOrder_ReleaseInTheRoundTheLastChannelArrives(
        int first, int second, int third)
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File($"SYNC={first}", "UNITS=MM", "UNITS=MM", $"SYNC={second}", $"SYNC={third}", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", $"SYNC={first}", $"SYNC={second}", "UNITS=MM", "UNITS=MM",
                "UNITS=MM", $"SYNC={third}", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal([$"SYNC={first} round 4", $"SYNC={second} round 7", $"SYNC={third} round 11"], job.Releases());
        Assert.Equal(12, job.Result.Rounds);
    }

    // VM 3.7: each channel raises SYNC_WAIT in the round it reaches the mark and SYNC_RELEASE in the round the last
    // participant arrives, with the participants, all channels of the job without WITH.
    [Fact]
    public void Rounds_ChannelWaitsForTheOther_TimelineHasTheWaitsAndTheReleases()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=150", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "SYNC=150", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(
        [
            "1 SYNC_WAIT(3): mark 150, channels 1,2, round 2",
            "2 SYNC_WAIT(5): mark 150, channels 1,2, round 4",
            "1 SYNC_RELEASE(3): mark 150, channels 1,2, round 4",
            "2 SYNC_RELEASE(5): mark 150, channels 1,2, round 4",
        ], job.Timeline());
    }

    // VM 3.7, language 4.8: a program may wait at the same mark many times (the DMG templates do); the first wait of
    // one channel pairs with the first of the other, the second with the second.
    [Fact]
    public void Rounds_SameMarkTwice_PairsInExecutionOrder()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100", "UNITS=MM", "SYNC=100", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "SYNC=100", "SYNC=100", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(["SYNC=100 round 4", "SYNC=100 round 6"], job.Releases());
    }

    // VM 3.7: a channel waits at a SYNC inside a called subprogram and goes on inside it once released, in both modes:
    // the CALL block, SUB=BEGIN and the SYNC are its rounds 2 to 4.
    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Static)]
    public void Rounds_SyncInsideACalledSubprogram_WaitsInsideTheCall(ExecutionMode mode)
    {
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files =
            [
                JobHarness.File("CALL=10", "UNITS=MM", "PROGRAM=END", "SUB=BEGIN NAME=10", "SYNC=100", "SUB=END"),
                JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "UNITS=MM", "SYNC=100", "PROGRAM=END"),
            ],
            Mode = mode,
        });

        job.AssertFinished();
        Assert.Equal(["SYNC=100 round 6"], job.Releases());
        Assert.Contains("1 SYNC_WAIT(7): mark 100, channels 1,2, round 4", job.Timeline());
    }

    // VM 3.7: the channels run in rounds without a mark as well; a channel that has finished executes nothing more and
    // the job ends when the last one finishes.
    [Fact]
    public void Rounds_ChannelsWithoutMarks_JobEndsWithTheLongestChannel()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Empty(job.Result.Timeline);
        Assert.Equal(5, job.Result.Rounds);
        Assert.Empty(job.Codes());
    }

    // VM 2.9: an ERROR stops the run, the whole job, once every channel has executed its block of the round.
    [Fact]
    public void Rounds_ErrorInOneChannel_StopsTheJobAfterTheRound()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("LINE X=1 F=100", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        Assert.True(job.Result.Stopped);
        Assert.Equal(Severity.Error, job.Single(DiagnosticCodes.MotionBeforeUnits).Severity);
        Assert.Equal(2, job.Result.Rounds);
        Assert.True(job.IndexOf(2, "STATE_CHANGE", line: 3) >= 0);
        Assert.False(job.Result.Channels[1].Finished);
    }
}
