# P5-01 Siemens reader

Phase: 5 | Milestone: M8 | Depends on: `P3-07` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

SINUMERIK 840D sl programs into canonical NCX, per section 11 of `controllers/siemens.md`.

## Scope

- Tokenizer for `%_N_..._MPF/SPF` units and archives, `;$PATH`, `N`, `;`, `/n`, addresses with `=` and extensions, expressions as values, strings, `( )` argument lists, labels `NAME:`.
- G groups with the source-side state, `AC()`/`IC()`/`DC()`/`ACP()`/`ACN()`, arcs (`I J K`, `I=AC()`, `CR=`, `AR=`, `TURN=`, `CIP`, `CT`), polar with poles, chamfer and contour forms expanded, feed words, path control and orientation words as `RAW` modal words.
- Frames per section 4 of the Siemens document (replacing versus additive, `G58`/`G59`, `CYCLE800` into `TILT`/`TILT_AXIS` with `MOVE` and a `RETRACT`, `$P_UIFR` as `RAW`), `G53`/`G153`/`SUPA`, `G74`/`G75`, `PRESETON`, `DIAMON`/`DIAMOF`/`DIAM90`, `G70`/`G71`/`G700`/`G710`.
- Tools and spindles: `T`, `T=`, `T="name"`, `T<n>=`, `M6`, `D`, `T0`, `S<n>=`, `M<n>=3`, `SETMS`, `G96`.. with `LIMS`, `G25`/`G26`, `SPOS`/`SPOSA`/`M19`/`M70`, `COUPON`/`COUPOF`.
- Cycles per the sl signatures with `_AXN` as `AXIS`, `MCALL` handling, patterns expanded; flow structures lowered; `PROC`/`RET`/`M17`, `EXTERN`, `L`, `name P3`, `CALL`, `EXTCALL`; `WAITM`/`INIT`/`START`/`WAITE`.
- `MSG`, `STOPRE`, `WORKPIECE`, synchronized actions, interrupts, `DEFINE`: `RAW`.

## References

- controllers/siemens.md
- controller-mapping.md (Siemens column)
- machine-builders.md section 2

## Done when

- The Hermle C22 U program and a Burkhardt+Weber program of the corpus convert without loss (every block accounted for, `RAW` only where the document says so).
- Every `.mpf` of the corpus converts without a crash.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

- Claude (agent), 2026-09-14: the reader is in `src/Ncx.Readers/Siemens/` (tokenizer, units and archives, G groups, motion with arcs, polar coordinates and corners, frames and `CYCLE800`, tools and spindles, cycles and patterns, flow and calls, channels, `RAW`), registered for `ncx convert` in `src/Ncx.Cli/Program.cs`, with the codes RDR500 to RDR505 in `src/Ncx.Readers/DiagnosticCodes.Siemens.cs`. The tests per rule of siemens 11 and per Siemens cell of the mapping are in `tests/Ncx.Readers.Tests/Siemens/`, the hand-written 840D sl program and the corpus runs in `tests/Ncx.Acceptance/Examples/SiemensReaderTests.cs`.
- Decisions made while doing it, none of them a change of the specification:
  - An `_INI` unit is a `PROGRAM` section of `RAW` blocks named `NAME_INI`, since the program of the archive it initializes has the name `NAME` (virtual machine 3.6); its last block ends it without the RDR010 of a program without `M30`.
  - A block kept as `RAW` as a whole leaves unknown the facts it was about to change that the reader writes as modal words of their own (the working plane, the feed type, the diameter mode, the modal cycle, the transformation, the chain of transforms); the facts the reader writes into every block, such as the motion code or the master spindle, stay as the block set them (D5).
  - `SCALE` with factors leaves the chain unknown after its cut, so the next instruction that deletes the frame stays `RAW`; `G59` without an additive shift appends one after the shift of `TRANS` or `G58`; a new swivel and `CYCLE800()` remove every tilt of `CYCLE800` at the end of the chain, one `RESET` each.
  - `EXTCALL("/.../_N_NAME_SPF")` calls `NAME`; `RNDM` and `FRCM` in a block without a line write nothing; `ANG` without an end coordinate starts the contour of two blocks.
  - The phase plan names a test `CYCLE800Mode39_BecomesTiltAxisWithRetract`, while siemens 4 and controller-mapping 1 make the axis-wise mode 39 a `TILT` and only the rotary-axes mode a `TILT_AXIS`; the tests follow the documents (`CYCLE800Mode39_WithOneTurningAngle_BecomesTiltWithRetract`, `CYCLE800RotaryAxesMode_BecomesTiltAxis`).
  - `Convert_MachineOfAControllerWithoutReader_ExitsOneWithCli251` of `tests/Ncx.Acceptance/Cli/ConvertCommandTests.cs` runs with a registry that holds only the Fanuc reader, since Siemens now has one.
- Open questions, each a `TODO(question)` in the code: the order of the `TILT` angles against the axis-wise orders of `CYCLE800` (mode 39 with two turning angles, the Hermle form, stays `RAW`); the rotary axes that `_A` and `_B` of the rotary-axes mode name; a replacing frame instruction whose cut would remove a tilt of `CYCLE800`; a `MESSAGE` word for `MSG` (sample-corpus 3); the pair `G70`/`G71` against `G700`/`G710`; `T=0` on a mill; the number of the configured master spindle; the feed mode after `G97`, `G971`, `G972`; `G54` and the programmable frame; `G75` without `FP`; `DIV`; `RET` in a main program; a name that is neither a subprogram of the file nor `EXTERN`; the type of `DEF`; the `DEFINE` block itself; `GROUP_END`; `CYCLE801`/`CYCLE802`; the sign of `DPR`. The ones of D119, D132, D149, D154, D155, D157, D181 and wave-2 questions #55, #56, #74 follow their recommended workarounds.
- Done when: neither criterion can be shown yet. `NCX_CORPUS` is not set where this ran, so the corpus tests skip with their message, and the batch runner of P3-07, whose report under `tests/corpus-reports/` is the evidence for "without loss", is not on main; the Hermle program also keeps its `CYCLE800` of mode 39 with two turning angles `RAW` until the order question is answered. What holds: the hand-written 840D sl program converts and checks without ERROR with `siemens-840dsl-mill.toml`, with `RAW` only for `MSG`, `WORKPIECE` and `STOPRE`, and the INDEX archive form reads as one file with a section per unit. The task stays in `inbox/`.
- Claude (agent), 2026-09-14, review fixes (commit "P5-01 review fixes", the branch rebased onto main first):
  - `G4 S10` and `G4 S2=10`: the spindle words of a block with `G4` belong to the dwell and no longer set the speed (siemens 3), so the dwell converts with the known speed into `DWELL` in seconds (controller-mapping 1, DWELL); `S0=` counts the master spindle.
  - `T0` and `T=0` alone on a mill that changes with `M6` write `PRELOAD=0` and leave tool 0 as the selection, so that the `M6` after them is `TOOL=0` (siemens 5; controller-mapping 3, TOOL=4 and TOOL=0), not a bare `TOOL` that check rejects (VM041). Where the program ends after them without an `M6` or another `T`, they are `TOOL=0`, as the TOOL=0 row reads the Hermle's `T=0` before `M30`. The open question on `T=0` is narrower now: a `T0` alone that another `T` or the return of a subprogram follows (`PRELOAD=0` there).
  - An `IF`, `WHILE`, `FOR` or `REPEAT ... UNTIL` whose condition has no NCX form (`IF $P_SEARCH`) opens a structure kept as `RAW` as a whole: its blocks, its `ELSE` and its end are `RAW` with a WARNING, without the ERROR RDR504, and what they may change becomes unknown (D5; siemens 8). `REPEAT` looks ahead to its `UNTIL` for this. `IF cond GOTOS` is a conditional `JUMP` to the label after the header (siemens 8); before, it was taken for a structure.
  - A repeated range that does not end directly before its repeat (`REPEAT START END P=`, `REPEATB LABEL P=`, `REPEAT LABEL P=` with `ENDLABEL:` between) is lowered to a `SUB` (controller-mapping 6, REPEAT + TIMES; siemens 11 rule 6): the structure pass copies the range into a `SUB` section `REPEAT_<first>_<last>` behind its program, the blocks stay where they stand since the control runs them there as well, and the repeat is `CALL=REPEAT_<first>_<last> TIMES=P`; the section runs with the state its repeats agree on. A range with an end, a return, a jump, a call of a subprogram of the file, another repeat or a structure it does not hold whole stays `RAW` with the reason. `ENDLABEL:` writes no `LABEL`, since a unit may hold it more than once. A repeat without `P` writes no `TIMES`, as D212 recommends. Shared files for this: `src/Ncx.Readers/SourceStructure.cs` (`Repeat`), `SourceRepeat.cs` (new), `StructurePass.cs`, `StructurePass.Repeats.cs` (new), `StructurePass.Contours.cs` (`NewSectionName`), `ContourLayout.cs` (`Repeats`) and `ReaderBase.cs` (`RepeatOf`).
  - A second hand-written program in `tests/Ncx.Acceptance/Examples/SiemensReaderTests.cs` covers the constructs the first leaves out (`DEF`, `EXTERN`, `DEFINE`, the skip levels, `T="name"`, `T0` then `M6`, `T=0` before `M30`, `CHR`, `RNDM`, `TURN`, `G4 F` and `G4 S`, `G505`, `G500`, `G59`, `ROT`, `MIRROR`, `ROTS`, `SCALE`, `G153`, `SUPA`, `G75`, `G74`, `PRESETON`, `LOOP`, `GOTOB`, `GOTOS`, the forms of `REPEAT`, `L100`, `CALL`, `EXTCALL`, `M17`); it converts and checks without ERROR, with `RAW` only for `EXTERN` of a subprogram outside the file and the factors of `SCALE`. The unit tests whose output carries tools, motion or flow now run check on it (`SiemensRead.CheckedBody`); the five-axis test machine now puts its rotary axes directly behind the linear ones, since appended behind the other tables of the file the machine held only the rotary axes.
  - Open questions added, each a `TODO(question)` in the code: whether the blocks of a structure whose condition has no NCX form stay `RAW` with it (controller-mapping 6 keeps only the system variables of the `GOTOF` form `RAW`; the reader keeps the whole structure `RAW`, the least committal reading); whether the range of a `REPEAT` lowered to a `SUB` leaves its place or is copied, and how the `SUB` is named (copied, `REPEAT_<first>_<last>`). The `T=0` question is narrowed as said above; `REPEAT` without `P` cites D212.
  - Done when: unchanged, neither criterion can be shown yet for the reasons above; the task stays in `inbox/`.
