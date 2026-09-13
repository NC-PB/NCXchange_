namespace Ncx.Core.Catalog;

// TODO(question): language 4.6 gives FUNC an address that names a machine function (FUNC:CHIP_CONVEYOR=ON), 4.9 gives
// ARG one that names an argument (ARG:A=1), and 4.1 writes TOLERANCE:ROTARY with a fixed address; neither the ADDR row
// of language 3 nor the AddrKind of architecture 4 has a kind for them. The enumeration keeps the list of architecture
// 4 until the word catalog, which records these words, settles it.

/// <summary>
/// What the address of a word names, the ADDR of language 3: an axis name, an offset kind, a coolant channel, a
/// resource role, a variable name or a controller family (architecture 4).
/// </summary>
public enum AddrKind
{
    /// <summary>
    /// The word takes no address.
    /// </summary>
    None,

    /// <summary>
    /// An axis name: CENTER:X, CENTER:IX (language 4.3).
    /// </summary>
    Axis,

    /// <summary>
    /// An offset kind: OFFSET:LEN, OFFSET:RAD (language 4.4).
    /// </summary>
    OffsetKind,

    /// <summary>
    /// A coolant channel: COOLANT:THROUGH (language 4.6).
    /// </summary>
    Channel,

    /// <summary>
    /// A resource role: SPINDLE:MAIN, TOOL:TURRET1 (language 4.10).
    /// </summary>
    Role,

    /// <summary>
    /// A variable name: VAR:Q1 (language 4.9).
    /// </summary>
    Variable,

    /// <summary>
    /// A controller family or builder dialect: CYCLE:HEIDENHAIN, RAW:FANUC (language 4.1, 4.7.1, D94).
    /// </summary>
    Controller,
}
