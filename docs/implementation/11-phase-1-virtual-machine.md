# Phase 1: virtual machine

Status: 2026-09-11, not started. Milestone M2. Tasks P1-01 to P1-07. Closed when the five examples check with no ERROR without a machine file (D103), every validation rule has a test, the coolant clutch rule expands and restores, and trace and annotate output exists for `2.5D_FRAESEN.ncx` (`../plan/phases.md`).

## Entry state

Phase 0 closed. **In addition, P2-01 (TOML schema and loading) and P2-02 (templates) are done before P1-01 starts** (`20-schedule.md`, reason 1): the VM reads the machine configuration from its first task (roles, defaults, axes, tolerance, reference points, limits), so it gets the real `MachineConfig` and the built-in default machine of D103 instead of a throwaway "default configuration object". The definition of done (code-guidelines 12) applies in full from here (D78).

## Decisions needed first

Batch 2 of `02-decisions-proposed.md`: D99 (subprogram entry state), D100 (`HOME` without reference points), D101 (`SETPOS` after `HOME`), D102 (the polar plane), D103 (no machine file), D104 (`millturn1.toml`, needed for the with-machine check in phase 2 but the example must at least parse and check without it now), D106 (where `IVmListener` and `IProgramRewriter` live). The document fixes F18, F20 and the architecture 5.1 part of F21 land in the tasks named there; the vector types of F19, F29, F30 and the rest of F21 were made on 2026-09-11 and 2026-09-13. Batch 2 was asked and answered 2026-09-11, every decision as recommended with D99, D100, D101, D102, D103 and D106 clarified (`../decisions/rationale.md`, `../decisions/decisions.md`); nothing in this phase waits for a decision any more and nothing blocks P1-01, and D106 fixes `IVmListener`, `IProgramRewriter`, `RewriteResult` and the `RewriteContext` abstraction in `Ncx.Core`. D107 puts the records of the machine model in `Ncx.Core` as well, so the VM reads them without `Ncx.Config`.

## Tasks

### P1-01 VM state classes and snapshots

Files in `src/Ncx.Core/VirtualMachine/State/`, one class per VM 2 table, mutable, `sealed`, only the VM mutates them (code-guidelines 7):

- `ChannelState` (ChannelId, Program, Frame, Motion, Cycle, Flow, Vars, Spindles by resource id, Holders by resource id, LastHolder, Coolant by channel name, Functions by name, WaitingAt, Finished) with `Snapshot()` returning `ChannelSnapshot`, an immutable deep copy (records with `IReadOnlyList` and `IReadOnlyDictionary`), the `Before` and `After` of every event.
- `ProgramState` (Section, Active, Name, Number, Ended).
- `FrameState` (Units with UNKNOWN, Workplane, Origin, Chain as `List<TransformEntry>`, SetposShift per axis, Diameter, Cylinder with radius, Polar, Tcpm, RotaryPath, RotaryFeed, Tolerance as `ToleranceState` (Value or OFF, Rotary, Mode), WorkpieceHolder, MachineFrameBlock).
- `TransformEntry` (Kind: Shift, Rotate, Mirror, Tilt, TiltAxis; Shift per axis as `decimal`; Angle; Mirrored axes; Angles A B C as `decimal`; Move; Rot).
- `MotionState` (Position as `Dictionary<string, AxisPosition>`, Feed, FeedMode, Comp, BlockVerb, ToolVector, SurfaceNormal, both as `decimal` triples, unknown when absent).
- `AxisPosition` (Value, Frame: Workpiece, Machine, Polar, Cylinder; Known).
- `HolderState` (SpindleTool as `ToolRef`, Preloaded as `ToolRef?`, OffsetLen, OffsetRad, OffsetCombined, PendingRememberedTool for the `PRELOAD=0` return of architecture 5.2).
- `SpindleState` (Direction, Rpm, Mode, Orientation, SyncPartner, SyncPhase, Css, Vc, RpmMax).
- `CycleState` (Name or OFF, Axis, Parameters as words).
- `FlowState` (Pc, Calls, Repeats, Labels per section, BlocksExecuted, RestoreStack for D95).
- `VariableStore` (Get, Set, GetSystem, PushLocals, PopLocals; UNKNOWN as a value).

Initial values exactly as the VM 2 tables say, from the configuration where the table says "from TOML": the `MachineConfig` loaded by P2-01, or its `DefaultMachine` of D103 when no file is given (built in P2-01, not here).

Cite: VM 2 (every table), 3.10 (restore stack); architecture 5 (class diagram, amended per F19 and F20).

Tests first (`tests/Ncx.Core.Tests/VirtualMachine/State/`): one test per row of the VM 2 tables setting the variable and reading it back from a snapshot while the live state changes; the start-position rule (an axis with `home` starts known in the MACHINE frame at that point and unknown in the workpiece frame, an axis without `home` unknown in every frame, VM 2.2, 3.4; with `nakamura-ntjx.toml` after P2-04 a `HOME` makes the axis known in the MACHINE frame, without it the D100 WARNING and still unknown); `ToolRef` equality for number and name; the `PRELOAD=0` return to Empty or Loaded.

Document fix in this task: the rest of F19 (`LastHolder`, `WaitingAt`, `Finished`, `ToleranceState` in the architecture 5 diagram; its vector and shift types were fixed on 2026-09-13) and F20 (`ToolRef` for the tool in `HolderState`, `ToolRef?` for the preload, now drawn as `int?`, in the diagram and in the code-guidelines 2 sample).

### P1-02 Block execution: state words, frames, tool change

Files in `src/Ncx.Core/VirtualMachine/`:

- `VirtualMachine` (Mode, Options, Machine, State, Diagnostics, `Run(program)`, `Execute(block)` as the seven steps of VM 3 in seven private methods named after them: `ParseAndValidate`, `ResolveRolesAndAxes`, `ApplyStateWords`, `ApplyFrameVerb`, `ExecuteMotion`, `ApplyFlowWords`, `ResetBlockScope`, `RaiseEvents`; STATIC walks every program section once and follows each `CALL` of a `SUB` section of the file into the subprogram with the caller's state at that point (`TIMES=n` n times in sequence, each pass from the state the previous one left; within `CallDepth`, a deeper `CALL` an ERROR and not entered; a `CALL` of an external program recorded, not followed, and the position becomes unknown, D99); a subprogram nothing calls is walked once from the D99 default entry state; INTERPRETED comes in P4-01).
- `VmOptions` (ExpandCycles, SkipBlocks, BlockCap, CallDepth, ArcTolerance, Unassigned).
- `WordHandlers`: the table from key to handler (code-guidelines 5, table-driven dispatch), one handler class per group folder: `Handlers/FrameHandlers.cs`, `ToolHandlers.cs`, `SpindleHandlers.cs`, `FunctionHandlers.cs`, `CycleHandlers.cs`, `VariableHandlers.cs`, `ResourceHandlers.cs`.
- `ToolChangeRules` exactly as the code-guidelines 2 sample, extended to the full transition table of architecture 5.2 (Empty, Loaded, Pending; `TOOL=0`; `PRELOAD` of the tool in the spindle; the WARNING rows of VM 3.5).
- `FrameRules`: chain append, `RESET` cutting at the last entry of the kind and everything after it, `ORIGIN` emptying the chain and the setpos shifts, `SHIFT` folded into the position (VM 3.4), `SETPOS` per D101, `WORKPIECE` marking positions unknown (D57), `FRAME=MACHINE` for one block (D35).
- `ResourceResolver` (VM 3.8 rules 1 to 5 against `MachineConfig`, the D103 WARNING path against `DefaultMachine`, a work spindle created on the spot getting a rotary axis of its own (D103); `A`, `B`, `C` to the current holder's rotary axis; `SPINDLE_MODE` and `C=` rules; `SPINDLE_SYNC` and `PHASE`).
- `DiameterRules` (D60: `X`, `IX`, absolute `CENTER:X`, the X words of an `AXIS=X` cycle halved on the way in; everything else a radius).
- The coolant default channel `STANDARD` (F29).

Cite: VM 3 (the seven steps), 3.4, 3.5, 3.8, 4 (modal summary); language 4.2, 4.4, 4.5, 4.6, 4.10, 4.11; D31, D35, D55, D57, D60, D101, D103.

Tests first: one test per row of the modal summary (VM 4) named after the row; `ORIGIN=1`, `SHIFT Z=-5`, `TILT B=45` versus `ORIGIN=1`, `TILT B=45`, `SHIFT Z=-5` give different chains; `SHIFT=RESET` after a tilt removes the shift and the tilt after it; one test per row of the tool change table (VM 3.5) and per transition of architecture 5.2; `TOOL:TURRET1=3` then `OFFSET=3` goes to the holder called last; `SPINDLE:MAIN=CW RPM:MAIN=1500` per resource; unknown role with a machine file is an ERROR, without one a WARNING (D103); `HOME C` then `SETPOS C=0` with a test machine whose C axis has `home` records the shift against the machine position; without a reference point the `HOME` WARNING is raised, the shift stays unknown and C is known in the workpiece frame as 0, not an ERROR (D100, D101).

F21 and F29 were fixed in VM 2.2 and 2.5 when D90 to D106 were applied (2026-09-11); the architecture 5.1 flowchart (`TILT_AXIS`, `RETRACT`) is the part of F21 left for this task.

### P1-03 Block execution: motion, arcs, retract, cycles

Files:

- `VirtualMachine/MotionRules.cs`: targets from absolute and incremental words (VM 3.1), `IX` from an unknown position an ERROR, except in a subprogram nothing calls, where the position becomes unknown (D99), `LINE` without feed, motion before `UNITS`, vector words under `TCPM=ON` stored unresolved with the unit-length check (D81), rotary and vector words mixed an ERROR.
- `Geometry/Vec3.cs` (`readonly record struct`, double, the one type with operators), `Geometry/Plane.cs` (XY, ZX, YZ orientation, the polar and the cylinder plane of D102 with the direction convention of VM 3.2), `Geometry/ArcResolver.cs` (`CENTER` form with the tolerance check, `R` form with the center formula of VM 3.2, `ANGLE` form of D84 with full turns and rest, helix by the tool-axis word, start equals end as a full circle only with `CENTER`), `Geometry/Angle.cs`.
- `VirtualMachine/RetractRules.cs` (VM 3.1a: tool axis only, to the limit from `[[axis]] limits` or by the distance, unknown under a tilt, feed or other axis words an ERROR).
- `VirtualMachine/HomeRules.cs` (VM 3 step 5 with D100: reference coordinates from `home` or `home2` with `POINT`, else WARNING and unknown; afterwards known in the MACHINE frame; the `[positions]` table of D100 is not read here, `HOME` is the reference point return and the named positions belong to the expander).
- `VirtualMachine/CycleRules.cs` (VM 3.3: the sequence along `AXIS`, `CLEARANCE` and `DEPTH` required, `CYCLE_F`, `CYCLE_DWELL`, `PECK`, `CYCLE_RETRACT`, the position afterwards; `ExpandCycles` raising the individual MOTION events, D37).
- Arc tolerance from the configuration with the D36 defaults per units.

Cite: VM 3.1, 3.1a, 3.2, 3.3; language 4.3, 4.7; D36, D37, D59, D60, D81, D83, D84, D99, D100, D102.

Tests first: `ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50` from 50.534/69.993 (radius 20 within tolerance); the `R` arcs of `2.5D_FRAESEN` (`X=2 Y=7 R=5` from 7/2) recompute a center that the `CENTER` form reproduces; a negative `R`; `d > 2|R|` is an ERROR; the sweep `ANGLE=737.956 IZ=-5.4` ends at the right point and reports two full turns and a rest of 17.956 degrees; the drilling example of language 6 produces the four positions and the retract plane; `AXIS=X` on a lathe under `DIAMETER=ON`; the hexagon of `POLAR_FACE` closes under D102; `RETRACT` with `F` is an ERROR; `HOME Z` without `home` is a WARNING and unknown, with `home` known in the MACHINE frame.

### P1-04 Validation rules and diagnostics

Files: `src/Ncx.Core/VirtualMachine/Validation/`, one file per family of VM 5 (structure, frame, motion, arc, vector, retract and home, tool, spindle, cycle, flow, expression, resource, channel), each rule a `VM` code in `DiagnosticCodes` with a one-line description; a test writes `docs/spec/generated/diagnostics.md` (code, severity, rule text, section) and fails on a difference. Machine limits (`rpm_min`, `rpm_max`, `max_feed`, axis `limits`) as WARNINGs here; the `limits = "clamp"` rewrite belongs to the expander (P1-06) with the WARNING saying so (D64). Unreachable block after an unconditional `JUMP` (D89), `JUMP=END` inside a subprogram, `RETURN` in a program, `SYNC` in a single-channel run; the "spindle OFF before a `LINE`" rule per F30.

Cite: VM 5 (every item), 3.9, language 4.13 rules; D64, D89, D99, D100, D103.

Tests first: one test per ERROR and per WARNING of VM 5, named after the rule, with the smallest input that triggers it (a two- or three-block file inside the frame); the expected diagnostics of each example as text files `tests/Ncx.Acceptance/Expected/<name>.check.txt` (without a machine file: `PATTERN_LOOP` one WARNING for the unresolved expressions, `INCREMENTAL_SUB` none after D99 and D100 except the `HOME` WARNINGs, `MILLTURN_TRANSFER` the D103 "not checked" WARNINGs for `TURRET1`, `SUB`, `SUB_CHUCK`, `MAIN_CHUCK` and `Z2` and the "spindle OFF before a `LINE`" WARNING at `LINE C=90` (the holder `TURRET1` created on the spot has no spindle, so the default spindle `MAIN` is checked; the `SUB` created on the spot has its own C axis, D103), `POLAR_FACE` only the `HOME C` WARNING (D100), `2.5D_FRAESEN` none), compared as whole files.

Acceptance (D103 reading of the phase table): no ERROR for any of the five examples without a machine file.

### P1-05 Events with Before and After

Files: `src/Ncx.Core/VirtualMachine/Events/`, one record per row of VM 7 as amended by F18: `VmEvent` (Channel, Block, Before, After), `FileEvent`, `ProgramEvent`, `SubEvent`, `SectionEvent`, `ToolEvent` (Begin, End, with distance and block count under the tool), `PreloadEvent`, `MotionEvent` (verb, from, to, center, direction, sweep, tool vector, surface normal, feed, compensation, frame, length), `CycleCallEvent`, `StateChangeEvent`, `VarChangeEvent`, `FlowEvent` (Jump, Call, Return, Repeat), `SyncEvent` (Wait, Release), `DwellEvent`, `StopEvent`, `FunctionEvent` (`FUNC`, `MFUNC`, `COOLANT`), `BlockWriteEvent` (declared here, raised by the compilers in P3-03); `IVmListener` with `On(VmEvent)` in `Ncx.Core` (D106); `VirtualMachine.Subscribe`. The distance under a tool is summed from MOTION lengths per holder.

Cite: VM 7 (every row), 3.5 (TOOL_BEGIN and TOOL_END), architecture 5.3 as amended; D16, D61, D106.

Tests first: a `FakeListener` that records `kind(line): payload` lines; the event sequence of `2.5D_FRAESEN.ncx` as `tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.events.txt`; `TOOL_END` carries the distance and block count; every event carries a `Before` that equals the previous `After`.

Document fix in this task: F18 (VM 7, architecture 5.3 and 9).

### P1-06 Expander and generated blocks

Files: `src/Ncx.Core/Expander/`: `Expander` (`Expand(program, machine, rewriters)` returning a new `NcxProgram` with generated blocks in place, architecture 5.5), `ExpansionRule` as loaded by `Ncx.Config` (`pre`, `post`, `requires`, `restore` on function states, the tool change and catalog cycles; machine-config 5a; the placeholder `{position:NAME}` in a `pre` or `post` block expands to the axis words of the named entry of the machine's `[positions]` table, D100), `GeneratedBlock` origin (the originating block, the rule or plugin, the reason), the `@SAVE` and `@RESTORE` words written through the parser under the D95 option, the `limits = "clamp"` rewrite with its WARNING (D64); `IProgramRewriter` in `Ncx.Core` (D106) with `RewriteResult` (`Unchanged`, `Replace`, `Surround`) and a `RewriteContext` interface; the VM's `RestoreStack` re-applying the saved words as if written (VM 3.10); generated blocks executed in both modes, shown by `trace` and `annotate` with their origin, never written by `format`.

Cite: VM 1, 3.10; language 4.15; machine-config 5a; architecture 5.5; D61, D63, D64, D95, D106.

Tests first: the coolant clutch example (`COOLANT:THROUGH=ON` with `requires = { SPINDLE = "OFF" }`, `restore = ["SPINDLE"]` in a hand-built `MachineConfig`) produces `@SAVE=SPINDLE:MAIN`, `SPINDLE:MAIN=OFF`, the coolant block, `@RESTORE=SPINDLE:MAIN`, and the state after it equals the state without the rule plus the coolant, with `RPM:MAIN` restored; `[tool_change] pre = ["HOME Z"]` inserts `HOME Z` before every `TOOL` block; `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]` with `tool_change = { X = 0, Z = -120 }` inserts `RAPID X=0 Z=-120 FRAME=MACHINE` (D100); a pseudo-word in a user file is an ERROR; a diagnostic on a generated block names the originating line; `format` of the expanded program equals `format` of the original.

### P1-07 `ncx check`, `ncx trace`, `ncx annotate`

Files: `src/Ncx.Cli/Commands/CheckCommand.cs`, `TraceCommand.cs`, `AnnotateCommand.cs`, `src/Ncx.Cli/Pipeline.cs` (parse, expand, run STATIC, collect), the shared options `--machine` (optional per D103, resolved by name in `machines/` or by path from P2-04 on), `--strict`, `--skip-blocks <none|all|1,3>` (D53), `--expand-cycles` (D37), `--format csv|text` for trace. `trace` writes `channel, block, variable, old, new` per changed state variable per executed block (VM 6); `annotate` writes the program with the previous values as line comments (`LINE X=55.44 ; X 33.22 -> 55.44`), which the parser reads as ordinary comments. Exit codes per D97. Architecture 10 updated to the one CLI table (F28).

Cite: VM 6; D16, D37, D53, D97, D103; architecture 10.

Tests first: the three commands run on every example (acceptance); the annotate output, comments stripped, formats back to the original; `check --strict` exits 1 on `PATTERN_LOOP` (its one WARNING) and 0 without `--strict`; trace output of `2.5D_FRAESEN` as an expected file.

Then `docs/reading-the-code.md` (P0-07 tour) is written from `Program.cs` through one `format` and one `check` run.

## Risks and open ends

- The STATIC pass over uncalled subprograms with the D99 default entry state and the suppressed validations is the place where a wrong assumption stays hidden until a compiler needs the state; keep the suppressed list short and name every suppression in the diagnostics table.
- `Snapshot()` on every block for `Before` and `After` costs allocations on the 834-block `3D_FRAESEN` programs and the megabyte corpus files; measure in P1-05 and, if needed, snapshot lazily per changed part. Correctness first; the code-guidelines forbid `Span<T>` in the parts an NC programmer touches, and the VM core is not such a part.
- The arc resolver uses `double`; the tolerance comparison must be against the configured `decimal` tolerance converted once. Never let a `double` reach the position store as anything but a rounded `decimal` with the units' decimals (D62).
- Events and snapshots are the plugin surface (D106); every public property here is an API promise. Keep the records small and name them after the specification.

## Exit checklist

- `phases.md` row 1: the five examples check with no ERROR (no machine file, D103); every VM 5 rule has a named test; the coolant clutch rule expands and restores; `trace` and `annotate` outputs for `2.5D_FRAESEN` committed as expected files.
- `docs/spec/generated/diagnostics.md` committed.
- D99 to D103 and D106 have rows; VM 1, 3, 3.1, 3.2, 3.4, 3.8, 3.9, 3.10, 5, 7 and architecture 5 read as amended.
- `docs/reading-the-code.md` exists and its named files exist (test).
- P1-01 to P1-07 in `done/`; F12 to F21, F26 (the Core part), F29, F30 marked resolved.

## Log

(filled when the phase starts and when it closes)
