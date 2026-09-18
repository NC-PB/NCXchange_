using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The arc rules of virtual machine 5, one test per rule with an arc from X=0 Y=0 in the XY plane: CENTER, R or ANGLE,
/// the center, the radius, the full circle, the sweep, and the compensation (language 4.3; VM 3.2; D36, D84).
/// </summary>
public sealed class ArcValidationTests
{
    // VM 3.2, 5: ARC without CENTER, R or ANGLE is an ERROR.
    [Fact]
    public void Arc_WithoutCenterRadiusOrAngle_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW X=10 Y=0"), DiagnosticCodes.ArcWithoutCenterRadiusOrAngle);
    }

    // VM 3.2, 5, D36: an inconsistent center, radii 3 and 7 from CENTER.
    [Fact]
    public void Arc_InconsistentCenter_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW X=10 Y=0 CENTER:X=3 CENTER:Y=0"), DiagnosticCodes.ArcInconsistentCenter);
    }

    // VM 3.2, 5, D36: radius too small, the end 10 away from the start with R=2.
    [Fact]
    public void Arc_RadiusTooSmall_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW X=10 Y=0 R=2"), DiagnosticCodes.ArcRadiusTooSmall);
    }

    // VM 3.2, 5: a full circle with R is an ERROR; full circles need CENTER (language 4.3).
    [Fact]
    public void Arc_FullCircleWithRadius_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW X=0 Y=0 R=5"), DiagnosticCodes.ArcFullCircleWithRadius);
    }

    // VM 3.2, 5, D84: ANGLE with R is an ERROR.
    [Fact]
    public void Angle_WithRadius_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW CENTER:X=5 CENTER:Y=0 ANGLE=90 R=5"), DiagnosticCodes.ArcAngleWithRadius);
    }

    // VM 3.2, 5, D84: ANGLE with a plane end-point word is an ERROR.
    [Fact]
    public void Angle_WithAPlaneEndPointWord_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW X=5 CENTER:X=5 CENTER:Y=0 ANGLE=90"),
            DiagnosticCodes.ArcAngleWithPlaneEndPoint);
    }

    // VM 5, D84: ANGLE not greater than 0 is an ERROR.
    [Fact]
    public void Angle_NotGreaterThanZero_IsAnError()
    {
        RuleAssert.Only(FromOrigin("ARC=CW CENTER:X=5 CENTER:Y=0 ANGLE=0"), DiagnosticCodes.ArcAngleNotGreaterThanZero);
    }

    // Language 4.3 (the CENTER rows: the address is a plane axis) and VM 3.2 (the arc runs only in the working plane):
    // CENTER on an axis outside the working plane is an ERROR.
    [Fact]
    public void Center_OnAnAxisOutsideTheWorkingPlane_IsAnError()
    {
        Diagnostic error = RuleAssert.Only(FromOrigin("ARC=CW X=10 Y=0 CENTER:X=5 CENTER:Y=0 CENTER:Z=0"),
            DiagnosticCodes.ArcCenterOutsideThePlane);

        Assert.EndsWith("(language 4.3, virtual machine 3.2).", error.Message, StringComparison.Ordinal);
    }

    // The row of the rule in the table of the validation, and so in generated/diagnostics.md, cites the CENTER rows of
    // language 4.3 beside virtual machine 3.2.
    [Fact]
    public void CenterOutsideThePlane_RowOfTheTable_CitesLanguage43AndVm32()
    {
        ValidationRule rule = Assert.Single(ArcValidation.Family.Rules,
            row => row.Code == DiagnosticCodes.ArcCenterOutsideThePlane);

        Assert.Equal("language 4.3; VM 3.2", rule.Section);
    }

    // VM 5: a COMP change in an ARC block is an ERROR; the compensation changes in a straight move.
    [Fact]
    public void Compensation_ChangeInAnArcBlock_IsAnError()
    {
        Diagnostic error = RuleAssert.Only(FromOrigin("ARC=CW X=10 Y=0 R=5 COMP=LEFT"),
            DiagnosticCodes.CompensationChangeInArc);

        Assert.StartsWith("COMP=LEFT changes the compensation in an ARC block", error.Message,
            StringComparison.Ordinal);
    }

    // VM 5: a COMP word that repeats the compensation in force changes nothing.
    [Fact]
    public void Compensation_RepeatedInAnArcBlock_IsNoChange()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "RAPID X=0 Y=0", "COMP=LEFT", "ARC=CW X=10 Y=0 R=5 COMP=LEFT");

        vm.AssertNoDiagnostics();
    }

    // An arc from X=0 Y=0, known in the workpiece frame of the XY plane.
    private static VmHarness FromOrigin(string arc)
    {
        return new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID X=0 Y=0", arc);
    }
}
