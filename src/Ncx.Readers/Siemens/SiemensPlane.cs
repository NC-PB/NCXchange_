using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The axes of a working plane and the addresses of their arc centers (controllers siemens.md 2 and 3): X Y with I J
/// and the tool axis Z with K under G17, Z X with K I and Y with J under G18, Y Z with J K and X with I under G19.
/// </summary>
/// <param name="First">The first axis of the plane.</param>
/// <param name="Second">The second axis of the plane.</param>
/// <param name="Tool">The tool axis, perpendicular to the plane.</param>
/// <param name="FirstCenter">The center address of the first axis.</param>
/// <param name="SecondCenter">The center address of the second axis.</param>
/// <param name="ToolCenter">The center address of the tool axis.</param>
internal sealed record SiemensPlane(
    string First, string Second, string Tool, string FirstCenter, string SecondCenter, string ToolCenter)
{
    /// <summary>
    /// The plane of G17, G18 or G19.
    /// </summary>
    /// <param name="workplane">The working plane.</param>
    public static SiemensPlane Of(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => new SiemensPlane("Z", "X", "Y", "K", "I", "J"),
            Workplane.YZ => new SiemensPlane("Y", "Z", "X", "J", "K", "I"),
            _ => new SiemensPlane("X", "Y", "Z", "I", "J", "K"),
        };
    }

    /// <summary>
    /// The point of the plane where the source-side state knows both axes; null otherwise.
    /// </summary>
    /// <param name="positions">The positions by axis.</param>
    public SiemensPoint? PointOf(IReadOnlyDictionary<string, decimal> positions)
    {
        return positions.TryGetValue(First, out decimal first) && positions.TryGetValue(Second, out decimal second)
            ? new SiemensPoint(first, second)
            : null;
    }
}
