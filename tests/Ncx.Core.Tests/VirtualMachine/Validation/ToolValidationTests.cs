using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The tool rules of virtual machine 5, one test per rule with the smallest input: the rows of the tool change table
/// (VM 3.5) and the offsets in one form per program (language 4.4).
/// </summary>
public sealed class ToolValidationTests
{
    // VM 3.5, 5: the preload of the tool already in the spindle is a WARNING.
    [Fact]
    public void Preload_OfTheToolAlreadyInTheSpindle_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "PRELOAD=4");

        RuleAssert.Only(vm, DiagnosticCodes.ToolAlreadyInSpindle);
    }

    // VM 3.5, 5: TOOL without preload is an ERROR.
    [Fact]
    public void Tool_WithoutPreload_IsAnError()
    {
        RuleAssert.Only(new VmHarness(VmMachines.Default()).Execute("TOOL"), DiagnosticCodes.NothingPreloaded);
    }

    // VM 3.5, 5, D42: a different tool preloaded than called is a WARNING.
    [Fact]
    public void Tool_DifferentToolPreloaded_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=5", "TOOL=4");

        RuleAssert.Only(vm, DiagnosticCodes.PreloadMismatch);
    }

    // VM 3.5, 4, 5: TOOL while a cycle is active is a WARNING.
    [Fact]
    public void Tool_WhileACycleIsActive_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("CYCLE=DRILL CLEARANCE=2 DEPTH=-5", "TOOL=4");

        RuleAssert.Only(vm, DiagnosticCodes.ToolChangeWhileCycleActive);
    }

    // VM 3.5, 5: TOOL while compensation is on is a WARNING.
    [Fact]
    public void Tool_WhileCompensationIsOn_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("COMP=LEFT", "TOOL=4");

        RuleAssert.Only(vm, DiagnosticCodes.ToolChangeWithCompensationOn);
    }

    // VM 5: OFFSET mixed with OFFSET:LEN or OFFSET:RAD in one program is an ERROR.
    [Fact]
    public void Offset_MixedWithOffsetLenInOneProgram_IsAnError()
    {
        string text = VmHarness.File("TOOL=1 OFFSET=1", "TOOL=2 OFFSET:LEN=2", "PROGRAM=END");

        Diagnostic error = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.OffsetFormsMixed);

        Assert.Equal(4, error.Line);
        Assert.Contains("since line 3", error.Message, StringComparison.Ordinal);
    }

    // VM 5: the two forms in one block are mixed as well.
    [Fact]
    public void Offset_BothFormsInOneBlock_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PROGRAM=BEGIN", "TOOL=1 OFFSET=1 OFFSET:RAD=1");

        RuleAssert.Only(vm, DiagnosticCodes.OffsetFormsMixed);
    }

    // VM 5: the form is chosen per program; two programs of one file may choose differently.
    [Fact]
    public void Offset_OneFormInEachOfTwoPrograms_IsAccepted()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            TOOL=1 OFFSET=1
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1
            PROGRAM=END
            FILE=END
            """;

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }

    // VM 5, D99: a subprogram the program calls belongs to it, so the form of the subprogram counts for the program.
    [Fact]
    public void Offset_OtherFormInACalledSubprogram_IsAnError()
    {
        string text = VmHarness.File(
            "TOOL=1 OFFSET:LEN=1", "CALL=9", "PROGRAM=END", "SUB=BEGIN NAME=9", "OFFSET=1", "SUB=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.OffsetFormsMixed);
    }
}
