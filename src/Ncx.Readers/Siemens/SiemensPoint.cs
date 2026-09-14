namespace Ncx.Readers.Siemens;

/// <summary>
/// A point or a direction in the working plane, its first and its second axis: X and Y under G17, Z and X under G18, Y
/// and Z under G19 (controllers siemens.md 2, group 6).
/// </summary>
/// <param name="First">The coordinate of the first axis of the plane.</param>
/// <param name="Second">The coordinate of the second axis of the plane.</param>
internal readonly record struct SiemensPoint(decimal First, decimal Second);
