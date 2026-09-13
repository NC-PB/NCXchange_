namespace Ncx.Core.Machine;

/// <summary>
/// [variables]: what reading an unassigned variable does, how variable names compile to another family, and the
/// limits of INTERPRETED mode (machine-config 7, virtual machine 3.6).
/// </summary>
public sealed record VariablesConfig
{
    /// <summary>
    /// unassigned: reading an unassigned variable is an ERROR unless the file sets unassigned = 0 (virtual machine
    /// 3.6, D38).
    /// </summary>
    public UnassignedVariable Unassigned { get; init; } = UnassignedVariable.Error;

    /// <summary>
    /// map: variable prefix to native prefix, Q = "#1" compiles Q1 to #101; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Map { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// block_cap: the blocks INTERPRETED mode executes before the ERROR "possible endless loop"; 1 000 000 when left
    /// out (virtual machine 3.6).
    /// </summary>
    public long BlockCap { get; init; } = 1_000_000;

    /// <summary>
    /// call_depth: how deep calls and repeats may nest; 8 when left out (virtual machine 3.6, 3.9).
    /// </summary>
    public int CallDepth { get; init; } = 8;
}
