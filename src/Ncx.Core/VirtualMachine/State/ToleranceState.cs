namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The path tolerance of the control: TOLERANCE, TOLERANCE:ROTARY and TOLERANCE_MODE together (language 4.1, virtual
/// machine 2.1, D85). State only, written through by the compiler. Immutable: a word that changes one of the three
/// replaces the whole value.
/// </summary>
public sealed record ToleranceState
{
    /// <summary>
    /// TOLERANCE: the path tolerance in the active units; null for OFF, the control's default.
    /// </summary>
    public required decimal? Value { get; init; }

    /// <summary>
    /// TOLERANCE:ROTARY: the orientation tolerance of the rotary axes in degrees; null when none is set.
    /// </summary>
    public required decimal? Rotary { get; init; }

    /// <summary>
    /// TOLERANCE_MODE: what the control optimizes for under the tolerance.
    /// </summary>
    public required ToleranceMode Mode { get; init; }
}
