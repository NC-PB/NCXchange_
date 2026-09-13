# P4-03 Analytics: segment length and tool vector change

Phase: 4 | Milestone: M7 | Depends on: `P4-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The two 5-axis analytics: distance between consecutive end points and the angle between consecutive tool vectors.

## Scope

- Per MOTION: the segment length (workpiece frame, known positions only), a histogram and the minimum, maximum and mean over the range.
- Tool vector change: from `TX TY TZ` when present, else from the rotary axis positions with the machine's rotary axes as given in `[[axis]]` (A/C table, B head: a fixed convention until the kinematics module exists, documented in the report), the angle between consecutive vectors, histogram and extremes.
- Both take `--from`/`--to`; both list the ten worst blocks with their line numbers.

## References

- architecture.md section 9
- ncx-virtual-machine.md section 8

## Done when

- Results on `3D_FRAESEN` and on a 5-axis program of the corpus (ask the maintainer for the 5X pair) are recorded as reference files in the acceptance project (M7).
- This closes phase 4.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
