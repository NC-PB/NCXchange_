using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// One rule of the validation, one row of docs/spec/generated/diagnostics.md: its code, its severity, the rule in the
/// words of the specification and the section it comes from (virtual machine 5, D98). Rules of virtual machine 5 that
/// another stage raises with a code of its own project, or a mode that is not built yet, stand in the table without a
/// code of Ncx.Core, with the stage that raises them.
/// </summary>
internal sealed record ValidationRule
{
    /// <summary>
    /// The code, a constant of DiagnosticCodes: VM042; null for a rule that is raised outside the virtual machine of
    /// phase 1 with a code of its own (RaisedBy says where).
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// ERROR, WARNING or INFO (D98).
    /// </summary>
    public required Severity Severity { get; init; }

    /// <summary>
    /// The rule in one line, in the words of the specification.
    /// </summary>
    public required string Rule { get; init; }

    /// <summary>
    /// Where the rule stands: "VM 3.5, 5; D42", "language 4.13".
    /// </summary>
    public required string Section { get; init; }

    /// <summary>
    /// True for a rule that depends on the state of a caller and is suppressed inside a subprogram that no program of
    /// the file calls (virtual machine 3.9, 5, D99).
    /// </summary>
    public bool SuppressedInUncalledSub { get; init; }

    /// <summary>
    /// The stage that raises the rule when it is not the virtual machine or the parser of this table: "the job
    /// scheduler", "the compiler"; null otherwise.
    /// </summary>
    public string? RaisedBy { get; init; }

    /// <summary>
    /// An ERROR: the run stops (virtual machine 2.9).
    /// </summary>
    public static ValidationRule Error(string code, string rule, string section)
    {
        return new ValidationRule { Code = code, Severity = Severity.Error, Rule = rule, Section = section };
    }

    /// <summary>
    /// A WARNING: reported, and the run continues (virtual machine 2.9).
    /// </summary>
    public static ValidationRule Warning(string code, string rule, string section)
    {
        return new ValidationRule { Code = code, Severity = Severity.Warning, Rule = rule, Section = section };
    }
}
