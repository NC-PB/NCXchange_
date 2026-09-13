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

Claude (agent), 2026-09-13:

- Built `src/Ncx.Core/VirtualMachine/Events/`: one record per row of VM 7 as amended by F18 (`FileEvent`, `ProgramEvent`, `SubEvent`, `SectionEvent`, `ToolEvent`, `PreloadEvent`, `MotionEvent`, `CycleCallEvent`, `StateChangeEvent`, `VarChangeEvent`, `FlowEvent`, `SyncEvent`, `DwellEvent`, `StopEvent`, `FunctionEvent`, `BlockWriteEvent`) on the base `VmEvent` (Channel, Block, Before, After as `ChannelSnapshot`, Kind), `IVmListener.On(VmEvent)` in `Ncx.Core` (D106) and `VirtualMachine.Subscribe`. An event reads as `KIND(line): payload` (`ToString`), the line `FakeListener` records; `SyncEvent` is declared for the job scheduler (P6-01), `BlockWriteEvent` for the compilers (P3-03).
- Step 7 (`RaiseEvents`, `BlockEvents`) raises the events of a block in a fixed order: what the block opens (`PROGRAM_BEGIN`, `SUB_BEGIN`, `SECTION`), its state words (`TOOL_END` before `TOOL_BEGIN`, `PRELOAD`, `FUNCTION`, `VAR_CHANGE`), its verb (`CYCLE_CALL`, `MOTION`, then `DWELL` and `STOP`), its flow words, the `STATE_CHANGE`s (architecture 5.1), what it closes (`SUB_END`, `PROGRAM_END`). `Run` raises `FILE_BEGIN` and `FILE_END`, whose blocks no walk executes. STATIC mode records `RETURN`, so `SUB_END` comes from `SUB=END`.
- Before and After: one snapshot per block, none without a listener. The Before of a block is the After of the events raised before it, so every Before is the previous After (by reference) within a walk; a block that raises nothing leaves the chain as it was. A walk on another channel state (a second program, a subprogram nothing calls, D99) and the position an external `CALL` leaves unknown restart the chain from the state as it is (two one-line calls in `VirtualMachine.Static.cs`).
- The distance under a tool is summed from the MOTION lengths per holder, under the holder of the last `TOOL`; the blocks count from the block that brought the tool in; an unknown length adds nothing. A MOTION length is euclidean over the linear axes (a rotary axis only in the polar and cylinder frame, D102), the arc length with the travel of a helix; unknown when a moved axis is not known at both ends in one frame; rounded to the position decimals (wave-1 question #46), the sweep to 3 decimals.
- `STATE_CHANGE` names a variable by its state key (VM 3.10, as `ChannelState.Unknown` does): the axis for a position, `F`, `UNITS`, `CHAIN`, `SETPOS:X`, `TOOL:H1`, `OFFSET:LEN:H1`, `SPINDLE:S2`, `RPM:S2`, `COOLANT:STANDARD`, `FUNC:name`, `CYCLE`; the values as NCX writes them, empty for unknown or none (VM 6). `VAR_CHANGE` of an `ARG` is raised at the `CALL` block when it enters a subprogram of the file.
- Code-structure choices: `Geometry.ArcDirection` is public for `MotionEvent.Direction`; CA1716 is suppressed on `IVmListener.On`, the specification's name, with that justification; no VM code was needed, VM600-VM649 stay free.
- Performance (phase 1 risk): 50 000 `LINE` blocks run in about 60 ms without a listener and 800 ms with `FakeListener`, which also writes each of the 150 000 events as text; lazy snapshots are not needed now.
- Document fix F18: VM 7 (`length` on MOTION; the rows DWELL, STOP, FUNCTION and TOOL_POSE of the kinematics module; one record per row), architecture 5.3 (every record, `ChannelSnapshot` Before and After, `Subscribe`), architecture 9 (JUMP to REPEAT and SYNC_WAIT, SYNC_RELEASE instead of FLOW and SYNC; DWELL for the runtime estimate).
- Open questions, each a `TODO(question)` in the code: HOME raised as a MOTION (`MotionEvents`); no `TOOL_END` for the tool that stays in the spindle at `PROGRAM=END`; `CYCLE_CALL` raised at every call with the MOTIONs of `ExpandCycles` after it; the run statistics of `PROGRAM_END` (blocks and distance); under which holder a block counts on a machine with several holders; the variables `STATE_CHANGE` covers (`StateChanges`).
- Done when: the listener test of `2.5D_FRAESEN.ncx` against `tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.events.txt` holds (`ExampleEventTests`), and `TOOL_END` carries the distance and block count under the tool (`ToolEventTests`). Both hold; the task moves to `done/`.

Claude (agent), 2026-09-13, review fixes:

- Blocking finding: a `CALL` of an external program made the position unknown only after step 7 had raised the events of its block, and then restarted the chain of `Before` and `After`. So no `STATE_CHANGE` reported the change, and the next block's `Before` was not the previous `After`. Now the `CALL` block makes the position unknown itself, before step 7 (`ApplyFlowWords` through `CallsExternalProgram`), as VM 1 and 3.9 say. Its `After` carries the unknown position, its `STATE_CHANGE` events report every axis that was known (`X 10 -> ?`), and the next block starts from its `After`. `ForgetPosition` no longer restarts the chain. That replaces the restart after the external `CALL` described above; the chain now restarts only at the start of the run and of a walk on another channel state (D99). New tests: `StateChange_ExternalCall_ReportsThePositionBecomingUnknownAtTheCallBlock` and `BeforeAndAfter_ExternalCall_EveryBeforeIsThePreviousAfter`.
- Code-structure choice: a `CALL` is external when it is a string that names neither a subprogram nor a program of the file (language 4.9, 4.13). A `CALL` that names a program stays the ERROR of `FollowCall`. A block executed outside a run (the test harness) has no file, so its `CALL` is never external and leaves the position as it is, as before. INTERPRETED mode (P4-01) loads the external program instead (VM 3.6).
- Done when: unchanged, both criteria hold.
