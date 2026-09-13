# Commands

One class per command of architecture 10. `FormatCommand.cs` shows the shape: `Create(output, error)` builds the `System.CommandLine` command with its argument and options, and `Run(...)` does the work and returns the exit code of D97. A new command adds its file here and one line in `../Program.cs`.

`CheckCommand.cs`, `TraceCommand.cs` and `AnnotateCommand.cs` share the file argument and the options of `RunOptions.cs`: `--machine` (D103), `--strict` (D97), `--skip-blocks none|all|1,3` (D53) and `--expand-cycles` (D37); `trace` adds `--format text|csv`. Each hands its settings to `../Pipeline.cs` with the listeners it needs and writes what the run gives it.

Never here: the pipeline itself. A command stays thin and calls the stages of `Ncx.Core` and the other projects (phase 0, risks).
