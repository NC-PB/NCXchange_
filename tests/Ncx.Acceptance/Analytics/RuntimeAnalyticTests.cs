using System.Globalization;
using Ncx.Acceptance.Cli;
using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Config;
using Ncx.Core.Machine;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The runtime estimate of virtual machine 8 and D64 on small programs, the times computed by hand: the trapezoidal
/// profile per motion from max_feed, acceleration and rapid of the moving axes, block_time, path_mode with
/// corner_speed, the feed per revolution and CSS, spindle accel_time, dwells and cycles, the fallback without
/// [dynamics], the totals and the block range (implementation 14, P4-02). Every program starts with a RAPID from the
/// unknown start position, which has no length and no time (D100), so the block under test starts from rest.
/// </summary>
public sealed class RuntimeAnalyticTests
{
    private const int Precision = 9;

    // P4-02 test first, the profile on one block: LINE X=100 at F=6000 (100 mm/s) with 1000 mm/s^2, exact stop: 0.1 s
    // and 5 mm to reach the feed, 0.1 s and 5 mm to stop, 90 mm at 100 mm/s; 1.1 s.
    [Fact]
    public void TrapezoidalProfile_OneLineExactStop_TakesTheTimeComputedByHand()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=6000");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: a block too short to reach its feed accelerates and decelerates at once: LINE X=4 at 100 mm/s with
    // 1000 mm/s^2 reaches sqrt(1000 * 4) mm/s, 2 * sqrt(4000) / 1000 s.
    [Fact]
    public void TrapezoidalProfile_LineTooShortForItsFeed_AcceleratesAndBrakesAtOnce()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=4 F=6000");

        Assert.Equal(2 * Math.Sqrt(4000) / 1000, runtime.TotalSeconds, Precision);
    }

    // P4-02 test first, the block-time floor: a 0.01 mm segment at 100 mm/s takes 0.0001 s, and a block can never be
    // shorter than the control's block_time, 0.001 s (VM 8). Without acceleration in the machine file the segment moves
    // at its feed from start to end.
    [Fact]
    public void BlockTime_SegmentOf0_01mm_TakesTheBlockTime()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, acceleration: null),
            "LINE X=0.01 F=6000");

        Assert.Equal(0.001, runtime.TotalSeconds, Precision);
    }

    // VM 8: the block time "is what makes thousands of short segments in 3D and 5-axis programs slow": a hundred
    // segments of 0.01 mm take a hundred block times, 0.1 s, ten times their travel time at the feed.
    [Fact]
    public void BlockTime_HundredSegmentsOf0_01mm_TakeAHundredBlockTimes()
    {
        var blocks = new List<string>();
        for (int segment = 1; segment <= 100; segment++)
        {
            string x = (segment * 0.01m).ToString(CultureInfo.InvariantCulture);
            blocks.Add(segment == 1 ? $"LINE X={x} F=6000" : $"LINE X={x}");
        }

        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, acceleration: null),
            blocks.ToArray());

        Assert.Equal(0.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: path_mode = "exact_stop" brakes to zero in every block: two lines of 100 mm take 1.1 s each.
    [Fact]
    public void PathModeExactStop_TwoLinesInARow_BrakeToZeroInTheCorner()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=6000",
            "LINE X=200");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: "continuous" carries the speed through corners up to corner_speed, 3000 mm/min (50 mm/s): each line
    // accelerates from or brakes to 50 mm/s in 0.05 s over 3.75 mm, and to or from rest in 0.1 s over 5 mm, and travels
    // the other 91.25 mm at 100 mm/s; 1.0625 s each, 2.125 s for the two.
    [Fact]
    public void PathModeContinuous_TwoLinesInARow_CarryTheCornerSpeedThroughTheCorner()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "LINE X=100 F=6000",
            "LINE X=200");

        Assert.Equal(2.125, runtime.TotalSeconds, Precision);
    }

    // VM 8: the commanded feed is limited by the max_feed of every axis that moves: F=12000 on X with max_feed 6000
    // moves at 6000, 1.1 s for 100 mm.
    [Fact]
    public void MaxFeed_FeedAboveTheMaxFeedOfTheAxis_MovesAtTheMaxFeed()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=12000");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: the block accelerates with the acceleration of the axes that move: Y alone with 500 mm/s^2 takes 0.2 s and
    // 10 mm to reach 100 mm/s and as much to stop, 80 mm at 100 mm/s; 1.2 s.
    [Fact]
    public void Acceleration_OnlyYMoves_AcceleratesWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE Y=100 F=6000");

        Assert.Equal(1.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: with X (1000 mm/s^2) and Y (500 mm/s^2) moving, the smallest acceleration of the moving axes applies:
    // 100 mm to X=60 Y=80 take 1.2 s as Y alone does.
    [Fact]
    public void Acceleration_XAndYMove_AcceleratesWithTheSmallestOfTheMovingAxes()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=60 Y=80 F=6000");

        Assert.Equal(1.2, runtime.TotalSeconds, Precision);
    }

    // VM 8 with VM 3.2: an ARC turns about its center on both plane axes, so a full circle (start = end) moves X and Y
    // although neither ends elsewhere. F=12000 is limited to the max_feed of 6000 mm/min (100 mm/s), and the block
    // accelerates with the 500 mm/s^2 of Y: from X=10 Y=0 about X=0 Y=0, 2 pi 10 mm, which the MOTION gives rounded to
    // 62.832 mm (D62); 0.2 s and 10 mm to reach 100 mm/s, as much to stop, the rest at 100 mm/s: 62.832 / 100 + 0.2 s.
    [Fact]
    public void MovingAxes_FullCircleAboveTheMaxFeed_MovesAtTheMaxFeedWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=10 Y=0 Z=0"],
            "ARC=CCW X=10 Y=0 CENTER:X=0 CENTER:Y=0 F=12000");

        Assert.Equal((62.832 / 100) + 0.2, runtime.TotalSeconds, Precision);
    }

    // VM 8 with VM 3.2: a half circle from X=10 Y=0 to X=-10 Y=0 about X=0 Y=0 ends on the Y it started from, and Y
    // moves along it all the same: the block accelerates with the 500 mm/s^2 of Y, not the 1000 mm/s^2 of X. pi 10 mm,
    // rounded to 31.416 mm (D62), at 100 mm/s: 31.416 / 100 + 0.2 s.
    [Fact]
    public void MovingAxes_HalfCircleEndingOnTheYItStartedFrom_AcceleratesWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=10 Y=0 Z=0"],
            "ARC=CCW X=-10 Y=0 CENTER:X=0 CENTER:Y=0 F=6000");

        Assert.Equal((31.416 / 100) + 0.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: RAPID and HOME use the rapid rate with the same profile: 100 mm at the rapid of 6000 mm/min, 1.1 s,
    // although the program has no F.
    [Fact]
    public void Rapid_RapidBlock_MovesAtTheRapidRateWithTheSameProfile()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "RAPID X=100");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // The reading of the TODO(question) in RuntimeEstimator.SpeedOf: a RETRACT takes no feed (VM 3.1a) and moves at the
    // rapid rate with the same profile, as the tool list counts it as rapid. LINE X=100 at F=600 (10 mm/s) takes
    // 100 / 10 + 10 / 1000 s; RETRACT=50 moves Z alone at its rapid of 6000 mm/min (100 mm/s) with 1000 mm/s^2, 0.1 s
    // and 5 mm to reach it, as much to stop, 40 mm at 100 mm/s: 0.6 s, not the 5.01 s of the active F.
    [Fact]
    public void Retract_AfterALineWithAFeed_MovesAtTheRapidRateOfTheToolAxis()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=600",
            "RETRACT=50");

        Assert.Equal(10.01 + 0.6, runtime.TotalSeconds, Precision);
    }

    // The same reading without an F before it: RETRACT=50 is timed at the rapid rate of Z, 0.6 s, and is not counted
    // as a motion without a known feed.
    [Fact]
    public void Retract_WithoutAnyFeedBeforeIt_IsTimedAtTheRapidRate()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "RETRACT=50"), machine, runtime);

        Assert.Equal(0.6, runtime.TotalSeconds, Precision);
        Assert.Contains(", 0 motions without a known feed or rapid rate, ", report, StringComparison.Ordinal);
    }

    // The same reading without [dynamics] (VM 8, the fallback): LINE X=100 at F=600 in 10 s, RETRACT=50 distance over
    // the rapid rate of Z, 6000 mm/min, in 0.5 s; the report says so.
    [Fact]
    public void Retract_MachineWithoutDynamics_IsDistanceOverTheRapidRate()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(dynamics: null)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=600", "RETRACT=50"),
            AnalyticMachines.Mill(dynamics: null), runtime);

        Assert.Equal(10.5, runtime.TotalSeconds, Precision);
        Assert.Contains("RAPID, HOME and RETRACT distance over the rapid rate", report, StringComparison.Ordinal);
    }

    // VM 8: a feed per revolution times the rpm: 6 mm/rev at 1000 rpm is 6000 mm/min, 1.1 s for 100 mm.
    [Fact]
    public void FeedPerRevolution_Line_MovesAtTheFeedTimesTheRpm()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM=1000", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=0 Y=0 Z=0"],
            "LINE X=100 F=6",
            "SPINDLE=OFF");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: under CSS the rpm follows from VC and the current X radius: 200 m/min at X=50 is 200000 / (2 pi 50) rpm,
    // times 0.2 mm/rev; LINE Z=-100 moves X nowhere, from rest to rest with 1000 mm/s^2: 100 / v + v / 1000 s.
    [Fact]
    public void ConstantSurfaceSpeed_LineAtRadius50_TakesTheRpmFromVcAndTheRadius()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0"],
            "LINE Z=-100 F=0.2",
            "SPINDLE=OFF");

        double speed = 0.2 * (200000 / (2 * Math.PI * 50)) / 60;
        Assert.Equal((100 / speed) + (speed / 1000), runtime.TotalSeconds, Precision);
    }

    // VM 8: the rpm of CSS is capped by RPM_MAX: 200 m/min at X=5 would be 6366 rpm, RPM_MAX=3000 caps it, 0.2 mm/rev
    // times 3000 is 10 mm/s; 100 mm from rest to rest take 100 / 10 + 10 / 1000 s.
    [Fact]
    public void ConstantSurfaceSpeed_LineAtRadius5_IsCappedByRpmMax()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=5 Y=0 Z=0"],
            "LINE Z=-100 F=0.2",
            "SPINDLE=OFF");

        Assert.Equal(10.01, runtime.TotalSeconds, Precision);
    }

    // VM 8, machine-config 5: a spindle start or stop adds accel_time of that spindle, 2 s each.
    [Fact]
    public void SpindleAccelTime_StartAndStop_AddTheAccelTimeOfTheSpindle()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, accelTime: 2m),
            [],
            "SPINDLE=CW",
            "SPINDLE=OFF");

        Assert.Equal(4.0, runtime.TotalSeconds, Precision);
    }

    // The reading of the TODO(question) in RuntimeEstimator: a start to less than rpm_max adds the whole accel_time,
    // and a change from CW to CCW counts as a stop and a start; CW, CCW, OFF: 2 + 4 + 2 s.
    [Fact]
    public void SpindleAccelTime_Reversal_CountsAsAStopAndAStart()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, accelTime: 2m),
            [],
            "RPM=100 SPINDLE=CW",
            "SPINDLE=CCW",
            "SPINDLE=OFF");

        Assert.Equal(8.0, runtime.TotalSeconds, Precision);
    }

    // VM 8: dwells add their times; DWELL is in seconds (language 4.1).
    [Fact]
    public void Dwell_DwellBlock_AddsItsSeconds()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            [],
            "DWELL=2.5");

        Assert.Equal(2.5, runtime.TotalSeconds, Precision);
    }

    // VM 8, 3.3, D37: cycle plunges and dwells add their times, from the motions ExpandCycles raises. From X=0 Z=10 the
    // call at X=10 rapids 10 mm in the plane (0.1 s up to 100 mm/s, 0.1 s down: 0.2 s), 8 mm to CLEARANCE (a triangle,
    // 2 * sqrt(8000) / 1000 s), feeds 12 mm to DEPTH at CYCLE_F=600, 10 mm/s (12 / 10 + 10 / 1000 s), dwells 1 s and
    // rapids 12 mm back to CLEARANCE (12 / 100 + 100 / 1000 s).
    [Fact]
    public void CycleCall_ExpandedDrillingCycle_AddsItsRapidsPlungeAndDwell()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=0 Y=0 Z=10"],
            "CYCLE=DRILL_DWELL DEPTH=-10 CLEARANCE=2 CYCLE_F=600 CYCLE_DWELL=1 CYCLE_RETRACT=CLEARANCE",
            "CYCLE_CALL X=10",
            "CYCLE=OFF");

        double expected = 0.2 + (2 * Math.Sqrt(8000) / 1000) + (1.2 + 0.01) + 1 + (0.12 + 0.1);
        Assert.Equal(expected, runtime.TotalSeconds, Precision);
    }

    // VM 8: without dynamics in the configuration the estimate falls back to distance over feed and says so: 100 mm at
    // 6000 mm/min in 1 s, and 100 mm of RAPID at the rapid rate of 6000 mm/min in 1 s.
    [Fact]
    public void Fallback_MachineWithoutDynamics_IsDistanceOverFeedAndSaysSo()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(dynamics: null)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=6000", "RAPID X=0"),
            AnalyticMachines.Mill(dynamics: null), runtime);

        Assert.Equal(2.0, runtime.TotalSeconds, Precision);
        Assert.Contains("Motions: distance over feed", report, StringComparison.Ordinal);
        Assert.Contains("the machine has no [dynamics] (virtual machine 8)", report, StringComparison.Ordinal);
    }

    // The machine file gives its speeds in mm/min and mm/s^2 (machine-config 4): 4 in at 100 in/min are 101.6 mm at
    // 2540 mm/min (42.333 mm/s), from rest to rest with 1000 mm/s^2, 101.6 / v + v / 1000 s.
    [Fact]
    public void Units_InchProgram_IsTimedInMillimetresAgainstTheMachineFile()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(AnalyticMachines.ExactStop)));
        AnalyticRuns.Run(
            CliHarness.OneProgram("UNITS=INCH", "RAPID X=0 Y=0 Z=0", "LINE X=4 F=100"),
            AnalyticMachines.Mill(AnalyticMachines.ExactStop), runtime);

        double speed = 2540d / 60;
        Assert.Equal((101.6 / speed) + (speed / 1000), runtime.TotalSeconds, Precision);
    }

    // VM 8: totals per tool and per section: 1.1 s under tool 1 in ROUGH, 1.1 s under tool 2 in FINISH.
    [Fact]
    public void Totals_TwoToolsInTwoSections_ArePerToolAndPerSection()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["SECTION=\"ROUGH\"", "TOOL=1", "RAPID X=0 Y=0 Z=0"],
            "LINE X=100 F=6000",
            "SECTION=\"FINISH\"",
            "TOOL=2",
            "LINE X=0");

        Assert.Equal(1.1, runtime.SecondsOfTool("1"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfTool("2"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfSection("ROUGH"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfSection("FINISH"), Precision);
        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // VM 8, D67: the estimate over a block range counts the blocks on its lines only: line 6 of the program, the second
    // LINE, 1.1 s of the 2.2 s.
    [Fact]
    public void BlockRange_FromToOneLine_TimesOnlyTheBlockOnIt()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine, new BlockRange { From = 6, To = 6 }));
        AnalyticRuns.Run(
            CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=100 F=6000", "LINE X=200"),
            machine, runtime);

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // Implementation 14, P4-02 and risks: the report always says "estimate" and carries the machine file and every
    // value it used.
    [Fact]
    public void Report_Always_SaysEstimateAndNamesTheMachineFileAndItsValues()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(AnalyticMachines.ExactStop)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=6000"),
            AnalyticMachines.Mill(AnalyticMachines.ExactStop), runtime);

        Assert.StartsWith("Runtime estimate: test.ncx, the whole file, on test.toml, INTERPRETED run\n", report,
            StringComparison.Ordinal);
        Assert.Contains("An estimate from the values of the machine file", report, StringComparison.Ordinal);
        Assert.Contains("path_mode exact_stop, corner_speed none mm/min, block_time 0.001 s", report,
            StringComparison.Ordinal);
        Assert.Contains("X     6000            6000               1000", report, StringComparison.Ordinal);
        Assert.Contains("Estimated time: 1.1 s (0:00:01), the longest channel.", report, StringComparison.Ordinal);
    }

    // Third review of P4-02 (implementation 14, risks: the report carries "every value it used"): in the polar plane
    // the C word is a length (VM 3.1, D102), so a motion of C limits its feed by the max_feed of C and accelerates with
    // the acceleration of C (VM 8): LINE C=10 at F=6000 with max_feed 1200 (20 mm/s) and 100 mm/s^2, exact stop, takes
    // 0.2 s and 2 mm to reach 20 mm/s, 0.2 s and 2 mm to stop, 6 mm at 20 mm/s; 0.7 s. The report names those values
    // of C in the table of the rotary axes, in the units of the example files, and says how the estimate reads them.
    [Fact]
    public void Report_MotionInThePolarPlane_NamesTheValuesOfTheRotaryAxisItUsed()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        string report = AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID Z=2", "POLAR=ON", "LINE X=10 C=0 F=6000"], "LINE C=10"),
            lathe, runtime);

        Assert.Equal(0.7, runtime.TotalSeconds, Precision);
        Assert.Contains("\nRotary axes: they limit and accelerate a motion only in the polar or cylinder plane, where "
            + "their word is a length (virtual machine 3.1, D102); the estimate then reads their numbers as mm/min "
            + "and mm/s^2.\n"
            + "axis  rapid (deg/min)  max_feed (deg/min)  acceleration (deg/s^2)\n"
            + "C     3600             1200                100\n", report, StringComparison.Ordinal);
        Assert.Contains("Not in the estimate: the travel of rotary axes outside the polar and cylinder plane (0 "
            + "motions turned one;", report, StringComparison.Ordinal);
    }

    // VM 8, D103: 2.5D_FRAESEN without a machine file: the default machine has neither [dynamics] nor a rapid rate, so
    // the LINE and ARC moves are distance over feed and the five RAPIDs of known length and the four of unknown length
    // are not timed, which the report counts.
    [Fact]
    public void NotTimed_Fraesen25DWithoutMachineFile_CountsWhatTheEstimateCannotTime()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(DefaultMachine.Create()));
        string report = AnalyticRuns.RunExample("2.5D_FRAESEN.ncx", runtime);

        Assert.Contains("Not timed: 4 motions of unknown length, 5 motions without a known feed or rapid rate, ",
            report, StringComparison.Ordinal);
    }

    // A program of the given blocks after UNITS=MM and the RAPID from the unknown start position.
    private static RuntimeAnalytic Run(MachineConfig machine, params string[] blocks)
    {
        return Run(machine, ["RAPID X=0 Y=0 Z=0"], blocks);
    }

    // A program of the given blocks after UNITS=MM and the blocks that set it up.
    private static RuntimeAnalytic Run(MachineConfig machine, string[] setUp, params string[] blocks)
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        AnalyticRuns.Run(Program(setUp, blocks), machine, runtime);
        return runtime;
    }

    private static string Program(string[] setUp, params string[] blocks)
    {
        var lines = new List<string> { "UNITS=MM" };
        lines.AddRange(setUp);
        lines.AddRange(blocks);
        return CliHarness.OneProgram(lines.ToArray());
    }
}
