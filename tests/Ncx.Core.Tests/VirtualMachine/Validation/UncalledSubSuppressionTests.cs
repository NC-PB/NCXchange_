using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The suppressions of D99: inside a subprogram that no program of the file calls, the rules whose state belongs to a
/// caller are suppressed (LINE without feed, motion before UNITS, the tool and offset rules, the cycle rules, IX from
/// an unknown position, spindle OFF before a LINE), and at a CALL the same blocks raise them (VM 3.9, 5). Each case
/// is a rule of the table that names its suppression.
/// </summary>
public sealed class UncalledSubSuppressionTests
{
    /// <summary>
    /// The caller rules, each with the blocks of a subprogram that break it when a caller of the default machine enters
    /// it without UNITS, feed, tool or spindle.
    /// </summary>
    public static TheoryData<string, string[]> CallerRules()
    {
        return new TheoryData<string, string[]>
        {
            { DiagnosticCodes.MotionBeforeUnits, ["RAPID X=0"] },
            { DiagnosticCodes.LineWithoutFeed, ["UNITS=MM SPINDLE=CW", "LINE X=1"] },
            { DiagnosticCodes.IncrementalFromUnknownPosition, ["UNITS=MM F=1 SPINDLE=CW", "LINE IX=1"] },
            { DiagnosticCodes.ArcStartUnknownInThePlane, ["UNITS=MM", "ARC=CW X=1 Y=0 R=1"] },
            { DiagnosticCodes.ToolAlreadyInSpindle, ["TOOL=4", "PRELOAD=4"] },
            { DiagnosticCodes.NothingPreloaded, ["TOOL"] },
            { DiagnosticCodes.PreloadMismatch, ["PRELOAD=5", "TOOL=4"] },
            { DiagnosticCodes.ToolChangeWhileCycleActive, ["CYCLE=DRILL CLEARANCE=2 DEPTH=-5", "TOOL=4"] },
            { DiagnosticCodes.ToolChangeWithCompensationOn, ["COMP=LEFT", "TOOL=4"] },
            { DiagnosticCodes.OffsetFormsMixed, ["OFFSET=1", "OFFSET:LEN=1"] },
            { DiagnosticCodes.SpindleOffBeforeLine, ["UNITS=MM F=1", "LINE X=1"] },
            { DiagnosticCodes.CycleCallWithoutCycle, ["UNITS=MM", "CYCLE_CALL"] },
            { DiagnosticCodes.CycleCallWithoutDepthOrClearance, ["UNITS=MM", "CYCLE=DRILL CLEARANCE=2", "CYCLE_CALL"] },
        };
    }

    // VM 3.9, 5, D99: inside a subprogram that no program of the file calls the rule is suppressed, and the table names
    // the suppression.
    [Theory]
    [MemberData(nameof(CallerRules))]
    public void CallerRule_InASubprogramNothingCalls_IsSuppressed(string code, string[] blocks)
    {
        VmHarness vm = VmHarness.Run(Subprogram(called: false, blocks), VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.True(DiagnosticTable.Find(code)!.SuppressedInUncalledSub);
    }

    // VM 3.9, 5, D99: at a CALL the subprogram runs with the caller's state, and the rule applies.
    [Theory]
    [MemberData(nameof(CallerRules))]
    public void CallerRule_InACalledSubprogram_Applies(string code, string[] blocks)
    {
        VmHarness vm = VmHarness.Run(Subprogram(called: true, blocks), VmMachines.Default());

        Assert.Contains(code, vm.Codes());
    }

    // A program that calls the subprogram 1 or not, and the subprogram with the blocks.
    private static string Subprogram(bool called, string[] blocks)
    {
        var lines = new List<string>();
        if (called)
        {
            lines.Add("CALL=1");
        }

        lines.Add("PROGRAM=END");
        lines.Add("SUB=BEGIN NAME=1");
        lines.AddRange(blocks);
        lines.Add("SUB=END");
        return VmHarness.File([.. lines]);
    }
}
