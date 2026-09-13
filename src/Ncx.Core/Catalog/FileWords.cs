namespace Ncx.Core.Catalog;

/// <summary>
/// The file and program words of language 4.1: the frame of the file and of its programs, the header words, units,
/// comments, stops, dwell, source text kept as RAW, the optional block skip and the path tolerance.
/// </summary>
internal static class FileWords
{
    /// <summary>
    /// The rows of the table of language 4.1.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "FILE",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["BEGIN", "END"],
            Scope = Scope.File,
            CanonicalRank = CanonicalRanks.File,
            Section = "4.1",
            Description = "First block of every file, FILE=BEGIN with NCX=1, and its last block, FILE=END.",
        },
        new WordDefinition
        {
            Key = "NCX",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.File,
            CanonicalRank = CanonicalRanks.Ncx,
            Section = "4.1",
            Description = "Format version, NCX=1, in the FILE=BEGIN block.",
        },
        new WordDefinition
        {
            Key = "PROGRAM",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["BEGIN", "END"],
            Scope = Scope.Program,
            CanonicalRank = CanonicalRanks.Program,
            Section = "4.1",
            Description = "Start of a program, PROGRAM=BEGIN, and its executed end, PROGRAM=END, exactly once and its "
                + "last block.",
        },
        // NAME="..." is a string: the program name with PROGRAM=BEGIN, and the program selector with START_CHANNEL,
        // which names a program by that name. With SUB=BEGIN it is an identifier, an integer or a string, as the EBNF
        // line of sub has it (language 4.1, 4.8; language 3; D90).
        new WordDefinition
        {
            Key = "NAME",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.String,
            ValueKindsWithPartner =
            [
                new PartnerValueKinds("SUB", "BEGIN", ValueKinds.String | ValueKinds.Ident | ValueKinds.Integer),
            ],
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.Name,
            Section = "4.1",
            Description = "Program name with PROGRAM=BEGIN, name of a subprogram section with SUB=BEGIN (4.9, 4.13), "
                + "optional program selector with START_CHANNEL (4.8); an identifier or an integer names a subprogram "
                + "section only (D90).",
        },
        new WordDefinition
        {
            Key = "NUMBER",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.WithPartner,
            CanonicalRank = CanonicalRanks.Number,
            Section = "4.1",
            Description = "Program number (Fanuc O0001), with PROGRAM=BEGIN.",
        },
        new WordDefinition
        {
            Key = "CHANNEL",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Header,
            CanonicalRank = CanonicalRanks.Channel,
            Section = "4.1",
            Description = "Channel the program runs on, a header word after PROGRAM=BEGIN; default 1 (4.14).",
        },
        new WordDefinition
        {
            Key = "UNITS",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["MM", "INCH"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Units,
            Section = "4.1",
            Description = "Measurement units, required before the first motion.",
        },
        new WordDefinition
        {
            Key = "COMMENT",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.String,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Comment,
            Section = "4.1",
            Description = "Program comment, kept in the output.",
        },
        new WordDefinition
        {
            Key = "SECTION",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.String,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Section,
            Section = "4.1",
            Description = "Structuring comment (Heidenhain * -), a plain comment on other controllers.",
        },
        new WordDefinition
        {
            Key = "STOP",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["PROGRAM", "OPTIONAL"],
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Stop,
            Section = "4.1",
            Description = "Program stop M0, STOP=PROGRAM, or optional stop M1, STOP=OPTIONAL.",
        },
        new WordDefinition
        {
            Key = "DWELL",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Dwell,
            Section = "4.1",
            Description = "Dwell in seconds (G4).",
        },
        new WordDefinition
        {
            Key = "RAW",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.String,
            AddrKind = AddrKind.Controller,
            IsAddrRequired = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Raw,
            Section = "4.1",
            Description = "Source text NCX could not express, kept verbatim; the address is the controller or builder "
                + "dialect, and the text compiles to that one only (D5).",
        },
        new WordDefinition
        {
            Key = "SKIP",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Bare | ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Skip,
            Section = "4.1",
            Description = "Optional block skip, bare or with the number 1 to 9 of the block skip switch (D53).",
        },
        new WordDefinition
        {
            Key = "TOLERANCE",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.NumberOrExpr | ValueKinds.Ident,
            AllowedIdents = ["OFF"],
            AddrKind = AddrKind.ToleranceKind,
            AddrRanks = new Dictionary<string, int> { ["ROTARY"] = CanonicalRanks.ToleranceRotary },
            AddressedValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Tolerance,
            Section = "4.1",
            Description = "Path tolerance in the active units, OFF for the control's default; TOLERANCE:ROTARY is the "
                + "orientation tolerance of the rotary axes in degrees (D85).",
        },
        new WordDefinition
        {
            Key = "TOLERANCE_MODE",
            Group = WordKind.Program,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["FINISH", "ROUGH"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.ToleranceMode,
            Section = "4.1",
            Description = "What the control optimizes for under the tolerance, accuracy or speed; default FINISH "
                + "(D85).",
        },
    ];
}
