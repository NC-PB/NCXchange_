namespace Ncx.Core.Machine;

/// <summary>
/// [positions]: named positions in machine coordinates, each a set of axis values, tool_change = { X = 0, Z = -120 }.
/// Expansion rules reach them through {position:NAME} (machine-config 4, 5a, D100).
/// </summary>
public sealed record PositionsTable
{
    /// <summary>
    /// Every named position with its axis values by NCX axis name, in file order.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> Entries { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

    /// <summary>
    /// The axis values of a named position, X = 0 and Z = -120 for tool_change; null when the file names no such
    /// position.
    /// </summary>
    /// <param name="name">The name of the position, tool_change.</param>
    public IReadOnlyDictionary<string, decimal>? Find(string name)
    {
        return Entries.TryGetValue(name, out IReadOnlyDictionary<string, decimal>? axes) ? axes : null;
    }
}
