# Ncx.Cli

The command line tool `ncx` (architecture 10). `Program.cs` is the composition root (code-guidelines 5): it builds the root command with its commands by hand and runs one command line; `Commands/` holds one class per command. So far these are `ncx format <file> [--check] [--output <file>] [--strict]`, the parser and the canonical writer and nothing else (D91), and `ncx check`, `ncx trace` and `ncx annotate`, which run the virtual machine STATIC over the file (virtual machine 1, 6), `trace --interpreted` INTERPRETED with the start values of the vars file (virtual machine 2.7, 3.6), and `ncx convert <file> --machine <toml> [--output <file>] [--strict]`, which reads a controller program with the reader of the machine's controller, writes it with the canonical writer and checks it STATIC (architecture 7, 10; D77); `compile` comes in phase 3.

## Open first

1. `Program.cs`: `Main`, and `Run(args, output, error)`; a usage error is `CLI001` with exit code 2.
2. `Commands/FormatCommand.cs`: read the file, `Parser.Parse`, `NcxWriter.Write`, the diagnostics to the standard error (D98).
3. `Pipeline.cs`: what check, trace and annotate share: read the file and the machine of the run, parse, expand, run STATIC, collect the diagnostics; `PipelineRun.cs` is what a run gives its command. `RunMachine.cs` is the machine of the run: `ncx.toml` of the working directory, the machine file of `--machine` (else the one `ncx.toml` names, else the built-in default machine of D103) and the cycle catalog it names; `ProjectFolders.cs` finds a machine by name in the machine folders and a catalog in the cycle folders (P2-04).
4. `Commands/CheckCommand.cs`, `TraceCommand.cs`, `AnnotateCommand.cs`: the three commands on the pipeline; `History/` holds the listeners and the text of trace and annotate.
5. `Commands/ConvertCommand.cs`: the machine of the run through `RunMachine.cs` (never the default machine, D77), the reader that `Program.Readers()` registers for the machine's controller, `NcxWriter.Write`, then the expander and the virtual machine STATIC over the program the reader produced.
6. `ExitCodes.cs`: 0, 1 and 2 of D97, and `--strict`, under which a WARNING exits 1.

`DiagnosticCodes.cs` holds the `CLI` codes, `CLI001` to `CLI004` of format and the command line, `DiagnosticCodes.Pipeline.cs` those of check, trace and annotate, `CLI100` for the machine file and `CLI101` for the vars file of `trace --interpreted`, `DiagnosticCodes.Project.cs` those of the machine of a run, `CLI200` to `CLI202`, and `DiagnosticCodes.Convert.cs` those of convert, `CLI250` and `CLI251` (D98). `InputFile.cs` reads the NCX file, the controller program, `ncx.toml`, the machine file, the cycle catalog and the vars file as UTF-8 text; `OutputFile.cs` writes the text of `--output`. The build copies `../../machines/` and `../../cycles/` next to the tool (`Ncx.Cli.csproj`). The tests are in `../../tests/Ncx.Acceptance/Cli/`.

## Never here

- A rule of the language, the virtual machine or a machine: a command reads its input, calls the stages of the other projects and writes the result (code-guidelines 4, dependency inversion).
- A dependency injection container or global state: `Program.cs` wires everything by hand (code-guidelines 5).
