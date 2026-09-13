using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The examples with a flow, run in INTERPRETED mode without a machine file (virtual machine 1, 3.6; D103): the loop of
/// PATTERN_LOOP, the four calls of INCREMENTAL_SUB, and a WHILE loop as the Fanuc reader lowers it (implementation 14,
/// P4-01).
/// </summary>
public sealed class InterpretedExampleTests
{
    // PATTERN_LOOP note 1, implementation 14 P4-01: INTERPRETED mode runs the loop five times, and the cycle is called at
    // X = 10, 30, 50, 70, 90, all at Y = 10; every expression is evaluated, so nothing is left unresolved (VM 5).
    [Fact]
    public void PatternLoop_Interpreted_CallsTheCycleFiveTimesAtX10To90()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(Fixture.ReadText("PATTERN_LOOP.ncx"));

        var callPoints = new List<string>();
        foreach (CycleCallEvent call in vm.Events.Of<CycleCallEvent>())
        {
            callPoints.Add(Coordinate(call.At["X"]) + "/" + Coordinate(call.At["Y"]));
        }

        Assert.Equal(["10/10", "30/10", "50/10", "70/10", "90/10"], callPoints);
        vm.AssertNoDiagnostics();
    }

    // PATTERN_LOOP note 1: every pass sets Q1 and Q3, Q1 to the next call position and Q3 to the passes done; the
    // condition ends the loop after the fifth pass.
    [Fact]
    public void PatternLoop_Interpreted_SetsQ1AndQ3InEveryPass()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(Fixture.ReadText("PATTERN_LOOP.ncx"));

        Assert.Equal(["10", "30", "50", "70", "90", "110"], ValuesOf(vm, "Q1"));
        Assert.Equal(["0", "1", "2", "3", "4", "5"], ValuesOf(vm, "Q3"));
        Assert.Equal(4, vm.Events.LinesOf("JUMP").Count);
        Assert.True(vm.State.Finished);
    }

    // INCREMENTAL_SUB, implementation 14 P4-01: CALL=100 TIMES=4 enters the subprogram four times.
    [Fact]
    public void IncrementalSub_Interpreted_CallsTheSubprogramFourTimes()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(Fixture.ReadText("INCREMENTAL_SUB.ncx"));

        vm.AssertNoErrors();
        Assert.Equal(4, vm.Events.LinesOf("SUB_BEGIN").Count);
        Assert.Equal(4, vm.Events.LinesOf("SUB_END").Count);
        Assert.Equal(["CALL(13): target 100, depth 0"], vm.Events.LinesOf("CALL"));
    }

    // INCREMENTAL_SUB, implementation 14 P4-01: after the four passes X = 0 (four IX=30 and IX=-30 pairs), Y = 60 (four
    // IY=15), and RAPID Z=50 on line 15 leaves Z = 50; then the flow jumps to the parking section, whose HOME finds no
    // reference point without a machine file (D100), back to LABEL=22 and to the end.
    [Fact]
    public void IncrementalSub_Interpreted_EndsAtTheExpectedPosition()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(Fixture.ReadText("INCREMENTAL_SUB.ncx"));

        vm.AssertNoErrors();
        MotionEvent retract = Assert.Single(vm.Events.Of<MotionEvent>(), motion => motion.Block.Line == 15);
        IReadOnlyDictionary<string, AxisPosition> position = retract.After.Motion.Position;
        Assert.Equal(["0", "60", "50"], [Coordinate(position["X"]), Coordinate(position["Y"]), Coordinate(position["Z"])]);
        Assert.Equal(
            ["JUMP(18): target 300, depth 0", "JUMP(26): target 22, depth 0", "JUMP(20): target END, depth 0"],
            vm.Events.LinesOf("JUMP"));
        Assert.Equal(
            [DiagnosticCodes.HomeWithoutReferencePoint, DiagnosticCodes.HomeWithoutReferencePoint,
                DiagnosticCodes.HomeWithoutReferencePoint],
            vm.Codes());
        Assert.False(vm.Position("X").Known);
        Assert.True(vm.State.Finished);
    }

    // Controller-mapping 1 (structured loops): the Fanuc reader lowers WHILE [#1 LT 3] DO1 ... END1 to a LABEL, a JUMP
    // out with IF and a JUMP back; INTERPRETED mode runs the loop as the control does. Reading the Fanuc source itself
    // is the Fanuc reader's (P3-02).
    [Fact]
    public void WhileLoop_AsTheFanucReaderLowersIt_RunsUntilItsConditionFails()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:V1=0", "VAR:V2=0", "LABEL=1", "JUMP=2 IF={NOT ($V1 < 3)}", "VAR:V1={$V1 + 1}", "VAR:V2={$V2 + 10}",
            "JUMP=1", "LABEL=2", "PROGRAM=END"));

        Assert.Equal("3", vm.Var("V1"));
        Assert.Equal("30", vm.Var("V2"));
    }

    // The values a variable took, in order, from its VAR_CHANGE events.
    private static List<string> ValuesOf(InterpretedHarness vm, string name)
    {
        var values = new List<string>();
        foreach (VarChangeEvent change in vm.Events.Of<VarChangeEvent>())
        {
            if (change.Name == name)
            {
                values.Add(change.NewValue.ToString());
            }
        }

        return values;
    }

    private static string Coordinate(AxisPosition position)
    {
        Assert.True(position.Known);
        return position.Value.ToString(CultureInfo.InvariantCulture);
    }
}
