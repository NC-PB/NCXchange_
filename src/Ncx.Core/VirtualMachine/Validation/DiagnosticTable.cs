namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The table of the validation: every code of Ncx.Core with its severity, its rule and its section, family by family in
/// the order of the validation list of virtual machine 5, and the rules of that list that other stages raise. The test
/// DiagnosticTableTests writes docs/spec/generated/diagnostics.md from it, so that the documentation can cite a code
/// (D98).
/// </summary>
internal static class DiagnosticTable
{
    /// <summary>
    /// The families in the order of the validation list: the file and its words first, with the blocks the expander
    /// generates for it, which the list does not name (virtual machine 3.10); then the frame, the motion and its forms,
    /// the tool and the spindle, the cycle, the flow, the expressions, the resources and the channels.
    /// </summary>
    public static IReadOnlyList<ValidationFamily> Families { get; } =
    [
        StructureValidation.Family,
        GeneratedBlockValidation.Family,
        FrameValidation.Family,
        MotionValidation.Family,
        ArcValidation.Family,
        VectorValidation.Family,
        RetractAndHomeValidation.Family,
        ToolValidation.Family,
        SpindleValidation.Family,
        CycleValidation.Family,
        FlowValidation.Family,
        ExpressionValidation.Family,
        ResourceValidation.Family,
        ChannelValidation.Family,
    ];

    /// <summary>
    /// The rule of a code; null for a code the table does not list.
    /// </summary>
    /// <param name="code">A constant of DiagnosticCodes: VM042.</param>
    public static ValidationRule? Find(string code)
    {
        foreach (ValidationFamily family in Families)
        {
            foreach (ValidationRule rule in family.Rules)
            {
                if (rule.Code == code)
                {
                    return rule;
                }
            }
        }

        return null;
    }
}
