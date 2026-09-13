namespace Ncx.Core.Machine;

/// <summary>
/// The kinematic tree of the machine: a flat node list with parents, resources as leaves, read only by the kinematics
/// module; the loader keeps it and does not interpret it (machine-config 9).
/// </summary>
public sealed record KinematicTree
{
    /// <summary>
    /// [machine] kinematics: the second TOML file that holds a long tree, "dmu50.kin.toml"; null when left out.
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// [[node]]: the nodes of the machine file, in file order.
    /// </summary>
    public IReadOnlyList<KinematicNode> Nodes { get; init; } = [];
}
