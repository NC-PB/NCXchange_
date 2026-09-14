# Commands

One class per command of architecture 10. `FormatCommand.cs` shows the shape: `Create(output, error)` builds the `System.CommandLine` command with its argument and options, and `Run(...)` does the work and returns the exit code of D97. A new command adds its file here and one line in `../Program.cs`.

`CheckCommand.cs`, `TraceCommand.cs` and `AnnotateCommand.cs` share the file argument and the options of `RunOptions.cs`: `--machine` (D103), `--strict` (D97), `--skip-blocks none|all|1,3` (D53) and `--expand-cycles` (D37); `trace` adds `--format text|csv`, and `--interpreted` with `--vars <toml>` for an INTERPRETED run (virtual machine 3.6). Each hands its settings to `../Pipeline.cs` with the listeners it needs and writes what the run gives it.

`ConvertCommand.cs` takes a controller program, `--machine` (required unless `ncx.toml` names the machine, D77), `--output <file>` and `--strict`; `Create(readers, output, error)` gets the readers that `../Program.cs` registers, and `Run(...)` reads the program with the reader of the machine's controller, writes it with `NcxWriter` and checks it STATIC.

`AnalyzeCommand.cs` takes the file, `--machine`, `--vars`, `--from` and `--to` (D67), `--analytic tools,runtime`, `--static`, `--format text|csv`, `--skip-blocks` and `--strict` (`AnalyzeSettings.cs` holds what goes beyond the run); `Create(analytics, output, error)` gets the analytics that `../Program.cs` registers, and `Run(...)` hands `../Pipeline.cs` a factory that makes them for the machine of the run, then writes their reports.

`CompileCommand.cs` takes an NCX file, `--machine` (required unless `ncx.toml` names the machine, D77), `--output <folder>` and `--strict`; `Create(compilers, error)` gets the compilers that `../Program.cs` registers, and `Run(...)` parses the file, hands it with the tool table of the machine (D10) to the compiler of the machine's controller and writes the files it gives into `out/<machine>/`, or into the folder of `--output`.

Never here: the pipeline itself. A command stays thin and calls the stages of `Ncx.Core` and the other projects (phase 0, risks).
