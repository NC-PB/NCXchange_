using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The structure of the file and the words of a block (language 3, 4.1, 4.13, 5; virtual machine 3 step 1, 5): the
/// file frame, the programs and subprograms, unknown keys and values, duplicate keys, two verbs, axis words without a
/// verb, the partner words, pseudo-words, and RAW. The parser raises all of them but RAW, with its PAR codes (D98).
/// </summary>
internal static class StructureValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Structure",
        Summary = "The file frame, the programs and subprograms, and the words of a block (language 3, 4.1, 4.13, 5; "
            + "VM 3 step 1, 5). The parser reports them before the virtual machine runs.",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.MixedLineEndings,
                "A line ends with LF where the first line break of the file is CRLF, or the other way round.",
                "language 3"),
            ValidationRule.Error(DiagnosticCodes.MalformedWord,
                "A word that is not KEY, KEY=VALUE or KEY:ADDR=VALUE.", "language 3"),
            ValidationRule.Error(DiagnosticCodes.MalformedValue,
                "A value that is none of the value types: wrong value type.", "language 3; VM 3 step 1"),
            ValidationRule.Error(DiagnosticCodes.UnclosedString, "A string without its closing quote.", "language 3"),
            ValidationRule.Error(DiagnosticCodes.InvalidStringEscape,
                "A backslash in a string that escapes neither a quote nor a backslash.", "language 3"),
            ValidationRule.Error(DiagnosticCodes.UnclosedExpression, "An expression without its closing brace.",
                "language 3"),
            ValidationRule.Error(DiagnosticCodes.PseudoWordInUserFile,
                "A pseudo-word (@SAVE, @RESTORE) in a user file.", "language 3; VM 3.10, 5; D95"),
            ValidationRule.Error(DiagnosticCodes.NumberOutOfRange, "A number too large to be kept as a number.",
                "language 3"),
            ValidationRule.Error(DiagnosticCodes.UnknownKey,
                "Unknown key: neither a catalog word, nor a machine axis word, nor a native parameter of a "
                + "CYCLE:controller=n block.", "VM 3 step 1, 5; D93, D94"),
            ValidationRule.Error(DiagnosticCodes.TwoVerbs, "Two verbs in one block.", "language 5 rule 1; VM 5"),
            ValidationRule.Error(DiagnosticCodes.AxisWordWithoutVerb,
                "Axis word without verb, or under a verb that does not carry it: RETRACT with other axis words.",
                "language 5 rule 2; VM 3.1a, 5"),
            ValidationRule.Error(DiagnosticCodes.HomeTakesBareAxisNames,
                "A word in a HOME block that is not a bare axis name.", "language 4.3, 5 rule 2"),
            ValidationRule.Error(DiagnosticCodes.AxisWordNeedsValue,
                "An axis word without a value outside a HOME block.", "language 4.3, 5 rule 2; D93"),
            ValidationRule.Error(DiagnosticCodes.DuplicateKey, "Duplicate key: a key twice in one block with one "
                + "address.", "language 5 rule 4; VM 5"),
            ValidationRule.Error(DiagnosticCodes.PartnerWordMissing,
                "IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT or PHASE without its verb or partner word in the "
                + "block.", "language 5 rule 5; VM 5"),
            ValidationRule.Error(DiagnosticCodes.FileBeginNotFirstBlock,
                "Missing or misplaced FILE=BEGIN: it is the first block of the file and stands nowhere else.",
                "language 4.1; VM 5; D92"),
            ValidationRule.Error(DiagnosticCodes.NcxVersionMissing, "FILE=BEGIN without NCX, the format version.",
                "language 4.1; VM 5"),
            ValidationRule.Error(DiagnosticCodes.NcxVersionUnknown, "NCX names a format version other than 1.",
                "language 4.1"),
            ValidationRule.Error(DiagnosticCodes.NcxMisplaced, "Misplaced NCX, in a block other than FILE=BEGIN.",
                "language 4.1; VM 5"),
            ValidationRule.Error(DiagnosticCodes.FileEndNotLastBlock,
                "Missing or misplaced FILE=END: it is the last block of the file, and no block follows it.",
                "language 4.1; VM 5; D92"),
            ValidationRule.Error(DiagnosticCodes.StructuralBlockOtherWord,
                "A block of the file or section frame with a word its grammar does not give it (FILE=END UNITS=MM).",
                "language 3 EBNF, 4.1"),
            ValidationRule.Error(DiagnosticCodes.BlockOutsideSection, "A block outside every section.",
                "language 4.13; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.SubInsideProgram, "A SUB inside a PROGRAM.",
                "language 4.9, 4.13; VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.FileWithoutProgram, "A file without a program.",
                "language 4.1, 4.13; VM 5"),
            ValidationRule.Error(DiagnosticCodes.DuplicateSectionName,
                "Duplicate SUB: two sections of the file with one name.", "VM 3.6, 5"),
            ValidationRule.Error(DiagnosticCodes.ProgramEndMissing,
                "Missing PROGRAM=END: a program without it, or a PROGRAM=BEGIN before it.", "language 4.13; VM 5"),
            ValidationRule.Error(DiagnosticCodes.ProgramEndOutsideProgram,
                "Misplaced PROGRAM=END, outside a program.", "language 4.13; VM 5"),
            ValidationRule.Error(DiagnosticCodes.SubEndMissing,
                "Missing SUB=END: a subprogram without it, or another section before it.", "language 4.13; VM 5"),
            ValidationRule.Error(DiagnosticCodes.SubEndOutsideSub, "Misplaced SUB=END, outside a subprogram.",
                "language 4.13; VM 5"),
            ValidationRule.Error(DiagnosticCodes.SubNameMissing, "SUB=BEGIN without NAME.", "language 4.9, 4.13"),
            ValidationRule.Error(DiagnosticCodes.WordTakesNoAddress,
                "A word written with an address that it does not take (LINE:X).", "language 3, 4"),
            ValidationRule.Error(DiagnosticCodes.WordNeedsAddress,
                "A word that takes an address written without one (CENTER=5).", "language 4"),
            ValidationRule.Error(DiagnosticCodes.WordAddressNotAccepted,
                "An address outside the fixed set of its word (OFFSET:LENGTH).", "language 4"),
            ValidationRule.Error(DiagnosticCodes.WordValueNotAccepted,
                "Unknown value: a value its word does not accept (SPINDLE=UP).", "language 3, 4; VM 5; D96"),
            ValidationRule.Error(DiagnosticCodes.SkipSwitchOutOfRange, "SKIP=n with a switch outside 1 to 9.",
                "language 4.1"),
            ValidationRule.Warning(DiagnosticCodes.RawPresent,
                "RAW present: source text kept verbatim, which compiles only to its own controller or builder.",
                "language 4.1; VM 5"),
            new ValidationRule
            {
                Severity = Severity.Error,
                Rule = "RAW, or a CYCLE:controller=n block with its native parameters, compiled for another "
                    + "controller family.",
                Section = "language 4.1, 4.7.1; VM 5; D5, D94",
                RaisedBy = "the compiler, at compile time",
            },
        ],
    };

    /// <summary>
    /// RAW present is a WARNING (virtual machine 5): the source text NCX could not express is kept verbatim and
    /// compiles only to the controller or builder of its address; any other target is an ERROR of the compiler
    /// (language 4.1).
    /// </summary>
    public static void CheckRaw(Block block, Diagnostics diagnostics)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key != "RAW")
            {
                continue;
            }

            string raw = word.Addr is null ? word.Key : word.Key + ":" + word.Addr;
            diagnostics.Warning(block, DiagnosticCodes.RawPresent,
                $"{raw} is source text that NCX could not express, kept verbatim; it compiles only to the controller "
                + "or builder of its address (language 4.1, virtual machine 5).");
        }
    }
}
