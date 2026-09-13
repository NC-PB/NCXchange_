using System.Globalization;
using System.Text;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// CYCLE_CALL (language 4.7, virtual machine 3.3, 5): the sequence of the built-in drilling family along the drilling
/// axis, the position afterwards, the individual motions under ExpandCycles (D37), the lathe cycle with AXIS=X under
/// DIAMETER=ON (D59, D60), the native cycle (D94), and the cycle rules with their suppression (3.9, D99).
/// </summary>
public sealed class CycleCallTests
{
    // Language 6, VM 3.3, D37: the drilling example of the language. Under ExpandCycles each call is four motions, the
    // rapid to the hole, the rapid to CLEARANCE, the feed to DEPTH at CYCLE_F and the retract to CLEARANCE; afterwards
    // the plane axes stand at the call point and Z at the retract plane.
    [Fact]
    public void CycleCall_DrillingExampleOfTheLanguage_GivesTheFourPositionsAndTheRetractPlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565",
            "CYCLE_CALL X=10 Y=10");

        Assert.Equal(
            ["RAPID X=10 Y=10", "RAPID Z=5", "LINE Z=-21.732 F=565", "RAPID Z=5"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));

        vm.Execute("CYCLE_CALL X=30");

        Assert.Equal(
            ["RAPID X=30", "RAPID Z=5", "LINE Z=-21.732 F=565", "RAPID Z=5"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(30m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));

        vm.Execute("CYCLE=OFF");

        vm.AssertNoDiagnostics();
        Assert.False(vm.State.Cycle.Active);
    }

    // VM 3.3, D37: without ExpandCycles the call raises no individual motions; the position afterwards is the same.
    [Fact]
    public void CycleCall_WithoutExpandCycles_GivesNoMotionsAndTheSamePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM",
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_F=565",
            "CYCLE_CALL X=10 Y=10");

        vm.AssertNoDiagnostics();
        Assert.Empty(vm.Vm.LastCycleMotions);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));
    }

    // VM 3.3, language 4.7: CYCLE_RETRACT=SAFE retracts to SAFE.
    [Fact]
    public void CycleCall_CycleRetractSafe_RetractsToTheSafePlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_F=100",
            "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal("RAPID Z=50", Describe(vm.Vm.LastCycleMotions)[^1]);
        Assert.Equal(Workpiece(50m), vm.Position("Z"));
    }

    // VM 3.3, language 4.7: CYCLE_RETRACT=SAFE without SAFE gives no retract plane, and Z is unknown afterwards.
    [Fact]
    public void CycleCall_CycleRetractSafeWithoutSafe_LeavesTheDrillingAxisUnknown()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_RETRACT=SAFE CYCLE_F=100",
            "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.3, 5: CYCLE_CALL without a cycle is an ERROR.
    [Fact]
    public void CycleCall_WithoutACycle_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleCallWithoutCycle], vm.Codes());
    }

    // VM 3.3, 5: a built-in drilling cycle needs DEPTH and CLEARANCE.
    [Theory]
    [InlineData("CYCLE=DRILL CLEARANCE=5 CYCLE_F=100")]
    [InlineData("CYCLE=DRILL DEPTH=-10 CYCLE_F=100")]
    public void CycleCall_BuiltInCycleWithoutDepthOrClearance_IsAnError(string cycle)
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", cycle, "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleCallWithoutDepthOrClearance], vm.Codes());
    }

    // VM 3.3, D94: for a CYCLE:controller=n cycle the VM does not know the sequence: the call positions the plane
    // axes, the drilling axis is unknown afterwards, ExpandCycles raises no motions, and there is no DEPTH or
    // CLEARANCE check.
    [Fact]
    public void CycleCall_NativeCycle_PositionsThePlaneAxesOnly()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50", "CYCLE:HEIDENHAIN=200 Q200=2 Q201=-15 Q206=300", "CYCLE_CALL X=10 Y=10");

        vm.AssertNoDiagnostics();
        Assert.Empty(vm.Vm.LastCycleMotions);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.3, D59, D60: a side drilling cycle on a lathe with AXIS=X under DIAMETER=ON, as the cross holes of
    // POLAR_FACE: the X values of the cycle are diameters and halved, C positions the hole, and X ends at the clearance
    // radius 27.
    [Fact]
    public void CycleCall_AxisXOnALatheUnderDiameterOn_HalvesTheXValuesAndPositionsWithC()
    {
        VmHarness vm = Expanding().Execute(
            "WORKPLANE=ZX DIAMETER=ON SPINDLE_MODE:MAIN=AXIS",
            "RAPID X=60 Z=-20 C=0",
            "CYCLE=DRILL AXIS=X SURFACE=50 CLEARANCE=54 DEPTH=30 CYCLE_RETRACT=CLEARANCE CYCLE_F=0.05",
            "CYCLE_CALL C=120");

        vm.AssertNoDiagnostics();
        Assert.Equal(
            ["RAPID C=120", "RAPID X=27", "LINE X=15 F=0.05", "RAPID X=27"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(27m), vm.Position("X"));
        Assert.Equal(Workpiece(-20m), vm.Position("Z"));
        Assert.Equal(Workpiece(120m), vm.Position("C"));
    }

    // VM 5, D59: AXIS naming an axis that is not a linear axis of the machine is an ERROR.
    [Fact]
    public void CycleCall_AxisNamingARotaryAxis_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "CYCLE=DRILL AXIS=C CLEARANCE=5 DEPTH=-10", "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleAxisNotLinear], vm.Codes());
    }

    // VM 3.3, language 4.7, D37: PECK, the deep hole cycle, feeds in pecks of PECK and returns to CLEARANCE between
    // them.
    [Fact]
    public void CycleCall_PeckCycle_FeedsInPecksWithAFullRetract()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=PECK CLEARANCE=2 DEPTH=-10 PECK=5 CYCLE_F=100", "CYCLE_CALL X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(
            [
                "RAPID X=0", "RAPID Z=2", "LINE Z=-3 F=100", "RAPID Z=2", "RAPID Z=-3", "LINE Z=-8 F=100", "RAPID Z=2",
                "RAPID Z=-8", "LINE Z=-10 F=100", "RAPID Z=2",
            ],
            Describe(vm.Vm.LastCycleMotions));
    }

    // VM 3.3, language 4.7, D37: CHIP_BREAK feeds in pecks without leaving the hole.
    [Fact]
    public void CycleCall_ChipBreakCycle_FeedsInPecksWithoutLeavingTheHole()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=CHIP_BREAK CLEARANCE=2 DEPTH=-10 PECK=5 CYCLE_F=100", "CYCLE_CALL X=0");

        Assert.Equal(
            ["RAPID X=0", "RAPID Z=2", "LINE Z=-3 F=100", "LINE Z=-8 F=100", "LINE Z=-10 F=100", "RAPID Z=2"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // VM 3.3, language 4.7, D37: TAP backs out at CYCLE_F behind the spindle reversal; a call without axis words
    // executes at the current position.
    [Fact]
    public void CycleCall_TapWithoutAxisWords_FeedsOutAtTheCurrentPosition()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=5 Y=5 Z=10", "CYCLE=TAP CLEARANCE=2 DEPTH=-10 PITCH=1.5 CYCLE_F=150", "CYCLE_CALL");

        vm.AssertNoDiagnostics();
        Assert.Equal(["RAPID Z=2", "LINE Z=-10 F=150", "LINE Z=2 F=150"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(5m), vm.Position("X"));
    }

    // VM 1, 3.3: a DEPTH from an expression is not evaluated in STATIC mode; the depth is unknown, the call is no
    // ERROR, and Z still ends at the known clearance plane.
    [Fact]
    public void CycleCall_DepthFromAnExpression_IsNoErrorAndEndsAtTheClearancePlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE=5 DEPTH={$Q1} CYCLE_F=100", "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(["RAPID X=10", "RAPID Z=5", "LINE Z=? F=100", "RAPID Z=5"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));
    }

    // VM 3.9, 5, D99: inside a subprogram that no program calls the cycle rules are suppressed.
    [Fact]
    public void UncalledSub_CycleCallWithoutACycle_IsSuppressed()
    {
        string text = VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "CYCLE_CALL X=1", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.False(vm.Result!.Stopped);
    }

    // The default machine of D103 with ExpandCycles and the units set.
    private static VmHarness Expanding()
    {
        return new VmHarness(VmMachines.Default(), new VmOptions { ExpandCycles = true }).Execute("UNITS=MM");
    }

    private static AxisPosition Workpiece(decimal value)
    {
        return new AxisPosition(value, PositionFrame.Workpiece, Known: true);
    }

    // Each motion as an NCX-like line, the verb and the axes in name order with their stored values, ? for unknown.
    private static List<string> Describe(IReadOnlyList<CycleMotion> motions)
    {
        var lines = new List<string>();
        foreach (CycleMotion motion in motions)
        {
            var line = new StringBuilder(motion.Verb == Verb.Rapid ? "RAPID" : "LINE");
            var axes = new List<string>(motion.To.Keys);
            axes.Sort(StringComparer.Ordinal);
            foreach (string axis in axes)
            {
                AxisPosition position = motion.To[axis];
                string value = position.Known ? position.Value.ToString(CultureInfo.InvariantCulture) : "?";
                line.Append(' ').Append(axis).Append('=').Append(value);
            }

            if (motion.Feed is decimal feed)
            {
                line.Append(" F=").Append(feed.ToString(CultureInfo.InvariantCulture));
            }

            lines.Add(line.ToString());
        }

        return lines;
    }
}
