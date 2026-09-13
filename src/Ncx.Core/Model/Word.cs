using Ncx.Core.Catalog;

namespace Ncx.Core.Model;

/// <summary>
/// A word of a block: a key with an optional address and an optional value, written KEY, KEY=VALUE or
/// KEY:ADDR=VALUE (language 3, Word).
/// </summary>
public sealed record Word
{
    /// <summary>
    /// The key, uppercase: "SPINDLE" (language 3, KEY).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The address after the colon, uppercase: "MAIN" in SPINDLE:MAIN=CW; null for a word without one (language 3,
    /// ADDR).
    /// </summary>
    public string? Addr { get; init; }

    /// <summary>
    /// The value after the equals sign; NoValue for a word written without one (language 3, VALUE).
    /// </summary>
    public Value Value { get; init; } = NoValue.Instance;

    /// <summary>
    /// The catalog entry of the key; null for a word the catalog does not list: a machine axis word (D93), a native
    /// parameter of a CYCLE:controller=n block (D94), or an unknown key the parser reported.
    /// </summary>
    public WordDefinition? Definition { get; init; }

    /// <summary>
    /// The word as canonical NCX writes it: the key, the address after a colon, the value after an equals sign
    /// (language 3, Word).
    /// </summary>
    public string ToCanonical()
    {
        // KEY, KEY:ADDR, KEY=VALUE or KEY:ADDR=VALUE; a word without a value has no equals sign (language 3).
        string text = Addr is null ? Key : Key + ":" + Addr;
        if (Value is NoValue)
        {
            return text;
        }

        return text + "=" + Value.ToCanonical();
    }
}
