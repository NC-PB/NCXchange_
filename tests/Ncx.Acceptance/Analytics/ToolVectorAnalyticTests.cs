using Ncx.Acceptance.Cli;
using Ncx.Analytics;
using Ncx.Analytics.ToolVectors;
using Ncx.Config;
using Ncx.Core.Machine;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The tool vector change of virtual machine 8 with the convention of implementation 14, P4-03, on small programs, the
/// angles computed by hand: the tool vector from TX TY TZ when the program gives them, else from the rotary axes of
/// [[axis]] applied to the normal of the WORKPLANE, and the angle between the vectors at the start and at the end of
/// each motion. The rotary axes of the test machines start unknown (D100); a program starts on line 3
/// (CliHarness.OneProgram).
/// </summary>
public sealed class ToolVectorAnalyticTests
{
    private const int Precision = 3;

    // The angle whose cosine is 0.75, 41.41 degrees: a quarter turn about Z swings a tool vector that stands 30 degrees
    // off Z, from 0.5 0 0.866 to 0 -0.5 0.866 (their dot product is 0.75).
    private static readonly double s_quarterTurnUnderThirty = Math.Acos(0.75) * 180 / Math.PI;

    // P4-03 test first: two LINE blocks under TCPM=ON with vectors 30 degrees apart. TX TY TZ turn from 0 0 1 to
    // 0 0.5 0.866 (the sine and the cosine of 30 degrees): 30 degrees on line 7. The first LINE (line 6) starts from
    // the rotary axes, which are unknown, so its change is not known; the RAPID of line 5 turns no rotary axis and
    // gives no vector, so its tool vector stays as it was, a change of 0.
    [Fact]
    public void ToolVectorForm_TwoLinesThirtyDegreesApart_ChangeByThirtyDegrees()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisTable(), "UNITS=MM", "TCPM=ON", "RAPID X=0 Y=0 Z=0",
            "LINE X=10 F=1000 TX=0 TY=0 TZ=1", "LINE X=20 TX=0 TY=0.5 TZ=0.8660254");

        Assert.Equal(2, vectors.Statistics.All.Count);
        Assert.Equal(0, vectors.Statistics.All.Minimum, Precision);
        Assert.Equal(30, vectors.Statistics.All.Maximum, Precision);
        Assert.Equal(1, vectors.StartUnknown);
        WorstMotion largest = Assert.Single(vectors.Largest);
        Assert.Equal(7, largest.Line);
        Assert.Equal(30, largest.Value, Precision);
        Assert.Equal("0 0 1", largest.Start);
        Assert.Equal("0 0.5 0.866", largest.End);
    }

    // P4-03 test first, the same with A and C words on an A/C table: A turns the tool axis 0 0 1 about X, C about Z,
    // as table axes with the opposite sign; from A=0 C=45 to A=30 C=45 (line 6) the vector turns by 30 degrees. The
    // RAPID of line 4 starts from unknown rotary axes; the LINE of line 5 turns no rotary axis, a change of 0.
    [Fact]
    public void RotaryAxes_AcTableThirtyDegreesApart_ChangeByThirtyDegrees()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisTable(), "UNITS=MM", "RAPID X=0 Y=0 Z=0 A=0 C=45",
            "LINE X=10 F=1000", "LINE X=20 A=30");

        Assert.Equal(2, vectors.Statistics.All.Count);
        Assert.Equal(30, vectors.Statistics.All.Maximum, Precision);
        Assert.Equal(1, vectors.StartUnknown);
        WorstMotion largest = Assert.Single(vectors.Largest);
        Assert.Equal(6, largest.Line);
        Assert.Equal(30, largest.Value, Precision);
    }

    // The convention of P4-03 applies the rotary axes in the order of [[axis]], A before C: at A=0 a turn of C (line 5)
    // turns the tool axis about itself, a change of 0; A=30 (line 6) tilts it by 30 degrees to 0.5 0 0.866 at C=90; the
    // quarter turn of C to 180 (line 7) swings it about Z to 0 -0.5 0.866, by the angle whose cosine is 0.75. Applied
    // the other way round, C first, the turns of C would change nothing.
    [Fact]
    public void RotaryAxes_TurnOfCBeforeAndAfterTheTiltOfA_ChangesOnlyUnderTheTilt()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisTable(), "UNITS=MM", "RAPID X=0 Y=0 Z=0 A=0 C=0",
            "LINE C=90 F=1000", "LINE A=30", "LINE C=180");

        Assert.Equal(3, vectors.Statistics.All.Count);
        Assert.Equal(2, vectors.Largest.Count);
        Assert.Equal(7, vectors.Largest[0].Line);
        Assert.Equal(s_quarterTurnUnderThirty, vectors.Largest[0].Value, Precision);
        Assert.Equal("0.5 0 0.866", vectors.Largest[0].Start);
        Assert.Equal("0 -0.5 0.866", vectors.Largest[0].End);
        Assert.Equal(6, vectors.Largest[1].Line);
        Assert.Equal(30, vectors.Largest[1].Value, Precision);
    }

    // P4-03: a head axis turns the tool (its owner is the tool spindle): B=30 turns 0 0 1 about Y to 0.5 0 0.866, 30
    // degrees (line 5); the C table then turns the workpiece under the tilted tool, and a quarter turn swings the
    // vector about Z to 0 -0.5 0.866 (line 6).
    [Fact]
    public void RotaryAxes_BHeadAndCTable_TurnTheToolAndTheWorkpiece()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisHeadTable(), "UNITS=MM",
            "RAPID X=0 Y=0 Z=0 B=0 C=0", "LINE B=30 F=1000", "LINE C=90");

        Assert.Equal(2, vectors.Largest.Count);
        Assert.Equal(6, vectors.Largest[0].Line);
        Assert.Equal(s_quarterTurnUnderThirty, vectors.Largest[0].Value, Precision);
        Assert.Equal("0 -0.5 0.866", vectors.Largest[0].End);
        Assert.Equal(5, vectors.Largest[1].Line);
        Assert.Equal(30, vectors.Largest[1].Value, Precision);
        Assert.Equal("0.5 0 0.866", vectors.Largest[1].End);
    }

    // P4-03: the tool axis is the normal of the WORKPLANE. Under WORKPLANE=ZX it is Y, which the turn of the B head
    // about Y (line 6) leaves where it is, while under XY the same turn changes it by its angle (the test above).
    [Fact]
    public void ToolAxis_WorkplaneZx_IsYAndATurnAboutYLeavesIt()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisHeadTable(), "UNITS=MM", "WORKPLANE=ZX",
            "RAPID X=0 Y=0 Z=0 B=0 C=0", "LINE B=30 F=1000");

        Assert.Equal(1, vectors.Statistics.All.Count);
        Assert.Equal(0, vectors.Statistics.All.Maximum, Precision);
        Assert.Empty(vectors.Largest);
    }

    // P4-03, the motions whose tool vector change is not known: line 4 turns C from its unknown start, line 5 moves A
    // in the machine frame from its unknown start, line 6 moves A from the MACHINE frame into the workpiece frame (VM
    // 3.4, D35). Line 7 turns C at A=30 by the angle whose cosine is 0.75; line 9 turns from that vector, 0.5 0 0.866,
    // to TX TY TZ 0 0 1, 30 degrees, and leaves the rotary axes unknown (VM 3.1, D81), so the A word of line 10 ends
    // with C unknown.
    [Fact]
    public void Skipped_EveryReason_IsCountedWithIt()
    {
        ToolVectorAnalytic vectors = Run(AnalyticMachines.FiveAxisTable(), "UNITS=MM", "RAPID X=0 Y=0 Z=0 C=0",
            "RAPID A=0 FRAME=MACHINE", "RAPID A=30", "LINE C=90 F=1000", "TCPM=ON", "LINE X=10 TX=0 TY=0 TZ=1",
            "LINE X=20 A=10");

        Assert.Equal(2, vectors.Statistics.All.Count);
        Assert.Equal(6, vectors.Motions);
        Assert.Equal(2, vectors.StartUnknown);
        Assert.Equal(1, vectors.BetweenFrames);
        Assert.Equal(1, vectors.EndUnknown);
        Assert.Equal(0, vectors.InTransformedPlane);
        Assert.Equal(s_quarterTurnUnderThirty, vectors.Largest[0].Value, Precision);
        Assert.Equal(30, vectors.Largest[1].Value, Precision);
        Assert.Equal("0.5 0 0.866", vectors.Largest[1].Start);
    }

    // VM 3.1, D102: under POLAR=ON the word of C is a length in the face plane, not an angle. On the default machine,
    // whose C is the axis of the work spindle MAIN that holds the workpiece (D103), line 5 turns C from its unknown
    // start, line 7 carries C from the workpiece frame into the polar frame, and line 8 moves C as a length of the
    // polar plane: none of the three has a known change.
    [Fact]
    public void Skipped_PolarPlane_CIsALengthAndNoAngle()
    {
        ToolVectorAnalytic vectors = Run(DefaultMachine.Create(), "UNITS=MM", "SPINDLE_MODE:MAIN=AXIS",
            "RAPID X=20 Z=0 C=0", "POLAR=ON", "LINE X=10 C=5 F=100", "LINE X=10 C=10", "POLAR=OFF");

        Assert.Equal(0, vectors.Statistics.All.Count);
        Assert.Equal(1, vectors.StartUnknown);
        Assert.Equal(1, vectors.BetweenFrames);
        Assert.Equal(1, vectors.InTransformedPlane);
    }

    // VM 8, D67: over line 7 alone only the quarter turn of C on that line is measured.
    [Fact]
    public void BlockRange_OneLine_MeasuresOnlyTheMotionOnIt()
    {
        MachineConfig machine = AnalyticMachines.FiveAxisTable();
        var vectors = new ToolVectorAnalytic(AnalyticRuns.Options(machine, new BlockRange { From = 7, To = 7 }));
        AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0 A=0 C=0", "LINE C=90 F=1000",
            "LINE A=30", "LINE C=180"), machine, vectors);

        Assert.Equal(1, vectors.Motions);
        Assert.Equal(s_quarterTurnUnderThirty, vectors.Statistics.All.Maximum, Precision);
    }

    // P4-03: a histogram with configurable bins. With the bounds 1 and 35 degrees the change of 0 falls into 0 to 1,
    // the 30 degrees into 1 to 35 and the 41.41 degrees into 35 and more.
    [Fact]
    public void Histogram_BoundsGiven_CountsTheChangesIntoThoseBins()
    {
        MachineConfig machine = AnalyticMachines.FiveAxisTable();
        var vectors = new ToolVectorAnalytic(AnalyticRuns.Options(machine), [1m, 35m]);
        AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0 A=0 C=0", "LINE C=90 F=1000",
            "LINE A=30", "LINE C=180"), machine, vectors);

        Assert.Equal(1, vectors.Histogram.CountOf(0));
        Assert.Equal(1, vectors.Histogram.CountOf(1));
        Assert.Equal(1, vectors.Histogram.CountOf(2));
    }

    // The report documents the convention in its header until the kinematics module exists (P4-03), lists the rotary
    // axes of the machine in the order it applies them, the largest changes with their lines and vectors, and the
    // motions skipped and why; here in CSV (VM 8).
    [Fact]
    public void Report_AcTable_DocumentsTheConventionAndListsTheAxesAndTheLargestChanges()
    {
        MachineConfig machine = AnalyticMachines.FiveAxisTable();
        var vectors = new ToolVectorAnalytic(AnalyticRuns.Options(machine) with { Format = TableFormat.Csv });
        string report = AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0 A=0 C=0",
            "LINE C=90 F=1000", "LINE A=30", "LINE C=180"), machine, vectors);

        Assert.StartsWith("\"Tool vector change: test.ncx, the whole file, on test.toml, INTERPRETED run\"\n", report,
            StringComparison.Ordinal);
        Assert.Contains("the tool axis is the normal of the WORKPLANE; a rotary axis named A, B or C turns it about "
            + "the machine X, Y or Z axis, in the order of [[axis]]", report, StringComparison.Ordinal);
        Assert.Contains("\naxis,id,about,turns\nA,A1,X,the workpiece of TABLE1\nC,C1,Z,the workpiece of TABLE1\n",
            report, StringComparison.Ordinal);
        Assert.Contains("\nline,verb,change (degrees),from,to\n7,LINE,41.41,0.5 0 0.866,0 -0.5 0.866\n"
            + "6,LINE,30,0 0 1,0.5 0 0.866\n", report, StringComparison.Ordinal);
        Assert.Contains("\"Skipped: 1 of 4 motions: 1 with the tool vector unknown at the start, 0 unknown at the end, "
            + "0 with a rotary axis from one frame into another, 0 with a rotary axis that is a length of the polar or "
            + "cylinder plane (virtual machine 3.1, 3.4, 8).\"\n", report, StringComparison.Ordinal);
    }

    // A machine without a rotary axis named A, B or C (the three mills of machines/) changes the tool vector only by
    // TX TY TZ: the report says so, and a three-axis program changes nothing.
    [Fact]
    public void Report_ThreeAxisMill_SaysOnlyTxTyTzChangeTheToolVector()
    {
        MachineConfig machine = AnalyticMachines.Mill(dynamics: null);
        var vectors = new ToolVectorAnalytic(AnalyticRuns.Options(machine));
        string report = AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=10 F=1000"),
            machine, vectors);

        Assert.Contains("\nRotary axes: none named A, B or C in [[axis]]; only TX TY TZ change the tool vector, "
            + "which is otherwise the normal of the WORKPLANE.\n", report, StringComparison.Ordinal);
        Assert.Contains("\nNo motion in the range changes the tool vector.\n", report, StringComparison.Ordinal);
        Assert.Equal(2, vectors.Statistics.All.Count);
        Assert.Equal(0, vectors.Statistics.All.Maximum, Precision);
    }

    private static ToolVectorAnalytic Run(MachineConfig machine, params string[] blocks)
    {
        var vectors = new ToolVectorAnalytic(AnalyticRuns.Options(machine));
        AnalyticRuns.Run(CliHarness.OneProgram(blocks), machine, vectors);
        return vectors;
    }
}
