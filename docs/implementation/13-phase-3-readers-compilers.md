# Phase 3: readers and compilers, first pair

Status: 2026-09-11, not started. Milestones M4, M5, M6. Tasks P3-01 to P3-07. Closed when `2.5D_FRAESEN` reads from both sources to the same canonical text and compiles back to both without loss, `BOHREN` and `3D_FRAESEN` round-trip, and the corpus programs of both families never crash the readers (`../plan/phases.md`).

## Entry state

Phases 0, 1 and 2 closed. The maintainer's corpus is requested at the start of this phase (`20-schedule.md`); the tests that need it read `NCX_CORPUS` and skip without it.

## Decisions needed first

None new expected. D106 places `ISourceRule` in `Ncx.Readers` and `IBlockWriter` in `Ncx.Compilers`. The comparison rules of P3-07 are written down in this file (section "Comparison rules") and copied into `code-guidelines.md` 8 in P3-07; they need no decision because they change no meaning. New decisions will come from the sources: every construct the readers meet that is neither a word nor a documented `RAW` is written up per `sample-corpus.md` 3 and asked, not decided in the code.

## Task order inside the phase

P3-01, P3-02, P3-03, P3-04, P3-05, P3-06, P3-07, one after the other. P3-04 depends on P3-02 (it compiles `BOHREN.ncx`, which only the Fanuc reader produces; F27).

## Tasks

### P3-01 Reader framework and source-side state

Files in `src/Ncx.Readers/`:

- `IReader` (`Controller`, `Read(SourceFile, MachineConfig, ReadOptions)` returning `NcxProgram`), `ReaderBase` (template method: tokenize, walk the source blocks, `SourceState.Apply`, `ReadBlock` per family, `NcxBuilder`, diagnostics with `RDR` codes, the structure pass), `ReaderRegistry` (name to factory), `ReadOptions`.
- `ISourceTokenizer`, `SourceBlock` (Line, Text, Words, Comment, BlockSkip, Continuation), `SourceWord` (Address, Text, Number, Expression).
- `SourceState`: the small source-side virtual machine of architecture 7: modal groups, absolute or incremental, plane, feed mode, active cycle, preloaded tool, the spindle that owns the last `S`, the G-code system, the positions the reader needs for chamfers and polar conversions.
- `StructurePass` on the reader side (language 4.13, D48, D89): programs and subprograms as sections, jump-entered code after the end moved in front of `PROGRAM=END` behind a `JUMP=END`, the `M99` loop rule of controller-mapping 6 (`LABEL=START`, `JUMP=START`, `/M99` as `SKIP JUMP=START`, `/M30` as `SKIP JUMP=END`).
- `RawEmitter`: `RAW:<CONTROLLER>` or `RAW:<BUILDER>` with the source text and a WARNING per block (D5); nothing is dropped.
- `ISourceRule` (public, D106): `Read(SourceBlock, SourceState, NcxBuilder)` returning whether the rule claimed the block or sequence (D40, D66); the configuration tables are tried first, M and G codes compared by number so that a source `M08` matches the table's `M8` (D105).
- Comments: a source comment on a block becomes the block's comment; a comment-only source line becomes trivia; `( name )` after `O` becomes `NAME`; Heidenhain `* -` becomes `SECTION` (controller-mapping 1).

Cite: architecture 7 (class and sequence diagrams); language 4.13; controller-mapping 1, 6, 9; D5, D40, D48, D66, D89.

Tests first (`tests/Ncx.Readers.Tests/`): a fake reader with three rules reads a three-line input into the expected NCX text; `RAW` blocks survive a `format` round trip; the jump-entered section case of `INCREMENTAL_SUB` (a `GOTO 300` below `M30` and `GOTO 22` back) lands in front of the end behind `JUMP=END` with the labels intact; the `M99` loop case.

### P3-02 Fanuc reader

Files in `src/Ncx.Readers/Fanuc/`, one file per concern so that an NC programmer finds `G81` in `FanucCycles.cs`:

- `FanucTokenizer` (`%`, `O`, `N`, `( )` comments anywhere, `/` and `/n`, addresses with signed numbers with leading or trailing dots, `#` variables, `[ ]` expressions, several G per block, up to three M per block; CRLF and LF).
- `FanucReader` (`ReadBlock` dispatching by address to the files below), `FanucModalGroups` (groups 01, 02, 03, 05, 06, 07, 08, 09, 10, 12 of `fanuc.md` 3; system A, B, C from `gcode_system`; `U W H V` in system A).
- `FanucMotion` (`G0`..`G3` with the verb on every block, `G90`/`G91` per word, `I J K` as `CENTER:IX`, `R`, helix, `G16`/`G15` polar converted, chamfer and rounding `,C` and `,R` expanded per D58).
- `FanucToolWords` (`T` alone as `PRELOAD`, `M6` as `TOOL` from the source state, `T4 M6`, lathe `T0656` as `TOOL=6 OFFSET=56`, `G43 H`, `G49`, `D`; the `S` binding rule; `M6` without preload as a source ERROR).
- `FanucFrames` (`G54`.., `G54.1 P`, `G52` with `SHIFT=RESET`, `G53`, `G28`/`G30` as `HOME` with `POINT`, `G50`/`G92` as `SETPOS` or `RPM_MAX`, `G68`/`G69`, `G51.1`/`G50.1`, `G68.2`/`G53.1` as `TILT` with `MOVE`, `G68.1` as `TILT_AXIS`, `G43.4`/`G43.5` as `TCPM` and vectors, `G12.1`/`G7.1`, `G5.1 Q1` as `TOLERANCE`).
- `FanucCycles` (`G73`..`G89` into `CYCLE=` with absolute `CLEARANCE` and `DEPTH`, `G98`/`G99` as `CYCLE_RETRACT`, a `CYCLE_CALL` per position block, `G80`, `K` repeats expanded, lathe cycles and the modal turning cycles from the catalog, `AXIS=X` for `G87`..`G89`).
- `FanucMacro` (`#n` to `Vn`, `[ ]` expressions to `{ }`, `EQ NE GT GE LT LE` to the NCX operators, `FIX`/`FUP` to `INT` forms, `IF GOTO`, `IF THEN`, `WHILE DO END` lowered to `LABEL`/`JUMP`/`IF`, `GOTO`, `G65` with the letter table to `CALL` with `ARG`, `G66`/`G67` as `RAW`, `M98 P L`, `M99`, `M99 P`).
- `FanucBuilder` (M codes through the machine's tables, compared by number (D105), `MFUNC` fallback with the WARNING, wait marks to `SYNC` with `WITH` per `[sync]`, `G10`, `G31`, `G38`, builder G macros as `RAW`).
- `ncx convert <file> --machine <name>` in `Ncx.Cli` (`--machine` required, D77; the reader, `NcxWriter`, then a STATIC `check` over the produced program whose diagnostics are reported together with the reader's).

Cite: controllers `fanuc.md` 1 to 9; controller-mapping (the Fanuc column of every section); language 4.4, 4.7, 4.9, 4.13; D7, D33, D49, D58, D77.

Tests first: one string-in string-out test per row of the Fanuc column (`G81_WithG99_BecomesDrillCycleWithClearanceRetract`, `T5Alone_BecomesPreload`, `G91G28Z0_BecomesHomeZ`, `G52_ReplacesEarlierShift_EmitsReset`, ...); then the acceptance:

```
dotnet run --project src/Ncx.Cli -- convert docs/spec/examples/sources/2.5D_FRAESEN.fanuc.nc --machine fanuc-mill-30i
```

equals `docs/spec/examples/2.5D_FRAESEN.ncx` byte for byte, except the trivia and comments (the example's line comments are the specification's reading aid, not reader output; the comparison strips comments and trivia on both sides and compares the blocks, which after D90 are canonical by construction). `BOHREN.fanuc.nc` converts into `tests/Ncx.Acceptance/Expected/BOHREN.ncx`, reviewed by hand against `controller-mapping` 5 before it is frozen. `3D_FRAESEN.fanuc.nc` converts in well under a second. The Nakamura pair converts with `RAW:NAKAMURA` only for `G411`, `G300`, `G10`, `G333`, `G131` and the builder codes the `[raw]` table names. Under `NCX_CORPUS`, every Fanuc and ISO file converts without a crash.

### P3-03 Compiler framework and number formatting

Files in `src/Ncx.Compilers/`:

- `ICompiler` (`Controller`, `Compile(NcxProgram, MachineConfig, CompileOptions)` returning `CompileResult` with text per output file and diagnostics), `CompilerBase` (template method: expand, run the VM in STATIC mode with the compiler subscribed, `WriteHeader`, `WriteBlock(block, before, after)`, `WriteFooter`, `WriteSubprograms`, which emits each `SUB` section once, not once per `CALL`, although the STATIC pass walks it at every `CALL` with the caller's state (D99): the section is written from an unknown target state, so every modal word stands at its first use inside it and the text is right for every caller, and walks that would write different lines are an ERROR naming the section and the calls; under `program_layout = "file_per_program"` (Heidenhain) once per calling program (VM 3.9, language 4.13); look-ahead from the STATIC pre-pass for `{next}`, `{b}`, `{c}`, `auto_preload` (D52, VM 3.5); the tool table of D10 with the warning block at the head), `CompilerRegistry`, `CompileOptions`.
- `TargetState`: what the target control has active (modal G per group, the last feed, the active tool, the last spindle M), so that a modal word is written only on change.
- `IBlockWriter` (public, D106) and the `BLOCK_WRITE` raise with mutable output lines before they reach the buffer.
- `NumberFormatter` (per `[format]`: decimals per address, `trailing_zeros`, `decimal_separator`, InvariantCulture, never rounds what fits, WARNING when a value has more decimals than the machine takes), `OutputBuffer` (lines, `block_numbers`, `max_line_length`, `comment_charset` transliteration, `line_ending`), `ProgramLayout` (`one_file` or `file_per_program`, D48), output under `out/<machine>/` or `--output`.
- `ChainWriter`: the frame chain written in program order on every target (D31).
- `RAW` of another controller or builder, and a `CYCLE:<controller>=n` block of another controller family (D94): `CMP` ERROR with the text of controller-mapping 9.

Cite: architecture 8 (class and sequence diagrams); machine-config 2, 3; language 2 rule 5, 4.15; VM 3.5; D10, D31, D48, D52.

Tests first (`tests/Ncx.Compilers.Tests/`): a fake compiler writes three blocks with two different `[format]` tables (comma, decimals, block numbers); `RAW:FANUC` compiles to Fanuc and fails for Heidenhain with the specification's ERROR text; a `CYCLE:HEIDENHAIN=251 Q215=0` block compiles to Heidenhain with `Q215` in source order and fails for Fanuc with the same ERROR (D94); a modal word is written once; `{next}` is filled from a following `PRELOAD` and from `auto_preload`; the D10 warning block appears when the tool table is missing.

### P3-04 Heidenhain compiler

Files in `src/Ncx.Compilers/Heidenhain/`: `HeidenhainCompiler`, `HeidenhainProgramFrame` (`BEGIN PGM name MM`, consecutive block numbers from 0, `END PGM`), `HeidenhainMotion` (`L` with signs, `FMAX`, `F` on change, `RL`/`RR`/`R0`, `CC` plus `C` as the canonical arc form, `CR` when the block had `R`, `CP IPA` for sweeps, `LN` for vectors, `IX` forms), `HeidenhainToolCall` (`TOOL CALL n axis S` assembled from `TOOL`, `WORKPLANE` and the `RPM` of the same or the next block, `TOOL DEF` for `PRELOAD`, `offsets_with_change`), `HeidenhainFrames` (`CYCL DEF 247`, `7`, `8`, `10`, `PLANE SPATIAL` and `AXIAL` with `MOVE` and `ROT` from `[transform]`, `M91` moves for `HOME` from `home` (D100: ERROR here when missing), `M128`/`M129` or `FUNCTION TCPM`, `M126`/`M127`, `M116`/`M117`, `M140 MB`, `CYCL DEF 32`), `HeidenhainCycles` (`CYCL DEF` with the `Q` parameters in the control's order from the catalog, `CYCL CALL` or `M99`, `Q204` for `CYCLE_RETRACT=SAFE`), `HeidenhainSubprograms` (`LBL n` ... `LBL 0` after the `M30` of every calling program, or `CALL PGM` files per setting), `HeidenhainFlow` (`CALL LBL REP`, `FN 9`..`FN 12`, formulas for `VAR`), `HeidenhainNumbers` (the comma). `ncx compile <file.ncx> --machine <name>` in `Ncx.Cli`.

Cite: controllers `heidenhain.md` 8 (every rule); controller-mapping (the Heidenhain column); machine-config 2, 3; D31, D34, D48, D100.

Tests first: one test per rule of `heidenhain.md` 8; then the acceptance:

```
dotnet run --project src/Ncx.Cli -- compile docs/spec/examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530
```

is equivalent to `docs/spec/examples/sources/2.5D_FRAESEN.h` under the comparison rules below (M5); `Expected/BOHREN.ncx` compiles to a program equivalent to `BOHREN.h`.

### P3-05 Heidenhain reader

Files in `src/Ncx.Readers/Heidenhain/`: `HeidenhainTokenizer` (numbered blocks, `~` continuation, the comma as decimal separator with the dot accepted, `;` comments, `* -` sections, `/` skip), `HeidenhainMotion` (`L`, `C` with the `CC` pole into one `ARC` with an absolute `CENTER`, `CR`, `CT` converted, `LP`/`CP` with the pole converted to Cartesian, `CP IPA` beyond 360 degrees as `ANGLE`, `LN` vectors, `IX`.. prefixes, `FMAX`/`F`/`F AUTO`, `RL`/`RR`/`R0`, `M91`), `HeidenhainToolCall` (`TOOL CALL` split into `TOOL`, `RPM`, `WORKPLANE` from the axis letter, both offset words with the tool number; `TOOL CALL S` alone; `TOOL DEF`), `HeidenhainCycles` (`CYCL DEF` 200 to 209, 240, 241 into `CYCLE=` with `Q203`-relative values made absolute, `CYCL CALL`, `M99`, `CYCL CALL PAT` expanded, `CYCLE=OFF` emitted before the next non-cycle motion, cycles 7, 8, 10, 247, 19, 32, 9), `HeidenhainPlane` (`PLANE SPATIAL` as `TILT`, `PLANE AXIAL` and cycle 19 as `TILT_AXIS`, `MOVE` and `ROT` from the options, `PLANE RESET`, other forms as `RAW`), `HeidenhainLabels` (two passes: `LBL n` closed by `LBL 0` and called without `REP` is a `SUB` section; an `LBL` used by `REP` or as an `FN 9`..`FN 12` target is a `LABEL`; `CALL LBL REP` as `REPEAT`/`TIMES`; `CALL PGM`), `HeidenhainQ` (`Q`, `QL`, `QR`, `QS` names kept, `FN 0`..`FN 5`, formulas, `FN 9`..`FN 12`), `M128`/`M129`, `M126`/`M127`, `M116`/`M117`, `M136`/`M137`, `M140`; `BLK FORM`, `APPR`/`DEP`, `TCH PROBE`, `FN 16`/`18`/`19`, OEM cycles as `RAW:HEIDENHAIN`.

Cite: controllers `heidenhain.md` 1 to 7; controller-mapping (the Heidenhain column); D7, D31, D58, D81 to D86.

Tests first: one test per rule of `heidenhain.md` 7; the acceptance: `2.5D_FRAESEN.h` converts to the `.ncx` (blocks equal; the WARNING about the missing `M30`); `BOHREN.h` converts to the same `Expected/BOHREN.ncx` as the Fanuc source. This is the real test of milestone M6 and the place where the two readers will disagree: `BOHREN.h` has `Q204=5` (second clearance) where the Fanuc source has `G99` and `R5.`; whether the Heidenhain reader emits `SAFE=5 CYCLE_RETRACT=SAFE` or `CYCLE_RETRACT=CLEARANCE` when `SAFE` equals `CLEARANCE` is decided in the task log with a note in controller-mapping 5, and the expected file follows the decision. `3D_FRAESEN.h` (833 blocks) converts in well under a second. Under `NCX_CORPUS`, every Heidenhain file converts without a crash and `PLANE AXIAL`, `CP IPA`, `LN`, `M140` read into their words.

### P3-06 Fanuc compiler

Files in `src/Ncx.Compilers/Fanuc/`: `FanucCompiler`, `FanucProgramFrame` (`%`, `O` with the name, `N` per `[format]`, `program_end`, `O` subprograms after the end), `FanucMotion` (modal G on change, `G90` in the header or `G91` blocks per setting, `I J K` or `R`, sweeps split into turns, `G17`..`G19`, `G41`/`G42` with `D`), `FanucToolWords` (`T`/`M6` per `[tool_change]`, `G43 H`, `D`, `M3 S` in one block per the `S` binding rule), `FanucFrames` (`G54`.., `G52`, `G53`, `G28`/`G30` for `HOME` with the system's form, `G50`/`G92` for `SETPOS`, `G68`, `G51.1`, `G68.2`/`G53.1`, `G43.4`/`G43.5`, `G12.1`, `G7.1`, `G5.1`), `FanucCycles` (`G81`.. with `R` and `Z` from the absolute words, `G98`/`G99` from `CYCLE_RETRACT`, `G80`, lathe cycles, `AXIS=X` as `G87`..), `FanucFlow` (`M98 P L`, `GOTO`, `IF [ ] GOTO`, `WHILE` never reconstructed, `#` variables through `[variables] map`), `FanucSync` (wait marks from `[sync]`, the path form). The G-code system of the target (`U W`, `G98`/`G99` in system A).

Cite: controllers `fanuc.md` 10 (every rule); controller-mapping (the Fanuc column); machine-config 2, 3, 5; D28, D31, D49, D60.

Tests first: one test per rule of `fanuc.md` 10; the acceptance: `2.5D_FRAESEN.ncx` compiles to a program equivalent to `2.5D_FRAESEN.fanuc.nc`; the round trips Fanuc to NCX to Fanuc for `2.5D_FRAESEN`, `BOHREN`, `3D_FRAESEN` reproduce the sources under the comparison rules (M6).

### P3-07 Acceptance project and corpus runner

Files: `tests/Ncx.Acceptance/RoundTrips.cs` (the matrix: for `2.5D_FRAESEN`, convert from both sources and compare with the `.ncx`, compile to both and compare with the sources; for `BOHREN` and `3D_FRAESEN`, convert from both sources and compare the two results with each other and with the frozen expected file, compile back to both; the five `.ncx` examples format clean (no machine file, D91) and check clean without and with their machine files; the Nakamura pair converts and checks as single files, the job is phase 6), `NcComparer` (the comparison rules below, a unified diff on failure), `src/Ncx.Cli/Commands/BatchCommand.cs` (`ncx convert --batch <folder> --machine <name> --report <file>`: every file of the folder, never stops on an error, a summary per file with blocks, `RAW` blocks per word, diagnostics per code, and totals), `tests/corpus-reports/README.md` (what the maintainer runs and commits: the report text, never the programs). `docs/reading-the-code.md` extended to `convert` and `compile`. Code-guidelines 8 gets the comparison rules.

Comparison rules (from P3-07, made precise here): two NC programs are equivalent when, after removing block numbers, comments and blank lines, trimming whitespace, and formatting every number of both sides with the target machine's `[format] decimals` (so `70.` and `70.0` and `70` are equal, and `-.534` equals `-0.534`), the remaining lines are equal in order; the header block of the source may carry modal words in another order than the compiled one, so a header line is compared as a set of words; a `TOOL CALL` or `T M6` line is compared as a set of words. Everything else, including the order of words inside a motion block, must match, which is what makes the test say something.

Cite: architecture 11; code-guidelines 8; controllers `sample-corpus.md` 3; D70.

Tests first: the comparer on hand-written pairs (equal, block numbers differ, `70.` vs `70.0`, a real difference shows a diff); the batch command on `docs/spec/examples/sources/` produces the report.

Acceptance: the project is green; the maintainer runs the batch over the corpus and commits the report under `tests/corpus-reports/` with zero crashes for the Fanuc and Heidenhain families.

## Risks and open ends

- The two readers producing the same `BOHREN.ncx` is the first place where the language's cycle model meets two real controllers; expect to discover a rule the mapping does not state (the `SAFE` question above is one). Each such rule becomes a sentence in controller-mapping 5 and a test, and a decision when it changes a word's meaning.
- The comparison rules are deliberately strict on word order inside motion blocks; the first Fanuc round trip of `2.5D_FRAESEN` will show whether the source's own habits (`G43 Z2. H1`, `G0 G17 X50.4`) can be reproduced from the machine file's settings or need a `[format]` option (`G17` on the first motion, `G43` in the first Z block). Add the option, not a special case.
- `3D_FRAESEN` and the megabyte corpus files are the performance gate for the whole chain (reader, writer, parser, VM with snapshots, compiler); measure at P3-06 and fix before phase 4.
- The corpus is customer property; the batch runner writes only counts and codes into the report, never source lines.

## Exit checklist

- `phases.md` row 3: `2.5D_FRAESEN` from both sources to the same canonical text and back to both without loss; `BOHREN` and `3D_FRAESEN` round-trip; the corpus never crashes the readers (report committed).
- `ncx convert`, `ncx compile`, `ncx convert --batch` in the CLI table; `reading-the-code.md` covers convert and compile.
- New rules found in the sources are sentences in controller-mapping with tests; decisions taken have rows.
- P3-01 to P3-07 in `done/`; F26 (the Readers and Compilers part), F27 (the P3 rows) marked resolved.

## Log

(filled when the phase starts and when it closes)
