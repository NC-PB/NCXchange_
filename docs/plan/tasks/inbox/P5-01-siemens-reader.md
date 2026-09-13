# P5-01 Siemens reader

Phase: 5 | Milestone: M8 | Depends on: `P3-07` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

SINUMERIK 840D sl programs into canonical NCX, per section 11 of `controllers/siemens.md`.

## Scope

- Tokenizer for `%_N_..._MPF/SPF` units and archives, `;$PATH`, `N`, `;`, `/n`, addresses with `=` and extensions, expressions as values, strings, `( )` argument lists, labels `NAME:`.
- G groups with the source-side state, `AC()`/`IC()`/`DC()`/`ACP()`/`ACN()`, arcs (`I J K`, `I=AC()`, `CR=`, `AR=`, `TURN=`, `CIP`, `CT`), polar with poles, chamfer and contour forms expanded, feed words, path control and orientation words as `RAW` modal words.
- Frames per section 4 of the Siemens document (replacing versus additive, `G58`/`G59`, `CYCLE800` into `TILT`/`TILT_AXIS` with `MOVE` and a `RETRACT`, `$P_UIFR` as `RAW`), `G53`/`G153`/`SUPA`, `G74`/`G75`, `PRESETON`, `DIAMON`/`DIAMOF`/`DIAM90`, `G70`/`G71`/`G700`/`G710`.
- Tools and spindles: `T`, `T=`, `T="name"`, `T<n>=`, `M6`, `D`, `T0`, `S<n>=`, `M<n>=3`, `SETMS`, `G96`.. with `LIMS`, `G25`/`G26`, `SPOS`/`SPOSA`/`M19`/`M70`, `COUPON`/`COUPOF`.
- Cycles per the sl signatures with `_AXN` as `AXIS`, `MCALL` handling, patterns expanded; flow structures lowered; `PROC`/`RET`/`M17`, `EXTERN`, `L`, `name P3`, `CALL`, `EXTCALL`; `WAITM`/`INIT`/`START`/`WAITE`.
- `MSG`, `STOPRE`, `WORKPIECE`, synchronized actions, interrupts, `DEFINE`: `RAW`.

## References

- controllers/siemens.md
- controller-mapping.md (Siemens column)
- machine-builders.md section 2

## Done when

- The Hermle C22 U program and a Burkhardt+Weber program of the corpus convert without loss (every block accounted for, `RAW` only where the document says so).
- Every `.mpf` of the corpus converts without a crash.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
