namespace Ncx.Readers;

// The codes of the reader's structure pass, RDR010-RDR029 (the ranges in DiagnosticCodes.cs, D98).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR010, a WARNING: a program of the source has no end (no M30, M2 or loop back to its start); the reader writes
    /// PROGRAM=END after its last block, because every program ends with it (language 4.13; controller-mapping 1, the
    /// missing M30 of Klartext).
    /// </summary>
    public const string ProgramEndMissing = "RDR010";

    /// <summary>
    /// RDR011, a WARNING: a subprogram of the source has no return (no M99, LBL 0 or RET); the reader writes SUB=END
    /// after its last block, because every subprogram ends with it (language 4.13).
    /// </summary>
    public const string SubReturnMissing = "RDR011";
}
