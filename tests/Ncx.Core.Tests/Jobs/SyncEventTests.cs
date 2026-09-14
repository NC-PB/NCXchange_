using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// SYNC_WAIT and SYNC_RELEASE (virtual machine 3.7, 7): the job scheduler raises them to the listeners of the channel,
/// with channel.waitingAt changing between their Before and After (2.8), in the chain of Before and After of the
/// channel (architecture 5.3).
/// </summary>
public sealed class SyncEventTests
{
    // VM 2.8, 7: SYNC sets waitingAt, the release clears it; the events follow the events of the SYNC block.
    [Fact]
    public void SyncEvents_WaitAndRelease_CarryWaitingAtInBeforeAndAfter()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SYNC=100", "PROGRAM=END"));

        job.AssertFinished();
        List<VmEvent> events = job.EventsOf(1);
        SyncEvent wait = Assert.IsType<SyncEvent>(events.Find(vmEvent => vmEvent.Kind == "SYNC_WAIT"));
        SyncEvent release = Assert.IsType<SyncEvent>(events.Find(vmEvent => vmEvent.Kind == "SYNC_RELEASE"));
        Assert.Null(wait.Before.WaitingAt);
        Assert.Equal(100, wait.After.WaitingAt);
        Assert.Equal(100, release.Before.WaitingAt);
        Assert.Null(release.After.WaitingAt);
        Assert.Equal(3, wait.Block.Line);
        Assert.Same(wait.Block, release.Block);
    }

    // Architecture 5.3, VM 7: every Before of a channel is the After of its events before it, through the waits.
    [Fact]
    public void SyncEvents_ChainOfTheChannel_EveryBeforeIsThePreviousAfter()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "SYNC=100", "RAPID X=1", "PROGRAM=END"),
            JobHarness.File("SYNC=100", "PROGRAM=END"));

        job.AssertFinished();
        List<VmEvent> events = job.EventsOf(1);
        for (int index = 1; index < events.Count; index++)
        {
            if (!ReferenceEquals(events[index].Before, events[index - 1].Before))
            {
                Assert.Same(events[index - 1].After, events[index].Before);
            }
        }
    }
}
