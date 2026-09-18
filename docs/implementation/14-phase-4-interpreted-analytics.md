# Phase 4: interpreted virtual machine and analytics

Status: written 2026-09-11; open on 2026-09-18. P4-01 is in `../plan/tasks/done/`; P4-02 and P4-03 are on `main` (857a3b3, dbd8753) and wait for the measured cycle time of `3D_FRAESEN` and the 5-axis pair of the corpus. Milestone M7. Tasks P4-01 to P4-03. Closed when `PATTERN_LOOP.ncx` runs to the end with the expected variable values and the analytics run on `3D_FRAESEN` and on a 5-axis program with their results recorded as the reference (`../plan/phases.md`).

## Entry state

Phase 1 closed (P4-01 needs only the VM and the expression parser); the plan runs the phase after phase 3, as the table does, so that the analytics can read converted corpus programs, but P4-01 can be pulled forward if phase 3 waits for the corpus. At the start of the phase the maintainer is asked for the measured cycle time of `3D_FRAESEN` and for the two large pairs (the 5-axis A/C program of 1.2 MB and the 5.7 MB point list, `sources/README.md`).

Checked 2026-09-18, at the close of phase 1: the phase was pulled forward and ran beside phases 1 and 3. The evaluator of P4-01 (e9a941a) landed on 2026-09-13, before P0-04. The interpreted flow (428a513) followed P1-07 on 2026-09-14. P4-02 (857a3b3) and P4-03 (dbd8753) came the same morning, beside P3-03. Phase 1 closed formally on 2026-09-18. The measured cycle time of `3D_FRAESEN` and the two large pairs have not arrived. P4-02 commits its computed runtime as an unverified reference, and the 5-axis test of P4-03 skips (their logs).

## Decisions needed first

None new. D38 (unassigned variables), D51 (system variables), D53 (`SKIP`), D64 (runtime model), D67 (block ranges) govern the phase. The tool-vector convention of P4-03 without a kinematics module is documented in the report and in this file, not decided as language (D24 keeps kinematics out of NCX).

## Tasks

### P4-01 Expression evaluation and interpreted flow

Files:

- `src/Ncx.Core/Expressions/Evaluator.cs`: `Evaluate(ExprNode, VariableStore, UnassignedVariable, Block, Diagnostics)` returning a `decimal` or a string or UNKNOWN, null after the ERROR, with the one `[variables] unassigned` setting of the run (`VmOptions.Unassigned`, from which the virtual machine also builds the store, D38) and the block whose line the ERROR carries; `decimal` arithmetic; `MOD` keeps the sign of the dividend, `INT` truncates toward zero, `ROUND` rounds half away from zero, `SIN`..`ATAN2` in degrees, `LN`, `EXP`, `SQRT`, `ABS`, `FRAC`, `SGN`, `MIN`, `MAX`; comparisons and `AND`, `OR`, `NOT` yield 1 or 0; division by zero, a string where a number is required and an unassigned variable (unless `unassigned = 0`, D38) are `VM` ERRORs with the block line.
- `VariableStore`: locals `V1`..`V33` pushed per `CALL` and popped by `SUB=END` and `RETURN` (VM 3.6), `ARG` words assigned to the callee's locals, `SYS_` names read through `[system_variables]` with an index expression (D51), assignment to a `SYS_` name an ERROR, the `<file>.vars.toml` seed.
- `VirtualMachine.RunInterpreted`: pc over the running program (the first of the file, or the one the job or `--program` names), labels from the pre-pass, `JUMP` with `IF`, `JUMP=END` (from inside a subprogram a WARNING, VM 3.6), `CALL` of a `SUB` section or of an external program from the working directory (with its own `PROGRAM` frame, `UNITS` and `WORKPLANE` not contradicting the caller), `TIMES`, `REPEAT` from the label to the block, depth limit (default 8), block cap (default 1 000 000, "possible endless loop"), `SKIP` per the run option, `PROGRAM=END` finishing the channel with the run statistics event, state words applied exactly as in STATIC but with expressions resolved.
- Every place where STATIC left a value UNKNOWN (an axis word with an expression, `VAR` from an expression, `IF`) now resolves.

Cite: VM 1, 2.7, 3.6, 3.9; language 4.9, 4.12, 4.13; machine-config 7, 8; D33, D38, D51, D53.

Tests first: the evaluator on every function and operator of 4.12 with the edge cases named (`{-7 MOD 3}` is -1, `{INT(-2.7)}` is -2, `{ROUND(2.5)}` is 3, `{ROUND(-2.5)}` is -3, `{1 / 0}` ERROR, `{$Q9}` unassigned ERROR and 0 under the option); the acceptance:

```
dotnet run --project src/Ncx.Cli -- trace docs/spec/examples/PATTERN_LOOP.ncx --interpreted
```

shows five `CYCLE_CALL` blocks at X = 10, 30, 50, 70, 90 and `Q1`, `Q3` per iteration as the example's note 1 says; `INCREMENTAL_SUB` calls the subprogram four times and ends at the asserted position (X = 0 + 4 * 0 = 0 after the four `IX=30` and `IX=-30` pairs, Y = 60, Z = 50, then `HOME`); a `WHILE`-style loop lowered by the Fanuc reader runs; the block cap triggers on `LABEL=1` `JUMP=1`; depth 9 is an ERROR.

### P4-02 Analytics: tool list and runtime estimate

Files in `src/Ncx.Analytics/`:

- `IAnalytic` (an `IVmListener` with `Report()` returning text, and the block range of D67: `--from`, `--to` as NCX line numbers, per channel file in a job), `AnalyticsRegistry`, `TextTable` (aligned columns, CSV option).
- `ToolListAnalytic`: per `TOOL_BEGIN`/`TOOL_END`: tool, holder, rpm range, feeds, offsets, cutting distance (LINE, ARC, cycle plunges), rapid distance, block count, estimated time, whether the next tool was preloaded before the change (from `PRELOAD` events).
- `RuntimeAnalytic`: per MOTION the trapezoidal profile of VM 8 and D64: commanded feed (per minute; per revolution times rpm; under `CSS` from `VC` and the X radius capped by `RPM_MAX`), limited by `max_feed` of every moving axis, accelerating and decelerating with the smallest `acceleration`, never shorter than `block_time`, `path_mode` exact stop or continuous with `corner_speed`; `RAPID` and `HOME` at `rapid`; cycle plunges and dwells from `ExpandCycles` and `DwellEvent`; spindle starts and stops with `accel_time`; totals per tool, per `SECTION`, per channel; the job time as the longest channel with waits (phase 6). Without `[dynamics]` the estimate falls back to distance over feed and the report says so; the report always says "estimate".
- `ncx analyze <file.ncx> [--machine <name>] [--vars <file>] [--from n --to m] [--analytic tools,runtime,...]` in `Ncx.Cli`, INTERPRETED by default, `--static` for the one-pass form; `--machine` is optional and without it the built-in default machine is used (D103).

Cite: VM 8 (tool list, runtime estimate); architecture 9 (the analytics table); machine-config 4 (`[[axis]]` dynamics), 5 (`accel_time`); D37, D64, D67.

Tests first: the profile on one block (distance, feed, acceleration, expected time by hand); the block-time floor on a 0.01 mm segment; the tool list of `2.5D_FRAESEN` names two tools (1 and the preloaded 0 is not a tool: one tool, tool 1, with its distances; the test asserts the distances computed by hand from the example) and the tool list of `INCREMENTAL_SUB` names tools 2 and 3; the acceptance: the runtime of the converted `3D_FRAESEN` is within 10 percent of the maintainer's measured value once it is known, until then the computed value is committed as the reference with a note that it is unverified.

### P4-03 Analytics: segment length and tool vector change

Files: `SegmentAnalytic` (per MOTION in the workpiece frame with known positions: the euclidean length, the arc length for arcs; histogram with configurable bins, minimum, maximum, mean, the ten shortest blocks with line numbers, the count of skipped blocks with unknown positions and why), `ToolVectorAnalytic` (the tool vector from `TX TY TZ` when present, else from the rotary positions applied to the tool axis of the `WORKPLANE` with the machine's rotary axes as `[[axis]]` lists them: an A/C table or a B head, the convention documented in the report header and in this file until the kinematics module exists; the angle between consecutive vectors; histogram, extremes, the ten largest changes with line numbers), both over `--from`/`--to`.

The tool-vector convention without kinematics: the tool axis is the normal of the `WORKPLANE`; a rotary axis named `A`, `B` or `C` rotates that vector about the machine X, Y or Z axis in the order the `[[axis]]` list gives; a head axis (owner is the tool spindle) rotates the tool, a table axis (owner is a work spindle or table) rotates the workpiece, which is the same angle change with the opposite sign for the relative vector. This is enough for the change between consecutive blocks, which is what the analytic reports; it is not a pose.

Cite: VM 8 (segment length and tool vector change); architecture 9; D24, D67, D81.

Tests first: a three-block program with known lengths and one arc; two `LINE` blocks under `TCPM=ON` with vectors 30 degrees apart; the same with `A` and `C` words on an A/C table; the acceptance: the results on `3D_FRAESEN` (both sources) and on the 5-axis corpus program committed as `tests/Ncx.Acceptance/Expected/analytics/<name>.segments.txt` and `.vectors.txt`, the reference for later versions (M7).

## Risks and open ends

- The runtime estimate will be wrong before it is right; the D100 dynamics values are guesses. The report carries the machine file name and every value it used, so that the maintainer can correct the file instead of the code.
- The 5.7 MB point list is the memory gate for the INTERPRETED loop with snapshots per block; the analytics must stream (aggregate per event, keep only the ten worst), never keep the event list.
- `SYS_` variables that read the VM state (`$SYS_POS_X`) need the position in the workpiece frame; when it is unknown (after `HOME` without a datum table) the read is an ERROR in INTERPRETED mode (VM 2.7), which real Nakamura programs will hit (`#5025` in the transfer). The vars file is the documented way out; say so in the diagnostic.

## Exit checklist

- `phases.md` row 4: `PATTERN_LOOP` runs to the end with the expected values; the analytics run on `3D_FRAESEN` and a 5-axis program and their results are the committed reference.
- `ncx analyze` and its options in the CLI table.
- P4-01 to P4-03 in `done/`.

## Log

(filled when the phase starts and when it closes)
