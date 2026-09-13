# Sample corpus

Readers are only as good as the programs they have seen. Two sets exist.

## 1. In the repository: `../spec/examples/sources/`

Small, licensed for the repository, and each one the acceptance test of a milestone (see the README there):

| Files | Controller | Used by |
|---|---|---|
| `2.5D_FRAESEN.fanuc.nc`, `2.5D_FRAESEN.h` | Fanuc, Heidenhain iTNC 530 | M4, M5, M6: both must read to `../spec/examples/2.5D_FRAESEN.ncx` and compile back without loss |
| `BOHREN.fanuc.nc`, `BOHREN.h` | Fanuc, Heidenhain | M4, M6: the drilling cycles of both controllers into the same `CYCLE=` words |
| `3D_FRAESEN.fanuc.nc`, `3D_FRAESEN.h` | Fanuc, Heidenhain | M6, M7: long point lists, the segment length analytic |
| `NAKAMURA_WY250L_O1000.path1.nc`, `.path2.nc` | Fanuc 31i-B, Nakamura dialect, two paths | M9: a job with wait marks, transfer and `RAW:BUILDER` |

The five `.ncx` files next to them are the language examples and the acceptance tests of M1 and M2 (parse and format to themselves; check with no ERROR without a machine file, D103, and with their machine files from M3 on).

## 2. Outside the repository: the maintainer's corpus

About 480 programs collected from customers and manuals, kept by the maintainer (they are customer property or copyrighted manual samples and stay out of the repository). Ask for it when a reader is ready for robustness runs: `convert` must never crash on any of them and must report every unread block as `RAW`. What an automated survey of it showed, and what that meant for the language, is section 11 of `../spec/controller-mapping.md`; the short version:

- 415 files in the first pass: 177 Fanuc/ISO (Mazak EIA, Hyundai WIA, Doosan, Matsuura, Mori Seiki, Nakamura, Biglia, Hermle ISO), 151 Heidenhain (Hermle, Fehlmann, DMU), 68 Siemens (including TopSolid postprocessor sources), 9 Okuma OSP (out of scope), 10 other. Almost all of it is CAM output: `L` 345 593 lines and `CR` 50 240 on the Heidenhain side, `G1` 20 381, `G3` 14 901 and `G2` 9 754 on the Fanuc side, hardly any hand-written macros (`FN` 5 lines, `G65` in 8 files).
- The words the language lacked were found here: `LN` blocks with surface normal and tool vector (one 5-axis program, 1 467 lines), `PLANE AXIAL` (1 392 lines in 10 files) against `PLANE SPATIAL` (514), `CYCL DEF 19` (476 lines in 32 files), `CP IPA` helices beyond 360 degrees (2 388 lines in 40 files), `M140 MB MAX` (130 lines in 13 files), `CYCL DEF 32 TOLERANCE` (132), `M126`/`M116`, `G43.4`/`G41.2` (Mazak), `G68.1`/`G53.1`. They became the words of decisions D81 to D86.
- 62 Siemens `.mpf` programs in the second pass (Monforts 25, DMG CLX and Gildemeister MD 11, Burkhardt+Weber 12, STAMA 2, Doosan on Siemens 1, Hermle 1, Pittler 2, INDEX 4, DMG 5-axis 4): `MCALL CYCLE81` and `CYCLE86` are the only cycles used, `CYCLE800` in the axis-wise mode is the tilt, `TRAORI` the 5-axis form, `SETMS`, `LIMS`, `G96`/`G95`, `DIAMON`, `SUPA`/`G53 ... D0` retracts, `$P_UIFR` datum writes, `WAITM` on the INDEX and the STAMA, `MSG(...)` before every operation (273 lines in 47 files), `STOPRE` (105 in 20), `GOTOF` restart structures (32 in 31), `T="name"` (60 in 15); absent: `CYCLE95`/`951`/`952`, the pocket and pattern cycles, `WHILE`/`FOR`/`CASE`, `ATRANS`, `MIRROR`, `SCALE`, `AP=`/`RP=`, `A3=` vectors, `FGROUP`, `COMPCAD`, `CYCLE832` itself (only the STAMA wrapper).
- OEM cycles (Hermle 3xx, Matsuura `G120`/`G121`/`G400`, `G153`/`G154`, `G254`/`G255`) are catalog entries or `RAW:BUILDER`; `BLK FORM` and `WORKPIECE` stay header information.

## 3. How to use a corpus program

1. `ncx convert program.nc --machine <closest machine file>` and read the diagnostics: every `RAW` is a WARNING that names the source block; a crash is a bug.
2. `ncx check` on the result; then `ncx compile` back to the same controller family and diff against the source, ignoring block numbers, formatting and comment placement (the comparison rules of `../architecture/code-guidelines.md`, section 8).
3. A construct that appears in more than one customer's programs and is neither a word nor a `RAW` of a builder is a candidate for the language: write it down as a question with a recommendation and the counts under a new `D` number in `../decisions/rationale.md`; the row in `../decisions/decisions.md` follows once the maintainer has answered.
