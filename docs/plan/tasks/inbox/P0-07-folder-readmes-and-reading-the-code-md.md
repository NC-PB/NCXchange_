# P0-07 Folder READMEs and `reading-the-code.md`

Phase: 0 | Milestone: M1 | Depends on: `P0-06` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The path through the code for the next person, who may not be a programmer.

## Scope

- One `README.md` per project folder: what lives here, the two or three files to open first, what never goes here.
- `docs/reading-the-code.md`: a guided tour from `Program.cs` through one `ncx format` run (extended to `convert` and `compile` in phase 3), file by file.
- The three extension levels (configuration, plugin, source) named on the front page of `docs/`.

## References

- code-guidelines.md section 10 (code for readers who are not programmers)

## Done when

- A reader who has never seen the repository can name the file that parses a word after ten minutes with the READMEs.
- The tour is checked against the code in the acceptance test project by a test that asserts the mentioned files exist.

## Notes

May be skipped for M1 per D78 and written at the end of phase 1 instead; not later.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13, part one (the folder READMEs; branch `p0-07-readmes`). Built on `main` at P1-02, with every task of the wave merged (P0-01 to P0-06, P1-01, P1-02, P1-03a, P2-01 to P2-03, P2-04a, P4-01a), and written from the code as it is there. Decision applied: D78 (the folder READMEs now, the tour at the end of phase 1). No code changed, no shared file changed, the specification is unchanged, and no decision was needed.

- 42 files, one `README.md` in every folder under `src/` and `tests/` except the build output.
  - `src/README.md`: the seven projects, the dependency rule, a "Looking for" table from a question to the file that answers it (a word of the language, the file that parses a word, the block rules, the tool change, a template, a diagnostic code, a command), and how the section references read.
  - One per project: `src/Ncx.Core`, `Ncx.Config`, `Ncx.Readers`, `Ncx.Compilers`, `Ncx.Analytics`, `Ncx.Plugins`, `Ncx.Cli`; `tests/Ncx.Core.Tests`, `Ncx.Config.Tests`, `Ncx.Readers.Tests`, `Ncx.Compilers.Tests`, `Ncx.Acceptance`. Each says what lives here, which files to open first and what never goes here (phase 0, P0-07).
  - One of a few lines per subfolder: what is in it, where to start reading, and which section of the specification it implements (code-guidelines 10.3). The subfolders are `src/Ncx.Core/Model`, `Catalog`, `Parsing`, `Expressions`, `Writing`, `Geometry`, `Machine`, `VirtualMachine` with `State` and `Handlers`; `src/Ncx.Config/Cycles`, `Templates`; `src/Ncx.Cli/Commands`; the test folders that mirror them; `tests/Fixtures`; `tests/Ncx.Acceptance/Cli`, `Examples`, `Repository`; `tests/Ncx.Config.Tests/Fixtures`.
- Decided while doing it (conventions, no decision of the log):
  - Section references are written as the documents and the code comments write them (`language 4.5`, `virtual machine 3.5`, `D90`), and `src/README.md` says once which file each name is. Paths in backticks are relative to the README, as the P0-01 READMEs write them.
  - `src/README.md` is added beside the per-project READMEs. Code-guidelines 10.3 asks for one in every folder, and it is where a first reader lands.
  - The four empty source projects and the two empty test projects get a README all the same. It says what will live there by architecture 3, 7, 8 and 9 and the phase plans, which task fills the project, and what never goes there. The types it names for later (`IReader`, `ReaderBase`, `ICompiler`, `IAnalytic`) are marked as coming.
  - `tests/Ncx.Config.Tests/Fixtures/` holds a data file, not code, and gets a README too (code-guidelines 10.3: every folder). No README is compiled or embedded: `tests/Fixtures.props` compiles `tests/Fixtures/*.cs` only, and `Ncx.Config.Tests` embeds `expected-machine-warnings.txt` by its name.
  - A README describes its folder as it is on `main`. The lines that name work still to come (in `VirtualMachine/`, `ExecuteMotion` executes `HOME` only and `RaiseEvents` is empty; the empty projects) go stale when that work lands, and the task that lands it updates the README (code-guidelines 12; `src/README.md` says so).
- Checked by hand, with a script outside the repository: every folder under `src/` and `tests/` has a README; every file path a README names exists on `main`; every type and member it names occurs in the code, except the types of later tasks, which are named as coming. No test was written, since this part changes no code.

Done when:

- "A reader who has never seen the repository can name the file that parses a word after ten minutes with the READMEs": holds as far as documents can show it; it was not tried with a person. The table "Looking for" of `src/README.md` names `src/Ncx.Core/Parsing/WordLexer.cs`, which reads one word, and `Parser.cs`, which makes it a `Word` against the catalog. `src/Ncx.Core/README.md` leads to `Parsing/`, whose README says the same.
- "The tour is checked against the code in the acceptance test project by a test that asserts the mentioned files exist": waits for part two.

Remains for part two, at the end of phase 1 (D78 allows no later date):

- `docs/reading-the-code.md`, the tour from `Program.cs` through one `ncx format` run, ending in the virtual machine with `ncx check` (P1-07), and extended to `convert` and `compile` in P3-07.
- The three extension levels of code-guidelines 10.1 on the front page of `docs/README.md`, in the same commit as the tour.
- The test in `Ncx.Acceptance` that every file the tour names exists. The same test can check the paths the folder READMEs name, which this part checked by hand.
- The task file stays in `inbox/` until then (phase 0, exit checklist).

Gate: `dotnet build -warnaserror` with 0 warnings; `dotnet test` with 2268 tests passing (1329 in `Ncx.Core.Tests`, 897 in `Ncx.Config.Tests`, 40 in `Ncx.Acceptance`); `dotnet format --verify-no-changes` clean.
