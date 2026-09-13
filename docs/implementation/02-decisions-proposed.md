# Proposed decisions D90 to D106

Status: 2026-09-11, drafts; copied into `../decisions/rationale.md` the same day and answered there (fourth round): all seventeen as recommended, D99, D100 and D105 clarified the same day, D99 to D103 and D106 on 2026-09-12 and 2026-09-13; D107 (the machine model in `Ncx.Core`) came from the final review. The copy here is kept for the implementation notes and is not updated with the answers. Each entry is written in the format of `../decisions/rationale.md` (question, recommendation, where it lands) so that it can be copied there unchanged when it is put to the maintainer. The `ANSWER` line stays empty here; the answer goes into `rationale.md`, the row into `decisions.md`, and the specification sections, the examples and the tests change in the same commit as the code that needs the decision. The numbers are provisional: they are taken in the order below when the batches are asked, and a number is never reused.

Two batches, by the phase gate they block (`20-schedule.md`): batch 1, D90 to D98, before P0-03; batch 2, D99 to D106, before P1-01. D97 and D98 are not language questions and are taken with a log line if the maintainer does not answer them by then; the others wait.

The recommendation is what the code implements when the answer is "as recommended". Where a real alternative exists it is named, so that the answer can pick it.

---

## Batch 1: before phase 0 continues past the model

#### D90 Canonical order as an explicit rank table
Question: section 5 rule 6 of the language orders words by buckets, but nineteen words have no bucket, several buckets have no inner order, and twelve blocks of the five examples (every header block; `TOOL=1 RPM=1592 OFFSET:LEN=1 OFFSET:RAD=1`; `TOOL=12 OFFSET=12 FEED_MODE=PER_REV`; the drilling cycle with `CYCLE_F` before `CYCLE_RETRACT`) do not follow the rule as written, although `ncx format` must reproduce them byte for byte (P0-06). Does every word get a rank in the catalog, and do the examples follow the rank, or does the rule follow the examples?
Recommendation: every word gets a rank in the catalog, the catalog is the rule, and a test writes the rank table into `docs/spec/generated/word-catalog.md` next to the prose of rule 6. The bucket order of rule 6 stays as it is; the buckets get this inner order:

1. `SKIP` (it stands first, as the slash does on every control), then the structural words `FILE`, `NCX`, `PROGRAM`, `SUB`, `NAME`, `NUMBER`, `CHANNEL`.
2. The verb.
3. Axis words: `X Y Z A B C`, then machine axes in alphabetical order (letter, then number: `C2`, `Z2`, `W`); all absolute words in that order, then all incremental words in the same order.
4. `TX TY TZ`, `NX NY NZ`.
5. `CENTER:X`, `CENTER:Y`, `CENTER:Z`, then the incremental forms.
6. `R` or `ANGLE`.
7. `F`.
8. `FEED_MODE`.
9. Tool words: `PRELOAD`, `TOOL`, `OFFSET`, `OFFSET:LEN`, `OFFSET:RAD`, `COMP`.
10. Spindle words: `SPINDLE`, `RPM`, `CSS`, `VC`, `RPM_MAX`, `SPINDLE_MODE`, `ORIENT`, `SPINDLE_SYNC`, `PHASE`.
11. `COOLANT`, `FUNC`, `MFUNC`.
12. Frame and state words: `UNITS`, `WORKPLANE`, `ORIGIN`, `DIAMETER`, `WORKPIECE`, `FRAME`, `SHIFT=RESET`, `ROTATE`, `MIRROR`, `TILT=RESET`, `TILT_AXIS=RESET`, `MOVE`, `ROT`, `POINT`, `CYLINDER`, `POLAR`, `TCPM`, `ROTARY_PATH`, `ROTARY_FEED`, `TOLERANCE`, `TOLERANCE:ROTARY`, `TOLERANCE_MODE`.
13. Cycle words: `CYCLE`, `AXIS`, `SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `CYCLE_RETRACT`, `PECK`, `CYCLE_F`, `CYCLE_DWELL`, `PITCH`, `CONTOUR`, then native parameters of a `CYCLE:CONTROLLER` block in source order (D94).
14. Channel words: `SYNC`, `WITH`, `START_CHANNEL`, `WAIT_CHANNEL`.
15. Variable and flow words: `VAR`, `LABEL`, `JUMP`, `CALL`, `ARG`, `TIMES`, `REPEAT`, `RETURN`, `IF`.
16. `STOP`, `DWELL`, `RAW`.
17. `COMMENT` or `SECTION`.

Words of one key with different addresses sort by the address text (`COOLANT:AIR` before `COOLANT:THROUGH`, `VAR:Q1` before `VAR:Q3`). `CONTOUR` gets a row in 4.7 (cycle name reference, D65). `DIAMETER` keeps its row in 4.2 and 4.11 refers to it. The EBNF line for `sub` becomes `"NAME=" ( ident | integer | string )`. The twelve example blocks and the two snippets in language 6 (lines 366 and 373) are rewritten by running `ncx format` once it exists, reviewed by eye, and committed with this decision; the examples keep their meaning and lose nothing but the order. Alternative: move bucket 12 in front of `F` so that headers keep reading `UNITS=MM WORKPLANE=XY ...`; not recommended, it changes the rule's text and the examples the same way for the same information.
Where: language 5 rule 6, 3 (EBNF), 4.7 (`CONTOUR`), 4.11, 6; the five examples; P0-03, P0-06.

ANSWER:

---

#### D91 `ncx format` is the parser and the writer, nothing else
Question: D18 and VM 1 say `format` runs the VM in STATIC mode, VM 1 also says it runs without the expander, the CLI table of the architecture says it is parser and writer, and D27 says `format` writes the number for a bare `TOOL`, which needs the VM's tool state and, with D93, a machine file that `format` does not take. Which is it?
Recommendation: `format` is parser and writer only, takes no machine file, and reproduces a program's meaning without resolving it: a bare `TOOL` stays bare. Readers never emit a bare `TOOL`, and the compilers write the number from the VM. D18 loses `format` from its STATIC list, D27 loses the sentence about `format`, note 2 of `INCREMENTAL_SUB` is rewritten. `check` remains the command that resolves.
Where: D18, D27; language 4.4; VM 1; architecture 1, 10; `INCREMENTAL_SUB.ncx` note 2.

ANSWER:

---

#### D92 Comments, comment-only lines and blank lines are kept
Question: the parser "ignores" comments (language 3) and design rule 8 keeps them; every example has comment-only lines before `FILE=BEGIN` and after `FILE=END` ("Nothing follows it"), blank lines between sections, and trailing comments aligned at column 57. The model has no place for any of it. What does `format` keep, and where?
Recommendation: a comment-only line and a blank line are not blocks; they are trivia, kept in the program in their place and written back as read, so they may stand anywhere, including before `FILE=BEGIN` and after `FILE=END` (4.1 becomes "no block follows it"). A block keeps its trailing comment text. The canonical layout of a block is its words, then, when it has a comment, spaces up to column 57 (the semicolon in column 57) or three spaces when the words end at column 54 or later, then the comment. Two or more blank lines in a row are kept as they are (nothing is normalized but the comment column). The one misaligned line of the examples (`PATTERN_LOOP.ncx:15`) is realigned by `format` in the D90 commit. The model gets `Block.Comment` and `NcxProgram.Trivia` (line number, text); the readers pass source comments through as trivia or as `COMMENT` words per controller-mapping 1.
Where: language 3 (rows Block and Comment), 4.1 (`FILE=END`); architecture 4; P0-02, P0-04, P0-06.

ANSWER:

---

#### D93 Machine axis words in a file parsed without a machine
Question: `MILLTURN_TRANSFER` writes `Z2=`, and `format` has no machine file to resolve it against; the catalog says machine axes are "resolved later", the VM says an unresolved axis is an ERROR, the architecture says an unknown key is an ERROR and the block becomes RAW text. How does the parser treat an axis-looking key it does not know, and how does the canonical order sort it?
Recommendation: a key of the form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, that is not a catalog word is accepted by the parser as a machine axis word with a number or expression value, in a block whose verb takes axis words (rule 2 of language 5 applies unchanged). The VM resolves it through `[[axis]]` at `check`, `compile` and `analyze`, and VM 3.8 rule 3 stays: no entry, ERROR (under D103, a WARNING when no machine file was given at all). In the canonical order machine axes sort alphabetically after `X Y Z A B C`; rule 6 loses "as listed in the machine configuration", so the canonical text of a program does not depend on a machine.
Where: language 3, 4.3, 5 rule 6; VM 3.8; architecture 4.1; P0-03, P0-04.

ANSWER:

---

#### D94 Native cycle parameters
Question: language 4.7.1 allows `CYCLE:HEIDENHAIN=251 Q215=0 Q218=60`; `Q215` is an unknown key, which VM 3 step 1 makes an ERROR. How are native parameters carried?
Recommendation: in a block that carries `CYCLE:<controller>=n`, every key the catalog does not know is a native parameter with a number or expression value, kept in source order and ranked after the catalog's cycle words. The parser accepts it only in such a block; the VM records the parameters unresolved; a compiler for another controller family reports the block as an ERROR exactly as it does for `RAW`.
Where: language 4.7.1; VM 3 step 1, 5; P0-03, P0-04.

ANSWER:

---

#### D95 Pseudo-words
Question: `@SAVE=SPINDLE:MAIN` and `@RESTORE=SPINDLE:MAIN` cannot be lexed: `@` is not a key character and `SPINDLE:MAIN` is no value type; yet plugins insert them as text, the expander parses that text, and a pseudo-word in a user file must be reported as such. What is their form?
Recommendation: the lexer accepts a key starting with `@` only when the parser runs with the option the expander uses for generated text; its value is a state key, `KEY[:ADDR]`, a new internal value type that names a state variable of the channel. In a user file a word starting with `@` is the ERROR "pseudo-word in a user file"; it is recognized by the lexer, not rejected as garbage. Language 3 gets a footnote; VM 3.10 gets the value form.
Where: language 3; VM 3.10; architecture 4 (`StateKeyValue`); P0-03, P0-04, P1-06.

ANSWER:

---

#### D96 `CYLINDER` carries its radius as the value
Question: `CYLINDER=ON R=30` reuses the arc radius key `R`, which rule 4 of language 5 (a key once per block) and rule 6 (ranked with the motion words) both assume is the arc word; D87 gave the same clash on `RETRACT` a prefix. Rename?
Recommendation: `CYLINDER=30` switches the transformation on with the reference radius 30, `CYLINDER=OFF` switches it off; there is no `ON` form. The templates keep `{r}`.
Where: language 4.2; machine-config 5 (`[transform]` comment); P0-03.

ANSWER:

---

#### D97 Exit codes
Question: four statements exist (architecture 10: 0 without errors, 1 with errors, 2 for usage; P1-07: 0 clean, 1 WARNING under `--strict`, 2 ERROR; P0-06: 1 on ERROR). One table?
Recommendation: for every command, 0 when the run produced no ERROR, 1 when it produced at least one ERROR or, under `--strict`, at least one WARNING, 2 for a usage error, an unreadable input, or a missing machine file where one is required. `format --check` exits 1 when the output differs.
Where: architecture 10; P0-06; P1-07.

ANSWER:

---

#### D98 Diagnostic codes
Question: code-guidelines 6 shows `VM042`, P0-02 shows `NCX0012`; P0-02 adds a severity INFO; generated blocks need the originating line, which `Diagnostic` lacks. One scheme?
Recommendation: a code is an area prefix and three digits: `PAR` (lexer, parser, catalog), `VM`, `CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI`; one `DiagnosticCodes` class per project; rendered as `file(line): ERROR VM042: message`. Severities ERROR, WARNING and INFO, where INFO carries notes that are neither (`plugin MyShopRules: inserted 2 blocks at line 12`). `Diagnostic` gets `OriginLine` for diagnostics on generated blocks, rendered as `file(line, from 12)`.
Where: code-guidelines 6; architecture 4; P0-02.

ANSWER:

---

## Batch 2: before the virtual machine

#### D99 The STATIC entry state of a subprogram
Question: VM 1 says a `SUB` starts "from the initial state (units and workplane kept from the file's first program)", VM 3.9 "from a reset modal state" with feed, tool and cycle validation suppressed. Which state of the first program, and which validations are suppressed? As written, the subprogram of `INCREMENTAL_SUB` (`LINE IX=30 F=800` from an unknown position) is an ERROR and the example cannot check clean.
Recommendation: a `SUB` section in STATIC mode starts with units, workplane, diameter and feed mode as they stand at the first verb of the file's first program, and everything else at its initial value; the position is unknown. Inside a `SUB` section these validations are suppressed because the entry state belongs to the caller: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, incremental word from an unknown position (the position becomes unknown, no ERROR), and "spindle OFF before a `LINE`". Everything structural stays.
Where: VM 1, 3.1, 3.9, 5; P1-03, P1-04.

ANSWER:

---

#### D100 `HOME` without a reference point in the configuration
Question: VM 5 makes `HOME` with an axis that has no reference point an ERROR; no example machine file has one, and `INCREMENTAL_SUB`, `POLAR_FACE` and the expander's own `pre = ["HOME Z"]` case fail. ERROR at check time, or later?
Recommendation: at check time `HOME` on an axis without a reference point is a WARNING, once per run and axis ("no reference point for C in the configuration"), and the axis becomes unknown in every frame afterwards (as after a machine-frame move, D35). The ERROR moves to the compiler that has to write coordinates for it (Heidenhain `L ... M91`); a compiler with a `[home]` template does not need them. The four example machine files get `home` on every axis, and `limits`, `rapid`, `max_feed`, `acceleration`, `[dynamics]`, `rpm_min`, `rpm_max`, `accel_time` with plausible values marked "not verified on the machine", so that the runtime and limit analytics have something to work with.
Where: VM 3 step 5, 5; machine-config 4; the machine files; P1-03, P2-04.

ANSWER:

---

#### D101 `SETPOS` after `HOME`
Question: `POLAR_FACE` writes `HOME C` then `SETPOS C=0`; after `HOME` the axis is known in the MACHINE frame and unknown in the workpiece frame (D35), and `SETPOS` on an unknown position is an ERROR (VM 5). The idiom is the reason the example exists. Which position does `SETPOS` need?
Recommendation: `SETPOS` needs the axis known in some frame. When it is known in the MACHINE frame only, the setpos shift is recorded against the machine position (newSetposShift = machinePos minus declared) and the axis becomes known in the workpiece frame with the declared value; the ERROR remains for an axis that is unknown in every frame.
Where: VM 3.4, 5; P1-02.

ANSWER:

---

#### D102 The working plane under `POLAR=ON`
Question: VM 3.2 allows arcs "only in the working plane" with the start "known in the plane"; `POLAR_FACE` has `WORKPLANE=ZX` and arcs in X and C while VM 3.1 marks the Cartesian position unknown. The example's numbers show that X is a diameter under `POLAR` as D60 says (the hexagon closes at radius 17.32 only when X is halved). What is the arc plane?
Recommendation: while `POLAR=ON`, the working plane for `ARC` and for `COMP` is the face plane whose axes are the X word (a diameter under `DIAMETER=ON`, D60) and the C word (a Cartesian length in the active units); the position is known in that polar frame and unknown in the workpiece frame until `POLAR=OFF`. `R` and `CENTER` keep their meaning in that plane. `CYLINDER=ON` is treated the same way with the C word as a length on the circumference.
Where: VM 3.1, 3.2; P1-03.

ANSWER:

---

#### D103 Running without a machine file
Question: `check`, `trace` and `annotate` take `--machine`, but VM 3.8, D36 and `HOME` all read the configuration, P1-01 speaks of "a default configuration object" no document defines, and in phase 1 no configuration exists. Two examples (`MILLTURN_TRANSFER`, `POLAR_FACE`) address roles and functions no default can know. What happens without a machine file?
Recommendation: `check`, `trace`, `annotate` and `analyze` run without `--machine` against a built-in default machine: one work spindle `MAIN` with the C axis, one tool holder `TOOL` with the tool spindle `TOOL`, axes `X Y Z A B C` without limits and without reference points, coolant channel `STANDARD`, no named functions, the default arc tolerance of D36, units default `MM`. A role, function or machine axis the default machine does not have is then a WARNING "not checked: no machine file" instead of an ERROR, once per name, and the word is executed against a resource created on the spot. With a machine file VM 3.8 applies in full. The phase 1 acceptance "the five examples check clean" therefore means: no ERROR for the five files without a machine file; phase 2 adds: no ERROR for the five files with their machine files. `compile` and `convert` keep requiring a machine file (D77).
Where: VM 3.8; architecture 10; P1-01, P1-04, P1-07, P2-04.

ANSWER:

---

#### D104 A machine file for `MILLTURN_TRANSFER`
Question: the example, machine-config 8 and 10 name `millturn1.toml`, which does not exist; P5-02 compiles the example for `dmg-ctx-840d.toml`, which has other axis names (`Z3`, `C3`) and a tool change template that needs a tool name. Which machine does the example belong to?
Recommendation: write `millturn1.toml` as the fifth example machine, a generic SINUMERIK 840D sl mill-turn without builder cycles: roles `MAIN` (`S1`, axis `C`), `SUB` (`S2`, axis `C2`), `TOOL` (`S3`), `TURRET1` (`H1`); axes `X Y Z C Z2 C2`; functions `SUB_CHUCK`, `MAIN_CHUCK`; numeric tools with `T{tool} D{offset}`; `COUPON`/`COUPOF` for the synchronization; `[workpiece]` with `SUB_frame = "datum"`. P5-02 compiles the example for it; `dmg-ctx-840d.toml` stays the documentation of the structure programming (D68).
Where: machine-config 11; `spec/examples/machines/`; P2-04, P5-02.

ANSWER:

---

#### D105 Function values in the machine file
Question: machine-config 5 writes M codes as integers (`ON = 8`), all four example files as strings (`ON = "M8"`). Which?
Recommendation: strings, as the files have them; they are templates like every other value (`"M03 P11"`, `"L707({angle})"`). A bare integer is accepted and means `M` followed by the number, so a hand-written file may be terse.
Where: machine-config 5; P2-01.

ANSWER:

---

#### D106 Where the plugin interfaces live
Question: architecture 9 puts the four interfaces in `Ncx.Plugins`, code-guidelines 4 puts `IVmListener` and `IProgramRewriter` in `Ncx.Core`, P1-05 has Core "re-export" them, and their callers (the expander in Core, the readers, the compilers) would depend on Plugins, against the dependency direction of architecture 3. A plugin "references Ncx.Plugins, no other packages" but its methods take `Block`, `VmEvent` and `ChannelState` from Core. Where does each interface live, and what does a plugin reference?
Recommendation: an interface lives with its caller: `IProgramRewriter` and `IVmListener` in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers`, all public, together with the public model and event types they take. `Ncx.Plugins` holds what only plugins need: the loader, `RewriteResult`, `RewriteContext` (machine name, channel, line, and the plugin's own settings dictionary of D80), the diagnostics helpers; it references Core, Readers and Compilers, so that a plugin project references `Ncx.Plugins` alone and gets the rest transitively. The dependency graph of architecture 3 gains Plugins to Readers and Plugins to Compilers. "Loads into its own `AssemblyLoadContext` and sees only `Ncx.Plugins`" becomes: the context shares every `Ncx.*` assembly with the host, so types are identical, and isolates everything else a plugin brings. "No access to the VM state" (D61) means no mutation: listeners receive read-only snapshots. `Block.Has(key)` and `Block.Has(key, addr, value)` both exist.
Where: architecture 3, 5.3, 9; code-guidelines 4, 5, 11; P0-01, P1-05, P1-06, P3-01, P3-03, P7-01.

ANSWER:

---

## Document fixes that need no decision

These change the outline, a task text or a comment in a machine file, never the meaning of a word; they are made in the task named, with a line in the task's log, and they are listed here so that they are not forgotten.

| Finding | Fix | In task |
|---|---|---|
| F8 | `ValueKinds` as flags in `WordDefinition`; the EBNF line for `sub` (part of the D90 commit) | P0-03 |
| F18 | Architecture 5.3 gets one record per VM 7 row (`FileEvent`, `ProgramEvent`, `SubEvent`, `SectionEvent`, `PreloadEvent`, `VarChangeEvent`); `MotionEvent` gets sweep, tool vector, surface normal, compensation, frame; VM 7 gets `DWELL`, `STOP` and `FUNCTION` (for `FUNC`, `MFUNC`, `COOLANT`) events; architecture 9 cites the VM 7 names; `TOOL_POSE` is listed as the kinematics module's event | P1-05 |
| F19 | The `ChannelState` diagram follows VM 2: `LastHolder`, `WaitingAt`, `Finished`, `Programs`; `ToleranceState` defined (value, rotary, mode); shifts and angles as `decimal` per axis, `Vec3` only in `Ncx.Core.Geometry` | P1-01 |
| F20 | `HolderState.SpindleTool` is a `ToolRef` (number or name), `Preloaded` a `ToolRef?`; the code-guidelines sample is updated to match | P1-01 |
| F21 | VM 2.2 lists all ten verbs; the architecture 5.1 flowchart routes `TILT_AXIS` and `RETRACT`; P1-02 says "frame words" instead of "frame verbs" for `ORIGIN` | P1-02 |
| F23 | Machine-config gains `[raw]`, `channel` on `[[resource]]`, and the function states the files use; `nakamura-ntjx.toml` declares axis `C2` | P2-01, P2-04 |
| F24 | One name, `<name>.ncxjob.toml`; `[job] machine` and `JobManifest.Machine`; `[shared] axes`; the CLI table lists `--job` on `compile`, `check`, `analyze` | P2-01, P6-01 |
| F27 | `tasks/README.md` dependency rows: P2-04 also on P1-07; P3-04 also on P3-02; P7-01 also on P3-01; `phases.md` last paragraph rewritten per `20-schedule.md` | P2-01 (first task after phase 0) |
| F28 | Architecture 10 is the one CLI table; every task that adds an option or command adds its row there in the same commit | P0-06, P1-07, P3-07, P6-01 |
| F29 | Language 4.6 and VM 2.5: the coolant channel `STANDARD` is the default channel that a bare `COOLANT` addresses | P1-02 |
| F30 | VM 5: "spindle OFF before a `LINE`" means the spindle of the current tool holder (`[[resource]] spindle`), the default spindle when the holder has none | P1-04 |
