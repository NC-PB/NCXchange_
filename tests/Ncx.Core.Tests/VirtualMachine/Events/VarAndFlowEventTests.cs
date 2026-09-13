using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// VAR_CHANGE at VAR and ARG, and the flow events JUMP, CALL, RETURN and REPEAT with target, condition and depth
/// (virtual machine 1, 3.6, 7; D99).
/// </summary>
public sealed class VarAndFlowEventTests
{
    // VM 7, row VAR_CHANGE: name, old and new of a VAR; UNKNOWN from an expression in STATIC mode (VM 1).
    [Fact]
    public void VarChange_Var_CarriesTheNameTheOldAndTheNewValue()
    {
        FakeListener listener = EventRuns.Execute("VAR:Q1=10", "VAR:Q1={$Q1 + 1}");

        Assert.Equal(["VAR_CHANGE(1): Q1 ? -> 10", "VAR_CHANGE(2): Q1 10 -> UNKNOWN"], listener.LinesOf("VAR_CHANGE"));
        VarChangeEvent first = listener.Of<VarChangeEvent>()[0];
        Assert.Equal("Q1", first.Name);
        Assert.Null(first.OldValue);
        Assert.Equal("10", first.NewValue.ToString());
    }

    // VM 7, row VAR_CHANGE; VM 3.6: an ARG of a CALL gives the callee's local its value, raised at the CALL block
    // before the CALL itself.
    [Fact]
    public void VarChange_ArgOfACall_CarriesTheValueTheCalleeGets()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "CALL=10 ARG:V1=5", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        Assert.Equal(["VAR_CHANGE(3): V1 ? -> 5"], listener.LinesOf("VAR_CHANGE"));
        Assert.Equal(
            ["FILE_BEGIN", "PROGRAM_BEGIN", "VAR_CHANGE", "CALL", "SUB_BEGIN", "SUB_END", "PROGRAM_END", "FILE_END"],
            listener.Kinds());
    }

    // VM 7, row JUMP; VM 1: STATIC mode records a JUMP with its target and the condition as written.
    [Fact]
    public void Jump_WithIf_CarriesTargetConditionAndDepth()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "LABEL=1", "VAR:Q1=1", "JUMP=1 IF={$Q1 < 3}", "PROGRAM=END"));

        FlowEvent jump = Assert.Single(listener.Of<FlowEvent>());
        Assert.Equal(FlowKind.Jump, jump.Flow);
        Assert.Equal("1", jump.Target);
        Assert.IsType<ExprValue>(jump.Condition);
        Assert.Equal(0, jump.Depth);
        Assert.Equal("JUMP(5): target 1, condition {$Q1 < 3}, depth 0", jump.ToString());
    }

    // VM 7, row CALL; D99: the depth of a CALL is that of the block it stands in, 1 inside a called subprogram.
    [Fact]
    public void Call_InsideACalledSubprogram_HasDepthOne()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "CALL=20", "SUB=END", "SUB=BEGIN NAME=20", "SUB=END"));

        Assert.Equal(["CALL(3): target 10, depth 0", "CALL(6): target 20, depth 1"], listener.LinesOf("CALL"));
    }

    // VM 1, 3.9, D99: a CALL of an external program is recorded with its file name and not followed.
    [Fact]
    public void Call_ExternalProgram_IsRecordedAndNotEntered()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("CALL=\"O9010\"", "PROGRAM=END"));

        Assert.Equal(["CALL(3): target O9010, depth 0"], listener.LinesOf("CALL"));
        Assert.Empty(listener.Of<SubEvent>());
    }

    // VM 7, row RETURN; VM 1: STATIC mode records a RETURN, and the walk leaves the subprogram at its SUB=END.
    [Fact]
    public void Return_InsideASubprogram_IsRecordedAndTheSubprogramEndsAtSubEnd()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "RETURN", "SUB=END"));

        Assert.Equal(["RETURN(6): depth 1"], listener.LinesOf("RETURN"));
        Assert.Equal(["SUB_END(7): name 10, caller \"TEST\""], listener.LinesOf("SUB_END"));
    }

    // VM 7, row REPEAT; VM 1: STATIC mode records a REPEAT with its label.
    [Fact]
    public void Repeat_WithTimes_CarriesTheLabel()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("LABEL=1", "VAR:Q1=1", "REPEAT=1 TIMES=2", "PROGRAM=END"));

        FlowEvent repeat = Assert.Single(listener.Of<FlowEvent>());
        Assert.Equal(FlowKind.Repeat, repeat.Flow);
        Assert.Equal("REPEAT(5): target 1, depth 0", repeat.ToString());
    }
}
