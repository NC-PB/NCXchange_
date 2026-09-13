namespace Ncx.Core.Catalog;

/// <summary>
/// The catalog entry of one key: the values it takes, whether it is a verb, what its address names, its scope and
/// its rank in the canonical order (architecture 4). The word catalog is the schema of the language; its entries are
/// the rows of the tables of language 4, and the rank table of D90 is the canonical order of language 5 rule 6.
/// </summary>
public sealed record WordDefinition
{
    /// <summary>
    /// The key, uppercase: "SPINDLE", "@SAVE" (language 3, KEY).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The group of the word, the table of language 4 it comes from.
    /// </summary>
    public required WordKind Group { get; init; }

    /// <summary>
    /// The value types the word accepts (language 3, value types), Bare among them for a word that may stand without
    /// a value. Where a row of language 4 says "number", an expression is accepted as well (language 3, expression).
    /// An address or a partner word in the block can change them (AddressedValueKinds, ValueKindsWithPartner);
    /// WordCheck.ValueKindsOf gives those of a word in its block.
    /// </summary>
    public required ValueKinds ValueKinds { get; init; }

    /// <summary>
    /// The identifiers the word accepts when ValueKinds holds Ident: CW, CCW, OFF for SPINDLE; empty when the word
    /// accepts any identifier, as a cycle name, a label or a role (language 4).
    /// </summary>
    public IReadOnlyList<string> AllowedIdents { get; init; } = [];

    /// <summary>
    /// The value types the word accepts when it carries an address, where they differ from ValueKinds: the native
    /// cycle number of CYCLE:HEIDENHAIN=251 (language 4.7, D94), the number of TOLERANCE:ROTARY (language 4.1); null
    /// for every other word.
    /// </summary>
    public ValueKinds? AddressedValueKinds { get; init; }

    /// <summary>
    /// The value types the word accepts when a partner word stands in its block, where they differ from ValueKinds:
    /// NAME takes a string, and with SUB=BEGIN an identifier, an integer or a string (language 4.1, D90); the first
    /// partner of the list that stands in the block decides. Empty for every other word.
    /// </summary>
    public IReadOnlyList<PartnerValueKinds> ValueKindsWithPartner { get; init; } = [];

    /// <summary>
    /// True for a verb; a block has at most one (language 5 rule 1).
    /// </summary>
    public bool IsVerb { get; init; }

    /// <summary>
    /// True for a verb that carries axis words in its block: the motion verbs but RETRACT, which carries none, HOME
    /// with bare axis names, and SHIFT, TILT, TILT_AXIS and SETPOS with their own (language 5 rule 2). A machine axis
    /// word stands only in a block whose verb takes axis words (D93).
    /// </summary>
    public bool TakesAxisWords { get; init; }

    /// <summary>
    /// True for an axis word, which requires a verb that takes axis words in its block: X and IX for every standard
    /// axis, CENTER, R, ANGLE, TX TY TZ, NX NY NZ (language 5 rule 2).
    /// </summary>
    public bool IsAxisWord { get; init; }

    /// <summary>
    /// What the address of the word names; None for a word without an address (language 3, ADDR).
    /// </summary>
    public AddrKind AddrKind { get; init; }

    /// <summary>
    /// True for a word that is written with an address only: CENTER:X, FUNC:CHIP_CONVEYOR, VAR:Q1, ARG:A, RAW:FANUC
    /// (language 4). A role address is optional: a word without one targets the default resource of its kind
    /// (language 4.10).
    /// </summary>
    public bool IsAddrRequired { get; init; }

    /// <summary>
    /// The addresses of a word whose addresses are a fixed set, each with its rank in the canonical order: OFFSET:LEN
    /// and OFFSET:RAD, CENTER:X to CENTER:IC, TOLERANCE:ROTARY (language 4, 5 rule 6, D90); the word accepts no other
    /// address. Empty for a word whose address names something open, a role, a channel or a variable; its words sort
    /// under its own rank by the address text.
    /// </summary>
    public IReadOnlyDictionary<string, int> AddrRanks { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// How long the word applies, the Scope column of language 4.
    /// </summary>
    public required Scope Scope { get; init; }

    /// <summary>
    /// The rank of the word in the canonical order: a word of lower rank stands first (language 5 rule 6, D90).
    /// </summary>
    public required int CanonicalRank { get; init; }

    /// <summary>
    /// The rank of the =RESET form of SHIFT, TILT and TILT_AXIS, which removes its entry from the frame chain and
    /// stands with the frame words, not as the verb (language 4.2, 5 rule 6 bucket 12, D90); null for every other
    /// word.
    /// </summary>
    public int? ResetRank { get; init; }

    /// <summary>
    /// True for an internal entry, the pseudo-words @SAVE and @RESTORE of the group Pseudo, which the parser accepts
    /// only under the option the expander uses for generated text (architecture 4, D95).
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// The section of the language the word comes from, "4.5" for SPINDLE, so that the generated table can be laid
    /// next to the specification.
    /// </summary>
    public required string Section { get; init; }

    /// <summary>
    /// What the word means, in the words of its row of the section.
    /// </summary>
    public required string Description { get; init; }
}
