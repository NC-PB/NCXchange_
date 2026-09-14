namespace Ncx.Readers;

// The codes of the Heidenhain reader, RDR300-RDR499 (the ranges in DiagnosticCodes.cs, D98). What the reader keeps as
// RAW carries RDR001 with the reason in its message, the missing M30 before END PGM the RDR010 of the structure pass;
// the codes here are the other findings of reading a Klartext source.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR300, a WARNING: an M function that neither the controller nor a table of the machine names is written as
    /// MFUNC=n (machine-config 5, language 4.6).
    /// </summary>
    public const string HeidenhainMCodeNotNamed = "RDR300";

    /// <summary>
    /// RDR301, a WARNING: a Q parameter of a drilling cycle definition that no NCX word of its catalog entry carries is
    /// not written (controllers heidenhain.md 5, 7 rule 7; machine-config 6).
    /// </summary>
    public const string HeidenhainCycleParameterNotCarried = "RDR301";

    /// <summary>
    /// RDR302, an ERROR: CYCL CALL, CYCL CALL PAT or M99 calls a cycle, and no CYCL DEF of a machining cycle stands
    /// before it; the block is kept as RAW (controllers heidenhain.md 5).
    /// </summary>
    public const string HeidenhainCycleCallWithoutDefinition = "RDR302";
}
