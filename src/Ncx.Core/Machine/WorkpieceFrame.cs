namespace Ncx.Core.Machine;

/// <summary>
/// [workpiece] ROLE_frame: how the machine programs the side of a holder whose Z runs the other way (machine-config 5,
/// D57).
/// </summary>
public enum WorkpieceFrame
{
    /// <summary>
    /// SUB_frame = "datum": the datum of that side runs Z the other way and the compiler negates Z.
    /// </summary>
    Datum,

    /// <summary>
    /// SUB_frame = "mirror": the compiler writes the mirror cycle of ROLE_mirror.
    /// </summary>
    Mirror,
}
