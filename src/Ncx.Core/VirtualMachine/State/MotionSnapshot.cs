namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The motion rows of virtual machine 2.2 as they stood when the snapshot was taken; immutable, part of the Before and
/// After of every event (architecture 5.3).
/// </summary>
public sealed record MotionSnapshot
{
    /// <summary>
    /// The position of each axis with its frame, by the NCX name of the axis.
    /// </summary>
    public required IReadOnlyDictionary<string, AxisPosition> Position { get; init; }

    /// <summary>
    /// feed.value: the F of the program in the feed mode; null for none.
    /// </summary>
    public required decimal? Feed { get; init; }

    /// <summary>
    /// feed.mode.
    /// </summary>
    public required FeedMode FeedMode { get; init; }

    /// <summary>
    /// compensation.
    /// </summary>
    public required Compensation Comp { get; init; }

    /// <summary>
    /// verb (block): the verb of the block; null for none.
    /// </summary>
    public required Verb? BlockVerb { get; init; }

    /// <summary>
    /// tool vector: TX, TY, TZ of a LINE under TCPM=ON as written; null for unknown (D81).
    /// </summary>
    public required IReadOnlyList<decimal>? ToolVector { get; init; }

    /// <summary>
    /// surface normal: NX, NY, NZ of that LINE as written; null for unknown (D81).
    /// </summary>
    public required IReadOnlyList<decimal>? SurfaceNormal { get; init; }

    /// <summary>
    /// skip (block): true for a block with SKIP (D53).
    /// </summary>
    public required bool Skip { get; init; }

    /// <summary>
    /// skip (block): the switch number n of SKIP=n; null for a bare SKIP and for a block without one.
    /// </summary>
    public required int? SkipNumber { get; init; }
}
