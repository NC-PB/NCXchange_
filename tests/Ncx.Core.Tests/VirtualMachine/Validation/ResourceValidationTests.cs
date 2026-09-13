using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The resource rules of virtual machine 5, one test per rule with the smallest input: roles, functions and machine
/// axes with a machine file and without one (VM 3.8, D93, D103), MFUNC against the configuration (language 4.6), and
/// WORKPIECE while the old holder's spindle runs.
/// </summary>
public sealed class ResourceValidationTests
{
    // VM 3.8 rule 1, 5: an unknown role is an ERROR when a machine file is given.
    [Fact]
    public void Role_UnknownWithAMachineFile_IsAnError()
    {
        RuleAssert.Only(new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:NOSE=CW"), DiagnosticCodes.UnknownRole);
    }

    // VM 3.8, 5: an unknown function is an ERROR when a machine file is given.
    [Fact]
    public void Function_UnknownWithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("FUNC:DOOR=OPEN");

        RuleAssert.Only(vm, DiagnosticCodes.UnknownFunction);
    }

    // Machine-config 5, VM 5: an unknown value of a function, a state the function does not list, is an ERROR.
    [Fact]
    public void Function_UnknownState_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("FUNC:SUB_CHUCK=HALF");

        RuleAssert.Only(vm, DiagnosticCodes.UnknownFunctionState);
    }

    // VM 3.8 rule 3, 5, D93: a machine axis word not declared in [[axis]] is an ERROR when a machine file is given.
    [Fact]
    public void MachineAxis_NotDeclared_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "RAPID W=1");

        RuleAssert.Only(vm, DiagnosticCodes.UnknownMachineAxis);
    }

    // VM 3.8, 5, D103: without a machine file the name the default machine lacks is "not checked: no machine file".
    [Fact]
    public void Function_WithoutAMachineFile_IsNotChecked()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("FUNC:DOOR=OPEN");

        RuleAssert.Only(vm, DiagnosticCodes.NotCheckedNoMachineFile);
    }

    // Language 4.6, VM 5: MFUNC with a number the configuration names is a WARNING: M8 is COOLANT on the mill-turn.
    [Fact]
    public void Mfunc_ANumberTheConfigurationNames_Warns()
    {
        string text = VmHarness.File("MFUNC=8", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.MillTurn()),
            DiagnosticCodes.MfuncNamedByMachine);

        Assert.StartsWith("MFUNC=8 is M8, which the machine configuration writes for [coolant] STANDARD ON",
            warning.Message, StringComparison.Ordinal);
    }

    // Language 4.6: MFUNC is for the functions the configuration does not name.
    [Fact]
    public void Mfunc_ANumberTheConfigurationDoesNotName_IsAccepted()
    {
        VmHarness.Run(VmHarness.File("MFUNC=136", "PROGRAM=END"), VmMachines.MillTurn()).AssertNoDiagnostics();
    }

    // D105: an M code is a word of a template, M3 of "M3 P11", never a part of another number.
    [Fact]
    public void Mfunc_APartOfALongerCode_IsNoMatch()
    {
        VmHarness.Run(VmHarness.File("MFUNC=5", "PROGRAM=END"), VmMachines.MillTurn()).AssertNoDiagnostics();
    }

    // VM 5: a WORKPIECE change while the old holder's spindle runs is a WARNING.
    [Fact]
    public void Workpiece_ChangeWhileTheOldHoldersSpindleRuns_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:MAIN=CW", "WORKPIECE=SUB");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.WorkpieceChangeWhileSpindleRuns);

        Assert.StartsWith("WORKPIECE=SUB changes the workpiece holder while the spindle MAIN of the old holder runs",
            warning.Message, StringComparison.Ordinal);
    }

    // VM 3 step 3: the old holder's spindle stopped in the same block no longer runs at the change.
    [Fact]
    public void Workpiece_ChangeWithTheSpindleStoppedInTheSameBlock_IsAccepted()
    {
        new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:MAIN=CW", "WORKPIECE=SUB SPINDLE:MAIN=OFF")
            .AssertNoDiagnostics();
    }

    // VM 3.4: selecting the holder that already holds the part is no change.
    [Fact]
    public void Workpiece_TheHolderThatHoldsThePart_IsNoChange()
    {
        new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:MAIN=CW", "WORKPIECE=MAIN").AssertNoDiagnostics();
    }
}
