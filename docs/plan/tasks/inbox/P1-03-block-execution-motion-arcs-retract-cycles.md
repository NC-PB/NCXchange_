# P1-03 Block execution: motion, arcs, retract, cycles

Phase: 1 | Milestone: M2 | Depends on: `P1-02` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

Targets, arc geometry, sweep arcs, the retract verb and the cycle sequence.

## Scope

- `RAPID`/`LINE` targets with absolute and incremental words, ERROR when an incremental word meets an unknown position (inside a subprogram that no program of the file calls the position becomes unknown instead, D99), `LINE` without feed, motion before `UNITS`; the vector form under `TCPM=ON` stored unresolved (D81).
- STATIC mode follows every `CALL` of a `SUB` section of the file within the configured call depth (default 8; a deeper `CALL` is the ERROR "call depth exceeded" and the subprogram is not entered) and walks the subprogram with the caller's state at that point, a `CALL` with `TIMES=n` n times in sequence, each pass from the state the previous one left; a `CALL` of an external program is not followed (the call is recorded and the position becomes unknown, D99); a subprogram nothing calls is walked once from the default entry state of D99 (units, workplane, diameter and feed mode as at the first verb of the file's first program, everything else initial, position unknown).
- `ARC`: the `CENTER` form with the tolerance check, the `R` form with the center computation of section 3.2, the `ANGLE` form (D84), helix by the tool-axis word, `WORKPLANE` orientation for the direction; under `POLAR=ON` the arc plane is the face plane of the X word (a diameter under `DIAMETER=ON`) and the C word as a length, the position known in that polar frame only, `CYLINDER=n` the same way with the cylinder axis (Z on a lathe) in place of X and C as a length on the circumference; the arc direction in both planes follows the fixed convention of VM 3.2, independent of `WORKPLANE` (D102).
- `RETRACT` (section 3.1a): tool axis only, to the limit or by a distance, unknown under a tilt without kinematics.
- `HOME` with `POINT`, `FRAME=MACHINE` blocks and the unknown rule (D35); `HOME` on an axis without a reference point in the configuration is a WARNING once per run and axis and the axis becomes unknown in every frame afterwards (D100).
- `CYCLE_CALL` sequence of section 3.3 along `AXIS`, with `CYCLE_F`, `CYCLE_DWELL`, `PECK`, `CYCLE_RETRACT`; the `ExpandCycles` option raising MOTION events (D37).
- A `CYCLE:<controller>=n` definition stores its native parameters as unresolved words in source order and `CYCLE_CALL` of such a cycle positions the plane axes only, without `DEPTH`/`CLEARANCE` checks and without `ExpandCycles` events (VM 2.6, 3, 3.3; D94).
- Arc tolerance from the configuration, defaults 0.01 mm and 0.0005 in (D36).

## References

- ncx-virtual-machine.md sections 1, 3.1, 3.1a, 3.2, 3.3, 3.9 (with D99, D100, D102)
- ncx-language.md sections 4.3, 4.7

## Done when

- Arc tests with the numbers of the language examples (`ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50` from 50.534/69.993) and an `R` arc whose center is recomputed to the known value.
- A sweep of 737.956 degrees with `IZ=-5.4` ends at the right point and reports two full turns plus a rest.
- The drilling example of the language produces the four positions and the retract plane.
- The hexagon of `POLAR_FACE` checks without ERROR and closes: under `POLAR=ON` the arcs and lines run in the face plane of X (halved, D60) and C, the vertices lie at radius 17.32 (D102).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
