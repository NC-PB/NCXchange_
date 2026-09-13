using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The spindle rules of virtual machine 5, one test per rule with the smallest input: the spindle modes of 3.8 rule 4,
/// the spindle of the tool holder before a LINE, and the speed limits of the machine (machine-config 5, D64).
/// </summary>
public sealed class SpindleValidationTests
{
    // VM 3.8 rule 4, 5: SPINDLE on a spindle in AXIS mode is an ERROR.
    [Fact]
    public void Spindle_InAxisMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_MODE:MAIN=AXIS", "SPINDLE:MAIN=CW");

        RuleAssert.Only(vm, DiagnosticCodes.SpindleInAxisMode);
    }

    // VM 3.8 rule 4, 5: C= on a spindle in SPINDLE mode is an ERROR.
    [Fact]
    public void AxisWord_OnASpindleInSpindleMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "RAPID C=0");

        RuleAssert.Only(vm, DiagnosticCodes.AxisOfSpindleInSpindleMode);
    }

    // VM 5: spindle OFF before a LINE is a WARNING; on the default machine the tool holder carries the spindle TOOL.
    [Fact]
    public void SpindleOff_BeforeALine_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100", "LINE X=1");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "LINE cuts while spindle TOOL, the spindle of the current tool holder, is OFF (virtual machine 5).",
            warning.Message);
    }

    // VM 5: a holder without a spindle of its own checks the default spindle: the second turret of the mill-turn has
    // none, and the default spindle MAIN is OFF while the tool spindle runs.
    [Fact]
    public void SpindleOff_HolderWithoutASpindle_ChecksTheDefaultSpindle()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM F=100 SPINDLE:TOOL=CW", "TOOL:TURRET2=5", "LINE X=1");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "LINE cuts while the default spindle MAIN is OFF; the current tool holder TURRET2 has no spindle of its "
            + "own (virtual machine 5).",
            warning.Message);
    }

    // Language 5 rule 3: the state words of a motion block take effect before its motion, so the spindle started in the
    // LINE block runs when it cuts.
    [Fact]
    public void SpindleOff_StartedInTheLineBlock_IsOn()
    {
        new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100", "LINE X=1 SPINDLE=CW").AssertNoDiagnostics();
    }

    // VM 5, D64: RPM above the spindle's rpm_max is a WARNING.
    [Fact]
    public void Rpm_AboveRpmMax_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM:MAIN=8000");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.RpmAboveMax);

        Assert.StartsWith("RPM:MAIN=8000 is above the rpm_max 6000 of the spindle MAIN", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D64: RPM below the spindle's rpm_min is a WARNING.
    [Fact]
    public void Rpm_BelowRpmMin_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM:MAIN=10");

        RuleAssert.Only(vm, DiagnosticCodes.RpmBelowMin);
    }

    // VM 3.8 rule 2: RPM without a role address is the speed of the default spindle, MAIN on the mill-turn.
    [Fact]
    public void Rpm_WithoutRoleAboveRpmMaxOfTheDefaultSpindle_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM=8000");

        RuleAssert.Only(vm, DiagnosticCodes.RpmAboveMax);
    }

    // Language 4.11: RPM is ignored while CSS is on, so it is not compared.
    [Fact]
    public void Rpm_UnderCss_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("CSS:MAIN=ON RPM:MAIN=8000")
            .AssertNoDiagnostics();
    }
}
