using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The spindle rows of virtual machine 2.4, per spindle resource: their start values, and a snapshot that keeps the
/// values it was taken with while the live state changes.
/// </summary>
public sealed class SpindleStateTests
{
    // VM 2.4: spindle[r].direction and rpm start OFF and 0.
    [Fact]
    public void DirectionAndRpm_AtStart_AreOffAndZero()
    {
        SpindleState spindle = Spindle();

        Assert.Equal(SpindleDirection.Off, spindle.Direction);
        Assert.Equal(0m, spindle.Rpm);
    }

    // VM 2.4: SPINDLE and RPM set them, per resource.
    [Fact]
    public void DirectionAndRpm_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S1"].Direction = SpindleDirection.Clockwise;
        state.Spindles["S1"].Rpm = 1500m;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S1"].Direction = SpindleDirection.Off;
        state.Spindles["S1"].Rpm = 0m;

        Assert.Equal(SpindleDirection.Clockwise, snapshot.Spindles["S1"].Direction);
        Assert.Equal(1500m, snapshot.Spindles["S1"].Rpm);
        Assert.Equal(SpindleDirection.Off, state.Spindles["S1"].Direction);
    }

    // VM 2.4: spindle[r].mode starts SPINDLE.
    [Fact]
    public void Mode_AtStart_IsSpindle()
    {
        Assert.Equal(SpindleMode.Spindle, Spindle().Mode);
    }

    // VM 2.4: SPINDLE_MODE sets it.
    [Fact]
    public void Mode_ChangedAfterASnapshot_SnapshotKeepsAxis()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S1"].Mode = SpindleMode.Axis;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S1"].Mode = SpindleMode.Spindle;

        Assert.Equal(SpindleMode.Axis, snapshot.Spindles["S1"].Mode);
        Assert.Equal(SpindleMode.Spindle, state.Spindles["S1"].Mode);
    }

    // VM 2.4: spindle[r].orientation starts none.
    [Fact]
    public void Orientation_AtStart_IsNone()
    {
        Assert.Null(Spindle().Orientation);
    }

    // VM 2.4: ORIENT sets it.
    [Fact]
    public void Orientation_ChangedAfterASnapshot_SnapshotKeepsNinety()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S3"].Orientation = 90m;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S3"].Orientation = null;

        Assert.Equal(90m, snapshot.Spindles["S3"].Orientation);
        Assert.Null(state.Spindles["S3"].Orientation);
    }

    // VM 2.4: spindle[r].syncPartner and syncPhase start none.
    [Fact]
    public void SyncPartnerAndPhase_AtStart_AreNone()
    {
        SpindleState spindle = Spindle();

        Assert.Null(spindle.SyncPartner);
        Assert.Null(spindle.SyncPhase);
    }

    // VM 2.4: SPINDLE_SYNC and PHASE set them.
    [Fact]
    public void SyncPartnerAndPhase_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S2"].SyncPartner = "S1";
        state.Spindles["S2"].SyncPhase = 113.5m;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S2"].SyncPartner = null;
        state.Spindles["S2"].SyncPhase = null;

        Assert.Equal("S1", snapshot.Spindles["S2"].SyncPartner);
        Assert.Equal(113.5m, snapshot.Spindles["S2"].SyncPhase);
        Assert.Null(state.Spindles["S2"].SyncPartner);
    }

    // VM 2.4: spindle[r].css, vc and rpmMax start OFF, none and none.
    [Fact]
    public void CssVcAndRpmMax_AtStart_AreOffNoneAndNone()
    {
        SpindleState spindle = Spindle();

        Assert.False(spindle.Css);
        Assert.Null(spindle.Vc);
        Assert.Null(spindle.RpmMax);
    }

    // VM 2.4: CSS, VC and RPM_MAX set them.
    [Fact]
    public void CssVcAndRpmMax_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Spindles["S1"].Css = true;
        state.Spindles["S1"].Vc = 140m;
        state.Spindles["S1"].RpmMax = 3000m;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Spindles["S1"].Css = false;
        state.Spindles["S1"].Vc = null;
        state.Spindles["S1"].RpmMax = null;

        Assert.True(snapshot.Spindles["S1"].Css);
        Assert.Equal(140m, snapshot.Spindles["S1"].Vc);
        Assert.Equal(3000m, snapshot.Spindles["S1"].RpmMax);
        Assert.False(state.Spindles["S1"].Css);
    }

    private static SpindleState Spindle()
    {
        return new ChannelState(StateMachines.MillTurn()).Spindles["S1"];
    }
}
