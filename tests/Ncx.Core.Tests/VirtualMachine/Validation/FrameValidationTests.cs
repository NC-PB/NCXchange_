using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The frame rules of virtual machine 5, one test per rule with the smallest input: SETPOS, the path tolerance and ROT
/// against the kinematics of the machine (language 4.1, 4.2; D82, D85, D101).
/// </summary>
public sealed class FrameValidationTests
{
    // VM 5, language 4.1, D85: TOLERANCE:ROTARY without an active TOLERANCE is an ERROR.
    [Fact]
    public void ToleranceRotary_WithoutTolerance_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE:ROTARY=0.05");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 5, language 4.1, D85: TOLERANCE_MODE without an active TOLERANCE is an ERROR.
    [Fact]
    public void ToleranceMode_WithoutTolerance_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE_MODE=ROUGH");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 3 step 3: the state words of a block do not depend on each other, so the TOLERANCE of the block makes it
    // active for TOLERANCE:ROTARY and TOLERANCE_MODE (language 6, the path tolerance example).
    [Fact]
    public void ToleranceWords_WithTheToleranceOfTheirBlock_AreAccepted()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH", "TOLERANCE_MODE=ROUGH");

        vm.AssertNoDiagnostics();
    }

    // VM 5, D85: after TOLERANCE=OFF the tolerance is no longer active.
    [Fact]
    public void ToleranceMode_AfterToleranceOff_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE=0.02", "TOLERANCE=OFF",
            "TOLERANCE_MODE=ROUGH");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 5, language 4.2: SETPOS without an axis word is an ERROR.
    [Fact]
    public void Setpos_WithoutAnAxisWord_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SETPOS");

        RuleAssert.Only(vm, DiagnosticCodes.SetposWithoutAxis);
    }

    // VM 3.4, 5, D101: SETPOS with an axis unknown in every frame is an ERROR.
    [Fact]
    public void Setpos_AxisUnknownInEveryFrame_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SETPOS X=0");

        RuleAssert.Only(vm, DiagnosticCodes.SetposAxisUnknown);
    }

    // VM 5, language 4.2, D82: ROT on a target without table kinematics is a WARNING; the mill-turn has no rotary axis
    // on a table.
    [Fact]
    public void Rot_OnAMachineWithoutATable_Warns()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.MillTurn()), DiagnosticCodes.RotWithoutTableKinematics);
    }

    // Language 4.2, D82: on table kinematics ROT chooses how the table reaches the plane.
    [Fact]
    public void Rot_OnAMachineWithARotaryTable_IsAccepted()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        VmHarness.Run(text, ValidationMachines.MillWithRotaryTable()).AssertNoDiagnostics();
    }

    // D77, D103: the built-in default machine is no target, so ROT is not checked without a machine file.
    [Fact]
    public void Rot_WithoutAMachineFile_IsNotChecked()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }
}
