# Tests

One test project per source project it tests (`../docs/implementation/00-method.md`, section 4): `Ncx.Core.Tests`, `Ncx.Config.Tests`, `Ncx.Readers.Tests`, `Ncx.Compilers.Tests`, and `Ncx.Acceptance` for the example files end to end, the rules of the repository, and `Ncx.Analytics`, `Ncx.Plugins` and `Ncx.Cli` until they have a test project of their own. Tests are named `Rule_Scenario_Expectation` (code-guidelines 8); `.editorconfig` here switches off CA1707, which would forbid the underscores.

## Fixtures

Every test project imports `Fixtures.props`. It embeds every file of `docs/spec/examples/` (the five `.ncx` examples, `sources/`, `machines/`) into the test assembly and links `Fixtures/Fixture.cs` into the project. A test reads an example by its path relative to `docs/spec/examples`, never through the working directory (code-guidelines 8):

| Call | Gives |
|---|---|
| `Fixture.ReadText("2.5D_FRAESEN.ncx")` | the text, UTF-8, line endings as in the file |
| `Fixture.ReadBytes("sources/BOHREN.h")` | the bytes |
| `Fixture.List()` | every embedded example, `"2.5D_FRAESEN.ncx"`, `"machines/millturn1.toml"`, ... |
| `Fixture.RepositoryRoot()` | the folder of `NCXchange.sln`, found from the test assembly, for tests that write into the repository |

A test project may keep inputs of its own that are no example in its `Fixtures/` folder, embedded by its project file under their path from the repository root: `Ncx.Acceptance` embeds the job manifest of the Nakamura pair, `tests/Ncx.Acceptance/Fixtures/nakamura-wy250l.ncxjob.toml`, and reads it through `Jobs/JobFixture.cs`.

The namespace is `Ncx.Tests.Fixtures`. A new test project imports `../Fixtures.props` and declares `<Using Include="Xunit" />`. The examples are never copied: the specification folder stays the single source of truth, a new file there is embedded on the next build, `Ncx.Acceptance` checks that every embedded copy equals its original byte for byte, and `Fixtures/EmbeddedExamplesTests.cs` checks in every test project that none is missing.
