using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The arc (language 4.3; virtual machine 3.2, 5; D36, D84, D102): CENTER, R or ANGLE, the center checked against the
/// arc tolerance, the radius, the full circle, the sweep (raised by ArcRules), and the compensation, which does not
/// change in an arc.
/// </summary>
internal static class ArcValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Arc",
        Summary = "The three forms of an arc in its working plane (language 4.3; VM 3.2, 5; D36, D84, D102).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.ArcWithoutCenterRadiusOrAngle, "ARC without CENTER, R or ANGLE.",
                "language 4.3; VM 3.2, 5"),
            ValidationRule.Error(DiagnosticCodes.ArcCenterWithoutBothPlaneAxes, "CENTER on one plane axis only.",
                "VM 3.2"),
            ValidationRule.Error(DiagnosticCodes.ArcCenterOutsideThePlane,
                "CENTER on an axis outside the working plane.", "language 4.3; VM 3.2"),
            ValidationRule.Error(DiagnosticCodes.ArcCenterWithRadius, "ARC with both CENTER and R.", "language 4.3"),
            ValidationRule.Error(DiagnosticCodes.ArcAngleWithRadius, "ANGLE with R.", "language 4.3; VM 3.2, 5; D84"),
            ValidationRule.Error(DiagnosticCodes.ArcAngleWithPlaneEndPoint, "ANGLE with plane end-point words.",
                "language 4.3; VM 3.2, 5; D84"),
            ValidationRule.Error(DiagnosticCodes.ArcAngleWithoutCenter, "ANGLE without CENTER.",
                "language 4.3; VM 3.2; D84"),
            ValidationRule.Error(DiagnosticCodes.ArcStartUnknownInThePlane,
                "ARC from a start that is not known in the working plane.", "VM 3.2; D102")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.ArcInconsistentCenter,
                "Inconsistent center: the start and the end lie at radii from CENTER that differ by more than the arc "
                + "tolerance.", "VM 3.2, 5; D36"),
            ValidationRule.Error(DiagnosticCodes.ArcRadiusTooSmall,
                "Radius too small: the end lies farther from the start than twice R plus the arc tolerance.",
                "VM 3.2, 5; D36"),
            ValidationRule.Error(DiagnosticCodes.ArcFullCircleWithRadius, "Full circle with R.",
                "language 4.3; VM 3.2, 5"),
            ValidationRule.Error(DiagnosticCodes.ArcRadiusZero, "R=0.", "language 4.3"),
            ValidationRule.Error(DiagnosticCodes.ArcAngleNotGreaterThanZero, "ANGLE not greater than 0.",
                "language 4.3; VM 5; D84"),
            ValidationRule.Error(DiagnosticCodes.CompensationChangeInArc, "COMP change in an ARC block.", "VM 5"),
        ],
    };

    /// <summary>
    /// A COMP change in an ARC block is an ERROR (virtual machine 5): a state word of a motion block takes effect
    /// before its motion (language 5 rule 3), and the compensation is switched on or off in a straight move, not in
    /// an arc. A COMP word that repeats the compensation in force changes nothing.
    /// </summary>
    /// <param name="context">The block after its state words.</param>
    /// <param name="before">The compensation before the block.</param>
    public static void CheckCompensationChange(BlockContext context, Compensation before)
    {
        Block block = context.Block;
        if (block.Verb?.Key != "ARC" || block.Find("COMP") is not Word comp || context.State.Motion.Comp == before)
        {
            return;
        }

        context.Diagnostics.Error(block, DiagnosticCodes.CompensationChangeInArc,
            $"{comp.ToCanonical()} changes the compensation in an ARC block; it changes in a straight move (language 5 "
            + "rule 3, virtual machine 5).");
    }
}
