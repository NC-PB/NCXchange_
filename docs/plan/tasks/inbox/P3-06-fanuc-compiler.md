# P3-06 Fanuc compiler

Phase: 3 | Milestone: M6 | Depends on: `P3-03`, `P3-05` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

NCX to Fanuc G-code, per section 10 of `controllers/fanuc.md`.

## Scope

- `%`, `O` with the name, `N` numbers per `[format]`, modal G only on change, `G90` header or `G91` blocks, `I J K` or `R`, sweeps split into turns, `G43 H`/`D`, `T`/`M6` per `[tool_change]`, `M3 S` in one block, `G54`, `G52`, `G53`, `G28` for `HOME`, `G50`/`G92` for `SETPOS`, `G68`, `G51.1`, `G68.2`/`G53.1`, `G43.4`, cycles `G81`.. with `R` and `Z` from the absolute words and `G98`/`G99` from `CYCLE_RETRACT`, `G80`, `O` subprograms after `M30`, `M98`, `GOTO`/`IF`/`WHILE` from the flow words, `#` variables, the wait marks from `[sync]`, `program_end`.
- The G-code system of the target (`U W`, `G98`/`G99` in system A).

## References

- controllers/fanuc.md section 10
- controller-mapping.md (Fanuc column)
- examples/sources/2.5D_FRAESEN.fanuc.nc

## Done when

- `2.5D_FRAESEN.ncx` compiles to a program equivalent to `2.5D_FRAESEN.fanuc.nc`; the round trip Fanuc to NCX to Fanuc reproduces the source under the comparison rules; the same for `BOHREN` and `3D_FRAESEN` (M6).
- This closes phase 3.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
