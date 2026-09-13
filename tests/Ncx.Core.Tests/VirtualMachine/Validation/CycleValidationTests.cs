using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The cycle rules of virtual machine 5, one test per rule with the smallest input (VM 3.3; D59, D94).
/// </summary>
public sealed class CycleValidationTests
{
    // VM 3.3, 5: CYCLE_CALL without cycle is an ERROR.
    [Fact]
    public void CycleCall_WithoutACycle_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleCallWithoutCycle);
    }

    // VM 3.3, 5: CYCLE_CALL of a built-in drilling cycle without DEPTH is an ERROR.
    [Fact]
    public void CycleCall_BuiltInDrillingCycleWithoutDepth_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "CYCLE=DRILL CLEARANCE=2", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleCallWithoutDepthOrClearance);
    }

    // VM 3.3, 5, D59: AXIS naming an axis that is not a linear axis of the machine is an ERROR.
    [Fact]
    public void Axis_NotALinearAxis_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "CYCLE=DRILL AXIS=C CLEARANCE=2 DEPTH=-5", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleAxisNotLinear);
    }
}
