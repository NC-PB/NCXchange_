# P4-01 Expression evaluation and interpreted flow

Phase: 4 | Milestone: M7 | Depends on: `P1-07` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The INTERPRETED mode: variables evaluated, jumps and calls followed.

## Scope

- Evaluator over `ExprNode` with `decimal` arithmetic, the function set of section 4.12, `MOD` and `INT` semantics, comparisons to 1 and 0, division by zero ERROR, unassigned variables ERROR unless `unassigned = 0` (D38), `$SYS_*` through the configuration mapping with indexes (D51).
- Flow of section 3.6: `JUMP` with `IF`, `JUMP=END`, `CALL` with `ARG` and locals `V1`..`V33`, `SUB=END`/`RETURN`, `REPEAT`/`TIMES`, external programs, depth limit (default 8), block cap (default 1 000 000), `SKIP` per run option.
- Vars files (`<file>.vars.toml`) as start values.

## References

- ncx-virtual-machine.md sections 1, 2.7, 3.6, 3.9
- ncx-language.md sections 4.9, 4.12

## Done when

- `PATTERN_LOOP.ncx` runs the loop five times with the call positions X = 10, 30, 50, 70, 90 of its notes, and the trace shows Q1 and Q3 per iteration.
- `INCREMENTAL_SUB.ncx` calls the subprogram four times and ends at the expected position.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
