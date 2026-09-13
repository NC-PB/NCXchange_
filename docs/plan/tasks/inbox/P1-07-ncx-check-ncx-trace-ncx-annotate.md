# P1-07 `ncx check`, `ncx trace`, `ncx annotate`

Phase: 1 | Milestone: M2 | Depends on: `P1-06` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The three commands that make the VM visible without a compiler.

## Scope

- `ncx check <file> [--machine <toml>]`: parse, expand, STATIC run, diagnostics with the exit codes of D97 (0 no ERROR, 1 an ERROR or a WARNING under `--strict`, 2 a usage error or an unreadable input, including a `--machine` that names a file that cannot be found or read; a machine file that reads but loads with a CFG ERROR exits 1 like any other ERROR); `--machine` is optional, without it the three commands run against the built-in default machine (D103).
- `ncx trace`: one row per changed state variable per executed block (`channel, block, variable, old, new`), CSV or aligned text.
- `ncx annotate`: a copy of the program with the previous values as line comments (`LINE X=55.44 ; X 33.22 -> 55.44`), which the VM ignores (the parser keeps them as the block's comment, D92).
- The run options `skip_blocks` (D53) and `ExpandCycles` on the command line.

## References

- ncx-virtual-machine.md section 6
- architecture.md section 10 (CLI table)

## Done when

- The three commands run on every example; the annotate output parses and formats back to the original when the comments are stripped.
- This closes M2 and phase 1.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
