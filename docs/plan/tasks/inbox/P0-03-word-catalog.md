# P0-03 Word catalog

Phase: 0 | Milestone: M1 | Depends on: `P0-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The schema of the language as code: for every key its value kind, whether it is a verb, its address rule, its scope and its rank in the canonical order (D76).

## Scope

- `WordDefinition` and `WordCatalog` with one entry per word of `ncx-language.md` section 4 (all tables), including the axis words (the standard axes `X Y Z A B C` and their `I` incremental forms; machine axes such as `Z2` are not entries, they are accepted by the pattern rule of the next item, D93), `CENTER:*`, `TX TY TZ`, `NX NY NZ`, `ANGLE`, the cycle words with `CONTOUR` (D90), the flow words, `FILE`, `PROGRAM`, `SUB`, `RAW:*`, and the pseudo-words `@SAVE`/`@RESTORE` marked as internal, with a state key `KEY[:ADDR]` as their value (D95).
- Catalog methods for the two rules that accept keys the catalog does not list: the machine-axis form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, in a block whose verb takes axis words (D93), and a native parameter in a block that carries `CYCLE:<controller>=n` (D94).
- Value validation per definition (identifier sets such as `CW`/`CCW`, `ON`/`OFF`, `TURN`/`MOVE`/`STAY`; numbers; lists; strings; expressions); `CYLINDER` takes the reference radius or `OFF`, there is no `ON` form (D96).
- Canonical rank per section 5 rule 6 of the language: every word has a rank, the rank table of D90 is the rule.
- A generated Markdown table from the catalog, `docs/spec/generated/word-catalog.md` written by a test (D90), so that the catalog and the specification can be compared by eye.

## References

- ncx-language.md section 4 (every table) and section 5 (block rules)
- architecture.md section 4 (WordDefinition, WordCatalog)
- code-guidelines.md section 5 (table-driven dispatch)

## Done when

- Every word used in the five examples and in the language examples of section 6 has a definition.
- A test enumerates the catalog and asserts that no two words share a key with a different meaning and that every verb is marked.
- The rank order equals the table of D90.

## Notes

Write the catalog as data in code (a static list), not as a TOML file: the catalog is the language, the cycle catalogs are data.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
