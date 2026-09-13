namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The spindle rows of virtual machine 2.4 for one spindle resource: direction, speed, mode, orientation,
/// synchronization and constant surface speed. Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class SpindleState
{
    // direction starts OFF and rpm 0, mode SPINDLE, orientation none, syncPartner and syncPhase none, css OFF, vc and
    // rpmMax none (virtual machine 2.4).
    public SpindleState()
    {
        Direction = SpindleDirection.Off;
        Rpm = 0m;
        Mode = SpindleMode.Spindle;
        Orientation = null;
        SyncPartner = null;
        SyncPhase = null;
        Css = false;
        Vc = null;
        RpmMax = null;
    }

    public SpindleDirection Direction { get; set; }

    public decimal Rpm { get; set; }

    public SpindleMode Mode { get; set; }

    /// <summary>
    /// The angle of ORIENT in degrees; null for none.
    /// </summary>
    public decimal? Orientation { get; set; }

    /// <summary>
    /// The resource id of the spindle this one runs synchronized with; null for none.
    /// </summary>
    public string? SyncPartner { get; set; }

    /// <summary>
    /// The angular offset of PHASE in degrees; null for none.
    /// </summary>
    public decimal? SyncPhase { get; set; }

    public bool Css { get; set; }

    /// <summary>
    /// The cutting speed of VC; null for none.
    /// </summary>
    public decimal? Vc { get; set; }

    /// <summary>
    /// The speed limit of RPM_MAX; null for none.
    /// </summary>
    public decimal? RpmMax { get; set; }

    /// <summary>
    /// An immutable copy of the spindle as it is now.
    /// </summary>
    public SpindleSnapshot Snapshot()
    {
        return new SpindleSnapshot
        {
            Direction = Direction,
            Rpm = Rpm,
            Mode = Mode,
            Orientation = Orientation,
            SyncPartner = SyncPartner,
            SyncPhase = SyncPhase,
            Css = Css,
            Vc = Vc,
            RpmMax = RpmMax,
        };
    }
}
