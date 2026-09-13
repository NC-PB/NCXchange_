namespace Ncx.Core.Machine;

/// <summary>
/// [func_meta]: reader fallback rules per function state, SUB_CHUCK.CLOSE = "workpiece_transfer_to = SUB", for
/// machines without a [workpiece] table (machine-config 5, D40).
/// </summary>
public sealed record FuncMeta
{
    /// <summary>
    /// The rule text of each function state, keyed FUNCTION.STATE as the file writes it, "SUB_CHUCK.CLOSE".
    /// </summary>
    public IReadOnlyDictionary<string, string> Entries { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The rule of one function state, "workpiece_transfer_to = SUB" for SUB_CHUCK and CLOSE; null when the file has
    /// none.
    /// </summary>
    /// <param name="function">The function name, SUB_CHUCK.</param>
    /// <param name="state">The state, CLOSE.</param>
    public string? Find(string function, string state)
    {
        return Entries.TryGetValue(function + "." + state, out string? rule) ? rule : null;
    }
}
