using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The channel rows of virtual machine 2.3, 2.5 and 2.8 and the resources from TOML: their start values, and a
/// snapshot that keeps the values it was taken with while the live state changes.
/// </summary>
public sealed class ChannelStateTests
{
    // VM 2.8: channel.id is 1 without CHANNEL.
    [Fact]
    public void ChannelId_AtStart_IsOne()
    {
        Assert.Equal(1, new ChannelState(StateMachines.MillTurn()).ChannelId);
    }

    // VM 2.8: channel.id is the CHANNEL of the program the channel runs.
    [Fact]
    public void ChannelId_OfAChannelProgram_IsItsChannel()
    {
        Assert.Equal(2, new ChannelState(StateMachines.MillTurn(), channelId: 2).ChannelId);
    }

    // VM 2.8: the header sets it.
    [Fact]
    public void ChannelId_ChangedAfterASnapshot_SnapshotKeepsIt()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.ChannelId = 2;

        ChannelSnapshot snapshot = state.Snapshot();
        state.ChannelId = 3;

        Assert.Equal(2, snapshot.ChannelId);
        Assert.Equal(3, state.ChannelId);
    }

    // VM 2.8: channel.waitingAt and finished start none and false.
    [Fact]
    public void WaitingAtAndFinished_AtStart_AreNoneAndFalse()
    {
        var state = new ChannelState(StateMachines.MillTurn());

        Assert.Null(state.WaitingAt);
        Assert.False(state.Finished);
    }

    // VM 2.8: SYNC and PROGRAM=END set them.
    [Fact]
    public void WaitingAtAndFinished_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.WaitingAt = 100;
        state.Finished = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.WaitingAt = null;
        state.Finished = false;

        Assert.Equal(100, snapshot.WaitingAt);
        Assert.True(snapshot.Finished);
        Assert.Null(state.WaitingAt);
    }

    // VM 2.8: the resources come from TOML; every work and tool spindle has the rows of 2.4, every tool holder the
    // rows of 2.3, by resource id.
    [Fact]
    public void Resources_FromTheMachine_EverySpindleAndEveryHolderHasItsState()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        string[] spindles = ["S1", "S2", "S3"];
        string[] holders = ["H1", "H2"];

        Assert.Equal(spindles, state.Spindles.Keys);
        Assert.Equal(holders, state.Holders.Keys);
    }

    // D103: the default machine has the work spindle MAIN, the tool spindle TOOL and one holder.
    [Fact]
    public void Resources_DefaultMachineOfD103_TwoSpindlesAndOneHolder()
    {
        var state = new ChannelState(StateMachines.Default());
        string[] spindles = ["S1", "S2"];

        Assert.Equal(spindles, state.Spindles.Keys);
        Assert.Equal("H1", Assert.Single(state.Holders).Key);
    }

    // VM 2.3: lastHolder starts at default_holder from TOML.
    [Fact]
    public void LastHolder_AtStart_IsTheDefaultHolder()
    {
        Assert.Equal("H1", new ChannelState(StateMachines.MillTurn()).LastHolder);
    }

    // VM 3.8 rule 2: a machine with one holder needs no default_holder; that holder is the default.
    [Fact]
    public void LastHolder_MachineWithOneHolderAndNoDefault_IsThatHolder()
    {
        MachineConfig machine = StateMachines.Default() with
        {
            Machine = new MachineIdentity { Name = "Mill without defaults" },
        };

        Assert.Equal("H1", new ChannelState(machine).LastHolder);
    }

    // VM 2.3: TOOL:r sets it.
    [Fact]
    public void LastHolder_ChangedAfterASnapshot_SnapshotKeepsIt()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.LastHolder = "H2";

        ChannelSnapshot snapshot = state.Snapshot();
        state.LastHolder = "H1";

        Assert.Equal("H2", snapshot.LastHolder);
        Assert.Equal("H1", state.LastHolder);
    }

    // VM 2.5: every coolant channel starts OFF.
    [Fact]
    public void Coolant_AtStart_EveryChannelIsOff()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        string[] channels = ["STANDARD", "THROUGH"];

        Assert.Equal(channels, state.Coolant.Keys);
        Assert.All(state.Coolant.Values, Assert.False);
    }

    // VM 2.5: the channel STANDARD is the default channel a bare COOLANT addresses, also on a machine whose
    // [coolant] table does not list it.
    [Fact]
    public void Coolant_MachineWithoutStandard_HasTheDefaultChannelOff()
    {
        MachineConfig machine = StateMachines.MillTurn() with
        {
            Coolant = new Dictionary<string, FunctionTable> { ["AIR"] = new FunctionTable() },
        };

        var state = new ChannelState(machine);

        Assert.False(state.Coolant["STANDARD"]);
        Assert.False(state.Coolant["AIR"]);
    }

    // VM 2.5: COOLANT[:channel] sets it.
    [Fact]
    public void Coolant_ChangedAfterASnapshot_SnapshotKeepsItOn()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Coolant["THROUGH"] = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Coolant["THROUGH"] = false;

        Assert.True(snapshot.Coolant["THROUGH"]);
        Assert.False(snapshot.Coolant["STANDARD"]);
        Assert.False(state.Coolant["THROUGH"]);
    }

    // VM 2.5: the named functions come from TOML, each without a state until FUNC sets one.
    [Fact]
    public void Functions_AtStart_EveryNamedFunctionWithoutAState()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        string[] functions = ["SUB_CHUCK", "MAIN_CHUCK"];

        Assert.Equal(functions, state.Functions.Keys);
        Assert.All(state.Functions.Values, Assert.Null);
    }

    // D103: the default machine has no named functions.
    [Fact]
    public void Functions_DefaultMachineOfD103_None()
    {
        Assert.Empty(new ChannelState(StateMachines.Default()).Functions);
    }

    // VM 2.5: FUNC:name sets it.
    [Fact]
    public void Functions_ChangedAfterASnapshot_SnapshotKeepsTheState()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Functions["SUB_CHUCK"] = "OPEN";

        ChannelSnapshot snapshot = state.Snapshot();
        state.Functions["SUB_CHUCK"] = "CLOSE";

        Assert.Equal("OPEN", snapshot.Functions["SUB_CHUCK"]);
        Assert.Equal("CLOSE", state.Functions["SUB_CHUCK"]);
    }

    // Architecture 5.3: a snapshot is a deep copy; a spindle and a holder changed afterwards leave it as it was.
    [Fact]
    public void Snapshot_ResourcesChangedAfterwards_KeepsThemAsTheyWere()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S3"].Rpm = 3000m;
        state.Holders["H1"].SpindleTool = new ToolRef(1);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S3"].Rpm = 1500m;
        state.Holders["H1"].SpindleTool = new ToolRef(2);
        state.Spindles.Remove("S2");

        Assert.Equal(3000m, snapshot.Spindles["S3"].Rpm);
        Assert.Equal(new ToolRef(1), snapshot.Holders["H1"].SpindleTool);
        Assert.Equal(3, snapshot.Spindles.Count);
    }

    // Architecture 5.3: two snapshots of the same channel are independent of each other.
    [Fact]
    public void Snapshot_TakenTwice_EachKeepsItsOwnValues()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Feed = 200m;
        ChannelSnapshot before = state.Snapshot();

        state.Motion.Feed = 800m;
        ChannelSnapshot after = state.Snapshot();

        Assert.Equal(200m, before.Motion.Feed);
        Assert.Equal(800m, after.Motion.Feed);
    }
}
