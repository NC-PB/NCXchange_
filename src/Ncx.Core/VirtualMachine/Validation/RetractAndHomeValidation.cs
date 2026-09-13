using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// RETRACT along the tool axis and HOME to the reference point (language 4.3; virtual machine 3 step 5, 3.1a, 5; D83,
/// D100). RetractRules and HomeRules raise them; RETRACT with other axis words is PAR012 of the structure.
/// </summary>
internal static class RetractAndHomeValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Retract and home",
        Summary = "RETRACT along the tool axis and HOME to the reference point (language 4.3; VM 3 step 5, 3.1a, 5; "
            + "D83, D100). RETRACT with other axis words is PAR012 of the structure.",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.HomeWithoutReferencePoint,
                "HOME on an axis without a reference point in the configuration, once per run and axis; the axis is "
                + "unknown in every frame afterwards.", "VM 3 step 5, 5; D100"),
            ValidationRule.Error(DiagnosticCodes.HomeWithoutAxis, "HOME without an axis name.", "VM 5"),
            ValidationRule.Error(DiagnosticCodes.RetractWithFeed, "RETRACT with feed.", "VM 3.1a, 5; D83"),
        ],
    };
}
