using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The five examples of the specification run in STATIC mode without a machine file, against the built-in default
/// machine of D103: no ERROR, and the WARNINGs the notes, D100, D103 and the validation list of virtual machine 5 name.
/// The whole text of the diagnostics of each is compared in Ncx.Acceptance (ExampleCheckTests).
/// </summary>
public sealed class ExampleRunTests
{
    // Phases table, D103: the five examples run with no ERROR without a machine file.
    [Theory]
    [InlineData("2.5D_FRAESEN.ncx")]
    [InlineData("PATTERN_LOOP.ncx")]
    [InlineData("INCREMENTAL_SUB.ncx")]
    [InlineData("MILLTURN_TRANSFER.ncx")]
    [InlineData("POLAR_FACE.ncx")]
    public void Example_WithoutAMachineFile_RunsWithoutAnError(string example)
    {
        VmHarness vm = Run(example);

        Assert.False(vm.Diagnostics.HasErrors, vm.Diagnostics.ToText());
        Assert.False(vm.Result!.Stopped);
    }

    // 2.5D_FRAESEN: nothing to report from block execution; tool 1 stays in the spindle at PROGRAM=END (VM 4).
    [Fact]
    public void Fraesen25D_WithoutAMachineFile_RaisesNothingAndKeepsTheTool()
    {
        VmHarness vm = Run("2.5D_FRAESEN.ncx");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(1), vm.State.Holders["H1"].SpindleTool);
        Assert.Equal(1, vm.State.Holders["H1"].OffsetLen);
        Assert.False(vm.State.Coolant["STANDARD"]);
    }

    // PATTERN_LOOP: a variable set from an expression is UNKNOWN in STATIC mode (VM 1), and the four expressions of the
    // loop are one WARNING, counted and reported once (VM 5).
    [Fact]
    public void PatternLoop_WithoutAMachineFile_LeavesTheLoopVariablesUnknown()
    {
        VmHarness vm = Run("PATTERN_LOOP.ncx");

        Assert.Equal([DiagnosticCodes.UnresolvedExpressions], vm.Codes());
        Assert.True(vm.State.Vars.Get("Q1")?.IsUnknown);
        Assert.True(vm.State.Vars.Get("Q3")?.IsUnknown);
        Assert.Equal("5", vm.State.Vars.Get("Q2")?.ToString());
    }

    // INCREMENTAL_SUB: the subprogram is followed at its CALL (D99), the bare TOOL changes to the preloaded tool 3, and
    // the parking section warns for HOME Z, X and Y without reference points (D100).
    [Fact]
    public void IncrementalSub_WithoutAMachineFile_WarnsOnlyForTheHomesWithoutReferencePoints()
    {
        VmHarness vm = Run("INCREMENTAL_SUB.ncx");

        Assert.Equal(
            [
                DiagnosticCodes.HomeWithoutReferencePoint,
                DiagnosticCodes.HomeWithoutReferencePoint,
                DiagnosticCodes.HomeWithoutReferencePoint,
            ],
            vm.Codes());
        Assert.Equal(new ToolRef(3), vm.State.Holders["H1"].SpindleTool);
        Assert.Null(vm.State.Holders["H1"].Preloaded);
    }

    // MILLTURN_TRANSFER: the D103 WARNINGs name TURRET1, SUB, SUB_CHUCK, Z2 and MAIN_CHUCK, once each; the holder and
    // the sub spindle are created on the spot, and C resolves to the sub spindle's own axis after WORKPIECE=SUB. The
    // holder TURRET1 created on the spot has no spindle, so the LINEs of lines 13 and 14 find the default spindle, the
    // tool spindle TOOL, still OFF: spindle OFF before a LINE (VM 5). Which spindle is the default one on the default
    // machine is wave-1 question #53; at LINE C=90 the tool spindle runs.
    [Fact]
    public void MillturnTransfer_WithoutAMachineFile_WarnsNotCheckedOnceForEachMissingName()
    {
        VmHarness vm = Run("MILLTURN_TRANSFER.ncx");

        List<string> messages = vm.Messages(DiagnosticCodes.NotCheckedNoMachineFile);
        var spindleOffLines = new List<int>();
        foreach (Diagnostic diagnostic in vm.Diagnostics.Items)
        {
            if (diagnostic.Code == DiagnosticCodes.SpindleOffBeforeLine)
            {
                spindleOffLines.Add(diagnostic.Line);
            }
        }

        Assert.Equal(7, vm.Codes().Count);
        Assert.Equal(5, messages.Count);
        Assert.Equal([13, 14], spindleOffLines);
        string[] names = ["role TURRET1", "role SUB", "function SUB_CHUCK", "axis Z2", "function MAIN_CHUCK"];
        for (int index = 0; index < names.Length; index++)
        {
            Assert.Contains(names[index], messages[index], StringComparison.Ordinal);
        }

        Assert.Equal(new ToolRef(12), vm.State.Holders["TURRET1"].SpindleTool);
        Assert.Equal(12, vm.State.Holders["TURRET1"].OffsetLen);
        Assert.Equal("SUB", vm.State.Frame.WorkpieceHolder);
        Assert.True(vm.State.Motion.Position.ContainsKey("C_SUB"));
    }

    // POLAR_FACE: only the HOME C WARNING (D100); SETPOS C=0 directly after it makes C known in the workpiece frame
    // with the shift unknown (D101).
    [Fact]
    public void PolarFace_WithoutAMachineFile_WarnsOnlyForHomeCAndAcceptsTheSetpos()
    {
        VmHarness vm = Run("POLAR_FACE.ncx");

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint], vm.Codes());
        Assert.Contains("SETPOS:C", vm.State.Unknown);

        // C ends where the last cross hole put it, CYCLE_CALL C=240, read through the shift that stayed unknown (VM
        // 3.3, 3.4, D101).
        Assert.Equal(new AxisPosition(240m, PositionFrame.Workpiece, Known: true), vm.Position("C"));
    }

    private static VmHarness Run(string example)
    {
        return VmHarness.Run(Fixture.ReadText(example), VmMachines.Default());
    }
}
