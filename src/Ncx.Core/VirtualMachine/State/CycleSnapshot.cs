using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The cycle row of virtual machine 2.6 as it stood when the snapshot was taken; immutable, part of the Before and
/// After of every event (architecture 5.3).
/// </summary>
public sealed record CycleSnapshot
{
    /// <summary>
    /// cycle.name: DRILL, a catalog name, or the native number of CYCLE:controller=n; null for OFF.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    /// The controller address of a native cycle, HEIDENHAIN of CYCLE:HEIDENHAIN=251 (D94); null for a built-in or a
    /// catalog cycle.
    /// </summary>
    public required string? Controller { get; init; }

    /// <summary>
    /// cycle.axis: the drilling axis by its NCX name.
    /// </summary>
    public required string Axis { get; init; }

    /// <summary>
    /// cycle.parameters: the parameter words of the CYCLE block, the native ones in source order (D94).
    /// </summary>
    public required IReadOnlyList<Word> Parameters { get; init; }

    /// <summary>
    /// True while a cycle is defined, cycle.name not OFF (virtual machine 3.3).
    /// </summary>
    public bool Active => Name is not null;
}
