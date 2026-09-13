namespace Ncx.Core.Model;

// The codes of the lexer, the parser and the structure pass, PAR001-PAR099 (the ranges in DiagnosticCodes.cs, D98):
// PAR001-PAR009 the lexical rules of language 3, PAR010-PAR019 the block rules of language 5 and the unknown key,
// PAR020-PAR039 the file structure of language 4.1, 4.13 and the structural part of virtual machine 5. A word whose
// address or value its catalog entry does not accept keeps the codes of the catalog, PAR150-PAR154, also for the two
// kinds of word without an entry, the machine axis words of D93 and the native parameters of D94.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// PAR001, a WARNING: a line of the file ends with LF where the first line break of the file is CRLF, or the other
    /// way round; the file keeps the line ending of its first line break (language 3, Encoding).
    /// </summary>
    public const string MixedLineEndings = "PAR001";

    /// <summary>
    /// PAR002: a word that is not KEY, KEY=VALUE or KEY:ADDR=VALUE, such as 1X=2 or X:=1 (language 3, Word, KEY,
    /// ADDR).
    /// </summary>
    public const string MalformedWord = "PAR002";

    /// <summary>
    /// PAR003: a value that is none of the value types of language 3, such as X=1. or F=+200 or X= without a value.
    /// </summary>
    public const string MalformedValue = "PAR003";

    /// <summary>
    /// PAR004: a string whose closing quote is missing (language 3, string).
    /// </summary>
    public const string UnclosedString = "PAR004";

    /// <summary>
    /// PAR005: a backslash in a string that escapes neither a quote nor a backslash (language 3, string).
    /// </summary>
    public const string InvalidStringEscape = "PAR005";

    /// <summary>
    /// PAR006: an expression whose closing brace is missing (language 3, expression).
    /// </summary>
    public const string UnclosedExpression = "PAR006";

    /// <summary>
    /// PAR007: a word starting with @ in a user file, "pseudo-word in a user file" (language 3, KEY; D95).
    /// </summary>
    public const string PseudoWordInUserFile = "PAR007";

    /// <summary>
    /// PAR008: an integer or a decimal too large to be kept as a number (language 3, integer, decimal).
    /// </summary>
    public const string NumberOutOfRange = "PAR008";

    /// <summary>
    /// PAR010: a key the catalog does not know that is neither a machine axis word (D93) nor a native parameter of a
    /// CYCLE:controller=n block (D94); the block keeps its source text (virtual machine 3 step 1, architecture 4.1).
    /// </summary>
    public const string UnknownKey = "PAR010";

    /// <summary>
    /// PAR011: a second verb in a block; a block has at most one (language 5 rule 1).
    /// </summary>
    public const string TwoVerbs = "PAR011";

    /// <summary>
    /// PAR012: an axis word in a block without a verb that carries axis words, the machine axis words of D93 among
    /// them; RETRACT carries none, and SHIFT, TILT, TILT_AXIS and SETPOS carry the axis names only, no CENTER, R,
    /// ANGLE, TX TY TZ or NX NY NZ (language 5 rule 2, 4.2).
    /// </summary>
    public const string AxisWordWithoutVerb = "PAR012";

    /// <summary>
    /// PAR013: a word in a HOME block that is not a bare axis name, such as HOME X=0 or HOME IX (language 5 rule 2,
    /// 4.3).
    /// </summary>
    public const string HomeTakesBareAxisNames = "PAR013";

    /// <summary>
    /// PAR014: an axis word without a value outside a HOME block, such as LINE X (language 5 rule 2, 4.3; D93).
    /// </summary>
    public const string AxisWordNeedsValue = "PAR014";

    /// <summary>
    /// PAR015: a key written twice in a block with the same address (language 5 rule 4).
    /// </summary>
    public const string DuplicateKey = "PAR015";

    /// <summary>
    /// PAR016: IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT or PHASE without its verb or partner word in the block
    /// (language 5 rule 5).
    /// </summary>
    public const string PartnerWordMissing = "PAR016";

    /// <summary>
    /// PAR020: the first block of the file is not FILE=BEGIN, or FILE=BEGIN stands on another block (language 4.1,
    /// virtual machine 5).
    /// </summary>
    public const string FileBeginNotFirstBlock = "PAR020";

    /// <summary>
    /// PAR021: the FILE=BEGIN block has no NCX version word (language 3 EBNF, 4.1, virtual machine 5).
    /// </summary>
    public const string NcxVersionMissing = "PAR021";

    /// <summary>
    /// PAR022: NCX names a format version other than 1 (language 4.1).
    /// </summary>
    public const string NcxVersionUnknown = "PAR022";

    /// <summary>
    /// PAR023: NCX in a block other than FILE=BEGIN (language 4.1, virtual machine 5).
    /// </summary>
    public const string NcxMisplaced = "PAR023";

    /// <summary>
    /// PAR024: the last block of the file is not FILE=END, or a block follows FILE=END (language 4.1, virtual
    /// machine 5).
    /// </summary>
    public const string FileEndNotLastBlock = "PAR024";

    /// <summary>
    /// PAR025: a block of the file frame or of a section frame holds a word its grammar does not give it, such as
    /// FILE=END UNITS=MM (language 3 EBNF, 4.1).
    /// </summary>
    public const string StructuralBlockOtherWord = "PAR025";

    /// <summary>
    /// PAR026: a block outside every program and subprogram (language 4.13, virtual machine 3.6, 5).
    /// </summary>
    public const string BlockOutsideSection = "PAR026";

    /// <summary>
    /// PAR027: SUB=BEGIN inside a program; subprograms stand next to the programs, never inside one (language 4.9,
    /// 4.13, virtual machine 3.6, 5).
    /// </summary>
    public const string SubInsideProgram = "PAR027";

    /// <summary>
    /// PAR028: a file without a program (language 4.1, 4.13, virtual machine 5).
    /// </summary>
    public const string FileWithoutProgram = "PAR028";

    /// <summary>
    /// PAR029: two sections of the file with the same name (virtual machine 3.6, 5).
    /// </summary>
    public const string DuplicateSectionName = "PAR029";

    /// <summary>
    /// PAR030: a program without its PROGRAM=END (language 4.13, virtual machine 5).
    /// </summary>
    public const string ProgramEndMissing = "PAR030";

    /// <summary>
    /// PAR031: PROGRAM=END outside a program, a second PROGRAM=END among them (language 4.13, virtual machine 5).
    /// </summary>
    public const string ProgramEndOutsideProgram = "PAR031";

    /// <summary>
    /// PAR032: a subprogram without its SUB=END (language 4.13, virtual machine 5).
    /// </summary>
    public const string SubEndMissing = "PAR032";

    /// <summary>
    /// PAR033: SUB=END outside a subprogram (language 4.13, virtual machine 5).
    /// </summary>
    public const string SubEndOutsideSub = "PAR033";

    /// <summary>
    /// PAR034: SUB=BEGIN without NAME (language 3 EBNF, 4.9, 4.13).
    /// </summary>
    public const string SubNameMissing = "PAR034";

    /// <summary>
    /// PAR035: LABEL=END; END is reserved for JUMP=END (language 4.9, virtual machine 2.7, 5).
    /// </summary>
    public const string LabelEnd = "PAR035";
}
