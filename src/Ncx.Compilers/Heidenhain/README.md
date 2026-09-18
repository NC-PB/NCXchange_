# Heidenhain

The Heidenhain compiler: an NCX program as iTNC 530 Klartext, per section 8 of `docs/controllers/heidenhain.md` and the Heidenhain column of `docs/spec/controller-mapping.md` (phase 3, P3-04). One `.h` file per program between `BEGIN PGM` and `END PGM`, consecutive block numbers from 0, the comma, a sign on every coordinate, `TOOL CALL`, `CC` plus `C`, `CYCL DEF` with its `Q` parameters, `LBL` sections after the `M30` of every caller.

## Open first

1. `HeidenhainCompiler.cs`: the compiler on the framework of `../CompilerBase.cs`, and `WriteBlock`, which writes a block through the concerns below in the order the control executes it: the structure and the label, the state words (each in a Klartext block of its own before the motion, language 5 rule 3), the motion, then what follows it. A word no concern writes is the ERROR `CMP101` (language 2 rule 8).
2. `HeidenhainBlock.cs`: the block being written with what the concerns write it with (the state before and after, the machine, the numbers, the target state, the templates) and the words already written.
3. The concerns, one file each, named after what they map:
   - `HeidenhainProgramFrame.cs` (rule 1): `BEGIN PGM`, `M30`, `END PGM`, `LBL 0`, the `;` and `* -` blocks, `RAW`.
   - `HeidenhainMotion.cs` (rule 2): `L` with `FMAX` or `F` on change, `M91`, `IX`, `LN`; `HeidenhainCompensation.cs`: `R0`/`RL`/`RR` on the `L` block where the compensation changes, and the arcs, `CYCL CALL` and `M140` that the control would run with another one (`CMP115`); `HeidenhainArcs.cs` (rule 4): `CR`, `CC` plus `C`, `CC` plus `CP IPA`; `HeidenhainAxes.cs` and `HeidenhainNumbers.cs`: the axis words, the signs and the `Q` parameters in coordinates; `HeidenhainParameters.cs`: `FQ1` and `SQ2`, a feed or a speed of a `Q` parameter, where it has the value NCX read (`CMP117`).
   - `HeidenhainToolCall.cs` (rule 3): `TOOL CALL n axis S` from `TOOL`, `WORKPLANE` and the `RPM` of the same or the following block, `TOOL DEF`.
   - `HeidenhainFrames.cs` (rule 5): `HOME` as `M91` moves to `home` of `[[axis]]` (D100), `M140 MB`, `M128`/`M129` or `FUNCTION TCPM`, `M126`/`M127`, `M116`/`M117`, cycle 32; `HeidenhainChain.cs`: `ORIGIN` as cycle 247 and the chain as cycles 7, 8, 10 and `PLANE` in program order (D31); `HeidenhainSetpos.cs`: `SETPOS` folded into the cycle 7 datum shift that makes the position read as declared (machine-config 3, D55).
   - `HeidenhainCycles.cs` and `HeidenhainCycleValues.cs` (rule 7): `CYCL DEF` with the `Q` parameters of the catalog's signature, `CYCL CALL` or `M99`, cycle 9.
   - `HeidenhainSubprograms.cs` (rule 6): `LBL n` sections, `CALL LBL`, `CALL PGM`; `HeidenhainFlow.cs` and `HeidenhainFormula.cs`: `LBL`, `FN 9` to `FN 12`, `CALL LBL REP`, the `Q` parameter formulas; `HeidenhainLabels.cs`: the labels and `LBL` sections of one program, which must be one `LBL` each and never `LBL 0` (`CMP116`); `HeidenhainArrivals.cs`: what a `JUMP` or `CALL LBL REP` brings to the blocks after its label, which the STATIC walk does not follow: after a label a word a block states stands again (`HeidenhainFlow.WriteLabel`), `R0`/`RL`/`RR` and `F` only where a block from the label on states them, since every way brings the modal state of the program (language 2 rule 2, 4.9), and each way of a jump is followed to the first `L` block and feed motion after its label (`CMP115`, `CMP118`), with `HeidenhainLabelWays.cs` (what each label found on the way the text runs, and the jumps forward that wait for their label), `HeidenhainLabelWay.cs` and `HeidenhainArrival.cs`.
   - `HeidenhainFunctions.cs`: `M136`/`M137`, the spindle, the coolant and `[func]` through the machine's tables, `MFUNC`, `M0`/`M1`.

The codes are `CMP100` to `CMP299`, in `../DiagnosticCodes.Heidenhain.cs`. The tests are in `../../../tests/Ncx.Compilers.Tests/Heidenhain/` (one test per rule of heidenhain 8), the acceptance of milestone M5 in `../../../tests/Ncx.Acceptance/Examples/HeidenhainCompilerTests.cs`.

## Never here

- A class per machine or builder: two Heidenhain machines differ in their machine file (templates, tables), not in this code.
- Deciding what a word means: the virtual machine did, and the compiler writes the state before and after each block.
