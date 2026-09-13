using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The rules of RETRACT and HOME in virtual machine 5, one test per rule with the smallest input (VM 3 step 5, 3.1a;
/// D83, D100).
/// </summary>
public sealed class RetractAndHomeValidationTests
{
    // VM 3.1a, 5, D83: RETRACT with feed is an ERROR.
    [Fact]
    public void Retract_WithFeed_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RETRACT F=100");

        RuleAssert.Only(vm, DiagnosticCodes.RetractWithFeed);
    }

    // VM 5: HOME without an axis name is an ERROR.
    [Fact]
    public void Home_WithoutAnAxisName_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "HOME");

        RuleAssert.Only(vm, DiagnosticCodes.HomeWithoutAxis);
    }

    // VM 3 step 5, 5, D100: HOME on an axis without a reference point in the configuration is a WARNING.
    [Fact]
    public void Home_OnAnAxisWithoutAReferencePoint_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "HOME Z");

        RuleAssert.Only(vm, DiagnosticCodes.HomeWithoutReferencePoint);
    }
}
