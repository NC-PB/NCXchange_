namespace Ncx.Core.Geometry;

/// <summary>
/// The direction of ARC=CW and ARC=CCW, seen looking against the tool axis onto the working plane (language 4.3, the
/// G2/G3 definition; Heidenhain DR- is CW, DR+ is CCW).
/// </summary>
internal enum ArcDirection
{
    /// <summary>
    /// ARC=CW: from the second plane axis toward the first.
    /// </summary>
    Clockwise,

    /// <summary>
    /// ARC=CCW: from the first plane axis toward the second.
    /// </summary>
    Counterclockwise,
}
