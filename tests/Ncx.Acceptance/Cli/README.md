# CLI tests

`ncx format` through `Program.Run`, with the exit codes of D97 and the diagnostics on the standard error in the form of D98, each test in a temporary folder of its own (`FormatCommandTests`); `DiagnosticCodesTests` checks the `CLI` codes. A new command of `src/Ncx.Cli/Commands/` gets its test file here.
