using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The motion rules of virtual machine 5, one test per rule with the smallest input: UNITS, feed, IX, F in a RAPID
/// block, and the machine limits max_feed and limits (machine-config 4, D64, D100).
/// </summary>
public sealed class MotionValidationTests
{
    // VM 3.1, 5: motion before UNITS is an ERROR.
    [Fact]
    public void Motion_BeforeUnits_IsAnError()
    {
        RuleAssert.Only(new VmHarness(VmMachines.Default()).Execute("RAPID X=0"), DiagnosticCodes.MotionBeforeUnits);
    }

    // VM 3.1, 5: LINE without feed is an ERROR.
    [Fact]
    public void Line_WithoutFeed_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM SPINDLE=CW", "LINE X=1");

        RuleAssert.Only(vm, DiagnosticCodes.LineWithoutFeed);
    }

    // VM 3.1, 5: IX from an unknown position is an ERROR.
    [Fact]
    public void IncrementalWord_FromAnUnknownPosition_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100 SPINDLE=CW", "LINE IX=1");

        RuleAssert.Only(vm, DiagnosticCodes.IncrementalFromUnknownPosition);
    }

    // VM 5: F in a RAPID block is a WARNING; RAPID moves at the rapid rate (language 4.3).
    [Fact]
    public void Feed_InARapidBlock_Warns()
    {
        string text = VmHarness.File("UNITS=MM", "RAPID X=0 F=500", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.FeedInRapid);

        Assert.Equal(4, warning.Line);
    }

    // VM 5, D64: F above an axis max_feed is a WARNING that names the axes: X follows 10000 mm/min, Z 8000.
    [Fact]
    public void Feed_AboveTheMaxFeedOfTwoAxes_WarnsNamingBoth()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=MM F=12000");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);

        Assert.StartsWith("F=12000 is above the max_feed of X (10000 mm/min), Z (8000 mm/min)", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D64: a feed that one axis can follow and another cannot names the other only.
    [Fact]
    public void Feed_AboveTheMaxFeedOfOneAxis_NamesThatAxis()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=MM F=9000");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);

        Assert.StartsWith("F=9000 is above the max_feed of Z (8000 mm/min),", warning.Message,
            StringComparison.Ordinal);
    }

    // Machine-config 4: max_feed is in mm/min, and F under UNITS=INCH is in inches per minute: 400 in/min is 10160.
    [Fact]
    public void Feed_InInchesAboveTheMaxFeed_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=INCH F=400");

        RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);
    }

    // VM 5, D64: a feed per revolution is not compared with a feed per minute (the TODO(question) D192 of
    // MotionValidation).
    [Fact]
    public void Feed_PerRevolution_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=MM FEED_MODE=PER_REV F=12000")
            .AssertNoDiagnostics();
    }

    // VM 5, D100: a target beyond the upper limit, compared in the MACHINE frame: X homes to 300 and a machine-frame
    // move of 10 more ends at 310.
    [Fact]
    public void Target_BeyondTheUpperLimitInTheMachineFrame_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME X", "RAPID IX=10 FRAME=MACHINE");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.StartsWith("X ends at 310 in the MACHINE frame, beyond its upper limit 300", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D100: the lower limit: Z homes to 450 and moves 500 down in the machine frame to -50, below -10.
    [Fact]
    public void Target_BelowTheLowerLimitInTheMachineFrame_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME Z", "RAPID IZ=-500 FRAME=MACHINE");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.Contains("beyond its lower limit -10", warning.Message, StringComparison.Ordinal);
    }

    // VM 3.4, 5, D101: after SETPOS against the machine position the machine position follows the workpiece
    // coordinate, and the target is compared there: X=0 declared at 300, X=20 stands at 320 in the MACHINE frame.
    [Fact]
    public void Target_BeyondTheLimitThroughTheSetposShift_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=0", "RAPID X=20");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.StartsWith("X ends at 320 in the MACHINE frame", warning.Message, StringComparison.Ordinal);
    }

    // VM 5, D100: a target is not compared while the machine position is unknown: X known in the workpiece frame of an
    // ORIGIN whose datum the configuration does not give.
    [Fact]
    public void Target_WithTheMachinePositionUnknown_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=MM", "RAPID X=1000")
            .AssertNoDiagnostics();
    }
}
