using Ncx.Core.Machine;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The motion rows of virtual machine 2.2: the position of every axis with its frame, feed and feed mode,
/// compensation, and the verb, tool vector, surface normal and skip of the block. Mutable; only the virtual machine
/// changes it (code-guidelines 7).
/// </summary>
internal sealed class MotionState
{
    public MotionState(MachineConfig machine)
    {
        // The verb and the skip of the block start none, the tool vector and the surface normal unknown, feed.value
        // none in the mode PER_MIN, and compensation OFF (virtual machine 2.2).
        BlockVerb = null;
        Skip = false;
        SkipNumber = null;
        ToolVector = null;
        SurfaceNormal = null;
        Feed = null;
        FeedMode = FeedMode.PerMin;
        Comp = Compensation.Off;

        // An axis whose [[axis]] entry has home starts known in the MACHINE frame at that reference point and unknown
        // in the workpiece frame; an axis without home starts unknown in every frame, its position frame UNKNOWN
        // (virtual machine 2.2, 3.4, D35, D100). home is reference point 1; home2 is only for HOME with POINT=2.
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Home is decimal home)
            {
                Position[axis.NcxName] = new AxisPosition(home, PositionFrame.Machine, Known: true);
            }
            else
            {
                Position[axis.NcxName] = AxisPosition.Unknown;
            }
        }
    }

    /// <summary>
    /// The position of each axis with its frame, by the NCX name of the axis, the name a program writes and home
    /// belongs to (machine-config 4).
    /// </summary>
    public Dictionary<string, AxisPosition> Position { get; } = new();

    /// <summary>
    /// feed.value; null for none.
    /// </summary>
    public decimal? Feed { get; set; }

    public FeedMode FeedMode { get; set; }

    public Compensation Comp { get; set; }

    /// <summary>
    /// verb (block); null for none.
    /// </summary>
    public Verb? BlockVerb { get; set; }

    /// <summary>
    /// TX, TY, TZ as written; null for unknown (D81).
    /// </summary>
    public IReadOnlyList<decimal>? ToolVector { get; set; }

    /// <summary>
    /// NX, NY, NZ as written; null for unknown (D81).
    /// </summary>
    public IReadOnlyList<decimal>? SurfaceNormal { get; set; }

    /// <summary>
    /// skip (block): true for a block with SKIP (D53).
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// skip (block): n of SKIP=n; null for a bare SKIP and for a block without one.
    /// </summary>
    public int? SkipNumber { get; set; }

    /// <summary>
    /// An immutable copy of the motion rows as they are now.
    /// </summary>
    public MotionSnapshot Snapshot()
    {
        // The copy keeps the positions and the vectors as they are now while the live motion goes on changing
        // (code-guidelines 7).
        return new MotionSnapshot
        {
            Position = new Dictionary<string, AxisPosition>(Position).AsReadOnly(),
            Feed = Feed,
            FeedMode = FeedMode,
            Comp = Comp,
            BlockVerb = BlockVerb,
            ToolVector = ToolVector is null ? null : new List<decimal>(ToolVector).AsReadOnly(),
            SurfaceNormal = SurfaceNormal is null ? null : new List<decimal>(SurfaceNormal).AsReadOnly(),
            Skip = Skip,
            SkipNumber = SkipNumber,
        };
    }
}
