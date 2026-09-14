using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The facts of the reader at one CALL LBL of a subprogram, the state the subprogram runs with at that call (virtual
/// machine 3.9): the working plane, the pole, the cycle definition and whether NCX has it on, the chain of transforms
/// with the values of cycle 7, TCPM.
/// </summary>
internal sealed record HeidenhainCallerState
{
    /// <summary>
    /// The working plane; null when the caller does not know it.
    /// </summary>
    public required Workplane? Plane { get; init; }

    /// <summary>
    /// The pole of CC; null when none is known.
    /// </summary>
    public required HeidenhainPoint? Pole { get; init; }

    /// <summary>
    /// The decimals of the pole's source words.
    /// </summary>
    public required int PoleDecimals { get; init; }

    /// <summary>
    /// The active cycle definition; null when none is, or it is not known.
    /// </summary>
    public required HeidenhainDefinition? Definition { get; init; }

    /// <summary>
    /// True when the caller does not know its active definition.
    /// </summary>
    public required bool DefinitionUnknown { get; init; }

    /// <summary>
    /// Whether the NCX cycle is on; null when not known.
    /// </summary>
    public required bool? CycleOn { get; init; }

    /// <summary>
    /// The entries of the chain; null when the caller does not know it.
    /// </summary>
    public required IReadOnlyList<HeidenhainChainEntry>? Chain { get; init; }

    /// <summary>
    /// The chain as text, for comparing callers; null when not known.
    /// </summary>
    public required string? ChainText { get; init; }

    /// <summary>
    /// The values of the active cycle 7.
    /// </summary>
    public required IReadOnlyDictionary<string, decimal> DatumShift { get; init; }

    /// <summary>
    /// Whether M128 is on; null when not known.
    /// </summary>
    public required bool? Tcpm { get; init; }

    /// <summary>
    /// The facts of the reader where the call stands.
    /// </summary>
    /// <param name="state">The facts of the file.</param>
    public static HeidenhainCallerState Of(HeidenhainState state)
    {
        return new HeidenhainCallerState
        {
            Plane = state.Plane,
            Pole = state.Pole,
            PoleDecimals = state.PoleDecimals,
            Definition = state.Definition,
            DefinitionUnknown = state.DefinitionUnknown,
            CycleOn = state.CycleOn,
            Chain = state.Chain.Known ? [.. state.Chain.Entries] : null,
            ChainText = state.Chain.ToText(),
            DatumShift = new Dictionary<string, decimal>(state.DatumShift, StringComparer.Ordinal),
            Tcpm = state.Tcpm,
        };
    }
}
