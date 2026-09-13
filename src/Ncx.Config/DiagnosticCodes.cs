namespace Ncx.Config;

// The diagnostic codes of Ncx.Config (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix CFG and three digits; the codes of Ncx.Core (PAR, VM) live in the DiagnosticCodes class
// of that project. A diagnostic renders as "file(line): ERROR CFG003: message" with the severities ERROR, WARNING and
// INFO. A constant is named after its rule, and a code is never renumbered or reused, so that tests and users can
// rely on it.
//
// Each component keeps its codes in a part of this class of its own, a file DiagnosticCodes.<Component>.cs next to
// this one, and takes its codes from its own range:
//
//   CFG001-CFG099  loading of the machine file, the job manifest and the vars file (P2-01), and of ncx.toml (P2-04)
//   CFG100-CFG149  templates (P2-02)
//   CFG150-CFG199  cycle catalogs (P2-03)

/// <summary>
/// The diagnostic codes of Ncx.Config: one constant per rule, named after the rule, the area prefix CFG and three
/// digits (D98).
/// </summary>
public static partial class DiagnosticCodes
{
}
