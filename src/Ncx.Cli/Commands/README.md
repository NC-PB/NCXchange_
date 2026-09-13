# Commands

One class per command of architecture 10. `FormatCommand.cs` shows the shape: `Create(output, error)` builds the `System.CommandLine` command with its argument and options, and `Run(...)` does the work and returns the exit code of D97. A new command adds its file here and one line in `../Program.cs`.

Never here: the pipeline itself. A command stays thin and calls the stages of `Ncx.Core` and the other projects (phase 0, risks).
