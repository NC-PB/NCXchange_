using System.Text;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// Writes the table of the validation as docs/spec/generated/diagnostics.md: every code of Ncx.Core with its severity,
/// its rule and the section it comes from, family by family in the order of the validation list of virtual machine 5,
/// so that the documentation can cite a code (P1-04, D98).
/// </summary>
internal static class DiagnosticsDocument
{
    // What the file is, where it comes from and how to read it.
    private const string Head = """
        # Diagnostics

        Generated from the table of the validation in `src/Ncx.Core/VirtualMachine/Validation/` by the test
        `DiagnosticTableTests` (P1-04); change the table and run the tests instead of editing this file. It lists every
        code of `Ncx.Core`: the `PAR` codes of the lexer, the parser and the word catalog, and the `VM` codes of the
        virtual machine, family by family in the order of the validation list of `../ncx-virtual-machine.md` section 5.
        An ERROR stops the run, a WARNING is reported and the run continues (D98); a diagnostic reads
        `file(line): ERROR VM042: message`. A rule of section 5 that another stage raises stands in its family with that
        stage named, and without a code where the code belongs to that stage's own project. The codes of the other
        projects (`CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI`) are not listed here.

        """;

    /// <summary>
    /// The text of the file, with LF line endings.
    /// </summary>
    public static string Write()
    {
        var text = new StringBuilder(Head.ReplaceLineEndings("\n"));
        text.Append('\n')
            .Append("Suppressed inside a subprogram that no program of the file calls, because the state they check ")
            .Append("belongs to a caller that does not exist (virtual machine 3.9, D99): ")
            .Append(string.Join(", ", SuppressedCodes()))
            .Append(".\n");

        foreach (ValidationFamily family in DiagnosticTable.Families)
        {
            text.Append('\n').Append("## ").Append(family.Name).Append("\n\n")
                .Append(family.Summary).Append("\n\n")
                .Append("| Code | Severity | Rule | Section |\n")
                .Append("|---|---|---|---|\n");
            foreach (ValidationRule rule in family.Rules)
            {
                text.Append(Row(rule)).Append('\n');
            }
        }

        return text.ToString();
    }

    // One row: the code, the severity in capitals (D98), the rule with the stage that raises it and its suppression,
    // the section.
    private static string Row(ValidationRule rule)
    {
        string code = rule.Code is string known ? "`" + known + "`" : "(none)";
        var ruleText = new StringBuilder(rule.Rule);
        if (rule.RaisedBy is string stage)
        {
            ruleText.Append(" Raised by ").Append(stage).Append('.');
        }

        if (rule.SuppressedInUncalledSub)
        {
            ruleText.Append(" Suppressed inside a subprogram that no program of the file calls (D99).");
        }

        return $"| {code} | {SeverityText(rule.Severity)} | {Cell(ruleText.ToString())} | {Cell(rule.Section)} |";
    }

    // A vertical bar inside a cell would end it: it is escaped (GitHub Markdown tables).
    private static string Cell(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal);
    }

    // The suppressed codes in code order.
    private static List<string> SuppressedCodes()
    {
        var codes = new List<string>();
        foreach (ValidationFamily family in DiagnosticTable.Families)
        {
            foreach (ValidationRule rule in family.Rules)
            {
                if (rule.SuppressedInUncalledSub && rule.Code is string code)
                {
                    codes.Add(code);
                }
            }
        }

        codes.Sort(StringComparer.Ordinal);
        var quoted = new List<string>();
        foreach (string code in codes)
        {
            quoted.Add("`" + code + "`");
        }

        return quoted;
    }

    private static string SeverityText(Severity severity)
    {
        return severity switch
        {
            Severity.Error => "ERROR",
            Severity.Warning => "WARNING",
            _ => "INFO",
        };
    }
}
