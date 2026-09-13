# Handoff

Date: 2026-09-12. State: the design is complete and decided; no code exists yet. This note says what to do with the folder next to it.

## What NCXchange is

A controller-independent NC program format, NCX, and the tool `ncx` around it. Three uses, in the order they came up: converting a program from one controller to another (Fanuc G-code in, Heidenhain Klartext out, and every other pair); writing machine-independent programs by hand; letting a CAM postprocessor emit NCX once so that one postprocessor covers every machine through the NCX compiler and a machine configuration in TOML. A program means the same thing everywhere; every machine specific detail lives in the configuration. One virtual machine resolves the full state of a program at every block and serves the readers, the compilers and the analytics. Plugins extend the readers (source rules), the expander (program rewriters), the virtual machine (listeners) and the compilers (block writers) without touching the core. The people who will extend it are NC programmers first and C# programmers second; the code guidelines are written for them.

## What is decided

Everything in `decisions/decisions.md` (96 decisions numbered D1 to D107, none open; D90 to D106 were added 2026-09-11 by the fourth round from the implementation plan, `implementation/02-decisions-proposed.md`, and D107 on 2026-09-13 by its final review). The ones that shape the code most:

- The notation: bare words `KEY[:ADDR][=VALUE]`, one block per line, an explicit verb on every motion block (D2), incremental coordinates per value (D44), canonical order mandatory for every writer (D43).
- The file structure: `FILE=BEGIN NCX=1` ... `FILE=END` around programs (`PROGRAM=BEGIN` ... `PROGRAM=END`, the executed end) and subprograms (`SUB=BEGIN NAME=` ... `SUB=END`); an early end is `JUMP=END` (D48, D50, D88, D89).
- The state model: modal like G-code, one virtual machine, STATIC mode for convert, compile and check, INTERPRETED mode for analyze; `format` is the parser and the canonical writer only and does not run the VM (D3, D18, D91); the frame is a transform chain in program order (D31); the VM stores radii and `DIAMETER` says how X words are meant (D28, D60).
- The tool change is two words, `PRELOAD` and `TOOL` (D47); resources are addressed by role (D22); machine sequences are expansion rules that generate ordinary NCX blocks with `@SAVE`/`@RESTORE` (D61, D63); plugins never write VM state (D61).
- Machine specifics are templates in TOML (tool change, spindle functions per role, wait marks, transformations, retract, tolerance, cycle catalogs); the word catalog is code, the cycle catalogs are data (D76); `convert` always needs a machine file (D77); `check`, `trace`, `annotate` and `analyze` run without one against the built-in default machine of `Ncx.Config` and report a role, function or machine axis it lacks as the WARNING "not checked: no machine file" (D103).
- `HOME` on an axis without `home` is a WARNING at check time and an ERROR only in a compiler that must write the coordinates; every `[[axis]]` carries `home` and `limits` in machine coordinates and a `[positions]` table feeds expansion rules through `{position:NAME}` (D100).
- Scope of 1.0: Fanuc, Heidenhain iTNC 530, generic SINUMERIK 840D sl; no kinematics module (D24), no Heidenhain turning, no GILDEMEISTER structure programming (D68), no Okuma or Mazatrol (D69).
- The stack: current LTS .NET, one target framework (D72), `Ncx.*` projects (D73), MIT (D75), xUnit with plain asserts and hand-written fakes (D79), Tomlyn for TOML, System.CommandLine for the CLI, no math package in the core (D62).
- Diagnostics: a code is an area prefix and three digits, rendered `file(line): ERROR VM042: message`, with the severities ERROR, WARNING and INFO and an `OriginLine` for diagnostics on generated blocks (D98); exit code 0 without an ERROR, 1 on an ERROR (or a WARNING under `--strict`, or a differing `format --check`), 2 for a usage error, an unreadable input or a missing machine file where one is required (`convert`, `compile`; D97, D103).

## What to do first

Phase 0 of `plan/phases.md`: the repository skeleton (`plan/tasks/inbox/P0-01-...`), the core model, the word catalog, the lexer and parser, the expression parser, the canonical writer and `ncx format`. The acceptance test of the whole phase is that the five files in `spec/examples/*.ncx` parse and format to themselves byte for byte. From there the phases follow the build order of the architecture: virtual machine, configuration, the Fanuc and Heidenhain readers and compilers with `spec/examples/sources/` as the acceptance inputs, analytics, Siemens, jobs, plugins, release.

Read before writing code: `architecture/code-guidelines.md` sections 1 and 2 (the four rules and the comment rule; the code explains the comment to the computer, and every comment cites the specification sentence it implements), section 10 (what to avoid so that an NC programmer can follow the code), section 12 (definition of done).

## What to ask the maintainer

- The sample corpus (about 480 customer and manual programs, `controllers/sample-corpus.md`) for the robustness runs of phase 3 and the 5-axis reference programs of phase 4; it stays outside the repository.
- A measured cycle time for `3D_FRAESEN` to calibrate the runtime estimate (task P4-02).
- Anything the specification does not answer: write the question and a recommendation under a new `D` number in `decisions/rationale.md`, get the answer, add the row to `decisions/decisions.md`. Do not decide language questions silently in the code.

## What is deliberately not here

- No history of how the documents were drafted beyond the paragraph at the end of `decisions/decisions.md`; the documents are version 1.0 and change with the code from now on.
- No manuals: they are the controller and machine builders' documents, named by title in `controllers/README.md` and in `spec/controller-mapping.md` section 10; the facts needed from them are in `controllers/`.
- No code. The architecture is an outline; the class diagrams are the intended shape, not a contract. Where the code finds a better cut, change the diagram in the same commit and say why in the decision log if a decision was involved.
