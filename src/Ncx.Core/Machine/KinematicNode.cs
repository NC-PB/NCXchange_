namespace Ncx.Core.Machine;

/// <summary>
/// One [[node]] of the kinematic tree, kept as written for the kinematics module (machine-config 9).
/// </summary>
public sealed record KinematicNode
{
    /// <summary>
    /// id: the node, an axis id or a resource id, X1, TABLE1.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// parent: the node it hangs on, BASE; null when left out.
    /// </summary>
    public string? Parent { get; init; }

    /// <summary>
    /// type: linear or rotary, as written; null for a resource leaf.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// direction: the axis direction, [1, 0, 0]; empty when left out.
    /// </summary>
    public IReadOnlyList<decimal> Direction { get; init; } = [];
}
