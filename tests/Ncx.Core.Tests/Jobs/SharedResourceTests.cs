using Ncx.Core.Model;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// Resources are per job: two channels commanding the same spindle of [shared] between two marks is a WARNING (virtual
/// machine 3.7; machine-config 8, D20), and the axes of [shared] are checked like the spindles (F24). On the built-in
/// machine of D103 the spindle S1 is the role MAIN, and X is an axis.
/// </summary>
public sealed class SharedResourceTests
{
    // VM 3.7: both channels command S1 before any mark; the WARNING stands on the later command and names the earlier.
    [Fact]
    public void SharedSpindle_CommandedFromBothChannelsBetweenMarks_Warns()
    {
        JobHarness job = Run(["S1"], [],
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SPINDLE:MAIN=OFF", "PROGRAM=END"));

        job.AssertFinished();
        Diagnostic warning = job.Single(DiagnosticCodes.SpindleSharedBetweenMarks);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal("ch2.ncx", warning.File);
        Assert.Equal(4, warning.Line);
        Assert.Equal(
            "Spindle S1 of [shared] is commanded here by channel 2 and by channel 1 (ch1.ncx line 3) between the same "
            + "two marks; two channels commanding one resource between two marks is a WARNING (virtual machine 3.7, "
            + "machine-config 8).", warning.Message);
    }

    // VM 3.7: a mark both channels wait at lies between the two commands; no WARNING.
    [Fact]
    public void SharedSpindle_CommandedOnEitherSideOfAMark_DoesNotWarn()
    {
        JobHarness job = Run(["S1"], [],
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "SYNC=100", "PROGRAM=END"),
            JobHarness.File("SYNC=100", "SPINDLE:MAIN=OFF", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Empty(job.Codes());
    }

    // VM 3.7: a mark of channels 1 and 3 does not order the commands of channels 1 and 2.
    [Fact]
    public void SharedSpindle_MarkOfOtherChannels_DoesNotSeparateTheCommands()
    {
        JobHarness job = Run(["S1"], [],
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "SYNC=100 WITH=1,3", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "SPINDLE:MAIN=OFF", "PROGRAM=END"),
            JobHarness.File("SYNC=100 WITH=1,3", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal([DiagnosticCodes.SpindleSharedBetweenMarks], job.Codes());
    }

    // VM 3.7: a WAIT_CHANNEL orders what the awaited channel did before everything the waiting channel does after.
    [Fact]
    public void SharedSpindle_CommandedAfterWaitChannel_DoesNotWarn()
    {
        JobHarness job = Run(["S1"], [],
            JobHarness.File("WAIT_CHANNEL=2", "SPINDLE:MAIN=OFF", "PROGRAM=END"),
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Empty(job.Codes());
    }

    // VM 3.7: the WARNING stands once for a pair of channels between two marks, however often they command the spindle.
    [Fact]
    public void SharedSpindle_SeveralCommandsBetweenTheSameMarks_WarnsOnce()
    {
        JobHarness job = Run(["S1"], [],
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "SPINDLE:MAIN=CCW", "UNITS=MM", "SYNC=100",
                "SPINDLE:MAIN=OFF", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SPINDLE:MAIN=OFF", "SPINDLE:MAIN=CW", "SYNC=100", "SPINDLE:MAIN=CCW",
                "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal([DiagnosticCodes.SpindleSharedBetweenMarks, DiagnosticCodes.SpindleSharedBetweenMarks],
            job.Codes());
    }

    // Machine-config 8, D20 (the TODO(question) of SharedResources): a spindle that [shared] does not name is not
    // checked.
    [Fact]
    public void Spindle_NotInShared_IsNotChecked()
    {
        JobHarness job = Run([], [],
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=100", "PROGRAM=END"),
            JobHarness.File("SPINDLE:MAIN=OFF", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Empty(job.Codes());
    }

    // Machine-config 8, F24: an axis of [shared] moved from both channels between two marks warns like a spindle.
    [Fact]
    public void SharedAxis_MovedFromBothChannelsBetweenMarks_Warns()
    {
        JobHarness job = Run([], ["X"],
            JobHarness.File("UNITS=MM", "RAPID X=10", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "RAPID X=20", "PROGRAM=END"));

        job.AssertFinished();
        Diagnostic warning = job.Single(DiagnosticCodes.AxisSharedBetweenMarks);
        Assert.Equal("ch2.ncx", warning.File);
        Assert.Equal(4, warning.Line);
        Assert.StartsWith("Axis X of [shared] is commanded here by channel 2 and by channel 1 (ch1.ncx line 4)",
            warning.Message, StringComparison.Ordinal);
    }

    private static JobHarness Run(string[] spindles, string[] axes, params string[] files)
    {
        return JobHarness.Run(new JobSetup { Files = files, SharedSpindles = spindles, SharedAxes = axes });
    }
}
