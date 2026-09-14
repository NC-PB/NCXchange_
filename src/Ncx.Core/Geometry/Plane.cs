namespace Ncx.Core.Geometry;

/// <summary>
/// A working plane of ARC (virtual machine 3.2): its two plane axes and its tool axis, by the names NCX gives the
/// axes. An arc is resolved in plane coordinates, a <see cref="Vec3"/> with X along the first plane axis, Y along the
/// second and Z along the tool axis. In every plane ARC=CCW turns from the first plane axis toward the second, seen
/// looking against the tool axis onto the plane (language 4.3, the G2/G3 definition).
/// </summary>
/// <param name="Name">The plane as the specification names it: XY, ZX, YZ, POLAR, CYLINDER.</param>
/// <param name="FirstAxis">The first plane axis, where CCW turns from.</param>
/// <param name="SecondAxis">The second plane axis, where CCW turns toward.</param>
/// <param name="ToolAxis">The axis perpendicular to the plane; a word on it makes an arc a helix.</param>
internal sealed record Plane(string Name, string FirstAxis, string SecondAxis, string ToolAxis)
{
    // WORKPLANE=XY is G17: X then Y, with the tool axis Z perpendicular to them (language 4.2).
    public static Plane XY { get; } = new("XY", "X", "Y", "Z");

    // WORKPLANE=ZX and YZ use the G18/G19 axis orientation for the direction (virtual machine 3.2): Z then X with the
    // tool axis Y, Y then Z with the tool axis X. Both keep X, Y and Z in their cyclic order, so that the first axis,
    // the second axis and the tool axis are right-handed like X, Y and Z and CCW is the same turn against each tool
    // axis.
    public static Plane ZX { get; } = new("ZX", "Z", "X", "Y");

    public static Plane YZ { get; } = new("YZ", "Y", "Z", "X");

    // Under POLAR=ON the working plane is the face plane of the X word (a diameter under DIAMETER=ON, halved before it
    // reaches the plane, D60) and the C word as a Cartesian length in the active units. The direction is seen looking
    // against the tool axis onto the face with X as the first and C as the second plane axis (the G12.1 and TRANSMIT
    // convention), independent of WORKPLANE; the tool axis is the one the face is seen along, Z (virtual machine 3.1,
    // 3.2, D102).
    public static Plane Polar { get; } = new("POLAR", "X", "C", "Z");

    // Under CYLINDER=n the working plane is the cylinder axis (Z on a lathe) and the C word as a length on the
    // circumference, X staying a workpiece coordinate. The direction is seen looking onto the developed surface with
    // the cylinder axis first and C second (G7.1, TRACYL), independent of WORKPLANE; the tool axis is the radial one,
    // X (virtual machine 3.1, 3.2, 3.4, D102).
    // TODO(question): the documents name the cylinder axis for a lathe only (Z); for a machine whose cylinder turns
    // about another axis (an A axis on a mill) neither the cylinder axis nor the rotary word is said, so this is the
    // lathe's plane (D146).
    public static Plane Cylinder { get; } = new("CYLINDER", "Z", "C", "X");
}
