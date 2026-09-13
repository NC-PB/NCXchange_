# P1-07 `ncx check`, `ncx trace`, `ncx annotate`

Phase: 1 | Milestone: M2 | Depends on: `P1-06` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The three commands that make the VM visible without a compiler.

## Scope

- `ncx check <file> [--machine <toml>]`: parse, expand, STATIC run, diagnostics with the exit codes of D97 (0 no ERROR, 1 an ERROR or a WARNING under `--strict`, 2 a usage error or an unreadable input, including a `--machine` that names a file that cannot be found or read; a machine file that reads but loads with a CFG ERROR exits 1 like any other ERROR); `--machine` is optional, without it the three commands run against the built-in default machine (D103).
- `ncx trace`: one row per changed state variable per executed block (`channel, block, variable, old, new`), CSV or aligned text.
- `ncx annotate`: a copy of the program with the previous values as line comments (`LINE X=55.44 ; X 33.22 -> 55.44`), which the VM ignores (the parser keeps them as the block's comment, D92).
- The run options `skip_blocks` (D53) and `ExpandCycles` on the command line.

## References

- ncx-virtual-machine.md section 6
- architecture.md section 10 (CLI table)

## Done when

- The three commands run on every example; the annotate output parses and formats back to the original when the comments are stripped.
- This closes M2 and phase 1.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13, branch `p1-07-check-trace-annotate`: all of section P1-07 of `implementation/11-phase-1-virtual-machine.md` except the tour (P0-07 part two). Decisions implemented: D16, D37, D53, D91, D92, D97, D98, D99 (a subprogram walked at several calls appears in trace once per walk, in annotate with its first walk), D103. The specification is unchanged; everything below is a reading of it.

- Files:
  - `src/Ncx.Cli/Pipeline.cs`, with `PipelineRun.cs` and `RunSettings.cs`: read the NCX file and the machine file of `--machine`; load the machine, the built-in default machine of D103 without `--machine`; parse; expand with `Expander.Expand` (no plugin rewriters until P7-01); run STATIC with the run options of the command line over `VmOptions.ForMachine`; collect the diagnostics in the order reported, the machine file's first, then the parser's, the expander's and the VM's.
  - `src/Ncx.Cli/Commands/CheckCommand.cs`, `TraceCommand.cs` and `AnnotateCommand.cs` on the pipeline, and `RunOptions.cs` with the shared file argument and options: `--machine <toml>` by path, `--strict`, `--skip-blocks none|all|1,3`, `--expand-cycles`. `trace` adds `--format text|csv`.
  - `src/Ncx.Cli/History/`, with a README:
    - `TraceRow`: a STATE_CHANGE, or a VAR_CHANGE that changed its variable.
    - `TraceListener` and `TraceTable`: the rows in aligned text or CSV.
    - `AnnotationListener`: the values of each block's first walk.
    - `AnnotatedText`: the copy through `NcxWriter.WriteBlock`.
    - `GeneratedOrigin`: how both name the origin of a generated block.
  - `src/Ncx.Cli/InputFile.cs`: the UTF-8 reading of `FormatCommand`, moved out on its second use.
  - `ExitCodes.OfRun`, and `DiagnosticCodes.Pipeline.cs` with CLI100 (the machine file of `--machine` cannot be found or read; exit 2).
- Exit codes (D97):
  - 2 when the NCX file or the machine file of `--machine` cannot be read. Both are read, and both are reported.
  - 1 when the machine file loads with a CFG ERROR (the run does not start), when any stage reports an ERROR, or under `--strict` on any WARNING, the machine file's included.
  - 0 otherwise. An INFO never counts.
  - `--strict` joined `format` as well: P0-06 says the shared option "applies to `format` as well", and architecture 10 says every command accepts it.
- Readings decided while doing it:
  - The diagnostics of all three commands go to the standard error, as P0-06 put them. `check` writes nothing else ("diagnostics only", architecture 10).
  - Trace and annotate include the variables:
    - Why: the variables are state variables of the channel (VM 2.7), VM 6 writes every changed state variable, and PATTERN_LOOP note 1 has the trace show Q1 and Q3.
    - How: the VAR_CHANGE of VAR and ARG, named `VAR:Q1` after the key that sets them (the state keys of VM 3.10). A VAR that leaves the text of its value as it was gives no row.
    - Architecture 9 lists STATE_CHANGE alone for trace and annotate; its row could name VAR_CHANGE as well (not edited here).
  - The other variables are named as P1-05's STATE_CHANGE names them, by resource id (`SPINDLE:S2`, `TOOL:H1`). Their values are written as P1-05 writes them: empty when unknown or none, `0 (MACHINE)` for a position known only in the machine frame.
  - The first walk of a block (VM 6) is tracked from the events:
    - PROGRAM_BEGIN and SUB_BEGIN open a walk; PROGRAM_END and SUB_END close it.
    - A walk is the first of its subprogram when no SUB_BEGIN of that name came before.
    - A block is found by its pc, `After.Flow.Pc`, the index in the expanded program. The pc also finds a generated block whose `@RESTORE` the VM rewrote.
    - A block whose first walk changed nothing gets no values, even when a later walk changes something.
- Code-structure choices (no behaviour question):
  - The trace and annotate listeners live in `Ncx.Cli/History/`. The phase file names only files of `Ncx.Cli`, and architecture 3 lists the analytics of `Ncx.Analytics` without them.
  - Loading a machine by path is one method, `Pipeline.LoadMachine`, with a plain TODO for P2-04 part two: the name in `machines/`, `ncx.toml`, and the catalog file of `[cycles] catalog` beneath the `[[cycle]]` entries. Until then the cycle catalog of a machine given by path is the built-in drilling family with the machine's own `[[cycle]]` entries.
- Open questions, each marked `TODO(question)` in the code:
  - The form of the trace table (`History/TraceTable.cs`): a header row, text as the default format, CSV quoting (RFC 4180, LF line endings), and the block column of a generated block (`5 generated by [coolant] THROUGH (requires)`).
  - The form of the annotate comment (`History/AnnotatedText.cs`):
    - several variables separated by ", ";
    - an unknown value written `?`;
    - a generated block written as a comment line with its origin, where the VM executed it;
    - a block rewritten in place written as such a comment line before the block of the file.
  - What trace and annotate write when an ERROR stops the run (`Commands/TraceCommand.cs`, `AnnotateCommand.cs`): the output of the blocks executed before the ERROR, and nothing when the file, the machine file, the parser or the expander stopped the run before its first block.
  - Wave-1 question #80 is cited, not asked again: annotate keeps a byte order mark as format does.
- Document fix F28: architecture 10 is the one CLI table. It now lists `format` with `--output`, `check` with `--skip-blocks` and `--expand-cycles`, `trace` with `--format`, and `annotate`; the pipelines include the expander; a sentence names the options that `check`, `trace` and `annotate` share; and a task that adds a command or an option adds it there in the same commit.
- Shared files changed:
  - `src/Ncx.Cli/Program.cs`: three commands registered.
  - `Commands/FormatCommand.cs`: `--strict`, and reading through `InputFile`.
  - `ExitCodes.cs`: `OfRun`.
  - `DiagnosticCodes.cs`: the CLI100-CLI199 range line.
  - The READMEs of `src/Ncx.Cli/`, its `Commands/`, `tests/Ncx.Acceptance/`, and its `Cli/` and `Expected/`.

Done when:

- The three commands run on every example: holds. `CheckCommandTests`, `TraceCommandTests` and `AnnotateCommandTests` each have a theory over the five examples, and `check` reports each example's expected file of P1-04.
- The annotate output parses and formats back to the original when the comments are stripped: holds for every example, and for the coolant clutch machine with its generated blocks. Every line of an example's annotate output begins with the original line.
- The other tests of the phase file hold:
  - `check --strict` exits 1 on PATTERN_LOOP and 0 without (`CheckStrict_PatternLoopWithItsWarning_ExitsOne`).
  - The trace of 2.5D_FRAESEN is the expected file `tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.trace.txt`, and its annotate output is `2.5D_FRAESEN.annotate.txt` (phases table, exit checklist).
- This closes M2 and phase 1: M2 holds.
  - Architecture 12: the five examples check with no ERROR without a machine file, now through `ncx check`; every validation rule has a test (P1-04); the coolant clutch rule expands and restores (P1-06).
  - The rest of the phase 1 exit checklist is outside this task: `docs/reading-the-code.md` is P0-07 part two, and the schedule and findings bookkeeping belongs to the merge step.

Gate:

- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2866 tests passing. 135 of them are in `Ncx.Acceptance`, 110 of those in `Cli/`, 84 of those new.
- `dotnet format --verify-no-changes`: clean.
- A mutation check disabled three rules in turn and restored each file afterwards. Each mutation failed between two and four tests:
  - annotate recording every walk;
  - trace dropping the variables;
  - `--strict` ignored.
