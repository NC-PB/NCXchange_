namespace Ncx.Core.Model;

// The diagnostic codes of Ncx.Core (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is an area prefix and three digits. Ncx.Core holds the areas PAR (lexer, parser, catalog) and VM (virtual
// machine); CFG, RDR, CMP, ANA, PLG and CLI codes live in the DiagnosticCodes class of their own project. A diagnostic
// renders as "file(line): ERROR VM042: message", one on a generated block as "file(line, from 12): ERROR VM042:
// message", with the severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never
// renumbered or reused, so that tests and users can rely on it.
//
// Each component keeps its codes in a part of this class of its own, a file DiagnosticCodes.<Component>.cs next to
// this one, and takes its codes from its own range:
//
//   PAR001-PAR099  lexer, parser and structure pass (P0-04)
//   PAR100-PAR149  expressions (P0-05)
//   PAR150-PAR199  word catalog (P0-03)
//   VM001-VM199    block execution (P1-02)
//   VM200-VM399    motion, arcs, retract, home, cycles (P1-03)
//   VM400-VM599    validation (P1-04)
//   VM600-VM649    events (P1-05)
//   VM650-VM749    expander and generated blocks (P1-06)
//   VM750-VM849    INTERPRETED mode and its flow (P4-01 part two)
//   VM850-VM899    the job scheduler (P6-01)
//   VM900-VM949    expression evaluation (P4-01 part one)
//
// Every code has its row, with its severity, its rule and the section it comes from, in the table of the validation
// (VirtualMachine/Validation/DiagnosticTable.cs), which docs/spec/generated/diagnostics.md is written from; a new code
// gets its row in the family of virtual machine 5 it belongs to.

/// <summary>
/// The diagnostic codes of Ncx.Core: one constant per rule, named after the rule, an area prefix and three digits
/// (D98).
/// </summary>
public static partial class DiagnosticCodes
{
}
