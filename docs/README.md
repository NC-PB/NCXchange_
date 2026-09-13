# NCXchange documentation

NCXchange is a controller-independent NC program format (NCX) with a virtual machine, readers for Fanuc, Heidenhain and Siemens programs, compilers driven by TOML machine configurations, analytics and plugins, written in C# on .NET. This folder is the complete documentation of version 1.0 of the design: everything the engineer needs is here, and nothing here refers to anything outside the repository except the official controller and machine-builder manuals, which are named by title.

Start with `HANDOFF.md`. Then, in this order: `spec/ncx-language.md` (what a program says), `spec/ncx-virtual-machine.md` (what it means), `spec/machine-config.md` (what the machine adds), `spec/controller-mapping.md` (how the controllers say it), `architecture/architecture.md` (how the code is cut), `architecture/code-guidelines.md` (how the code is written), `plan/phases.md` and `plan/tasks/` (what to do first), then `implementation/` (how the engineer builds it, and what has to be settled before the first code).

## Layout

| Path | Content |
|---|---|
| `HANDOFF.md` | Where the project stands, what is decided, what to do first, what to ask the maintainer |
| `spec/ncx-language.md` | The language: purpose, design rules, lexical rules and grammar, the word catalog, block rules and canonical order, examples |
| `spec/ncx-virtual-machine.md` | The virtual machine: state, block execution, arcs, cycles, tool change, flow, scheduler, validation, events, analytics, kinematics contract |
| `spec/machine-config.md` | The TOML machine configuration: identity, output format, tool change templates, roles and resources, axes, functions, expansion rules, cycle catalog, variables, job manifest, kinematic tree |
| `spec/controller-mapping.md` | What readers derive from Fanuc, Heidenhain and Siemens source and what compilers write, per NCX word; the machine-builder dialects; sources; the corpus survey |
| `spec/examples/*.ncx` | Five example programs, the acceptance tests of the parser and the virtual machine |
| `spec/examples/sources/` | The controller programs behind the examples: the acceptance tests of the readers and compilers |
| `spec/examples/machines/*.toml` | Five machine configuration sketches (Nakamura, Doosan, Mori Seiki on Fanuc; DMG and the generic 840D sl mill-turn `millturn1.toml` of `MILLTURN_TRANSFER.ncx` on Siemens, D104) |
| `architecture/architecture.md` | Solution layout, core model, virtual machine, configuration, readers, compilers, analytics, plugins, CLI, tests, build order; Mermaid diagrams |
| `architecture/code-guidelines.md` | The four rules, the comment rule, C# conventions, SOLID with KISS, patterns, diagnostics, types, tests, repository settings, code for non-programmers, the plugin template, definition of done |
| `controllers/` | The controller knowledge base: differences, Fanuc, Heidenhain, Siemens, machine builders, sample corpus |
| `decisions/decisions.md` | Every decision by number, as the documents cite them |
| `decisions/rationale.md` | The questions, recommendations and answers behind the decisions |
| `plan/phases.md` | The phases with what each delivers and when it is closed |
| `plan/tasks/` | One file per task, `inbox/` and `done/`, with the index in `plan/tasks/README.md` |
| `implementation/` | The engineer's execution plan: method, the findings against the specification, proposed decisions D90 and up, one file per phase, the schedule |
| `glossary.md` | The terms of the project and of the shop floor |

## Conventions

- Decisions are cited as `D31`, `D48`; section references as `language 4.13`, `virtual machine 3.6`. When code implements a sentence of the specification, the comment cites it (code-guidelines section 2).
- A change to the language, the virtual machine or the configuration is a decision: a row in `decisions/decisions.md`, the sections updated in the same commit, the examples updated, the tests updated.
- Mermaid diagrams render on GitHub and in most editors; keep them small and cut them rather than growing them.
- The documents are written in English, in prose where prose does the job, in tables where a reader looks things up.
