using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// Every event carries Before and After, the snapshots of the channel around its block; the Before of a block is the
/// After of the events before it, and a listener receives read-only snapshots (virtual machine 7, architecture 5.3,
/// D61, D106).
/// </summary>
public sealed class BeforeAndAfterTests
{
    // P1-05: every event carries a Before that equals the previous After, over the whole of 2.5D_FRAESEN.
    [Fact]
    public void BeforeAndAfter_Fraesen25D_EveryBeforeIsThePreviousAfter()
    {
        FakeListener listener = EventRuns.Run(Fixture.ReadText("2.5D_FRAESEN.ncx"));

        AssertChained(listener.Events);
    }

    // P1-05, D99: the chain holds through the calls of subprograms, which STATIC mode follows.
    [Fact]
    public void BeforeAndAfter_CallsOfSubprograms_EveryBeforeIsThePreviousAfter()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "UNITS=MM", "RAPID X=0 Y=0 Z=0", "CALL=10 TIMES=2", "RAPID Z=50", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "LINE IX=10 F=100", "SUB=END"));

        AssertChained(listener.Events);
        Assert.Equal(20m, TheX(listener.Events[^1].After));
    }

    // P1-05, VM 1: the chain holds across a CALL of an external program, whose unknown position is the After of the
    // CALL block and the Before of the block after it.
    [Fact]
    public void BeforeAndAfter_ExternalCall_EveryBeforeIsThePreviousAfter()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "UNITS=MM", "RAPID X=10 Y=10 Z=5", "CALL=\"O9010\"", "LINE X=20 F=100", "PROGRAM=END"));

        AssertChained(listener.Events);
        VmEvent afterTheCall = listener.Events.Find(vmEvent => vmEvent.Block.Line == 6)!;
        Assert.False(afterTheCall.Before.Motion.Position["Y"].Known);
    }

    // VM 7: the events of one block share its Before and After.
    [Fact]
    public void BeforeAndAfter_EventsOfOneBlock_ShareTheSnapshotsOfTheBlock()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "TOOL=1 RPM=1000 SPINDLE=CW");

        List<VmEvent> ofTheToolBlock = listener.Events.FindAll(vmEvent => vmEvent.Block.Line == 2);
        Assert.True(ofTheToolBlock.Count > 1);
        foreach (VmEvent vmEvent in ofTheToolBlock)
        {
            Assert.Same(ofTheToolBlock[0].Before, vmEvent.Before);
            Assert.Same(ofTheToolBlock[0].After, vmEvent.After);
        }
    }

    // Architecture 5.3, D61: Before and After are immutable snapshots; a later block does not change what an event
    // carries.
    [Fact]
    public void BeforeAndAfter_LaterBlocks_LeaveTheSnapshotsOfAnEventAsTheyWere()
    {
        FakeListener listener = EventRuns.Execute("TOOL=1", "TOOL=2", "TOOL=3");

        VmEvent first = listener.Events[0];
        Assert.Equal(new ToolRef(0), first.Before.Holders["H1"].SpindleTool);
        Assert.Equal(new ToolRef(1), first.After.Holders["H1"].SpindleTool);
    }

    // Architecture 5.3, code-guidelines 5 (observer): every listener receives every event, in the order raised.
    [Fact]
    public void Subscribe_TwoListeners_ReceiveEveryEventInTheSameOrder()
    {
        var harness = new VmHarness(VmMachines.Default());
        var first = new FakeListener();
        var second = new FakeListener();
        harness.Vm.Subscribe(first);
        harness.Vm.Subscribe(second);

        harness.Execute("UNITS=MM", "TOOL=1", "RAPID X=1");

        Assert.NotEmpty(first.Lines);
        Assert.Equal(first.Lines, second.Lines);
    }

    // VM 2.8, 7: the channel of every event is channel.id of the program that runs.
    [Fact]
    public void Channel_ProgramOfChannel2_IsTheChannelOfEveryEvent()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="PATH2" CHANNEL=2
            UNITS=MM
            PROGRAM=END
            FILE=END
            """;

        FakeListener listener = EventRuns.Run(text);

        Assert.All(listener.Events, vmEvent => Assert.Equal(2, vmEvent.Channel));
    }

    // Consecutive events either belong to one block and share its snapshots, or the later one starts from the After of
    // the earlier one.
    private static void AssertChained(List<VmEvent> events)
    {
        for (int index = 1; index < events.Count; index++)
        {
            VmEvent previous = events[index - 1];
            VmEvent current = events[index];
            bool sameBlock = ReferenceEquals(previous.Before, current.Before)
                && ReferenceEquals(previous.After, current.After);
            Assert.True(sameBlock || ReferenceEquals(previous.After, current.Before),
                $"{current} does not start from the After of {previous}.");
        }
    }

    private static decimal TheX(ChannelSnapshot snapshot)
    {
        return snapshot.Motion.Position["X"].Value;
    }
}
