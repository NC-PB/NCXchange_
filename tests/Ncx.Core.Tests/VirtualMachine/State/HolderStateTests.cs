using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The tool rows of virtual machine 2.3 for one holder, the tool as a number or a name (language 4.4), and the tool
/// change states of architecture 5.2 with the return of PRELOAD=0.
/// </summary>
public sealed class HolderStateTests
{
    // VM 2.3: holder[r].spindleTool starts 0, the empty spindle.
    [Fact]
    public void SpindleTool_AtStart_IsZeroTheEmptySpindle()
    {
        Assert.Equal(new ToolRef(0), Holder().SpindleTool);
    }

    // VM 2.3: holder[r].preloaded starts none.
    [Fact]
    public void Preloaded_AtStart_IsNone()
    {
        Assert.Null(Holder().Preloaded);
    }

    // VM 2.3: holder[r].offset.length, offset.radius and offset.combined start 0.
    [Fact]
    public void Offsets_AtStart_AreZero()
    {
        HolderState holder = Holder();

        Assert.Equal(0, holder.OffsetLen);
        Assert.Equal(0, holder.OffsetRad);
        Assert.Equal(0, holder.OffsetCombined);
    }

    // VM 2.3: TOOL and PRELOAD set them.
    [Fact]
    public void SpindleToolAndPreload_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H1"].SpindleTool = new ToolRef(4);
        state.Holders["H1"].Preloaded = new ToolRef("DRILL_D8");

        ChannelSnapshot snapshot = state.Snapshot();
        state.Holders["H1"].SpindleTool = new ToolRef("DRILL_D8");
        state.Holders["H1"].Preloaded = null;

        Assert.Equal(new ToolRef(4), snapshot.Holders["H1"].SpindleTool);
        Assert.Equal(new ToolRef("DRILL_D8"), snapshot.Holders["H1"].Preloaded);
        Assert.Null(state.Holders["H1"].Preloaded);
    }

    // VM 2.3: OFFSET:LEN, OFFSET:RAD and OFFSET set them on the holder called last.
    [Fact]
    public void Offsets_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H2"].OffsetLen = 1;
        state.Holders["H2"].OffsetRad = 2;
        state.Holders["H2"].OffsetCombined = 56;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Holders["H2"].OffsetLen = 0;
        state.Holders["H2"].OffsetRad = 0;
        state.Holders["H2"].OffsetCombined = 0;

        Assert.Equal(1, snapshot.Holders["H2"].OffsetLen);
        Assert.Equal(2, snapshot.Holders["H2"].OffsetRad);
        Assert.Equal(56, snapshot.Holders["H2"].OffsetCombined);
        Assert.Equal(0, state.Holders["H2"].OffsetCombined);
    }

    // Architecture 5.2: a holder is created Empty.
    [Fact]
    public void ToolChange_AtStart_IsEmpty()
    {
        Assert.Equal(ToolChangeState.Empty, Holder().ToolChange);
    }

    // Architecture 5.2: TOOL=n from Empty is Loaded.
    [Fact]
    public void ToolChange_ToolInTheSpindle_IsLoaded()
    {
        HolderState holder = Holder();
        holder.SpindleTool = new ToolRef(4);

        Assert.Equal(ToolChangeState.Loaded, holder.ToolChange);
    }

    // Architecture 5.2: PRELOAD=n from Empty and PRELOAD=m from Loaded are Pending.
    [Fact]
    public void ToolChange_ToolPreloaded_IsPendingWithOrWithoutAToolInTheSpindle()
    {
        HolderState empty = Holder();
        empty.Preloaded = new ToolRef(5);
        HolderState loaded = Holder();
        loaded.SpindleTool = new ToolRef(4);
        loaded.Preloaded = new ToolRef(5);

        Assert.Equal(ToolChangeState.Pending, empty.ToolChange);
        Assert.Equal(ToolChangeState.Pending, loaded.ToolChange);
    }

    // Architecture 5.2: PRELOAD=0 drops the preload and returns to the state the spindle was in, Empty here.
    [Fact]
    public void ToolChange_PreloadClearedWithAnEmptySpindle_ReturnsToEmpty()
    {
        HolderState holder = Holder();
        holder.Preloaded = new ToolRef(5);

        holder.Preloaded = null;

        Assert.Equal(ToolChangeState.Empty, holder.ToolChange);
    }

    // Architecture 5.2: PRELOAD=0 returns to Loaded with the tool that stayed in the spindle, since PRELOAD never
    // changes the spindle (VM 3.5).
    [Fact]
    public void ToolChange_PreloadClearedWithAToolInTheSpindle_ReturnsToLoadedWithThatTool()
    {
        HolderState holder = Holder();
        holder.SpindleTool = new ToolRef(4);
        holder.Preloaded = new ToolRef(5);

        holder.Preloaded = null;

        Assert.Equal(ToolChangeState.Loaded, holder.ToolChange);
        Assert.Equal(new ToolRef(4), holder.SpindleTool);
    }

    // Architecture 5.2: the snapshot keeps the tool change state it was taken in.
    [Fact]
    public void ToolChange_ChangedAfterASnapshot_SnapshotKeepsPending()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H1"].Preloaded = new ToolRef(5);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Holders["H1"].SpindleTool = new ToolRef(5);
        state.Holders["H1"].Preloaded = null;

        Assert.Equal(ToolChangeState.Pending, snapshot.Holders["H1"].ToolChange);
        Assert.Equal(ToolChangeState.Loaded, state.Holders["H1"].ToolChange);
    }

    // Language 4.4, VM 3.5: the preload is consumed when TOOL names the same tool; by number that is the same number.
    [Fact]
    public void ToolRef_PreloadedAndCalledByTheSameNumber_AreTheSameTool()
    {
        HolderState holder = Holder();
        holder.Preloaded = new ToolRef(5);

        Assert.Equal(new ToolRef(5), holder.Preloaded);
        Assert.NotEqual(new ToolRef(6), holder.Preloaded);
    }

    // Language 4.4: a tool by name is the same tool when the name is the same.
    [Fact]
    public void ToolRef_PreloadedAndCalledByTheSameName_AreTheSameTool()
    {
        HolderState holder = Holder();
        holder.Preloaded = new ToolRef("DRILL_D8");

        Assert.Equal(new ToolRef("DRILL_D8"), holder.Preloaded);
        Assert.NotEqual(new ToolRef("DRILL_D10"), holder.Preloaded);
    }

    // Language 4.4: TOOL=4 and TOOL="4" are two tools, a number and a name.
    [Fact]
    public void ToolRef_NumberAndNameWithTheSameDigits_AreDifferentTools()
    {
        HolderState holder = Holder();
        holder.SpindleTool = new ToolRef(4);
        holder.Preloaded = new ToolRef("4");

        Assert.NotEqual(holder.SpindleTool, holder.Preloaded);
    }

    // Language 4.4: TOOL=0 empties the spindle; a tool named "0" is a tool.
    [Fact]
    public void ToolChange_ToolNamedZero_IsLoaded()
    {
        HolderState holder = Holder();
        holder.SpindleTool = new ToolRef("0");

        Assert.Equal(ToolChangeState.Loaded, holder.ToolChange);
    }

    private static HolderState Holder()
    {
        return new ChannelState(StateMachines.MillTurn()).Holders["H1"];
    }
}
