namespace Ncx.Core.Catalog;

/// <summary>
/// The coolant and machine function words of language 4.6.
/// </summary>
internal static class FunctionWords
{
    /// <summary>
    /// The rows of the table of language 4.6.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "COOLANT",
            Group = WordKind.Function,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["ON", "OFF"],
            AddrKind = AddrKind.Channel,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Coolant,
            Section = "4.6",
            Description = "Default coolant channel (M8, M9) or a named channel of the machine configuration, modal "
                + "per channel.",
        },

        // The value of FUNC is a state of the function that the machine configuration names, any identifier (D9).
        new WordDefinition
        {
            Key = "FUNC",
            Group = WordKind.Function,
            ValueKinds = ValueKinds.Ident,
            AddrKind = AddrKind.Function,
            IsAddrRequired = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Func,
            Section = "4.6",
            Description = "Named machine function of the machine configuration with its state as the value, the "
                + "normal way to write machine functions (D9).",
        },
        new WordDefinition
        {
            Key = "MFUNC",
            Group = WordKind.Function,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.MFunc,
            Section = "4.6",
            Description = "Raw M function by number, for functions the machine configuration does not name; the "
                + "compiler warns.",
        },
    ];
}
