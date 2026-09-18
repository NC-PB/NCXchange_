# Fanuc

The Fanuc compiler (P3-06): an NCX program in, a Fanuc or ISO program out, per section 10 of `../../../docs/controllers/fanuc.md` and the Fanuc column of `../../../docs/spec/controller-mapping.md`. It builds on the framework of `..` (`CompilerBase`: the STATIC run, the target state, the number format, the tool change of `[tool_change]`, each `SUB` once, the file layout).

## Open first

1. `FanucCompiler.cs`: the class the CLI registers (`../../Ncx.Cli/Program.cs`), and `WriteBlock`, which writes a block concern by concern in the order the control executes it.
2. `FanucBlock.cs` (one block being written: its state, the target state, the main line, the words written) and `FanucLine.cs` (the order of the words in a Fanuc block); `FanucCodes.cs` holds the G codes by modal group and G-code system; `FanucPaths.cs` (with `FanucModalValue.cs`) compares the modal values F, the feed mode, the plane, D, S, G96 or G97 and the cycle over the paths that meet at a label a jump reaches and after a skipped block, `FanucPaths.Steps.cs` looks ahead in the steps for a jump back that the label is written before, and `FanucMeeting.cs` keeps what a label and its jumps tell each other.
3. The concerns, one file each: `FanucProgramFrame.cs` (`%`, `O` with the name, the start block of `[format] header`, `program_end`, the `O` subprograms, `M99`), `FanucMotion.cs` and `FanucArcs.cs` (G0 to G3, G90 or G91 by the words, I J K or R, sweeps split into turns, G53, G4), `FanucToolWords.cs` (G43 H, D with G41, M3 with S by the S binding rule, G96, G97), `FanucFunctions.cs` (the M codes of the machine's tables, MFUNC, M0, M1), `FanucFrames.cs` (G54, the chain with G52, G68, G51.1 and G68.2, G28 and G30, G92 or G50, G43.4, G12.1, G7.1, G5.1), `FanucCycles.cs` (G81 to G89 with R and Z from the absolute planes, G98 and G99, G80, the lathe codes), `FanucFlow.cs` and `FanucExpressions.cs` (GOTO, IF [ ] GOTO, # variables, M98 P L, G65), `FanucSync.cs` (the wait marks of `[sync]`).

`DiagnosticCodes.Fanuc.cs` holds the codes `CMP300` to `CMP499`. The tests are in `../../../tests/Ncx.Compilers.Tests/Fanuc/`, the round trips of the example sources in `../../../tests/Ncx.Acceptance/Examples/FanucCompilerTests.cs`.

## Never here

- Deciding what a word means (the virtual machine's); a class per machine or per builder (they differ in the templates of their machine files).
- A word dropped silently: a word no concern writes is the ERROR `CMP308`.
