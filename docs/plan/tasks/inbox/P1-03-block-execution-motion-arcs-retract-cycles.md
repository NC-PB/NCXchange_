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
