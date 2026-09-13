# Ncx.Readers.Tests

The tests of `src/Ncx.Readers`: a source snippet in, the expected NCX text out, named after what the reader does with the controller's words, `G81_WithG99_BecomesDrillCycleWithClearanceRetract` (architecture 11; code-guidelines 10.3).

The framework (P3-01) is tested through `Fakes/`: `FakeTokenizer` and `FakeReader` read a small Fanuc-like syntax on top of `ReaderBase`, `CodeRule` is a reader rule as a plugin writes one, and `FakeRead` reads a snippet against a small machine and checks that the output formats to itself. Start with `ReaderRulesTests.cs` (the three rules of the "Done when", the tables first), `StructurePassTests.cs` (the jump-entered section of `INCREMENTAL_SUB`, the `M99` loop) and `RawTests.cs` (`RAW` through a format round trip). The contract tests that run the same cases against every reader come with the readers (code-guidelines 4, Liskov substitution).

Never here: whole example files compared end to end (`../Ncx.Acceptance/`), a path relative to the working directory (`../README.md`).
