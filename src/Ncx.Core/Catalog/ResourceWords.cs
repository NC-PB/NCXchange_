namespace Ncx.Core.Catalog;

/// <summary>
/// The resource word of language 4.10. The roles themselves are the addresses of the spindle, tool and lathe words.
/// </summary>
internal static class ResourceWords
{
    /// <summary>
    /// The row of the table of language 4.10.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "WORKPIECE",
            Group = WordKind.Resource,
            ValueKinds = ValueKinds.Ident,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Workpiece,
            Section = "4.10",
            Description = "From here on the program machines the part held by this holder role; ORIGIN, SHIFT and "
                + "A B C refer to it (D57).",
        },
    ];
}
