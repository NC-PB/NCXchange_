# P3-02 Fanuc reader

Phase: 3 | Milestone: M4 | Depends on: `P3-01` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

Fanuc and ISO programs into canonical NCX, per section 9 of `controllers/fanuc.md`.

## Scope

- Tokenizer for `%`, `O`, `N`, `( )`, `/`, addresses with signed numbers with leading or trailing dots, `#` variables and `[ ]` expressions.
- Modal resolution (groups 01, 02, 03, 05, 06, 07, 08, 09, 10, 12), the G-code system of the machine (`gcode_system`, `U W` addresses in system A).
- Tools per section 5 of the Fanuc document: `T` alone, `M6`, `T4 M6`, lathe `T0656`, `G43 H`, `D`; the `S` binding rule.
- Frames: `G54`.., `G52` with `SHIFT=RESET`, `G53`, `G28`/`G30` as `HOME`, `G50`/`G92` as `SETPOS`, `G68`/`G69`, `G51.1`, `G68.2`/`G53.1` as `TILT` with `MOVE`, `G68.1` as `TILT_AXIS`, `G43.4`/`G43.5` as `TCPM`, `G12.1`/`G7.1`.
- Cycles `G73`..`G89` into `CYCLE=` with absolute coordinates and `CYCLE_CALL` per position, `G80`, `K` repeats; lathe cycles from the catalog.
- Macro B: `#n` to `Vn`, expressions, `IF GOTO`, `WHILE DO`, `GOTO`, `G65` calls with the letter table, `M98`/`M99`, `M99` in the main program as a loop, `/M30`.
- `ncx convert` wired to the reader.

## References

- controllers/fanuc.md
- controller-mapping.md (Fanuc column of every section)
- examples/sources/2.5D_FRAESEN.fanuc.nc, BOHREN.fanuc.nc

## Done when

- `ncx convert 2.5D_FRAESEN.fanuc.nc --machine fanuc-mill-30i` equals `examples/2.5D_FRAESEN.ncx` (M4).
- `BOHREN.fanuc.nc` reads into the expected `CYCLE=` words (expected file added to the acceptance project).
- Every Fanuc/ISO file of the corpus converts without a crash.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
