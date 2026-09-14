using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The motion whose time waits for the next one: in continuous mode its exit speed is the corner speed toward the
/// motion that follows, known only when that motion arrives (virtual machine 8).
/// </summary>
internal sealed record PendingMotion
{
    /// <summary>
    /// The MOTION event.
    /// </summary>
    public required MotionEvent Motion { get; init; }

    /// <summary>
    /// Its length in mm.
    /// </summary>
    public required double Length { get; init; }

    /// <summary>
    /// The speed it travels at in mm/s: the rapid rate, or the commanded feed limited by max_feed.
    /// </summary>
    public required double Speed { get; init; }

    /// <summary>
    /// The speed it enters with in mm/s.
    /// </summary>
    public required double Entry { get; init; }

    /// <summary>
    /// The smallest acceleration of its moving axes in mm/s^2; null when none gives one.
    /// </summary>
    public double? Acceleration { get; init; }

    /// <summary>
    /// The SECTION it stood in.
    /// </summary>
    public string? Section { get; init; }
}
