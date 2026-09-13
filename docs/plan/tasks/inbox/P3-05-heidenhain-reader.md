# P3-05 Heidenhain reader

Phase: 3 | Milestone: M6 | Depends on: `P3-02`, `P3-04` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

Klartext into canonical NCX, per section 7 of `controllers/heidenhain.md`.

## Scope

- Tokenizer for numbered blocks, `~` continuations, the comma, `;` and `* -`, `/`.
- `L`, `C`/`CC`, `CR`, `CT`, `LP`/`CP` with the pole, `LN` vectors, `IX`.. prefixes, `FMAX`/`F`/`F AUTO`, `RL`/`RR`/`R0`, `M91`, `TOOL CALL` split into words, `TOOL DEF`, cycle definitions into absolute coordinates, `CYCL CALL`/`M99`/`CYCL CALL PAT` into calls with `CYCLE=OFF` before the next motion, `CYCL DEF 7`/`8`/`10`/`247`/`19`/`32`/`9`, `PLANE` forms, `M128`/`M129`, `M126`/`M127`, `M116`/`M117`, `M140`, `M136`/`M137`, `LBL` as `SUB` or `LABEL` by usage, `CALL LBL REP`, `CALL PGM`, `FN 0`..`FN 12`, formulas, `Q` names kept.
- `BLK FORM`, `APPR`/`DEP`, `TCH PROBE`, `FN 16`/`18`/`19`, OEM cycles: `RAW`.

## References

- controllers/heidenhain.md section 7
- controller-mapping.md (Heidenhain column)
- examples/sources/*.h

## Done when

- `ncx convert 2.5D_FRAESEN.h --machine heidenhain-itnc530` equals `examples/2.5D_FRAESEN.ncx` (with the WARNING about the missing `M30`).
- Every Heidenhain file of the corpus converts without a crash; `PLANE AXIAL`, `CP IPA`, `LN`, `M140` read into their words.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
