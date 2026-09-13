# Repository tests

Rules of the repository that every build keeps: `ReferenceGraphTests` reads the project files and asserts the dependency diagram of architecture 3 and the package rule of code-guidelines 9; `FixtureTests` asserts that the embedded examples equal `docs/spec/examples` byte for byte (`../../README.md`, Fixtures).
