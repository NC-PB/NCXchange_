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
