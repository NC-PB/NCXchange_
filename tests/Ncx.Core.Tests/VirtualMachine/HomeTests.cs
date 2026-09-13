using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// HOME, the reference point return (language 4.3, virtual machine 3 step 5, D100).
/// </summary>
public sealed class HomeTests
{
    // VM 3 step 5: afterwards the axes are known in the MACHINE frame at the reference coordinates.
    [Fact]
    public void Home_AxesWithAReferencePoint_AreKnownInTheMachineFrameAtIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X Z");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3 step 5: POINT=2 selects the second reference point.
    [Fact]
    public void Home_Point2_GoesToTheSecondReferencePoint()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X POINT=2");

        Assert.Equal(new AxisPosition(150m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // D100: HOME on an axis without a reference point is a WARNING and the axis is unknown in every frame.
    [Fact]
    public void Home_AxisWithoutAReferencePoint_WarnsAndIsUnknownInEveryFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .At("Y", 5m, PositionFrame.Workpiece)
            .Execute("UNITS=MM", "HOME Y");

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint], vm.Codes());
        Assert.Equal(AxisPosition.Unknown, vm.Position("Y"));
    }

    // D100: the WARNING comes once per run and axis.
    [Fact]
    public void Home_TheSameAxisSeveralTimesInARun_WarnsOncePerAxis()
    {
        string text = VmHarness.File("UNITS=MM", "HOME Y", "HOME Y", "HOME X Y", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.Equal(1, vm.Count(DiagnosticCodes.HomeWithoutReferencePoint));
    }

    // D100: POINT=2 on an axis without home2 has no reference point either.
    [Fact]
    public void Home_Point2OnAnAxisWithoutASecondReferencePoint_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME Z POINT=2");

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint], vm.Codes());
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 5: HOME without an axis name is an ERROR.
    [Fact]
    public void Home_WithoutAnAxisName_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME");

        Assert.Equal([DiagnosticCodes.HomeWithoutAxis], vm.Codes());
    }

    // Language 4.3, D93: a machine axis name stands bare under HOME and resolves against [[axis]].
    [Fact]
    public void Home_MachineAxisName_ResolvesThroughTheAxisList()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME Z2");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("Z2"));
    }

    // D103: the default machine has no reference points, so HOME warns for every axis.
    [Fact]
    public void Home_DefaultMachine_WarnsForEveryAxis()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "HOME X Y Z");

        Assert.Equal(3, vm.Count(DiagnosticCodes.HomeWithoutReferencePoint));
    }
}
