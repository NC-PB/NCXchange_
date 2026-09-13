namespace Ncx.Core.Model;

// The codes of the expander and of the restore stack of the virtual machine, VM650-VM749 (the ranges in
// DiagnosticCodes.cs, D98): VM650-VM669 the generated text of the expansion rules and the program rewriters
// (machine-config 5a, architecture 5.5, D100), VM670-VM679 the limits = "clamp" rewrite (D64), VM680-VM699 the restore
// stack of @SAVE and @RESTORE (virtual machine 3.10, D95). A pseudo-word in a block of the file keeps PAR007, the one
// code of that rule, and a generated text that does not parse keeps the code the parser gives it.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM650: {position:NAME} in a pre or post block of an expansion rule names no entry of [positions]; an ERROR on
    /// the rule, reported once per rule text (machine-config 5a, architecture 5.5, D100).
    /// </summary>
    public const string UnknownPosition = "VM650";

    /// <summary>
    /// VM651: a text of an expansion rule or a program rewriter that holds no block, a blank or a comment-only line
    /// (language 3, Block; machine-config 5a).
    /// </summary>
    public const string GeneratedTextWithoutBlock = "VM651";

    /// <summary>
    /// VM652: generated blocks that would stand outside every program and subprogram, before a FILE=BEGIN,
    /// PROGRAM=BEGIN or SUB=BEGIN block or after an END block, or a rewrite of such a block (language 4.1, 4.13).
    /// </summary>
    public const string GeneratedBlockOutsideSection = "VM652";

    /// <summary>
    /// VM653: a generated text with FILE, NCX, PROGRAM or SUB, the words that open and close the file, a program and a
    /// subprogram exactly once (language 4.1, 4.13).
    /// </summary>
    public const string GeneratedFrameWord = "VM653";

    /// <summary>
    /// VM670, a WARNING: under limits = "clamp" the expander rewrote RPM, F or a target beyond a machine limit to the
    /// limit (machine-config 1, virtual machine 5, D64).
    /// </summary>
    public const string LimitClamped = "VM670";

    /// <summary>
    /// VM680: @RESTORE of a state variable for which the restore stack holds no value that @SAVE saved (virtual machine
    /// 3.10).
    /// </summary>
    public const string NothingSaved = "VM680";

    /// <summary>
    /// VM681: @SAVE or @RESTORE with a state key that names no state variable the restore stack keeps (virtual machine
    /// 3.10).
    /// </summary>
    public const string StateKeyNotKept = "VM681";
}
