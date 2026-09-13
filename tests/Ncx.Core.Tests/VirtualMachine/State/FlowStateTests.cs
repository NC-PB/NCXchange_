using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The flow rows of virtual machine 2.1 and 2.7 and the restore stack of 3.10: their start values, and a snapshot
/// that keeps the values it was taken with while the live state changes.
/// </summary>
public sealed class FlowStateTests
{
    // VM 2.1: file.programs and file.subs are empty until the pre-pass over the file.
    [Fact]
    public void ProgramsAndSubs_AtStart_AreEmpty()
    {
        FlowState flow = Flow();

        Assert.Empty(flow.Programs);
        Assert.Empty(flow.Subs);
    }

    // VM 2.1: the pre-pass collects the PROGRAM sections and the SUB sections by NAME.
    [Fact]
    public void ProgramsAndSubs_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        Section shaft = Program("SHAFT", 1, 20);
        Section sub = Sub("100", 21, 30);
        state.Flow.Programs.Add(shaft);
        state.Flow.Subs["100"] = sub;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Flow.Programs.Add(Program("SHAFT_OP2", 31, 40));
        state.Flow.Subs.Clear();

        Assert.Equal(shaft, Assert.Single(snapshot.Flow.Programs));
        Assert.Equal(sub, snapshot.Flow.Subs["100"]);
        Assert.Equal(2, state.Flow.Programs.Count);
    }

    // VM 2.7: the labels come from the pre-pass over each program and subprogram.
    [Fact]
    public void Labels_AtStart_AreEmpty()
    {
        Assert.Empty(Flow().Labels);
    }

    // VM 2.7: labels per section, each with the block it stands on.
    [Fact]
    public void Labels_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        Section pattern = Program("PATTERN", 1, 22);
        state.Flow.Labels[pattern] = new Dictionary<string, int> { ["1"] = 14 };

        ChannelSnapshot snapshot = state.Snapshot();
        state.Flow.Labels[pattern]["300"] = 18;
        state.Flow.Labels[Sub("100", 23, 30)] = new Dictionary<string, int> { ["2"] = 25 };

        Assert.Equal(14, Assert.Single(snapshot.Flow.Labels[pattern]).Value);
        Assert.Single(snapshot.Flow.Labels);
        Assert.Equal(2, state.Flow.Labels[pattern].Count);
    }

    // VM 2.7: pc, callStack and repeatStack start at the first block, empty and empty.
    [Fact]
    public void PcCallsAndRepeats_AtStart_AreTheFirstBlockAndEmpty()
    {
        FlowState flow = Flow();

        Assert.Equal(0, flow.Pc);
        Assert.Empty(flow.Calls);
        Assert.Empty(flow.Repeats);
    }

    // VM 2.7, 3.6: the flow words set them.
    [Fact]
    public void PcCallsAndRepeats_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Flow.Pc = 12;
        state.Flow.Calls.Push(new CallFrame(ReturnPc: 13, Target: "100"));
        state.Flow.Repeats.Push(new RepeatFrame(RepeatPc: 16, Label: "1", Remaining: 2));

        ChannelSnapshot snapshot = state.Snapshot();
        state.Flow.Pc = 25;
        state.Flow.Calls.Pop();
        state.Flow.Repeats.Push(new RepeatFrame(RepeatPc: 20, Label: "2", Remaining: 4));

        Assert.Equal(12, snapshot.Flow.Pc);
        Assert.Equal(new CallFrame(13, "100"), Assert.Single(snapshot.Flow.Calls));
        Assert.Equal(new RepeatFrame(16, "1", 2), Assert.Single(snapshot.Flow.Repeats));
        Assert.Empty(state.Flow.Calls);
    }

    // VM 2.7: the snapshot lists the calls that have not returned, the innermost first.
    [Fact]
    public void Calls_TwoDeep_SnapshotListsTheInnermostFirst()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Flow.Calls.Push(new CallFrame(13, "100"));
        state.Flow.Calls.Push(new CallFrame(27, "200"));

        ChannelSnapshot snapshot = state.Snapshot();

        Assert.Equal("200", snapshot.Flow.Calls[0].Target);
        Assert.Equal("100", snapshot.Flow.Calls[1].Target);
    }

    // VM 2.7: blocksExecuted starts 0.
    [Fact]
    public void BlocksExecuted_AtStart_IsZero()
    {
        Assert.Equal(0, Flow().BlocksExecuted);
    }

    // VM 2.7: every executed block counts (INTERPRETED).
    [Fact]
    public void BlocksExecuted_ChangedAfterASnapshot_SnapshotKeepsTheCount()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Flow.BlocksExecuted = 41;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Flow.BlocksExecuted = 42;

        Assert.Equal(41, snapshot.Flow.BlocksExecuted);
        Assert.Equal(42, state.Flow.BlocksExecuted);
    }

    // VM 3.10, D95: the restore stack starts empty.
    [Fact]
    public void RestoreStack_AtStart_IsEmpty()
    {
        Assert.Empty(Flow().RestoreStack);
    }

    // VM 3.10, D95: @SAVE pushes the words that set the saved value again; @RESTORE pops them.
    [Fact]
    public void RestoreStack_ChangedAfterASnapshot_SnapshotKeepsTheSavedWords()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        Word spindle = new() { Key = "SPINDLE", Addr = "MAIN", Value = new IdentValue("CW") };
        Word rpm = new() { Key = "RPM", Addr = "MAIN", Value = new IntegerValue(1500, "1500") };
        var saved = new RestoreEntry(new StateKeyValue("SPINDLE", "MAIN"), [spindle, rpm]);
        state.Flow.RestoreStack.Add(saved);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Flow.RestoreStack.Clear();

        Assert.Equal(saved, Assert.Single(snapshot.Flow.RestoreStack));
        Assert.Equal("SPINDLE:MAIN", snapshot.Flow.RestoreStack[0].Key.ToCanonical());
        Assert.Empty(state.Flow.RestoreStack);
    }

    private static FlowState Flow()
    {
        return new ChannelState(StateMachines.MillTurn()).Flow;
    }

    private static Section Program(string name, int firstBlock, int lastBlock)
    {
        return new Section { Kind = SectionKind.Program, Name = name, FirstBlock = firstBlock, LastBlock = lastBlock };
    }

    private static Section Sub(string name, int firstBlock, int lastBlock)
    {
        return new Section { Kind = SectionKind.Sub, Name = name, FirstBlock = firstBlock, LastBlock = lastBlock };
    }
}
