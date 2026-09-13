using System.Text;
using Ncx.Core.Geometry;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// MOTION (virtual machine 7 as amended by F18): every RAPID, LINE, ARC and RETRACT, and each motion of an expanded
/// CYCLE_CALL (3.3, D37), with its verb, where it starts and ends, the arc's center, direction and sweep, the tool vector
/// and surface normal, feed, compensation, frame and length.
/// </summary>
public sealed record MotionEvent : VmEvent
{
    /// <summary>
    /// RAPID, LINE, ARC, RETRACT or HOME; RAPID or LINE for a motion of an expanded cycle.
    /// </summary>
    public required Verb Verb { get; init; }

    /// <summary>
    /// The position of every axis where the motion starts, as the position store holds it (virtual machine 2.2).
    /// </summary>
    public required IReadOnlyDictionary<string, AxisPosition> From { get; init; }

    /// <summary>
    /// The position of every axis where the motion ends (virtual machine 2.2).
    /// </summary>
    public required IReadOnlyDictionary<string, AxisPosition> To { get; init; }

    /// <summary>
    /// The center of an ARC on its two plane axes, given or computed from R, as the position store would hold it
    /// (virtual machine 3.2); null for any other motion and for an arc that did not resolve.
    /// </summary>
    public IReadOnlyDictionary<string, AxisPosition>? Center { get; init; }

    /// <summary>
    /// CW or CCW of an ARC (language 4.3); null for any other motion.
    /// </summary>
    public ArcDirection? Direction { get; init; }

    /// <summary>
    /// The sweep of an ARC in degrees in its direction, 360 for a full circle and more for an ANGLE of several turns
    /// (virtual machine 3.2, D84); null for any other motion and for an arc that did not resolve.
    /// </summary>
    public decimal? Sweep { get; init; }

    /// <summary>
    /// tool vector: TX, TY, TZ under TCPM=ON; null while unknown (virtual machine 2.2, D81).
    /// </summary>
    public IReadOnlyList<decimal>? ToolVector { get; init; }

    /// <summary>
    /// surface normal: NX, NY, NZ under TCPM=ON; null while unknown (virtual machine 2.2, D81).
    /// </summary>
    public IReadOnlyList<decimal>? SurfaceNormal { get; init; }

    /// <summary>
    /// feed.value the motion moves at: the active F of a LINE, ARC or RETRACT, CYCLE_F of a feed move of an expanded
    /// cycle; null for RAPID and HOME, which move at rapid (virtual machine 8), and for a feed that is none or unknown.
    /// </summary>
    public decimal? Feed { get; init; }

    /// <summary>
    /// feed.mode of the feed (virtual machine 2.2).
    /// </summary>
    public required FeedMode FeedMode { get; init; }

    /// <summary>
    /// compensation (virtual machine 2.2).
    /// </summary>
    public required Compensation Comp { get; init; }

    /// <summary>
    /// frame (block): MACHINE for a FRAME=MACHINE block and a HOME, which move in machine coordinates, WORKPIECE
    /// otherwise (virtual machine 2.1, 3 step 5, 3.4).
    /// </summary>
    public required PositionFrame Frame { get; init; }

    /// <summary>
    /// The length of the motion in the active units: euclidean over the linear axes, the arc length for an arc with
    /// the travel of a helix (virtual machine 8); null when an axis the motion moved is not known at both ends in one
    /// frame.
    /// </summary>
    public decimal? Length { get; init; }

    /// <inheritdoc/>
    public override string Kind => "MOTION";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        var text = new StringBuilder(EventText.Ident(Verb));
        if (Direction is ArcDirection direction)
        {
            text.Append(' ').Append(EventText.Ident(direction));
        }

        text.Append(' ').Append(EventText.Moves(From, To)).Append("; ");
        var parts = new List<string>();
        if (Center is not null)
        {
            parts.Add("center " + EventText.KnownPositions(Center));
        }

        if (Sweep is decimal sweep)
        {
            parts.Add("sweep " + EventText.Number(sweep));
        }

        if (ToolVector is not null)
        {
            parts.Add("tool vector " + EventText.Vector(ToolVector));
        }

        if (SurfaceNormal is not null)
        {
            parts.Add("surface normal " + EventText.Vector(SurfaceNormal));
        }

        if (Feed is decimal feed)
        {
            parts.Add("feed " + EventText.Number(feed) + " " + EventText.Ident(FeedMode));
        }

        parts.Add("comp " + EventText.Ident(Comp));
        parts.Add("frame " + EventText.Frame(Frame));
        parts.Add("length " + EventText.Number(Length));
        return text.Append(string.Join(", ", parts)).ToString();
    }
}
