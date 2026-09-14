# Ncx.Compilers

A compiler writes an `NcxProgram` for one machine (architecture 8): it runs the virtual machine in STATIC mode with itself subscribed and writes one or more lines per block from the state before and after it and the templates of the machine. One folder per controller family, `Fanuc/` (P3-06), `Heidenhain/` (P3-04), `Siemens/` (P5-02); here the framework they share (P3-03), the number formatting of `[format]`, and `IBlockWriter`, the compiler-side plugin interface (D106).

## Open first

1. `ICompiler.cs`, `CompileOptions.cs`, `CompileResult.cs`: what a compiler takes and gives. `CompilerRegistry.cs`: the compilers by controller family, one registration line each in `../Ncx.Cli/Program.cs`.
2. `CompilerBase.cs`: the template method `Compile`: expand; RAW and native cycles of another controller are an ERROR (`CompilerBase.Native.cs`, D94); run STATIC with `StepRecorder` subscribed, every block with its Before and After as a `BlockStep`, and the listeners of the options after it (the plugins', virtual machine 7); write every step (`CompilerBase.Walks.cs`: a program into its lines, each `SUB` once from an unknown target state, D99, and BLOCK_WRITE to the block writers); lay the files out (`CompilerBase.Layout.cs`: `one_file` or `file_per_program`, D48, and the warning block of D10). A family writes `WriteBlock(block, before, after)` with `Line(...)`, and `WriteHeader` and `WriteFooter` where it needs them.
3. `CompilerBase.Templates.cs`: the templates of the machine (`WriteTemplate`, `WriteProgramEnd`, `WriteSubEnd`); `CompilerBase.Tools.cs`: the tool change and the preload with `{next}`, `{kind}`, `{b}`, `{c}` and `auto_preload` from `LookAhead.cs` (D52, virtual machine 3.5).
4. The parts a family uses: `TargetState.cs` (a modal word written only on change), `NumberFormatter.cs` (decimals, trailing zeros, the decimal separator), `CommentCharset.cs` (the umlauts), `ChainWriter.cs` (the frame chain in program order, D31); `OutputBuffer.cs` numbers the lines and ends them per `[format]`.

`DiagnosticCodes.cs` holds the `CMP` codes of the framework, `CMP001` to `CMP099`; a family takes a range of its own. The tests are in `../../tests/Ncx.Compilers.Tests/`.

## Never here

- Deciding what a word means (the virtual machine's, architecture 8); a class per machine or per builder (they differ in templates and tables, architecture 2 rule 2).
- Generics of our own, LINQ chains of more than two calls, files longer than one screen (code-guidelines 10.2).
