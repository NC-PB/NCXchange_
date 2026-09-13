namespace Ncx.Core.Catalog;

/// <summary>
/// The cycle words of language 4.7: the cycle definition with its parameters, the contour reference of D65 and the
/// cycle call. The native parameters of a CYCLE:controller=n block are no entries (4.7.1, D94).
/// </summary>
internal static class CycleWords
{
    // The table of language 4.7.
    private const string TableSection = "4.7";

    /// <summary>
    /// The rows of the table of language 4.7, CONTOUR among them (D90).
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        // A cycle by name, or natively by the cycle number of a controller family (language 4.7, 4.7.1, D94).
        new WordDefinition
        {
            Key = "CYCLE",
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.Ident,
            AddressedValueKinds = ValueKinds.Integer,
            AddrKind = AddrKind.Controller,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Cycle,
            Section = TableSection,
            Description = "Defines the active cycle by name (DRILL, a catalog name, OFF), or natively with a "
                + "controller address and the cycle number (CYCLE:HEIDENHAIN=251, D94).",
        },
        CycleNumber("SURFACE", CanonicalRanks.Surface,
            "Absolute coordinate of the workpiece surface along the drilling axis (Q203)."),
        CycleNumber("CLEARANCE", CanonicalRanks.Clearance,
            "Absolute coordinate of the clearance plane along the drilling axis (Fanuc R, Q203 + Q200)."),
        CycleNumber("DEPTH", CanonicalRanks.Depth,
            "Absolute coordinate of the hole bottom along the drilling axis (Q203 + Q201)."),
        CycleNumber("SAFE", CanonicalRanks.Safe,
            "Absolute coordinate of the safe plane along the drilling axis (G98 initial level); optional."),
        new WordDefinition
        {
            Key = "CYCLE_RETRACT",
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["CLEARANCE", "SAFE"],
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.CycleRetract,
            Section = TableSection,
            Description = "Where the tool ends after each hole (G99, G98); default CLEARANCE (D83, D87).",
        },
        CycleNumber("PECK", CanonicalRanks.Peck, "Peck depth (Q, Q202)."),
        CycleNumber("CYCLE_F", CanonicalRanks.CycleF,
            "Plunge feed of the cycle in the active feed mode, its own word so that the motion feed F is never "
            + "touched (D29)."),
        CycleNumber("CYCLE_DWELL", CanonicalRanks.CycleDwell, "Dwell at the bottom in seconds (P, Q211; D29)."),
        CycleNumber("PITCH", CanonicalRanks.Pitch, "Thread pitch for TAP (Q239)."),

        // The contour is the NAME of a SUB section, written as NAME is on SUB=BEGIN (language 3 EBNF, 4.7, D65).
        new WordDefinition
        {
            Key = "CONTOUR",
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.Ident | ValueKinds.Integer | ValueKinds.String,
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.Contour,
            Section = TableSection,
            Description = "Contour reference of a multiple repetitive turning cycle, the NAME of a SUB section of the "
                + "file (D65, D90).",
        },
        new WordDefinition
        {
            Key = "AXIS",
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.Ident,
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.Axis,
            Section = TableSection,
            Description = "Drilling axis of the cycle, an axis name; default the tool axis of the active WORKPLANE "
                + "(D59).",
        },
        new WordDefinition
        {
            Key = "CYCLE_CALL",
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.Bare,
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Verb,
            Section = TableSection,
            Description = "Executes the active cycle at the position of the axis words, or at the current position "
                + "without them (CYCL CALL, M99).",
        },
    ];

    // A number parameter of the cycle definition, which stands with CYCLE in its block (language 4.7).
    private static WordDefinition CycleNumber(string key, int rank, string description)
    {
        return new WordDefinition
        {
            Key = key,
            Group = WordKind.Cycle,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.WithPartner,
            CanonicalRank = rank,
            Section = TableSection,
            Description = description,
        };
    }
}
