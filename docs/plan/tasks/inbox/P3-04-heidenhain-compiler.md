# P3-04 Heidenhain compiler

Phase: 3 | Milestone: M5 | Depends on: `P3-03`, `P3-02` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

NCX to iTNC 530 Klartext, per section 8 of `controllers/heidenhain.md`.

## Scope

- `BEGIN PGM`/`END PGM` with consecutive block numbers, the comma, signs on coordinates, `L`/`FMAX`/`F`, `CC` plus `C` and `CR`, `CP IPA` for sweeps, `TOOL CALL` assembled from `TOOL`, `WORKPLANE` and `RPM`, `TOOL DEF` for `PRELOAD`, `RL`/`RR`/`R0`, `CYCL DEF 247`, `CYCL DEF 7`, `CYCL DEF 8`, `CYCL DEF 10`, `PLANE SPATIAL`/`AXIAL` with the options from `MOVE` and `ROT`, `M91` moves for `HOME`, `M128`/`M129` or `FUNCTION TCPM`, `M126`/`M127`, `M116`/`M117`, `M140 MB MAX`, `CYCL DEF 32`, cycle blocks with `Q` parameters and `CYCL CALL`/`M99`, `LBL` subprograms after `M30` per calling program, `CALL LBL REP`, `FN 9`..`FN 12` and formulas for `JUMP`/`IF`/`VAR`.
- `ncx compile` wired to the compiler.

## References

- controllers/heidenhain.md section 8
- controller-mapping.md (Heidenhain column)
- examples/sources/2.5D_FRAESEN.h
- `BOHREN.ncx` as read from `BOHREN.fanuc.nc` by the Fanuc reader (P3-02)

## Done when

- `ncx compile examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` is equivalent to `2.5D_FRAESEN.h` under the comparison rules (block numbers, formatting and comment placement ignored) (M5).
- `BOHREN.ncx` (from the Fanuc source, read by P3-02) compiles to a program equivalent to `BOHREN.h`.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
