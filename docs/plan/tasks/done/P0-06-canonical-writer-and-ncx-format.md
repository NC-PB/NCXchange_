# P0-06 Canonical writer and `ncx format`

Phase: 0 | Milestone: M1 | Depends on: `P0-05` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The one way an NCX program is written, so that two programs that mean the same thing are the same text (D43).

## Scope

- `NcxWriter`: canonical word order per section 5 rule 6 (the rank table of D90), numbers untouched, strings escaped, the block's comment last at column 57 (three spaces when the words end at column 54 or later), trivia lines written back as read (D92), `SUB` sections and programs in file order, generated blocks never written; a provisional machine axis word (D93) has no catalog entry and takes the rank of bucket 3 behind `X Y Z A B C`, absolute before incremental, alphabetical by letter then number, so the canonical text never depends on a machine file.
- `NcxBuilder` (fluent, used by readers later): `Begin(line).Verb(...).Word(key, addr, value)...End()`.
- `ncx format <file> [--check]`: parse, write, nothing else; it takes no machine file and does not run the VM, a bare `TOOL` stays bare (D91); exit codes per D97: 0 without ERROR, 1 with an ERROR, 2 for a usage error or an unreadable input (the missing-machine-file case of D97 cannot occur here); `--check` exits 1 when the output differs from the input; the shared `--strict` option (a WARNING exits 1) arrives with P1-07 and applies to `format` as well.
- The CLI project with System.CommandLine, the `ncx` root command (`format` has no `--machine` option, D91), exit codes and diagnostic output to stderr.

## References

- ncx-language.md section 5 rules 6 and 7, section 2 rule 7
- architecture.md sections 4 (NcxBuilder) and 10 (CLI table)

## Done when

- The five examples format to themselves byte for byte (line endings per the file); they were rewritten into the rank order and the comment of `PATTERN_LOOP.ncx:15` realigned in the D90 commit (D90, D92), so `ncx format` must produce an empty diff over `docs/spec/examples/*.ncx`.
- A hand-written block in free order formats into the canonical order and back to itself.

## Notes

This closes M1.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13. Built on `main` with the parser of P0-04, the catalog ranks and `CanonicalOrder` of P0-03 and the expression print of P0-05. Files: `src/Ncx.Core/Writing/NcxWriter.cs`, `WriterOptions.cs`, `NcxBuilder.cs`; `src/Ncx.Cli/Program.cs` (the composition root, replacing the P0-01 placeholder), `Commands/FormatCommand.cs`, `DiagnosticCodes.cs` (`CLI001`-`CLI004` of the range `CLI001`-`CLI099`), `ExitCodes.cs`. Tests in `tests/Ncx.Core.Tests/Writing/` (`NcxWriterTests`, `NcxBuilderTests`), `tests/Ncx.Acceptance/Examples/ExampleFormatTests.cs` and `tests/Ncx.Acceptance/Cli/` (`FormatCommandTests`, `DiagnosticCodesTests`). Decisions implemented: D43 and D90 (the words of a block in the order of the rank table, through `CanonicalOrder.Sort`), D91 (`format` is the parser and the writer: no machine file, no `--machine` option, a bare `TOOL` stays bare), D92 (trivia written back as read, the comment in column 57 or three spaces after words that reach column 54), D93 (machine axis words behind `X Y Z A B C` without a machine file), D94 (native parameters in source order), D97 (exit codes), D98 (diagnostics on the standard error as `file(line): SEVERITY CODE: message`). The specification is unchanged; no shared file was touched.

Decided while doing it (readings of the specification and conventions, no decision of the log):

- `NcxWriter` is a public static class: `Write(program)`, `Write(program, WriterOptions)` and `WriteBlock(block)`. For every line of the file it writes a trivia line or a block: each trivia line before the first block of a greater line, the rest after the last block, all in list order; a parsed file has one line per block or trivia, so every line comes back at its place. A block is its words in canonical order, one space apart, from the first column (language 3, Word; 5 rule 7: "the words"), so the indentation and the trailing whitespace of a block line go; the comment follows as read, after `max(56 - length of the words, 3)` spaces. Every line ends with the line ending of the program, LF when it has none, and the last line too. Generated blocks are skipped unless `WriterOptions.IncludeGenerated`.
- The writer writes the words the program holds. `ncx format` never writes a program with an ERROR: the run stops on ERROR (code-guidelines 5 and 6), the diagnostics go to the standard error, nothing goes to the standard output or to `--output`, exit code 1. A WARNING (mixed line endings, PAR001) is printed and the run continues, exit code 0 until `--strict` arrives with P1-07.
- `NcxBuilder` takes the reader's `Diagnostics`, and the program it builds carries them and their file name. The calls are fluent as the phase file and the sequence diagram of architecture 7 chain them (the class diagram's `Block Begin(...)` is not followed): `Begin(sourceLine)`, `Verb(key)` and `Verb(key, value)`, `Word(key, addr, value)`, `Raw(controller, text)`, `Comment(text)`, `End()`, `Trivia(text)`, `Build()`. `Begin` stores the source line as `Block.Line`, which the diagnostics cite. `End()` sorts the words with `CanonicalOrder.Sort` and marks the verb as the parser does. `Comment` and `Trivia` take the text as it stands in the NCX file (a comment from its semicolon; a blank, whitespace-only or comment-only line), so the builder adds no layout of its own. `Build()` runs the parser's `StructurePass` for the sections and the file frame, whose ERRORs join the diagnostics, and leaves the line ending null (LF). A broken contract (a word outside a block, two verbs, a block without words, a comment without its semicolon, a trivia line with words, a second `Build()`) is a programmer error and throws (code-guidelines 6).
- CLI: `Program.Run(args, output, error)` builds the root command by hand and runs it; `Main` hands it the standard output as UTF-8 without a byte order mark, so the canonical text goes out byte for byte. A usage error from System.CommandLine (no command, no file, an unknown option such as `--machine`, `--check` together with `--output`) is `CLI001`, exit code 2. `--check` with `--output` is refused rather than given a meaning, because `--check` writes nothing (architecture 10). An input that cannot be read, or whose bytes are no UTF-8 (language 3, Encoding), is `CLI002` on line 1 of the file, exit code 2; a diagnostic about a whole file stands on line 1, as the loaders of `Ncx.Config` report one. Under `--check` a difference is an INFO, `CLI003`, on the first line that differs, and it sets exit code 1 (D97). It is no ERROR, because hand-written files are free (language 5 rule 6), and no WARNING. An output file that cannot be written is the ERROR `CLI004`, exit code 1, because the run has started (architecture 10: "a run that has started ends with 0 or 1").
- `ExitCodes`: `NoError` 0, `Error` 1, `NotStarted` 2 (D97).

Open questions, marked `TODO(question)` in the files:

1. `NcxWriter.cs`: language 5 rule 7 counts columns and does not say what a column is beyond ASCII (a UTF-16 unit, a code point, a character as an editor shows it); a column is one character of the string, as the lexer counts its columns.
2. `NcxBuilder.cs`, `Trivia`: a program places its trivia among its blocks by line number alone, and a reader begins each block with its source line (architecture 7). A trivia line takes the line of the block begun last, which keeps the order of the calls while the lines do not decrease. Where they do (a section moved in front of `PROGRAM=END`, language 4.13), or where two blocks of one source line stand around a trivia line, the writer may place it elsewhere. P3-01 meets this.
3. `NcxBuilder.cs`, `Raw`: architecture 7 draws `void Raw(controller, text)`, a call of its own without a line, which reads as a whole RAW block; the phase plan lists it among the calls of a block. It adds `RAW:controller="text"` to the block being built, so that a whole source block is `Begin(line).Raw(...).End()`.
4. `Program.cs`: D98 renders a diagnostic as `file(line)`, and a usage error is about the command line; it is rendered `ncx(1): ERROR CLI001: ...`.
5. `FormatCommand.cs`: language 3 says UTF-8 and does not say whether a byte order mark belongs to an NCX file; it is kept in front of the canonical text as it came, so a canonical file with one passes `--check`.

Done when:

- The five examples format to themselves byte for byte: holds (`ExampleFormatTests.Format_Example_ReproducesItByteForByte` over the embedded copies of every `.ncx` example, `FormatCommandTests.FormatCheck_Example_ExitsZeroAndWritesNothing` through the CLI; `dotnet run --project src/Ncx.Cli -- format docs/spec/examples/<file> --check` exits 0 for each of the five, and the standard output of `format` equals each file byte for byte).
- A hand-written block in free order formats into the canonical order and back to itself: holds (`NcxWriterTests.Rule6_BlockInFreeOrder_WritesTheCanonicalOrder` and `Rule6_CanonicalBlock_IsStableOnASecondPass`; `FormatCommandTests.Format_FreeOrderFile_WritesItInCanonicalForm` and `FormatCheck_FileThatFormatWrote_ExitsZero`).

This closes M1 (Notes). Gate: `dotnet build -warnaserror` with 0 warnings, 2087 tests passing (1148 in `Ncx.Core.Tests`, 40 in `Ncx.Acceptance`), `dotnet format --verify-no-changes` clean.
