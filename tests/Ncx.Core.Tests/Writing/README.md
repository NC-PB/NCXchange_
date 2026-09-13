# Writing tests

`NcxWriterTests`: the canonical order, the numbers as read, strings escaped, the comment column, trivia and line endings (language 2 rule 7, 5 rules 6 and 7; D90, D92, D93). `NcxBuilderTests`: the builder a reader assembles its program with, the words sorted at `End()` (architecture 7). That the five examples format to themselves byte for byte is an acceptance test, `tests/Ncx.Acceptance/Examples/ExampleFormatTests.cs`.
