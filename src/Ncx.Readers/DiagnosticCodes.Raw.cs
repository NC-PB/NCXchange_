namespace Ncx.Readers;

// The codes of the RAW fallback, RDR001-RDR009 (the ranges in DiagnosticCodes.cs, D98).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR001, a WARNING: a source block NCX cannot express is kept verbatim as RAW:controller or RAW:builder; it
    /// compiles only to the same controller or builder, and nothing is dropped (D5, controller-mapping 9).
    /// </summary>
    public const string KeptAsRaw = "RDR001";
}
