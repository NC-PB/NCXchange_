using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// Every place where STATIC mode leaves a value UNKNOWN resolves in INTERPRETED mode (virtual machine 1, 3.6;
/// implementation 14, P4-01): an axis word, VAR, ARG, TIMES and F from an expression; the variables start from the vars
/// file, and the SYS_ names read the state through [system_variables], or the vars file where the state has no value
/// (virtual machine 2.7; D38, D51).
/// </summary>
public sealed class InterpretedExpressionTests
{
    // VM 1, 3.6: an axis word from an expression moves the axis to the value it gives.
    [Fact]
    public void AxisWord_FromAnExpression_MovesTheAxisToItsValue()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "UNITS=MM", "VAR:Q1=12.5", "RAPID X={$Q1 * 2} Y=0", "PROGRAM=END"));

        Assert.True(vm.Position("X").Known);
        Assert.Equal(25m, vm.Position("X").Value);
    }

    // VM 1, 3.6: F from an expression is the feed of its LINE, known, not UNKNOWN as in STATIC mode.
    [Fact]
    public void Feed_FromAnExpression_IsTheFeedOfTheLine()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "UNITS=MM", "VAR:Q1=250", "RAPID X=0 Y=0 Z=0", "LINE X=10 F={$Q1}", "PROGRAM=END"));

        MotionEvent line = Assert.Single(vm.Events.Of<MotionEvent>(), motion => motion.Verb == Verb.Line);
        Assert.Equal(250m, line.Feed);
        Assert.DoesNotContain("F", line.After.Unknown);
    }

    // Language 3, 4.9: VAR from an expression holds the number it gives, a decimal written without trailing zeros.
    [Theory]
    [InlineData("{$Q1 / 4}", "2.5")]
    [InlineData("{1.25 * 2}", "2.5")]
    [InlineData("{$Q1 - 10.75}", "-0.75")]
    [InlineData("{SIN(30)}", "0.5")]
    public void Var_FromAnExpression_HoldsTheNumberItGives(string expression, string value)
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=10", "VAR:Q2=" + expression, "PROGRAM=END"));

        Assert.Equal(value, vm.Var("Q2"));
    }

    // Language 3: a whole number an expression gives is an integer, so that a word that takes an integer takes it.
    [Fact]
    public void Var_WholeNumberFromDecimals_IsAnInteger()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File("VAR:Q2={2.5 * 2}", "PROGRAM=END"));

        Assert.Equal(new IntegerValue(5, "5"), vm.State.Vars.Get("Q2")?.Value);
    }

    // Language 4.9: a string passes through an expression as the whole value; VAR takes it.
    [Fact]
    public void Var_StringFromAnExpression_HoldsTheString()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:QS1=\"SIDE MILL\"", "VAR:QS2={$QS1}", "PROGRAM=END"));

        Assert.Equal(new StringValue("SIDE MILL"), vm.State.Vars.Get("QS2")?.Value);
    }

    // VM 3 step 3: the state words of a block do not depend on each other; every expression of a block reads the
    // variables as the block finds them.
    [Fact]
    public void Expressions_OfOneBlock_ReadTheVariablesAsTheBlockFindsThem()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=1", "VAR:Q1={$Q1 + 1} VAR:Q2={$Q1}", "PROGRAM=END"));

        Assert.Equal("2", vm.Var("Q1"));
        Assert.Equal("1", vm.Var("Q2"));
    }

    // VM 5: a string where a number is required is an ERROR.
    [Fact]
    public void AxisWord_StringFromAnExpression_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "UNITS=MM", "VAR:QS1=\"A\"", "RAPID X={$QS1}", "PROGRAM=END"));

        Diagnostic error = vm.Single(DiagnosticCodes.StringWhereNumberIsRequired);
        Assert.Equal(5, error.Line);
        Assert.True(vm.Result!.Stopped);
    }

    // Language 4.9, VM 5: TIMES takes an integer; an expression that gives a number with decimals is an ERROR.
    [Fact]
    public void Times_NumberWithDecimalsFromAnExpression_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "CALL=10 TIMES={5 / 2}", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        Diagnostic error = vm.Single(DiagnosticCodes.ValueNotAnInteger);
        Assert.Contains("TIMES takes an integer", error.Message, StringComparison.Ordinal);
        Assert.Empty(vm.Events.LinesOf("SUB_BEGIN"));
    }

    // Language 4.12: division by zero is an ERROR, and an ERROR stops the run (VM 2.9).
    [Fact]
    public void Expression_DivisionByZero_StopsTheRun()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:Q1={1 / 0}", "VAR:Q2=1", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.DivisionByZero], vm.Codes());
        Assert.Null(vm.Var("Q2"));
        Assert.True(vm.Result!.Stopped);
    }

    // D53: a block the run option skips is not executed, and its expressions are not evaluated.
    [Fact]
    public void Expression_InASkippedBlock_IsNotEvaluated()
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with { SkipBlocks = SkipBlocks.Every };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "SKIP VAR:Q1={1 / 0}", "PROGRAM=END"));

        vm.AssertNoDiagnostics();
    }

    // Language 4.9: a block whose IF is 0 does not execute, and its other expressions are not evaluated.
    [Fact]
    public void Expression_InABlockWhoseIfIsZero_IsNotEvaluated()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:Q1={1 / 0} JUMP=9 IF={0}", "LABEL=9", "PROGRAM=END"));

        vm.AssertNoDiagnostics();
    }

    // VM 3.6, D38: reading an unassigned variable, in IF as anywhere, is an ERROR.
    [Fact]
    public void If_UnassignedVariable_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "JUMP=9 IF={$Q9 == 0}", "LABEL=9", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.UnassignedVariableRead], vm.Codes());
    }

    // VM 3.6, D38, wave-1 question #88: under unassigned = 0 of the run an unassigned variable reads 0; the store and
    // the evaluator read the one setting of VmOptions.
    [Fact]
    public void Var_UnassignedVariableUnderUnassignedZero_Reads0()
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with { Unassigned = UnassignedVariable.Zero };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "VAR:Q1={$Q9 + 1}", "JUMP=9 IF={$Q8 == 0}", "VAR:Q2=1", "LABEL=9", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("1", vm.Var("Q1"));
        Assert.False(vm.State.Vars.Snapshot().ContainsKey("Q2"));
    }

    // VM 2.7, 3.6: the variables start from the vars file, which the expressions read.
    [Fact]
    public void Var_StartValueOfTheVarsFile_IsReadByTheExpressions()
    {
        var startValues = new Dictionary<string, Value> { ["Q1"] = new IntegerValue(4, "4") };

        InterpretedHarness vm = new InterpretedHarness(startValues: startValues).Run(VmHarness.File(
            "VAR:Q2={$Q1 + 1}", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("5", vm.Var("Q2"));
    }

    // VM 2.7, 3.6, wave-1 question #89: a register the virtual machine does not hold reads its value from the vars
    // file, by the name as the program writes it with its index; the index may be an expression (language 4.12).
    [Theory]
    [InlineData("{$SYS_WEAR_Z[99]}")]
    [InlineData("{$SYS_WEAR_Z[$Q9 + 1]}")]
    public void SystemVariable_RegisterTheStateDoesNotHold_ReadsTheVarsFile(string expression)
    {
        var startValues = new Dictionary<string, Value> { ["SYS_WEAR_Z[99]"] = new DecimalValue(0.012m, "0.012") };

        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default()),
            startValues: startValues).Run(VmHarness.File("VAR:Q9=98", "VAR:Q1=" + expression, "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("0.012", vm.Var("Q1"));
    }

    // VM 2.7, 3.6: without a value in the vars file, a register the virtual machine does not hold is an ERROR in
    // INTERPRETED mode, and the message names the vars file as the way out (implementation 14, phase 4 risks).
    [Fact]
    public void SystemVariable_WithoutAValueInTheVarsFile_IsAnErrorNamingTheVarsFile()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default())).Run(VmHarness.File(
            "VAR:Q1={$SYS_WEAR_Z[99]}", "PROGRAM=END"));

        Diagnostic error = vm.Single(DiagnosticCodes.SystemVariableUnknown);
        Assert.Contains("<file>.vars.toml", error.Message, StringComparison.Ordinal);
        Assert.Contains("\"SYS_WEAR_Z[99]\" = value", error.Message, StringComparison.Ordinal);
    }

    // Language 4.12, VM 2.7: $SYS_TOOL reads the active tool from the state through the mapping of the configuration.
    [Fact]
    public void SystemVariable_ActiveTool_ReadsTheState()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default())).Run(VmHarness.File(
            "TOOL=5", "VAR:Q1={$SYS_TOOL + 100}", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("105", vm.Var("Q1"));
    }

    // Language 4.12, VM 2.7, 3.4: $SYS_POS_X reads the current position of X in the workpiece frame.
    [Fact]
    public void SystemVariable_PositionInTheWorkpieceFrame_ReadsTheState()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default())).Run(VmHarness.File(
            "UNITS=MM", "RAPID X=12.5 Y=0 Z=0", "VAR:Q1={$SYS_POS_X}", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("12.5", vm.Var("Q1"));
    }

    // Language 4.12, VM 3.4, D100: $SYS_MPOS_X reads the machine position, known at the reference point of the axis
    // from the start.
    [Fact]
    public void SystemVariable_MachinePosition_ReadsTheReferencePointAtTheStart()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.MillTurn())).Run(VmHarness.File(
            "VAR:Q1={$SYS_MPOS_X}", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal("300", vm.Var("Q1"));
    }

    // VM 2.7, D35, D100: after a HOME without a reference point the position is unknown, and reading it is an ERROR in
    // INTERPRETED mode (implementation 14, phase 4 risks).
    [Fact]
    public void SystemVariable_PositionUnknownAfterHome_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default())).Run(VmHarness.File(
            "UNITS=MM", "RAPID X=12.5", "HOME X", "VAR:Q1={$SYS_POS_X}", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint, DiagnosticCodes.SystemVariableUnknown], vm.Codes());
    }

    // The reading of the open question in VariableStore.PositionOf: under DIAMETER=ON $SYS_POS_X reads the diameter,
    // as the X words of the program are written, while the virtual machine stores the radius (D60).
    [Fact]
    public void SystemVariable_PositionOfXUnderDiameterOn_ReadsTheDiameter()
    {
        InterpretedHarness vm = new InterpretedHarness(WithSystemVariables(VmMachines.Default())).Run(VmHarness.File(
            "UNITS=MM", "DIAMETER=ON", "RAPID X=40 Z=0", "VAR:Q1={$SYS_POS_X}", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal(20m, vm.Position("X").Value);
        Assert.Equal("40", vm.Var("Q1"));
    }

    // VM 2.7, 5: a SYS_ name is never assigned by the program, by ARG neither: ERROR.
    [Fact]
    public void Arg_SystemVariable_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "CALL=10 ARG:SYS_TOOL=1", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        Assert.Equal([DiagnosticCodes.SystemVariableAssigned], vm.Codes());
    }

    // A machine with the [system_variables] of machine-config 7 for the tool, the X position and the Z wear registers.
    private static MachineConfig WithSystemVariables(MachineConfig machine)
    {
        return machine with
        {
            SystemVariables = new SystemVariables
            {
                Entries = new Dictionary<string, string>
                {
                    ["SYS_TOOL"] = "#4120",
                    ["SYS_POS_X"] = "#5041",
                    ["SYS_MPOS_X"] = "#5021",
                    ["SYS_WEAR_Z"] = "#11{index:03}",
                },
            },
        };
    }
}
