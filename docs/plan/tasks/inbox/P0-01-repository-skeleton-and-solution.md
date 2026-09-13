# P0-01 Repository skeleton and solution

Phase: 0 | Milestone: M1 | Depends on: none | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

A repository that builds empty projects for every `Ncx.*` library, the CLI and the test projects, with the settings of the code guidelines applied from the first commit.

## Scope

- `NCXchange.sln` with `src/Ncx.Core`, `Ncx.Config`, `Ncx.Readers`, `Ncx.Compilers`, `Ncx.Analytics`, `Ncx.Plugins`, `Ncx.Cli` and `tests/Ncx.Core.Tests`, `Ncx.Readers.Tests`, `Ncx.Compilers.Tests`, `Ncx.Acceptance` (xUnit).
- Target framework: the current LTS .NET (D72); one `Directory.Build.props` with nullable on, warnings as errors, `TreatWarningsAsErrors`, the analyzers with CA1305 (culture) as an error, `LangVersion` latest.
- `.editorconfig` with the naming and layout rules of the guidelines (section 3), 120 characters per line, file-scoped namespaces.
- `LICENSE` (MIT, D75), `README.md` (one paragraph and the link to `docs/`), `.gitignore`, `global.json` pinning the SDK.
- Folders `machines/`, `cycles/`, `templates/ncx-plugin/`, `samples/plugins/` created with a README each saying what goes there (may be one sentence at this point).
- A CI workflow (GitHub Actions or equivalent) that restores, builds and runs the tests on every push and pull request.

## References

- architecture.md section 3 (solution layout), section 2 (guiding rules)
- code-guidelines.md sections 3, 9, 10

## Done when

- `dotnet build` and `dotnet test` succeed on a clean checkout with no warnings.
- The CI badge is green on the main branch.
- Every project references only what the dependency diagram allows (Core nothing; Config Core; Readers and Compilers Core and Config; Analytics Core; Plugins Core, Readers and Compilers, so that a plugin references `Ncx.Plugins` alone, D106; Cli everything).

## Notes

Keep the first commit small; the point is that every later task lands in a repository whose rules already hold.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13. Commit 1 is the maintainer's "initial commit" (the documentation as handed over), `.gitignore` followed in 858ddae, the skeleton is the next commit on `main` (not pushed). Built: `NCXchange.sln` (classic format) with the seven `src/` projects and five test projects (`Ncx.Config.Tests` added per implementation 00-method 4); `global.json` (10.0.400, `latestPatch`); `Directory.Build.props`; `Directory.Packages.props`; `.editorconfig` and `tests/.editorconfig`; `.gitattributes`; `nuget.config`; `LICENSE`; `README.md` with the CI badge; `.github/workflows/ci.yml`; the READMEs of `machines/`, `cycles/`, `templates/ncx-plugin/`, `samples/plugins/` and `tests/`; the fixture mechanism. The specification is unchanged; everything below is a repository convention.

- Fixtures: `tests/Fixtures.props`, imported by every test project, embeds every file of `docs/spec/examples/` directly, with its path from the repository root as the logical name, instead of copying the files into `tests/Ncx.Acceptance/Fixtures/` as the phase file sketched, so there is no second copy to go stale. It links `tests/Fixtures/Fixture.cs` (`ReadText`, `ReadBytes`, `List`, `RepositoryRoot`, the last walking up from the test assembly to `NCXchange.sln`) and `tests/Fixtures/EmbeddedExamplesTests.cs`, which checks in every test project that no example is missing. `Ncx.Acceptance` checks that every embedded copy equals its original byte for byte. Usage in `tests/README.md`.
- `tests/.editorconfig` switches off CA1707, which forbids the underscores of `Rule_Scenario_Expectation` (phase 0, risks); no other rule needed an override. The helper folder is `tests/Fixtures`, not `tests/Shared`, because CA1716 rejects the namespace segment `Shared` (a Visual Basic keyword).
- `.gitattributes`: every text file is LF in every checkout, so the string-in string-out tests read the same text on Linux and Windows; `docs/spec/examples/**` is `-text`, so the CAM sources keep their CRLF byte for byte on both. `.editorconfig` says `end_of_line = lf` to match and leaves `docs/spec/examples/` alone.
- `.editorconfig` is the dotnet/docs file with the additions marked `NCXchange:`: CA1305 as error; file-scoped namespaces as error, also as `dotnet_diagnostic.IDE0161`, which the build enforces (checked); 120 characters; the naming rules of code-guidelines 3.1 and the preferences of 3.2 and 3.3 at the dotnet/docs severity `suggestion`, so the build does not enforce them; `charset = utf-8` instead of `utf-8-bom`.
- `nuget.config` clears inherited feeds and names nuget.org only, so restore is the same on every machine and in CI.
- Packages, current stable, pinned in `Directory.Packages.props`: Tomlyn 2.10.1 (BSD-2-Clause), System.CommandLine 2.0.12 (MIT), xunit 2.9.3 (Apache-2.0), xunit.runner.visualstudio 4.0.0 (Apache-2.0, runs xUnit v2), Microsoft.NET.Test.Sdk 18.10.0 (MIT); the .NET analyzers come with the SDK, no package. `Ncx.Config` references Tomlyn and `Ncx.Cli` System.CommandLine from the start (architecture 3).
- References per architecture 3 with D106 and D107; each test project references what it tests, `Ncx.Acceptance` references Analytics, Plugins and Cli (00-method 4). `InternalsVisibleTo` as project items: each library to its test project, Analytics, Plugins and Cli to `Ncx.Acceptance`. `tests/Ncx.Acceptance/Repository/ReferenceGraphTests.cs` asserts the source graph, the test graph, the packages per project and the five central versions.
- Placeholders: an empty `internal sealed class Placeholder` in each library, with a `TODO` to delete it with the first real type; `src/Ncx.Config/Placeholder.cs` sits in the folder of P2-01, which deletes it. `src/Ncx.Cli/Program.cs` is a `Main` returning 0 that P0-06 replaces (the folder is P0-06's). `ncx` packs as a .NET tool with the command `ncx` (checked).
- CI: `actions/checkout@v7`, `actions/setup-dotnet@v6` with `global-json-file`, and the four steps of the phase file on ubuntu-latest and windows-latest.
- Open questions, marked `TODO(question)` in the files: the analyzers do not report CA1305 while `InvariantGlobalization` is true (checked: `decimal.Parse(text)` passes, and is an error with the property false), so the two settings of code-guidelines 3.4 and 9 cancel out; both are kept as written. `dotnet format` has no line-length rule, so the 120 characters of code-guidelines 3.3 guide the editors only. The copyright holder in `LICENSE` is "the NCXchange authors" until the maintainer names one.

Done when:

- `dotnet build` and `dotnet test` succeed on a clean checkout with no warnings: holds (`-warnaserror` with 0 warnings, 12 tests passing, `dotnet format --verify-no-changes` clean, also on a copy of the committed files outside the repository).
- The CI badge is green on the main branch: waits for the maintainer's push; the workflow and the badge are in place.
- Every project references only what the dependency diagram allows: holds, and `ReferenceGraphTests` keeps it so.

The file stays in `inbox/` until CI is green on `main`.
