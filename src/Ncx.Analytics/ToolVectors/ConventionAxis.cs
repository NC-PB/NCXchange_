namespace Ncx.Analytics.ToolVectors;

/// <summary>
/// A rotary axis of [[axis]] as the tool-vector convention applies it (implementation 14, P4-03): named A, B or C, it
/// turns the tool vector about the machine X, Y or Z axis; a head axis turns the tool, a table axis the workpiece.
/// </summary>
internal sealed record ConventionAxis
{
    /// <summary>
    /// ncx: the name of the axis in the programs and in the position store, A, C2 (machine-config 4, D93).
    /// </summary>
    public required string NcxName { get; init; }

    /// <summary>
    /// id: the machine's own name of the axis, A1.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The machine axis it turns about: "X" for A, "Y" for B, "Z" for C.
    /// </summary>
    public required string About { get; init; }

    /// <summary>
    /// owner: the resource the axis belongs to (machine-config 4); null for an axis without owner.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// True for a table axis, owned by a work spindle or a table, which turns the workpiece; false for a head axis,
    /// which turns the tool.
    /// </summary>
    public required bool Table { get; init; }

    /// <summary>
    /// True when the axis turns the tool relative to the workpiece while the holder holds it: a head axis always, a
    /// table axis only when that holder owns it, as the axis of another holder turns another part (language 4.10: the
    /// program machines the part held by the WORKPIECE resource).
    /// </summary>
    /// <param name="workpieceHolder">The resource id of the workpiece holder of the block; null when the machine names
    /// none.</param>
    public bool AppliesWhile(string? workpieceHolder)
    {
        return !Table || Owner == workpieceHolder;
    }

    /// <summary>
    /// What the axis turns, as the report writes it: "the workpiece of TABLE1", "the tool of S1", "the tool, no owner".
    /// </summary>
    public string Turns()
    {
        if (Owner is null)
        {
            return "the tool, no owner";
        }

        return (Table ? "the workpiece of " : "the tool of ") + Owner;
    }
}
