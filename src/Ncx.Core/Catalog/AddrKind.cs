namespace Ncx.Core.Catalog;

// Language 4.1 writes TOLERANCE:ROTARY with the fixed address ROTARY, which the catalog calls a tolerance kind after
// the offset kind of OFFSET:LEN; it accepts ROTARY only and refuses OFF on it, as the rows of 4.1 give the word
// (PAR152, PAR153). Which kind an address has is the catalog's own representation: only None decides behaviour, a
// word written with an address it does not take (PAR150; architecture 4; wave-1 questions #30, #34).

/// <summary>
/// What the address of a word names, the ADDR of language 3: an axis name, an offset kind, a coolant channel, a
/// resource role, a variable name or a controller family (architecture 4), and the function name of FUNC and the
/// argument name of ARG that the rows of language 4.6 and 4.9 give their address.
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
    /// A tolerance kind: TOLERANCE:ROTARY (language 4.1).
    /// </summary>
    ToleranceKind,

    /// <summary>
    /// A coolant channel: COOLANT:THROUGH (language 4.6).
    /// </summary>
    Channel,

    /// <summary>
    /// A resource role: SPINDLE:MAIN, TOOL:TURRET1 (language 4.10).
    /// </summary>
    Role,

    /// <summary>
    /// A named machine function of the machine configuration: FUNC:CHIP_CONVEYOR (language 4.6).
    /// </summary>
    Function,

    /// <summary>
    /// A variable name: VAR:Q1 (language 4.9).
    /// </summary>
    Variable,

    /// <summary>
    /// An argument name of the CALL in the same block: ARG:A (language 4.9).
    /// </summary>
    Argument,

    /// <summary>
    /// A controller family or builder dialect: CYCLE:HEIDENHAIN, RAW:FANUC (language 4.1, 4.7.1, D94).
    /// </summary>
    Controller,
}
