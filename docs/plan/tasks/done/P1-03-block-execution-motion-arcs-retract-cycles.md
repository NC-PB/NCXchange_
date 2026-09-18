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

Claude (agent), 2026-09-13, part one of the task (P1-03a, branch `p1-03a-geometry`): the geometry of the phase file, pure and without the virtual machine, which does not exist yet. `src/Ncx.Core/Geometry/`, one type per file: `Vec3`, `Plane`, `Angle`, `ArcResolver`, and beside them `Arc` (architecture 4.2 names it: the resolved arc), `ArcResult`, `ArcError`, `ArcDirection`. Tests in `tests/Ncx.Core.Tests/Geometry/`. Decisions implemented: D36 (the arc tolerance as a parameter of the `CENTER` check and the `R` check), D62 (double only inside the geometry, no math package), D84 (the `ANGLE` form with full turns and rest), D102 (the polar and the cylinder plane with the fixed direction convention); D60 only as a reading (the X word reaches the polar plane halved; the halving is the VM's, `DiameterRules` of P1-02). The specification is unchanged; everything below is a reading of it.

- Plane coordinates: the resolver works on `Vec3` with X along the first plane axis, Y along the second and Z along the tool axis; `Plane` names the three axes, and in every plane `ARC=CCW` is the turn from the first plane axis toward the second (language 4.3, VM 3.2). `XY` is X, Y, tool Z; `ZX` is Z, X, tool Y and `YZ` is Y, Z, tool X (G18, G19: X, Y, Z in cyclic order, so every plane is right-handed); `POLAR` is X, C, tool Z (the face is seen along the spindle axis); `CYLINDER` is Z, C, tool X (X stays a workpiece coordinate, VM 3.4). Geometry defines no `Workplane` enum: that closed set belongs to the VM state of P1-01, and the VM maps it to `Plane.XY`, `ZX`, `YZ`.
- API, parameters in the canonical word order (verb, end point, `CENTER`, `R` or `ANGLE`): `ResolveCenterForm(direction, start, end, center, arcTolerance)`, `ResolveRadiusForm(direction, start, end, radius, arcTolerance)`, `ResolveAngleForm(direction, start, toolAxisEnd, center, angle)`. Points come in as `Vec3.FromDecimals(...)`, the model's decimals converted once; `R`, `ANGLE` and the tolerance come in as decimals and are converted once inside. `CENTER:IX` is added to the start by the caller. A problem is an `ArcResult` with an `ArcError`, never a diagnostic and never an exception: `InconsistentCenter`, `RadiusTooSmall`, `FullCircleWithRadius`, `RadiusZero` (language 4.3, "not 0") and `AngleNotGreaterThanZero`, the geometric ones among the arc ERRORs of VM 5. The word-level ones (`CENTER`, `R` or `ANGLE` present, both plane axes of `CENTER`, `ANGLE` with `R` or with plane end-point words) stay with the VM, which maps every kind to a code of VM200-VM399.
- `CENTER` form: an error when the start and end radii differ by more than the tolerance; start = end (equal plane coordinates) is a full circle, and with a tool-axis word a full turn of a helix. `R` form: the formula of VM 3.2 as written, d > 2|R| plus the tolerance an error, start = end an error. For 2|R| < d ≤ 2|R| plus the tolerance nothing is left under the root: h is 0 and the center is the midpoint, the limit of the formula. `ANGLE` form: the end is the start rotated by the rest alone, because full turns come back to the start; its Z is the tool-axis end the caller gives. The center is a point of the plane and carries the start's Z. `Arc.Sweep` is in degrees in the direction of the verb; `FullTurns` is floor(sweep / 360) and `Rest` is sweep minus the full turns.
- A double goes back to a position only through `Vec3.RoundToDecimal(coordinate, decimals)`, rounding half away from zero, the one rounding NCX defines (language 4.12, `ROUND`).
- The geometry types are `internal` (visible to `Ncx.Core.Tests`); the analytics task that needs them in `Ncx.Analytics` (P4-03) makes public what it uses.
- Open questions, marked `TODO(question)` in the files: how many decimals "the units' decimals" of the phase file's risks are (no document says; the caller passes them, `Vec3.cs`); whether start = end is compared within the arc tolerance, and what an arc of radius 0 (a center on the start) is (both resolve to what their coordinates give, `ArcResolver.cs`); the cylinder axis on a machine other than a lathe (the documents name Z on a lathe only, `Plane.cs`).

Done when:

- Arc tests with the numbers of the language examples (`ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50` from 50.534/69.993) and an `R` arc whose center is recomputed to the known value: holds as geometry. `ArcCenterFormTests` covers radius 20.00013 against 20 within 0.01 mm. `ArcRadiusFormTests` covers the four corner arcs of `2.5D_FRAESEN` at 7/7, 7/93, 93/93, 93/7, reproduced by the `CENTER` form; H36 recomputed within 0.00013 of 50/50; H33 tangent to the pocket circle; a negative `R`; d > 2|R|. Through blocks and the VM it waits for part two.
- A sweep of 737.956 degrees with `IZ=-5.4` ends at the right point and reports two full turns plus a rest: holds as geometry (`ArcAngleFormTests`: from 70/50 around 50/50 to 69.026/56.166 at Z -5.4, 2 turns, rest 17.956); through the VM, part two.
- The drilling example of the language produces the four positions and the retract plane: waits for part two (`CycleRules`), on the VM of P1-02.
- The hexagon of `POLAR_FACE` checks without ERROR and closes: the geometry holds (`PolarFaceHexagonTests` reads the blocks between `POLAR=ON` and `POLAR=OFF` from the example and halves X by hand: both arcs turn around X=27 C=0, tangent to the flat X=15, the six corners lie at radius 17.32 within the tolerance, and the contour closes); "checks without ERROR" waits for part two and `ncx check` (P1-04, P1-07).

What remains for part two: `VirtualMachine/MotionRules.cs` (targets from absolute and incremental words, `IX` from an unknown position, D99, `LINE` without feed, motion before `UNITS`, the D81 vector words; the plane chosen from `WORKPLANE`, `POLAR`, `CYLINDER`; the start known in the plane; `CENTER:IX` added to the start; the D60 halving; the `ArcError` kinds mapped to VM codes; the arc tolerance from the configuration with the D36 defaults per units; the resolved end rounded into the position store), `RetractRules.cs`, `HomeRules.cs`, `CycleRules.cs`, and the rest of the scope above.

Gate: `dotnet build -warnaserror` with 0 warnings, 143 tests passing (132 in `Ncx.Core.Tests`, 64 of them new), `dotnet format --verify-no-changes` clean. A mutation check flipped the side s of the center formula: the geometry tests fail, and they pass again once the file is restored.

Claude (agent), 2026-09-13, part two of the task (P1-03b, branch `p1-03b-motion`): the rest of section P1-03 of the phase file, on the virtual machine of P1-02 (which took `HomeRules`) and the geometry of part one. Decisions implemented: D35, D36, D37, D59, D60, D81, D83, D84, D94, D99, D100, D101, D102. The specification is unchanged; everything below is a reading of it.

- Files in `src/Ncx.Core/VirtualMachine/`:
  - `MotionRules` (RAPID and LINE; the target of every axis word, used by the other rules as well; UNITS before the first motion, LINE without feed, one form per axis of language 4.3).
  - `ArcRules` with `ArcWords` (the words of an ARC block by their part in the arc) and `PlaneArc` (the resolved arc with its plane).
  - `ToolVectorRules` (D81), `RetractRules` (VM 3.1a), `CycleRules` with `CycleMotion` (VM 3.3, D37).
  - `ExecuteMotion` hands each motion verb to its rules. `VirtualMachine.LastArc` and `LastCycleMotions` keep the resolved arc and the motions of an expanded cycle of the last block for the MOTION events of step 7 (P1-05).
  - Codes VM200-VM252 in `Model/DiagnosticCodes.Motion.cs`.
- The frame an axis word programs in (VM 3.1, 3.4, D35, D102): FRAME=MACHINE gives the MACHINE frame; under POLAR=ON the X word and the C word give the polar frame, under CYLINDER=n the Z word and the C word the cylinder frame; every other word is a coordinate of the workpiece frame and goes into the store through the setpos shift (`FrameRules.WorkpiecePosition`). IX adds to the value in that frame. A word from an expression leaves its axis unknown.
- Motion before UNITS covers HOME as well: language 2 rule 2 names HOME among the verbs of the blocks that move, and VM 3 step 5 executes it as a motion verb. The P1-02 tests that ran HOME or RAPID without UNITS got a `UNITS=MM` line.
- D99: motion before UNITS, LINE without feed, IX from an unknown position and the cycle rules report to `BlockContext.CallerRuleDiagnostics`, which a subprogram nothing calls never reads; there IX leaves the axis unknown.
- ARC:
  - The plane comes from POLAR, CYLINDER and WORKPLANE (`Geometry.Plane`); the start must be known in the plane's frame. A plane axis the block does not name is found through `ResourceResolver.KeyOfAxis`, which resolves quietly: no diagnostic, no axis created on the spot.
  - CENTER:IX is added to the start and not halved; X, IX and the absolute CENTER:X are halved under DIAMETER=ON (`DiameterRules`).
  - The end of the CENTER and R forms comes from the words as decimals; the end of the ANGLE form is a double rounded to 3 decimals in millimetres and 4 in inches (wave-1 question #46).
  - A value from an expression leaves the arc unresolved, with the end where the words give it. The geometric errors map `ArcError` to VM230-VM234.
- The arc tolerance is `VmOptions.ArcTolerance`, or the D36 default per units (wave-1 questions #54 and #91); the unit length of the vector words is checked with it.
- The vector words: TCPM=ON, no rotary words, TX TY TZ complete, NX NY NZ complete and only with them, unit length. They are stored as written, and the rotary axes are unknown afterwards. Any rotary axis word of a motion block forgets both vectors (VM 2.2).
- RETRACT: the tool axis of WORKPLANE. Bare to the upper limit of `[[axis]]`, known in the MACHINE frame (the limits are machine coordinates, D100; away from the workpiece is +, D57), unknown without limits. By a distance in the frame the axis is known in. Under a TILT or TILT_AXIS X, Y and Z are unknown. F is VM240; axis words under RETRACT are the parser's PAR error already (language 5 rule 2), which a test shows.
- CYCLE_CALL:
  - A built-in name (`DrillingFamily.Names`) runs the sequence. The drilling axis is resolved like an axis word, and a rotary one is VM252. Under AXIS=X and DIAMETER=ON the planes are halved at the call, where they reach the position store.
  - The retract goes to CLEARANCE, or to SAFE under CYCLE_RETRACT=SAFE; the plane axes end at the call point.
  - Under ExpandCycles the motions are the rapid to the hole, the rapid to CLEARANCE, the pecks, the feed to DEPTH and the retract.
  - A native cycle and a catalog cycle position the axes of the call block and leave the drilling axis unknown, with no motions.
- The two blocking findings of P1-02's second review, checked against main:
  - Finding 1 (a change of the workpiece frame, or SETPOS or SHIFT from an expression, throws away the machine position of an axis whose setpos shift was recorded against it; a later SETPOS raises a false VM050): P1-02's second round fixed it only for a change of the frame that directly follows the SETPOS, by returning the axis to the MACHINE frame before anything is marked unknown. Once motions came in it still held, as the second review of this task found. The return to the MACHINE frame dropped the record while the setpos shift stayed, and it dropped the record of an axis already in the MACHINE frame after HOME as well. So a change of the frame before a motion (ROTATE and its reset, WORKPIECE=SUB and back, TILT and its reset) left the axis known in the workpiece frame through the shift, without the record. The next change of the frame then made it unknown in every frame, so the next SETPOS raised a false VM050, and ORIGIN left it known in the workpiece frame at its machine value. The first version of this entry said the finding no longer held; that was wrong. The finding is fixed in the review round below, and `SetposMotionTests` covers the sequences with the detour and without it.
  - Finding 2 (with a SHIFT in the chain before the SETPOS the axis returns to the MACHINE frame at a wrong value): for a known SHIFT the record keeps the chain's shifts at the SETPOS, and the machine position comes out right. The first version of this entry said so for "every sequence tried"; that was wrong for a SHIFT from an expression, whose chain entry keeps 0. After its reset and a motion, ORIGIN or a change of the frame returned the axis to a machine position off by the unknown shift, known and without a diagnostic, as the third review of this task found; the third review round below fixes it. The point the fix kept, SHIFT=RESET folding such a shift back so that X reads 105 where it read 100, is settled by the text: VM 3.4 folds back "a RESET that removes shifts" for every position known outside the MACHINE frame, and after SETPOS against the machine position the axis is known in the workpiece frame (D101); a control that cancels G52 after G92 does the same. No code change and no question. `SetposMotionTests` pins it with a motion and ORIGIN afterwards (MACHINE 295).
  - The seam P1-02 left (HOME leaves the record of an axis alone, and P1-03's motions can make such an axis known in the workpiece frame again): the first version of this entry kept the record "only where P1-02 drops it", a change of the frame among those places. That was wrong, for the reason given under finding 1, and it also said nothing about a motion in a turned frame. The review round below settles it. The record stands as long as its setpos shift does. HOME, a change of the frame, POLAR=OFF and CYLINDER=OFF leave it alone. ORIGIN, the external CALL and every SETPOS whose new shift is not recorded against the machine position take it out. A motion keeps the machine position known through the record only when it moves the machine by as much as the workpiece coordinate; the third review round below says which motions those are (`FrameRules.FollowMotion`).
- Shared files changed:
  - `VirtualMachine.cs`: the step `ExecuteMotion`, and `LastArc` and `LastCycleMotions`.
  - `ResourceResolver.cs`: `KeyOfAxis`.
  - The READMEs of `src/Ncx.Core/VirtualMachine/` and `tests/Ncx.Core.Tests/VirtualMachine/`.
  - The P1-02 tests: `UNITS=MM` before their motions in `HomeTests`, `SetposTests`, `ResourceResolutionTests`, `ModalSummaryTests` and `StaticRunTests`. In `ExampleRunTests` C of POLAR_FACE ends at 240, the last cross hole; in `ResourceResolutionTests` the created axis Z2 now moves to -5.
- Open questions, marked `TODO(question)` in the files:
  - IX adds to the value in the frame of the block, and a value known only in another frame (after HOME) is the ERROR (`MotionRules`).
  - Only LINE is checked for a feed, not ARC (`MotionRules`).
  - Vector words under RAPID, ARC and CYCLE_CALL are checked and stored as on a LINE (`ToolVectorRules`).
  - ARC with CENTER and R is an ERROR (`ArcRules`).
  - CENTER on an axis outside the working plane is an ERROR (`ArcRules`).
  - An ARC from an unknown start is suppressed in a subprogram nothing calls, like IX (`ArcRules`).
  - A catalog cycle is called like the native form (`CycleRules`).
  - DEPTH or CLEARANCE from an expression is no ERROR (`CycleRules`).
  - CYCLE_RETRACT=SAFE without SAFE leaves the drilling axis unknown (`CycleRules`).
  - A drilling-axis word in the call block moves with the rapid to the hole (`CycleRules`).
  - The motions of pecking, chip breaking, tapping and the retract under ExpandCycles (`CycleRules`).

Done when:

- Arc tests with the numbers of the language examples and an `R` arc whose center is recomputed to the known value: holds through the VM (`ArcTests`). The CENTER example from 50.534/69.993 resolves at radius 20.00013. The corner arc `X=2 Y=7 R=5` from 7/2 recomputes 7/7, which the CENTER form reproduces. H36 recomputes 50/50. A negative R turns around 2/2 by 270 degrees, and d > 2|R| is VM231.
- A sweep of 737.956 degrees with `IZ=-5.4`: holds through the VM (`ArcTests`). It ends at X 69.026, Y 56.166, Z -5.4, with 2 full turns and a rest of 17.956 degrees.
- The drilling example of the language: holds (`CycleCallTests`). Each call is four motions, to the hole, to CLEARANCE 5, to DEPTH -21.732 at 565 and back to 5, and afterwards Z stands at the retract plane with X and Y at the call point.
- The hexagon of `POLAR_FACE`: holds for the STATIC run that `check` runs. `PolarCylinderTests` walks the example through the VM: no ERROR, both arcs turn around X=27 C=0 in the polar plane, the six corners lie at radius 17.32, and the contour closes. `ExampleRunTests` runs the whole file without a machine file with the HOME C WARNING only. The `ncx check` command (P1-07) and the validations of P1-04 come on top.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2373 tests passing, 1434 of them in `Ncx.Core.Tests`, 105 of those new.
- `dotnet format --verify-no-changes`: clean.
- A mutation check changed four rules at once: the retract plane of the cycle, the rounding of the ANGLE end, the unit-length check, and the upper limit of RETRACT. Ten tests failed, each mutation among them, and the files were restored afterwards.

Claude (agent), 2026-09-13, review fixes of part two (the second review of P1-03b found two blocking findings; branch `p1-03b-motion`). Decisions implemented: D35, D54, D57, D101, D102. The specification is unchanged; everything below is a reading of it.

- Finding 1, the first motion under POLAR=ON or CYLINDER=n. VM 3.1, 3.4 and D102 say that from the first motion under the transformation the positions of its axes are known in its frame and unknown in the workpiece frame.
  - `MotionRules.EnterTransformation` runs before the verb of every motion block (`ExecuteMotion`). The axes of the transformation are X and C under POLAR, and the cylinder axis Z and C under CYLINDER. Such an axis becomes unknown when the block does not name it and it is not known in the transformation's frame yet, an axis known in the MACHINE frame only included.
  - The reading behind it: VM 2.2 gives an axis one position frame, and converting a workpiece or machine coordinate into the face plane, or onto the developed surface, is the kinematics module's job (VM 3.1, D54). The setpos record stays.
  - After POLAR=OFF or CYLINDER=OFF, an incremental word on such an axis is VM202. `PolarCylinderTests` covers a first motion under POLAR that names only C, only X, or neither, and under CYLINDER one that names only Z, only C, or only X. An axis already in the polar frame keeps its value.
- Finding 2, the record of a setpos shift taken against the machine position:
  - `FrameState.SetposAgainstMachine` holds a `SetposRecord` per axis with three parts: the chain's shifts on the axis at the SETPOS, as before; the workpiece holder at the SETPOS; and whether the machine position is known through the record.
  - The record stands as long as its setpos shift does (VM 3.4: only ORIGIN clears a setpos shift). ORIGIN takes it out. So do the external CALL and every SETPOS whose new shift is not recorded against the machine position: one from an expression, one directly after a HOME without a reference point, and one on a position whose machine position is not known. A change of the frame, HOME, POLAR=OFF and CYLINDER=OFF no longer drop it.
  - `FrameRules.FollowMotion` runs after every motion block that moves in the workpiece frame, that is, every one except HOME and a FRAME=MACHINE block. This round let the machine position of every axis with a record stay known only when the frame related to the machine frame as the frame of the SETPOS did, through known shifts alone: the holder of the SETPOS, no ROTATE, MIRROR, TILT or TILT_AXIS in the chain, and no SHIFT from an expression on the axis. The third review round below replaces that rule: it looks only at the axes the block can have moved, lets a ROTATE, a MIRROR or a SHIFT from an expression leave an axis alone where they do, and allows the default workpiece holder only.
  - So a reset after a motion in a turned frame leaves the axis unknown in every frame instead of returning it to a wrong machine value, and ORIGIN leaves it unknown as well. This entry also said "the rule covers every axis with a record, whether the block names it or not"; that was wrong, as the third review found: a motion that cannot move the axis in the machine frame leaves it as it was (VM 3.4).
  - The chain entry of a SHIFT from an expression now names its unknown axes (`TransformEntry.UnknownShift`); `Shift` keeps the 0 of wave-1 question #100. Removing such an entry leaves those axes unknown outside the MACHINE frame, as appending it did (`FrameRules.CutAt`). Before, the workpiece coordinate read as if the unknown shift had been 0, and the machine position would have followed from it.
  - `SetposMotionTests` has the probes of the review: the detours before the motion, each followed by a change of the frame and a SETPOS (no VM050) or by ORIGIN (MACHINE 250, unknown in the workpiece frame). It also covers a motion under a turned frame, under another holder, and under a SHIFT from an expression; a SETPOS under a rotation without a motion; and a first motion under POLAR that does not name X.
- The claims of part two about finding 1 and the seam are corrected in place above.
- Shared files changed:
  - `State/FrameState.cs` and `State/FrameSnapshot.cs`: the value type of `SetposAgainstMachine`.
  - `State/TransformEntry.cs`: `UnknownShift`.
  - The new `State/SetposRecord.cs`, and `State/README.md`.
  - `VirtualMachine.cs`: `ApplyShift` hands its unknown axes to `AppendShift`, and `ExecuteMotion` calls `EnterTransformation` and `FollowMotion`.
  - A comment in `Handlers/FrameHandlers.cs`.
  - P1-02's tests: in `SetposTests` the record now stays after a change of the frame and is read through `ChainShift`, and `FrameChainTests` has one new test.
- No new open question.

Done when: unchanged; all four criteria hold, as recorded for part two.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2405 tests passing, 1466 of them in `Ncx.Core.Tests`, 32 of those new.
- `dotnet format --verify-no-changes`: clean.
- Mutation checks, one at a time, with the files restored afterwards:
  - `EnterTransformation` doing nothing fails the 10 transformation tests.
  - `FollowMotion` doing nothing fails 9 tests: the motions under a turned frame, under another holder, and under a SHIFT from an expression.
  - `ReturnToMachineFrame` dropping the record, as on main, fails 21 tests, every detour probe of the review among them.

Claude (agent), 2026-09-13, fixes of the third review of part two (three blocking findings; branch `p1-03b-motion`). Decisions implemented: D31, D35, D57, D101. The specification is unchanged; everything below is a reading of it.

- Finding 1, a SHIFT from an expression that stood before the SETPOS. SETPOS declares the position in the active workpiece frame (language 4.2), and that frame holds the unknown shift, whose chain entry keeps 0 (wave-1 question #100).
  - The record now keeps those entries (`SetposRecord.UnknownShifts`). A motion keeps the machine position known only while the chain holds exactly them. The unknown shift is then in the frame of the SETPOS and of the motion alike, and it cancels out of the machine position. Once one of them is cut, or another one stands, a motion leaves the machine position unknown (VM 1: a value from an expression is unknown). ORIGIN and a change of the frame then leave the axis unknown instead of at a wrong machine value.
  - The review suggested recording the machine position as unknown from the SETPOS on. That is not done. Nothing moves at the SETPOS, so the machine position is known there, and `HOME X`, `SHIFT X={$Q1}`, `SETPOS X=100`, `SHIFT=RESET`, `SETPOS X=0` would have raised a false VM050, the failure class of finding 1 of P1-02's review. With the unknown shift still in the chain, `LINE X=50` and `ORIGIN=1` return X to 250, where it stands. The review's reproducer holds either way: X is unknown after ORIGIN and after TILT.
- Finding 2, the sub spindle. VM 3.4 gives each holder its own right-handed frame with +Z out of its chuck, and readers and compilers apply the machine's mirror or datum convention outside the VM (D57).
  - A motion moves the machine by as much as the workpiece coordinate only under the machine's default workpiece holder (`default_workpiece`), and only for a SETPOS taken under it. After a motion of an axis under any other holder, the machine position of that axis is unknown, also when that holder took the SETPOS.
  - The doc comments of `SetposRecord.Holder` and `FollowMotion` no longer cite D57 for a relation it does not give. This needs no question: D101's formula (newSetposShift = machinePos minus declared) relates the default holder's frame to the machine frame, and VM 3.4 leaves every other holder's frame to the convention outside the VM.
- Finding 3, the axes a motion can move. VM 3.4: "A motion that names some axes leaves the others as they were".
  - `FollowMotion` now gets the position store as it was before the block; `ExecuteMotion` copies it while a record stands. It looks only at an axis whose stored position the block changed, and at one the frame couples with such an axis.
  - Coupled: under a ROTATE, the other axis of the plane where the ROTATE stood, when one of its axes moves; under a TILT or TILT_AXIS, every linear axis, when a linear axis moves. A MIRROR and a SHIFT couple nothing, and a rotary axis moves no linear axis. (The residual review below narrows the tilt to X, Y and Z, and applies the coupling to an axis known in the MACHINE frame as well.)
  - For an axis the block changed, the machine position stays known when no ROTATE turns a plane it is in, no MIRROR names it and no TILT or TILT_AXIS stands. For a coupled axis it is unknown. (The residual review below adds the same condition for the frame of the SETPOS.)
  - The stored position is compared, not the words of the block: RETRACT names no axis and moves the tool axis, a cycle moves its drilling axis, and an axis the block leaves where it stood has not moved in the machine frame either.
  - The ROTATE entry now keeps the working plane where it stood (`TransformEntry.Workplane`). Every chain entry applies to the frame active where it stands (language 4.2, D31), and a WORKPLANE in the same block counts in either order (VM 3 step 3).
  - TCPM stays state only (VM 2.1, D54): `FollowMotion` treats it as no coupling, as the path of an axis known in the MACHINE frame does.
- `SetposMotionTests` has the reproducers of the three findings, the two sequences the review's suggestion for finding 1 would have made wrong, and the couplings under TILT and under a ROTATE in ZX.
- The claims about finding 2 of P1-02's review, the seam and `FollowMotion` are corrected in place above.
- Shared files changed:
  - `State/SetposRecord.cs`: `UnknownShifts`, and the doc comments.
  - `State/TransformEntry.cs`: `Workplane`.
  - `State/FrameState.cs` and `State/FrameSnapshot.cs`: the doc comments of `SetposAgainstMachine`.
  - `Handlers/FrameHandlers.cs`: ROTATE records its plane.
  - `VirtualMachine.cs`: `ExecuteMotion` hands the store before the block to `FollowMotion`.
- Open question, marked `TODO(question)` in `FrameRules`: under a TILT or TILT_AXIS, does a rotary axis word move the axis in the machine frame by as much as in the tilted frame? Until it is answered, a motion of a rotary axis under a tilt leaves its machine position unknown.

Done when: unchanged; all four criteria hold, as recorded for part two.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2419 tests passing, 1480 of them in `Ncx.Core.Tests`, 14 of those new.
- `dotnet format --verify-no-changes`: clean.
- Mutation checks, one at a time, with the file restored afterwards:
  - Requiring the holder of the SETPOS in place of the default holder fails the sub spindle probe.
  - Not requiring the unknown shifts of the SETPOS to stand fails both probes of finding 1.
  - No coupling fails 3 tests: a motion of Y under ROTATE, the same after a change of the WORKPLANE, and a motion of Z under TILT.
  - A ROTATE always in XY fails both ZX probes.

Claude (agent), 2026-09-14, residual review of motion and frames (RR-P1-03, branch `rr-p1-03`). Nobody reviewed the fixes of the third review round above. This round checks each finding of the second and third reviews against main and fixes what still held. It also applies the answered follow-up of wave-1 question #100 and turns the markers of the answered rows in `FrameRules.cs` into comments. Decisions implemented: D35, D57, D101, and the answer of wave-1 question #100 (virtual machine 1); D100 and D102 checked. The specification is unchanged.

- Checked against main, still fixed:
  - Second review, finding 1 (the first motion under POLAR=ON or CYLINDER=n): `MotionRules.EnterTransformation` leaves an axis of the transformation that the block does not name unknown. After POLAR=OFF or CYLINDER=OFF an incremental word on it is VM202 (`PolarCylinderTests`).
  - Second review, finding 2 (the return to the MACHINE frame dropped the record): `ReturnToMachineFrame` keeps the record, and the detour probes of `SetposMotionTests` pass.
  - Third review, finding 1 (a SHIFT from an expression before the SETPOS): the record keeps the unknown shifts of the SETPOS. A motion keeps the machine position known only while exactly those stand.
- Third review, finding 2 (the frame of the SETPOS): the sub spindle case is fixed, but the rest of the frame of the SETPOS was not checked.
  - `MovesAsItsWorkpieceCoordinate` looked at the ROTATE, MIRROR, TILT and TILT_AXIS of the frame of the motion only. `HOME X`, `ROTATE=30`, `SETPOS X=100`, `ROTATE=RESET`, `LINE X=50`, `ORIGIN=1` left X known in the MACHINE frame at 250, and so did the same program with `MIRROR=X`, a TILT or a TILT_AXIS. The declared value relates to the machine position only through the rotation, which the kinematics module converts (VM 3.4, 10, D101). This is the failure class of the finding: a machine position the VM cannot know, reported as known.
  - Fixed: `SetposRecord.Turned` records whether the frame of the SETPOS turned or mirrored the axis (`FrameRules.Turns`: a ROTATE of a plane the axis is in, a MIRROR of the axis, a TILT or TILT_AXIS). A motion of such an axis leaves its machine position unknown. While nothing moves, the reset still returns the axis to its machine position. A ROTATE of another plane, or a MIRROR of another axis, at the SETPOS does not turn it.
- Third review, finding 3 (the axes a motion can move): fixed for the motions the review named (a rotary axis, the tool axis of a ROTATE, another axis under a MIRROR). Its last point, that the record path disagrees with an axis known in the MACHINE frame, still held for the coupled axes.
  - Under a ROTATE, a motion of Y left X known in the MACHINE frame at its old value, while the same X known through a record became unknown.
  - A motion along a tilted axis left X, Y and Z known in the MACHINE frame, where a RETRACT under the same tilt makes them unknown (VM 3.1a).
  - The documents do not settle which is right (open question below). The least committal workaround now applies to both paths: `FollowMotion` also makes an axis known in the MACHINE frame unknown when the frame couples it with a moved axis, and `ExecuteMotion` takes the store before the block whenever the chain holds a ROTATE, TILT or TILT_AXIS (`FrameRules.FollowsMotion`). (Reversed by the review fixes below: VM 3.4 governs the stored position, and an axis known in the MACHINE frame keeps it.)
  - Under a TILT or TILT_AXIS the coupled axes are now X, Y and Z, the axes whose combination the tilted tool axis is (VM 3.1a, as `RetractRules` reads it), no longer every linear axis: a motion of X does not move the sub spindle slide Z2. (The review fixes below drop the citation of VM 3.1a, a sentence about RETRACT only.)
  - An axis the block names and leaves unknown (a target from an expression) counts as moved, also when it was unknown before.
- Wave-1 question #100, answered by VM 1 (a state variable set from an expression becomes UNKNOWN, and the chain is a state variable, VM 2.1):
  - `TransformEntry` holds a value from an expression as UNKNOWN, as null: `Shift` and `Angles` map to `decimal?`, and `Angle` is `decimal?`. `UnknownShift` is gone. `ApplyShift`, `ApplyTilt` and `ApplyRotate` store null where they stored 0.
  - A RESET or ORIGIN that removes a shift from an expression makes its axes unknown outside the MACHINE frame instead of folding back 0 (`CutAt`, `Fold`); the P1-03 review rounds had already done this with the 0 and a list of axes. The reproducer `HOME X`, `SHIFT X={$Q1}`, `SETPOS X=100`, `SHIFT=RESET` leaves X unknown in the workpiece frame. Nothing moved and the record stands, so X is known in the MACHINE frame at 300 (D101). The same holds for ORIGIN.
  - The chain in STATE_CHANGE, trace and annotate shows an unknown value as `?`: `ROTATE={$Q2}` reads `ROTATE=?`, no longer `ROTATE=0`, and `TILT B={$Q3}` reads `TILT B=?`. No expected file changes, because no example has a chain value from an expression.
  - The TransformEntry box of the architecture 5 class diagram shows the values nullable.
- Markers:
  - The TODO(question) of `FrameRules.Cut` (wave-1 question #95, answered by VM 2.1, the chain paragraph of language 4.2 and D31) is now a comment citing them.
  - The marker of `SelectOrigin` names D123. The tilt marker, now in `FrameRules.Turns`, names wave-2 question #12, widened to a linear axis other than X, Y and Z.
  - The three chain markers of #100 in `VirtualMachine.cs` and `Handlers/FrameHandlers.cs` are now comments citing the answer. The markers of `ApplyShift` and of the TILT_AXIS frame name D125 and D126.
  - `TransformEntry.cs` had no marker; its summary cites the answer.
- Decided without a question, because no output depends on the representation: null for UNKNOWN in the entry, not a key in `ChannelState.Unknown`, because a chain can hold several entries of one kind and an entry is immutable and shared by the snapshots. `FrameRules.Setpos` takes the `BlockContext`, which `Turns` needs to resolve the axis names.
- Shared files changed: `Events/StateChanges.cs` (the chain text), the doc comments of `State/FrameState.cs` and `State/FrameSnapshot.cs`, `docs/architecture/architecture.md` (the TransformEntry box), and the README of `tests/Ncx.Core.Tests/VirtualMachine/` (`TurnedFrameMotionTests`).
- Open question, marked `TODO(question)` above `FrameRules.Couples`: does a motion in a frame that a ROTATE, TILT or TILT_AXIS turns leave an axis it does not name at its machine position (VM 3.4: "A motion that names some axes leaves the others as they were")? Or is that position unknown afterwards, as VM 3.1a makes the axes of a RETRACT under a tilt? Until it is answered, such an axis is unknown afterwards, whether it was known in the MACHINE frame or through the record of its setpos shift. (Narrowed by the review fixes below to the machine position a record derives.)

Done when: unchanged; all four criteria hold, as recorded for part two.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 3396 tests passing (3 skipped), 1882 of them in `Ncx.Core.Tests`, 25 of those new.
- `dotnet format --verify-no-changes`: clean.
- Mutation checks, one at a time, with `FrameRules.cs` restored afterwards:
  - Without `SetposRecord.Turned`, the 4 cases of the SETPOS in a turned frame fail.
  - Without the coupling of an axis known in the MACHINE frame, 4 tests of `TurnedFrameMotionTests` fail.
  - A tilt that couples every axis fails 5 tests: the rotary motion under a TILT on both paths, the two tilt cases and Z2.
  - An axis the block names and leaves unknown not counted as moved: the ROTATE probe with a target from an expression fails.

Claude (agent), 2026-09-14, review fixes of the residual review of motion and frames (RR-P1-03, branch `rr-p1-03`, rebased onto main). The review of the entry above found one blocking finding. Decisions implemented: VM 3.4 ("A motion that names some axes leaves the others as they were"), D35, D100 and D101. The specification is unchanged.

- Verified: the finding held. `FrameRules.FollowsMotion` took the store before the block whenever the chain held a ROTATE, TILT or TILT_AXIS, and the first loop of `FollowMotion` then set an axis known in the MACHINE frame (from the `home` of D100, HOME or FRAME=MACHINE) to unknown after a motion under the turned frame that did not name it. All three of the reviewer's probes reproduced on the branch and not on main:
  - `ROTATE=30`, `LINE Y=5`, `ROTATE=RESET`, `SETPOS X=0` raised VM050.
  - `TILT B=45`, `LINE X=10`, `RAPID IZ=5 FRAME=MACHINE` raised VM202.
  - Under `WORKPLANE=ZX ROTATE=30`, the first motion under POLAR=ON made Z unknown.
  - VM 3.1a, which the entry above cited, is a sentence about RETRACT only, and no document states the coupling.
- Fixed in the direction of VM 3.4: a motion never changes the stored position of an axis it does not name.
  - The first loop of `FollowMotion` and `FrameRules.FollowsMotion` are gone. `ExecuteMotion` takes the store before the block only while a setpos shift recorded against the machine position stands, as on main.
  - An axis known in the MACHINE frame keeps its position through every motion that does not move it, in a turned frame too. `TurnedFrameMotionTests` pins this: X stays MACHINE 300 after `ROTATE=30`, `LINE Y=5`, and X, Z, Z2 and B stay after `TILT B=45` or `TILT_AXIS B=45`, `LINE Y=5`. The three probes are regression tests there.
  - The record path keeps the coupling of main, under the `TODO(question)` above `FrameRules.Couples`. After a motion that the frame couples with the axis, only the machine position that the record derives (`SetposRecord.MachinePositionKnown`, D101) is taken as unknown; the workpiece coordinate stays. The two paths therefore differ after such a motion, and the marker says so. Under a TILT or TILT_AXIS the coupled axes stay X, Y and Z.
  - The comments no longer present the coupling as a rule of language 4.2 or VM 3.1a, 3.4 or 10. This covers the summaries of `FrameRules`, `FollowMotion` and `Couples`, the comment in `ExecuteMotion`, `TurnedFrameMotionTests`, four comments in `SetposMotionTests` and the VM tests README.
- Wave-1 question #100 and the markers: checked on the rebased branch, nothing to change.
  - `TransformEntry` holds a value from an expression as null (UNKNOWN).
  - The reproducer `HOME X`, `SHIFT X={$Q1}`, `SETPOS X=100`, `SHIFT=RESET`, and the same with `ORIGIN=1`, leaves X unknown in the workpiece frame and known in the MACHINE frame at 300 (`SetposTests`).
  - The markers left in `FrameRules.cs` belong to rows that are still open. `SelectOrigin` names D123 (wave-1 #96 and #111), `Turns` names wave-2 question #12, and `Couples` is the new question below. `TransformEntry.cs` has no marker.
- Open question, reported for a new D entry because none of D121 to D127 covers it; marked `TODO(question)` above `FrameRules.Couples`. Does a motion in a frame that a ROTATE, TILT or TILT_AXIS turns change the machine position of an axis the block does not name? VM 3.4 keeps the stored position, while on a controller the turned frame moves that axis as well, by an amount only the kinematics module knows (VM 10). Until it is answered, an axis known in the MACHINE frame keeps its position, and only the machine position that a record derives for a coupled axis is taken as unknown.
- Shared files: none beyond those of the entry above. The line for `TurnedFrameMotionTests` in the VM tests README changed.

Done when: unchanged; all four criteria hold, as recorded for part two.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 3407 tests passing (3 skipped), 1885 of them in `Ncx.Core.Tests`. 3 of those are new (the probes), and 4 cases of `TurnedFrameMotionTests` now expect the axis known in the MACHINE frame.
- `dotnet format --verify-no-changes`: clean.
- Mutation checks, one at a time, with `FrameRules.cs` restored afterwards:
  - The branch before this fix, with the MACHINE-frame coupling, fails 7 tests of `TurnedFrameMotionTests`.
  - No coupling on the record path fails 3 tests of `SetposMotionTests`: a motion of Y under ROTATE, the same after a change of the WORKPLANE, and a motion of Z under TILT.

Claude (agent), 2026-09-14, second review fixes of the residual review of motion and frames (RR-P1-03, branch `rr-p1-03`). The branch of the entries above was squashed onto main (77035c1); this round starts from main. The review of the review fixes above found one blocking finding. Decisions implemented: VM 3.4 ("A motion that names some axes leaves the others as they were"; after SETPOS "the position store keeps its physical value"), D35 and D101; D230 cited. The specification is unchanged.

- Verified on main: the finding held. `FollowMotion` still made the machine position that the record of a setpos shift derives (`SetposRecord.MachinePositionKnown`) unknown for an axis the block did not move, whenever a ROTATE, TILT or TILT_AXIS coupled it with a moved axis (`FrameRules.Couples`). All four of the reviewer's probes reproduced on the mill-turn machine, as the failing twins below:
  - `HOME X`, `ROTATE=30`, `SETPOS X=100`, `LINE Y=5`, `ROTATE=RESET`, then `SETPOS X=0` raised VM050, and `RAPID IX=5 FRAME=MACHINE` raised VM202.
  - `HOME Z`, `TILT B=45`, `SETPOS Z=100`, `LINE X=10`, `TILT=RESET`, then `SETPOS Z=0` raised VM050, and `RAPID IZ=5 FRAME=MACHINE` raised VM202.
  - `SPINDLE_MODE:MAIN=AXIS`, `HOME Z`, `WORKPLANE=ZX ROTATE=30`, `SETPOS Z=100`, `WORKPLANE=XY`, `POLAR=ON`, `LINE C=5`, `POLAR=OFF`, `ROTATE=RESET`, `SETPOS Z=0` raised VM050.
- VM 3.4 settles both paths, so no question stays open. After SETPOS the store keeps the physical value, and D101 records the shift against the machine position. The store of an axis the block did not move is therefore the machine position the record derives, as it is for an axis known in the MACHINE frame, and VM 3.4 leaves it as it was.
- Fixed: `FollowMotion` looks only at the axes the block moved (`MovedAxes`). For such an axis, `MovesAsItsWorkpieceCoordinate` decides as before (the holder, the unknown shifts, `Turns`, `SetposRecord.Turned`). `Couples`, `SpaceAxes` and their `TODO(question)` are gone, and the record path and the MACHINE path now agree after a motion that does not name the axis.
- Tests:
  - `SetposMotionTests`: `Reset_AfterAMotionOfAnotherAxisInATurnedFrame_ReturnsTheAxisToItsMachinePosition` and `Tilt_AMotionOfAnotherLinearAxis_KeepsTheMachinePosition` are renamed from `..._LeavesTheAxisUnknown` and `..._LeavesTheMachinePositionUnknown`, and both now expect X back in the MACHINE frame at 300.
  - `SetposMotionTests`: `Rotate_AfterAChangeOfTheWorkplane_StillTurnsThePlaneWhereItStood` expects X at 300 and checks that the entry keeps XY. Without the coupling, only a moved axis tells which plane a ROTATE turns. The new `Rotate_AfterAChangeOfTheWorkplane_AMotionOfAnAxisOutsideItsPlaneKeepsTheMachinePosition` does that: after `WORKPLANE=ZX` a motion of Z keeps its machine position (445).
  - `SetposMotionTests`: the comments of the ZX, rotary and Z2 cases cite VM 3.4 in place of the workaround.
  - `TurnedFrameMotionTests` gets the record-path twins of its three probes, each with no diagnostic. The ROTATE twin and the TILT twin each end in `SETPOS` or in an incremental `FRAME=MACHINE` move. The POLAR twin checks Z's workpiece coordinate 100 and machine position 450 under POLAR=ON. The class summary and the line of the VM tests README cover both paths.
- Marker: the tilt marker in `FrameRules.Turns` now names D230, the entry wave-2 question #12 became. Its recommendation ("Keep what the code does") is what the code does. The marker of `SelectOrigin` names D123 (still open). `TransformEntry.cs` has no marker.
- Wave-1 question #100: checked on main, nothing to change. `TransformEntry` holds a value from an expression as null (UNKNOWN), and `CutAt` leaves the axes of a removed unknown shift unknown outside the MACHINE frame. The reproducer `HOME X`, `SHIFT X={$Q1}`, `SETPOS X=100`, `SHIFT=RESET` (and the same with `ORIGIN=1`) leaves X unknown in the workpiece frame and known in the MACHINE frame at 300 (`SetposTests.Reset_OfAShiftFromAnExpressionBeforeTheSetpos_LeavesTheAxisUnknownOutsideTheMachineFrame`).
- Shared files: the doc comment of `MachinePositionKnown` in `State/SetposRecord.cs`, and the `TurnedFrameMotionTests` line of the VM tests README.

Done when: unchanged; all four criteria hold, as recorded for part two.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 3929 tests passing (6 skipped), 1943 of them in `Ncx.Core.Tests`. 6 of those are new: the 5 cases of the twins and the Z case after a change of the WORKPLANE. 3 cases of `SetposMotionTests` now expect the axis back in the MACHINE frame.
- `dotnet format --verify-no-changes`: clean.
- Mutation checks, one at a time, with `FrameRules.cs` restored afterwards:
  - With the coupling of main, 8 tests fail: the 5 cases of the twins and the 3 changed cases of `SetposMotionTests`.
  - A ROTATE that turns the plane of the current WORKPLANE instead of its own fails the new Z case.
