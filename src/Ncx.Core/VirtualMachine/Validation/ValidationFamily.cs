namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// One family of the validation list of virtual machine 5, the rules of one file of this folder: its name, what it is
/// about, and its rules in the order of their codes.
/// </summary>
internal sealed record ValidationFamily
{
    /// <summary>
    /// The name of the family, the heading of its table in docs/spec/generated/diagnostics.md: "Tool".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the rules of the family are about, one sentence with the sections.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public required IReadOnlyList<ValidationRule> Rules { get; init; }
}
