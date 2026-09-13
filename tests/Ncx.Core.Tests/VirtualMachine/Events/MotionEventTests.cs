using Ncx.Core.Geometry;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// MOTION (virtual machine 7 as amended by F18): verb, from, to, center, direction, sweep angle, tool vector and
/// surface normal, feed, compensation, frame and the length of every RAPID, LINE, ARC and RETRACT.
/// </summary>
public sealed class MotionEventTests
{
    // VM 7, row MOTION: a LINE with its end points, feed, compensation, frame and length.
    [Fact]
    public void Motion_Line_CarriesFromToFeedCompensationFrameAndLength()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=30 Y=40 F=500 COMP=LEFT");

        MotionEvent line = listener.Of<MotionEvent>()[1];
        Assert.Equal(Verb.Line, line.Verb);
        Assert.Equal(Workpiece(0m), line.From["X"]);
        Assert.Equal(Workpiece(30m), line.To["X"]);
        Assert.Equal(Workpiece(40m), line.To["Y"]);
        Assert.Equal(500m, line.Feed);
        Assert.Equal(FeedMode.PerMin, line.FeedMode);
        Assert.Equal(Compensation.Left, line.Comp);
        Assert.Equal(PositionFrame.Workpiece, line.Frame);
        Assert.Equal(50m, line.Length);
        Assert.Null(line.Center);
        Assert.Null(line.Sweep);
        Assert.Equal(
            "MOTION(3): LINE X 0 -> 30, Y 0 -> 40; feed 500 PER_MIN, comp LEFT, frame WORKPIECE, length 50",
            line.ToString());
    }

    // VM 3.1, 7: a motion from an unknown position has no known length; a RAPID moves at rapid and carries no feed.
    [Fact]
    public void Motion_RapidFromAnUnknownPosition_HasNoFeedAndNoKnownLength()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "F=100", "RAPID X=50.4 Y=-7.025");

        MotionEvent rapid = Assert.Single(listener.Of<MotionEvent>());
        Assert.Null(rapid.Feed);
        Assert.Null(rapid.Length);
        Assert.Equal("MOTION(3): RAPID X ? -> 50.4, Y ? -> -7.025; comp OFF, frame WORKPIECE, length ?", rapid.ToString());
    }

    // VM 3.2, 7: the R form of 2.5D_FRAESEN line 22, a quarter circle about the computed center 7/7, the arc length.
    [Fact]
    public void Motion_ArcInTheRForm_CarriesCenterDirectionSweepAndArcLength()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID X=7 Y=2 Z=-10", "ARC=CW X=2 Y=7 R=5 F=2387");

        MotionEvent arc = listener.Of<MotionEvent>()[1];
        Assert.Equal(Verb.Arc, arc.Verb);
        Assert.Equal(ArcDirection.Clockwise, arc.Direction);
        Assert.Equal(Workpiece(7m), arc.Center?["X"]);
        Assert.Equal(Workpiece(7m), arc.Center?["Y"]);
        Assert.Equal(90m, arc.Sweep);
        Assert.Equal(7.854m, arc.Length);
        Assert.Equal(
            "MOTION(3): ARC CW X 7 -> 2, Y 2 -> 7; center X=7 Y=7, sweep 90, feed 2387 PER_MIN, comp OFF, "
            + "frame WORKPIECE, length 7.854",
            arc.ToString());
    }

    // VM 3.2, 7: the CENTER form of 2.5D_FRAESEN line 41, counterclockwise from 88.47 degrees round to 0 degrees.
    [Fact]
    public void Motion_ArcInTheCenterForm_CarriesTheSweepAndTheArcLength()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "RAPID X=50.534 Y=69.993 Z=-10", "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50 F=1.5");

        MotionEvent arc = listener.Of<MotionEvent>()[1];
        Assert.Equal(ArcDirection.Counterclockwise, arc.Direction);
        Assert.Equal(271.53m, arc.Sweep);
        Assert.Equal(94.782m, arc.Length);
    }

    // VM 3.2: a tool-axis word makes a helix, and its travel is part of the length.
    [Fact]
    public void Motion_Helix_LengthIncludesTheTravelOfTheToolAxis()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "RAPID X=10 Y=0 Z=0", "ARC=CCW X=10 Y=0 Z=-5 CENTER:X=0 CENTER:Y=0 F=100");

        MotionEvent helix = listener.Of<MotionEvent>()[1];
        Assert.Equal(360m, helix.Sweep);
        Assert.Equal(63.03m, helix.Length);
    }

    // VM 2.1, row frame (block); 3.4, D35: a FRAME=MACHINE block moves in machine coordinates, and a move from the
    // workpiece frame into the machine frame has no known length.
    [Fact]
    public void Motion_FrameMachine_CarriesTheMachineFrame()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID Z=2", "RAPID Z=0 FRAME=MACHINE");

        MotionEvent rapid = listener.Of<MotionEvent>()[1];
        Assert.Equal(PositionFrame.Machine, rapid.Frame);
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), rapid.To["Z"]);
        Assert.Null(rapid.Length);
        Assert.Equal("MOTION(3): RAPID Z 2 -> 0 (MACHINE); comp OFF, frame MACHINE, length ?", rapid.ToString());
    }

    // VM 8, segment length: the euclidean length of the linear axes; a rotary axis turns in degrees and is no part of
    // it (the tool vector change covers it).
    [Fact]
    public void Motion_RotaryAxis_IsNoPartOfTheLength()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID X=0 A=0", "LINE X=10 A=90 F=100");

        Assert.Equal(10m, listener.Of<MotionEvent>()[1].Length);
    }

    // VM 7, D81: the tool vector and the surface normal of a LINE under TCPM=ON when given.
    [Fact]
    public void Motion_VectorWordsUnderTcpm_CarryTheToolVectorAndTheSurfaceNormal()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "TCPM=ON", "RAPID X=0 Y=0 Z=0", "LINE X=10 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=100");

        MotionEvent line = listener.Of<MotionEvent>()[1];
        Assert.Equal([0m, 0.5m, 0.866m], line.ToolVector);
        Assert.Equal([0m, 0m, 1m], line.SurfaceNormal);
        Assert.Contains("tool vector 0,0.5,0.866, surface normal 0,0,1", line.ToString(), StringComparison.Ordinal);
    }

    // VM 7, row MOTION; 3.1a: RETRACT is a motion along the tool axis.
    [Fact]
    public void Motion_Retract_MovesTheToolAxis()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "RAPID X=10 Y=10 Z=2", "RETRACT=50");

        MotionEvent retract = listener.Of<MotionEvent>()[1];
        Assert.Equal(Verb.Retract, retract.Verb);
        Assert.Equal(Workpiece(52m), retract.To["Z"]);
        Assert.Equal(50m, retract.Length);
    }

    // VM 3 step 5, 8; architecture 5.1: HOME moves at rapid to the reference point in machine coordinates and is
    // raised as a MOTION (the question is marked in BlockEvents).
    [Fact]
    public void Motion_Home_IsAMotionInTheMachineFrame()
    {
        FakeListener listener = EventRuns.Execute(VmMachines.MillTurn(), null, "UNITS=MM", "RAPID Z=0", "HOME Z");

        MotionEvent home = listener.Of<MotionEvent>()[1];
        Assert.Equal(Verb.Home, home.Verb);
        Assert.Equal(PositionFrame.Machine, home.Frame);
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), home.To["Z"]);
    }

    private static AxisPosition Workpiece(decimal value)
    {
        return new AxisPosition(value, PositionFrame.Workpiece, Known: true);
    }
}
