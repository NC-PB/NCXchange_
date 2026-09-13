namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The frame rows of virtual machine 2.1 as they stood when the snapshot was taken; immutable, part of the Before and
/// After of every event (architecture 5.3).
/// </summary>
public sealed record FrameSnapshot
{
    /// <summary>
    /// units: UNKNOWN until UNITS.
    /// </summary>
    public required Units Units { get; init; }

    /// <summary>
    /// workplane.
    /// </summary>
    public required Workplane Workplane { get; init; }

    /// <summary>
    /// origin: the workpiece datum number of ORIGIN.
    /// </summary>
    public required int Origin { get; init; }

    /// <summary>
    /// The transform chain in program order, the first entry first (D31).
    /// </summary>
    public required IReadOnlyList<TransformEntry> Chain { get; init; }

    /// <summary>
    /// The setpos shift of each axis by its NCX name (virtual machine 3.4, D55).
    /// </summary>
    public required IReadOnlyDictionary<string, decimal> SetposShift { get; init; }

    /// <summary>
    /// The axes whose setpos shift SETPOS recorded against the machine position, each with the sum of the SHIFT
    /// entries of the chain on that axis at the SETPOS (virtual machine 3.4, D101).
    /// </summary>
    public required IReadOnlyDictionary<string, decimal> SetposAgainstMachine { get; init; }

    /// <summary>
    /// diameter: true under DIAMETER=ON.
    /// </summary>
    public required bool Diameter { get; init; }

    /// <summary>
    /// cylinder: the reference radius of CYLINDER=n; null for OFF (D96).
    /// </summary>
    public required decimal? Cylinder { get; init; }

    /// <summary>
    /// polar: true under POLAR=ON.
    /// </summary>
    public required bool Polar { get; init; }

    /// <summary>
    /// tcpm: true under TCPM=ON.
    /// </summary>
    public required bool Tcpm { get; init; }

    /// <summary>
    /// rotary path (D86).
    /// </summary>
    public required RotaryPath RotaryPath { get; init; }

    /// <summary>
    /// rotary feed (D86).
    /// </summary>
    public required RotaryFeed RotaryFeed { get; init; }

    /// <summary>
    /// tolerance, rotary tolerance and tolerance mode (D85).
    /// </summary>
    public required ToleranceState Tolerance { get; init; }

    /// <summary>
    /// workpiece holder: the resource id of the holder the program machines (D57); null when the machine names none.
    /// </summary>
    public required string? WorkpieceHolder { get; init; }

    /// <summary>
    /// frame (block): true while the block moves in machine coordinates, FRAME=MACHINE (D35).
    /// </summary>
    public required bool MachineFrameBlock { get; init; }
}
