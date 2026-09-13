namespace Ncx.Core.Model;

/// <summary>
/// The value of the pseudo-words @SAVE and @RESTORE of generated blocks: a state key KEY[:ADDR] that names a state
/// variable of the channel by the key that sets it, SPINDLE:MAIN, COOLANT, F (language 3, virtual machine 3.10,
/// D95). Internal to generated blocks: the parser produces it only under the option the expander uses for generated
/// text, never for a user file.
/// </summary>
/// <param name="Key">The key that sets the state variable: "SPINDLE".</param>
/// <param name="Addr">Its address: "MAIN"; null for a key without one.</param>
public sealed record StateKeyValue(string Key, string? Addr) : Value
{
    /// <summary>
    /// KEY or KEY:ADDR (language 3, state key).
    /// </summary>
    public override string ToCanonical()
    {
        return Addr is null ? Key : Key + ":" + Addr;
    }
}
