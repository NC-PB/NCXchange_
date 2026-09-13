using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The vector form of 5-axis motion (language 4.3, virtual machine 2.2, 3.1, 5, D81): TX TY TZ and NX NY NZ under
/// TCPM=ON, stored as written, the rotary axes unknown, the length checked with the arc tolerance, and the words that
/// may not stand with them.
/// </summary>
public sealed class VectorWordTests
{
    // Language 6, D81: the 5-axis line under TCPM stores the vectors as written, marks the rotary axes unknown and
    // does not resolve the vectors to axes; the linear axes are known.
    [Fact]
    public void Line_VectorFormOfTheLanguageExample_StoresTheVectorsAsWritten()
    {
        VmHarness vm = Tcpm().Execute(
            "RAPID A=0 B=0",
            "LINE X=41.786 Y=-57.382 Z=95.488 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841");

        vm.AssertNoDiagnostics();
        decimal[] toolVector = [0m, 0.5m, 0.866m];
        decimal[] surfaceNormal = [0m, 0m, 1m];
        Assert.Equal(toolVector, vm.State.Motion.ToolVector);
        Assert.Equal(surfaceNormal, vm.State.Motion.SurfaceNormal);
        Assert.Equal(AxisPosition.Unknown, vm.Position("A"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("B"));
        Assert.Equal(new AxisPosition(41.786m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
    }

    // VM 3.1, 5, D81: vector words without TCPM=ON are an ERROR.
    [Fact]
    public void Line_VectorWithoutTcpm_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM F=100 SPINDLE=CW", "LINE X=1 TX=0 TY=0 TZ=1");

        Assert.Equal([DiagnosticCodes.VectorWithoutTcpm], vm.Codes());
    }

    // VM 3.1, 5, D81: rotary words and vector words in one block are an ERROR.
    [Fact]
    public void Line_VectorWithARotaryWord_IsAnError()
    {
        VmHarness vm = Tcpm().Execute("LINE X=1 B=10 TX=0 TY=0 TZ=1");

        Assert.Equal([DiagnosticCodes.VectorWithRotaryWords], vm.Codes());
    }

    // Language 4.3, VM 5, D81: TX TY TZ all three together, NX NY NZ all three and only together with TX TY TZ.
    [Theory]
    [InlineData("LINE X=1 TX=0 TY=0")]
    [InlineData("LINE X=1 TX=0 TY=0 TZ=1 NX=0 NY=0")]
    [InlineData("LINE X=1 NX=0 NY=0 NZ=1")]
    public void Line_IncompleteVector_IsAnError(string line)
    {
        VmHarness vm = Tcpm().Execute(line);

        Assert.Equal([DiagnosticCodes.VectorIncomplete], vm.Codes());
    }

    // VM 3.1, D36, D81: a vector not of length 1 within the arc tolerance is an ERROR, the tool vector as the normal.
    [Theory]
    [InlineData("LINE X=1 TX=0 TY=0 TZ=1.02")]
    [InlineData("LINE X=1 TX=0 TY=0 TZ=1 NX=0 NY=0 NZ=0.5")]
    public void Line_VectorNotOfUnitLength_IsAnError(string line)
    {
        VmHarness vm = Tcpm().Execute(line);

        Assert.Equal([DiagnosticCodes.VectorNotUnitLength], vm.Codes());
    }

    // VM 3.1, D36: a length within the arc tolerance of 0.01 is a unit length.
    [Fact]
    public void Line_VectorOfUnitLengthWithinTheTolerance_IsStored()
    {
        VmHarness vm = Tcpm().Execute("LINE X=1 TX=0 TY=0 TZ=1.005");

        vm.AssertNoDiagnostics();
        decimal[] toolVector = [0m, 0m, 1.005m];
        Assert.Equal(toolVector, vm.State.Motion.ToolVector);
    }

    // VM 2.2, D81: the tool vector and the surface normal are unknown again after any rotary axis word.
    [Fact]
    public void Line_RotaryWordAfterAVector_MakesTheVectorsUnknown()
    {
        VmHarness vm = Tcpm().Execute("LINE X=1 TX=0 TY=0 TZ=1 NX=0 NY=0 NZ=1", "LINE B=10");

        vm.AssertNoDiagnostics();
        Assert.Null(vm.State.Motion.ToolVector);
        Assert.Null(vm.State.Motion.SurfaceNormal);
    }

    // VM 1, D81: a component from an expression is not evaluated in STATIC mode; the vector is unknown and its length
    // is not checked.
    [Fact]
    public void Line_VectorFromAnExpression_IsUnknown()
    {
        VmHarness vm = Tcpm().Execute("LINE X=1 TX={$Q1} TY=0 TZ=1");

        vm.AssertNoDiagnostics();
        Assert.Null(vm.State.Motion.ToolVector);
    }

    // The default machine of D103 with the units, a feed and TCPM=ON.
    private static VmHarness Tcpm()
    {
        return new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100 TCPM=ON SPINDLE=CW");
    }
}
