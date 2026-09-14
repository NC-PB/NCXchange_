namespace Ncx.Readers.Heidenhain;

/// <summary>
/// Two coordinates in the working plane, the first and the second axis of the plane (X and Y under a Z tool axis): the
/// pole of CC, a point of a PATTERN DEF, or the direction a contour element ends in (controllers heidenhain.md 2, 5).
/// </summary>
/// <param name="First">The coordinate of the first axis of the plane.</param>
/// <param name="Second">The coordinate of the second axis of the plane.</param>
internal sealed record HeidenhainPoint(decimal First, decimal Second);
