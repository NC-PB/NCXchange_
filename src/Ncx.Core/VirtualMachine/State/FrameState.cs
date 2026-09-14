using Ncx.Core.Machine;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The frame rows of virtual machine 2.1: units, workplane, origin, the transform chain, the setpos shifts, the
/// kinematic transformations, the rotary and tolerance settings, the workpiece holder and the frame of the block.
/// Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class FrameState
{
    public FrameState(MachineConfig machine)
    {
        // units start UNKNOWN (virtual machine 2.1): the program sets them before its first motion (D11, D34).
        Units = Units.Unknown;

        // workplane and origin start from TOML, XY and 0 (virtual machine 2.1).
        // TODO(question): virtual machine 2.1 takes the start workplane and origin "from TOML (XY)" and "from TOML
        // (0)", and machine-config names no key for either; they are XY and 0 until it does.
        Workplane = Workplane.XY;
        Origin = 0;

        // The transform chain starts empty (virtual machine 2.1, D31), the setpos shift of every axis at 0 (2.1, 3.4).
        foreach (AxisDef axis in machine.Axes)
        {
            SetposShift[axis.NcxName] = 0m;
        }

        // cylinder, polar and tcpm start OFF; rotary path FULL and rotary feed DEG_MIN; tolerance OFF with no rotary
        // tolerance and the mode FINISH; the frame of the block WORKPIECE; diameter OFF (virtual machine 2.1).
        Cylinder = null;
        Polar = false;
        Tcpm = false;
        RotaryPath = RotaryPath.Full;
        RotaryFeed = RotaryFeed.DegMin;
        Tolerance = new ToleranceState { Value = null, Rotary = null, Mode = ToleranceMode.Finish };
        MachineFrameBlock = false;
        Diameter = false;

        // The workpiece holder starts at default_workpiece from TOML (virtual machine 2.1).
        WorkpieceHolder = machine.Machine.DefaultWorkpiece;
    }

    public Units Units { get; set; }

    public Workplane Workplane { get; set; }

    public int Origin { get; set; }

    /// <summary>
    /// The transform chain in program order, the first entry first (D31).
    /// </summary>
    public List<TransformEntry> Chain { get; } = [];

    /// <summary>
    /// The setpos shift of each axis by its NCX name (virtual machine 3.4, D55).
    /// </summary>
    public Dictionary<string, decimal> SetposShift { get; } = new();

    /// <summary>
    /// The setpos shifts that SETPOS recorded against the machine position, on an axis known in the MACHINE frame only,
    /// by axis: each with the sum of the SHIFT entries of the chain on that axis at the SETPOS, the SHIFT entries from
    /// an expression on it and the workpiece holder of the SETPOS (virtual machine 1, 3.4, D35, D57, D101). While the
    /// axis is known in the workpiece frame with its
    /// machine position known through the record, the machine position is the stored value plus the shifts of the
    /// chain on the axis minus that sum, and ORIGIN and a change of the frame return the axis to it. A record stands as
    /// long as its setpos shift does. ORIGIN takes it out, and so do the call of an external program and every SETPOS
    /// whose new shift is not recorded against the machine position: one from an expression, one directly after a HOME
    /// without a reference point, one on a position whose machine position is not known. Empty at the start.
    /// </summary>
    public Dictionary<string, SetposRecord> SetposAgainstMachine { get; } = new(StringComparer.Ordinal);

    public bool Diameter { get; set; }

    /// <summary>
    /// The reference radius of CYLINDER=n; null for OFF (D96).
    /// </summary>
    public decimal? Cylinder { get; set; }

    public bool Polar { get; set; }

    public bool Tcpm { get; set; }

    public RotaryPath RotaryPath { get; set; }

    public RotaryFeed RotaryFeed { get; set; }

    public ToleranceState Tolerance { get; set; }

    /// <summary>
    /// The resource id of the workpiece holder; null when the machine names no default_workpiece.
    /// </summary>
    public string? WorkpieceHolder { get; set; }

    /// <summary>
    /// frame (block): true in a block with FRAME=MACHINE (D35).
    /// </summary>
    public bool MachineFrameBlock { get; set; }

    /// <summary>
    /// An immutable copy of the frame rows as they are now.
    /// </summary>
    public FrameSnapshot Snapshot()
    {
        // The copy keeps the chain and the setpos shifts as they are now while the live frame goes on changing; the
        // entries and the tolerance are immutable and shared (code-guidelines 7).
        return new FrameSnapshot
        {
            Units = Units,
            Workplane = Workplane,
            Origin = Origin,
            Chain = new List<TransformEntry>(Chain).AsReadOnly(),
            SetposShift = new Dictionary<string, decimal>(SetposShift).AsReadOnly(),
            SetposAgainstMachine = new Dictionary<string, SetposRecord>(SetposAgainstMachine, StringComparer.Ordinal)
                .AsReadOnly(),
            Diameter = Diameter,
            Cylinder = Cylinder,
            Polar = Polar,
            Tcpm = Tcpm,
            RotaryPath = RotaryPath,
            RotaryFeed = RotaryFeed,
            Tolerance = Tolerance,
            WorkpieceHolder = WorkpieceHolder,
            MachineFrameBlock = MachineFrameBlock,
        };
    }
}
