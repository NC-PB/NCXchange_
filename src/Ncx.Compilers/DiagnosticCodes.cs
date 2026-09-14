namespace Ncx.Compilers;

// The diagnostic codes of Ncx.Compilers (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix CMP and three digits; the codes of Ncx.Core (PAR, VM) and Ncx.Config (CFG) live in the
// DiagnosticCodes class of their own project. A diagnostic renders as "file(line): ERROR CMP001: message" with the
// severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never renumbered or reused, so
// that tests and users can rely on it.
//
// Each component keeps its codes in a part of this class of its own and takes its codes from its own range:
//
//   CMP001-CMP099  compiler framework (P3-03): CMP001-CMP009 what a machine takes at all and the subprograms
//                  (D94, D99), CMP010-CMP019 the templates of the machine (machine-config 3), CMP020-CMP029 the
//                  numbers and the lines of the output (machine-config 2)
//
// The compilers of the controller families take ranges of their own in parts next to their folders.

/// <summary>
/// The diagnostic codes of Ncx.Compilers: one constant per rule, named after the rule, the area prefix CMP and three
/// digits (D98).
/// </summary>
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP001: RAW of another controller or builder, or a CYCLE:controller=n block of another controller family, is
    /// compiled; it compiles only to the controller it names, and any other target is an ERROR (language 4.1, 4.7.1;
    /// controller-mapping 9; D5, D94).
    /// </summary>
    public const string NativeTextOfAnotherController = "CMP001";

    /// <summary>
    /// CMP002: two walks of one subprogram write different lines, and the section is written once for every caller
    /// (virtual machine 3.9, D99).
    /// </summary>
    public const string SubprogramWalksDiffer = "CMP002";

    /// <summary>
    /// CMP003, a WARNING: under program_layout = "file_per_program" a subprogram that no program calls stands in no
    /// output file (machine-config 2, D48, D99).
    /// </summary>
    public const string UncalledSubprogramNotWritten = "CMP003";

    /// <summary>
    /// CMP010: a word of the program needs a template that the machine file does not give, so nothing can be written
    /// for it (machine-config 2, 3).
    /// </summary>
    public const string TemplateMissing = "CMP010";

    /// <summary>
    /// CMP011, a WARNING: PRELOAD on a machine without a preload template is dropped (machine-config 3).
    /// </summary>
    public const string PreloadDropped = "CMP011";

    /// <summary>
    /// CMP020, a WARNING: a value has more decimals than the machine takes for its address, and is written rounded to
    /// them (machine-config 2, phase 3 P3-03).
    /// </summary>
    public const string MoreDecimalsThanTheMachineTakes = "CMP020";

    /// <summary>
    /// CMP021, a WARNING: a line is longer than the max_line_length of the machine (machine-config 2).
    /// </summary>
    public const string LineLongerThanTheMachineTakes = "CMP021";

    /// <summary>
    /// CMP022, a WARNING: a comment holds a character outside the comment_charset of the machine that has no
    /// transliteration (machine-config 2).
    /// </summary>
    public const string CommentCharacterOutsideCharset = "CMP022";
}
