# Ncx.Cli

The command line tool `ncx` (architecture 10). `Program.cs` is the composition root (code-guidelines 5): it builds the root command with its commands by hand and runs one command line; `Commands/` holds one class per command. So far that is `ncx format <file> [--check] [--output <file>]`, the parser and the canonical writer and nothing else (D91); `check`, `trace` and `annotate` come with P1-07, `convert` and `compile` in phase 3.

## Open first

1. `Program.cs`: `Main`, and `Run(args, output, error)`; a usage error is `CLI001` with exit code 2.
2. `Commands/FormatCommand.cs`: read the file, `Parser.Parse`, `NcxWriter.Write`, the diagnostics to the standard error (D98).
3. `ExitCodes.cs`: 0, 1 and 2 of D97.

`DiagnosticCodes.cs` holds the `CLI` codes, `CLI001` to `CLI004` so far (D98). The tests are in `../../tests/Ncx.Acceptance/Cli/`.

## Never here

- A rule of the language, the virtual machine or a machine: a command reads its input, calls the stages of the other projects and writes the result (code-guidelines 4, dependency inversion).
- A dependency injection container or global state: `Program.cs` wires everything by hand (code-guidelines 5).
