using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

/// <summary>
/// The last command of one channel on a spindle or an axis of [shared]: where it stands, and the channels it has not
/// waited at a mark together with since (virtual machine 3.7, machine-config 8).
/// </summary>
internal sealed class SharedCommand
{
    /// <summary>
    /// The channel that commanded the resource.
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The resource: "spindle S1" or "axis B", the axis by the NCX name the virtual machine keeps it under.
    /// </summary>
    public required string Resource { get; init; }

    /// <summary>
    /// The block of the command.
    /// </summary>
    public required Block Block { get; init; }

    /// <summary>
    /// The file of the block, which a WARNING names (D98).
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The other channels of the job the command stands between the same two marks with: every other channel until the
    /// two wait at a mark together, or one waits for the end of the other, or starts it.
    /// </summary>
    public required HashSet<int> Unsynchronized { get; init; }

    /// <summary>
    /// The other channels this channel has been warned about on the resource since they last synchronized, so that the
    /// WARNING stands once for a pair of channels between two marks.
    /// </summary>
    public required HashSet<int> Warned { get; init; }
}
