using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The tool change and the offsets (language 4.4; virtual machine 2.3, 3.5, 5): the rows of the tool change table
/// (raised by ToolChangeRules) and the offsets given in one form per program. All of them depend on the caller's state
/// and are suppressed inside a subprogram that no program of the file calls (3.9, D99).
/// </summary>
internal static class ToolValidation
{
    // The two forms of the offsets: the combined register, and length and radius apart (language 4.4).
    private const string CombinedForm = "OFFSET";
    private const string SeparateForm = "OFFSET:LEN and OFFSET:RAD";

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Tool",
        Summary = "The tool change of the two-word model and the offsets (language 4.4; VM 2.3, 3.5, 5; D42).",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.ToolAlreadyInSpindle,
                "Preload of the tool already in the spindle, a no-op on the machine.", "VM 3.5, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.NothingPreloaded, "TOOL without preload.",
                "language 4.4; VM 3.5, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.PreloadMismatch,
                "A different tool preloaded than called; the magazine has to cycle twice.", "VM 3.5, 5; D42")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.ToolChangeWhileCycleActive,
                "TOOL while a cycle is active; the change ends the cycle.", "VM 3.5, 4, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.ToolChangeWithCompensationOn, "TOOL while compensation is on.",
                "VM 3.5, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.OffsetFormsMixed,
                "OFFSET mixed with OFFSET:LEN or OFFSET:RAD in one program.", "language 4.4; VM 5")
                with { SuppressedInUncalledSub = true },
        ],
    };

    /// <summary>
    /// A program gives its offsets combined, OFFSET, or as length and radius, OFFSET:LEN and OFFSET:RAD (language 4.4);
    /// the two mixed in one program are an ERROR (virtual machine 5). A subprogram a program calls belongs to it. The
    /// rule depends on the caller's state and is suppressed inside a subprogram that no program of the file calls (3.9,
    /// D99).
    /// </summary>
    /// <param name="context">The block.</param>
    /// <param name="firstLineOfForm">The line where the program gave each form first, by form; the caller empties it
    /// at every PROGRAM=BEGIN.</param>
    public static void CheckOffsetForms(BlockContext context, Dictionary<string, int> firstLineOfForm)
    {
        foreach (Word word in context.Block.Words)
        {
            if (word.Key != "OFFSET")
            {
                continue;
            }

            string form = word.Addr is null ? CombinedForm : SeparateForm;
            string otherForm = word.Addr is null ? SeparateForm : CombinedForm;
            if (firstLineOfForm.TryGetValue(otherForm, out int otherLine))
            {
                context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.OffsetFormsMixed,
                    string.Create(CultureInfo.InvariantCulture,
                        $"{word.ToCanonical()} gives the offset as {form}, and the program gives it as {otherForm} "
                        + $"since line {otherLine}; one program uses one of the two forms (language 4.4, virtual "
                        + $"machine 5)."));
            }

            firstLineOfForm.TryAdd(form, context.Block.Line);
        }
    }
}
