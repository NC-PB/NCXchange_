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
