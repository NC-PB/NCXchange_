using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The cycle call (language 4.7; virtual machine 3.3, 5; D59, D94): a cycle to call, DEPTH and CLEARANCE of the
/// built-in drilling family, and the drilling axis. CycleRules raises them.
/// </summary>
internal static class CycleValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Cycle",
        Summary = "The cycle call (language 4.7; VM 3.3, 5; D59, D94).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.CycleCallWithoutCycle, "CYCLE_CALL without cycle.", "VM 3.3, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.CycleCallWithoutDepthOrClearance,
                "CYCLE_CALL of a built-in drilling cycle without DEPTH or CLEARANCE; a catalog or CYCLE:controller=n "
                + "cycle carries its own parameters.", "VM 3.3, 5; D94") with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.CycleAxisNotLinear,
                "AXIS naming an axis that is not a linear axis of the machine.", "language 4.7; VM 3.3, 5; D59"),
        ],
    };
}
