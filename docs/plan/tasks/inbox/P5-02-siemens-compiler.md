# P5-02 Siemens compiler

Phase: 5 | Milestone: M8 | Depends on: `P5-01` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

NCX to 840D sl, per section 12 of `controllers/siemens.md`.

## Scope

- Units and archives per `program_layout`, `PROC`/`RET`, `M30`; `=` addresses; `I=AC()`, `CR=`, `TURN=`; `T`/`M6`/`D` per `[tool_change]`; `SETMS` in its own block, `S<n>=`, `M<n>=`; frames as `TRANS` plus `ATRANS`/`AROT`/`AMIRROR` in chain order; `CYCLE800(...)` from the template; cycles with the full signatures from the catalog and `MCALL`; `WAITM` from `[sync]`; `CYCLE832` for `TOLERANCE`; `RAW:SIEMENS` verbatim.

## References

- controllers/siemens.md section 12
- controller-mapping.md (Siemens column)
- examples/machines/millturn1.toml (D104)

## Done when

- `MILLTURN_TRANSFER.ncx` compiles for `millturn1.toml` (D104) using the generic words (`dmg-ctx-840d.toml` stays the documentation of the structure programming, D68).
- The two corpus programs of P5-01 round-trip under the comparison rules (M8). This closes phase 5.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
