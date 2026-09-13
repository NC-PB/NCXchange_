using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The vector form of 5-axis motion (language 4.3; virtual machine 3.1, 5; D81): TX TY TZ and NX NY NZ under TCPM=ON,
/// complete, of unit length and never with rotary axis words. ToolVectorRules raises them.
/// </summary>
internal static class VectorValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Vector",
        Summary = "The tool vector and the surface normal under TCPM (language 4.3; VM 3.1, 5; D81).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.VectorWithoutTcpm, "Vector words without TCPM=ON.",
                "language 4.3; VM 3.1, 5; D81"),
            ValidationRule.Error(DiagnosticCodes.VectorWithRotaryWords, "Vector words mixed with rotary words.",
                "language 4.3; VM 3.1, 5; D81"),
            ValidationRule.Error(DiagnosticCodes.VectorIncomplete,
                "Incomplete vector words: TX TY TZ all three, NX NY NZ all three and only with TX TY TZ.",
                "language 4.3; VM 5; D81"),
            ValidationRule.Error(DiagnosticCodes.VectorNotUnitLength,
                "Vector words not of unit length within the arc tolerance.", "VM 3.1, 5; D36, D81"),
        ],
    };
}
