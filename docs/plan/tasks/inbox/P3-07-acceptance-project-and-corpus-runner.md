# P3-07 Acceptance project and corpus runner

Phase: 3 | Milestone: M6 | Depends on: `P3-06` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The tests that hold the whole chain together, and the tool that runs a reader over hundreds of files.

## Scope

- `tests/Ncx.Acceptance`: for every pair in `docs/spec/examples/sources/`, convert from both sources, compare with the `.ncx`, compile back to both, compare with the sources under the comparison rules; the five `.ncx` examples format clean (no machine file, D91) and check with no ERROR without and with their machine files (D103).
- A comparison that ignores block numbers, number formatting within the machine's decimals, comment placement and blank lines, and shows a diff on failure.
- `ncx convert --batch <folder>` (or a test-only runner) that converts every file of a folder, never stops on an error, and writes a summary: files, blocks, `RAW` blocks per word, diagnostics per rule.

## References

- architecture.md section 11 (testing)
- code-guidelines.md section 8
- controllers/sample-corpus.md section 3

## Done when

- The acceptance project is green.
- The batch runner over the maintainer's corpus reports zero crashes for the Fanuc and Heidenhain families (run by the maintainer; the summary is committed as a text file under `tests/corpus-reports/`).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
