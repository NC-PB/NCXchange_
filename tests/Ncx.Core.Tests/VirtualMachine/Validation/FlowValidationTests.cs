using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The flow rules of virtual machine 5, one test per rule with the smallest file: labels, jump and call targets, the
/// call depth, JUMP=END from a subprogram, RETURN in a program and the unreachable blocks (language 4.9, 4.13; VM 3.6,
/// 3.9; D89, D99). The file of VmHarness.File has FILE=BEGIN on line 1 and PROGRAM=BEGIN on line 2.
/// </summary>
public sealed class FlowValidationTests
{
    // Language 4.9, VM 2.7, 5: LABEL=END is an ERROR; END is reserved for JUMP=END.
    [Fact]
    public void LabelEnd_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("LABEL=END").Diagnostics, DiagnosticCodes.LabelEnd);
    }

    // Language 4.9, VM 3.6, 5: a duplicate LABEL is an ERROR before execution.
    [Fact]
    public void Label_DuplicateInOneProgram_IsAnErrorBeforeExecution()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("LABEL=1", "TOOL", "LABEL=1", "PROGRAM=END"), VmMachines.Default());

        Diagnostic error = RuleAssert.Only(vm, DiagnosticCodes.DuplicateLabel);

        Assert.Equal(5, error.Line);
        Assert.True(vm.Result!.Stopped);
    }

    // Language 4.9: a label is unique per program or subprogram; two sections may use the same.
    [Fact]
    public void Label_TheSameInAProgramAndASubprogram_IsAccepted()
    {
        string text = VmHarness.File("LABEL=1", "CALL=9", "PROGRAM=END", "SUB=BEGIN NAME=9", "LABEL=1", "SUB=END");

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }

    // Language 4.9, VM 3.6, 5: a missing jump target is an ERROR before execution.
    [Fact]
    public void Jump_ToAMissingLabel_IsAnError()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("JUMP=7", "PROGRAM=END"), VmMachines.Default());

        RuleAssert.Only(vm, DiagnosticCodes.JumpTargetMissing);
    }

    // Language 4.9, VM 3.6: JUMP continues at a label of the current section; one of another section is missing.
    [Fact]
    public void Jump_ToALabelOfAnotherSection_IsAnError()
    {
        string text = VmHarness.File("JUMP=1", "PROGRAM=END", "SUB=BEGIN NAME=9", "LABEL=1", "SUB=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.JumpTargetMissing);
    }

    // Language 4.9, VM 3.6, 5: REPEAT repeats from its label; a missing one is a missing target.
    [Fact]
    public void Repeat_ToAMissingLabel_IsAnError()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("REPEAT=3 TIMES=2", "PROGRAM=END"), VmMachines.Default());

        RuleAssert.Only(vm, DiagnosticCodes.JumpTargetMissing);
    }

    // Language 4.9: JUMP=END continues at PROGRAM=END and needs no label.
    [Fact]
    public void JumpEnd_InAProgram_NeedsNoLabel()
    {
        VmHarness.Run(VmHarness.File("JUMP=END", "PROGRAM=END"), VmMachines.Default()).AssertNoDiagnostics();
    }

    // VM 3.6, 5: a missing call target is an ERROR.
    [Fact]
    public void Call_ToAMissingSubprogram_IsAnError()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("CALL=99", "PROGRAM=END"), VmMachines.Default());

        RuleAssert.Only(vm, DiagnosticCodes.CallTargetMissing);
    }

    // Language 4.13, VM 3.6, 5: a CALL of a program is an ERROR.
    [Fact]
    public void Call_OfAProgram_IsAnError()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            CALL=SECOND
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            PROGRAM=END
            FILE=END
            """;

        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.CallOfProgram);
    }

    // VM 3.9, 5, D99: a CALL beyond the call depth is an ERROR.
    [Fact]
    public void Call_BeyondTheCallDepth_IsAnError()
    {
        string text = VmHarness.File("CALL=1", "PROGRAM=END", "SUB=BEGIN NAME=1", "CALL=1", "SUB=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.CallDepthExceeded);
    }

    // VM 3.6, 5: JUMP=END from inside a subprogram is a WARNING; it ends the program from a call.
    [Fact]
    public void JumpEnd_InsideASubprogram_Warns()
    {
        string text = VmHarness.File("CALL=1", "PROGRAM=END", "SUB=BEGIN NAME=1", "JUMP=END", "SUB=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.JumpEndInSub);

        Assert.Equal(6, warning.Line);
    }

    // Language 4.9, VM 3.6, 5: RETURN in the main program is a WARNING, treated as JUMP=END.
    [Fact]
    public void Return_InAProgram_Warns()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("RETURN", "PROGRAM=END"), VmMachines.Default());

        RuleAssert.Only(vm, DiagnosticCodes.ReturnInProgram);
    }

    // Language 4.9: RETURN in a subprogram returns to the caller.
    [Fact]
    public void Return_InASubprogram_IsAccepted()
    {
        string text = VmHarness.File("CALL=1", "PROGRAM=END", "SUB=BEGIN NAME=1", "RETURN", "SUB=END");

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }

    // Language 4.13, VM 3.9, 5, D89: a block of a program after an unconditional JUMP that no LABEL makes reachable is
    // a WARNING.
    [Fact]
    public void UnreachableBlock_AfterAnUnconditionalJump_Warns()
    {
        string text = VmHarness.File("JUMP=1", "COOLANT=ON", "LABEL=1", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()),
            DiagnosticCodes.UnreachableBlock);

        Assert.Equal(4, warning.Line);
        Assert.StartsWith("The block follows the unconditional JUMP=1 of line 3", warning.Message,
            StringComparison.Ordinal);
    }

    // Language 4.13: each block that no LABEL makes reachable gets its WARNING, up to the next LABEL.
    [Fact]
    public void UnreachableBlock_EveryBlockUpToTheNextLabel_Warns()
    {
        string text = VmHarness.File("JUMP=END", "COOLANT=ON", "COOLANT=OFF", "LABEL=5", "JUMP=END", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.UnreachableBlock, DiagnosticCodes.UnreachableBlock], vm.Codes());
    }

    // Language 4.9: a JUMP with IF is conditional, and a JUMP with SKIP runs only while the switch is off (language
    // 4.1), so the block after either is reachable.
    [Theory]
    [InlineData("JUMP=1 IF={$Q1 > 0}")]
    [InlineData("SKIP JUMP=1")]
    public void UnreachableBlock_AfterAConditionalJump_IsReachable(string jump)
    {
        string text = VmHarness.File(jump, "COOLANT=ON", "LABEL=1", "PROGRAM=END");

        Assert.DoesNotContain(DiagnosticCodes.UnreachableBlock, VmHarness.Run(text, VmMachines.Default()).Codes());
    }

    // Language 4.9, VM 2.7: PROGRAM=END is the target of JUMP=END, so a program that loops back to a label before its
    // end has no unreachable block (language 4.13, the Fanuc M99 in a main program).
    [Fact]
    public void UnreachableBlock_ProgramEndAfterAJumpBack_IsReachable()
    {
        VmHarness.Run(VmHarness.File("LABEL=1", "JUMP=1", "PROGRAM=END"), VmMachines.Default()).AssertNoDiagnostics();
    }

    // VM 3.6: RETURN in a program is treated as JUMP=END, so the blocks after it are unreachable as well.
    [Fact]
    public void UnreachableBlock_AfterReturnInAProgram_Warns()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("RETURN", "COOLANT=ON", "PROGRAM=END"), VmMachines.Default());

        Assert.Equal([DiagnosticCodes.ReturnInProgram, DiagnosticCodes.UnreachableBlock], vm.Codes());
    }
}
