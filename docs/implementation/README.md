# Implementation plan

Status: 2026-09-18, written 2026-09-11 from version 1.0 of the documentation set before any code existed. On 2026-09-18, 26 of the 35 tasks are done. Phases 1 and 2 are closed, and phase 0 except its CI criterion, which waits for the maintainer's push; the logs of `10-` to `12-` record the closing. Phases 3 to 7 are open: every remaining task but the release P7-03 is on `main` and waits for something only the maintainer can give (the corpus, a measured cycle time, an answer, the push). This folder is the engineer's side of `../plan/`: `plan/phases.md` and `plan/tasks/` say what to build and when a task is done; the documents here say how the tasks are built, in which order they actually work, and what had to be settled first. Everything here is a plan; it changes as the code lands, and the log at the end of each phase file says what happened.

## Why a second plan

The task files carry acceptance criteria that read as testable, and most of them are. Reading the whole set against the examples before writing code found thirty places (and the final review of 2026-09-13 two more) where the specification, the architecture outline, the task files and the example programs disagree with each other, and a few of them decide whether the first two phases can be closed at all: the five `.ncx` examples are not in the canonical word order that `ncx format` must produce, and three of them cannot pass `ncx check` under the validation list as written. Those places are listed in `01-findings.md`, and the language questions among them are drafted as decisions D90 and up in `02-decisions-proposed.md`, in the format of `../decisions/rationale.md`, so that the maintainer can answer them the way the earlier 89 were answered. `HANDOFF.md` asks for exactly this: do not decide language questions silently in the code.

## Reading order

1. `00-method.md`: how a task is worked, the repository conventions, the environment.
2. `01-findings.md`: the thirty findings, each with the task it blocks and the way it is resolved.
3. `02-decisions-proposed.md`: the decision drafts D90 to D106, grouped by the phase that needs them.
4. `20-schedule.md`: the build order (it differs from the phase table in two places), the decision batches, the maintainer asks, the exit checklist per phase.
5. The phase file of the phase being worked on.

## Files

| File | Content |
|---|---|
| `00-method.md` | The loop per task, git and branch conventions, SDK and CI, conventions the documents leave open, definition of done |
| `01-findings.md` | F1 to F30: what contradicts what, where it bites, how it is resolved |
| `02-decisions-proposed.md` | D90 to D106 as question, recommendation, where; plus the document fixes that need no decision |
| `03-open-questions.md` | The questions found while building waves 1 to 3: task, question, its entry D108 to D255 in `../decisions/rationale.md` or the section that already answers it, and the code location of its workaround; the markers of wave 3 are not triaged yet |
| `10-phase-0-foundations.md` | P0-01 to P0-07: skeleton, model, catalog, lexer and parser, expressions, writer and `ncx format`, READMEs |
| `11-phase-1-virtual-machine.md` | P1-01 to P1-07: state, block execution, validation, events, expander, `check`, `trace`, `annotate` |
| `12-phase-2-configuration.md` | P2-01 to P2-04: TOML loading, templates, cycle catalogs, machine files |
| `13-phase-3-readers-compilers.md` | P3-01 to P3-07: reader and compiler frameworks, Fanuc and Heidenhain both ways, acceptance project, corpus runner |
| `14-phase-4-interpreted-analytics.md` | P4-01 to P4-03: evaluator and flow, tool list, runtime, segment length, tool vector change |
| `15-phase-5-siemens.md` | P5-01, P5-02: the SINUMERIK reader and compiler |
| `16-phase-6-jobs.md` | P6-01, P6-02: scheduler, job compiler, channel binding |
| `17-phase-7-plugins-release.md` | P7-01 to P7-03: plugin loading, template and commands, release 1.0 |
| `20-schedule.md` | Build order, corrected dependencies, parallel tracks, decision batches, maintainer asks, exit checklists |

Every phase file has the same shape: entry state, decisions needed first, one section per task (files and types to create, the specification sentences to cite, the tests to write first, the acceptance run as a command), risks and open ends, exit checklist, log.

## Rules for this folder

- A phase file is updated when its phase starts (the entry state is checked against reality) and when it closes (the log says what was built, what was decided, what was left out).
- A finding in `01-findings.md` gets its "resolved by" column filled when the decision row exists in `../decisions/decisions.md` or the document fix is committed.
- A decision draft moves to `../decisions/rationale.md` when it is put to the maintainer; the copy here keeps the recommendation and gets a line "asked on, answered on".
- Nothing in this folder is specification. Where a phase file and `../spec/` disagree, the specification wins and the phase file is wrong.
