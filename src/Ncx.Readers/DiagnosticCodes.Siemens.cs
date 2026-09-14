namespace Ncx.Readers;

// The codes of the Siemens reader, RDR500-RDR699 (the ranges in DiagnosticCodes.cs, D98). What the reader keeps as a
// whole RAW block carries RDR001 with the reason in its message, a program without an end the RDR010 of the structure
// pass; the codes here are the other findings of reading a SINUMERIK source.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR500, a WARNING: an M function that neither the controller nor a table of the machine names is written as
    /// MFUNC=n (machine-config 5, language 4.6).
    /// </summary>
    public const string SiemensMCodeNotNamed = "RDR500";

    /// <summary>
    /// RDR501, a WARNING: words of a block that NCX has no meaning for, the path control and orientation words of the
    /// G groups, the direction of DC(), ACP() and ACN(), are kept as a RAW:SIEMENS word of the block, verbatim, and
    /// compile only to Siemens (controllers siemens.md 2, 11 rule 8; controller-mapping 2 and 9; D5).
    /// </summary>
    public const string SiemensWordsKeptAsRaw = "RDR501";

    /// <summary>
    /// RDR502, a WARNING: a value of a cycle call or of CYCLE800 that no NCX word carries is not written (controllers
    /// siemens.md 4, 7; machine-config 6).
    /// </summary>
    public const string SiemensValueNotCarried = "RDR502";

    /// <summary>
    /// RDR503, a WARNING: tool vector words stand under ORIMKS, where the vector means the machine axes, while the NCX
    /// vector is meant in the workpiece frame (controller-mapping 2, tool vectors; D81).
    /// </summary>
    public const string SiemensVectorInMachineSystem = "RDR503";

    /// <summary>
    /// RDR504, an ERROR: the end of a structure, ELSE, ENDIF, ENDWHILE, ENDFOR, ENDLOOP or UNTIL, that no structure of
    /// its section opened; the block is kept as RAW (controllers siemens.md 8; controller-mapping 6).
    /// </summary>
    public const string SiemensStructureEndWithoutBegin = "RDR504";

    /// <summary>
    /// RDR505, a WARNING: an IF, WHILE, FOR, LOOP or REPEAT that no end closes before the end of its section; its jump
    /// back or its end label is missing (controllers siemens.md 8).
    /// </summary>
    public const string SiemensStructureNotClosed = "RDR505";
}
