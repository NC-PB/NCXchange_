namespace Ncx.Core.Catalog;

/// <summary>
/// The variable and control flow words of language 4.9: variables, labels, jumps, conditions, subprogram sections,
/// calls with their arguments and repeat counts. $Q1 of the table reads a variable inside an expression and is no
/// word of a block (language 4.12).
/// </summary>
internal static class FlowWords
{
    // The table of language 4.9.
    private const string TableSection = "4.9";

    /// <summary>
    /// The rows of the table of language 4.9.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "VAR",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.NumberOrExpr | ValueKinds.String,
            AddrKind = AddrKind.Variable,
            IsAddrRequired = true,
            Scope = Scope.None,
            CanonicalRank = CanonicalRanks.Var,
            Section = TableSection,
            Description = "Assigns a variable, creating it if needed; the address is the variable name.",
        },
        new WordDefinition
        {
            Key = "LABEL",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Integer | ValueKinds.Ident,
            Scope = Scope.None,
            CanonicalRank = CanonicalRanks.Label,
            Section = TableSection,
            Description = "Jump target and repeat start, unique per program or subprogram; END is reserved (D88).",
        },
        new WordDefinition
        {
            Key = "JUMP",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Integer | ValueKinds.Ident,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Jump,
            Section = TableSection,
            Description = "Continues at the label, or with JUMP=END at the PROGRAM=END of the current program; "
                + "conditional with IF (D88).",
        },
        new WordDefinition
        {
            Key = "IF",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Expr,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.If,
            Section = TableSection,
            Description = "Condition of the JUMP or CALL in the same block, which executes when the expression is "
                + "not 0.",
        },
        new WordDefinition
        {
            Key = "SUB",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["BEGIN", "END"],
            Scope = Scope.File,
            CanonicalRank = CanonicalRanks.Sub,
            Section = TableSection,
            Description = "Start of a subprogram section of the file, SUB=BEGIN with NAME, and its end, SUB=END, the "
                + "return to the caller (D89).",
        },
        new WordDefinition
        {
            Key = "RETURN",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Bare,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Return,
            Section = TableSection,
            Description = "Returns to the caller before SUB=END is reached; in a program a WARNING, treated as "
                + "JUMP=END.",
        },
        new WordDefinition
        {
            Key = "CALL",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Ident | ValueKinds.Integer | ValueKinds.String,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Call,
            Section = TableSection,
            Description = "Calls a subprogram of the file by its NAME, or an external program by file name as a "
                + "string.",
        },
        new WordDefinition
        {
            Key = "ARG",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.NumberOrExpr,
            AddrKind = AddrKind.Argument,
            IsAddrRequired = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Arg,
            Section = TableSection,
            Description = "Argument of the CALL in the same block (G65 P9010 A1); the callee sees it as a local "
                + "variable.",
        },
        new WordDefinition
        {
            Key = "TIMES",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Integer | ValueKinds.Expr,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Times,
            Section = TableSection,
            Description = "Repeat count of the CALL or REPEAT in the same block.",
        },
        new WordDefinition
        {
            Key = "REPEAT",
            Group = WordKind.Flow,
            ValueKinds = ValueKinds.Integer | ValueKinds.Ident,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Repeat,
            Section = TableSection,
            Description = "Repeats the blocks from the label to this block TIMES more times.",
        },
    ];
}
