# Phase 5: Siemens

Status: 2026-09-11, not started. Milestone M8. Tasks P5-01, P5-02. Closed when `MILLTURN_TRANSFER.ncx` compiles for its machine file with generic words only, and the Hermle C22 U and a Burkhardt+Weber program of the corpus read without loss and compile back (`../plan/phases.md`).

## Entry state

Phase 3 closed (the reader and compiler frameworks, the acceptance project, the comparison rules). `millturn1.toml` exists (D104, P2-04). The two corpus programs are requested from the maintainer at the start of the phase and placed under `NCX_CORPUS`; the acceptance tests skip without them and say so.

## Decisions needed first

D104 answered. Expected to arise, recorded as drafts and not implemented: a `MESSAGE="..."` word for `MSG("text")` (controller-mapping 11 already names it as a candidate; until then `RAW:SIEMENS`); whether `G700`/`G710` versus `G70`/`G71` is remembered for the compiler (controller-mapping 1 says the reader reports which pair the source used; a `[format]` key is the likely home). D68 holds: GILDEMEISTER structure programming stays documentation; nothing in this phase may prevent it.

## Tasks

### P5-01 Siemens reader

Files in `src/Ncx.Readers/Siemens/`:

- `SiemensTokenizer`: units (`%_N_name_MPF`, `_SPF`, `_INI`) and job archives with several units in one file, `;$PATH` as header comments, `N` numbers, `;` comments, `/n` skip levels, addresses with `=` and numeric extensions (`X1=`, `S2=`, `M2=3`, `C4=`), expressions as values (`X=R1*2`), strings, `( )` argument lists with empty positions, labels `NAME:`, `DEF` lines, the recommended but not required word order; the `<PROG_BEGIN_C1>` recipe markers of the STAMA post as `RAW`.
- `SiemensReader`, `SiemensGroups` (the G groups of `siemens.md` 2 in the source state; the words NCX has no meaning for kept as `RAW` modal words and written back in place by the compiler), `SiemensMotion` (`G0`..`G3`, `G90`/`G91`, `AC()`/`IC()` per word, `DC()`/`ACP()`/`ACN()` as `C=` with the direction kept as `RAW`, arcs with `I J K`, `I=AC()`, `CR=`, `AR=`, `TURN=` as `ANGLE`, `CIP` and `CT` converted with the source state, polar `AP=`/`RP=` with the `G110`..`G112` poles converted, chamfers `CHF=`/`CHR=`/`RND=`/`RNDM=` and `ANG=` contour forms expanded per D58, feed words `F`, `G94`/`G95`, `FB=` restored after the block, `G93`, `FZ=`, `FGROUP`, `FL[]` as `RAW`), `SiemensFrames` (`G54`..`G57`, `G505`..`G599`, `G500`, `TRANS`/`ROT`/`MIRROR`/`SCALE` cutting the chain then appending, `ATRANS`/`AROT`/`AMIRROR` appending, `ROT RPL=`, `G58`/`G59` as `SHIFT`, `CYCLE800` decoded per `siemens.md` 4 into `TILT` or `TILT_AXIS` with `MOVE` from `_DIR` and `_ST` and a `RETRACT` from `_FR`, `CYCLE800()` as `TILT=RESET`, `ROTS`/`AROTS`, `G53`/`G153`/`SUPA` as `FRAME=MACHINE` with `D0` as `OFFSET=0` in the block, `G74` as `HOME`, `G75 FP=` as `HOME POINT=`, `PRESETON` as `SETPOS`, `DIAMON`/`DIAMOF`/`DIAM90` (the `IX` doubled), `G70`/`G71`/`G700`/`G710`, `$P_UIFR` writes as `RAW`), `SiemensTools` (`T`, `T=`, `T="name"`, `T<n>=`, `M6`, `D`, `T0`, `D0`, `S<n>=`, `M<n>=3`, `SETMS` in the source state, `G96`/`G961`/`G962`/`G97`, `LIMS`, `G25`/`G26`, `SPOS`/`SPOSA`/`M19`/`M70`, `COUPDEF`/`COUPON`/`COUPOF` as `SPINDLE_SYNC` with `PHASE`), `SiemensCycles` (the sl signatures of `siemens.md` 7 through the catalog with the trailing mode integers, `_AXN` as `AXIS`, `MCALL name(...)` making the cycle modal with a `CYCLE_CALL` per position block, `MCALL` alone as `CYCLE=OFF`, a direct call as `CYCLE=` plus `CYCLE_CALL`, the modal `F` before `MCALL` as `CYCLE_F` and restored afterwards, `HOLES1`/`HOLES2`/`CYCLE801`/`CYCLE802` expanded into calls, `CYCLE832` as `TOLERANCE`), `SiemensFlow` (`GOTOF`/`GOTOB`/`GOTO` with `IF`, `CASE` lowered per case, `GOTOS` as a `JUMP` to a label after the header, `IF`/`ELSE`/`ENDIF`, `LOOP`, `FOR`, `WHILE`, `REPEAT`/`UNTIL` lowered to `LABEL`/`JUMP`/`IF`, `REPEAT label P=`, `REPEAT start end P=` as a `SUB`, `PROC` with parameters as `ARG` names, `EXTERN`, `RET`/`M17` as `SUB=END`, `L100`, `name`, `name P3`, `CALL "name"`, `EXTCALL` as `CALL="name"`, `PCALL`/`ISOCALL` as `RAW`, `R` parameters and `DEF` numeric names as `VAR`, the type kept for the compiler, `DEFINE` macros expanded), `SiemensChannels` (`WAITM`/`WAITMC` as `SYNC` with `WITH`, `INIT`/`START`/`WAITE` as the channel words, `SETM`/`CLEARM`/`GET`/`RELEASE` as `RAW`), `SiemensRaw` (`MSG`, `STOPRE`, `WORKPIECE`, `SETAL`, non-numeric `DEF`, synchronized actions, interrupts, `ORI*`, path control words, `COMPCAD`, `TRAORI(n, ...)` with offsets, `_INI` units as `RAW` block lists of their own section; the full list of controller-mapping 9 and `siemens.md` 11 rule 8).

Cite: controllers `siemens.md` 1 to 11; controller-mapping (the Siemens column of every section), 9, 11; machine-builders 2; D31, D58, D68, D81 to D86.

Tests first: one test per rule of `siemens.md` 11 and per Siemens cell of the mapping tables (`TRANS_ThenATRANS_CutsChainThenAppends`, `CYCLE800Mode39_BecomesTiltAxisWithRetract`, `MCALLCYCLE81_PositionsBecomeCycleCalls`, `SETMS2_BindsBareSToSpindle2`, ...); the acceptance: the Hermle C22 U program and one Burkhardt+Weber program convert with every block accounted for and `RAW` only where the documents say so (the report of the batch runner is the evidence, committed under `tests/corpus-reports/`); every `.mpf` of the corpus converts without a crash; the INDEX archive reads as one file with several sections.

### P5-02 Siemens compiler

Files in `src/Ncx.Compilers/Siemens/`: `SiemensCompiler`, `SiemensProgramFrame` (`%_N_NAME_MPF`/`_SPF` headers per `program_layout`, `;$PATH`, `PROC name` and `RET` or `M17` per `sub_end`, `M30` per `program_end`), `SiemensMotion` (`=` after every multi-letter address and extension, `I=AC()` for absolute centers, `CR=` for the `R` form, `TURN=` for sweeps or split turns for an older 840D per `dialect`, `G94`/`G95`, `G41`/`G42`/`G40`, vectors as `A3= B3= C3=` and `A4=`..`C5=` under `TRAORI`), `SiemensTools` (`T="name"` or `T1` and `M6` per `[tool_change]`, `D` from `OFFSET`, `SETMS(n)` in its own block when the master spindle changes, `S<n>=` and `M<n>=3` for the other spindles, `G96 S LIMS=`, `SPOS`, `COUPON`/`COUPOF` from `[spindle_sync]`), `SiemensFrames` (`G54`.., the chain as `TRANS` for the first entry and `ATRANS`/`AROT`/`AMIRROR` for the rest in program order, `CYCLE800(...)` from the `[transform]` template with `{dir}` from `MOVE`, `G53`/`SUPA` with `D0` for `FRAME=MACHINE`, `G74`/`G75` for `HOME`, `PRESETON` for `SETPOS`, `DIAMON`/`DIAMOF` per `programming`, `TRAORI`/`TRAFOOF`, `TRACYL`/`TRANSMIT`, `CYCLE832` from `[tolerance]`), `SiemensCycles` (the full sl signature from the catalog, `MCALL` for repeated calls, `MCALL` alone for `CYCLE=OFF`), `SiemensFlow` (`IF ... GOTOF` chains from `JUMP` and `IF`, labels as `NAME:`, `L` calls, `name P3`, `R` and `DEF` variables from `[variables]`, `EXTCALL`), `SiemensChannels` (`WAITM(mark, channels)` from `[sync]`, `START`/`WAITE`), `RAW:SIEMENS` verbatim.

Cite: controllers `siemens.md` 12 (every rule); controller-mapping (the Siemens column); machine-config 2, 3, 5; D31, D48, D68.

Tests first: one test per rule of `siemens.md` 12; the acceptance:

```
dotnet run --project src/Ncx.Cli -- compile docs/spec/examples/MILLTURN_TRANSFER.ncx --machine millturn1
```

produces a program with the generic words of the example's own comments (`COUPON(S2,S1)`, `M68`, `G0 Z2=-58`, `M2=70`, `S3=6000 M3=3`); the two corpus programs round-trip under the comparison rules (M8).

## Risks and open ends

- The Siemens surface is the widest of the three; the reader must stay a folder of small files and the `RAW` list the first answer for anything not in the mapping. Every construct promoted from `RAW` to a word is a decision, not a reader change.
- `CYCLE800` decoding depends on `_MODE` bits and the kinematics name; the reader keeps the form the source used (D82) and never converts between `TILT` and `TILT_AXIS`.
- Case: Siemens names are case-insensitive except tool names in `T="..."`; the NCX normalization to uppercase must not touch string values.
- The comparison rules of P3-07 need one addition for Siemens: `X10` and `X=10` are equal; add it to the comparer with a test.

## Exit checklist

- `phases.md` row 5: `MILLTURN_TRANSFER` compiles for `millturn1.toml`; the Hermle and Burkhardt+Weber programs read without loss and compile back; the `.mpf` corpus report shows zero crashes.
- Decision drafts for the constructs the corpus raised (`MESSAGE`, the inch pair) written in `rationale.md`, unanswered or answered.
- P5-01, P5-02 in `done/`.

## Log

(filled when the phase starts and when it closes)
