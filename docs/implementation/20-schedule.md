# Schedule

Status: 2026-09-11, before any code. The order in which the tasks of `../plan/tasks/` are actually worked, where and why it differs from the phase table, what can run in parallel, when the decision batches are asked, what the maintainer is asked for and when, and the checklist that closes a phase.

## 1. Build order

One engineer, one task at a time, in this order. A tick goes in front of a task when it moves to `done/`.

| # | Task | Phase file | Note |
|---|---|---|---|
| 1 | P0-01 Repository skeleton and solution | 10 | `git init` first |
| 2 | P0-02 Core model | 10 | |
| 3 | P0-03 Word catalog | 10 | decision batch 1, answered 2026-09-11 |
| 4 | P0-04 Lexer and parser | 10 | |
| 5 | P0-05 Expression parser | 10 | |
| 6 | P0-06 Canonical writer and `ncx format` | 10 | closes M1; the examples become canonical here |
| 7 | P0-07 Folder READMEs | 10 | the tour waits until 16 |
| 8 | P2-01 TOML schema and loading | 12 | pulled forward, see 2 |
| 9 | P2-02 Templates both ways | 12 | pulled forward, see 2 |
| 10 | P1-01 VM state classes and snapshots | 11 | decision batch 2, answered 2026-09-11 |
| 11 | P1-02 Block execution: state words, frames, tool change | 11 | |
| 12 | P1-03 Block execution: motion, arcs, retract, cycles | 11 | |
| 13 | P1-04 Validation rules and diagnostics | 11 | |
| 14 | P1-05 Events with Before and After | 11 | |
| 15 | P1-06 Expander and generated blocks | 11 | |
| 16 | P1-07 `ncx check`, `ncx trace`, `ncx annotate` | 11 | closes M2; then the `reading-the-code.md` tour of P0-07 |
| 17 | P2-03 Cycle catalogs | 12 | |
| 18 | P2-04 Example machines and `machines/` folder | 12 | closes M3; the second reading of "examples check clean" |
| 19 | P3-01 Reader framework and source-side state | 13 | ask for the corpus now |
| 20 | P3-02 Fanuc reader | 13 | closes M4 |
| 21 | P3-03 Compiler framework and number formatting | 13 | |
| 22 | P3-04 Heidenhain compiler | 13 | closes M5; needs `BOHREN.ncx` from 20 |
| 23 | P3-05 Heidenhain reader | 13 | |
| 24 | P3-06 Fanuc compiler | 13 | closes M6 |
| 25 | P3-07 Acceptance project and corpus runner | 13 | |
| 26 | P4-01 Expression evaluation and interpreted flow | 14 | ask for the cycle time and the large pairs now |
| 27 | P4-02 Analytics: tool list, runtime estimate | 14 | |
| 28 | P4-03 Analytics: segment length and tool vector change | 14 | closes M7 |
| 29 | P5-01 Siemens reader | 15 | |
| 30 | P5-02 Siemens compiler | 15 | closes M8 |
| 31 | P6-01 Job scheduler and `SYNC` | 16 | |
| 32 | P6-02 Job compiler and channel binding | 16 | closes M9 |
| 33 | P7-01 Plugin interfaces and loading | 17 | |
| 34 | P7-02 Plugin template and `ncx plugin` commands | 17 | |
| 35 | P7-03 Release 1.0 | 17 | closes M10 |

`PL-01` (kinematics) and `PL-02` (further controllers) stay in `inbox/` as documented in `phases.md`; nothing in the order above may make them harder (D24, D68).

## 2. Where the order differs from the phase table, and why

1. **P2-01 and P2-02 before phase 1.** The phase table let phase 2 start after the parser of phase 0, as if the virtual machine needed the configuration only at phase 1's end (`phases.md` now carries the corrected order). It does not: P1-01 takes initial values from the configuration, P1-02 resolves roles and axes against it (VM 3.8), P1-03 takes the arc tolerance (D36) and the reference points from it, P1-04 the machine limits. P1-01 spoke of a "default configuration object" that the real loader would have replaced one task later. So the loader and the templates are built first, the VM takes a `MachineConfig` from its first line, and the object used without a machine file is the `DefaultMachine` of D103, a real configuration. P2-03 and P2-04 stay after phase 1, because P2-04's acceptance runs `ncx check` (P1-07) and the with-machine reading of "examples check clean" needs P2-04. The phase closes at P2-04.
2. **P3-04 after P3-02.** The Heidenhain compiler's acceptance compiles `BOHREN.ncx`, which does not exist in the repository; the Fanuc reader produces it from `BOHREN.fanuc.nc`. The table's dependency (`P3-03` only) is corrected.

Both corrections and the smaller ones below were made in `tasks/README.md` and the last paragraph of `phases.md` on 2026-09-11 (finding F27).

| Task | Depends on (table) | Depends on (corrected) |
|---|---|---|
| P1-01 | P0-06 | P0-06, P2-01, P2-02 |
| P2-04 | P2-03 | P2-03, P1-07 |
| P3-04 | P3-03 | P3-03, P3-02 |
| P7-01 | P1-06, P3-03 | P1-06, P1-05, P3-01, P3-03 |

## 3. Critical path and parallel tracks

The critical path with one engineer is the order above; nothing is optional. If a second person joins, these pairs are independent and can run side by side without merge conflicts of substance:

- P2-03 (cycle catalogs, data) with P3-01 (reader framework, code).
- P3-04 (Heidenhain compiler) with P3-05 (Heidenhain reader), once P3-03 and P3-02 exist; they share the controller knowledge and nothing else.
- P4-01 to P4-03 (analytics) with P3-05 and P3-06: the analytics need only phase 1.
- Phase 5 (Siemens) with phase 6 (jobs): phase 6 needs the Fanuc side only.
- P7-02 (template, samples, `docs/plugins.md`) with P6-02.

The decision batches are the only external wait: batch 1 blocks task 3, batch 2 blocks task 10 (both asked and answered 2026-09-11). Tasks 1 and 2 (and P2-01, P2-02 if batch 2 is slow) can be done while the answers are pending.

## 4. Decision batches

| Batch | Decisions | Asked before | Blocks | If unanswered |
|---|---|---|---|---|
| 1 | D90 canonical ranks, D91 `format` without the VM, D92 trivia and comments, D93 machine axis words, D94 native cycle parameters, D95 pseudo-words, D96 `CYLINDER`, D97 exit codes, D98 diagnostic codes | task 3 (P0-03) | P0-03 to P0-06 | D97 and D98 are taken as recommended with a log line (not language); the rest waits, P0-01, P0-02, P2-01, P2-02 proceed |
| 2 | D99 subprogram entry state, D100 `HOME` without reference points, D101 `SETPOS` after `HOME`, D102 the polar plane, D103 no machine file, D104 `millturn1.toml`, D105 function values, D106 plugin interface homes | task 10 (P1-01) | P1-01 to P1-07, P2-04, P5-02, P7-01 | D105 and D106 are structure, taken as recommended with a log line; D99 to D104 wait |
| doc fixes | F18, F20, F21 (the architecture 5.1 flowchart), F23, F24, F28 | none | the task named in each | made in the task, one log line each; F19 (the vector types), F27, F29, F30 and the rest of F21 were made 2026-09-11 to 2026-09-13 |

Both batches were asked and answered 2026-09-11: all seventeen as recommended, with D99, D100 and D105 clarified the same day and D99 to D103 and D106 on 2026-09-12 and 2026-09-13; D107 followed on 2026-09-13 (`../decisions/rationale.md`). Nothing blocks tasks 3 and 10 any more.

Asking a batch means: copy the drafts of `02-decisions-proposed.md` into `../decisions/rationale.md` under a new "Answered (fourth round)" heading, send the maintainer the list with the recommendations, and on the answers add the rows to `decisions.md` with the date. The drafts here get a line "asked on, answered on".

## 5. Maintainer asks, by the time they are needed

| When | Ask | Why | Without it |
|---|---|---|---|
| Task 1 | The git remote and the CI provider (GitHub assumed, `gh` is installed) | P0-01 pushes and CI must be green on `main` | The workflow file is written anyway; CI runs when the remote exists |
| Task 3 | Decision batch 1 (answered 2026-09-11) | see 4 | answered |
| Task 10 | Decision batch 2 (answered 2026-09-11) | see 4 | answered |
| Task 19 | The sample corpus (about 480 programs, `controllers/sample-corpus.md`), placed outside the repository at the path in the environment variable `NCX_CORPUS`; the Hermle C22 U and Burkhardt+Weber programs and the 5-axis pair named in it | The robustness runs of phase 3, the Siemens acceptance of phase 5, the 5-axis analytics of phase 4 | The tests skip with a message; the batch report cannot be produced |
| Task 26 | The measured cycle time of `3D_FRAESEN` on the machine; the two large pairs (5-axis A/C 1.2 MB, point list 5.7 MB) | Calibrate the runtime estimate (P4-02) and the segment analytics (P4-03) | The computed values are committed as unverified references |
| Any task | Anything the specification does not answer | `HANDOFF.md`: never decided silently in the code | Drafted as a decision, the task waits or works around with a `TODO(Dnn)` |

These are the three items of `HANDOFF.md` "What to ask the maintainer" plus the remote.

## 6. Exit checklist per phase

A phase is closed when all of these hold, checked in the phase file's log:

1. The "closed when" cell of its row in `../plan/phases.md` holds, run as commands, not read as text.
2. CI is green on `main` on both operating systems.
3. Every task file of the phase is in `../plan/tasks/done/` with its log filled: who, when, what was decided while doing it.
4. Every decision taken in the phase has a row in `decisions.md`, its question and answer in `rationale.md`, and the specification sections, examples and tests changed in the same commit.
5. The five examples pass `ncx format --check`; from phase 1 on `ncx check` with no ERROR; from phase 3 on the round trips of the acceptance project.
6. The generated tables (`word-catalog.md`, `diagnostics.md`) equal the code.
7. The rows of `01-findings.md` for the phase have their "resolved by" column filled.
8. The phase file's entry state for the next phase has been checked against reality and corrected.

## 7. What "done" does not mean

The runtime is an estimate; the machine files are sketches; the Siemens reader keeps a long `RAW` list; Heidenhain turning and the GILDEMEISTER structure programming are documentation. Release 1.0 says all of this on its front page (P7-03). What 1.0 does promise: a program converted by `ncx` means the same thing on the target as on the source, or the diagnostics say where it does not.
