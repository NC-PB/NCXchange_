using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The flow of INTERPRETED mode (virtual machine 1, 3.6, 3.9): the program that runs, JUMP with IF, JUMP=END, CALL of a
/// subprogram with ARG and TIMES, SUB=END and RETURN, REPEAT with TIMES, the depth that calls and repeats share, the
/// block cap, SKIP per the run option (D53) and PROGRAM=END.
/// </summary>
public sealed class InterpretedFlowTests
{
    private const string TwoPrograms = """
        FILE=BEGIN NCX=1
        PROGRAM=BEGIN NAME="FIRST"
        VAR:Q1=1
        PROGRAM=END
        PROGRAM=BEGIN NAME="SECOND"
        VAR:Q2=2
        PROGRAM=END
        FILE=END
        """;

    // VM 1, 3.9: INTERPRETED mode executes the program that runs, the first of the file; the other programs of the
    // file are not executed unless the job runs them on their channels.
    [Fact]
    public void Run_TwoPrograms_ExecutesTheFirstOnly()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(TwoPrograms);

        Assert.Equal("1", vm.Var("Q1"));
        Assert.Null(vm.Var("Q2"));
        ProgramEvent begin = Assert.Single(vm.Events.Of<ProgramEvent>(), e => e.Phase == EventPhase.Begin);
        Assert.Equal("FIRST", begin.Name);
    }

    // VM 1: Run of a virtual machine built in INTERPRETED mode runs the first program of the file with its flow.
    [Fact]
    public void Run_VirtualMachineInInterpretedMode_RunsTheFirstProgram()
    {
        var harness = new InterpretedHarness();

        RunResult result = harness.Vm.Run(Parser.Parse(TwoPrograms, "test.ncx", new ParserOptions()));

        Assert.False(result.Stopped, harness.Diagnostics.ToText());
        Assert.Equal("1", harness.Var("Q1"));
        Assert.Null(harness.Var("Q2"));
    }

    // Language 4.13, VM 3.6: the job manifest or the command line names the program that runs.
    [Fact]
    public void RunInterpreted_ProgramNamed_RunsThatProgram()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(TwoPrograms, programName: "SECOND");

        vm.AssertNoErrors();
        Assert.Null(vm.Var("Q1"));
        Assert.Equal("2", vm.Var("Q2"));
    }

    // Language 4.13, VM 3.6: a program the file does not have cannot run: ERROR before the first block.
    [Fact]
    public void RunInterpreted_ProgramTheFileDoesNotHave_IsAnErrorBeforeTheFirstBlock()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(TwoPrograms, programName: "THIRD");

        Assert.Equal([DiagnosticCodes.ProgramToRunMissing], vm.Codes());
        Assert.True(vm.Result!.Stopped);
        Assert.Empty(vm.Events.Events);
    }

    // VM 1: RunInterpreted needs a virtual machine built in INTERPRETED mode; STATIC mode is walked by Run.
    [Fact]
    public void RunInterpreted_OnAStaticVirtualMachine_Throws()
    {
        var vm = new VmHarness(VmMachines.Default());
        NcxProgram program = Parser.Parse(TwoPrograms, "test.ncx", new ParserOptions());

        Assert.Throws<InvalidOperationException>(() => vm.Vm.RunInterpreted(program));
    }

    // Language 4.9, VM 3.6: JUMP with IF continues at the label when the expression is not 0, and with 0 the flow goes
    // on with the next block.
    [Theory]
    [InlineData("1", null)]
    [InlineData("0", "1")]
    public void Jump_WithIf_ContinuesAtTheLabelWhenTheConditionIsNotZero(string q1, string? q2)
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=" + q1, "JUMP=5 IF={$Q1 == 1}", "VAR:Q2=1", "LABEL=5", "PROGRAM=END"));

        Assert.Equal(q2, vm.Var("Q2"));
    }

    // VM 3.6: JUMP sets pc to a LABEL of the current section backward as well, which makes a loop; it ends when the
    // condition is 0, and the JUMP with the 0 condition does not execute (language 4.9).
    [Fact]
    public void Jump_Backward_LoopsUntilTheConditionIsZero()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "JUMP=1 IF={$Q1 < 3}", "PROGRAM=END"));

        Assert.Equal(
            ["VAR_CHANGE(3): Q1 ? -> 0", "VAR_CHANGE(5): Q1 0 -> 1", "VAR_CHANGE(5): Q1 1 -> 2",
                "VAR_CHANGE(5): Q1 2 -> 3"],
            vm.Events.LinesOf("VAR_CHANGE"));
        Assert.Equal(
            ["JUMP(6): target 1, condition {$Q1 < 3}, depth 0", "JUMP(6): target 1, condition {$Q1 < 3}, depth 0"],
            vm.Events.LinesOf("JUMP"));
    }

    // Language 4.9: the block with IF executes when the expression is not 0; with 0 none of its words takes effect.
    [Theory]
    [InlineData("0", null)]
    [InlineData("2", "5")]
    public void If_Zero_TheBlockDoesNotExecute(string condition, string? q1)
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=5 JUMP=9 IF={" + condition + "}", "LABEL=9", "PROGRAM=END"));

        Assert.Equal(q1, vm.Var("Q1"));
        Assert.Equal(q1 is null ? 0 : 1, vm.Events.Of<FlowEvent>().Count);
    }

    // VM 3.6, 3.9: an unconditional JUMP forward passes over the blocks between, which the pre-pass reports as
    // unreachable.
    [Fact]
    public void Jump_Forward_PassesOverTheBlocksBetween()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "JUMP=9", "VAR:Q2=1", "LABEL=9", "VAR:Q3=1", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal([DiagnosticCodes.UnreachableBlock], vm.Codes());
        Assert.Null(vm.Var("Q2"));
        Assert.Equal("1", vm.Var("Q3"));
    }

    // Language 4.9, VM 3.6: JUMP=END continues at the PROGRAM=END of the current program, which ends it.
    [Fact]
    public void JumpEnd_InTheProgram_ContinuesAtProgramEnd()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:Q1=1", "JUMP=END", "VAR:Q2=1", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Null(vm.Var("Q2"));
        Assert.True(vm.State.Finished);
        Assert.Single(vm.Events.LinesOf("PROGRAM_END"));
    }

    // VM 3.6: JUMP=END from inside a subprogram ends the program from the call, with a WARNING: the blocks of the
    // caller after the CALL do not run, and the calls unwind without SUB_END (virtual machine 7: SUB=END or RETURN).
    [Fact]
    public void JumpEnd_InsideASubprogram_EndsTheProgramWithAWarning()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "CALL=10", "VAR:Q2=1", "PROGRAM=END", "SUB=BEGIN NAME=10", "JUMP=END", "SUB=END"));

        vm.AssertNoErrors();
        Assert.Equal([DiagnosticCodes.JumpEndInSub], vm.Codes());
        Assert.Null(vm.Var("Q2"));
        Assert.Empty(vm.State.Flow.Calls);
        Assert.Empty(vm.Events.LinesOf("SUB_END"));
        Assert.Single(vm.Events.LinesOf("PROGRAM_END"));
        Assert.True(vm.State.Finished);
    }

    // VM 3.6, 7: RETURN pops the call before SUB=END is reached, and SUB_END is raised at the RETURN.
    [Fact]
    public void Return_InsideASubprogram_ReturnsBeforeSubEnd()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "CALL=10", "VAR:Q2={$Q1 + 1}", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "VAR:Q1=1", "RETURN", "VAR:Q1=2", "SUB=END"));

        Assert.Equal("1", vm.Var("Q1"));
        Assert.Equal("2", vm.Var("Q2"));
        Assert.Equal(["SUB_END(8): name 10, caller \"TEST\""], vm.Events.LinesOf("SUB_END"));
    }

    // Language 4.9, VM 3.6: RETURN in a program returns to no caller: a WARNING, treated as JUMP=END.
    [Fact]
    public void Return_InAProgram_IsTreatedAsJumpEndWithAWarning()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:Q1=1", "RETURN", "VAR:Q2=1", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal([DiagnosticCodes.ReturnInProgram, DiagnosticCodes.UnreachableBlock], vm.Codes());
        Assert.Null(vm.Var("Q2"));
        Assert.True(vm.State.Finished);
    }

    // VM 3.6, 3.9: CALL enters the subprogram with the caller's state, exactly as on the control, and the flow goes on
    // after the CALL when it returns; the incremental words of the subprogram work from the caller's position.
    [Fact]
    public void Call_Subprogram_RunsWithTheCallersStateAndReturnsAfterTheCall()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "UNITS=MM", "RAPID X=0 Y=0 Z=0", "CALL=10", "RAPID IX=1", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "RAPID IX=5 IY=2", "SUB=END"));

        Assert.Equal(6m, vm.Position("X").Value);
        Assert.Equal(2m, vm.Position("Y").Value);
        Assert.Single(vm.Events.LinesOf("SUB_BEGIN"));
        Assert.Single(vm.Events.LinesOf("SUB_END"));
        Assert.Empty(vm.State.Flow.Calls);
    }

    // Language 4.9, VM 3.6: CALL with TIMES=n enters the subprogram n times in sequence; TIMES may be an expression,
    // which INTERPRETED mode evaluates.
    [Theory]
    [InlineData("TIMES=3", 3)]
    [InlineData("TIMES={$Q5 + 1}", 3)]
    [InlineData("TIMES=0", 0)]
    public void CallWithTimes_EntersTheSubprogramTimesTimes(string times, int passes)
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "VAR:Q5=2", "CALL=10 " + times, "PROGRAM=END",
            "SUB=BEGIN NAME=10", "VAR:Q1={$Q1 + 1}", "SUB=END"));

        Assert.Equal(passes.ToString(CultureInfo.InvariantCulture), vm.Var("Q1"));
        Assert.Equal(passes, vm.Events.LinesOf("SUB_BEGIN").Count);
    }

    // VM 3.6, 4: CALL pushes the locals V1 to V33 and assigns the ARG words to the callee's locals; SUB=END restores
    // the caller's.
    [Fact]
    public void Call_ArgWords_AreTheCalleesLocalsAndTheCallersComeBack()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:V1=7", "CALL=10 ARG:V1=5", "VAR:Q2={$V1}", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "VAR:Q1={$V1 * 2}", "SUB=END"));

        Assert.Equal("10", vm.Var("Q1"));
        Assert.Equal("7", vm.Var("Q2"));
    }

    // VM 3.6: an ARG from an expression is evaluated with the variables of the caller before the call.
    [Fact]
    public void Call_ArgFromAnExpression_GivesTheCalleeItsValue()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=4", "CALL=10 ARG:V1={$Q1 * 3}", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "VAR:Q2={$V1}", "SUB=END"));

        Assert.Equal("12", vm.Var("Q2"));
        Assert.Contains("VAR_CHANGE(4): V1 ? -> 12", vm.Events.LinesOf("VAR_CHANGE"));
    }

    // VM 3.6, D38: a callee's locals start unassigned apart from the ARG values (wave-1 question #75), so reading one
    // the caller did not pass is the ERROR of an unassigned variable.
    [Fact]
    public void Call_LocalTheCallerDidNotPass_IsUnassignedInTheCallee()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:V2=3", "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "VAR:Q1={$V2}", "SUB=END"));

        Assert.Equal([DiagnosticCodes.UnassignedVariableRead], vm.Codes());
        Assert.True(vm.Result!.Stopped);
    }

    // Language 4.13, VM 3.6: a CALL that names a program instead of a subprogram is an ERROR; programs are entered from
    // the job only.
    [Fact]
    public void Call_OfAProgram_IsAnError()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            CALL=OTHER
            VAR:Q1=1
            PROGRAM=END
            PROGRAM=BEGIN NAME="OTHER"
            PROGRAM=END
            FILE=END
            """;

        InterpretedHarness vm = new InterpretedHarness().Run(text);

        Assert.Equal([DiagnosticCodes.CallOfProgram], vm.Codes());
        Assert.Null(vm.Var("Q1"));
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.6, 5: a missing call target is an ERROR before execution, also for a CALL the flow never reaches.
    [Fact]
    public void Call_SubprogramTheFileDoesNotHave_IsAnErrorBeforeExecution()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "VAR:Q1=0", "CALL=99 IF={$Q1 == 1}", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.CallTargetMissing], vm.Codes());
        Assert.Empty(vm.Events.Events);
    }

    // VM 3.6, 3.9: calls nest up to the configured depth, 8 by default: eight nested calls run.
    [Fact]
    public void CallDepth_EightNestedCalls_Run()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(NestedCalls(8));

        Assert.Equal("1", vm.Var("Q1"));
        Assert.Equal(8, vm.Events.LinesOf("SUB_BEGIN").Count);
    }

    // VM 3.6, 3.9, 5: a ninth nested call is beyond the depth of 8: the ERROR "call depth exceeded", and the
    // subprogram is not entered.
    [Fact]
    public void CallDepth_NinthNestedCall_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(NestedCalls(9));

        Diagnostic error = vm.Single(DiagnosticCodes.CallDepthExceeded);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Contains("call depth exceeded", error.Message, StringComparison.Ordinal);
        Assert.Null(vm.Var("Q1"));
        Assert.Equal(8, vm.Events.LinesOf("SUB_BEGIN").Count);
        Assert.True(vm.Result!.Stopped);
    }

    // Machine-config 7, VM 3.6: the depth comes from [variables] call_depth of the machine configuration.
    [Fact]
    public void CallDepth_FromTheMachineConfiguration_LimitsTheCalls()
    {
        MachineConfig machine = VmMachines.Default() with { Variables = new VariablesConfig { CallDepth = 2 } };

        InterpretedHarness vm = new InterpretedHarness(machine).Run(NestedCalls(3));

        Assert.Equal([DiagnosticCodes.CallDepthExceeded], vm.Codes());
    }

    // Language 4.9, VM 3.6: REPEAT with TIMES repeats the blocks from the label to the REPEAT block TIMES more times.
    [Fact]
    public void Repeat_WithTimes_RepeatsTheBlocksTimesMoreTimes()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "REPEAT=1 TIMES=3", "PROGRAM=END"));

        Assert.Equal("4", vm.Var("Q1"));
        Assert.Equal(4, vm.Events.LinesOf("REPEAT").Count);
        Assert.Empty(vm.State.Flow.Repeats);
    }

    // The reading of the open question in VirtualMachine.FollowRepeat: a REPEAT without TIMES repeats the blocks once.
    [Fact]
    public void Repeat_WithoutTimes_RepeatsTheBlocksOnce()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "REPEAT=1", "PROGRAM=END"));

        Assert.Equal("2", vm.Var("Q1"));
    }

    // VM 3.6: repeats nest; the inner repeat starts anew in every pass of the outer one.
    [Fact]
    public void Repeat_Nested_RunsTheInnerRepeatInEveryPassOfTheOuter()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "VAR:Q2=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "LABEL=2", "VAR:Q2={$Q2 + 1}", "REPEAT=2 TIMES=1",
            "REPEAT=1 TIMES=2", "PROGRAM=END"));

        Assert.Equal("3", vm.Var("Q1"));
        Assert.Equal("6", vm.Var("Q2"));
    }

    // VM 3.6: nested repeats and calls share the configured depth; a REPEAT beyond it is an ERROR.
    [Fact]
    public void Repeat_BeyondTheDepthThatCallsAndRepeatsShare_IsAnError()
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with { CallDepth = 1 };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "LABEL=1", "VAR:Q1=1", "REPEAT=1 TIMES=1", "SUB=END"));

        Assert.Equal([DiagnosticCodes.RepeatDepthExceeded], vm.Codes());
        Assert.True(vm.Result!.Stopped);
    }

    // The reading of the open question in VirtualMachine.EndRepeatsOutside: a JUMP out of the blocks a REPEAT repeats
    // ends the repeat.
    [Fact]
    public void Repeat_LeftByAJump_Ends()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "JUMP=9 IF={$Q1 == 2}", "REPEAT=1 TIMES=5", "LABEL=9",
            "PROGRAM=END"));

        Assert.Equal("2", vm.Var("Q1"));
        Assert.Empty(vm.State.Flow.Repeats);
    }

    // VM 3.6, 5: the block cap stops a program that never ends: the ERROR "possible endless loop" (LABEL=1, JUMP=1).
    [Fact]
    public void BlockCap_LabelAndAJumpToIt_IsAnErrorPossibleEndlessLoop()
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with { BlockCap = 50 };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "LABEL=1", "JUMP=1", "PROGRAM=END"));

        Diagnostic error = vm.Single(DiagnosticCodes.BlockCapExceeded);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Contains("possible endless loop", error.Message, StringComparison.Ordinal);
        Assert.Equal(50, vm.State.Flow.BlocksExecuted);
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.6, machine-config 7: the block cap is 1 000 000 by default.
    [Fact]
    public void BlockCap_Default_StopsAnEndlessLoopAfterAMillionBlocks()
    {
        InterpretedHarness vm = new InterpretedHarness(listen: false).Run(VmHarness.File(
            "LABEL=1", "JUMP=1", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.BlockCapExceeded], vm.Codes());
        Assert.Equal(1_000_000, vm.State.Flow.BlocksExecuted);
    }

    // Machine-config 7, VM 3.6: the block cap comes from [variables] block_cap of the machine configuration.
    [Fact]
    public void BlockCap_FromTheMachineConfiguration_StopsTheLoop()
    {
        MachineConfig machine = VmMachines.Default() with { Variables = new VariablesConfig { BlockCap = 20 } };

        InterpretedHarness vm = new InterpretedHarness(machine).Run(VmHarness.File("LABEL=1", "JUMP=1", "PROGRAM=END"));

        Assert.Equal([DiagnosticCodes.BlockCapExceeded], vm.Codes());
        Assert.Equal(20, vm.State.Flow.BlocksExecuted);
    }

    // D53, VM 3.6: SKIP blocks are executed unless the run option skip_blocks says otherwise.
    [Theory]
    [InlineData(false, "1")]
    [InlineData(true, null)]
    public void Skip_IsExecutedUnlessTheRunOptionSkipsIt(bool skipAll, string? q1)
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with
        {
            SkipBlocks = skipAll ? SkipBlocks.Every : SkipBlocks.None,
        };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "SKIP VAR:Q1=1", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal(q1, vm.Var("Q1"));
    }

    // VM 3.6: a LABEL on a skipped block still counts as a target.
    [Fact]
    public void Skip_LabelOnASkippedBlock_StillCountsAsATarget()
    {
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with { SkipBlocks = SkipBlocks.Every };

        InterpretedHarness vm = new InterpretedHarness(options: options).Run(VmHarness.File(
            "JUMP=5 IF={1}", "VAR:Q2=1", "SKIP LABEL=5 VAR:Q3=1", "VAR:Q4=1", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Null(vm.Var("Q2"));
        Assert.Null(vm.Var("Q3"));
        Assert.Equal("1", vm.Var("Q4"));
    }

    // VM 2.7, 3.6, 7: PROGRAM=END ends the program: the channel is finished, and PROGRAM_END carries the run
    // statistics, every block the loop executed counted every time, the JUMP whose IF is 0 not.
    [Fact]
    public void ProgramEnd_FinishesTheChannelWithTheBlocksTheRunExecuted()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:Q1=0", "LABEL=1", "VAR:Q1={$Q1 + 1}", "JUMP=1 IF={$Q1 < 3}", "PROGRAM=END"));

        Assert.True(vm.State.Finished);
        Assert.Equal(11, vm.State.Flow.BlocksExecuted);
        ProgramEvent end = Assert.Single(vm.Events.Of<ProgramEvent>(), e => e.Phase == EventPhase.End);
        Assert.Equal(11, end.Blocks);
    }

    // A program that calls subprogram 1, each subprogram the next one, and the last sets Q1: that many nested calls.
    private static string NestedCalls(int depth)
    {
        var lines = new List<string> { "CALL=1", "PROGRAM=END" };
        for (int level = 1; level <= depth; level++)
        {
            lines.Add("SUB=BEGIN NAME=" + level.ToString(CultureInfo.InvariantCulture));
            lines.Add(level < depth ? "CALL=" + (level + 1).ToString(CultureInfo.InvariantCulture) : "VAR:Q1=1");
            lines.Add("SUB=END");
        }

        return VmHarness.File(lines.ToArray());
    }
}
