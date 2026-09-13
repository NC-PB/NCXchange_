# P0-05 Expression parser

Phase: 0 | Milestone: M1 | Depends on: `P0-04` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The `{...}` grammar of section 4.12 parsed into a tree the VM can evaluate later.

## Scope

- Recursive descent per the EBNF: `OR`, `AND`, `NOT`, comparisons, sums, products with `MOD`, unary minus, power, primaries (number, `$variable` with an optional `[index]`, function calls, parentheses).
- `ExprNode` tree with a `ToString()` that reproduces the canonical text (used by `ncx format`).
- Errors as diagnostics with the position inside the expression.

## References

- ncx-language.md section 4.12
- architecture.md section 4 (ExprValue, ExprNode)

## Done when

- Every expression in `PATTERN_LOOP.ncx` and in the language examples parses and prints back identically.
- Precedence tests: `{1 + 2 * 3}`, `{-2 ^ 2}`, `{$Q1 < $Q2 AND NOT $Q3}`.

## Notes

Evaluation is phase 4; only the tree is needed now.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13. Built on `main` at P0-02 (the model, the `ExprNode` base in `src/Ncx.Core/Expressions/`, `DiagnosticCodes` as a partial class). Files in `src/Ncx.Core/Expressions/`: `ExprParser` (public; the entry point `ExprParser.Parse(text, line, diagnostics)` that the word parser of P0-04 calls on the text inside the braces; recursive descent with one method per production of language 4.12, `ParseExpr`, `ParseOrExpr`, `ParseAndExpr`, `ParseNotExpr`, `ParseCmpExpr`, `ParseSum`, `ParseProduct`, `ParseUnary`, `ParsePower`, `ParsePrimary`, and `ParseVariable`, `ParseCall`, `ParseParentheses` for the alternatives of `primary`), the node records `NumberNode`, `VariableNode`, `CallNode`, `UnaryNode`, `BinaryNode`, `ParenthesesNode`, the enums `ExprFunction` (the seventeen names), `UnaryOperator`, `BinaryOperator`, and the internal `ExprTokenizer`, `ExprToken`, `ExprTokenKind` and `ExprSymbols` (the spelling of every operator and function in one place, for the parser that reads and the nodes that write). Codes PAR100-PAR110 in `src/Ncx.Core/Model/DiagnosticCodes.Expressions.cs`, the part file the header of `DiagnosticCodes.cs` asks for. Tests in `tests/Ncx.Core.Tests/Expressions/`. No decision of the log was needed and the specification is unchanged; everything below is a reading of it.

- `ExprNode` (P0-02's base in this folder) gained the closed-set constructor that `Value` has, an abstract `ToCanonical()` (the model's name for canonical text) and a sealed `ToString()` that returns it, as the phase file asks for `ToString()`.
- The tree keeps written parentheses as `ParenthesesNode`, a sixth node type beside the five of the phase file, for the alternative `"(" expr ")"` of `primary`; the canonical text therefore never changes the grouping, and a tree built in code states its parentheses the same way.
- `NumberNode` holds the text only, as the phase file says. A number inside an expression has no sign; a minus in front of it is the unary minus of the grammar, so `{-2 ^ 2}` is the minus of the power.
- `CallNode` compares its arguments by value, as `ListValue` compares its items, so equal trees are equal records.
- Whitespace (spaces and tabs) is free between any two parts of the grammar, also in `$ Q1` and `SIN (30)`; the canonical text removes it.
- Diagnostics: the parser stops at the first problem, so an expression gives at most one ERROR, on the line passed in, with the 1-based position inside the text between the braces in the message ("at position 6", "at the end"), and no tree (`null`). PAR100 unexpected character, PAR101 malformed number, PAR102 `$` without a name, PAR103 empty, PAR104 missing operand, PAR105 unbalanced parenthesis, PAR106 unbalanced bracket, PAR107 unknown function, PAR108 name without `$` or parentheses, PAR109 part after a complete expression, PAR110 chained comparison.
- The examples: "the language examples" is read as the five complete programs that language 6 names (only `PATTERN_LOOP` holds expressions) plus the expressions written in the text of language 3, 4.9 and 4.12. The snippets of language 6 hold no expression today; the test reads them from `docs/spec/ncx-language.md`, so that one added later is covered.
- Open questions, marked `TODO(question)` in the files: `NOT` in the canonical text (the phase file writes it directly before its operand, its own case `{$Q1 < $Q2 AND NOT $Q3}` has a space, and `NOTSIN(30)` would read back as one name; written with one space); whether the canonical text keeps parentheses the grammar does not need (kept as written); lowercase function names and `AND`, `OR`, `NOT`, `MOD` (normalized like the variable names, which language 3, Case, covers as addresses); the number of arguments of each function (none checked; the grammar's one or more).

Done when:

- Every expression in `PATTERN_LOOP.ncx` and in the language examples parses and prints back identically: holds (`ExprExampleTests`).
- Precedence tests `{1 + 2 * 3}`, `{-2 ^ 2}`, `{$Q1 < $Q2 AND NOT $Q3}`: hold (`ExprParserGrammarTests`), with `{2 ^ 3 ^ 2}`, `{$SYS_WEAR_Z[99]}` and `{MAX($A, 2) * SIN(30)}` of the phase file; the error cases of the phase file (unknown function, unbalanced parenthesis, missing operand) in `ExprParserDiagnosticsTests`.

Waits for P0-04: the word parser calls `ExprParser.Parse` on every expression value and stores the tree in `ExprValue.Tree`; whether `ExprValue.Text` holds the canonical print or the source text is its choice (P0-02 log).

Gate: `dotnet build -warnaserror` with 0 warnings, 224 tests passing (213 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.
