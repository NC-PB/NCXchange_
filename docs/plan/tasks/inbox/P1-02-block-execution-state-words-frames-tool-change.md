# P1-02 Block execution: state words, frames, tool change

Phase: 1 | Milestone: M2 | Depends on: `P1-01` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

The STATIC execution loop for every non-motion word.

## Scope

- The seven steps of section 3: parse, resolve roles and axes (section 3.8, against the configuration or the default), apply state words, the frame verbs `SHIFT`, `TILT`, `TILT_AXIS`, `SETPOS` and the frame word `ORIGIN` (the `RESET` forms cutting the chain, section 3.4; `SETPOS` on an axis known in the MACHINE frame only records the shift against the machine position; directly after a `HOME` of that axis without a reference point the shift stays unknown and the axis becomes known in the workpiece frame with the declared value; the ERROR stays for every other axis unknown in every frame, D100, D101), reset block-scoped items, raise events.
- Tool change rules of section 3.5 (`PRELOAD`, `TOOL=n`, bare `TOOL`, `TOOL=0`, preload consumed, mismatch WARNING, `OFFSET` forms) as a transition table.
- Spindle words per resource, `SPINDLE_MODE`, `SPINDLE_SYNC` with `PHASE`, `CSS`/`VC`/`RPM_MAX`, coolant (default channel `STANDARD`) and `FUNC` tables, `WORKPIECE` with the position-unknown rule (D57).
- `DIAMETER` halving of the D60 word set on the way into the position store.

## References

- ncx-virtual-machine.md sections 3, 3.4 (with D101), 3.5, 3.8, 4 (modal summary)
- ncx-language.md sections 4.2, 4.4, 4.5, 4.6, 4.10, 4.11

## Done when

- The modal summary table of section 4 has one test per row.
- The chain tests: `ORIGIN`, `SHIFT`, `TILT` versus `ORIGIN`, `TILT`, `SHIFT` give different frames; `SHIFT=RESET` after a tilt removes the shift and the tilt after it.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
