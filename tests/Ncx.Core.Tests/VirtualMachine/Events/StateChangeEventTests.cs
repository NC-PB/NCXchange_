using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// STATE_CHANGE on any modal change, one event per changed state variable with its old and new value (virtual machine
/// 4, 6, 7; architecture 5.1).
/// </summary>
public sealed class StateChangeEventTests
{
    // VM 7, row STATE_CHANGE: variable, old, new; one event per variable, named by the key that sets it and the
    // resource it stands on (VM 3.10).
    [Fact]
    public void StateChange_SpindleWords_OneEventPerChangedVariable()
    {
        FakeListener listener = EventRuns.Execute("SPINDLE=CW RPM=1500");

        Assert.Equal(["STATE_CHANGE(1): SPINDLE:S2 OFF -> CW", "STATE_CHANGE(1): RPM:S2 0 -> 1500"],
            listener.LinesOf("STATE_CHANGE"));
        StateChangeEvent rpm = listener.Of<StateChangeEvent>()[1];
        Assert.Equal("RPM:S2", rpm.Variable);
        Assert.Equal("0", rpm.OldValue);
        Assert.Equal("1500", rpm.NewValue);
    }

    // VM 7: a word that sets the value the variable already has changes nothing and raises nothing.
    [Fact]
    public void StateChange_UnchangedValue_RaisesNothing()
    {
        FakeListener listener = EventRuns.Execute("COMP=OFF FEED_MODE=PER_MIN");

        Assert.Empty(listener.Events);
    }

    // VM 2.2, 6: the position of an axis is a state variable, named by the axis; an unknown value is empty.
    [Fact]
    public void StateChange_Motion_ReportsThePositionOfEveryMovedAxis()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID X=5 Z=2");

        Assert.Equal(["STATE_CHANGE(2): X ? -> 5", "STATE_CHANGE(2): Z ? -> 2"], listener.LinesOf("STATE_CHANGE")[1..]);
        Assert.Equal("", listener.Of<StateChangeEvent>()[1].OldValue);
    }

    // VM 1, 6: a state variable set from an expression becomes UNKNOWN in STATIC mode, an empty value.
    [Fact]
    public void StateChange_ValueFromAnExpression_BecomesEmpty()
    {
        FakeListener listener = EventRuns.Execute("F=100", "F={$Q1 * 2}");

        Assert.Equal(["STATE_CHANGE(1): F ? -> 100", "STATE_CHANGE(2): F 100 -> ?"], listener.LinesOf("STATE_CHANGE"));
    }

    // VM 4: FRAME, the verb and SKIP end with their block and are no modal change; the machine frame shows in the
    // position.
    [Fact]
    public void StateChange_BlockItems_AreNotReported()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID Z=2", "SKIP RAPID Z=0 FRAME=MACHINE");

        Assert.Equal(["STATE_CHANGE(3): Z 2 -> 0 (MACHINE)"], StateChangesOf(listener, line: 3));
    }

    // VM 4: PROGRAM=END resets feed, spindle and coolant; each reset is a STATE_CHANGE before PROGRAM_END.
    [Fact]
    public void StateChange_ProgramEnd_ReportsTheResetsBeforeProgramEnd()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "F=100", "COOLANT=ON", "SPINDLE=CW", "PROGRAM=END");

        Assert.Equal(
            [
                "STATE_CHANGE(5): F 100 -> ?",
                "STATE_CHANGE(5): SPINDLE:S2 CW -> OFF",
                "STATE_CHANGE(5): COOLANT:STANDARD ON -> OFF",
            ],
            StateChangesOf(listener, line: 5));
        Assert.Equal("PROGRAM_END", listener.Events[^1].Kind);
    }

    // VM 2.1, D31: the transform chain is one variable, its entries in program order.
    [Fact]
    public void StateChange_Shift_ReportsTheChain()
    {
        FakeListener listener = EventRuns.Execute("SHIFT X=60 Y=40 Z=-5", "ROTATE=30");

        Assert.Equal(
            ["STATE_CHANGE(1): CHAIN ? -> SHIFT X=60 Y=40 Z=-5", "STATE_CHANGE(2): CHAIN SHIFT X=60 Y=40 Z=-5 -> "
                + "SHIFT X=60 Y=40 Z=-5 | ROTATE=30"],
            listener.LinesOf("STATE_CHANGE"));
    }

    // VM 1, 3.9, 7: a CALL of an external program leaves the position unknown; the change belongs to the CALL block, so
    // its After carries the unknown position and its STATE_CHANGE events report every axis that was known.
    [Fact]
    public void StateChange_ExternalCall_ReportsThePositionBecomingUnknownAtTheCallBlock()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "UNITS=MM", "RAPID X=10 Y=10 Z=5", "CALL=\"O9010\"", "LINE X=20 F=100", "PROGRAM=END"));

        Assert.Equal(["STATE_CHANGE(5): X 10 -> ?", "STATE_CHANGE(5): Y 10 -> ?", "STATE_CHANGE(5): Z 5 -> ?"],
            StateChangesOf(listener, line: 5));
        FlowEvent call = Assert.Single(listener.Of<FlowEvent>());
        Assert.True(call.Before.Motion.Position["X"].Known);
        Assert.False(call.After.Motion.Position["X"].Known);
    }

    private static List<string> StateChangesOf(FakeListener listener, int line)
    {
        var lines = new List<string>();
        foreach (StateChangeEvent change in listener.Of<StateChangeEvent>())
        {
            if (change.Block.Line == line)
            {
                lines.Add(change.ToString());
            }
        }

        return lines;
    }
}
