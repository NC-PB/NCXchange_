# P2-03 Cycle catalogs

Phase: 2 | Milestone: M3 | Depends on: `P2-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The per-controller cycle catalogs as data, with the contour reference of D65.

## Scope

- `cycles/fanuc.toml`, `cycles/heidenhain.toml`, `cycles/siemens.toml` per section 6 of the configuration: NCX cycle name, parameter names and kinds, the native template with its parameter order, `CYCLE_F` and `CYCLE_DWELL` placement, `AXIS` handling, the `CONTOUR=name` reference for `G70`..`G76` and `CYCLE952`, expansion rule keys.
- The built-in drilling family is code; the catalog adds turning cycles (`TURN_OD`, `THREAD`, `FACE`, `ROUGH_TURN`...), pockets and patterns as far as the corpus needs them.
- `CYCLE:<controller>=n` native cycles pass through to the same family with every key the catalog does not know as a native parameter, in source order after the cycle words; a compiler for another controller family reports the block as an ERROR like `RAW` (D45, D94).

## References

- ncx-language.md section 4.7.1
- machine-config.md section 6
- controller-mapping.md section 5
- controllers/*.md cycle sections

## Done when

- A catalog entry for `G81`/`CYCLE81`/`CYCL DEF 200` exists in the three catalogs and maps the same NCX words.
- The modal Fanuc turning cycles read as `CYCLE=` plus `CYCLE_CALL` per repeat block (tested in phase 3, the entries exist now).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

- Claude (agent), 2026-09-13, P2-03a (part one, the catalog data; branch `p2-03a-cycle-data`): wrote `cycles/fanuc.toml`, `cycles/heidenhain.toml` and `cycles/siemens.toml` in the format of machine-config 6. Fanuc: the drilling family on the machining center codes (`G81`, `G82`, `G83`, `G73`, `G84`, `G85`, `G86`, all `modal`), the system A turning cycles `TURN_OD`, `TURN_ID` (`G90`), `THREAD` (`G92`), `FACE` (`G94`) as `modal`, and `FINISH`, `ROUGH_TURN`, `ROUGH_FACE`, `GROOVE`, `COMPOUND_THREAD` (`G70`..`G72`, `G75`, `G76`, `contour = ["P", "Q"]`, D65; `G75` and `G76` since the review fixes below) as one-shot. Heidenhain: 200 to 209, 240, 241, 251 to 254, 256, 257 with the Q parameters of heidenhain 5, `CLEARANCE`, `DEPTH` and `SAFE` in `absolute_from_surface` (NCX = Q203 + native), signatures of 200, 201, 203, 207 from `examples/sources/BOHREN.h`. Siemens: `CYCLE81` to `CYCLE86`, `CYCLE830`, `CYCLE840` with the documented leading part of their sl signatures, `AXIS = "_AXN"` on `CYCLE83`, `CYCLE84`, `CYCLE840`, `CYCLE_F = "F"` (the modal feed) on `CYCLE81`..`CYCLE83`, `VARI` fixed for `PECK` and `CHIP_BREAK`. Catalog names beyond the documents' `RECT_POCKET`: `BACK_BORE`, `UNIVERSAL_PECK`, `TAP_COMPENSATING` (Heidenhain 206 and Siemens `CYCLE840`, the same "tapping with a compensating chuck"), `BORE_MILL`, `TAP_CHIP_BREAK`, `CENTERING`, `SINGLE_LIP_DRILL`, `DEEP_HOLE`, `CIRC_POCKET`, `SLOT`, `CIRC_SLOT`, `RECT_STUD`, `CIRC_STUD`. Document fix: machine-config 6 got the entry keys `modal` (architecture 6 `CycleEntry.Modal`, language 4.7.1), `contour` (F25, D65, architecture 13), `signature` (the parameter order of siemens 12 and heidenhain 8) and `fixed` (the `VARI` of controller-mapping 5). What the documents do not give is a `TODO(question)` comment in the files: parameter kinds; the Fanuc surface clearance; lathe drilling codes by `AXIS` and entries by G-code system (system C: fanuc 3 against controller-mapping 5); `TURN_OD` against `TURN_ID`; the words of `G92` and of `G70`..`G76` beyond `P`/`Q`; `P`/`Q` of `G75`/`G76` (an OD groove and a compound thread have no contour blocks; see the review fixes below); `DRILL` against `DRILL_DWELL` on 200 and `PECK` against `CHIP_BREAK` on 203; values for unmapped Q parameters; the own parameters and signatures of 202, 204..206, 208, 209, 240, 241, 251..257; `PECK` on `CYCLE83`; `MPIT`; the feed of `CYCLE86`, `CYCLE830`, `CYCLE840`; the tails of the Siemens signatures. Remains for part two: `CycleCatalog`, `CycleEntry` (with `Contour`, `Signature`, `Fixed`), `CycleCatalogLoader`, the drilling family in code, `CYCLE:<controller>=n` pass-through (D45, D94), the tests of the phase file, the fix of architecture 6 and 13, and moving this file to `done/`.
- Claude (agent), 2026-09-13, P2-03a review fixes (branch `p2-03a-cycle-data`): (1) `CYCLE_DWELL = "P"` on every Fanuc drilling entry (`G81`, `G83`, `G73`, `G85`, `G86`; `G82` and `G84` had it), following controller-mapping 5 ("`F` and `P` on the `G8x` block"), so a dwell of cycle 200 (`Q211`) or `CYCLE81` (`DTB`) survives on `G81`; fanuc 6 names the dwell only for `G82`, and whether a Fanuc control executes `P` on the other codes is a `TODO(question)`. Status of the first "Done when" criterion: `G81`, `CYCL DEF 200` and `CYCLE81` now map `CLEARANCE`, `DEPTH`, `CYCLE_F`, `CYCLE_DWELL` as parameters; `SURFACE` and `SAFE` have no address on Fanuc and come from reader and compiler rules (surface = `R` minus the clearance or 0, initial level), and `CYCLE_RETRACT` is a rule in all three (`G98`/`G99`, with or without `Q204`, always `RTP`), no parameter. So the criterion holds for each entry plus the rules of its family, written in the header comments of the three files, not for the `params` keys alone; the test of part two compares entry plus rules (machine-config 6 has no key that lists rule-carried words, a `TODO(question)`). (2) `GROOVE` (`G75`) and `COMPOUND_THREAD` (`G76`) got `contour = ["P", "Q"]` as fanuc 6, controller-mapping 5, language 4.7.1 and the phase file say; the `TODO(question)` stays: an OD groove and a compound thread have no contour blocks, and the Fanuc lathe manuals use `P`/`Q` of `G74`..`G76` for infeeds and thread data; taking `contour` off needs a maintainer decision row amending those three documents. Remains as above.
