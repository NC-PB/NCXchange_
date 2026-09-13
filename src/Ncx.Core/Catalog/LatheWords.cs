namespace Ncx.Core.Catalog;

/// <summary>
/// The lathe words of language 4.11: constant surface speed, cutting speed and speed limit. DIAMETER is listed among
/// them and defined in 4.2; its entry is in FrameWords (D90).
/// </summary>
internal static class LatheWords
{
    /// <summary>
    /// The rows of the table of language 4.11 but DIAMETER.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "CSS",
            Group = WordKind.Lathe,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["ON", "OFF"],
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Css,
            Section = "4.11",
            Description = "Constant surface speed (G96, G97): the spindle follows VC while it is on.",
        },
        new WordDefinition
        {
            Key = "VC",
            Group = WordKind.Lathe,
            ValueKinds = ValueKinds.NumberOrExpr,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Vc,
            Section = "4.11",
            Description = "Cutting speed for CSS in m/min or ft/min (the S of G96 S140).",
        },
        new WordDefinition
        {
            Key = "RPM_MAX",
            Group = WordKind.Lathe,
            ValueKinds = ValueKinds.NumberOrExpr,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.RpmMax,
            Section = "4.11",
            Description = "Speed limit under CSS (G50 S, LIMS).",
        },
    ];
}
