using Ncx.Core.Geometry;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// An arc as the virtual machine resolved it (virtual machine 3.2): its working plane and the arc in the coordinates of
/// that plane, start, end, center, radius and sweep, which the MOTION event carries so that the compiler can write the
/// CENTER form, the R form, or full turns and a rest (7, D84).
/// </summary>
/// <param name="Plane">The working plane: XY, ZX, YZ, or the polar or the cylinder plane of D102.</param>
/// <param name="Arc">The arc in plane coordinates: X along the first plane axis, Y along the second, Z along the tool
/// axis.</param>
internal sealed record PlaneArc(Plane Plane, Arc Arc);
