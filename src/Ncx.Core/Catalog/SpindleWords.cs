namespace Ncx.Core.Catalog;

/// <summary>
/// The spindle words of language 4.5: direction, speed, mode, oriented stop, synchronous spindles and their phase.
/// </summary>
internal static class SpindleWords
{
    /// <summary>
    /// The rows of the table of language 4.5.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "SPINDLE",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["CW", "CCW", "OFF"],
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Spindle,
            Section = "4.5",
            Description = "Spindle on clockwise, counterclockwise, off (M3, M4, M5); without an address the machine's "
                + "default spindle.",
        },
        new WordDefinition
        {
            Key = "RPM",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.NumberOrExpr,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Rpm,
            Section = "4.5",
            Description = "Spindle speed, per spindle role.",
        },

        // TODO(question): the row of SPINDLE_MODE writes "addr = spindle role" where the other spindle words write
        // "optional addr", while language 4.10 says that a word without a role address targets the default resource
        // of its kind. The catalog keeps the address optional, as 4.10 says for every role address.
        new WordDefinition
        {
            Key = "SPINDLE_MODE",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["SPINDLE", "AXIS"],
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.SpindleMode,
            Section = "4.5",
            Description = "Work spindle as rotating spindle or as positioning C axis (M70, SPOS); in AXIS mode its "
                + "axis is driven with C.",
        },
        new WordDefinition
        {
            Key = "ORIENT",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.NumberOrExpr,
            AddrKind = AddrKind.Role,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Orient,
            Section = "4.5",
            Description = "Oriented spindle stop in degrees (M19, SPOS=); the spindle is stopped afterwards.",
        },
        new WordDefinition
        {
            Key = "SPINDLE_SYNC",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.List | ValueKinds.Ident,
            AllowedIdents = ["OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.SpindleSync,
            Section = "4.5",
            Description = "Synchronous spindles, a list of two spindle roles of which the second follows the first; "
                + "OFF ends it (D56).",
        },
        new WordDefinition
        {
            Key = "PHASE",
            Group = WordKind.Spindle,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.Phase,
            Section = "4.5",
            Description = "Angular offset in degrees of a phase-synchronous run; only with SPINDLE_SYNC.",
        },
    ];
}
