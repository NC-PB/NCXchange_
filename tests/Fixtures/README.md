# Fixtures

The fixture mechanism every test project shares, linked into each by `../Fixtures.props` (`../README.md`, Fixtures): `Fixture.cs` reads an embedded example of `docs/spec/examples` by its path, `Fixture.ReadText("2.5D_FRAESEN.ncx")`, and finds the repository root from the test assembly; `EmbeddedExamplesTests.cs` runs in every test project and checks that no example is missing.

Never here: a test of one project (only what every test project shares), a copy of an example (the specification folder stays the single source, implementation 00-method 4).
