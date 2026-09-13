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

Claude (agent), 2026-09-13. Built on `main` at P0-04 (the parser of P0-04, the catalog of P0-03, the state classes of P1-01, the machine records and `DefaultMachine` of P2-01, the geometry of P1-03a, the evaluator of P4-01a). All of section P1-02 of `implementation/11-phase-1-virtual-machine.md`, and in addition `HomeRules` (VM 3 step 5 with D100), which the phase plan lists under P1-03: it is taken into this task because the D101 `SETPOS` tests need it, and P1-03 does not redo it. Decisions implemented: D31, D35, D53, D55, D57, D60, D82, D91, D99, D100, D101, D103; F29 (the coolant channel `STANDARD`) and the architecture 5.1 part of F21. The specification is unchanged apart from that document fix; everything below is a reading of it.

- Files in `src/Ncx.Core/VirtualMachine/`. `VirtualMachine` (public: `Mode`, `Options`, `Machine`, `Diagnostics`, `Run(program)`, `Snapshot()`; internal: `State`, `Execute(block)`) in two files of one partial class: `VirtualMachine.cs` with `Execute` as the steps of VM 3 in the private methods `ParseAndValidate`, `ResolveRolesAndAxes`, `ApplyStateWords`, `ApplyFrameVerb`, `ExecuteMotion`, `ApplyFlowWords`, `ResetBlockScope`, `RaiseEvents`; `VirtualMachine.Static.cs` with the STATIC walk. Public beside it: `ExecutionMode`, `VmOptions` (with `ForMachine`, which takes block cap, call depth and unassigned from `[variables]`), `SkipBlocks` (D53), `RunResult`. Internal: `BlockContext` (what step 2 resolved), `BlockFlow`, `WordHandlers` (the key-to-handler table), `ToolChangeRules`, `FrameRules`, `ResourceResolver`, `DiameterRules`, `HomeRules`, `ProgramEndRules` (the PROGRAM=END column of VM 4). `Handlers/`: `FrameHandlers`, `ToolHandlers`, `SpindleHandlers` (with CSS, VC, RPM_MAX of 4.11), `FunctionHandlers`, `CycleHandlers` (the definition and `EndCycle`), `VariableHandlers`, `ResourceHandlers`, and `MotionHandlers` for F and FEED_MODE, the state words of the motion table, which none of the seven classes of the phase file covers. VM codes VM001-VM080 in `Model/DiagnosticCodes.Vm.cs`.
- Seams named after the steps. `ExecuteMotion` executes HOME and leaves RAPID, LINE, ARC, RETRACT and CYCLE_CALL to P1-03; `FrameRules.WorkpiecePosition` and `DiameterRules` are there for it. `RaiseEvents` is empty for P1-05. `ApplyFlowWords` records JUMP, REPEAT, RETURN and SYNC for P1-05 and P4-01. `Run` throws `NotSupportedException` for INTERPRETED (P4-01). The validation catalogue stays P1-04's: this task raises only the diagnostics of the rules it applies (resolution and D103, spindle rules 4 and 5, the tool change of 3.5, SETPOS, HOME, the call walk, a SYS_ assignment).
- UNKNOWN (VM 1; P1-01 left its form to this task): one set `ChannelState.Unknown`, mirrored in `ChannelSnapshot.Unknown`, of state keys `KEY[:ADDR]`, with the resource id or axis name as address: `F`, `RPM:S1`, `SETPOS:C`. A state word from an expression in STATIC mode names its variable there, and the variable keeps its old value until a known one replaces it. The unknown setpos shift of D101 is `SETPOS:axis`. VAR and ARG from an expression give `VariableValue.Unknown`.
- Positions (VM 3.4).
  - SETPOS: the store keeps the physical value, and the workpiece coordinate is read through the setpos shift (`FrameRules.WorkpieceCoordinate`).
    - On an axis known in the MACHINE frame only, SETPOS records machinePos minus declared and marks the axis known in the workpiece frame. It also records the axis in `FrameState.SetposAgainstMachine`, together with the sum of the chain's SHIFT entries on the axis at that moment. The store keeps the machine position and takes in only the shifts appended or removed after that. So the machine position is the store plus the chain's shifts on the axis minus the recorded sum, whether a SHIFT stood before the SETPOS or came after it.
    - The following return such an axis to the MACHINE frame at that machine position, unknown in the workpiece frame, and take it out of the record (D35, D101): ORIGIN; a change of the frame (TILT, TILT_AXIS, ROTATE, MIRROR, a RESET of one of them, WORKPIECE naming another holder); a SHIFT or SETPOS from an expression on the axis. The next SETPOS then records against the machine position again. After an external CALL the axis is unknown in every frame, and the record is emptied.
    - HOME leaves the record of an axis alone; the next ORIGIN or change of the frame drops it. P1-03's motions can make the axis known in the workpiece frame again before that; P1-03 decides with its motion rules whether the record is kept or dropped then.
    - Directly after a HOME without a reference point, the store holds the declared value and the shift is unknown. A later SETPOS on that axis keeps the shift unknown (oldPos, the physical position, is unknown) and stores the new declared value. ORIGIN then leaves the axis unknown.
    - An axis known in the MACHINE frame keeps its position at ORIGIN. It also keeps it at a SETPOS from an expression, which marks only `SETPOS:axis` unknown.
  - SHIFT folds into every axis known outside the MACHINE frame, and a RESET folds removed shifts back. That includes a shift that stood before a SETPOS against the machine position: the axis then reads the removed shift more, as every workpiece axis does.
  - These mark positions unknown outside the MACHINE frame: ROTATE, MIRROR, TILT and TILT_AXIS, a RESET that removes one of them, WORKPIECE naming another holder, and a CALL of an external program (which makes every axis unknown). An axis whose setpos shift was recorded against the machine position returns to the MACHINE frame instead, except at the external CALL. POLAR=OFF and CYLINDER=OFF mark only the axes known in their frame.
  - MOVE=TURN or MOVE on a TILT marks every rotary axis unknown; on a TILT_AXIS it puts the named axes at their angles.
- Order within a block (VM 3 step 3 says the state words do not depend on each other; four cases do):
  - OFFSET takes the holder of a TOOL in its block, found in step 2.
  - TOOL is applied before PRELOAD (language 4.4, the Nakamura `TOOL=1 OFFSET=1 PRELOAD=2`).
  - CYCLE comes last, because its default axis is the tool axis of the workplane the block leaves.
  - Spindle rules 4 and 5 are checked against the state all words leave, so `SPINDLE:MAIN=CW SPINDLE_MODE:MAIN=SPINDLE` passes in either order.
- Any TOOL word while a cycle is active is a WARNING and ends the cycle (VM 4, row cycle; VM 5). Any TOOL word with compensation on is a WARNING (VM 5); the VM 3.5 row names only TOOL=0.
- Default machine (D103).
  - It is recognized by its missing controller: `MachineIdentity.Controller` is null only there, and the loader requires it.
  - Resources created on the spot take their role as id. A work spindle created so gets the rotary axis `C_role` (C_SUB), which `C` resolves to while that spindle holds the workpiece. A spindle word, SPINDLE_MODE, SPINDLE_SYNC and WORKPIECE create a work spindle; TOOL and PRELOAD create a holder without a spindle.
  - An unknown coolant channel is treated like an unknown function.
- The five examples run STATIC without a machine file with no ERROR. The WARNINGs this task raises are the ones P1-04 expects:
  - INCREMENTAL_SUB: the three HOME WARNINGs.
  - MILLTURN_TRANSFER: the D103 WARNINGs for TURRET1, SUB, SUB_CHUCK, Z2 and MAIN_CHUCK.
  - POLAR_FACE: the HOME C WARNING, and SETPOS C=0 is accepted per D101.
- STATIC walk (D99).
  - Every program starts from a new channel state; after the run, the state is the one the last program left.
  - A CALL is followed once the events of its block are raised: return pc and locals are pushed, the ARG words assigned, and TIMES=n gives n passes, within the call depth of the options.
  - A CALL whose string value names no subprogram or program of the file is external: the position becomes unknown. A CALL of a program and a missing target are ERRORs.
  - A subprogram entered from a program counts as called. Every other subprogram is walked from `EntryStateOfUncalledSub`, with the tool rules reporting to a list nobody reads (`BlockContext.CallerRuleDiagnostics`). The other suppressions of D99 belong to the rules P1-03 and P1-04 build.
  - An ERROR stops the run after its block; an ERROR of the parser stops it before the first block.
- Open questions, marked `TODO(question)` in the files:
  - Bare SKIP under a switch list: executed (`SkipBlocks`).
  - The arc tolerance has no key in machine-config: null, the D36 defaults (`VmOptions`).
  - Which resource D103 creates for a spindle word: a work spindle. Id and axis name: the role and `C_role` (`ResourceResolver`).
  - An unknown coolant channel: ERROR with a machine file, WARNING without (`ResourceResolver`).
  - Which verbs count for "C=" of rule 4: motion verbs and SETPOS, not SHIFT or TILT_AXIS (`ResourceResolver`).
  - The row SHIFT=RESET says "the shifts" (plural), while VM 2.1 cuts at the last entry: cut at the last (`FrameRules`).
  - ORIGIN and the position of an axis known in the workpiece frame of the datum: kept, with the chain removed as by a RESET (`FrameRules`). An axis whose workpiece coordinate came from SETPOS is outside the question; VM 3.4 and D101 settle it (above).
  - TOOL=k with a different preload: VM 3.5 and the code-guidelines 2 sample keep the preload, the architecture 5.2 diagram goes to Loaded; the preload is kept (`ToolChangeRules`).
  - SAFE halved with SURFACE, CLEARANCE and DEPTH under AXIS=X (`DiameterRules`).
  - CSS set OFF at PROGRAM=END (`ProgramEndRules`).
  - A chain entry has no UNKNOWN form for an expression in SHIFT, TILT or ROTATE: 0 kept, affected axes unknown (`VirtualMachine`, `FrameHandlers`).
  - MIRROR=OFF read as the reset form (`FrameHandlers`).
  - syncPartner and syncPhase stand on the following spindle only (`SpindleHandlers`).
  - Incremental axis words under the frame verbs change nothing (`VirtualMachine`).
  - SHIFT X under DIAMETER=ON is not halved (`VirtualMachine`).
  - The frame of the rotary positions of TILT_AXIS with MOVE: MACHINE (`VirtualMachine`).
  - "Directly after a HOME" (D101): a block that does not name the axis may stand between (`VirtualMachine`).
  - The start state of programs after the first: a new channel state (`VirtualMachine.Static`).
  - The D99 entry state when the first program has no verb: initial values (`VirtualMachine.Static`).
  - ARG names outside V1 to V33 are assigned under their own name (`VirtualMachine.Static`).
  - TIMES from an expression in STATIC mode: one pass (`VirtualMachine.Static`).
- Document fix: the architecture 5.1 flowchart, with `TILT_AXIS` among the frame verbs and `RETRACT` among the motion verbs (the part of F21 left for this task).
- Shared files changed:
  - `VirtualMachine/State/ChannelState.cs` and `ChannelSnapshot.cs` of P1-01: the `Unknown` set.
  - `VirtualMachine/State/FrameState.cs` and `FrameSnapshot.cs` of P1-01: `SetposAgainstMachine`, since the second review round a dictionary from the axis to the chain's shifts on it at the SETPOS (review fixes).
  - `Model/DiagnosticCodes.Vm.cs`: a new part of `DiagnosticCodes`, next to the other parts in P0-02's folder.

Review fixes, Claude (agent), 2026-09-13 (commit "P1-02 review fixes"):

- ORIGIN after SETPOS (blocking finding).
  - Before the fix, ORIGIN left an axis whose workpiece coordinate came from SETPOS known in the workpiece frame at the value in the store. That value was a machine coordinate, or the stand-in declared value of D101.
  - Now `FrameState.SetposAgainstMachine` (mirrored in `FrameSnapshot`) names the axes whose setpos shift was recorded against the machine position (VM 3.4, D101).
  - ORIGIN returns those axes to the MACHINE frame at that position, unknown in the workpiece frame (D35). It makes an axis whose shift was unknown unknown in every frame (D100, D101), and it leaves an axis known in the MACHINE frame as it is. This did not hold for a SHIFT that stood before the SETPOS; the second round below fixes it.
- A second SETPOS on an axis that reads through an unknown shift keeps the shift unknown and stores the new declared value: newSetposShift = oldPos - declared, with the physical position unknown (VM 3.4, D101). Before, it recorded a known shift against the stand-in value.
- New tests in `SetposTests`:
  - `HOME X` / `SETPOS X=100` / `ORIGIN=1` on the mill-turn leaves X known in the MACHINE frame at 300, also with a SHIFT in between.
  - `HOME C` without a reference point / `SETPOS C=0` / `SETPOS C=10` / `ORIGIN=1` leaves C unknown; after the second SETPOS the shift is still unknown.
  - An axis homed again after `SETPOS X={$Q1}` keeps its machine position at ORIGIN.
  - The snapshot keeps the set.
- A mutation check disabled three branches in turn: the return to the MACHINE frame, the unknown shift kept by a second SETPOS, and the MACHINE-frame guard at ORIGIN. Each mutation failed its tests, and the file was restored afterwards.

Review fixes, round 2, Claude (agent), 2026-09-13 (a second commit "P1-02 review fixes"):

- Frame changes threw away the machine position of an axis whose setpos shift was recorded against it (blocking finding).
  - Before, TILT, TILT_AXIS, ROTATE, MIRROR, their RESETs, WORKPIECE, and a SHIFT or SETPOS from an expression made such an axis unknown in every frame. A later SETPOS on it raised a false VM050, which stopped a STATIC run. VM 3.4 marks the position unknown only in the new frame (for WORKPIECE, "in the new holder's frame"), and SETPOS moves nothing.
  - Now these return the axis to the MACHINE frame at its machine position and take it out of the record: `FrameRules.MarkUnknownOutsideMachineFrame`, which gained an overload for one axis, calls `ReturnToMachineFrame`. SETPOS from an expression on an axis known in the MACHINE frame keeps the machine position and marks only `SETPOS:axis` unknown.
  - The external CALL still makes every axis unknown; it now also empties the record.
- A SHIFT that stood before the SETPOS gave a wrong machine position (blocking finding).
  - Fold never took that shift out of the axis, which was in the MACHINE frame at the time. But it gave the shift back when ORIGIN or SHIFT=RESET removed it: `HOME X` / `SHIFT X=5` / `SETPOS X=100` / `ORIGIN=1` left X at Machine 305 instead of 300.
  - `FrameState.SetposAgainstMachine` (and `FrameSnapshot`) is now a dictionary from the axis to the sum of the chain's SHIFT entries on it at the SETPOS. The machine position is the store plus the chain's shifts on the axis minus that sum.
  - SHIFT=RESET still folds such a shift back into the store, as VM 3.4 says for every position known outside the MACHINE frame: X reads 105 afterwards, where it read 100, and its machine position stays 300.
  - The `SelectOrigin` comment and the Positions bullets above are corrected.
- New tests:
  - In `SetposTests`, each of these cases ends with a SETPOS that raises no ERROR:
    - the four chain transforms, each with its RESET;
    - WORKPIECE naming another holder;
    - SHIFT from an expression;
    - SETPOS from an expression, on an axis known in the MACHINE frame, and after a SETPOS against the machine position with a SHIFT.
  - Also in `SetposTests`:
    - TILT with a SHIFT before or after the SETPOS;
    - ORIGIN after `SHIFT X=5` / `SETPOS X=100`, with and without SHIFT=RESET;
    - the recorded sum;
    - two STATIC runs: a WORKPIECE change between two SETPOS, and `SETPOS X=0` after ORIGIN, which records 300.
  - `Setpos_FromAnExpression_LeavesTheShiftAndTheAxisUnknown` is now `Setpos_FromAnExpression_LeavesTheShiftUnknownAndTheAxisAtItsMachinePosition`, and it expects X at Machine 300.
  - In `StaticRunTests`: the external CALL after a SETPOS against the machine position.
- A mutation check in a scratch copy disabled six parts of the fix in turn. Each mutation failed between one and ten tests. The six parts:
  - the recorded sum;
  - the chain's shifts;
  - the return to the MACHINE frame at a frame change;
  - the path of SETPOS from an expression;
  - the path of SHIFT from an expression;
  - the emptied record at the external CALL.
- No new open question. The Done when criteria below still hold.

Done when:

- The modal summary table of section 4 has one test per row: holds. `ModalSummaryTests` has twelve tests, one per row.
- The chain tests: holds. `FrameChainTests.Chain_OriginShiftTiltAgainstOriginTiltShift_GivesDifferentChains` and `ShiftReset_AfterATilt_RemovesTheShiftAndTheTiltAfterIt`.
- The other tests the phase file lists for P1-02 exist and pass:
  - one per row of VM 3.5 and per transition of architecture 5.2 (`ToolChangeTests`);
  - `TOOL:TURRET1=3` then `OFFSET=3`, and `SPINDLE:MAIN=CW RPM:MAIN=1500` per resource;
  - an unknown role is an ERROR with a machine file and a WARNING without one (`ResourceResolutionTests`);
  - `HOME C` then `SETPOS C=0`, with and without a reference point (`SetposTests`).

Gate (after the second review round):
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2164 tests passing, 321 of them in the `VirtualMachine` test folders.
- `dotnet format --verify-no-changes`: clean.
- A mutation check confirmed the tests catch two of the rules: cutting at the first entry of the kind, and refusing SETPOS directly after a HOME without a reference point. Both mutations failed their tests, and the files were restored afterwards.
