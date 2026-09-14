# Ncx.Acceptance

End to end (implementation 00-method 4): the example files of the specification (`Examples/`), the rules of the repository itself (`Repository/`: the project references of architecture 3, the packages of code-guidelines 9, the embedded examples equal to their originals, the files the tour `../../docs/reading-the-code.md` names), and the tests of `Ncx.Cli` (`Cli/`), `Ncx.Analytics` (`Analytics/`) and `Ncx.Plugins` until they have test projects of their own. `Expected/` holds the whole-file outputs the example tests compare with, `Expected/analytics/` the reports that are the reference of the analytics.

Open first: `Examples/ExampleFormatTests.cs`, the five examples formatting to themselves byte for byte; then `Cli/FormatCommandTests.cs`, which runs `ncx format` in a temporary folder of its own, and `Cli/CheckCommandTests.cs`, which runs `ncx check` on every example. `Repository/ReferenceGraphTests.cs` is where a wrong project reference fails the build.

Never here: a unit test of one rule (it belongs to the test project of its source), output written anywhere but a temporary folder (implementation 00-method 4).
