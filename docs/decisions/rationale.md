# Rationale: the questions behind the decisions

Status: 2026-09-13, five rounds answered; the fourth round (2026-09-11) got follow-up clarifications on 2026-09-12 and 2026-09-13, and the fifth (D107) came from its final review. Nothing is open. The questions, recommendations and the maintainer's answers are kept below for the record; the settled decisions are in `decisions.md`.

---

## Still open

None.

---

## Answered (fifth round, 2026-09-13; settled in `decisions.md`)

One question from the final review of the fourth round (finding F31 in `../implementation/01-findings.md`), answered in the conversation with the implementation plan.

#### D107 Where the machine model lives
Question: the virtual machine in `Ncx.Core` holds a `MachineConfig` and the expander reads its expansion rules, but the architecture draws `MachineConfig` in `Ncx.Config`, which depends on `Ncx.Core`. That is a dependency cycle, already in the 1.0 outline. How is it cut?
Recommendation: the typed records of the machine file (`MachineConfig` and its tables) live in `Ncx.Core`; `Ncx.Config` keeps the Tomlyn loader, the templates, the cycle catalog loading, the job manifest and the `DefaultMachine`. No interface, no duplication, and `Ncx.Core` still takes no package. Alternative: an `IMachineModel` interface in `Ncx.Core`, implemented by `MachineConfig` in `Ncx.Config`.
Where: architecture 3, 6, 13; P2-01; implementation 12.

ANSWER:
as recommended (chosen 2026-09-13 in the conversation)
Applied 2026-09-13: the records keep template values as text and `Ncx.Config` parses them into `Template` objects (a `TemplateSet` per machine, which also does `FindFunctionByCode`); the job manifest records follow the same cut, because the job scheduler in `Ncx.Core` reads them.

---

## Answered (fourth round, 2026-09-11; settled in `decisions.md`)

Seventeen questions found by the implementation plan while reading the 1.0 set against the example programs before the first code; the finding behind each is in `../implementation/01-findings.md`.

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
As recommended
Applied 2026-09-11: the ten example blocks and the snippets of language 6 were rewritten into the rank order by hand when the decision was applied, checked against the rank table, and the comment of `PATTERN_LOOP.ncx` line 15 was realigned (D92); P0-06 checks that `ncx format` reproduces them unchanged. The twelve blocks of the question are the ten blocks of the examples and two snippets of language 6; item 3 sorts the machine axes as `C2`, `W`, `Z2`.

---

#### D91 `ncx format` is the parser and the writer, nothing else
Question: D18 and VM 1 say `format` runs the VM in STATIC mode, VM 1 also says it runs without the expander, the CLI table of the architecture says it is parser and writer, and D27 says `format` writes the number for a bare `TOOL`, which needs the VM's tool state and, with D93, a machine file that `format` does not take. Which is it?
Recommendation: `format` is parser and writer only, takes no machine file, and reproduces a program's meaning without resolving it: a bare `TOOL` stays bare. Readers never emit a bare `TOOL`, and the compilers write the number from the VM. D18 loses `format` from its STATIC list, D27 loses the sentence about `format`, note 2 of `INCREMENTAL_SUB` is rewritten. `check` remains the command that resolves.
Where: D18, D27; language 4.4; VM 1; architecture 1, 10; `INCREMENTAL_SUB.ncx` note 2.

ANSWER:
as recommended

---

#### D92 Comments, comment-only lines and blank lines are kept
Question: the parser "ignores" comments (language 3) and design rule 8 keeps them; every example has comment-only lines before `FILE=BEGIN` and after `FILE=END` ("Nothing follows it"), blank lines between sections, and trailing comments aligned at column 57. The model has no place for any of it. What does `format` keep, and where?
Recommendation: a comment-only line and a blank line are not blocks; they are trivia, kept in the program in their place and written back as read, so they may stand anywhere, including before `FILE=BEGIN` and after `FILE=END` (4.1 becomes "no block follows it"). A block keeps its trailing comment text. The canonical layout of a block is its words, then, when it has a comment, spaces up to column 57 (the semicolon in column 57) or three spaces when the words end at column 54 or later, then the comment. Two or more blank lines in a row are kept as they are (nothing is normalized but the comment column). The one misaligned line of the examples (`PATTERN_LOOP.ncx:15`) is realigned by `format` in the D90 commit. The model gets `Block.Comment` and `NcxProgram.Trivia` (line number, text); the readers pass source comments through as trivia or as `COMMENT` words per controller-mapping 1.
Where: language 3 (rows Block and Comment), 4.1 (`FILE=END`); architecture 4; P0-02, P0-04, P0-06.

ANSWER:
as recommended

---

#### D93 Machine axis words in a file parsed without a machine
Question: `MILLTURN_TRANSFER` writes `Z2=`, and `format` has no machine file to resolve it against; the catalog says machine axes are "resolved later", the VM says an unresolved axis is an ERROR, the architecture says an unknown key is an ERROR and the block becomes RAW text. How does the parser treat an axis-looking key it does not know, and how does the canonical order sort it?
Recommendation: a key of the form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, that is not a catalog word is accepted by the parser as a machine axis word with a number or expression value, in a block whose verb takes axis words (rule 2 of language 5 applies unchanged). The VM resolves it through `[[axis]]` at `check`, `compile` and `analyze`, and VM 3.8 rule 3 stays: no entry, ERROR (under D103, a WARNING when no machine file was given at all). In the canonical order machine axes sort alphabetically after `X Y Z A B C`; rule 6 loses "as listed in the machine configuration", so the canonical text of a program does not depend on a machine.
Where: language 3, 4.3, 5 rule 6; VM 3.8; architecture 4.1; P0-03, P0-04.

ANSWER:
as recommended

---

#### D94 Native cycle parameters
Question: language 4.7.1 allows `CYCLE:HEIDENHAIN=251 Q215=0 Q218=60`; `Q215` is an unknown key, which VM 3 step 1 makes an ERROR. How are native parameters carried?
Recommendation: in a block that carries `CYCLE:<controller>=n`, every key the catalog does not know is a native parameter with a number or expression value, kept in source order and ranked after the catalog's cycle words. The parser accepts it only in such a block; the VM records the parameters unresolved; a compiler for another controller family reports the block as an ERROR exactly as it does for `RAW`.
Where: language 4.7.1; VM 3 step 1, 5; P0-03, P0-04.

ANSWER:
as recommended

---

#### D95 Pseudo-words
Question: `@SAVE=SPINDLE:MAIN` and `@RESTORE=SPINDLE:MAIN` cannot be lexed: `@` is not a key character and `SPINDLE:MAIN` is no value type; yet plugins insert them as text, the expander parses that text, and a pseudo-word in a user file must be reported as such. What is their form?
Recommendation: the lexer accepts a key starting with `@` only when the parser runs with the option the expander uses for generated text; its value is a state key, `KEY[:ADDR]`, a new internal value type that names a state variable of the channel. In a user file a word starting with `@` is the ERROR "pseudo-word in a user file"; it is recognized by the lexer, not rejected as garbage. Language 3 gets a footnote; VM 3.10 gets the value form.
Where: language 3; VM 3.10; architecture 4 (`StateKeyValue`); P0-03, P0-04, P1-06.

ANSWER:
as recommended

---

#### D96 `CYLINDER` carries its radius as the value
Question: `CYLINDER=ON R=30` reuses the arc radius key `R`, which rule 4 of language 5 (a key once per block) and rule 6 (ranked with the motion words) both assume is the arc word; D87 gave the same clash on `RETRACT` a prefix. Rename?
Recommendation: `CYLINDER=30` switches the transformation on with the reference radius 30, `CYLINDER=OFF` switches it off; there is no `ON` form. The templates keep `{r}`.
Where: language 4.2; machine-config 5 (`[transform]` comment); P0-03.

ANSWER: as recommended

---

#### D97 Exit codes
Question: four statements exist (architecture 10: 0 without errors, 1 with errors, 2 for usage; P1-07: 0 clean, 1 WARNING under `--strict`, 2 ERROR; P0-06: 1 on ERROR). One table?
Recommendation: for every command, 0 when the run produced no ERROR, 1 when it produced at least one ERROR or, under `--strict`, at least one WARNING, 2 for a usage error, an unreadable input, or a missing machine file where one is required. `format --check` exits 1 when the output differs.
Where: architecture 10; P0-06; P1-07.

ANSWER: as recommended

---

#### D98 Diagnostic codes
Question: code-guidelines 6 shows `VM042`, P0-02 shows `NCX0012`; P0-02 adds a severity INFO; generated blocks need the originating line, which `Diagnostic` lacks. One scheme?
Recommendation: a code is an area prefix and three digits: `PAR` (lexer, parser, catalog), `VM`, `CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI`; one `DiagnosticCodes` class per project; rendered as `file(line): ERROR VM042: message`. Severities ERROR, WARNING and INFO, where INFO carries notes that are neither (`plugin MyShopRules: inserted 2 blocks at line 12`). `Diagnostic` gets `OriginLine` for diagnostics on generated blocks, rendered as `file(line, from 12)`.
Where: code-guidelines 6; architecture 4; P0-02.

ANSWER:
as recommended

---

#### D99 The STATIC entry state of a subprogram
Question: VM 1 says a `SUB` starts "from the initial state (units and workplane kept from the file's first program)", VM 3.9 "from a reset modal state" with feed, tool and cycle validation suppressed. Which state of the first program, and which validations are suppressed? As written, the subprogram of `INCREMENTAL_SUB` (`LINE IX=30 F=800` from an unknown position) is an ERROR and the example cannot check clean.
Recommendation: a `SUB` section in STATIC mode starts with units, workplane, diameter and feed mode as they stand at the first verb of the file's first program, and everything else at its initial value; the position is unknown. Inside a `SUB` section these validations are suppressed because the entry state belongs to the caller: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, incremental word from an unknown position (the position becomes unknown, no ERROR), and "spindle OFF before a `LINE`". Everything structural stays.
Where: VM 1, 3.1, 3.9, 5; P1-03, P1-04.

ANSWER: as recommended. the current state of the machine is the state where the sub is called from (positions, spindle, active feedrates etc)
Clarified 2026-09-11: STATIC mode follows every CALL and walks the subprogram with the caller's state at that point; a subprogram nothing calls starts from the default entry state of the recommendation; compilers emit each SUB once.
Clarified 2026-09-13: a `CALL` with `TIMES=n` is walked n times in sequence; a `CALL` of an external program is not followed in STATIC mode (the call is recorded and the position becomes unknown); the STATIC walk keeps a call stack with the configured depth (default 8), and a deeper `CALL` is an ERROR; the compiler writes each `SUB` from an unknown target state, so every modal word stands at its first use inside it, and walks that would write different lines are an ERROR; `trace` writes one row per walk, `annotate` the values of the first walk.

---

#### D100 `HOME` without a reference point in the configuration
Question: VM 5 makes `HOME` with an axis that has no reference point an ERROR; no example machine file has one, and `INCREMENTAL_SUB`, `POLAR_FACE` and the expander's own `pre = ["HOME Z"]` case fail. ERROR at check time, or later?
Recommendation: at check time `HOME` on an axis without a reference point is a WARNING, once per run and axis ("no reference point for C in the configuration"), and the axis becomes unknown in every frame afterwards (as after a machine-frame move, D35). The ERROR moves to the compiler that has to write coordinates for it (Heidenhain `L ... M91`); a compiler with a `[home]` template does not need them. The four example machine files get `home` on every axis, and `limits`, `rapid`, `max_feed`, `acceleration`, `[dynamics]`, `rpm_min`, `rpm_max`, `accel_time` with plausible values marked "not verified on the machine", so that the runtime and limit analytics have something to work with.
Where: VM 3 step 5, 5; machine-config 4; the machine files; P1-03, P2-04.

ANSWER: A Home Position and min max values for each axis based on the machine frame / G53 / M91 etc.
We can add the following: HOME Position, Tool change position, Program end position (optional) etc.
Clarified 2026-09-11: home and limits per [[axis]] in machine coordinates; the named positions (tool_change, program_end, ...) live in a [positions] table and expansion rules reach them through {position:NAME}; no new NCX word; the WARNING/ERROR split as recommended.
Clarified 2026-09-13: at program start an axis with `home` is known in the MACHINE frame at that reference point and unknown in the workpiece frame (D35); an axis without `home` is unknown in every frame, as version 1.0 said (virtual machine 2.2, 3.4).

---

#### D101 `SETPOS` after `HOME`
Question: `POLAR_FACE` writes `HOME C` then `SETPOS C=0`; after `HOME` the axis is known in the MACHINE frame and unknown in the workpiece frame (D35), and `SETPOS` on an unknown position is an ERROR (VM 5). The idiom is the reason the example exists. Which position does `SETPOS` need?
Recommendation: `SETPOS` needs the axis known in some frame. When it is known in the MACHINE frame only, the setpos shift is recorded against the machine position (newSetposShift = machinePos minus declared) and the axis becomes known in the workpiece frame with the declared value; the ERROR remains for an axis that is unknown in every frame.
Where: VM 3.4, 5; P1-02.

ANSWER: as recommended
Clarified 2026-09-12: directly after a `HOME` of that axis that found no reference point in the configuration (D100), `SETPOS` is accepted as well: the axis becomes known in the workpiece frame with the declared value and its machine position stays unknown; the ERROR remains for every other axis unknown in every frame.

---

#### D102 The working plane under `POLAR=ON`
Question: VM 3.2 allows arcs "only in the working plane" with the start "known in the plane"; `POLAR_FACE` has `WORKPLANE=ZX` and arcs in X and C while VM 3.1 marks the Cartesian position unknown. The example's numbers show that X is a diameter under `POLAR` as D60 says (the hexagon closes at radius 17.32 only when X is halved). What is the arc plane?
Recommendation: while `POLAR=ON`, the working plane for `ARC` and for `COMP` is the face plane whose axes are the X word (a diameter under `DIAMETER=ON`, D60) and the C word (a Cartesian length in the active units); the position is known in that polar frame and unknown in the workpiece frame until `POLAR=OFF`. `R` and `CENTER` keep their meaning in that plane. `CYLINDER=ON` is treated the same way with the C word as a length on the circumference.
Where: VM 3.1, 3.2; P1-03.

ANSWER:
as recommended
Clarified 2026-09-13: `CYLINDER=ON` above reads as `CYLINDER=n` (D96). Under `CYLINDER=n` the plane is the cylinder axis (Z on a lathe) and the C word as a length on the circumference; X stays a workpiece coordinate. The arc direction is X then C looking against the tool axis onto the face under `POLAR` (the `G12.1` and `TRANSMIT` convention) and the cylinder axis then C on the developed surface under `CYLINDER` (`G7.1`, `TRACYL`), independent of `WORKPLANE`. The position is known in that plane from the first motion under the transformation.

---

#### D103 Running without a machine file
Question: `check`, `trace` and `annotate` take `--machine`, but VM 3.8, D36 and `HOME` all read the configuration, P1-01 speaks of "a default configuration object" no document defines, and in phase 1 no configuration exists. Two examples (`MILLTURN_TRANSFER`, `POLAR_FACE`) address roles and functions no default can know. What happens without a machine file?
Recommendation: `check`, `trace`, `annotate` and `analyze` run without `--machine` against a built-in default machine: one work spindle `MAIN` with the C axis, one tool holder `TOOL` with the tool spindle `TOOL`, axes `X Y Z A B C` without limits and without reference points, coolant channel `STANDARD`, no named functions, the default arc tolerance of D36, units default `MM`. A role, function or machine axis the default machine does not have is then a WARNING "not checked: no machine file" instead of an ERROR, once per name, and the word is executed against a resource created on the spot. With a machine file VM 3.8 applies in full. The phase 1 acceptance "the five examples check clean" therefore means: no ERROR for the five files without a machine file; phase 2 adds: no ERROR for the five files with their machine files. `compile` and `convert` keep requiring a machine file (D77).
Where: VM 3.8; architecture 10; P1-01, P1-04, P1-07, P2-04.

ANSWER:
We create a default machine. 
Clarified 2026-09-13: a work spindle created on the spot gets a rotary axis of its own, so `C` resolves to it while it is the workpiece holder.

---

#### D104 A machine file for `MILLTURN_TRANSFER`
Question: the example, machine-config 8 and 10 name `millturn1.toml`, which does not exist; P5-02 compiles the example for `dmg-ctx-840d.toml`, which has other axis names (`Z3`, `C3`) and a tool change template that needs a tool name. Which machine does the example belong to?
Recommendation: write `millturn1.toml` as the fifth example machine, a generic SINUMERIK 840D sl mill-turn without builder cycles: roles `MAIN` (`S1`, axis `C`), `SUB` (`S2`, axis `C2`), `TOOL` (`S3`), `TURRET1` (`H1`); axes `X Y Z C Z2 C2`; functions `SUB_CHUCK`, `MAIN_CHUCK`; numeric tools with `T{tool} D{offset}`; `COUPON`/`COUPOF` for the synchronization; `[workpiece]` with `SUB_frame = "datum"`. P5-02 compiles the example for it; `dmg-ctx-840d.toml` stays the documentation of the structure programming (D68).
Where: machine-config 11; `spec/examples/machines/`; P2-04, P5-02.

ANSWER:
as recommended

---

#### D105 Function values in the machine file
Question: machine-config 5 writes M codes as integers (`ON = 8`), all four example files as strings (`ON = "M8"`). Which?
Recommendation: strings, as the files have them; they are templates like every other value (`"M03 P11"`, `"L707({angle})"`). A bare integer is accepted and means `M` followed by the number, so a hand-written file may be terse.
Where: machine-config 5; P2-01.

ANSWER:
Important: Sometimes things are mixed, M8 and M08, which is the same. So if the user writes ON=08, or ON=8, or ON=M8, or ON=M08, it has to be the same! but we define string as standard, as you recommended
Clarified 2026-09-11: the loader normalizes 8, 08, M8, M08 to M8 (and G01 to G1); the compiler always writes the normalized form; readers compare by number.

---

#### D106 Where the plugin interfaces live
Question: architecture 9 puts the four interfaces in `Ncx.Plugins`, code-guidelines 4 puts `IVmListener` and `IProgramRewriter` in `Ncx.Core`, P1-05 has Core "re-export" them, and their callers (the expander in Core, the readers, the compilers) would depend on Plugins, against the dependency direction of architecture 3. A plugin "references Ncx.Plugins, no other packages" but its methods take `Block`, `VmEvent` and `ChannelState` from Core. Where does each interface live, and what does a plugin reference?
Recommendation: an interface lives with its caller: `IProgramRewriter` and `IVmListener` in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers`, all public, together with the public model and event types they take. `Ncx.Plugins` holds what only plugins need: the loader, `RewriteResult`, `RewriteContext` (machine name, channel, line, and the plugin's own settings dictionary of D80), the diagnostics helpers; it references Core, Readers and Compilers, so that a plugin project references `Ncx.Plugins` alone and gets the rest transitively. The dependency graph of architecture 3 gains Plugins to Readers and Plugins to Compilers. "Loads into its own `AssemblyLoadContext` and sees only `Ncx.Plugins`" becomes: the context shares every `Ncx.*` assembly with the host, so types are identical, and isolates everything else a plugin brings. "No access to the VM state" (D61) means no mutation: listeners receive read-only snapshots. `Block.Has(key)` and `Block.Has(key, addr, value)` both exist.
Where: architecture 3, 5.3, 9; code-guidelines 4, 5, 11; P0-01, P1-05, P1-06, P3-01, P3-03, P7-01.

ANSWER:
as recommended
Clarified 2026-09-13: `RewriteResult` and the `RewriteContext` abstraction are the signature types of `IProgramRewriter.Rewrite` and live in `Ncx.Core` next to it (in `Ncx.Plugins`, as recommended, `Ncx.Core` would depend on `Ncx.Plugins`); `Ncx.Plugins` holds the concrete context that the loader fills from the D80 settings (machine name, channel, line, the plugin's settings dictionary, no VM), the loader and the diagnostics helpers.

---


## Answered (third round, 2026-09-12; settled in `decisions.md`)

#### D87 The cycle word `RETRACT` became `CYCLE_RETRACT`
Question: the second round made `RETRACT` a motion verb (D83: retract along the tool axis, `M140 MB MAX`). The drilling cycles already had a word `RETRACT=CLEARANCE|SAFE` (where the tool ends after each hole). One key with two meanings breaks design rule 1, and the D29 rule says that a cycle word that clashes with a motion word gets the `CYCLE_` prefix, as `CYCLE_F` and `CYCLE_DWELL` did. Draft 7 therefore renamed the cycle word to `CYCLE_RETRACT`; the other cycle-only words keep their short names as answered.
Recommendation: keep `CYCLE_RETRACT`; the alternative would be another name for the verb (`LIFT`), which reads worse for `M140 MB MAX`.
Where: language 4.3, 4.7; mapping 1, 5; `examples/POLAR_FACE.ncx`.

ANSWER:
as recommended

---

#### D88 An early program end is `JUMP=END`
Question: with `EXIT` gone, `PROGRAM=END` is the one executed end of a program and appears exactly once. Fanuc programs end early under a condition (`IF [#503 EQ 0] GOTO 9090` with `N9090 M30`) or from a section below `M30` (`N2 M2` in the Nakamura sample). Draft 7 reserves the label name `END`: `JUMP=END` (with `IF` when conditional) continues at the `PROGRAM=END` of the current program, and a `LABEL=END` is an ERROR. The alternative, allowing `PROGRAM=END` more than once, would make "the last block of the program" ambiguous for the compiler.
Recommendation: keep `JUMP=END`.
Where: language 4.1, 4.9, 4.13; virtual machine 3.6.

ANSWER:
as recommended

---

#### D89 Subprograms are sections of the file; jump-entered sections stay inside the program
Question: the answer to D48 said "SUB=BEGIN, SUB=END for sub-programs and labels". Draft 7 reads it as: a subprogram is `SUB=BEGIN NAME=100` ... `SUB=END` (with `RETURN` for an early return), it belongs to the file and stands next to the programs, never inside one, and every program of the file may call it (the compiler places it after the caller's `M30` on Fanuc, after the `M30` of every calling program on Heidenhain, as an `SPF` unit on Siemens). Code that a source keeps below its `M30` and enters by a jump is not a subprogram (it has no return); the reader moves it in front of `PROGRAM=END` behind a `JUMP=END`, so a program is one contiguous section (language 4.13, `examples/INCREMENTAL_SUB.ncx`). Was "and labels" meant this way, or should a jump-entered section also be a named section of the file (`LABEL=BEGIN` ... `LABEL=END`)?
Recommendation: as applied; a labelled section of the file would need a rule for where the section ends and what its entry state is, which the `JUMP=END` guard avoids.
Where: language 4.9, 4.13; virtual machine 2.7, 3.6, 3.9; mapping 1, 6.

ANSWER:
as recommended

---

## Answered (second round, 2026-09-12; settled in `decisions.md`)

Kept for the record: the question as asked, the recommendation, and the maintainer's answer.

#### D48 Name of the file terminator (follow-up)
Question: the first round asked about the executed end (`EXIT`); the answer proposed `FILE_END` for the file terminator, which today is `PROGRAM=END` (Heidenhain `END PGM`, Fanuc `%`). `FILE_END` says what it is and reads well after `EXIT`. Rename `PROGRAM=END` to `FILE_END` and keep `EXIT` and `PROGRAM=BEGIN` as they are, or rename the pair (`PROGRAM` ... `FILE_END`)?
Recommendation: `NCX=1`, `PROGRAM=BEGIN NAME=...` ... `EXIT` ... `FILE_END`; `EXIT` kept; `PROGRAM=BEGIN` kept until the header words are reviewed as a whole.
Where: language 4.1, 4.13, 7.

ANSWER:
Do a FILE_BEGIN, PROGRAM=BEGIN, PROGAM=END, FILE_END. it is theoretically possible to have multiple programs in one file. or we can use FILE=BEGIN and FILE=END to have it simmilar to program. Also SUB=BEGIN, SUB=END for sub-programs and labels.
i dont tink "EXIT" is needed at all. The PROGRAM=END will trigger whatever the toml says for program end (M30, M2, etc.)


---

#### D29 Prefix scope (follow-up)
Question: the answer asked for `CYCLE_F`. Only the two cycle words that clash with motion words got the prefix (`CYCLE_F`, `CYCLE_DWELL`); the cycle-only words `SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `RETRACT`, `PECK`, `PITCH`, `AXIS` stay bare. Prefix all of them for uniformity (`CYCLE_DEPTH=-21.7`), or keep the short forms?
Recommendation: keep the short forms; a cycle block is recognizable by its `CYCLE=` word, and the long forms cost readability in every drilling program.
Where: language 4.7, 7.

ANSWER:
as recommended

---

#### D81 Tool vector programming for 5-axis simultaneous motion
Question: the corpus has a 5-axis program written as `LN X Y Z NX NY NZ TX TY TZ` under `M128` (surface normal and tool vector, 1 467 lines); Fanuc writes `G43.5` with `I J K`, Siemens `A3= B3= C3=` under `TRAORI`. NCX has only rotary axis words. Add vector words to `LINE` (`NX`, `NY`, `NZ` surface normal, `TX`, `TY`, `TZ` tool vector, unit vectors, valid only under `TCPM=ON`), converted to rotary axes by the kinematics module when the target needs axes?
Recommendation: yes, as a second form of `LINE` that the VM stores without resolving (like `POLAR`), so that vector programs survive `convert` and compile to controllers that accept vectors; axis conversion is the kinematics module's job and out of 1.0.
Where: language 4.3; virtual machine 3.1; controller-mapping 11.

ANSWER:
as recomended, and there will be probably more variants in the future.

---

#### D82 Tilted plane by axis angles
Question: `PLANE AXIAL A B C` is the dominant tilt form in the corpus (1 392 lines in 10 files), `CYCL DEF 19` the older one (476 lines in 32 files); Fanuc has `G68.1` and `G53.1`, Siemens `CYCLE800` in axis-angle mode. NCX `TILT` takes spatial angles only. Add a second word `TILT_AXIS A= B= C=` meaning "the plane that results from these rotary positions on this machine" (machine specific by intent, like machine axis names), plus the positioning options (`MOVE=TURN|STAY|MOVE`, `ROT=TABLE|COORD`) on both forms?
Recommendation: yes; readers keep the form the source used, the compiler converts between the forms only when the kinematics module is present.
Where: language 4.2; controller-mapping 1, 11.

ANSWER:
yes, as recommended

---

#### D83 Retract along the tool axis
Question: `M140 MB MAX` (Heidenhain, 130 lines in 13 files) retracts along the tool axis in the tilted system, to the limit or by a distance; Siemens has retract subprograms, Fanuc a machine-frame move. Add a `RETRACT` verb (`RETRACT` bare = to the limit, `RETRACT=50` = by 50 along the tool axis)?
Recommendation: yes; the compiler writes `M140 MB MAX`, `L_FREI`, or a computed `G53` move where the kinematics is known.
Where: language 4.3; controller-mapping 1.

ANSWER:
as recommended

---

#### D84 Sweep angle on arcs (helices beyond 360 degrees)
Question: TopSolid writes helices as `CP IPA+737.956 IZ-5.4 DR+` (2 388 lines in 40 files): an incremental polar angle of more than one turn with an axial increment. NCX `ARC` ends at a point and cannot say "two turns". Add `ANGLE=737.956` (signed sweep angle) as an alternative to the end point on `ARC`, and let the compiler split into full turns for controllers that take one turn per block?
Recommendation: yes; polar coordinates themselves (`LP`, `CP` with `PA`/`PR`, Fanuc `G16`, Siemens `AP`/`RP`) are converted to Cartesian by the readers, only the sweep angle needs a word.
Where: language 4.3; virtual machine 3.2.

ANSWER:
as recommended

---

#### D85 Path tolerance
Question: `CYCL DEF 32 TOLERANCE` (132 lines), Fanuc `G5.1 Q1`, `G5 P10000`, `G8 P1`, `G61.1`/`G64`, Siemens `CYCLE832`, `G642`, `COMPCAD` set the contouring tolerance and mode of the control for 3D and 5-axis programs. NCX has no word. Add `TOLERANCE=0.02` (modal, with optional `ROTARY=0.05` and `MODE=FINISH|ROUGH`, `TOLERANCE=OFF`)?
Recommendation: yes; the machine file maps the mode to the control's form, unmapped options stay `RAW`.
Where: language 4.1; controller-mapping 1.

ANSWER:
yes, as recommended

---

#### D86 TCPM options
Question: with `M128` the corpus uses `M126`/`M127` (shortest rotary path), `M116`/`M117` (rotary feed in mm/min), `M138`, Fanuc `G43.4` types and `G41.2` (5-axis radius compensation), Siemens `ORIWKS`/`ORIMKS`. Are these options words on `TCPM` (`ROTARY_PATH=SHORTEST`, `ROTARY_FEED=MM_MIN`) or `RAW`?
Recommendation: the two that change geometry (`ROTARY_PATH`, `ROTARY_FEED`) as modal words, the rest `RAW` until a program needs them; `G41.2` stays `RAW` in 1.0.
Where: language 4.2; controller-mapping 1, 11.

ANSWER:
as recommended

---

## Answered (first round, 2026-09-11; settled in `decisions.md`)

Kept for the record: the question as asked, the recommendation, and the maintainer's answer. Where the answer changed the specification, the settled table says how.

### Group A: needed before the first code

#### D27 Bare `TOOL` for authoring
Question: keep `TOOL` without a value (change to the preloaded tool) as an authoring form, or always require the number?
Recommendation: keep it; `ncx format` writes the number.
Where: language 4.4, 7.

ANSWER:
As recommended

---

#### D28 Diameter programming inside the VM
Question: does the virtual machine store radii and let `DIAMETER=ON` only change the meaning of X words, or does it store what the program says?
Recommendation: radii internally; analytics and the kinematics module then never see diameters.
Where: language 7; virtual machine 3.1.

ANSWER:
When reading a NC-File from a lathe or Mill Turn, we have to know if the values are radii or diameter.
In the ncx format we have to know if the x-values are in radii or diameter for furhter processing.
If the converter CONTROL_1>NCX>CONTROL_2 moves from RADII>NCX>DIAMETER, we have to know which values we have to convert and which not.

---

#### D29 Cycle parameter words
Question: `F`, `DWELL`, `DEPTH` are reused inside `CYCLE` blocks with their general meaning. Acceptable, or prefix them (`CYCLE_F`)?
Recommendation: reuse; one meaning per word still holds.
Where: language 4.7, 7.

ANSWER:
Keep it seperate with "CYCLE_F"

---

#### D30 Machine axis names in programs
Question: may a program name a machine axis (`Z2`, `C2`) directly, or only through roles?
Recommendation: allowed; a program that names a machine axis is machine specific by intent.
Where: language 4.3, 7.

ANSWER:
Yes, a program can name a axis like Z2. Siemens does that, but with a "=". Like Z2=450, or C2=180.
But for my understandign Fanuc does not do that. But im not sure, check that before building up on my input.

---

#### D33 Fanuc macro variables
Question: `#100` becomes `V100` because `#` is not a legal NCX character. Fixed?
Recommendation: yes.
Where: language 4.9.

ANSWER:
yes

---

#### D34 Unknown initial state
Question: writers emit a complete header (`UNITS`, `WORKPLANE`, `FEED_MODE`, `COMP=OFF`, `CYCLE=OFF`); the VM takes configuration defaults only for source readers?
Recommendation: yes.
Where: virtual machine 11.

ANSWER:
yes

---

#### D35 Position after machine-frame moves
Question: after `FRAME=MACHINE` the moved axes are known in the machine frame and unknown in the workpiece frame unless a datum table is configured. Keep this simple rule?
Recommendation: yes; datum table optional.
Where: virtual machine 3.4, 11.

ANSWER:
yes; datum table optional.

---

#### D36 Arc tolerance
Question: the tolerance for the center consistency check comes from the machine configuration?
Recommendation: yes, defaults 0.01 mm and 0.0005 in.
Where: virtual machine 3.2, 11.

ANSWER:
yes, defaults 0.01 mm and 0.0005 in.

---

#### D37 Cycle expansion for analytics
Question: an `ExpandCycles` VM option that raises the individual motions of a cycle call as MOTION events?
Recommendation: yes.
Where: virtual machine 3.3, 11.

ANSWER:
yes

---

#### D38 Unassigned variables
Question: reading a variable that was never assigned is an ERROR, or 0?
Recommendation: ERROR by default, `unassigned = 0` per machine file.
Where: virtual machine 3.6, 11; machine-config 7.

ANSWER:
ERROR by default

---

#### D42 Preload mismatch
Question: "a different tool preloaded than called" is a WARNING, or should the VM stay silent because many machines handle it?
Recommendation: WARNING; it is what a converter user wants to know.
Where: virtual machine 3.5, 11.

ANSWER:
WARNING; it is what a converter user wants to know, but not a error. We have to assume also that the user wanted it that way to have a special pocket preselected for whatever reason.

---

#### D48 Name of the executed end
Question: `EXIT` (chosen because `END` reads as "end of what" next to `PROGRAM=END` and `STOP` already means M0/M1), or `FINISH`, or `M30`?
Recommendation: keep `EXIT`.
Where: language 4.1, 4.13, 7.

ANSWER:
What about "FILE_END"? on Heidenhain this would be PGM END, on (some) Fanuc it would be "%".
It is possible to have content after a M30 or M2 etc. But not after a file end.

---

#### D49 M2 versus M30
Question: NCX has one `EXIT`. Should a source `M2` be preserved as such for a round trip on the same controller?
Recommendation: no; `program_end` in the machine configuration decides what is written, readers accept both.
Where: language 7; machine-config 2.

ANSWER:
as recommended

---

#### D51 System variables
Question: reserved `SYS_` names (`$SYS_POS_X`, `$SYS_WEAR_Z[99]`, `$SYS_MPOS_B`) with a per-controller mapping table, or `RAW` only?
Recommendation: reserved names with an index; the Nakamura transfer program reads `#5024` and `#11099`, real programs need them.
Where: language 4.12, 7; machine-config 7.

ANSWER:
as recommended

---

#### D53 `SKIP` semantics in the VM
Question: are block-skip blocks executed or skipped by the VM?
Recommendation: run option `skip_blocks`, default execute; analytics can report both.
Where: language 4.1; virtual machine 3.6.

ANSWER:
run them, as recommended

---

#### D54 Kinematic transformations as state
Question: keep `CYLINDER`, `POLAR`, `TCPM` as modal words whose geometry is only resolved by the kinematics module, so that readers do not lose them?
Recommendation: yes, as state only.
Where: language 4.2, 7.

ANSWER:
as recommended

---

#### D55 `SETPOS`
Question: Fanuc `G50 X Z`/`G92 X Z` and `G50 C0` declare the current position instead of selecting a datum (the C form appears in every Nakamura polar example). A verb of its own, or fold it into `ORIGIN` with a computed offset?
Recommendation: own verb; the VM records a per-axis shift cleared by `ORIGIN`; the compiler writes the native form or, on Heidenhain, computes a `SHIFT`.
Where: language 4.2, 7; virtual machine 3.4.

ANSWER:
as recommended

---

#### D58 Chamfer and rounding words
Question: Fanuc `,C`/`,R`, Siemens `CHF`/`CHR`/`RND`, Heidenhain `CHF`/`RND` shorten hand-written programs. Readers expand them to `LINE` and `ARC` now. Add `CHAMFER=` and `ROUND=` words later?
Recommendation: expand now; add the words once hand authoring shows the need, CAM output never uses them.
Where: language 4.3, 7.

ANSWER:
expand now

---

#### D59 Drilling axis on lathes
Question: lathes drill along Z (`G83`..`G85`) or along X (`G87`..`G89`). `AXIS=X` on the `CYCLE` block, or a second cycle family (`SIDE_DRILL`)?
Recommendation: `AXIS` word, default from the `WORKPLANE`; one family, one set of parameter names.
Where: language 4.7, 7; virtual machine 3.3.

ANSWER:
We have CYCLE_RADIAL and CYCLE_AXIAL. Lets go with your recommendation for now with the AXIS.

---

#### D60 Diameter word set
Question: `DIAMETER=ON` halves `X`, `IX`, absolute `CENTER:X` and the X words of an `AXIS=X` cycle; `CENTER:IX`, `R` and other radial distances are radius values; Siemens `DIAM90` is folded by the reader; the machine setting `programming` (diameter, radius, switchable) on the X axis decides what is read and written. Is that the right cut?
Recommendation: yes. The Heidenhain part is B3 below.
Where: language 4.2, 7; virtual machine 3.1; machine-config 4; controller-mapping 1.

ANSWER:
yes

---

#### D61 Plugins never write VM state
Question: a plugin that wants to clamp a speed or insert a spindle stop rewrites or inserts NCX blocks before the VM executes them, or edits the output lines after; it never sets `spindle.rpm` itself?
Recommendation: yes; the VM stays the only holder of meaning and every change is visible in `trace`.
Where: language 4.15, 7; virtual machine 7; architecture 9; code-guidelines 5.

ANSWER:
yes

---

#### D62 Math library
Question: no math package in `Ncx.Core` (stored numbers `decimal` with the original text, geometry in `double` in a small own namespace), MathNet.Numerics (MIT) only in `Ncx.Kinematics` when needed?
Recommendation: yes.
Where: architecture 4.2.

ANSWER:
yes

---

#### D63 Expansion rules
Question: machine sequences as `pre`, `post`, `requires`, `restore` on function states, tool change and catalog cycles, turned by an expander into generated NCX blocks that the VM executes, with `@SAVE`/`@RESTORE` for the state a rule had to change?
Recommendation: yes.
Where: machine-config 5a; virtual machine 1, 3.10; language 4.15.

ANSWER:
yes

---

#### D72 .NET target
Question: the current LTS .NET (10 in 2026) as the only target, cross-platform console application?
Recommendation: yes; one target framework in `Directory.Build.props`.
Where: architecture (assumptions); code-guidelines 9.

ANSWER:
yes

---

#### D73 Namespace and project prefix
Question: `Ncx.Core`, `Ncx.Config`, `Ncx.Readers`, `Ncx.Compilers`, `Ncx.Analytics`, `Ncx.Plugins`, `Ncx.Cli`, later `Ncx.Kinematics`; the solution `NCXchange.sln`?
Recommendation: yes; short prefix, the product name only on the solution and the repository.
Where: architecture 3.

ANSWER:
yes

---

#### D74 Names on the command line and on disk
Question: the command is `ncx` with the verbs `convert`, `compile`, `format`, `check`, `analyze`, `trace`, `annotate`, `plugin`; the file extension is `.ncx`; a job manifest is `<name>.ncxjob.toml`; start values are `<name>.vars.toml`; the working directory file is `ncx.toml`?
Recommendation: yes.
Where: architecture 10; machine-config 8, 10.

ANSWER:
yes

---

#### D75 License of the new NCXchange
Question: the project is meant to be open source; the guidelines require MIT-compatible packages but say nothing about the project's own license. MIT, Apache-2.0, or GPL-3.0 again? The choice decides whether shops can keep their plugins private.
Recommendation: MIT or Apache-2.0 for the core so that private plugins are possible; GPL only if copyleft is the intent.
Where: code-guidelines 9 (packages); architecture (assumptions).

ANSWER:
MIT

---

#### D76 Word catalog: code or data
Question: adding an NCX word needs a VM handler, so the word catalog is code (a C# table) and an NC programmer can add cycle catalog entries and machine rules but not words. Intended line, or should the catalog be a TOML file with handlers looked up by name so that data and code stay apart?
Recommendation: code; a word without a handler is meaningless, and the language should not grow from a shop's TOML file.
Where: architecture 4; code-guidelines 10.1.

ANSWER:
as recommended

---

#### D77 `convert` always needs a machine file
Question: the reader needs the X axis `programming` of the source machine for a lathe, so `convert` cannot run on a controller family alone. Require a machine file for every `convert`, also for mills, or allow a bare controller family for mills with defaults?
Recommendation: require it always; one rule, and the machine file is where the reader gets M code names anyway.
Where: architecture 7, 13; controller-mapping 1.

ANSWER:
as recommended

---

### Group B: verification against real controllers

#### B1 (D31) Frame chain order
Question: NCX applies origin, then `SHIFT`, then `TILT`, then `ROTATE`, then `MIRROR`, each replacing its own kind. Does this match the behaviour of Fanuc (G54, G52, G68.2, G68, G51.1), Heidenhain (cycle 247, cycle 7, PLANE, cycle 10, cycle 8) and Siemens (G54, TRANS, CYCLE800, ROT, MIRROR) in that order, in particular where a rotation is programmed before a shift?
Recommendation: verify with one sample program per controller before the compiler is written; from postprocessor experience you may know the answer already.
Where: language 4.2, 7.

ANSWER:
Some of it depends on the math behind the cycle, especialy on TILT. Its usualy euler math. Some are strange configured. Usualy we only have 2 Rotation axis.

The most common process is to ORIGN>SHIFT>ROTATE/TILT. The shift is done before the rot. The same has to be mirrored in NCX and the tartget format. 

If we get a ORGIGIN>TILT>SHIFT on the source, it will most likely be the same on the target. Its just importat to follow the sequence, so the math adds up. Because the Shift after tilt and the shift before a tilt is not the same.

---

#### B2 (D40) Workpiece transfer detection by readers
Question: readers set `WORKPIECE=SUB` from the `[workpiece]` table where the builder has selection codes (Nakamura `M427`, Mori Seiki `M304`, Doosan `M134`, DMG `M813`) and otherwise from the chuck function rule (`SUB_CHUCK.CLOSE` marks the handover). Does this hold on the WY250L programs and on your own transfer programs?
Recommendation: verify on `O1000.NC`, `O1000.P-2` and the Walter Meier WY transfer program.
Where: virtual machine 11; machine-config 5.

ANSWER:
Make it modular. We have to provide a modular function which can be modified easily. Some of those things depend entirely on the machine and how it was set up. Some of those M-Functions run a hidden sub program on the machine. So we cant asume its the same on each machine, even from the same manufacturer.

---

#### B3 (D60) Heidenhain turning
Question: does the diameter rule of D60 fit Heidenhain turning (`FUNCTION TURNDATA`, TNC 640 turning mode)? No Heidenhain lathe manual was at hand at the time, only the iTNC 530 milling manuals.
Recommendation: get the TNC 640 turning documentation, or leave Heidenhain turning out of 1.0.
Where: controller-mapping 1.

ANSWER:

But heidenhain is not very good in turning. So if it makes no sense, we leave it behind for now.

---

### Group C: needed at the milestone that uses them

#### D10 Tool geometry
Question: tool geometry (length, radius, kind rotary or turning, holder) in NCX words, or in a separate tool table?
Recommendation: separate table, `tools.toml` next to the machine file; needed at the latest when `{kind}` is compiled for Mori Seiki `G361` and DMG `TC` (milestone M5 or M8).
Where: language 4.4; architecture 13.

ANSWER:
Make it optional. For {kind} we use the toml, if the toml has no tools, we use a standard value. But we write at converted program head, that the tool data is missing (with the path to the expected file, and the tools which are missing), so nobody runs the program by accident.

---

#### D16 History in the program
Question: old values next to new values in the program text (`[GOTO:X=new:old]` from the original idea)?
Recommendation: no; `Before`/`After` on every event, `ncx trace` and `ncx annotate` give the history without storing it. Can be closed as settled.
Where: virtual machine 6.

ANSWER:
as recommended

---

#### D39 Channel scheduler
Question: the job scheduler is round based (every runnable channel executes one block per round); time based only inside the timeline analytics?
Recommendation: yes. Needed at M9.
Where: virtual machine 3.7, 11.

ANSWER:
yes

---

#### D41 Resource types in 1.0
Question: work spindle, tool spindle, tool holder, table and axis are the resource types; bar feeder, tailstock, steady rest, pallet changer are named functions, not resources?
Recommendation: yes for 1.0.
Where: virtual machine 11; machine-config 4.

ANSWER:
yes

---

#### D52 Tool change macros with parameters
Question: the builders carry the index angle of the tool carrier (Nakamura `B` move after `G340`, Mori Seiki `G361 B`, DMG `TC(..., b1, ...)`), the tool spindle orientation for turning tools (Nakamura `G419 A`, DMG `TC(..., c1)`) and the tool kind (Mori `D0.`/`D1.`, DMG kind 1..4). Placeholders filled by look-ahead (`{b}` from the next positioning block, `{c}` from the next `ORIENT:TOOL`, `{kind}` from the tool table), or explicit words on the `TOOL` block?
Recommendation: placeholders with look-ahead and the tool table; the kind must come from the tool table in any case. Needed at M5 (first ATC compile).
Where: language 4.4, 7; machine-config 3.

ANSWER:
as recommended

---

#### D56 Channel-bound functions
Question: some codes may only be commanded from one channel (Nakamura spindle sync from the path that owns the second spindle, Biglia door only from channel 1) and some must stand in every channel program (Mori Seiki `M34`/`M35`). Where does NCX say this?
Recommendation: never in the program; the machine configuration marks the table with `channel = n` or `channels = "all"`, the job compiler moves or duplicates the words and inserts the `SYNC` it needs, a single-file compile reports an ERROR. Needed at M9.
Where: language 7; virtual machine 3.8; machine-config 5.

ANSWER:
as recommended

---

#### D57 Sub spindle frame
Question: under `WORKPIECE=SUB`, are coordinates in the sub spindle's own right-handed frame (+Z out of its chuck), or in the machine's Z direction as Fanuc programs with a `G59` datum write them?
Recommendation: own frame, one meaning on every machine; the configuration (`SUB_frame = "datum" | "mirror"`) says whether the compiler writes a Z mirror cycle (Nakamura `G360`/`G361`, Mori Seiki `M396`/`M395`) or negates Z against a datum, and the reader undoes the same. Needed at the first sub spindle compile (M8).
Where: language 4.10, 7; virtual machine 3.4; machine-config 5.

ANSWER:
as recommended. When reading the file, we see in the toml how the source config was.

---

#### D64 Runtime estimate and machine limits
Question: trapezoidal profile per block from `max_feed`, `acceleration`, `rapid`, the control's `block_time`, `path_mode` with `corner_speed`, spindle `accel_time`; deceleration equal to acceleration; machine limits (`rpm_min`/`rpm_max`, `max_feed`, travel) reported as WARNING or clamped per `limits = "warn" | "clamp"`?
Recommendation: yes, warn by default; a separate `deceleration` value only if a machine needs it. Needed at M7.
Where: virtual machine 5, 8; machine-config 1, 4.

ANSWER:
as recommended

---

#### D65 Cycle catalog for `G70`..`G76`
Question: the Fanuc multiple repetitive cycles name a contour block range with `P` and `Q`. How does a catalog entry reference a contour section of an NCX program: a `LABEL` range, a `SUB` section, or a contour word (`CONTOUR=name`) that Siemens `CYCLE95` with a contour subprogram would share?
Recommendation: a contour section as `SUB=name` ... `RETURN` after `EXIT`, referenced by `CONTOUR=name` on the cycle block; Fanuc `P Q` and Siemens `CYCLE95("name")` both map to it. Needed when the first turning program with `G71` is converted.
Where: language 4.7.1; architecture 13.

ANSWER:
as recommended

---

#### D66 Readers folding expanded sequences
Question: a rule expands `COOLANT:THROUGH=ON` into `M5`, `M51`, `M3 S1500`; the reader of that machine should fold the sequence back into the one word, otherwise a round trip accumulates spindle stops. Implement `Template.Matches` over several source blocks in 1.0, or accept the accumulation with a WARNING?
Recommendation: implement it for `requires`/`restore` rules only (the pattern is fixed: save, condition, block, restore), accept a WARNING for free-form `pre`/`post`. Needed at M6 (round trips).
Where: architecture 13; machine-config 5a.

ANSWER:
This would be the job of the AddIn to handle that, for example (dummy code):
If(Coolant == "Center")
{
    CurrentSpindle.Stop;
    Coolant.On;
    Spindle.Mode, Spindle.Rpm;
}

This function would be executed instead of the raw "Write coolant here" which would be regularly executed.
---

#### D67 Analytics in 1.0
Question: which analytics are in 1.0: tool list, runtime estimate, travel limits, datum/feed/speed lists, segment length and tool vector change, loop statistics, channel timeline?
Recommendation: 1.0 ships segment length and tool vector change, the tool list and the runtime estimate; limits and lists are cheap and can follow; loop statistics and the timeline come with M9.
Where: virtual machine 8; architecture 9, 12.

ANSWER:
as recommended. PLUS: a start index and a end index for the analytics. Because for the segment lenght, the user might dont want to analyse the whole program for point to point, but for example only Line 380 to 600

---

### Group D: scope and sources

#### D68 Siemens sources
Question: the DMG manuals cover GILDEMEISTER structure programming, not generic Sinumerik. The Siemens reader and compiler stay preliminary until a Sinumerik 840D programming manual (Fundamentals, Job planning) and sample programs are available. Do you have them, and is Siemens part of 1.0 or of a later milestone (M8 today)?
Recommendation: keep M8 but do not start it without the manual; the `.mpf` files of the sample corpus (`02705-2-03304_00.mpf`, `80270740_Zapfen_VO_SP2.mpf`) are the first samples.
Where: controller-mapping (status line); architecture 12.

ANSWER:
skip the GILDEMEISTER structure for now. But keep it in mind, so no decisions are made which prevent a implementation in the future.

---

#### D69 Controller families in scope
Question: the sample corpus hold Okuma OSP (`.min`), Mazak EIA (`.eia`), Hyundai WIA, Matsuura and Hermle programs. Fanuc, Heidenhain and Siemens are the families of the specification. Are Okuma OSP and Mazak (EIA is Fanuc-like, Mazatrol is not) planned as families later, or out of scope? The answer decides whether the reader design has to leave room for a fourth family in 1.0.
Recommendation: out of scope for 1.0; Mazak EIA programs can serve as Fanuc-family test inputs, Okuma OSP is a family of its own for later.
Where: architecture 12 (later).

ANSWER: as recommended


---

#### D70 Unread sample folders
Question: the sample corpus (Mazak, Matsuura and 422 customer programs) are not read yet. Should they be read for the specification now (they may show words NCX lacks, as the manuals did), or only used as reader test inputs at M4 and later?
Recommendation: a quick survey now of the Fanuc and Heidenhain files for words the language lacks; the rest as test corpus later.
Where: controller-mapping 10.

ANSWER: as recommended


---

#### D71 Versioning of the docs
Question: the specification drafts were not under version control while they were written. Initialize git there (private repo), or move the spec into `NCXchange/docs` now and version it with the code?
Where: README (how to iterate).

ANSWER: we version it when we have leveld out all bumps and have a clear picture


---

#### D78 Definition of done
Question: the guidelines end with a definition of done (spec sentence cited in a comment, code reads as it, naming per section 3, analyzers quiet, a test names the rule, examples still round-trip, decision log line if a decision was taken, an NC programmer could follow the change with the folder README). Right for a solo project, or too strict for early milestones?
Recommendation: keep it from M2 on; M1 may skip the folder READMEs.
Where: code-guidelines 12.

ANSWER: as recommended


---

#### D79 Test framework and assertions
Question: xUnit with plain `Assert` and hand-written fakes, no mocking framework, no FluentAssertions?
Recommendation: yes; fewer packages, and the string-in string-out tests need nothing more.
Where: code-guidelines 8, 9.

ANSWER: as recommended


---

#### D80 Plugin settings
Question: may a plugin read its own settings (a section `[plugins.MyShopRules]` in `ncx.toml`) through the `RewriteContext`, or are plugins configured in code only?
Recommendation: yes, a string dictionary from its own section; nothing else of `ncx.toml` is visible to it.
Where: architecture 9; code-guidelines 11.

ANSWER: as recommended

