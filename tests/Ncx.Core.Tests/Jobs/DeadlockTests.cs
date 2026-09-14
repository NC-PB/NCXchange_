using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The deadlock at SYNC (virtual machine 3.7, 5; architecture 5.4): all channels waiting with no releasable mark is an
/// ERROR naming the marks per channel, and the job stops.
/// </summary>
public sealed class DeadlockTests
{
    // P6-01 done when: a job whose channels wait at different marks reports the deadlock with both marks, in ncx check
    // (STATIC) and in ncx analyze (INTERPRETED). Two channels waiting at different marks with the same participants is
    // the deadlock case of VM 3.7.
    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Static)]
    public void Deadlock_ChannelsWaitingAtDifferentMarks_IsErrorNamingBothMarks(ExecutionMode mode)
    {
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files =
            [
                JobHarness.File("SYNC=100", "SYNC=200", "PROGRAM=END"),
                JobHarness.File("SYNC=200", "SYNC=100", "PROGRAM=END"),
            ],
            Mode = mode,
        });

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Equal(Severity.Error, deadlock.Severity);
        Assert.Equal("ch1.ncx", deadlock.File);
        Assert.Equal(3, deadlock.Line);
        Assert.Equal(
            "Deadlock: channel 1 waits at SYNC=100 with 1,2 (ch1.ncx line 3); channel 2 waits at SYNC=200 with 1,2 "
            + "(ch2.ncx line 3); no channel can go on and no mark can be released (virtual machine 3.7).",
            deadlock.Message);
        Assert.True(job.Result.Stopped);
        Assert.Equal(2, job.Result.Rounds);
    }

    // VM 3.7: a channel that waits at a mark of a channel that has finished can never go on; the deadlock names the
    // finished channel (the TODO(question) of JobRunner.ReportDeadlock).
    [Fact]
    public void Deadlock_MarkOfAChannelThatHasFinished_IsErrorNamingIt()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "SYNC=100", "PROGRAM=END"),
            JobHarness.File("PROGRAM=END"));

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Contains("channel 1 waits at SYNC=100 with 1,2 (ch1.ncx line 4); channel 2 has finished",
            deadlock.Message, StringComparison.Ordinal);
    }

    // VM 3.7: two channels that each wait for the end of the other.
    [Fact]
    public void Deadlock_EachChannelWaitsForTheOther_IsError()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("WAIT_CHANNEL=2", "PROGRAM=END"),
            JobHarness.File("WAIT_CHANNEL=1", "PROGRAM=END"));

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Contains("channel 1 waits for channel 2 to finish (ch1.ncx line 3); channel 2 waits for channel 1 to "
            + "finish (ch2.ncx line 3)", deadlock.Message, StringComparison.Ordinal);
    }

    // VM 3.7, language 4.8: the channels waiting at one mark with different participants are not released (the
    // TODO(question) of JobRunner.MarkReleasable); the deadlock names both sets.
    [Fact]
    public void Deadlock_OneMarkWithDifferentParticipants_IsErrorNamingThem()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100 WITH=1,2", "PROGRAM=END"),
            JobHarness.File("SYNC=100 WITH=1,2,3", "PROGRAM=END"),
            JobHarness.File("SYNC=100 WITH=2,3", "PROGRAM=END"));

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Contains("channel 1 waits at SYNC=100 with 1,2", deadlock.Message, StringComparison.Ordinal);
        Assert.Contains("channel 2 waits at SYNC=100 with 1,2,3", deadlock.Message, StringComparison.Ordinal);
    }
}
