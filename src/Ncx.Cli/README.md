# Ncx.Cli

The command line tool `ncx` (architecture 10). `Program.cs` is the composition root (code-guidelines 5): it builds the root command with its commands by hand and runs one command line; `Commands/` holds one class per command. So far these are `ncx format <file> [--check] [--output <file>] [--strict]`, the parser and the canonical writer and nothing else (D91), and `ncx check`, `ncx trace` and `ncx annotate`, which run the virtual machine STATIC over the file (virtual machine 1, 6); `convert` and `compile` come in phase 3.

## Open first

1. `Program.cs`: `Main`, and `Run(args, output, error)`; a usage error is `CLI001` with exit code 2.
2. `Commands/FormatCommand.cs`: read the file, `Parser.Parse`, `NcxWriter.Write`, the diagnostics to the standard error (D98).
3. `Pipeline.cs`: what check, trace and annotate share: read the file and the machine file of `--machine` (the built-in default machine of D103 without it), parse, expand, run STATIC, collect the diagnostics; `PipelineRun.cs` is what a run gives its command.
4. `Commands/CheckCommand.cs`, `TraceCommand.cs`, `AnnotateCommand.cs`: the three commands on the pipeline; `History/` holds the listeners and the text of trace and annotate.
5. `ExitCodes.cs`: 0, 1 and 2 of D97, and `--strict`, under which a WARNING exits 1.

`DiagnosticCodes.cs` holds the `CLI` codes, `CLI001` to `CLI004` of format and the command line, `DiagnosticCodes.Pipeline.cs` those of check, trace and annotate, `CLI100` so far (D98). `InputFile.cs` reads the NCX file and the machine file as UTF-8 text. The tests are in `../../tests/Ncx.Acceptance/Cli/`.

## Never here

- A rule of the language, the virtual machine or a machine: a command reads its input, calls the stages of the other projects and writes the result (code-guidelines 4, dependency inversion).
- A dependency injection container or global state: `Program.cs` wires everything by hand (code-guidelines 5).
