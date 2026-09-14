using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics;

/// <summary>
/// One motion of the list of the ten worst of a report: the line of its block, its verb and its value, and for the
/// tool vector change the vectors at its start and at its end (implementation 14, P4-03: with line numbers).
/// </summary>
internal sealed record WorstMotion
{
    /// <summary>
    /// The NCX line of the block; a generated block has the line of its origin.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The verb of the motion.
    /// </summary>
    public required Verb Verb { get; init; }

    /// <summary>
    /// The value the list is ordered by: a length in mm, an angle in degrees.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// The tool vector at the start of the motion as the report writes it; empty for a length.
    /// </summary>
    public string Start { get; init; } = "";

    /// <summary>
    /// The tool vector at the end of the motion as the report writes it; empty for a length.
    /// </summary>
    public string End { get; init; } = "";
}
