namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The spindle rows of virtual machine 2.4 for one spindle as they stood when the snapshot was taken; immutable, part
/// of the Before and After of every event (architecture 5.3).
/// </summary>
public sealed record SpindleSnapshot
{
    /// <summary>
    /// direction.
    /// </summary>
    public required SpindleDirection Direction { get; init; }

    /// <summary>
    /// rpm: the speed of RPM.
    /// </summary>
    public required decimal Rpm { get; init; }

    /// <summary>
    /// mode: rotating spindle or positioning axis.
    /// </summary>
    public required SpindleMode Mode { get; init; }

    /// <summary>
    /// orientation: the angle of ORIENT in degrees; null for none.
    /// </summary>
    public required decimal? Orientation { get; init; }

    /// <summary>
    /// syncPartner: the resource id of the spindle this one runs synchronized with (SPINDLE_SYNC); null for none.
    /// </summary>
    public required string? SyncPartner { get; init; }

    /// <summary>
    /// syncPhase: the angular offset of PHASE in degrees; null for none.
    /// </summary>
    public required decimal? SyncPhase { get; init; }

    /// <summary>
    /// css: true under CSS=ON.
    /// </summary>
    public required bool Css { get; init; }

    /// <summary>
    /// vc: the cutting speed of VC; null for none.
    /// </summary>
    public required decimal? Vc { get; init; }

    /// <summary>
    /// rpmMax: the speed limit of RPM_MAX under CSS; null for none.
    /// </summary>
    public required decimal? RpmMax { get; init; }
}
