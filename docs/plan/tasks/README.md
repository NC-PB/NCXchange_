# Tasks

One file per task in `inbox/`; a task moves to `done/` (same file name) when its acceptance criteria hold on the main branch and its log says who closed it. The order is the phase order of `../phases.md`; inside a phase the `Depends on` field is the order. Sizes are rough: S a day or two, M up to a week, L more than that.

Format of a task file: goal (one paragraph), scope (what is built, as a list), references (the specification sections that define it; cite them in the code comments), done when (the acceptance criteria, each testable), notes, and a log that the person working on it fills in. A task that turns out to need a decision the specification does not give: write the question and the recommendation into `../../decisions/rationale.md` under a new `D` number, decide it with the maintainer, add the row to `decisions.md`, then continue.

Adding a task: copy the format, give it the next number of its phase, add the row below.

| Id | Task | Phase | Milestone | Depends on | Size |
|---|---|---|---|---|---|
| `P0-01` | Repository skeleton and solution | 0 | M1 | none | S |
| `P0-02` | Core model: Block, Word, Value, Diagnostics | 0 | M1 | `P0-01` | S |
| `P0-03` | Word catalog | 0 | M1 | `P0-02` | M |
| `P0-04` | Lexer and parser | 0 | M1 | `P0-03` | M |
| `P0-05` | Expression parser | 0 | M1 | `P0-04` | S |
| `P0-06` | Canonical writer and `ncx format` | 0 | M1 | `P0-05` | M |
| `P0-07` | Folder READMEs and `reading-the-code.md` | 0 | M1 | `P0-06` | S |
| `P1-01` | VM state classes and snapshots | 1 | M2 | `P0-06`, `P2-01`, `P2-02` | M |
| `P1-02` | Block execution: state words, frames, tool change | 1 | M2 | `P1-01` | L |
| `P1-03` | Block execution: motion, arcs, retract, cycles | 1 | M2 | `P1-02` | L |
| `P1-04` | Validation rules and diagnostics | 1 | M2 | `P1-03` | M |
| `P1-05` | Events with Before and After | 1 | M2 | `P1-04` | M |
| `P1-06` | Expander and generated blocks | 1 | M2 | `P1-05` | M |
| `P1-07` | `ncx check`, `ncx trace`, `ncx annotate` | 1 | M2 | `P1-06` | S |
| `P2-01` | TOML schema and loading | 2 | M3 | `P0-04` | M |
| `P2-02` | Templates both ways | 2 | M3 | `P2-01` | M |
| `P2-03` | Cycle catalogs | 2 | M3 | `P2-02` | M |
| `P2-04` | Example machines and `machines/` folder | 2 | M3 | `P2-03`, `P1-07` | S |
| `P3-01` | Reader framework and source-side state | 3 | M4 | `P1-07`, `P2-04` | M |
| `P3-02` | Fanuc reader | 3 | M4 | `P3-01` | L |
| `P3-03` | Compiler framework and number formatting | 3 | M5 | `P3-01` | M |
| `P3-04` | Heidenhain compiler | 3 | M5 | `P3-03`, `P3-02` | L |
| `P3-05` | Heidenhain reader | 3 | M6 | `P3-02`, `P3-04` | L |
| `P3-06` | Fanuc compiler | 3 | M6 | `P3-03`, `P3-05` | L |
| `P3-07` | Acceptance project and corpus runner | 3 | M6 | `P3-06` | M |
| `P4-01` | Expression evaluation and interpreted flow | 4 | M7 | `P1-07` | M |
| `P4-02` | Analytics: tool list, runtime estimate | 4 | M7 | `P4-01` | M |
| `P4-03` | Analytics: segment length and tool vector change | 4 | M7 | `P4-02` | M |
| `P5-01` | Siemens reader | 5 | M8 | `P3-07` | L |
| `P5-02` | Siemens compiler | 5 | M8 | `P5-01` | L |
| `P6-01` | Job scheduler and `SYNC` | 6 | M9 | `P4-01` | M |
| `P6-02` | Job compiler and channel binding | 6 | M9 | `P6-01`, `P3-06` | M |
| `P7-01` | Plugin interfaces and loading | 7 | M10 | `P1-06`, `P1-05`, `P3-01`, `P3-03` | M |
| `P7-02` | Plugin template and `ncx plugin` commands | 7 | M10 | `P7-01` | M |
| `P7-03` | Release 1.0 | 7 | M10 | `P7-02`, `P6-02`, `P5-02`, `P4-03` | S |
| `PL-01` | Kinematics module | later | later | `P7-03` | L |
| `PL-02` | Further controllers and dialects | later | later | `P7-03` | L |
