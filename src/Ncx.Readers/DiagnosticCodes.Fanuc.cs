namespace Ncx.Readers;

// The codes of the Fanuc reader, RDR100-RDR299 (the ranges in DiagnosticCodes.cs, D98). What the reader keeps as RAW
// carries RDR001 with the reason in its message; the codes here are the other findings of reading a Fanuc source.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR100, a WARNING: an M code that neither the controller nor a table of the machine names is written as
    /// MFUNC=n (machine-config 5, language 4.6).
    /// </summary>
    public const string FanucMCodeNotNamed = "RDR100";

    /// <summary>
    /// RDR101, an ERROR: M6 changes to the preloaded tool, and nothing is preloaded; an error in the source
    /// (controller-mapping 3, controllers fanuc.md 5).
    /// </summary>
    public const string FanucChangeWithoutPreload = "RDR101";

    /// <summary>
    /// RDR102, an ERROR: a T word after the M6 of the same block, T4 M6 T5, which is illegal on most controls; the
    /// block is kept as RAW (controller-mapping 3, controllers fanuc.md 5).
    /// </summary>
    public const string FanucToolAfterChange = "RDR102";

    /// <summary>
    /// RDR103, an ERROR: an END of a WHILE loop that no DO with its number opened; the block is kept as RAW
    /// (controllers fanuc.md 7, controller-mapping 6).
    /// </summary>
    public const string FanucLoopEndWithoutDo = "RDR103";

    /// <summary>
    /// RDR104, a WARNING: a DO of a WHILE loop that no END with its number closes before the end of its program or
    /// subprogram; the loop head is written, the jump back is missing (controllers fanuc.md 7).
    /// </summary>
    public const string FanucLoopNotClosed = "RDR104";

    /// <summary>
    /// RDR105, a WARNING: a drilling cycle under G98 returns to the initial level, and the reader does not know where
    /// the drilling axis stood; CYCLE_RETRACT=SAFE is written without SAFE (controller-mapping 5, language 4.7).
    /// </summary>
    public const string FanucInitialLevelUnknown = "RDR105";
}
