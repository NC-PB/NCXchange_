using System.Globalization;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// SYNC_WAIT and SYNC_RELEASE (virtual machine 3.7, 7): raised by the job scheduler when a channel waits at a mark and
/// when the channels waiting at it are released. A single-channel run does not wait at a SYNC (3.7) and raises
/// neither.
/// </summary>
/// <remarks>
/// Declared with the other events; the job scheduler of phase 6 (P6-01) raises it.
/// </remarks>
public sealed record SyncEvent : VmEvent
{
    /// <summary>
    /// The mark m of SYNC=m.
    /// </summary>
    public required int Mark { get; init; }

    /// <summary>
    /// The channels that take part, WITH or all channels of the job (language 4.8).
    /// </summary>
    public required IReadOnlyList<int> Channels { get; init; }

    /// <summary>
    /// The round of the scheduler in which the channel waits or the channels are released (virtual machine 3.7).
    /// </summary>
    public required int Round { get; init; }

    /// <summary>
    /// False for SYNC_WAIT, true for SYNC_RELEASE.
    /// </summary>
    public required bool Released { get; init; }

    /// <inheritdoc/>
    public override string Kind => Released ? "SYNC_RELEASE" : "SYNC_WAIT";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        var channels = new List<string>();
        foreach (int channel in Channels)
        {
            channels.Add(channel.ToString(CultureInfo.InvariantCulture));
        }

        return $"mark {Mark.ToString(CultureInfo.InvariantCulture)}, channels {string.Join(",", channels)}, "
            + $"round {Round.ToString(CultureInfo.InvariantCulture)}";
    }
}
