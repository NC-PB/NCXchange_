# P1-05 Events with Before and After

Phase: 1 | Milestone: M2 | Depends on: `P1-04` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The observer surface that analytics, trace, annotate and plugins use.

## Scope

- The event table of section 7: FILE_BEGIN/END, PROGRAM_BEGIN/END, SUB_BEGIN/END, SECTION, TOOL_BEGIN/END, PRELOAD, MOTION (with center, sweep, vectors), CYCLE_CALL, STATE_CHANGE, VAR_CHANGE, JUMP/CALL/RETURN/REPEAT, SYNC_WAIT/RELEASE; BLOCK_WRITE is the compiler's (phase 3).
- Every event carries `Before` and `After` snapshots and the block.
- `IVmListener` in `Ncx.Core`, where its caller is (D106); listeners receive read-only snapshots.

## References

- ncx-virtual-machine.md section 7
- architecture.md section 5.3 (events)

## Done when

- A listener test records the event sequence of `2.5D_FRAESEN.ncx` and compares it with an expected text file.
- TOOL_END carries the distance and block count under the tool.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
