namespace Ncx.Core.Catalog;

/// <summary>
/// The tool words of language 4.4: the two words of a tool change, PRELOAD and TOOL, the offset registers and the
/// cutter radius compensation.
/// </summary>
internal static class ToolWords
{
    /// <summary>
    /// The rows of the table of language 4.4; the bare TOOL is the form of TOOL without a value.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "PRELOAD",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Integer | ValueKinds.String,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Preload,
            Section = "4.4",
            Description = "Prepares a tool in the magazine, modal until the change consumes it; PRELOAD=0 clears a "
                + "pending preload (D47).",
        },
        new WordDefinition
        {
            Key = "TOOL",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Bare | ValueKinds.Integer | ValueKinds.String,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Tool,
            Section = "4.4",
            Description = "The spindle or the addressed holder now carries this tool, TOOL=0 empties it; a bare TOOL "
                + "changes to the preloaded tool (D47, D91).",
        },

        // OFFSET alone is the combined register; LEN and RAD are the fixed addresses, in the order of bucket 9 (D90).
        new WordDefinition
        {
            Key = "OFFSET",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Integer,
            AddrKind = AddrKind.OffsetKind,
            AddrRanks = new Dictionary<string, int>
            {
                ["LEN"] = CanonicalRanks.OffsetLen,
                ["RAD"] = CanonicalRanks.OffsetRad,
            },
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Offset,
            Section = "4.4",
            Description = "Combined offset register; OFFSET:LEN the tool length offset register (G43 H), OFFSET:RAD "
                + "the tool radius offset register (D); 0 cancels.",
        },
        new WordDefinition
        {
            Key = "COMP",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["LEFT", "RIGHT", "OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Comp,
            Section = "4.4",
            Description = "Cutter radius compensation (G41, G42, G40), from the motion of the same block on.",
        },
    ];
}
