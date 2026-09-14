using Ncx.Core.Geometry;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The cycle row of virtual machine 2.6: the name of the active cycle, its drilling axis and its parameter words.
/// Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class CycleState
{
    // cycle.name starts OFF, the axis at the tool axis of the workplane, the parameters none (virtual machine 2.6).
    public CycleState(Workplane workplane)
    {
        Name = null;
        Controller = null;
        Axis = ToolAxisOf(workplane);
    }

    /// <summary>
    /// DRILL, a catalog name, or the native number of CYCLE:controller=n; null for OFF.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The controller address of a native cycle, CYCLE:HEIDENHAIN=251 (D94); null for a built-in or a catalog cycle.
    /// </summary>
    public string? Controller { get; set; }

    /// <summary>
    /// The drilling axis by its NCX name (virtual machine 3.3, D59).
    /// </summary>
    public string Axis { get; set; }

    /// <summary>
    /// The parameter words of the CYCLE block, the native ones unresolved in source order (virtual machine 2.6, D94).
    /// </summary>
    public List<Word> Parameters { get; } = [];

    /// <summary>
    /// True while a cycle is defined, cycle.name not OFF (virtual machine 3.3).
    /// </summary>
    public bool Active => Name is not null;

    /// <summary>
    /// The tool axis of a workplane, the axis perpendicular to it (language 4.2): Z for XY, Y for ZX, X for YZ, as the
    /// planes of the arc resolver have them (virtual machine 3.2).
    /// </summary>
    public static string ToolAxisOf(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.XY => Plane.XY.ToolAxis,
            Workplane.ZX => Plane.ZX.ToolAxis,
            Workplane.YZ => Plane.YZ.ToolAxis,
            _ => throw new ArgumentOutOfRangeException(nameof(workplane), workplane,
                "Not a workplane of language 4.2."),
        };
    }

    /// <summary>
    /// An immutable copy of the cycle as it is now.
    /// </summary>
    public CycleSnapshot Snapshot()
    {
        // The copy keeps the parameters as they are now: a new CYCLE replaces them all (virtual machine 2.6).
        return new CycleSnapshot
        {
            Name = Name,
            Controller = Controller,
            Axis = Axis,
            Parameters = new List<Word>(Parameters).AsReadOnly(),
        };
    }
}
