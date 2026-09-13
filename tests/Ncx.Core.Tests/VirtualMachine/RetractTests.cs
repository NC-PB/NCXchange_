using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// RETRACT (language 4.3, virtual machine 3.1a, 5, D83): the tool axis only, bare to its upper limit in machine
/// coordinates (D100), by a distance in its frame, unknown without limits, the moved axes unknown under a tilt; a feed
/// or another axis word is an ERROR.
/// </summary>
public sealed class RetractTests
{
    // VM 3.1a, D83, D100: a bare RETRACT moves the tool axis to its upper limit, a machine coordinate, so the axis is
    // known in the MACHINE frame afterwards; the other axes stay.
    [Fact]
    public void Retract_Bare_MovesTheToolAxisToItsUpperLimitInTheMachineFrame()
    {
        VmHarness vm = new VmHarness(WithLimits()).Execute("UNITS=MM", "RAPID X=10 Y=10 Z=2", "RETRACT");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(100m, PositionFrame.Machine, Known: true), vm.Position("Z"));
        Assert.Equal(new AxisPosition(10m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(10m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
    }

    // VM 3.1a, D83: RETRACT=50 moves the tool axis by that distance, away from the workpiece.
    [Fact]
    public void Retract_WithADistance_MovesTheToolAxisByIt()
    {
        VmHarness vm = new VmHarness(WithLimits()).Execute("UNITS=MM", "RAPID X=10 Y=10 Z=2", "RETRACT=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(52m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // VM 3.1a: without limits in the configuration the bare RETRACT leaves the tool axis unknown.
    [Fact]
    public void Retract_BareWithoutLimits_LeavesTheToolAxisUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID Z=2", "RETRACT");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.1a: the tool axis is the axis perpendicular to the WORKPLANE, Y for ZX.
    [Fact]
    public void Retract_WorkplaneZX_MovesY()
    {
        VmHarness vm = new VmHarness(WithLimits()).Execute("UNITS=MM WORKPLANE=ZX", "RAPID X=10 Y=0 Z=2", "RETRACT=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(5m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // VM 3.1a, D35: under a tilt the direction is known only with the kinematics module; without it the moved axes
    // become unknown.
    [Theory]
    [InlineData("TILT B=45")]
    [InlineData("TILT_AXIS B=45")]
    public void Retract_UnderATilt_LeavesTheLinearAxesUnknown(string tilt)
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", tilt, "RAPID X=1 Y=2 Z=3", "RETRACT=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Y"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.1a, 5: a RETRACT with a feed is an ERROR.
    [Fact]
    public void Retract_WithAFeed_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID Z=2", "RETRACT F=100");

        Assert.Equal([DiagnosticCodes.RetractWithFeed], vm.Codes());
    }

    // VM 3.1a, 5, language 5 rule 2: a RETRACT with other axis words is an ERROR, which the parser reports: RETRACT
    // carries no axis words.
    [Fact]
    public void Retract_WithAnAxisWord_IsAnErrorOfTheParser()
    {
        var diagnostics = new Diagnostics("test.ncx");

        Parser.ParseBlock("RETRACT Z=5", 1, new ParserOptions(), diagnostics);

        Assert.Contains(diagnostics.Items, diagnostic => diagnostic.Code == DiagnosticCodes.AxisWordWithoutVerb);
    }

    // VM 3.1a: a distance from an unknown position leaves the tool axis unknown, without an ERROR.
    [Fact]
    public void Retract_WithADistanceFromAnUnknownPosition_LeavesTheToolAxisUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RETRACT=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // The default machine of D103 with travel limits on X, Y and Z in machine coordinates (D100).
    private static MachineConfig WithLimits()
    {
        MachineConfig machine = VmMachines.Default();
        return machine with
        {
            Axes =
            [
                new AxisDef { Id = "X", NcxName = "X", Kind = AxisKind.Linear, Min = -10m, Max = 650m },
                new AxisDef { Id = "Y", NcxName = "Y", Kind = AxisKind.Linear, Min = -200m, Max = 200m },
                new AxisDef { Id = "Z", NcxName = "Z", Kind = AxisKind.Linear, Min = -500m, Max = 100m },
                new AxisDef { Id = "A", NcxName = "A", Kind = AxisKind.Rotary },
                new AxisDef { Id = "B", NcxName = "B", Kind = AxisKind.Rotary },
                new AxisDef { Id = "C", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1" },
            ],
        };
    }
}
