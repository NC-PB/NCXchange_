# Method

Status: 2026-09-11, before any code. How every task of `../plan/tasks/` is worked, and the repository conventions that the documents leave open.

## 1. The loop per task

1. **Read the sentences.** Open the task file, open every section it cites, and copy the sentences to implement into a checklist at the top of the working notes (one line per sentence, with its section number). The checklist is the scope; a sentence that is not on it is not implemented in this task.
2. **Tests first.** One test per sentence or rule, named `Rule_Scenario_Expectation` (code-guidelines 8), string in and expected text or state out, in the test project that mirrors the source project. The example files are the acceptance tests and run last.
3. **Comment, then code.** Every non-trivial block of logic starts with the comment that cites the sentence (code-guidelines 1 and 2); the code beneath it is the proof. Names come from the specification (`HolderState`, `Preloaded`, `CanonicalRank`).
4. **Run the examples.** `ncx format` over the five `.ncx` files, `ncx check` from phase 1 on, the round trips from phase 3 on. A difference is a failure of the task, not of the example, unless a decision says the example changes.
5. **Close the task.** Fill the Log of the task file (who, when, what was decided while doing it), move the file to `../plan/tasks/done/`, tick the row in `20-schedule.md`.
6. **Decisions.** A question the specification does not answer, or answers twice, is never settled in the code. Write it as `#### Dnn Title` with Question, Recommendation and Where into `../decisions/rationale.md`, get the answer, add the row to `../decisions/decisions.md`, and change the specification sections, the examples and the tests in the same commit as the code. `02-decisions-proposed.md` holds the drafts known today.

## 2. Repository

- The folder is not a git repository yet. `git init` is the first action of P0-01. Commit 1 is the documentation exactly as handed over, so that every later document fix is a visible diff against version 1.0. Commit 2 is the skeleton.
- One branch per task, named after the task (`p0-04-lexer-parser`), squash-merged to `main` when the task's acceptance criteria hold and CI is green. The commit message starts with the task id and one line; decisions taken are cited (`D90`).
- The remote and the CI provider are the maintainer's choice (`20-schedule.md`, maintainer asks). `gh` is installed; GitHub Actions is assumed and the workflow file is written in P0-01 whether or not a remote exists yet.
- `main` always builds and passes the tests; nothing lands red.

## 3. Environment

Verified on the development machine: .NET SDK 10.0.400 (also 10.0.301 and 7.0.302 present), git 2.50.1, gh 2.93.0.

- `global.json` pins `10.0.400` with `rollForward: latestPatch` (D72: current LTS, one target framework, `net10.0`).
- `Directory.Build.props` at the root per code-guidelines 9: `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors`, `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild`, `InvariantGlobalization`, `LangVersion` latest.
- `Directory.Packages.props` with central package versions: Tomlyn, System.CommandLine, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk. Every addition gets a line in the decision log with its license (code-guidelines 9).
- `.editorconfig` copied from `dotnet/docs` with the three project rules: 120-character lines, `dotnet_diagnostic.CA1305.severity = error`, `csharp_style_namespace_declarations = file_scoped:error`.
- CI: restore, build, `dotnet format --verify-no-changes`, `dotnet test`, on Ubuntu and Windows (the line-ending handling of the writer must hold on both).

## 4. Conventions the documents leave open

These are decided here because they are not language, virtual machine or configuration questions; each is one sentence and gets no decision number.

- Test projects mirror `src/`: `tests/Ncx.Core.Tests`, `Ncx.Config.Tests` (the architecture lists none for `Ncx.Config`; one is added), `Ncx.Readers.Tests`, `Ncx.Compilers.Tests`, `Ncx.Acceptance`. Analytics, Plugins and Cli are tested from `Ncx.Acceptance` until they have enough tests to deserve a project.
- Fixtures are embedded resources under `tests/<project>/Fixtures/`. The five `.ncx` examples, the source programs and the machine files are copied from `docs/spec/examples/` by a build step and a test asserts that the copies equal the originals, so the specification folder stays the single source of truth.
- `InternalsVisibleTo` for the matching test project only; `internal` and `sealed` by default (code-guidelines 3.4).
- One `DiagnosticCodes` class per project holding that project's area prefixes (`PAR` and `VM` in `Ncx.Core`; `CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI` in `Ncx.Config`, `Ncx.Readers`, `Ncx.Compilers`, `Ncx.Analytics`, `Ncx.Plugins`, `Ncx.Cli`) (D98); a code is never renumbered.
- Fixture paths and outputs are never relative to the working directory (code-guidelines 8); the acceptance tests write to a temporary folder.
- The corpus of the maintainer stays outside the repository; tests that need it read the environment variable `NCX_CORPUS` and skip with a message when it is not set.
- Generated documentation (`docs/spec/generated/word-catalog.md`, `docs/spec/generated/diagnostics.md`) is written by tests and committed, so the catalog and the specification can be compared by eye and the diff shows every change.

## 5. Definition of done

Code-guidelines 12 applies in full from P1-01 on (D78): the sentence is cited, the code reads as the sentence, the naming follows section 3, the analyzers are quiet, a test names the rule and passes, the examples still pass `format`, `check` and the round trip, the decision log has a line if a decision was taken, and an NC programmer could follow the change with the folder README in hand. Phase 0 may skip the folder READMEs; the plan writes them anyway at the end of phase 0 because they are cheap, and the guided tour `docs/reading-the-code.md` at the end of phase 1.

## 6. What the code is for

Two readers, kept in mind in every file: the NC programmer who adapts a reader to a machine or writes a plugin with a week of C# (code-guidelines 10: no LINQ chains beyond two calls, no generics of our own in readers, compilers, plugins and analytics, files of one screen named after what they map), and the reviewer who opens the cited specification section next to the code and expects to find the same rule. When a construct serves neither, it is the wrong construct.
