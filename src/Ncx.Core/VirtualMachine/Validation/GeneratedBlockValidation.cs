using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The generated blocks (language 4.15; virtual machine 1, 3.10; machine-config 5a; architecture 5.5; D95, D100,
/// D106): the texts of the expansion rules and of the program rewriters, which the expander raises before the run, and
/// the restore stack of @SAVE and @RESTORE, which RestoreRules and VirtualMachine.Restore raise during it. The list of
/// virtual machine 5 names none of them but the pseudo-word in a user file, PAR007 of the structure; the limits =
/// "clamp" rewrite of the expander has its row with the limits in the motion family.
/// </summary>
internal static class GeneratedBlockValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Generated blocks",
        Summary = "The blocks the expander generates from the expansion rules of the machine configuration and from "
            + "the program rewriters, and the restore stack of @SAVE and @RESTORE (language 4.15; VM 1, 3.10; "
            + "machine-config 5a; architecture 5.5; D95, D100, D106). A pseudo-word in a user file is PAR007 of the "
            + "structure, and a generated text that does not parse keeps the code the parser gives it.",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.UnknownPosition,
                "{position:NAME} in a pre or post block of an expansion rule names no entry of [positions]; reported "
                + "once per rule text.", "machine-config 5a; architecture 5.5; D100")
                with { RaisedBy = "the expander" },
            ValidationRule.Error(DiagnosticCodes.GeneratedTextWithoutBlock,
                "A text of an expansion rule or a program rewriter that holds no block: a blank or a comment-only "
                + "line.", "language 3, 4.15; machine-config 5a") with { RaisedBy = "the expander" },
            ValidationRule.Error(DiagnosticCodes.GeneratedBlockOutsideSection,
                "Generated blocks before a FILE=BEGIN, PROGRAM=BEGIN or SUB=BEGIN block or after an END block, outside "
                + "every program and subprogram, or a rewrite of such a block.", "language 4.1, 4.13")
                with { RaisedBy = "the expander" },
            ValidationRule.Error(DiagnosticCodes.GeneratedFrameWord,
                "A generated text with FILE, NCX, PROGRAM or SUB, the words that open and close the file, a program "
                + "and a subprogram exactly once.", "language 4.1, 4.13") with { RaisedBy = "the expander" },
            ValidationRule.Error(DiagnosticCodes.NothingSaved,
                "@RESTORE of a state variable for which the restore stack holds no value that @SAVE saved.",
                "VM 3.10; D95"),
            ValidationRule.Error(DiagnosticCodes.StateKeyNotKept,
                "@SAVE or @RESTORE with a state key that names no state variable the restore stack keeps.",
                "VM 3.10; D95"),
        ],
    };
}
