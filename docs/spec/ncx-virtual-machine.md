# NCX Virtual Machine

Status: Version 1.0 of the specification set, 2026-09-12 (draft 8 in the drafting history of `../decisions/decisions.md`). Companion to `ncx-language.md`: what every word does to the state of a channel, and what the virtual machine reports.

The virtual machine (VM) is the one component that knows what an NCX program means at every block. Readers use it to resolve what the source controller left implicit, the compiler uses it to know what the target controller needs written, and analytics read it. Nothing about the VM is visible in the NCX text; the text carries only what changes.

## 1. Where the VM sits

```
NC file ──► reader (Fanuc, Heidenhain, Siemens) ──► NCX blocks ──► writer ──► program.ncx
             uses a source-side VM to resolve modal G-codes, incremental values,
             preloaded tools, active cycles

program.ncx ──► parser ──► expander ──► VM ──► compiler (machine TOML) ──► NC file for one machine
program.ncx ──► parser ──► expander ──► VM ──► analytics (text output)
program.ncx ──► parser ──► expander ──► VM ──► kinematics module (optional, section 10)
```

The expander applies the expansion rules of the machine configuration and the program rewriters of the plugins: it inserts generated NCX blocks before or after a block (a spindle stop before the through-spindle coolant, a retract before a tool change, a mode M code before a cycle) and may rewrite words (a speed clamped to the spindle limit). Everything it produces is NCX, so the VM executes it like the rest (3.10). `format` does not run the VM at all: it is the parser and the writer (D91).

Two execution modes:

- **STATIC**: one pass top to bottom over every program of the file. The control flow words `JUMP` and `REPEAT` are recorded, not followed; `CALL` is followed: at every `CALL` the subprogram is walked with the caller's state at that point, so a subprogram is walked as often as it is called (D99). A `CALL` with `TIMES=n` is walked n times in sequence, each time from the state the previous pass left, so the caller continues with the state after the last pass (D99). Expressions are not evaluated; a state variable set from an expression becomes UNKNOWN. A subprogram that no program of the file calls is walked once from a default entry state: units, workplane, diameter and feed mode as at the first verb of the file's first program, everything else initial, position unknown (D99). A `CALL` of an external program is not followed in STATIC mode: the call is recorded, the state after it is the state before it, and the position becomes unknown (the external program is checked on its own). This is what `convert`, `compile` and `check` use (D91).
- **INTERPRETED**: the program is executed, variables evaluated, jumps and calls followed, with a block cap against endless loops. This is what `analyze` uses by default.

## 2. State

Everything below exists once per channel unless marked "per resource" or "per job".

### 2.1 Program and frame

| Variable | Initial | Set by |
|---|---|---|
| file.programs, file.subs | pre-pass over the file | `PROGRAM=BEGIN` ... `PROGRAM=END`, `SUB=BEGIN NAME=` ... `SUB=END` (3.6) |
| program.active, name, number | false, empty, 0 | `PROGRAM=BEGIN`, `NAME`, `NUMBER`; `PROGRAM=END` ends execution |
| ended | false | `PROGRAM=END`, also when reached by `JUMP=END` |
| units | UNKNOWN | `UNITS` |
| workplane | from TOML (XY) | `WORKPLANE` |
| origin | from TOML (0) | `ORIGIN` |
| transform chain | empty | `SHIFT`, `ROTATE`, `MIRROR`, `TILT`, `TILT_AXIS` append an entry in program order; their `RESET` forms cut the chain at the last entry of that kind; `ORIGIN` empties it (D31, D82) |
| setpos shift per axis | 0 | `SETPOS` (3.4); cleared by `ORIGIN` |
| cylinder, polar, tcpm | OFF, OFF, OFF | `CYLINDER` (the reference radius while on, D96), `POLAR`, `TCPM`; under `POLAR=ON` or `CYLINDER=n` the VM keeps the position in the polar or cylinder plane of 3.1 (D102), `TCPM` is state only; the Cartesian workpiece geometry belongs to the kinematics module (D54) |
| rotary path, rotary feed | FULL, DEG_MIN | `ROTARY_PATH`, `ROTARY_FEED` (state only, D86) |
| tolerance, rotary tolerance, tolerance mode | OFF, none, FINISH | `TOLERANCE`, `TOLERANCE:ROTARY`, `TOLERANCE_MODE` (state only, written through by the compiler, D85) |
| frame (block) | WORKPIECE | `FRAME=MACHINE` for one block |
| diameter | OFF | `DIAMETER` |
| workpiece holder | `default_workpiece` from TOML | `WORKPIECE` |

### 2.2 Motion

| Variable | Initial | Set by |
|---|---|---|
| verb (block) | none | `RAPID`, `LINE`, `ARC`, `RETRACT`, `HOME`, `CYCLE_CALL`, `SHIFT`, `TILT`, `TILT_AXIS`, `SETPOS` |
| tool vector, surface normal | unknown | `TX TY TZ`, `NX NY NZ` on a `LINE` under `TCPM=ON`; unknown again after any rotary axis word or `TCPM=OFF` (D81) |
| skip (block) | none | `SKIP`; executed or skipped per the run option `skip_blocks` (D53) |
| feed.value, feed.mode | none, PER_MIN | `F`, `FEED_MODE` |
| position per axis | unknown; an axis with `home` in `[[axis]]` starts known in the MACHINE frame at that reference point and unknown in the workpiece frame (D35, D100) | every executed motion |
| position frame per axis | UNKNOWN | motions (3.4) |
| compensation | OFF | `COMP` |

### 2.3 Tool (per holder resource)

| Variable | Initial | Set by |
|---|---|---|
| holder[r].spindleTool | 0 | `TOOL[:r]` |
| holder[r].preloaded | none | `PRELOAD[:r]`; consumed by `TOOL` |
| holder[r].offset.length, offset.radius, offset.combined | 0, 0, 0 | `OFFSET:LEN`, `OFFSET:RAD`, `OFFSET` on the holder called last |
| lastHolder | `default_holder` from TOML | `TOOL:r` |

### 2.4 Spindle (per spindle resource)

| Variable | Initial | Set by |
|---|---|---|
| spindle[r].direction, rpm | OFF, 0 | `SPINDLE[:r]`, `RPM[:r]` |
| spindle[r].mode | SPINDLE | `SPINDLE_MODE:r` |
| spindle[r].orientation | none | `ORIENT[:r]` |
| spindle[r].syncPartner, syncPhase | none, none | `SPINDLE_SYNC`, `PHASE` |
| spindle[r].css, vc, rpmMax | OFF, none, none | `CSS[:r]`, `VC[:r]`, `RPM_MAX[:r]` |

### 2.5 Machine functions

| Variable | Initial | Set by |
|---|---|---|
| coolant[channel] | all OFF | `COOLANT[:channel]`; the channel `STANDARD` is the default channel that a bare `COOLANT` addresses |
| function[name] | from TOML | `FUNC:name` |
| mfunctions | event log | `MFUNC` (an event the compiler passes through) |

### 2.6 Cycle

| Variable | Initial | Set by |
|---|---|---|
| cycle.name, axis, parameters | OFF, tool axis of the workplane, none | `CYCLE` with its parameter words and `AXIS`; a `CYCLE:<controller>=n` word stores the native parameters of the block as unresolved words in source order (D94); `CYCLE=OFF` clears |

A new `CYCLE` replaces all parameters; nothing is inherited from the previous cycle.

### 2.7 Variables and flow

| Variable | Initial | Set by |
|---|---|---|
| vars | empty or `<file>.vars.toml` | `VAR`, `ARG` |
| system variables `SYS_*`, with an optional index | read from the VM state through the configuration's mapping (`$SYS_WEAR_Z[99]` reads register 99 of the wear table, which the VM does not hold: UNKNOWN in STATIC mode, from the `vars` file or an ERROR in INTERPRETED mode); UNKNOWN when the state is unknown | never assigned by the program (ERROR) |
| labels | pre-pass over each program and subprogram | `LABEL`; `END` is reserved for `JUMP=END` |
| pc, callStack, repeatStack | first block, empty, empty | flow words |
| blocksExecuted | 0 | every executed block (INTERPRETED) |

### 2.8 Channel and job

| Variable | Initial | Set by |
|---|---|---|
| channel.id | 1 or `CHANNEL` | header |
| channel.waitingAt, finished | none, false | `SYNC`, `PROGRAM=END` |
| resources (per job) | from TOML | shared by all channels of the job |

### 2.9 Diagnostics

`block.index` (1-based line in the file, plus the file name for called programs) and a diagnostics list of (severity, code, file, line, message), where a diagnostic on a generated block also carries the line of the block it was generated for (`OriginLine`); a diagnostic is rendered as `file(line): ERROR VM042: message`, on a generated block as `file(line, from 12): ...` (D98). ERROR stops the run; WARNING is reported and the run continues; INFO carries notes that are neither (a plugin's inserted blocks) and never changes the outcome or the exit code.

## 3. Block execution

For every block:

1. Parse the words. Unknown key, wrong value type, duplicate key, two verbs, axis words without a verb: ERROR. A key of the form of a machine axis (D93) and, in a block that carries `CYCLE:<controller>=n`, a native parameter (D94) are not unknown keys; they are resolved in step 2 (3.8) or carried unresolved. A key starting with `@` is a pseudo-word (3.10): accepted, with a state key as its value, only in a block the expander generated; in a user file it is the ERROR "pseudo-word in a user file" (D95).
2. Resolve role addresses and axis names against the machine configuration (3.8).
3. Apply the state words, in any order; they do not depend on each other within a block. A `CYCLE:<controller>=n` word sets the cycle and stores the native parameters of the block unresolved, in source order; the VM does not interpret them (D94).
4. If the verb is `SHIFT`, `TILT`, `TILT_AXIS` or `SETPOS`, update the frame.
5. If the verb is a motion verb, resolve the target (3.1 to 3.3) and execute it. `HOME` moves the named axes at rapid to the reference point of the configuration (`POINT=2` selects the second reference point); afterwards those axes are known in the MACHINE frame at the reference coordinates. `HOME` on an axis without a reference point in the configuration is a WARNING, once per run and axis, and the axis becomes unknown in every frame afterwards; a compiler that must write the coordinates reports the ERROR (D100).
6. Reset block-scoped items: `FRAME`, `IF`, `ARG`, `TIMES`, `WITH`.
7. Raise the block's events (section 7).

### 3.1 Linear motion

Target = current position with the given axis words applied: `X=` replaces, `IX=` adds to the current value (ERROR when the current value is unknown). Axes not mentioned keep their value. `LINE` with feed.value none: ERROR. Motion before `UNITS`: ERROR. Inside a subprogram that no program of the file calls these three rules are suppressed and an incremental word leaves the position unknown instead of raising the ERROR (3.9, D99); at a `CALL` the subprogram runs with the caller's position, so the rules apply there as anywhere else. Under `DIAMETER=ON`, `X`, `IX` and the absolute `CENTER:X` are halved before they reach the position store; `CENTER:IX`, `R` and other radial distances are radius values already (D60). While `POLAR=ON`, the working plane for `ARC` and for `COMP` is the face plane whose axes are the X word (a diameter under `DIAMETER=ON`, D60) and the C word as a Cartesian length in the active units; the position is known in that polar frame and unknown in the workpiece frame; `POLAR=OFF` leaves X and C unknown in the workpiece frame until the next motion with known coordinates (D35); `R` and `CENTER` keep their meaning in that plane. `CYLINDER=n` is treated the same way with the cylinder axis (Z on a lathe) in place of X and the C word as a length on the circumference (D102). The kinematics module resolves the Cartesian workpiece position when present.

Vector form (D81): a `LINE` under `TCPM=ON` may carry `TX TY TZ` (tool axis direction at the end point) and optionally `NX NY NZ` (surface normal), both unit vectors in the active workpiece frame, instead of rotary axis words. The VM checks the length (1 within the arc tolerance, else ERROR), stores the vectors as written, marks the rotary axis positions unknown and does not resolve the vectors to axes; the kinematics module does that when present. Rotary words and vector words in one block, or vector words without `TCPM=ON`: ERROR.

#### 3.1a Retract

`RETRACT` moves the tool axis only, away from the workpiece: by the given distance, or bare to the machine limit of that axis. Without an active `TILT` or `TILT_AXIS` the tool axis is the axis perpendicular to the `WORKPLANE`, and the VM moves it (to the axis `limits` from the configuration for the bare form, unknown when there are none). Under a tilt the direction is known only with the kinematics module; without it the moved axes become unknown, which is what the unknown rule after machine-frame moves already does (D35). A `RETRACT` with a feed or with other axis words: ERROR.

### 3.2 Arc

Only in the working plane, which under `POLAR=ON` or `CYLINDER=n` is the polar or cylinder plane of 3.1 (D102); `WORKPLANE=ZX` and `YZ` use the G18/G19 axis orientation for the direction; in the polar plane the direction is seen looking against the tool axis onto the face with X as the first and C as the second plane axis (the `G12.1` and `TRANSMIT` convention), in the cylinder plane looking onto the developed surface with the cylinder axis first and C second (`G7.1`, `TRACYL`), independent of `WORKPLANE`. Start = current position, must be known in the plane. End = start with the axis words applied. A tool-axis word makes a helix.

`CENTER` form: `CENTER:X` absolute or `CENTER:IX` relative to the start point; both plane axes required. The VM validates |start - center| against |end - center| with the tolerance from the machine configuration (default 0.01 mm, 0.0005 in). Start = end is a full circle.

`R` form: signed radius. With d = |end - start|, d > 2|R| plus tolerance is an ERROR. h = sqrt(R² - (d/2)²), M = midpoint, u = (end - start)/d, n = left normal of u. Center = M + s·h·n with s = +1 for (CCW, R > 0) and (CW, R < 0), s = -1 otherwise. Start = end with `R`: ERROR. The VM keeps the computed center, so the compiler can write either form.

`ANGLE` form (D84): `CENTER` required, no plane end-point words, no `R`. The end point in the plane is the start point rotated about the center by `ANGLE` degrees in the direction of the verb; `ANGLE` greater than 360 is more than one turn, and a tool-axis word distributes its travel over the whole sweep (a helix of `ANGLE`/360 turns). The VM stores start, center, sweep and end, so the compiler can write full turns plus a rest for controllers that take at most one turn per block, or the sweep form for controllers that have it (Siemens `TURN=`, Heidenhain `CP IPA`).

### 3.3 Cycle call

Requires cycle.name not OFF and, for the built-in family, known `DEPTH` and `CLEARANCE`. The drilling axis is `AXIS` of the cycle, by default the tool axis of the active `WORKPLANE`; the other axis words of the call block position the hole (on a lathe with `AXIS=X` these are `Z` and `C`). Sequence: rapid in the plane to the axis words at the current drilling-axis position (if any), rapid to `CLEARANCE`, feed to `DEPTH` with `CYCLE_F` (pecking with `PECK`, dwell with `CYCLE_DWELL`, spindle reversal for `TAP`), retract to `CLEARANCE` or `SAFE` per `CYCLE_RETRACT`. Under `DIAMETER=ON` the X values of an `AXIS=X` cycle are halved like every other X. Position afterwards: plane axes at the call point, drilling axis at the retract plane. With the VM option `ExpandCycles` the call is raised as the individual MOTION events (used by analytics). For a `CYCLE:<controller>=n` cycle the VM does not know the sequence (D94): the call positions the plane axes at the axis words of the call block, the drilling axis is unknown afterwards, and `ExpandCycles` raises no MOTION events for it; the `CycleCallEvent` carries the native parameters as words.

### 3.4 Position frames

Initial position: an axis whose `[[axis]]` entry has `home` starts known in the MACHINE frame at that reference point and unknown in the workpiece frame (D35, D100); an axis without `home` starts unknown in every frame. A motion that names some axes leaves the others as they were. A `FRAME=MACHINE` block moves in machine coordinates; afterwards the moved axes are known in the MACHINE frame and unknown in the workpiece frame unless the configuration provides the datum table. A `SHIFT` appended to the chain is folded into the position: newPos = oldPos - shift, expressed in the frame active where the word stands; a `RESET` that removes shifts folds them back. `SETPOS` moves nothing: for every axis word it records a setpos shift such that the current position reads as the declared value (newSetposShift = oldPos - declared); the position store keeps its physical value, the workpiece coordinates are read through the setpos shift, and the next `ORIGIN` clears it (as `G54` cancels a `G50`/`G92` setting). `SETPOS` needs the axis known in some frame: when it is known in the MACHINE frame only (after `HOME` or a machine-frame move), the setpos shift is recorded against the machine position and the axis becomes known in the workpiece frame with the declared value; directly after a `HOME` of that axis that found no reference point in the configuration (D100), `SETPOS` is accepted as well: the axis becomes known in the workpiece frame with the declared value and its machine position stays unknown; the ERROR remains for every other axis unknown in every frame (D101). `POLAR=ON` puts X and C into the polar frame of 3.1, `CYLINDER=n` puts the cylinder axis (the linear axis along the cylinder's own axis, Z on a lathe) and C into the cylinder frame of 3.1, X staying a workpiece coordinate: from the first motion under the transformation their positions are known in that frame and unknown in the workpiece frame; `POLAR=OFF` and `CYLINDER=OFF` leave them unknown in the workpiece frame until the next motion with known coordinates, like a machine-frame move (D35, D102). `TILT`, `TILT_AXIS`, `ROTATE` and `MIRROR` changes mark the position unknown in the new frame unless the kinematics module is present to convert. `MOVE=TURN` or `MOVE=MOVE` on a tilt additionally marks the rotary axes as moved to the plane (their positions are known only from `TILT_AXIS` words, unknown after a spatial `TILT` without the kinematics module); `MOVE=STAY` leaves them. `WORKPIECE` changes mark the position unknown in the new holder's frame until the next `ORIGIN` or motion with known coordinates. The frame of a holder is its own right-handed frame with +Z out of its chuck (D57): the VM stores sub spindle positions in that frame, and readers and compilers apply the machine's mirror or datum convention outside the VM.

### 3.5 Tool change

The two-word model (`PRELOAD`, `TOOL`) is executed against the holder state:

| Word | Effect on holder state | Diagnostics |
|---|---|---|
| `PRELOAD=n` | preloaded = n (0 clears) | WARNING when n is already in the spindle (no-op on the machine) |
| `TOOL=n` | spindleTool = n; if preloaded == n: preloaded = none; raises TOOL_END for the old tool and TOOL_BEGIN for n | WARNING when a different tool was preloaded (the magazine has to cycle twice) |
| `TOOL` | spindleTool = preloaded; preloaded = none | ERROR when nothing is preloaded |
| `TOOL=0` | spindleTool = 0 | WARNING when a cycle is still active or compensation is on |

Readers produce these words from the source (see `controller-mapping.md`): Fanuc `T4 M6` gives `TOOL=4` in one block; `T5` alone gives `PRELOAD=5`; `M6` alone gives `TOOL=n` from the source-side VM. The compiler decides the output form from the machine's `[tool_change]` settings: `T4 M6`, `M6` alone when preloaded, `TOOL CALL 4 Z S1592` (folding `RPM` of the same block and `WORKPLANE` into the call), or `T0404` on a turret. When `auto_preload = true` and the program contains no `PRELOAD` words, the compiler inserts a preload of the next tool after each change using the STATIC pre-pass; a program that has its own `PRELOAD` words is left alone.

### 3.6 Expressions and control flow (INTERPRETED)

- Pre-pass over the file collects the `PROGRAM` sections, the `SUB` sections by `NAME` and the `LABEL`s of every section; duplicates, missing targets, a `LABEL=END`, a `SUB` inside a `PROGRAM` or a block outside every section are ERRORs before execution. The program that runs is the first of the file unless the job manifest or the command line names another.
- Variables start from `<file>.vars.toml` when present. Reading an unassigned variable is an ERROR unless the configuration sets `unassigned = 0`.
- `IF` evaluates to 0 or not 0. `IF` without `JUMP` or `CALL`: ERROR.
- `JUMP` sets pc to a `LABEL` of the current section, forward or backward (a `LABEL` directly after the header makes the program loop, which is how readers render a Fanuc `M99` in the main program); `JUMP=END` sets it to the `PROGRAM=END` of the current program, also from inside a subprogram (WARNING: it ends the program from a call, as a Fanuc `M30` in a subprogram does). `CALL` pushes return pc and the local variables `V1`..`V33`, assigns `ARG` words to the callee's locals, and enters the `SUB` section of the file or loads the external program (searched in the working directory). `SUB=END` and `RETURN` pop. `RETURN` in a program with an empty stack: WARNING, treated as `JUMP=END`.
- `PROGRAM=END` ends execution of the program: the channel is finished, the run statistics are raised. A `CALL` that names a program instead of a subprogram: ERROR.
- `SKIP` blocks are executed unless the run option `skip_blocks` (or `skip_blocks = [1, 3]` for numbered switches) says otherwise. A `LABEL` on a skipped block still counts as a target.
- `$SYS_*` variables are read from the VM state through the configuration's mapping (position, active tool, offsets). A `$SYS_*` name the configuration does not map, or a state that is unknown, evaluates to UNKNOWN in STATIC mode and is an ERROR in INTERPRETED mode.
- `REPEAT` with `TIMES` re-executes the section from the label to the current block; nested repeats and calls up to the configured depth (default 8).
- Block cap from the configuration (default 1 000 000): ERROR "possible endless loop".
- An external program must have its own `PROGRAM` frame and must not contradict the caller's `UNITS` and `WORKPLANE`: ERROR.

### 3.7 Channels and the job scheduler

A job runs one VM per channel program and advances them in rounds: every channel that is neither finished nor waiting executes one block. `SYNC=m` marks the channel waiting at m; when all participants (`WITH`, default all) wait at m, all are released. Marks are matched in execution order, not by uniqueness: a program may wait at the same mark many times (the DMG templates do), and two channels waiting at different marks with the same participants is the deadlock case below. `WAIT_CHANNEL=c` waits for channel c to finish. All channels waiting with no releasable mark: deadlock ERROR naming the marks. `SYNC` in a single-channel job: WARNING, no wait. Resources are per job: two channels commanding the same spindle between two marks is a WARNING.

### 3.8 Resource and axis resolution

1. A role address resolves through `[roles]` to a resource id; unknown role or wrong resource type: ERROR; when no machine file is given the default machine of D103 applies and an unknown role is the WARNING "not checked: no machine file" (below).
2. No role address: `SPINDLE`, `RPM`, `VC`, `CSS`, `RPM_MAX`, `ORIENT` target `default_spindle`; `TOOL`, `PRELOAD` target `default_holder`; `OFFSET`, `OFFSET:*` the holder of the last `TOOL`. More than one resource of a kind without an explicit default in the configuration: ERROR when the machine is loaded.
2a. Every function the compiler writes for a resource comes from that resource's template table in the configuration (`[spindle.MAIN] CW = "M3"`, `[spindle.SUB] CW = "M53"`, Doosan `CW = "M3 P11"` (the file may write `"M03 P11"`; the loader normalizes every M or G code and the compiler writes the normalized form, D105), DMG `[spindle_mode.MAIN] AXIS = "L707({angle})"`); the VM only knows the abstract word. A template table may be bound to a channel (`channel = 2`: the machine accepts the code only from that path, as Nakamura does for the spindle synchronization codes) or to all channels (`channels = "all"`: the code must stand in every path's program behind a wait, as Mori Seiki requires for `M34`/`M35`). The VM ignores this; the job compiler moves or duplicates the words and adds the `SYNC` it needs, and a single-channel compile reports an ERROR for a word bound to another channel (D56).
3. `A`, `B`, `C` resolve to the rotary axis of the current workpiece holder when it has one, otherwise to the machine axis with that NCX name. Explicit machine axis names resolve through the `[[axis]]` list; anything else is an ERROR (D93). When no machine file is given the default machine of D103 applies and an axis it does not have is a WARNING.
4. `SPINDLE_MODE:r=AXIS`: `SPINDLE:r` is an ERROR until back in `SPINDLE` mode; `C=` on that axis is allowed. In `SPINDLE` mode, `C=` on it is an ERROR.
5. `SPINDLE_SYNC=a,b` needs two work spindles in `SPINDLE` mode; `RPM:b` and `SPINDLE:b` while synchronized are WARNINGs. `PHASE` without `SPINDLE_SYNC` in the block is an ERROR.

Without a machine file, `check`, `trace`, `annotate` and `analyze` run against a built-in default machine: one work spindle `MAIN` with the C axis, one tool holder `TOOL` with the tool spindle `TOOL`, axes `X Y Z A B C` without limits and without reference points, coolant channel `STANDARD`, no named functions, the arc tolerance of D36, units default `MM`. A role, function or machine axis the default machine lacks is a WARNING "not checked: no machine file", once per name, and the word runs against a resource created on the spot; a work spindle created this way gets a rotary axis of its own, so `C` resolves to it while it is the workpiece holder (rule 3). With a machine file the rules above apply in full; `convert` and `compile` keep requiring a machine file (D77, D103).

### 3.9 Subprograms and the other programs of the file

A file holds programs and subprograms side by side (language 4.13). In INTERPRETED mode a subprogram is executed only when a `CALL` enters it, with the caller's state, exactly as on the control; the other programs of the file are not executed unless the job runs them on their channels. In STATIC mode a subprogram is walked at every `CALL` with the caller's state at that point, so that `check` validates it as the caller enters it; a subprogram that no program of the file calls is walked once from the default entry state of section 1, and inside it these validations are suppressed because the state they depend on belongs to a caller that does not exist: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, an incremental word from an unknown position (the position becomes unknown, no ERROR), spindle OFF before a `LINE`. Everything structural stays (unknown words, axis words without verb, a missing `SUB=END`), and the compiler emits each `SUB` section once, not once per `CALL`; where the target needs the section per program (Heidenhain, language 4.13) it is once per calling program (D99). It writes the section from an unknown target state, so that every modal word stands at its first use inside it and the text is right for every caller; walks of the section that would write different lines are an ERROR naming the section and the calls (D99). The STATIC walk keeps a call stack like INTERPRETED mode and applies the configured call depth (3.6, default 8): a `CALL` beyond it is the ERROR "call depth exceeded" and the subprogram is not entered again. A `CALL` with `TIMES=n` is walked n times in sequence in STATIC mode, each time from the state the previous pass left, so the caller continues with the state after the last pass. A `CALL` of an external program is not followed in STATIC mode (section 1): the call is recorded and the position becomes unknown. Inside a program, a block after an unconditional `JUMP` that no `LABEL` makes reachable is unreachable: WARNING. This is where the jump-entered sections of a source end up that the control kept below its `M30` (the reader moves them in front of `PROGRAM=END` behind a `JUMP=END`).

### 3.10 Generated blocks

A generated block carries its origin (the block it was generated for, the rule or plugin that made it) and two internal pseudo-words that only the expander may write, `@SAVE=SPINDLE:MAIN` and `@RESTORE=SPINDLE:MAIN`, whose value is a state key of the form `KEY[:ADDR]` naming a state variable of the channel by the key that sets it (`SPINDLE:MAIN`, `COOLANT`, `F`); the lexer accepts a key starting with `@` only under the parser option the expander uses for generated text (D95). `@SAVE` pushes the current value of that variable on a restore stack; `@RESTORE` pops it and re-applies it as if the program had written the word again (a saved `SPINDLE:MAIN=CW RPM:MAIN=1500` comes back as those two words, so the compiler writes `M3 S1500`). This is how a rule that had to stop the spindle gets it running again without knowing the speed. Generated blocks are executed in both modes, count for analytics (a spindle stop and restart costs cycle time and is reported as such), appear in `trace` and `annotate` with their origin, and are never written by `ncx format`. Pseudo-words in a user file are an ERROR (D95).

## 4. Modal summary

| Item | Modal | Reset by |
|---|---|---|
| units, workplane, origin, transform chain, diameter | yes | explicit word only; `ORIGIN` empties the chain |
| setpos shift | yes | `SETPOS` on the same axis, `ORIGIN` |
| verb, `FRAME`, `IF`, `ARG`, `TIMES`, `WITH` | block | end of block |
| feed value and mode, compensation | yes | explicit word; `PROGRAM=END` |
| tool in spindle, preloaded tool, offsets | yes | explicit word; preload consumed by `TOOL`; `PROGRAM=END` keeps the tool in the spindle |
| spindle direction, rpm, vc, mode, sync and phase, css | yes | explicit word; `PROGRAM=END` sets OFF, mode SPINDLE, sync OFF |
| cylinder, polar, tcpm, rotary path and feed, tolerance | yes | explicit word; `PROGRAM=END` sets OFF and the defaults |
| `SKIP`, `PHASE`, `POINT` | block | end of block |
| coolant, named functions | yes | explicit word; `PROGRAM=END` sets coolant OFF |
| cycle | yes | `CYCLE=OFF`, next `CYCLE`, `TOOL` (WARNING), `PROGRAM=END` |
| variables | program lifetime; `V1`..`V33` per call | `SUB=END` and `RETURN` restore locals |
| workpiece holder | yes | explicit word; kept at `PROGRAM=END` |

## 5. Validation

ERROR (run stops): missing or misplaced `FILE=BEGIN`, `NCX`, `FILE=END`, `PROGRAM=BEGIN`, `PROGRAM=END`, `SUB=BEGIN`, `SUB=END`; a block outside every section, a `SUB` inside a `PROGRAM`, a file without a program; `LABEL=END`; `CALL` of a program; unknown key (a machine axis word, D93, and a native parameter of a `CYCLE:<controller>=n` block, D94, are not unknown keys) or value; unknown role, function or machine axis word not declared in `[[axis]]` when a machine file is given (3.8 rule 3, D93; without one, the D103 WARNING); a pseudo-word (`@SAVE`, `@RESTORE`) in a user file (D95); duplicate key; two verbs; axis word without verb; motion before `UNITS`; `LINE` without feed; `ARC` without `CENTER`, `R` or `ANGLE`, inconsistent center, radius too small, full circle with `R`, `ANGLE` with `R` or with plane end-point words or not greater than 0; vector words without `TCPM=ON`, mixed with rotary words, incomplete or not of unit length; `RETRACT` with feed or other axis words; `MOVE` or `ROT` without `TILT`/`TILT_AXIS`; `TOLERANCE:ROTARY` or `TOLERANCE_MODE` without an active `TOLERANCE`; `HOME` without an axis name; `SETPOS` without an axis word or with an axis unknown in every frame, except directly after a `HOME` of that axis (D100, D101); `AXIS` naming an axis that is not a linear axis of the machine; `OFFSET` mixed with `OFFSET:LEN`/`OFFSET:RAD` in one program; `PHASE` or `POINT` without their verb or partner; assignment to a `SYS_` variable; `IX` from an unknown position; `CYCLE_CALL` without cycle, or of a built-in drilling cycle without `DEPTH`/`CLEARANCE` (a catalog or `CYCLE:<controller>=n` cycle carries its own parameters, D94); `COMP` change in an `ARC` block; `TOOL` without preload; duplicate `LABEL`/`SUB`, missing jump or call target, `IF` without partner, `ARG`/`TIMES`/`WITH` without partner; call depth or block cap exceeded; unassigned variable, division by zero, string where a number is required; `C=` on a spindle in `SPINDLE` mode, `SPINDLE` on a spindle in `AXIS` mode; `RAW`, or a `CYCLE:<controller>=n` block with its native parameters, for another controller family at compile time (D5, D94); deadlock at `SYNC`.

WARNING (run continues): `JUMP=END` from inside a subprogram; unreachable block after an unconditional `JUMP`; `ROT` on a target without table kinematics; `TOOL` while a cycle is active or compensation is on; a different tool preloaded than called; preload of the tool already in the spindle; spindle OFF before a `LINE` (the spindle of the current tool holder, the default spindle when the holder has none); `HOME` on an axis without a reference point in the configuration (D100); `F` in a `RAPID` block; `MFUNC` number that the configuration names; `RPM` above the spindle's `rpm_max` or below `rpm_min`, `F` above an axis `max_feed`, a target beyond the axis `limits` (compared in the MACHINE frame and not checked while the machine position is unknown, D100; with `limits = "clamp"` in the configuration the expander rewrites the value and the WARNING says so, D64); `SYNC` in a single-channel job; `RETURN` in the main program; a role, function or machine axis the built-in default machine lacks when no machine file is given: "not checked: no machine file", once per name (D103); unresolved expression in STATIC mode (counted, reported once); two channels on one spindle between marks; `WORKPIECE` change while the old holder's spindle runs; `RAW` present.

Suppressed inside a subprogram that no program of the file calls (its entry state is the default of section 1, D99): `LINE` without feed, motion before `UNITS`, the tool and offset rules, the cycle rules, `IX` from an unknown position (the position becomes unknown) and spindle OFF before a `LINE`. Every structural rule stays. At a `CALL` the subprogram runs with the caller's state and every rule applies.

## 6. Trace and annotate

The VM never stores history in the program. Two derived outputs give it instead: `ncx trace` writes one row per changed state variable per executed block (`channel, block, variable, old, new`, old empty when unknown); `ncx annotate` writes a copy of the program through the canonical writer with the previous values appended to each block's trailing comment (`LINE X=55.44` becomes `LINE X=55.44` followed by the comment `; X 33.22 -> 55.44` in the canonical column, D92; an existing comment is kept and the values follow it after one space), which the parser keeps as the block's comment and the VM ignores. A block of a subprogram that STATIC mode walks at several calls (D99) appears in `trace` once per walk; `annotate` writes the values of its first walk. Both are regenerated at will and read by nothing.

## 7. Events

| Event | Raised when | Payload |
|---|---|---|
| FILE_BEGIN, FILE_END | `FILE=BEGIN`, `FILE=END` | file name, programs and subprograms found |
| PROGRAM_BEGIN, PROGRAM_END | `PROGRAM=BEGIN`, `PROGRAM=END` (also reached by `JUMP=END`) | name, number; run statistics at `PROGRAM_END` |
| SUB_BEGIN, SUB_END | `SUB=BEGIN`, `SUB=END` or `RETURN` | name, caller |
| SECTION | `SECTION` | text |
| TOOL_BEGIN, TOOL_END | tool enters or leaves the spindle | tool, holder, rpm, distance and block count under the tool |
| PRELOAD | `PRELOAD` | tool, holder |
| MOTION | every `RAPID`, `LINE`, `ARC`, `RETRACT` | verb, from, to, center, direction, sweep angle, tool vector and surface normal when given, feed, compensation, frame, length |
| CYCLE_CALL | every call | cycle name and parameters, call point |
| STATE_CHANGE | any modal change | variable, old, new |
| VAR_CHANGE | `VAR`, `ARG` | name, old, new |
| JUMP, CALL, RETURN, REPEAT | flow | target, condition, depth |
| SYNC_WAIT, SYNC_RELEASE | scheduler | mark, channels, round |
| DWELL | `DWELL` | seconds |
| STOP | `STOP` | `PROGRAM` or `OPTIONAL` |
| FUNCTION | every `FUNC`, `MFUNC`, `COOLANT` | the word, the function or coolant channel it addresses |
| BLOCK_WRITE (compiler) | before a block is written | the block's words, mutable, so a plugin can split `Z` onto its own line or strip umlauts from a comment |
| TOOL_POSE (kinematics module) | per MOTION and CYCLE_CALL when the module is present (section 10) | pose of the tool tip relative to the workpiece frame |

Each row is one event record of `Ncx.Core` (architecture 5.3). Every event carries `Before` and `After`, the resolved state of the channel around the block. Plugins (the DLL hooks such as `tool_begin`) subscribe here. A plugin has four places to act, and none of them is the VM state (D61): as a reader rule it sees the source blocks and the source-side state of a reader and decides what an M code or a sequence of source blocks means on this particular machine, for example that `M5`, `M51`, `M3 S1500` is one `COOLANT:THROUGH=ON`, or that a builder M code with a hidden subprogram is the workpiece transfer (D40, D66: the configuration tables come first, the reader rule decides the rest); as a program rewriter in the expander it may change words and insert generated blocks before the VM executes them (clamp a speed, stop the spindle before a function, add a `HOME` before a tool change); as a listener it reads every event; on `BLOCK_WRITE` it edits the output lines of the compiler. Machine limits that are the same for every program (`rpm_max`, `max_feed`, travel limits) do not need a plugin: they are configuration and the expander applies them.

## 8. Analytics

All output is plain text (CSV or aligned columns).

- Tool list: from TOOL_BEGIN/TOOL_END, with rpm, feeds, offsets, distance, block count, preload behaviour (was the next tool preloaded before the change).
- Runtime estimate: per motion a trapezoidal velocity profile from the machine configuration (D64): the commanded feed (per minute, or per revolution times rpm; under `CSS` the rpm follows from `VC` and the current X radius, capped by `RPM_MAX`) is limited by the `max_feed` of every axis that moves, the block accelerates and decelerates with the smallest `acceleration` of the moving axes, and a block can never be shorter than the control's `block_time`, which is what makes thousands of short segments in 3D and 5-axis programs slow. `path_mode = "exact_stop"` brakes to zero in every block, `"continuous"` carries the speed through corners up to the configured `corner_speed`. `RAPID` and `HOME` use the `rapid` rate with the same profile; cycle plunges and dwells add their times; a spindle start or stop adds `accel_time` of that spindle; totals per tool, per section, per channel; job time as the longest channel including waits at marks. Without dynamics in the configuration the estimate falls back to distance over feed and says so.
- Travel limits: min and max per axis in the MACHINE frame against the `limits` of `[[axis]]`, which are machine coordinates (D100); a position known only in the workpiece frame is not checked and the report says so; blocks that exceed them.
- Datum, feed and speed lists: from STATE_CHANGE.
- Segment length and tool vector change: for every MOTION, the euclidean length (arc length for arcs) and the angle between the tool vectors before and after (rotary axes A, B, C applied to the tool axis). Specialist checks for CAM output quality.
- Every analytic accepts a block range (`--from 380 --to 600`, NCX line numbers of the file; for a job per channel file), so that a segment analysis can look at one operation instead of the whole program (D67).
- Loop statistics (INTERPRETED): executed blocks per label section, iteration counts, final variable values.
- Channel timeline: estimated time per block aligned at every mark, waiting time per channel per mark.

## 9. Depth of the VM

| Level | Content | Where |
|---|---|---|
| 0 | Program state per channel: frame, motion, feed, tool, cycle, variables, flow (sections 2 and 3) | VM, 1.0 |
| 1 | Named resources with their own state, roles, conflicts between channels (2.3, 2.4, 3.8) | VM, 1.0 |
| 2 | Kinematics: tool tip pose in the workpiece frame from the machine tree, 5-axis transformations, runtime of rotary moves, collision checks | separate module, later |

## 10. Kinematics module contract

The module subscribes to MOTION and CYCLE_CALL, reads the `[[node]]` tree from the machine configuration and the resource state from `Before`/`After`, and computes the pose of the tool tip relative to the current workpiece frame. It publishes `TOOL_POSE` events for analytics. It never writes VM state, never adds or changes an NCX word, and the VM runs completely without it. In the postprocessor picture: the CAM system gives the postprocessor a Cartesian path and a tool vector and the postprocessor computes axis values; here the VM gives axis values and the module computes the Cartesian pose.

## 11. Open decisions

All items of this section (D34 to D42) were settled on 2026-09-11 with the recommendations, see the log in `../decisions/decisions.md` and the answers in `../decisions/rationale.md`: complete header from writers, simple unknown rule after machine-frame moves, arc tolerance from the configuration (0.01 mm, 0.0005 in), `ExpandCycles`, unassigned variables an ERROR by default, round-based scheduler, transfer detection through the configuration tables with reader rules from plugins for the rest, resource types limited to spindles, holders, tables and axes, preload mismatch a WARNING (the user may have preselected a special pocket on purpose, so never an ERROR). D91, D93, D94, D95, D96, D98 and D99 to D103 of the fourth round were settled on 2026-09-11, D99 to D103 clarified on 2026-09-12 and 2026-09-13, and are applied in sections 1, 2.1, 2.2, 2.5, 2.6, 2.9, 3, 3.1, 3.2, 3.3, 3.4, 3.8, 3.9, 3.10, 5, 6 and 8.
