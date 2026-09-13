# PL-01 Kinematics module

Phase: later | Milestone: later | Depends on: `P7-03` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

The separate module that computes the tool pose from the `[[node]]` tree and converts between vectors, spatial tilts and axis angles; it never changes NCX (D24).

## Scope

- `Ncx.Kinematics` subscribing to MOTION and CYCLE_CALL, `TOOL_POSE` events, conversion of `TX TY TZ` to rotary positions and back, `TILT` to `TILT_AXIS` and back for the compilers, `RETRACT` under a tilt, positions after `TILT` and `WORKPIECE` known again, MathNet.Numerics allowed here (D62).

## References

- ncx-virtual-machine.md sections 9 and 10
- machine-config.md section 9

## Done when

- The Hermle 5-axis program converts to Fanuc with `G43.5` vectors and to Heidenhain with `LN` blocks through the module, and the tool pose matches on both.

## Notes

Out of scope for 1.0; the task exists so the interfaces of 1.0 keep it possible.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
