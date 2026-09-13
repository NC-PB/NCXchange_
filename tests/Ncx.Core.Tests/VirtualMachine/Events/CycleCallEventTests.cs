using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// CYCLE_CALL at every call with the cycle, its parameters and the call point, and the individual MOTION events of the
/// sequence under ExpandCycles (virtual machine 3.3, 7; D37, D94).
/// </summary>
public sealed class CycleCallEventTests
{
    // VM 7, row CYCLE_CALL: the cycle name and parameters and the call point; without ExpandCycles no MOTION.
    [Fact]
    public void CycleCall_EveryCall_CarriesTheCycleItsParametersAndTheCallPoint()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_F=100", "CYCLE_CALL X=10 Y=10");

        CycleCallEvent call = Assert.Single(listener.Of<CycleCallEvent>());
        Assert.Equal("DRILL", call.Cycle);
        Assert.Null(call.Controller);
        Assert.Equal(["CLEARANCE=5", "DEPTH=-10", "CYCLE_F=100"], Canonical(call.Parameters));
        Assert.Equal(new AxisPosition(10m, PositionFrame.Workpiece, Known: true), call.At["X"]);
        Assert.Single(listener.Of<MotionEvent>());
        Assert.Equal(
            "CYCLE_CALL(4): cycle DRILL, at X=10 Y=10 Z=5, parameters CLEARANCE=5 DEPTH=-10 CYCLE_F=100",
            call.ToString());
    }

    // VM 3.3, D37: under ExpandCycles the CYCLE_CALL is followed by the MOTION events of its sequence, each with its
    // own end points and length.
    [Fact]
    public void CycleCall_ExpandCycles_IsFollowedByTheMotionsOfTheSequence()
    {
        FakeListener listener = EventRuns.Execute(VmMachines.Default(), new VmOptions { ExpandCycles = true },
            "UNITS=MM", "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_F=100", "CYCLE_CALL X=10 Y=10");

        var callLines = new List<string>();
        foreach (VmEvent vmEvent in listener.Events)
        {
            if (vmEvent.Block.Line == 4 && vmEvent.Kind is "CYCLE_CALL" or "MOTION")
            {
                callLines.Add(vmEvent.ToString());
            }
        }

        Assert.Equal(
            [
                "CYCLE_CALL(4): cycle DRILL, at X=10 Y=10 Z=5, parameters CLEARANCE=5 DEPTH=-10 CYCLE_F=100",
                "MOTION(4): RAPID X 0 -> 10, Y 0 -> 10; comp OFF, frame WORKPIECE, length 14.142",
                "MOTION(4): RAPID Z 50 -> 5; comp OFF, frame WORKPIECE, length 45",
                "MOTION(4): LINE Z 5 -> -10; feed 100 PER_MIN, comp OFF, frame WORKPIECE, length 15",
                "MOTION(4): RAPID Z -10 -> 5; comp OFF, frame WORKPIECE, length 15",
            ],
            callLines);
    }

    // VM 3.3, D94: the CycleCallEvent of a CYCLE:controller=n cycle carries the native parameters as words.
    [Fact]
    public void CycleCall_NativeCycle_CarriesTheNativeParametersAsWords()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "RAPID X=0 Y=0 Z=50", "CYCLE:HEIDENHAIN=251 Q200=2 Q201=-10", "CYCLE_CALL X=10 Y=10");

        CycleCallEvent call = Assert.Single(listener.Of<CycleCallEvent>());
        Assert.Equal("251", call.Cycle);
        Assert.Equal("HEIDENHAIN", call.Controller);
        Assert.Equal(["Q200=2", "Q201=-10"], Canonical(call.Parameters));
    }

    private static List<string> Canonical(IReadOnlyList<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return texts;
    }
}
