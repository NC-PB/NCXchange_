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
