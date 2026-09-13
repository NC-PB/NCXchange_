namespace Ncx.Readers;

// The diagnostic codes of Ncx.Readers (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix RDR and three digits; the codes of Ncx.Core (PAR, VM) and Ncx.Config (CFG) live in the
// DiagnosticCodes class of their own project. A diagnostic renders as "file(line): WARNING RDR001: message" with the
// severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never renumbered or reused, so
// that tests and users can rely on it.
//
// Each component keeps its codes in a part of this class of its own, a file DiagnosticCodes.<Component>.cs next to
// its code, and takes its codes from its own range:
//
//   RDR001-RDR099  reader framework (P3-01): RDR001-RDR009 RAW, RDR010-RDR029 the file structure
//
// The readers of the controller families take ranges of their own in parts next to their folders.

/// <summary>
/// The diagnostic codes of Ncx.Readers: one constant per rule, named after the rule, the area prefix RDR and three
/// digits (D98).
/// </summary>
public static partial class DiagnosticCodes
{
}
