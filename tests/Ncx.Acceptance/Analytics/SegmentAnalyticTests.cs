using System.Globalization;
using Ncx.Acceptance.Cli;
using Ncx.Analytics;
using Ncx.Analytics.Segments;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The segment length of virtual machine 8 on small programs, each length computed by hand: per MOTION in the
/// workpiece frame with known positions the euclidean length, the arc length for an arc; the minimum, maximum and mean,
/// the histogram with configurable bins, the ten shortest motions with their lines, the motions skipped and why, over
/// the block range of D67 (implementation 14, P4-03). The programs run on the three-axis test mill, whose axes start
/// unknown, so the first RAPID of each has no length (D100); a program starts on line 3 (CliHarness.OneProgram).
/// </summary>
public sealed class SegmentAnalyticTests
{
    private const int Precision = 6;

    // UNITS on line 3, the RAPID from the unknown start on line 4, LINE X=30 Y=40 on line 5, LINE Z=-5 on line 6 and
    // the ARC on line 7.
    private static readonly string[] s_threeBlocks =
    [
        "UNITS=MM",
        "RAPID X=0 Y=0 Z=0",
        "LINE X=30 Y=40 F=1000",
        "LINE Z=-5",
        "ARC=CCW X=10 Y=40 CENTER:X=20 CENTER:Y=40",
    ];

    private static MachineConfig Mill => AnalyticMachines.Mill(dynamics: null);

    // P4-03 test first, three blocks with known lengths and one arc: LINE X=30 Y=40 from X=0 Y=0 is 50 mm, LINE Z=-5 is
    // 5 mm, the half circle ARC=CCW X=10 Y=40 about X=20 Y=40 is pi 10 mm, which the MOTION gives as 31.416 mm (D62);
    // minimum 5, maximum 50, mean 86.416 / 3. The RAPID from the unknown start has no length and is skipped.
    [Fact]
    public void SegmentLength_ThreeBlocksAndAnArc_AreTheLengthsComputedByHand()
    {
        SegmentAnalytic segments = Run(s_threeBlocks);

        ValueStatistics all = segments.Statistics.All;
        Assert.Equal(3, all.Count);
        Assert.Equal(5, all.Minimum, Precision);
        Assert.Equal(50, all.Maximum, Precision);
        Assert.Equal(86.416 / 3, all.Mean, Precision);
        Assert.Equal(31.416, segments.Statistics.Of(Verb.Arc)!.Maximum, Precision);
        Assert.Equal(2, segments.Statistics.Of(Verb.Line)!.Count);
        Assert.Equal(4, segments.Motions);
        Assert.Equal(1, segments.FromUnknown);
    }

    // P4-03: the count of skipped blocks with unknown positions and why. Line 4 starts from the unknown start position,
    // lines 6 and 8 move in the machine frame (FRAME=MACHINE, HOME: VM 2.1, 3.4), line 7 goes from the Z known in the
    // MACHINE frame into the workpiece frame (VM 3.4, D35), line 9 starts from the Z that HOME left unknown on an axis
    // without a reference point (D100), and the bare RETRACT of line 10 ends at a limit the machine file does not give
    // (VM 3.1a). Only line 5 has a length.
    [Fact]
    public void Skipped_EveryReason_IsCountedWithIt()
    {
        SegmentAnalytic segments = Run("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=10 F=1000", "RAPID Z=50 FRAME=MACHINE",
            "LINE Z=-5", "HOME Z", "LINE Z=-6", "RETRACT");

        Assert.Equal(1, segments.Statistics.All.Count);
        Assert.Equal(10, segments.Statistics.All.Maximum, Precision);
        Assert.Equal(7, segments.Motions);
        Assert.Equal(2, segments.InMachineFrame);
        Assert.Equal(2, segments.FromUnknown);
        Assert.Equal(1, segments.ToUnknown);
        Assert.Equal(1, segments.BetweenFrames);
        Assert.Equal(0, segments.UnresolvedArcs);
    }

    // P4-03: the ten shortest blocks with their line numbers. Twelve LINEs of 12, 11, ... 1 mm stand on lines 5 to 16;
    // the ten shortest are those of 1 to 10 mm, on lines 16 down to 7, the shortest first.
    [Fact]
    public void TenShortest_TwelveLines_AreTheTenShortestWithTheirLines()
    {
        var blocks = new List<string> { "UNITS=MM", "RAPID X=0 Y=0 Z=0" };
        int x = 0;
        for (int length = 12; length >= 1; length--)
        {
            x += length;
            blocks.Add(string.Create(CultureInfo.InvariantCulture, $"LINE X={x} F=1000"));
        }

        SegmentAnalytic segments = Run(blocks.ToArray());

        var lines = new List<int>();
        foreach (WorstMotion motion in segments.Shortest)
        {
            lines.Add(motion.Line);
        }

        Assert.Equal([16, 15, 14, 13, 12, 11, 10, 9, 8, 7], lines);
        Assert.Equal(1, segments.Shortest[0].Value, Precision);
        Assert.Equal(10, segments.Shortest[9].Value, Precision);
    }

    // VM 8, D67: over lines 6 to 7 the segment length measures the LINE Z=-5 and the arc only; the RAPID of line 4 lies
    // outside the range and is not counted as skipped either.
    [Fact]
    public void BlockRange_FromTo_MeasuresOnlyTheMotionsOnItsLines()
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill, new BlockRange { From = 6, To = 7 }));
        AnalyticRuns.Run(CliHarness.OneProgram(s_threeBlocks), Mill, segments);

        Assert.Equal(2, segments.Statistics.All.Count);
        Assert.Equal(5, segments.Statistics.All.Minimum, Precision);
        Assert.Equal(31.416, segments.Statistics.All.Maximum, Precision);
        Assert.Equal(2, segments.Motions);
        Assert.Equal(0, segments.FromUnknown);
    }

    // The reports give their distances in mm, as the tool list does: LINE X=4 under UNITS=INCH is 101.6 mm.
    [Fact]
    public void Units_InchProgram_IsMeasuredInMillimetres()
    {
        SegmentAnalytic segments = Run("UNITS=INCH", "RAPID X=0 Y=0 Z=0", "LINE X=4 F=100");

        Assert.Equal(101.6, segments.Statistics.All.Maximum, Precision);
    }

    // VM 8: every MOTION is measured, a RAPID of known length among them, in a row of its own verb.
    [Fact]
    public void SegmentLength_RapidOfKnownLength_IsMeasuredUnderItsVerb()
    {
        SegmentAnalytic segments = Run("UNITS=MM", "RAPID X=0 Y=0 Z=0", "RAPID X=3 Y=4");

        Assert.Equal(5, segments.Statistics.Of(Verb.Rapid)!.Maximum, Precision);
        Assert.Null(segments.Statistics.Of(Verb.Line));
    }

    // P4-03: a histogram with configurable bins. With the bounds 1 and 10 mm the 5 mm line falls into 1 to 10, the
    // 50 mm line and the 31.416 mm arc into 10 and more.
    [Fact]
    public void Histogram_BoundsGiven_CountsTheLengthsIntoThoseBins()
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill), [1m, 10m]);
        AnalyticRuns.Run(CliHarness.OneProgram(s_threeBlocks), Mill, segments);

        Assert.Equal(3, segments.Histogram.BinCount);
        Assert.Equal(0, segments.Histogram.CountOf(0));
        Assert.Equal(1, segments.Histogram.CountOf(1));
        Assert.Equal(2, segments.Histogram.CountOf(2));
    }

    // The report in aligned text: the title names the file, the range, the machine and the run (as every report does),
    // then what the analytic measures, and at the end the motions skipped and why.
    [Fact]
    public void Report_Text_NamesWhatItMeasuresAndTheSkippedMotions()
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill));
        string report = AnalyticRuns.Run(CliHarness.OneProgram(s_threeBlocks), Mill, segments);

        Assert.StartsWith("Segment length: test.ncx, the whole file, on test.toml, INTERPRETED run\n", report,
            StringComparison.Ordinal);
        Assert.Contains("Per MOTION the distance in mm between its start and its end point, the arc length for an ARC "
            + "(virtual machine 8)", report, StringComparison.Ordinal);
        Assert.Contains("\nThe ten shortest motions:\n", report, StringComparison.Ordinal);
        Assert.EndsWith("Skipped: 1 of 4 motions: 0 in the machine frame (HOME, FRAME=MACHINE), 1 from an unknown "
            + "position, 0 to an unknown position, 0 from one frame into another, 0 arcs that did not resolve (virtual "
            + "machine 3.4, 8).\n", report, StringComparison.Ordinal);
    }

    // The tables of the report in CSV (VM 8): the statistics per verb and over all, the histogram with the default
    // bounds, the ten shortest with their lines, the shortest first.
    [Fact]
    public void Report_Csv_WritesTheStatisticsTheHistogramAndTheShortest()
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill) with { Format = TableFormat.Csv });
        string report = AnalyticRuns.Run(CliHarness.OneProgram(s_threeBlocks), Mill, segments);

        Assert.Contains("\nverb,motions,minimum,maximum,mean\nLINE,2,5,50,27.5\nARC,1,31.416,31.416,31.416\n"
            + "all,3,5,50,28.805\n", report, StringComparison.Ordinal);
        Assert.Contains("\nlength (mm),motions\n0 to 0.01,0\n0.01 to 0.1,0\n0.1 to 0.5,0\n0.5 to 1,0\n1 to 5,0\n"
            + "5 to 10,1\n10 to 50,1\n50 to 100,1\n100 and more,0\n", report, StringComparison.Ordinal);
        Assert.Contains("\nline,verb,length (mm)\n6,LINE,5\n7,ARC,31.416\n5,LINE,50\n", report,
            StringComparison.Ordinal);
    }

    // A range without a motion of known length says so instead of an empty table.
    [Fact]
    public void Report_NoMotionOfKnownLength_SaysSo()
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill));
        string report = AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0"), Mill, segments);

        Assert.Contains("\nNo motion in the range has a known length.\n", report, StringComparison.Ordinal);
        Assert.Contains("\nall   0        -        -        -\n", report, StringComparison.Ordinal);
    }

    private static SegmentAnalytic Run(params string[] blocks)
    {
        var segments = new SegmentAnalytic(AnalyticRuns.Options(Mill));
        AnalyticRuns.Run(CliHarness.OneProgram(blocks), Mill, segments);
        return segments;
    }
}
