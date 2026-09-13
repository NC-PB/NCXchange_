namespace Ncx.Core.Catalog;

/// <summary>
/// The catalog entry of one key: the values it takes, whether it is a verb, what its address names, its scope and
/// its rank in the canonical order (architecture 4). The word catalog is the schema of the language; its entries are
/// the tables of language 4, and the rank table of D90 is the canonical order of language 5 rule 6.
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
    /// a value.
    /// </summary>
    public required ValueKinds ValueKinds { get; init; }

    /// <summary>
    /// The identifiers the word accepts when ValueKinds holds Ident: CW, CCW, OFF for SPINDLE; empty when the word
    /// accepts any identifier, as a cycle name or a label (language 4).
    /// </summary>
    public IReadOnlyList<string> AllowedIdents { get; init; } = [];

    /// <summary>
    /// True for a verb; a block has at most one (language 5 rule 1).
    /// </summary>
    public bool IsVerb { get; init; }

    /// <summary>
    /// True for a verb that carries axis words in its block (language 5 rule 2).
    /// </summary>
    public bool TakesAxisWords { get; init; }

    /// <summary>
    /// What the address of the word names; None for a word without an address (language 3, ADDR).
    /// </summary>
    public AddrKind AddrKind { get; init; }

    /// <summary>
    /// How long the word applies, the Scope column of language 4.
    /// </summary>
    public required Scope Scope { get; init; }

    /// <summary>
    /// The rank of the word in the canonical order: a word of lower rank stands first (language 5 rule 6, D90).
    /// </summary>
    public required int CanonicalRank { get; init; }

    /// <summary>
    /// True for an internal entry, the pseudo-words @SAVE and @RESTORE of the group Pseudo, which the parser accepts
    /// only under the option the expander uses for generated text (architecture 4, D95).
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// What the word means, with the section of the language it comes from, so that the generated table can be laid
    /// next to the specification.
    /// </summary>
    public required string Description { get; init; }
}
